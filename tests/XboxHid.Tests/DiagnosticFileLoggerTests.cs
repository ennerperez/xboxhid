namespace XboxHid.Tests;

public sealed class DiagnosticFileLoggerTests
{
    [Fact]
    public void Create_WhenDisabled_DoesNotCreateLogDirectory()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            using (DiagnosticFileLogger diagnostics = DiagnosticFileLogger.Create(false, directory))
            {
                diagnostics.Logger.Information("This message must not create a file");
            }

            Assert.False(Directory.Exists(Path.Combine(directory, "logs")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Create_WhenEnabled_WritesLogFile()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            using (DiagnosticFileLogger diagnostics = DiagnosticFileLogger.Create(true, directory))
            {
                diagnostics.Logger.Information("Diagnostic marker {Marker}", 42);
            }

            string logFile = Assert.Single(Directory.GetFiles(Path.Combine(directory, "logs"), "*.log"));
            Assert.Contains("Diagnostic marker 42", File.ReadAllText(logFile));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xboxhid-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
