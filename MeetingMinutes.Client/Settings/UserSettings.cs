using System.IO;
using System.Text.Json;

namespace MeetingMinutes.Settings;

public class UserSettingsData
{
    public int SettingsVersion { get; set; } = 0;
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;
    public string OllamaModel { get; set; } = "gemma3:12b";
    public string TranscriptionModel { get; set; } = "canary";
    public string TranscriptionLanguage { get; set; } = "cs";

    public UserSettingsData Clone() => (UserSettingsData)MemberwiseClone();

    public const string DefaultSystemPrompt =
        """
        Jsi asistent pro shrnutí přepisů schůzek. Odpovídej výhradně česky. Vytvoř strukturované shrnutí v Markdownu přesně podle šablony níže. Používej pouze informace z přepisu — nic nevymýšlej. Pokud požadovaná informace v přepisu chybí, napiš "není uvedeno".

        # Souhrn schůzky

        Uveď 2–3 věty vystihující hlavní téma a výsledek schůzky.

        ## Účastníci

        - Uváděj POUZE osoby, které v přepisu skutečně vystupují jako mluvčí (ne osoby, které jsou pouze zmiňovány).
        - Pokud jsou mluvčí označeni anonymními štítky (SPEAKER_XX), neuváděj je.

        ## Témata

        Pro každé projednávané téma použij následující strukturu:

        ### Téma N: {název tématu}

        **Shrnutí:** 1–2 věty shrnující dané téma.

        **Klíčové body:**
        - bod

        **Rozhodnutí:**
        - rozhodnutí (pokud žádné nepadlo, napiš "není uvedeno")

        ## Otevřené otázky

        - otázka (pokud žádné nejsou, napiš "není uvedeno")

        ---

        PRAVIDLA:
        - odpovídej pouze česky
        - používej pouze informace z přepisu, nic si nevymýšlej
        - pokud informace chybí, napiš "není uvedeno"
        - nerozšiřuj význam výroků — zachovej jejich původní smysl
        - neinterpretuj ani nepřepisuj tvrzení vlastními slovy, pokud to mění význam
        - nevyvozuj závěry, které nejsou explicitně řečeny
        - NEZOBECŇUJ — používej formulace odpovídající textu (např. „uvádí", „říká", „navrhuje")
        - neuváděj informace, které nelze přímo dohledat v textu
        """;
}

public static class UserSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MeetingMinutes", "user-settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private const int CurrentSettingsVersion = 1;

    private const string LegacyDefaultSystemPromptV0 =
        """
        Jsi asistent pro shrnutí přepisů schůzek.

        VÝSTUP MUSÍ MÍT TUTO STRUKTURU:

        1) Témata:
        - ...

        2) Účastníci:
        - ...

        3) Konkrétní návrhy / opatření:
        - ...

        4) Rozhodnutí:
        - ...

        5) Úkoly:
        - ...

        PRAVIDLA:
        - odpovídej pouze česky
        - používej pouze informace z přepisu
        - nic si nevymýšlej
        - pokud informace chybí napiš "není uvedeno"
        - pokud v přepisu nejsou rozhodnutí nebo úkoly, napiš to výslovně
        - používej pouze jména, která jsou v přepisu

        DŮLEŽITÉ:
        - do sekce "Účastníci" uváděj POUZE osoby, které v přepisu skutečně vystupují jako mluvčí (ne osoby, které jsou pouze zmiňovány)
        - nerozšiřuj význam výroků – zachovej jejich původní smysl
        - neinterpretuj ani nepřepisuj tvrzení vlastními slovy, pokud to mění význam
        - nevyvozuj závěry, které nejsou explicitně řečeny
        - NEZOBECŇUJ – používej formulace odpovídající textu (např. „uvádí", „říká", „navrhuje")
        - pokud si nejsi jistý, napiš "není uvedeno"
        - neuváděj informace, které nelze přímo dohledat v textu
        - buď stručný a věcný
        """;

    public static UserSettingsData Load()
    {
        if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak"))
        {
            var fresh = new UserSettingsData { SettingsVersion = CurrentSettingsVersion };
            Save(fresh);
            return fresh;
        }

        UserSettingsData? data = null;

        if (File.Exists(FilePath))
        {
            try
            {
                data = JsonSerializer.Deserialize<UserSettingsData>(File.ReadAllText(FilePath), JsonOpts);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserSettings] Failed to load main file: {ex.Message}");
                data = null;
            }
        }

        if (data == null && File.Exists(FilePath + ".bak"))
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[UserSettings] Falling back to .bak file.");
                data = JsonSerializer.Deserialize<UserSettingsData>(File.ReadAllText(FilePath + ".bak"), JsonOpts);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserSettings] Failed to load .bak file: {ex.Message}");
                data = null;
            }
        }

        data ??= new UserSettingsData();

        var migrated = false;
        while (data.SettingsVersion < CurrentSettingsVersion)
        {
            data = data.SettingsVersion switch
            {
                0 => Migrate_v0_to_v1(data),
                _ => throw new InvalidOperationException(
                         $"Unknown settings version {data.SettingsVersion}")
            };
            migrated = true;
        }
        if (migrated) Save(data);

        return data;
    }

    public static void Save(UserSettingsData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(data, JsonOpts);
        var tmpPath = FilePath + ".tmp";
        File.WriteAllText(tmpPath, json);
        if (File.Exists(FilePath))
            File.Replace(tmpPath, FilePath, FilePath + ".bak");
        else
            File.Move(tmpPath, FilePath);
    }

    private static UserSettingsData Migrate_v0_to_v1(UserSettingsData data) =>
        new()
        {
            SettingsVersion = 1,
            SystemPrompt = data.SystemPrompt == LegacyDefaultSystemPromptV0
                ? UserSettingsData.DefaultSystemPrompt
                : data.SystemPrompt,
            OllamaModel = data.OllamaModel,
            TranscriptionModel = data.TranscriptionModel,
            TranscriptionLanguage = data.TranscriptionLanguage,
        };
}
