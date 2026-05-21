using System.IO;
using System.Text.Json;

namespace MeetingMinutes.Settings;

public class UserSettingsData
{
    public int SettingsVersion { get; set; } = 0;
    public string OllamaModel { get; set; } = "gemma3:12b";
    public string TranscriptionModel { get; set; } = "canary";
    public string TranscriptionLanguage { get; set; } = "cs";

    public UserSettingsData Clone() => (UserSettingsData)MemberwiseClone();
}

public static class UserSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MeetingMinutes", "user-settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private const int CurrentSettingsVersion = 2;

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
                1 => Migrate_v1_to_v2(data),
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

    private static UserSettingsData Migrate_v0_to_v1(UserSettingsData data)
    {
        data.SettingsVersion = 1;
        return data;
    }

    private static UserSettingsData Migrate_v1_to_v2(UserSettingsData data)
    {
        data.SettingsVersion = 2;
        return data;
    }
}
