using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace XboxHid.Overlay;

// Avalonia lifecycle stays paired with App.axaml. Polling runs off UI thread so compositor
// events cannot block controller reads.
public sealed partial class OverlayApplication : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            throw new InvalidOperationException("Avalonia desktop lifetime no disponible.");

        (VirtualKeyboardOptions options, Func<IVirtualKeyboardOverlay, int> worker) = OverlayHost.TakeStartup();
        AvaloniaVirtualKeyboardOverlay overlay = new(options);
        desktop.MainWindow = overlay.Window;

        Thread thread = new(() =>
        {
            try
            {
                OverlayHost.CompleteWorker(worker(overlay));
            }
            catch (Exception error)
            {
                OverlayHost.CompleteWorker(1, error);
            }
            finally
            {
                Dispatcher.UIThread.Post(() => desktop.Shutdown());
            }
        })
        {
            IsBackground = true,
            Name = "XboxHid controller loop"
        };
        thread.Start();

        base.OnFrameworkInitializationCompleted();
    }
}
