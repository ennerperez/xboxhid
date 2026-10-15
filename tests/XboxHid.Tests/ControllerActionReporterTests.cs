namespace XboxHid.Tests;

public sealed class ControllerActionReporterTests
{
    [Fact]
    public void Report_WritesChangedAxisAndButton()
    {
        StringWriter output = new();
        ControllerActionReporter reporter = new(output);
        reporter.Report(GamepadSnapshot.Empty);

        float[] axes = new float[Enum.GetValues<GamepadAxis>().Length];
        bool[] buttons = new bool[Enum.GetValues<GamepadButton>().Length];
        axes[(int)GamepadAxis.RightX] = 0.5f;
        buttons[(int)GamepadButton.A] = true;

        reporter.Report(new(axes, buttons));

        string text = output.ToString();
        Assert.Contains("Eje RightX: +0.500", text);
        Assert.Contains("Botón A: presionado", text);
    }

    [Fact]
    public void Report_SuppressesSmallAxisNoise()
    {
        StringWriter output = new();
        ControllerActionReporter reporter = new(output, axisThreshold: 0.02f);
        reporter.Report(GamepadSnapshot.Empty);

        float[] axes = new float[Enum.GetValues<GamepadAxis>().Length];
        axes[(int)GamepadAxis.LeftX] = 0.01f;
        reporter.Report(new(axes, new bool[Enum.GetValues<GamepadButton>().Length]));

        Assert.DoesNotContain("Eje LeftX", output.ToString());
    }

    [Fact]
    public void Report_AccumulatesSlowAxisChangesFromLastReportedValue()
    {
        StringWriter output = new();
        ControllerActionReporter reporter = new(output, axisThreshold: 0.02f);
        reporter.Report(GamepadSnapshot.Empty);

        reporter.Report(SnapshotWithLeftX(0.01f));
        reporter.Report(SnapshotWithLeftX(0.019f));
        reporter.Report(SnapshotWithLeftX(0.029f));

        Assert.Contains("Eje LeftX: +0.029", output.ToString());
    }

    [Fact]
    public void Report_RateLimitsConsoleAxisOutput()
    {
        StringWriter output = new();
        ManualTimeProvider time = new();
        ControllerActionReporter reporter = new(output, timeProvider: time);
        reporter.Report(GamepadSnapshot.Empty);

        reporter.Report(SnapshotWithLeftX(0.1f));
        reporter.Report(SnapshotWithLeftX(0.2f));
        time.Advance(TimeSpan.FromMilliseconds(200));
        reporter.Report(SnapshotWithLeftX(0.3f));

        string[] axisLines = output.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("Eje LeftX", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, axisLines.Length);
        Assert.Contains("+0.100", axisLines[0]);
        Assert.Contains("+0.300", axisLines[1]);
    }

    private static GamepadSnapshot SnapshotWithLeftX(float value)
    {
        float[] axes = new float[Enum.GetValues<GamepadAxis>().Length];
        axes[(int)GamepadAxis.LeftX] = value;
        return new(axes, new bool[Enum.GetValues<GamepadButton>().Length]);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => 1_000;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan elapsed) => timestamp += (long)elapsed.TotalMilliseconds;
    }
}
