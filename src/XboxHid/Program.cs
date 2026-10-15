using System.Runtime.InteropServices;
using Serilog;
using XboxHid.Overlay;

namespace XboxHid;

internal static class Program
{
    public static int Main(string[] args)
    {
        bool logOnly = args.Contains("--log-only", StringComparer.OrdinalIgnoreCase);
        bool showOverlay = args.Contains("--show-overlay", StringComparer.OrdinalIgnoreCase);
        DiagnosticFileLogger? diagnostics = null;

        using CancellationTokenSource cancellation = new();
        using PosixSignalRegistration? interruptSignal = RegisterSignal(PosixSignal.SIGINT, cancellation);
        using PosixSignalRegistration? terminateSignal = RegisterSignal(PosixSignal.SIGTERM, cancellation);
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            string configurationPath = ControllerOptions.ResolvePath(args);
            ControllerOptions options = ControllerOptions.Load(configurationPath);
            diagnostics = DiagnosticFileLogger.Create(options.Debug, Environment.CurrentDirectory);
            ILogger logger = diagnostics.Logger;
            string configuredActions = string.Join(
                ", ",
                options.Actions.Select(binding => $"{binding.Binding}={binding.Shortcut}"));
            logger.Information(
                "XboxHid starting: primaryStick={PrimaryStick}, left={LeftButton}, right={RightButton}, middle={MiddleButton}, dragModifier={DragDropModifier}, speed={Speed}, invertedX={InvertedX}, invertedY={InvertedY}, boost={BoostButton}, multiplier={BoostMultiplier}, actions={Actions}, logOnly={LogOnly}",
                options.PrimaryStick,
                options.LeftButton,
                options.RightButton,
                options.MiddleButton,
                options.DragDropModifier,
                options.Speed,
                options.InvertedXAxis,
                options.InvertedYAxis,
                options.BoostButton,
                options.BoostMultiplier,
                configuredActions,
                logOnly);
            int RunController(IVirtualKeyboardOverlay? overlay) => RunControllerLoop(
                options,
                configurationPath,
                configuredActions,
                logOnly,
                showOverlay,
                cancellation.Token,
                diagnostics,
                logger,
                overlay);

            return options.Overlay.Enabled
                ? OverlayHost.Run(args, options.Overlay, RunController)
                : RunController(null);
        }
        catch (Exception exception) when (
            exception is PlatformNotSupportedException
                or InvalidOperationException
                or DllNotFoundException
                or System.ComponentModel.Win32Exception)
        {
            diagnostics?.Logger.Error(exception, "XboxHid terminated with an error");
            Console.Error.WriteLine($"Error: {exception.Message}");
            Console.Error.WriteLine("Usa --log-only para probar lectura del control sin inyección de mouse.");
            return 1;
        }
        finally
        {
            diagnostics?.Dispose();
        }
    }

    private static int RunControllerLoop(
        ControllerOptions options,
        string configurationPath,
        string configuredActions,
        bool logOnly,
        bool showOverlay,
        CancellationToken cancellationToken,
        DiagnosticFileLogger diagnostics,
        ILogger logger,
        IVirtualKeyboardOverlay? overlay)
    {
        using SdlGamepadSource gamepad = new(options);
        using IMouseController mouse = logOnly
            ? new NullMouseController()
            : MouseControllerFactory.Create();
        logger.Information("Mouse backend: {MouseBackend}", mouse.GetType().Name);

        if (overlay is not null)
        {
            overlay.TextEntered += text =>
            {
                mouse.SendText(text);
                string display = text switch { "\b" => "Backspace", "\n" => "Enter", " " => "Space", _ => text };
                Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] Teclado virtual: {display}");
                logger.Information("Virtual keyboard emitted: {Text}", display);
            };
            if (showOverlay)
            {
                overlay.Toggle(default);
            }
        }

        ControllerMouseMapper mapper = new(mouse, options);
        ControllerActionReporter reporter = new(Console.Out, logger: logger);
        ControllerApplication application = new(gamepad, mapper, reporter, Console.Out, logger, overlay);

        Console.WriteLine(
            $"XboxHid iniciado. PrimaryStick={options.PrimaryStick}; LeftButton={options.LeftButton}; " +
            $"RightButton={options.RightButton}; MiddleButton={options.MiddleButton}; " +
            $"drag & drop={options.DragDropModifier}+LeftButton; velocidad={options.Speed:0.##} px/s; " +
            $"X invertido={options.InvertedXAxis}; Y invertido={options.InvertedYAxis}; " +
            $"turbo={options.BoostButton} x{options.BoostMultiplier:0.##}.");
        Console.WriteLine($"Acciones: {configuredActions}.");
        Console.WriteLine($"Configuración: {configurationPath}");
        Console.WriteLine(options.Debug
            ? $"Debug Serilog activo: {diagnostics.LogDirectory}"
            : "Debug Serilog desactivado: no se escriben logs.");
        Console.WriteLine(logOnly
            ? "Modo --log-only: acciones se muestran sin controlar mouse o teclado."
            : "Control global de mouse y teclado activo.");
        Console.WriteLine(options.Overlay.Enabled
            ? $"Overlay {options.Overlay.Mode}: Back muestra/oculta; stick {options.Overlay.InputStick}."
            : "Overlay desactivado.");
        Console.WriteLine("Presiona Ctrl+C para salir.");
        Console.WriteLine("Start pausa o reanuda la intercepción del control.");
        Console.WriteLine("Back + Start cierra inmediatamente.");

        application.Run(cancellationToken);
        return 0;
    }

    private static PosixSignalRegistration? RegisterSignal(
        PosixSignal signal,
        CancellationTokenSource cancellation)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        return PosixSignalRegistration.Create(signal, context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        });
    }
}
