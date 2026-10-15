using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace XboxHid.Overlay;

public sealed partial class VirtualKeyboardWindow : Window
{
    private readonly TextBlock title;
    private readonly TextBlock preview;
    private readonly TextBlock status;
    private readonly TextBlock layer;
    private readonly UniformGrid groups;
    private readonly DaisyWheelControl daisyWheel;
    private readonly bool usesDaisyWheel;
    private readonly string stickLabel;

    public VirtualKeyboardWindow() : this(VirtualKeyboardOptions.Default) { }

    public VirtualKeyboardWindow(VirtualKeyboardOptions options)
    {
        InitializeComponent();
        title = this.FindControl<TextBlock>("TitleText")!;
        preview = this.FindControl<TextBlock>("PreviewText")!;
        status = this.FindControl<TextBlock>("StatusText")!;
        layer = this.FindControl<TextBlock>("LayerText")!;
        groups = this.FindControl<UniformGrid>("GroupsGrid")!;
        daisyWheel = this.FindControl<DaisyWheelControl>("DaisyWheel")!;

        usesDaisyWheel = options.Mode is VirtualKeyboardMode.DaisyWheel;
        stickLabel = options.InputStick is VirtualKeyboardStick.Left ? "LS" : "RS";
        Width = (usesDaisyWheel ? 650 : 760) * options.Scale;
        Height = (usesDaisyWheel ? 780 : 350) * options.Scale;
        Opacity = options.Opacity;
        daisyWheel.Height = 590 * options.Scale;
        daisyWheel.IsVisible = usesDaisyWheel;
        groups.IsVisible = !usesDaisyWheel;
        Opened += (_, _) => EnsureTopmost();
        Deactivated += (_, _) => Dispatcher.UIThread.Post(EnsureTopmost, DispatcherPriority.Loaded);
    }

    public void Render(VirtualKeyboardViewState state)
    {
        title.Text = $"{state.Title} · {stickLabel}";
        preview.Text = state.Preview;
        layer.Text = state.NumberLayer ? "123" : "abc";
        status.Text = state.Status;
        if (usesDaisyWheel)
        {
            daisyWheel.State = state;
            daisyWheel.StickLabel = stickLabel;
            daisyWheel.InvalidateVisual();
            return;
        }

        groups.Children.Clear();
        for (int index = 0; index < state.Groups.Count; index++)
        {
            bool selected = index == state.ActiveGroup;
            groups.Children.Add(new Border
            {
                Margin = new Thickness(4),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(9),
                Background = Brush.Parse(selected ? "#B43A6C86" : "#70242D3A"),
                BorderBrush = Brush.Parse(selected ? "#FF62E6FF" : "#405E6B7C"),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Child = new TextBlock
                {
                    Text = state.Groups[index],
                    FontSize = 14,
                    FontWeight = selected ? FontWeight.Bold : FontWeight.Normal,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                }
            });
        }
    }

    public void PlaceAtBottomCenter()
    {
        Screen? screen = Screens.Primary;
        if (screen is null) return;
        PixelRect area = screen.WorkingArea;
        Position = new PixelPoint(
            area.X + Math.Max(0, (area.Width - (int)Width) / 2),
            area.Bottom - (int)Height - 28);
    }

    public void EnsureTopmost()
    {
        // Some compositors discard always-on-top after a hidden window is remapped.
        Topmost = false;
        Topmost = true;
    }
}
