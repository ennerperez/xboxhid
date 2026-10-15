using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XboxHid.Overlay;

// Custom rendering keeps geometry resolution-independent. Declaration remains paired with
// DaisyWheelControl.axaml like every Avalonia UI component in this project.
public sealed partial class DaisyWheelControl : Control
{
    private static readonly Color[] FaceColors =
    [
        Color.Parse("#1976D2"), // X
        Color.Parse("#F4B400"), // Y
        Color.Parse("#E62117"), // B
        Color.Parse("#5B8E16")  // A
    ];

    public DaisyWheelControl() => InitializeComponent();

    public VirtualKeyboardViewState? State { get; set; }
    public string StickLabel { get; set; } = "LS";

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        VirtualKeyboardViewState? state = State;
        if (state is null || state.CharacterGroups.Count != 8) return;

        double size = Math.Min(Bounds.Width, Bounds.Height);
        Point center = new(Bounds.Width / 2, Bounds.Height / 2);
        double outerRadius = size * 0.46;
        double innerRadius = size * 0.15;
        double groupRadius = size * 0.31;

        context.DrawEllipse(Brush.Parse("#E9072B42"), new Pen(Brush.Parse("#553B789B"), 2), center, outerRadius + 7, outerRadius + 7);
        for (int index = 0; index < 8; index++)
        {
            bool active = index == state.ActiveGroup;
            StreamGeometry petal = CreatePetal(center, innerRadius, outerRadius, -90 + index * 45);
            IBrush fill = Brush.Parse(active ? "#E2477892" : "#E00B3B56");
            IPen border = new Pen(Brush.Parse(active ? "#FF78DCF3" : "#60265B75"), active ? 3 : 1);
            context.DrawGeometry(fill, border, petal);
            DrawCharacterGroup(context, center, groupRadius, index, state.CharacterGroups[index], active, state.ActiveFace, size);
        }

        DrawStick(context, center, size, StickLabel);
    }

    private static StreamGeometry CreatePetal(Point center, double inner, double outer, double degrees)
    {
        const double halfSector = 22.5;
        Point innerLeft = Polar(center, inner, degrees - halfSector);
        Point outerLeft = Polar(center, outer, degrees - halfSector);
        Point tip = Polar(center, outer * 1.02, degrees);
        Point outerRight = Polar(center, outer, degrees + halfSector);
        Point innerRight = Polar(center, inner, degrees + halfSector);
        Point shoulderLeft = Polar(center, outer * 0.72, degrees - halfSector * 0.72);
        Point shoulderRight = Polar(center, outer * 0.72, degrees + halfSector * 0.72);

        StreamGeometry geometry = new();
        using StreamGeometryContext path = geometry.Open();
        path.BeginFigure(innerLeft, true);
        path.QuadraticBezierTo(shoulderLeft, outerLeft);
        path.QuadraticBezierTo(tip, outerRight);
        path.QuadraticBezierTo(shoulderRight, innerRight);
        path.QuadraticBezierTo(center, innerLeft);
        path.EndFigure(true);
        return geometry;
    }

    private static void DrawCharacterGroup(
        DrawingContext context,
        Point wheelCenter,
        double radius,
        int direction,
        IReadOnlyList<string> characters,
        bool active,
        int activeFace,
        double size)
    {
        Point groupCenter = Polar(wheelCenter, radius, -90 + direction * 45);
        double offset = size * 0.036;
        Point[] positions =
        [
            new(groupCenter.X - offset, groupCenter.Y),
            new(groupCenter.X, groupCenter.Y - offset),
            new(groupCenter.X + offset, groupCenter.Y),
            new(groupCenter.X, groupCenter.Y + offset)
        ];

        for (int face = 0; face < 4; face++)
        {
            if (active)
            {
                double circleRadius = size * (face == activeFace ? 0.034 : 0.029);
                context.DrawEllipse(
                    new SolidColorBrush(FaceColors[face]),
                    face == activeFace ? new Pen(Brushes.White, 3) : null,
                    positions[face],
                    circleRadius,
                    circleRadius);
            }

            DrawCenteredText(context, characters[face], positions[face], size * 0.035, Brushes.White, FontWeight.Bold);
        }
    }

    private static void DrawStick(DrawingContext context, Point center, double size, string label)
    {
        double radius = size * 0.095;
        context.DrawEllipse(Brush.Parse("#CC161D24"), new Pen(Brush.Parse("#FF071019"), 3), new Point(center.X, center.Y + radius * 0.45), radius * 0.32, radius * 0.55);
        context.DrawEllipse(Brush.Parse("#FF6A7078"), new Pen(Brush.Parse("#FF101820"), 4), center, radius, radius * 0.78);
        context.DrawEllipse(null, new Pen(Brush.Parse("#807F8790"), 2), center, radius * 0.82, radius * 0.62);
        DrawCenteredText(context, label, center, size * 0.045, Brush.Parse("#FFE4E8ED"), FontWeight.Normal);
    }

    private static void DrawCenteredText(
        DrawingContext context,
        string text,
        Point center,
        double fontSize,
        IBrush brush,
        FontWeight weight)
    {
        FormattedText formatted = new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, weight),
            fontSize,
            brush);
        context.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private static Point Polar(Point center, double radius, double degrees)
    {
        double radians = degrees * Math.PI / 180;
        return new Point(center.X + Math.Cos(radians) * radius, center.Y + Math.Sin(radians) * radius);
    }
}
