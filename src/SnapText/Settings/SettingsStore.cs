using System.IO;
using System.Text.Json;

namespace SnapText.Settings;

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FilePath { get; } = Path.Combine(Log.RootDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    Log.Info("Settings loaded from " + FilePath);
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Settings load failed", ex);
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Log.RootDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
            Log.Info("Settings saved to " + FilePath);
        }
        catch (Exception ex)
        {
            Log.Error("Settings save failed", ex);
        }
    }
}
