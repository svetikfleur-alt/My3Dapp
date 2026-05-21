using System.Text.Json;

namespace My3DApp.AvaloniaApp.Services;

public sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "My3DApp", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public List<string> RecentFiles { get; set; } = [];

    public bool ShowStartupPageOnLaunch { get; set; } = true;

    public string PreferredAssistantProvider { get; set; } = "DeepSeek";

    public string PreferredAssistantModel { get; set; } = "DeepSeek Chat";

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { }
    }

    public void AddRecentFile(string path)
    {
        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 10)
            RecentFiles.RemoveRange(10, RecentFiles.Count - 10);
        Save();
    }
}
