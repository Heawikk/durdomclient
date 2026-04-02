namespace DurdomClient.Models
{
    public enum LogLevel { Info, Warning, Error }

    public class LogEntry
    {
        public string Time { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public LogLevel Level { get; init; } = LogLevel.Info;
    }
}
