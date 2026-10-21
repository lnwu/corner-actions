namespace CornerActions;

/// <summary>
/// UI strings. The language follows the Windows display language; anything that
/// is not Chinese falls back to English.
/// </summary>
internal sealed class Strings
{
    private readonly bool _chinese;

    internal Strings(AppConfig config)
        : this(config.Language)
    {
    }

    internal Strings(string language)
    {
        _chinese = language switch
        {
            "zh" => true,
            "en" => false,
            _ => IsChineseUiLanguage(NativeMethods.GetUserDefaultUILanguage()),
        };
    }

    /// <summary>Resolved UI language, for the startup log.</summary>
    internal string Code => _chinese ? "zh" : "en";

    internal string AppName => T("Corner Actions", "角落动作");

    internal string MenuEnabled => T("Enabled", "启用");

    internal string MenuTopLeft => T("Top-left corner → display off", "左上角 → 关闭屏幕");

    internal string MenuTopRight => T("Top-right corner → display off", "右上角 → 关闭屏幕");

    internal string MenuStartWithWindows => T("Start with Windows", "开机自启");

    internal string MenuOpenConfig => T("Open configuration file…", "打开配置文件…");

    internal string MenuReloadConfig => T("Reload configuration", "重载配置");

    internal string MenuExit => T("Exit", "退出");

    internal string TooltipEnabled => T("Corner Actions — enabled", "角落动作 — 已启用");

    internal string TooltipDisabled => T("Corner Actions — disabled", "角落动作 — 已停用");

    internal string ConfigUnavailable => T(
        "The configuration file could not be opened. See the log for details.",
        "无法打开配置文件，详情见日志。");

    /// <summary>Chinese (Simplified, Traditional, Hong Kong, Singapore).</summary>
    internal static bool IsChineseUiLanguage(uint languageId) => (languageId & 0x3FF) == 0x04;

    private string T(string english, string chinese) => _chinese ? chinese : english;
}
