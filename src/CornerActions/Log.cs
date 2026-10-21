namespace CornerActions;

/// <summary>
/// Appends English lines to a size-capped log file. Logging never throws into the
/// caller: a tray utility must not die because its log directory is read-only.
/// </summary>
internal static class Log
{
    private const long MaxBytes = 512 * 1024;

    private static readonly object Gate = new();

    private static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CornerActions",
        "corner-actions.log");

    internal static bool EchoToConsole { get; set; }

    internal static void Info(string message) => Write("INFO", message);

    internal static void Warn(string message) => Write("WARN", message);

    internal static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} {level} {message}";

        lock (Gate)
        {
            try
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                {
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                }

                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Nothing useful left to do; the file system refused the write.
            }
        }

        if (EchoToConsole)
        {
            Console.WriteLine(line);
        }
    }
}
