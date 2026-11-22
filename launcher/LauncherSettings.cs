using System.Text.Json;

namespace SanAndreasHorizonLauncher;

internal sealed class LauncherSettings
{
    public string GameFolder { get; set; } = "";
    public string ClientExecutable { get; set; } = "";
    public string ClientType { get; set; } = "open.mp";
    public string Nickname { get; set; } = "Jugador_Horizon";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 7777;

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SanAndreasHorizonLauncher");

    public static string SettingsPath =>
        Path.Combine(SettingsDirectory, "settings.json");

    public static LauncherSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsPath))
                    ?? new LauncherSettings();
        }
        catch
        {
            // A damaged settings file should not prevent the launcher from opening.
        }

        return new LauncherSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
