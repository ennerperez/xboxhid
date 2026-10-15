using System.Globalization;
using Microsoft.Extensions.Configuration;
using XboxHid.Overlay;

namespace XboxHid;

public sealed record ControllerOptions(
    PrimaryStick PrimaryStick,
    GamepadBinding LeftButton,
    GamepadBinding RightButton,
    GamepadBinding MiddleButton,
    GamepadBinding DragDropModifier,
    float DefaultThreshold,
    float Speed,
    float DeadZone,
    bool InvertedXAxis,
    bool InvertedYAxis,
    GamepadBinding BoostButton,
    float BoostMultiplier,
    IReadOnlyList<MappedAction> Actions,
    bool Debug,
    VirtualKeyboardOptions Overlay)
{
    private static readonly string[] RemovedMouseKeys =
    [
        "MovementStick", "LeftClickButton", "RightClickButton", "CenterClickButton",
        "PrimaryClickAlternative", "PrimaryClickAlternativeThreshold", "RightClickModifier",
        "RightClickModifierThreshold", "DragDropButton", "InvertVertical",
        "SpeedPixelsPerSecond", "BoostControl", "BoostActivationThreshold"
    ];

    public IEnumerable<GamepadControl> MappedControls => LeftButton.Controls
        .Concat(RightButton.Controls).Concat(MiddleButton.Controls).Concat(DragDropModifier.Controls)
        .Concat(BoostButton.Controls).Concat(Actions.SelectMany(action => action.Binding.Controls)).Distinct();

    public static ControllerOptions Load(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException($"No se encontró archivo de configuración: {path}");
        IConfiguration configuration = new ConfigurationBuilder().AddIniFile(path, false, false).Build();
        IConfigurationSection mouse = configuration.GetSection("Mouse");
        foreach (string removed in RemovedMouseKeys)
            if (mouse[removed] is not null)
                throw new InvalidOperationException($"Mouse:{removed} fue eliminado. Actualiza xboxhid.ini al esquema nuevo.");

        PrimaryStick primaryStick = ParseEnum<PrimaryStick>(mouse["PrimaryStick"] ?? "Right", "Mouse:PrimaryStick");
        float threshold = ParseFloat(mouse["DefaultThreshold"] ?? "0.5", "Mouse:DefaultThreshold", 0.05f, 1);
        GamepadBinding left = GamepadBinding.Parse(mouse["LeftButton"] ?? "LeftTrigger", "Mouse:LeftButton", threshold);
        GamepadBinding right = GamepadBinding.Parse(mouse["RightButton"] ?? "RightTrigger", "Mouse:RightButton", threshold);
        GamepadBinding middle = GamepadBinding.Parse(mouse["MiddleButton"] ?? "RightStick", "Mouse:MiddleButton", threshold);
        GamepadBinding drag = GamepadBinding.Parse(mouse["DragDropModifier"] ?? "RightShoulder", "Mouse:DragDropModifier", threshold);
        GamepadBinding boost = GamepadBinding.Parse(mouse["BoostButton"] ?? "RightShoulder", "Mouse:BoostButton", threshold);
        float speed = ParseFloat(mouse["Speed"] ?? "1200", "Mouse:Speed", 1, 10_000);
        float deadZone = ParseFloat(mouse["DeadZone"] ?? "0.16", "Mouse:DeadZone", 0, 0.99f);
        bool invertedX = ParseBoolean(mouse["InvertedXAxis"] ?? "false", "Mouse:InvertedXAxis");
        bool invertedY = ParseBoolean(mouse["InvertedYAxis"] ?? "false", "Mouse:InvertedYAxis");
        float boostMultiplier = ParseFloat(mouse["BoostMultiplier"] ?? "2", "Mouse:BoostMultiplier", 1, 10);
        IReadOnlyList<MappedAction> actions = ParseActions(configuration.GetSection("Actions"), threshold);
        bool debug = ParseBoolean(configuration["Diagnostics:Debug"] ?? "false", "Diagnostics:Debug");
        IConfigurationSection overlay = configuration.GetSection("Overlay");
        if (overlay["InputStick"] is not null)
            throw new InvalidOperationException("Overlay:InputStick fue eliminado; ahora se deriva como opuesto a Mouse:PrimaryStick.");
        // Cursor and overlay never compete for the same stick: overlay always owns the opposite one.
        VirtualKeyboardStick overlayStick = primaryStick is PrimaryStick.Left ? VirtualKeyboardStick.Right : VirtualKeyboardStick.Left;
        VirtualKeyboardOptions overlayOptions = new(
            ParseBoolean(overlay["Enabled"] ?? "true", "Overlay:Enabled"),
            ParseEnum<VirtualKeyboardMode>(overlay["Mode"] ?? "DaisyWheel", "Overlay:Mode"),
            overlayStick,
            ParseFloat(overlay["Opacity"] ?? "0.94", "Overlay:Opacity", 0.2f, 1),
            ParseFloat(overlay["Scale"] ?? "1", "Overlay:Scale", 0.5f, 2));
        return new(primaryStick, left, right, middle, drag, threshold, speed, deadZone, invertedX, invertedY,
            boost, boostMultiplier, actions, debug, overlayOptions);
    }

    public static string ResolvePath(string[] args)
    {
        int index = Array.FindIndex(args, value => value.Equals("--config", StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new InvalidOperationException("--config requiere ruta a archivo INI.");
            return Path.GetFullPath(args[index + 1]);
        }
        string local = Path.Combine(Environment.CurrentDirectory, "xboxhid.ini");
        return File.Exists(local) ? local : Path.Combine(AppContext.BaseDirectory, "xboxhid.ini");
    }

    private static IReadOnlyList<MappedAction> ParseActions(IConfigurationSection section, float threshold)
    {
        IConfigurationSection[] children = section.GetChildren().ToArray();
        if (children.Length == 0) return MappedAction.CreateDefaults(threshold);
        return children.Select(child => new MappedAction(
            GamepadBinding.Parse(child.Key, $"Actions:{child.Key}", threshold),
            KeyboardShortcut.Parse(child.Value ?? string.Empty, $"Actions:{child.Key}"))).ToArray();
    }

    private static float ParseFloat(string value, string key, float minimum, float maximum)
    {
        if (float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && parsed >= minimum && parsed <= maximum) return parsed;
        throw new InvalidOperationException($"Valor inválido para {key}: '{value}'. Rango: {minimum}-{maximum}.");
    }

    private static bool ParseBoolean(string value, string key)
    {
        if (bool.TryParse(value.Trim(), out bool parsed)) return parsed;
        throw new InvalidOperationException($"Valor inválido para {key}: '{value}'. Usa true o false.");
    }

    private static T ParseEnum<T>(string value, string key) where T : struct, Enum
    {
        if (Enum.TryParse(value.Trim(), true, out T parsed)) return parsed;
        throw new InvalidOperationException($"Valor inválido para {key}: '{value}'. Valores: {string.Join(", ", Enum.GetNames<T>())}.");
    }
}

public enum PrimaryStick { Left, Right }
