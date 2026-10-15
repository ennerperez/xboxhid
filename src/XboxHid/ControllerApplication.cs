using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Serilog;
using XboxHid.Overlay;

namespace XboxHid;

public sealed class ControllerApplication(
    SdlGamepadSource gamepad,
    ControllerMouseMapper mapper,
    ControllerActionReporter reporter,
    TextWriter output,
    ILogger? logger = null,
    IVirtualKeyboardOverlay? overlay = null)
{
    // Timing, cancellation, SDL polling and UI dispatch are integration concerns. Pure state
    // transitions called by this loop remain unit-tested below.
    [ExcludeFromCodeCoverage]
    public void Run(CancellationToken cancellationToken)
    {
        Stopwatch timer = Stopwatch.StartNew();
        TimeSpan previousTime = timer.Elapsed;
        TimeSpan nextHeartbeat = TimeSpan.FromSeconds(5);
        bool wasConnected = false;
        bool dragDropWasActive = false;
        ControllerInterceptionState interception = new();
        long pollCount = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                GamepadPollResult poll = gamepad.Poll();
                pollCount++;
                TimeSpan now = timer.Elapsed;
                TimeSpan elapsed = now - previousTime;
                previousTime = now;

                if (poll.StatusMessage is not null)
                {
                    output.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] {poll.StatusMessage}");
                    logger?.Information("Gamepad status: {Status}", poll.StatusMessage);
                }

                if (poll.Snapshot is not null)
                {
                    wasConnected = true;
                    ControllerInterceptionCommand command = interception.Observe(poll.Snapshot);
                    if (command is ControllerInterceptionCommand.Exit)
                    {
                        output.WriteLine("Cierre solicitado desde control: Back + Start.");
                        logger?.Information("Shutdown requested by Back + Start chord");
                        break;
                    }

                    if (command is ControllerInterceptionCommand.Pause)
                    {
                        mapper.ReleaseButtons();
                        overlay?.Hide();
                        ReportDragDropState(ref dragDropWasActive);
                        reporter.Reset();
                        output.WriteLine("Aplicación pausada. Solo Start reanuda; Back + Start cierra.");
                        logger?.Information("Controller interception paused");
                    }
                    else if (command is ControllerInterceptionCommand.Resume)
                    {
                        mapper.ReleaseButtons();
                        mapper.SynchronizeButtons(poll.Snapshot);
                        reporter.Reset();
                        output.WriteLine("Aplicación reanudada. Intercepción del control activa.");
                        logger?.Information("Controller interception resumed");
                    }
                    else if (command is ControllerInterceptionCommand.ToggleOverlay && overlay is not null)
                    {
                        mapper.ReleaseButtons();
                        ReportDragDropState(ref dragDropWasActive);
                        overlay.Toggle(ToOverlayInput(poll.Snapshot, overlay.InputStick));
                        if (!overlay.IsVisible) mapper.SynchronizeButtons(poll.Snapshot);
                        reporter.Reset();
                        output.WriteLine(overlay.IsVisible
                            ? "Teclado virtual visible. El mando está dedicado al overlay."
                            : "Teclado virtual oculto. Control de cursor reanudado.");
                        logger?.Information("Virtual keyboard visibility changed: visible={Visible}", overlay.IsVisible);
                    }
                    else if (!interception.IsPaused)
                    {
                        reporter.Report(poll.Snapshot);
                        if (overlay?.IsVisible == true)
                        {
                            overlay.Process(ToOverlayInput(poll.Snapshot, overlay.InputStick));
                        }
                        else
                        {
                            mapper.Apply(poll.Snapshot, elapsed);
                            ReportDragDropState(ref dragDropWasActive);
                        }
                    }
                }
                else if (wasConnected)
                {
                    mapper.ReleaseButtons();
                    ReportDragDropState(ref dragDropWasActive);
                    reporter.Reset();
                    interception.ResetButtonState();
                    overlay?.Hide();
                    wasConnected = false;
                }

                if (now >= nextHeartbeat)
                {
                    logger?.Debug(
                        "Heartbeat: connected={Connected}, paused={Paused}, polls={PollCount}, elapsedMs={ElapsedMilliseconds}",
                        wasConnected,
                        interception.IsPaused,
                        pollCount,
                        now.TotalMilliseconds);
                    nextHeartbeat = now.Add(TimeSpan.FromSeconds(5));
                }

                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(8));
            }
        }
        finally
        {
            mapper.ReleaseButtons();
            overlay?.Hide();
            ReportDragDropState(ref dragDropWasActive);
            output.WriteLine("XboxHid detenido.");
            logger?.Information("XboxHid stopped after {PollCount} polls", pollCount);
        }
    }

    public static bool IsExitChordPressed(GamepadSnapshot snapshot) =>
        snapshot.IsPressed(GamepadButton.Back) && snapshot.IsPressed(GamepadButton.Start);

    internal static VirtualKeyboardInput ToOverlayInput(GamepadSnapshot snapshot, VirtualKeyboardStick stick)
    {
        (GamepadAxis x, GamepadAxis y) = stick is VirtualKeyboardStick.Left
            ? (GamepadAxis.LeftX, GamepadAxis.LeftY)
            : (GamepadAxis.RightX, GamepadAxis.RightY);
        return new(
            snapshot.GetAxis(x),
            -snapshot.GetAxis(y),
            snapshot.IsPressed(GamepadButton.A),
            snapshot.IsPressed(GamepadButton.B),
            snapshot.IsPressed(GamepadButton.X),
            snapshot.IsPressed(GamepadButton.Y),
            snapshot.IsPressed(GamepadButton.DPadUp),
            snapshot.IsPressed(GamepadButton.DPadDown),
            snapshot.IsPressed(GamepadButton.DPadLeft),
            snapshot.IsPressed(GamepadButton.DPadRight));
    }

    [ExcludeFromCodeCoverage]
    private void ReportDragDropState(ref bool previousState)
    {
        if (mapper.IsDragDropActive == previousState)
        {
            return;
        }

        previousState = mapper.IsDragDropActive;
        string status = previousState
            ? "Drag & drop activado: click izquierdo retenido. Cualquier click o acción configurada hace drop."
            : "Drag & drop desactivado: click izquierdo liberado.";
        output.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] {status}");
        logger?.Information("Drag and drop helper changed: active={Active}", previousState);
    }
}

public enum ControllerInterceptionCommand
{
    None,
    Pause,
    Resume,
    ToggleOverlay,
    Exit,
}

public sealed class ControllerInterceptionState
{
    private bool startWasPressed;
    private bool backWasPressed;

    public bool IsPaused { get; private set; }

    public ControllerInterceptionCommand Observe(GamepadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (ControllerApplication.IsExitChordPressed(snapshot))
        {
            return ControllerInterceptionCommand.Exit;
        }

        bool startIsPressed = snapshot.IsPressed(GamepadButton.Start);
        bool backIsPressed = snapshot.IsPressed(GamepadButton.Back);
        ControllerInterceptionCommand command = ControllerInterceptionCommand.None;
        if (startIsPressed && !startWasPressed)
        {
            IsPaused = !IsPaused;
            command = IsPaused
                ? ControllerInterceptionCommand.Pause
                : ControllerInterceptionCommand.Resume;
        }
        else if (!IsPaused && backIsPressed && !backWasPressed)
        {
            command = ControllerInterceptionCommand.ToggleOverlay;
        }

        startWasPressed = startIsPressed;
        backWasPressed = backIsPressed;
        return command;
    }

    public void ResetButtonState()
    {
        startWasPressed = false;
        backWasPressed = false;
    }
}
