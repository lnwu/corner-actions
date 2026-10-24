using System.Text.Json;
using System.Text.Json.Serialization;

namespace CornerActions;

/// <summary>
/// User-editable settings, stored as JSON. Only the knobs a user can reasonably
/// reason about are exposed; timing that would only invite misconfiguration
/// (poll interval, cooldown, hysteresis) is fixed in code.
/// </summary>
internal sealed class AppConfig
{
    internal const int DefaultCornerSizeDips = 4;
    internal const int DefaultDwellMilliseconds = 300;

    internal const int MinCornerSizeDips = 1;
    internal const int MaxCornerSizeDips = 64;
    internal const int MaxDwellMilliseconds = 5000;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string Language { get; set; } = "auto";

    public bool Enabled { get; set; } = true;

    /// <summary>"primary" or "all".</summary>
    public string Monitors { get; set; } = "primary";

    public int CornerSizeDips { get; set; } = DefaultCornerSizeDips;

    public int DwellMilliseconds { get; set; } = DefaultDwellMilliseconds;

    public bool TopLeft { get; set; } = true;

    public bool TopRight { get; set; } = true;

    [JsonIgnore]
    internal MonitorScope Scope => string.Equals(Monitors, "all", StringComparison.OrdinalIgnoreCase)
        ? MonitorScope.All
        : MonitorScope.Primary;

    [JsonIgnore]
    internal static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CornerActions",
        "config.json");

    /// <summary>
    /// True when these are stand-in defaults for a file that could not be read.
    /// <see cref="Save"/> then refuses to write while that file still exists, so a
    /// menu click cannot replace the user's half-edited file with defaults.
    /// </summary>
    [JsonIgnore]
    internal bool LoadFailed { get; private init; }

    /// <summary>
    /// Loads the configuration. A missing file is created with defaults; a file
    /// that cannot be parsed is reported and left untouched, and defaults are
    /// used for this run so the app still starts.
    /// </summary>
    internal static AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var defaults = new AppConfig();
                defaults.Save();
                Log.Info($"config created path=\"{FilePath}\"");
                return defaults;
            }

            var json = File.ReadAllText(FilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions) ?? new AppConfig();
            return config.Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Error($"config load failed path=\"{FilePath}\" error=\"{ex.GetType().Name}: {ex.Message}\"");
            return new AppConfig { LoadFailed = true };
        }
    }

    internal void Save()
    {
        if (LoadFailed && File.Exists(FilePath))
        {
            Log.Warn($"config save skipped reason=load-failed path=\"{FilePath}\"");
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(Normalize(), SerializerOptions);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"config save failed path=\"{FilePath}\" error=\"{ex.GetType().Name}: {ex.Message}\"");
        }
    }

    /// <summary>Clamps every value into a range the rest of the code can trust.</summary>
    internal AppConfig Normalize()
    {
        Language = Language?.Trim().ToLowerInvariant() switch
        {
            "en" => "en",
            "zh" => "zh",
            _ => "auto",
        };

        Monitors = string.Equals(Monitors, "all", StringComparison.OrdinalIgnoreCase) ? "all" : "primary";

        CornerSizeDips = Math.Clamp(CornerSizeDips, MinCornerSizeDips, MaxCornerSizeDips);
        DwellMilliseconds = Math.Clamp(DwellMilliseconds, 0, MaxDwellMilliseconds);
        return this;
    }
}
