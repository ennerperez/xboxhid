using Serilog;

namespace XboxHid;

public sealed class DiagnosticFileLogger : IDisposable
{
    private readonly Serilog.Core.Logger logger;

    private DiagnosticFileLogger(Serilog.Core.Logger logger, string? logDirectory)
    {
        this.logger = logger;
        LogDirectory = logDirectory;
    }

    public ILogger Logger => logger;

    public string? LogDirectory { get; }

    public static DiagnosticFileLogger Create(bool enabled, string executionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionDirectory);

        if (!enabled)
        {
            return new(new LoggerConfiguration().CreateLogger(), null);
        }

        string logDirectory = Path.Combine(executionDirectory, "logs");
        string logPath = Path.Combine(logDirectory, "xboxhid-.log");
        Serilog.Core.Logger logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10_000_000,
                rollOnFileSizeLimit: true,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return new(logger, logDirectory);
    }

    public void Dispose()
    {
        logger.Information("Diagnostic logger stopped");
        logger.Dispose();
    }
}
