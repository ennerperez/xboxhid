namespace XboxHid;

public enum GamepadAxis
{
    LeftX,
    LeftY,
    RightX,
    RightY,
    LeftTrigger,
    RightTrigger,
}

public enum GamepadButton
{
    A,
    B,
    X,
    Y,
    Back,
    Guide,
    Start,
    LeftStick,
    RightStick,
    LeftShoulder,
    RightShoulder,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    Misc1,
    RightPaddle1,
    LeftPaddle1,
    RightPaddle2,
    LeftPaddle2,
    Touchpad,
    Misc2,
    Misc3,
    Misc4,
    Misc5,
    Misc6,
}

public sealed class GamepadSnapshot
{
    private readonly float[] axes;
    private readonly bool[] buttons;

    public GamepadSnapshot(float[] axes, bool[] buttons)
    {
        ArgumentNullException.ThrowIfNull(axes);
        ArgumentNullException.ThrowIfNull(buttons);

        if (axes.Length != Enum.GetValues<GamepadAxis>().Length)
        {
            throw new ArgumentException("Cantidad de ejes inválida.", nameof(axes));
        }

        if (buttons.Length != Enum.GetValues<GamepadButton>().Length)
        {
            throw new ArgumentException("Cantidad de botones inválida.", nameof(buttons));
        }

        this.axes = (float[])axes.Clone();
        this.buttons = (bool[])buttons.Clone();
    }

    public float GetAxis(GamepadAxis axis) => axes[(int)axis];

    public bool IsPressed(GamepadButton button) => buttons[(int)button];

    public static GamepadSnapshot Empty { get; } = new(
        new float[Enum.GetValues<GamepadAxis>().Length],
        new bool[Enum.GetValues<GamepadButton>().Length]);
}
