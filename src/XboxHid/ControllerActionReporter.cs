using Serilog;

namespace XboxHid;

public sealed class ControllerActionReporter
{
    private readonly TextWriter output;
    private readonly float axisThreshold;
    private readonly ILogger? logger;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan consoleAxisInterval;
    private float[]? lastDetailedAxes;
    private float[]? lastConsoleAxes;
    private long[]? lastConsoleTimestamps;
    private bool[]? hasConsoleReport;
    private bool[]? previousButtons;

    public ControllerActionReporter(
        TextWriter output,
        float axisThreshold = 0.02f,
        ILogger? logger = null,
        TimeProvider? timeProvider = null,
        TimeSpan? consoleAxisInterval = null)
    {
        this.output = output;
        this.axisThreshold = axisThreshold;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.consoleAxisInterval = consoleAxisInterval ?? TimeSpan.FromMilliseconds(200);
    }

    public void Report(GamepadSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (lastDetailedAxes is null || previousButtons is null)
        {
            lastDetailedAxes = Enum.GetValues<GamepadAxis>()
                .Select(current.GetAxis)
                .ToArray();
            lastConsoleAxes = (float[])lastDetailedAxes.Clone();
            lastConsoleTimestamps = new long[lastDetailedAxes.Length];
            hasConsoleReport = new bool[lastDetailedAxes.Length];
            previousButtons = Enum.GetValues<GamepadButton>()
                .Select(current.IsPressed)
                .ToArray();
            output.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] Estado inicial recibido.");
            logger?.Debug("Initial gamepad state received");
            return;
        }

        float[] consoleAxes = lastConsoleAxes!;
        long[] consoleTimestamps = lastConsoleTimestamps!;
        bool[] consoleReports = hasConsoleReport!;

        foreach (GamepadAxis axis in Enum.GetValues<GamepadAxis>())
        {
            float value = current.GetAxis(axis);
            int axisIndex = (int)axis;
            if (Math.Abs(value - lastDetailedAxes[axisIndex]) >= axisThreshold)
            {
                logger?.Debug("Gamepad axis {Axis} changed to {Value}", axis, value);
                lastDetailedAxes[axisIndex] = value;
            }

            long now = timeProvider.GetTimestamp();
            bool intervalElapsed = !consoleReports[axisIndex] ||
                timeProvider.GetElapsedTime(consoleTimestamps[axisIndex], now) >= consoleAxisInterval;
            if (Math.Abs(value - consoleAxes[axisIndex]) >= axisThreshold && intervalElapsed)
            {
                output.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] Eje {axis}: {value:+0.000;-0.000;0.000}");
                consoleAxes[axisIndex] = value;
                consoleTimestamps[axisIndex] = now;
                consoleReports[axisIndex] = true;
            }
        }

        foreach (GamepadButton button in Enum.GetValues<GamepadButton>())
        {
            bool pressed = current.IsPressed(button);
            if (pressed != previousButtons[(int)button])
            {
                output.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] Botón {button}: {(pressed ? "presionado" : "liberado")}");
                logger?.Debug("Gamepad button {Button} changed to {Pressed}", button, pressed);
            }

            previousButtons[(int)button] = pressed;
        }
    }

    public void Reset()
    {
        lastDetailedAxes = null;
        lastConsoleAxes = null;
        lastConsoleTimestamps = null;
        hasConsoleReport = null;
        previousButtons = null;
    }
}
