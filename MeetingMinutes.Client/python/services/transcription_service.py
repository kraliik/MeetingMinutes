import os
import sys
import contextlib
import tempfile
from pathlib import Path

import librosa
import soundfile as sf
import nemo.collections.asr as nemo_asr

from utils import progress, get_device
from services.diarization_service import DiarizationService


SAMPLE_RATE = 16000
MIN_DUR = 0.15
MERGE_GAP_S = 1.5


def _merge_adjacent_turns(
    turns: list[tuple[float, float, str]],
    max_gap: float = MERGE_GAP_S,
) -> list[tuple[float, float, str]]:
    coalesced: list[tuple[float, float, str]] = []
    for s, e, spk in turns:
        if coalesced:
            ps, pe, pspk = coalesced[-1]
            if pspk == spk and s >= pe and (s - pe) < max_gap:
                coalesced[-1] = (ps, e, pspk)
                continue
        coalesced.append((s, e, spk))
    return coalesced


_model_cache: dict[str, nemo_asr.models.ASRModel] = {}


def _models_root() -> Path:
    return Path(__file__).parent.parent / "Models"


_LOCAL_NEMO = _models_root() / "canary-1b-v2.nemo"
_PARAKEET_NEMO = _models_root() / "parakeet-tdt-0.6b-v3.nemo"


def _load_nemo_model(name: str, path: Path) -> nemo_asr.models.ASRModel:
    if name not in _model_cache:
        if not path.exists():
            raise FileNotFoundError(f"{name} model nenalezen: {path}")
        device = get_device()
        progress(f"Načítám {name} model z lokálního souboru ({path.name})...")
        with contextlib.redirect_stdout(sys.stderr):
            model = nemo_asr.models.ASRModel.restore_from(str(path))
        if device == "cuda":
            model = model.cuda()
        model.eval()
        _model_cache[name] = model
    return _model_cache[name]


def _load_canary_model() -> nemo_asr.models.ASRModel:
    return _load_nemo_model("Canary", _LOCAL_NEMO)


def _load_parakeet_model() -> nemo_asr.models.ASRModel:
    return _load_nemo_model("Parakeet", _PARAKEET_NEMO)


def _transcribe_segments(
    audio_path: str,
    diarizer: DiarizationService,
    model: nemo_asr.models.ASRModel,
    model_name: str,
    transcribe_kwargs: dict,
) -> list[dict]:
    turns = diarizer.get_speaker_turns(audio_path)
    if not turns:
        return []

    y_full, sr_native = sf.read(audio_path, dtype="float32", always_2d=False)
    if y_full.ndim > 1:
        y_full = y_full.mean(axis=1)
    if sr_native != SAMPLE_RATE:
        y_full = librosa.resample(y_full, orig_sr=sr_native, target_sr=SAMPLE_RATE)

    merged_short: list[tuple[float, float, str]] = []
    for s, e, spk in turns:
        if (e - s) >= MIN_DUR:
            merged_short.append((s, e, spk))
        elif merged_short:
            ps, _, pspk = merged_short[-1]
            merged_short[-1] = (ps, e, pspk)

    coalesced = _merge_adjacent_turns(merged_short)
    if not coalesced:
        return []

    temp_files: list[str] = []
    try:
        for s, e, _ in coalesced:
            tmp = tempfile.NamedTemporaryFile(suffix=".wav", delete=False)
            sf.write(tmp.name, y_full[int(s * SAMPLE_RATE):int(e * SAMPLE_RATE)], SAMPLE_RATE)
            tmp.close()
            temp_files.append(tmp.name)

        progress(f"Přepisuji {len(coalesced)} segmentů s {model_name}...")
        with contextlib.redirect_stdout(sys.stderr):
            transcriptions = model.transcribe(temp_files, **transcribe_kwargs)
    finally:
        for f in temp_files:
            try:
                os.unlink(f)
            except OSError:
                pass

    result = []
    for (s, e, spk), hyp in zip(coalesced, transcriptions):
        text = (hyp.text if hasattr(hyp, "text") else str(hyp)).strip()
        if text:
            result.append({"start": s, "end": e, "speaker": spk, "text": text})

    progress(f"Nalezeno {len(result)} segmentů.")
    return result


def _canary(audio_path: str, diarizer: DiarizationService, language: str = "cs", **_) -> list[dict]:
    return _transcribe_segments(
        audio_path, diarizer,
        model=_load_canary_model(),
        model_name="Canary",
        transcribe_kwargs={"source_lang": language, "target_lang": language},
    )


def _parakeet(audio_path: str, diarizer: DiarizationService, **_) -> list[dict]:
    return _transcribe_segments(
        audio_path, diarizer,
        model=_load_parakeet_model(),
        model_name="Parakeet",
        transcribe_kwargs={},
    )


_BACKENDS: dict[str, callable] = {
    "canary":   _canary,
    "parakeet": _parakeet,
}


class TranscriptionService:

    def __init__(self):
        self._diarizer = DiarizationService()

    def transcribe(self, audio_path: str, backend: str = "canary", **kwargs) -> list[dict]:
        if backend not in _BACKENDS:
            raise ValueError(f"Unknown backend '{backend}'. Available: {list(_BACKENDS)}")
        return _BACKENDS[backend](audio_path, self._diarizer, **kwargs)
