namespace XboxHid.Overlay;

public enum VirtualKeyboardMode
{
    DaisyWheel,
    CharacterCycling,
    GlyphStick
}

public enum VirtualKeyboardStick
{
    Left,
    Right
}

public sealed record VirtualKeyboardOptions(
    bool Enabled,
    VirtualKeyboardMode Mode,
    VirtualKeyboardStick InputStick,
    double Opacity,
    double Scale)
{
    public static VirtualKeyboardOptions Default { get; } =
        new(true, VirtualKeyboardMode.DaisyWheel, VirtualKeyboardStick.Left, 0.94, 1.0);
}

public readonly record struct VirtualKeyboardInput(
    float X,
    float Y,
    bool A,
    bool B,
    bool XButton,
    bool YButton,
    bool DPadUp,
    bool DPadDown,
    bool DPadLeft,
    bool DPadRight);

public sealed record VirtualKeyboardViewState(
    VirtualKeyboardMode Mode,
    bool NumberLayer,
    string Title,
    string Preview,
    string Status,
    IReadOnlyList<string> Groups,
    IReadOnlyList<IReadOnlyList<string>> CharacterGroups,
    int ActiveGroup,
    int ActiveFace);

public interface IVirtualKeyboardOverlay : IDisposable
{
    bool IsVisible { get; }
    VirtualKeyboardStick InputStick { get; }
    event Action<string>? TextEntered;
    void Toggle(VirtualKeyboardInput input);
    void Hide();
    void Process(VirtualKeyboardInput input);
}

public sealed class VirtualKeyboardProcessor
{
    private static readonly string[] Directions = ["↑", "↗", "→", "↘", "↓", "↙", "←", "↖"];
    private static readonly string[][] DaisyLetters =
    [
        ["a", "b", "c", "d"], ["e", "f", "g", "h"], ["i", "j", "k", "l"], ["m", "n", "o", "p"],
        ["q", "r", "s", "t"], ["u", "v", "w", "x"], ["y", "z", ".", ","], ["'", "-", "_", "?"]
    ];
    private static readonly string[][] DaisyNumbers =
    [
        ["1", "2", "3", "4"], ["5", "6", "7", "8"], ["9", "0", ".", ","], ["+", "-", "*", "/"],
        ["(", ")", "[", "]"], ["@", "#", "$", "%"], ["!", "?", ":", ";"], ["_", "=", "'", "\""]
    ];
    private static readonly string[] CycleLetters = ["abc", "def", "ghi", "jkl", "mno", "pqrs", "tuv", "wxyz"];
    private static readonly string[] CycleNumbers = ["123", "456", "789", "0.,", "+-*/", "()[]", "@#$%", "!?:;"];
    private static readonly IReadOnlyDictionary<string, string> Glyphs = new Dictionary<string, string>
    {
        ["U"] = "e",
        ["R"] = "t",
        ["D"] = "a",
        ["L"] = "o",
        ["UR"] = "i",
        ["UL"] = "n",
        ["RU"] = "s",
        ["RD"] = "h",
        ["DR"] = "r",
        ["DL"] = "d",
        ["LD"] = "l",
        ["LU"] = "c",
        ["UD"] = "u",
        ["DU"] = "m",
        ["URD"] = "w",
        ["ULD"] = "f",
        ["RUL"] = "g",
        ["RDL"] = "y",
        ["DRU"] = "p",
        ["DLU"] = "b",
        ["LUR"] = "v",
        ["LDR"] = "k",
        ["UDR"] = "j",
        ["DUL"] = "x",
        ["URDL"] = "q",
        ["ULDR"] = "z"
    };

    private readonly VirtualKeyboardMode mode;
    private VirtualKeyboardInput previous;
    private bool numberLayer;
    private int activeGroup = -1;
    private int cycleIndex;
    private bool cycleArmed = true;
    private string glyphPath = string.Empty;

    public VirtualKeyboardProcessor(VirtualKeyboardMode mode)
    {
        this.mode = mode;
        ViewState = BuildViewState();
    }

    public VirtualKeyboardViewState ViewState { get; private set; }

    public void Synchronize(VirtualKeyboardInput input)
    {
        previous = input;
        activeGroup = -1;
        cycleIndex = 0;
        cycleArmed = true;
        glyphPath = string.Empty;
        ViewState = BuildViewState();
    }

    public IReadOnlyList<string> Process(VirtualKeyboardInput input)
    {
        List<string> output = [];
        if (Rising(input.DPadLeft, previous.DPadLeft)) output.Add("\b");
        if (Rising(input.DPadDown, previous.DPadDown)) output.Add(" ");
        if (Rising(input.DPadRight, previous.DPadRight)) output.Add("\n");
        if (Rising(input.DPadUp, previous.DPadUp))
        {
            numberLayer = !numberLayer;
            activeGroup = -1;
            cycleIndex = 0;
            glyphPath = string.Empty;
        }

        switch (mode)
        {
            case VirtualKeyboardMode.DaisyWheel:
                ProcessDaisy(input, output);
                break;
            case VirtualKeyboardMode.CharacterCycling:
                ProcessCycling(input, output);
                break;
            case VirtualKeyboardMode.GlyphStick:
                ProcessGlyph(input, output);
                break;
        }

        previous = input;
        ViewState = BuildViewState();
        return output;
    }

    private void ProcessDaisy(VirtualKeyboardInput input, List<string> output)
    {
        int direction = GetOctant(input.X, input.Y, 0.45f);
        if (direction >= 0) activeGroup = direction;
        if (activeGroup < 0) return;

        string[][] groups = numberLayer ? DaisyNumbers : DaisyLetters;
        // Orden físico clásico del frontal Xbox: X, Y, B, A.
        if (Rising(input.XButton, previous.XButton)) output.Add(groups[activeGroup][0]);
        if (Rising(input.YButton, previous.YButton)) output.Add(groups[activeGroup][1]);
        if (Rising(input.B, previous.B)) output.Add(groups[activeGroup][2]);
        if (Rising(input.A, previous.A)) output.Add(groups[activeGroup][3]);
    }

    private void ProcessCycling(VirtualKeyboardInput input, List<string> output)
    {
        float radius = MathF.Sqrt(input.X * input.X + input.Y * input.Y);
        int direction = GetOctant(input.X, input.Y, 0.35f);
        if (direction >= 0 && activeGroup != direction)
        {
            activeGroup = direction;
            cycleIndex = 0;
            cycleArmed = true;
        }

        if (activeGroup >= 0 && radius >= 0.88f && cycleArmed)
        {
            string group = CurrentCycleGroups()[activeGroup];
            cycleIndex = (cycleIndex + 1) % group.Length;
            cycleArmed = false;
        }
        else if (radius < 0.72f)
        {
            cycleArmed = true;
        }

        if (activeGroup >= 0 && radius < 0.25f)
        {
            string group = CurrentCycleGroups()[activeGroup];
            output.Add(group[cycleIndex].ToString());
            activeGroup = -1;
            cycleIndex = 0;
        }
    }

    private void ProcessGlyph(VirtualKeyboardInput input, List<string> output)
    {
        float radius = MathF.Sqrt(input.X * input.X + input.Y * input.Y);
        if (numberLayer)
        {
            ProcessCycling(input, output);
            return;
        }

        if (radius >= 0.55f)
        {
            char cardinal = MathF.Abs(input.X) > MathF.Abs(input.Y)
                ? input.X > 0 ? 'R' : 'L'
                : input.Y > 0 ? 'U' : 'D';
            if (glyphPath.Length < 4 && (glyphPath.Length == 0 || glyphPath[^1] != cardinal))
                glyphPath += cardinal;
        }
        else if (radius < 0.22f && glyphPath.Length > 0)
        {
            if (Glyphs.TryGetValue(glyphPath, out string? character)) output.Add(character);
            glyphPath = string.Empty;
        }
    }

    private VirtualKeyboardViewState BuildViewState()
    {
        IReadOnlyList<string> groups;
        IReadOnlyList<IReadOnlyList<string>> characterGroups = [];
        string preview = string.Empty;
        string status;
        int activeFace = -1;

        if (mode == VirtualKeyboardMode.DaisyWheel)
        {
            string[][] source = numberLayer ? DaisyNumbers : DaisyLetters;
            groups = source.Select((g, i) => $"{Directions[i]}  X:{g[0]}  Y:{g[1]}  B:{g[2]}  A:{g[3]}").ToArray();
            characterGroups = source.Select(group => (IReadOnlyList<string>)group).ToArray();
            activeFace = previous.XButton ? 0 : previous.YButton ? 1 : previous.B ? 2 : previous.A ? 3 : -1;
            status = "Inclina stick, luego pulsa X/Y/B/A";
        }
        else if (mode == VirtualKeyboardMode.CharacterCycling || numberLayer)
        {
            string[] source = CurrentCycleGroups();
            groups = source.Select((g, i) => $"{Directions[i]}  {g}").ToArray();
            characterGroups = source.Select(group => (IReadOnlyList<string>)group.Select(character => character.ToString()).ToArray()).ToArray();
            if (activeGroup >= 0) preview = source[activeGroup][cycleIndex].ToString();
            status = "Inclina; borde exterior cambia carácter; centro confirma";
        }
        else
        {
            groups = Glyphs.Select(pair => $"{pair.Key}  {pair.Value}").ToArray();
            preview = Glyphs.TryGetValue(glyphPath, out string? character) ? character : glyphPath;
            status = "Dibuja con direcciones cardinales; centro confirma";
        }

        return new(mode, numberLayer, mode switch
        {
            VirtualKeyboardMode.DaisyWheel => "DAISYWHEEL",
            VirtualKeyboardMode.CharacterCycling => "CHARACTER CYCLING",
            _ => "GLYPH-STICK"
        }, preview, status, groups, characterGroups, activeGroup, activeFace);
    }

    private string[] CurrentCycleGroups() => numberLayer ? CycleNumbers : CycleLetters;

    private static bool Rising(bool current, bool old) => current && !old;

    private static int GetOctant(float x, float y, float threshold)
    {
        if (MathF.Sqrt(x * x + y * y) < threshold) return -1;
        double angle = Math.Atan2(x, y);
        if (angle < 0) angle += Math.PI * 2;
        return (int)Math.Round(angle / (Math.PI / 4), MidpointRounding.AwayFromZero) % 8;
    }
}
