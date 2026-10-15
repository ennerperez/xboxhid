using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace XboxHid.Overlay;

public static class OverlayHost
{
    private static readonly object Gate = new();
    private static VirtualKeyboardOptions? pendingOptions;
    private static Func<IVirtualKeyboardOverlay, int>? pendingWorker;
    private static ExceptionDispatchInfo? workerError;
    private static int workerExitCode;

    public static int Run(string[] args, VirtualKeyboardOptions options, Func<IVirtualKeyboardOverlay, int> worker)
    {
        lock (Gate)
        {
            pendingOptions = options;
            pendingWorker = worker;
            workerError = null;
            workerExitCode = 0;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        workerError?.Throw();
        return workerExitCode;
    }

    internal static (VirtualKeyboardOptions Options, Func<IVirtualKeyboardOverlay, int> Worker) TakeStartup()
    {
        lock (Gate)
            return (
                pendingOptions ?? throw new InvalidOperationException("Falta configuración del overlay."),
                pendingWorker ?? throw new InvalidOperationException("Falta proceso del control."));
    }

    internal static void CompleteWorker(int exitCode, Exception? error = null)
    {
        workerExitCode = exitCode;
        if (error is not null) workerError = ExceptionDispatchInfo.Capture(error);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<OverlayApplication>()
            .UsePlatformDetect()
            .LogToTrace();
}

internal sealed class AvaloniaVirtualKeyboardOverlay : IVirtualKeyboardOverlay
{
    private readonly object gate = new();
    private readonly VirtualKeyboardProcessor processor;
    private bool isVisible;
    private bool disposed;

    public AvaloniaVirtualKeyboardOverlay(VirtualKeyboardOptions options)
    {
        InputStick = options.InputStick;
        processor = new(options.Mode);
        Window = new VirtualKeyboardWindow(options);
        Window.Render(processor.ViewState);
    }

    public VirtualKeyboardWindow Window { get; }
    public VirtualKeyboardStick InputStick { get; }

    public bool IsVisible
    {
        get { lock (gate) return isVisible; }
    }

    public event Action<string>? TextEntered;

    public void Toggle(VirtualKeyboardInput input)
    {
        bool show;
        lock (gate)
        {
            if (disposed) return;
            isVisible = !isVisible;
            show = isVisible;
            processor.Synchronize(input);
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (show)
            {
                Window.Render(processor.ViewState);
                Window.Show();
                Window.PlaceAtBottomCenter();
                Window.EnsureTopmost();
                Dispatcher.UIThread.Post(Window.EnsureTopmost, DispatcherPriority.Loaded);
            }
            else
            {
                Window.Hide();
            }
        });
    }

    public void Hide()
    {
        lock (gate) isVisible = false;
        Dispatcher.UIThread.Post(Window.Hide);
    }

    public void Process(VirtualKeyboardInput input)
    {
        IReadOnlyList<string> output;
        VirtualKeyboardViewState state;
        lock (gate)
        {
            if (!isVisible || disposed) return;
            output = processor.Process(input);
            state = processor.ViewState;
        }

        foreach (string text in output) TextEntered?.Invoke(text);
        Dispatcher.UIThread.Post(() => Window.Render(state));
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            isVisible = false;
        }
        Dispatcher.UIThread.Post(Window.Close);
    }
}
