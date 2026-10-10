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

            // 先写临时文件再替换：写到一半崩溃不会留下半个（或空的）配置文件
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, FilePath, overwrite: true);

            Log.Info("Settings saved to " + FilePath);
        }
        catch (Exception ex)
        {
            Log.Error("Settings save failed", ex);
        }
    }
}
