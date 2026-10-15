using XboxHid.Overlay;

namespace XboxHid.Tests;

public sealed class ControllerMouseMapperTests
{
    [Fact]
    public void Apply_UsesPrimaryStickAndAxisInversion()
    {
        FakeMouseController mouse = new();
        ControllerMouseMapper mapper = new(mouse, Options(primaryStick: PrimaryStick.Left, invertedX: true, invertedY: true));

        mapper.Apply(Snapshot(leftX: 1, leftY: -1), TimeSpan.FromSeconds(0.1));

        Assert.Equal((-120, 120), mouse.Movements.Single());
    }

    [Fact]
    public void Apply_DefaultTriggersControlLeftAndRightButtons()
    {
        FakeMouseController mouse = new();
        ControllerMouseMapper mapper = new(mouse, Options());

        mapper.Apply(Snapshot(leftTrigger: 1, rightTrigger: 1), TimeSpan.Zero);
        mapper.Apply(Snapshot(), TimeSpan.Zero);

        Assert.Equal([true, false], mouse.LeftButtonStates);
        Assert.Equal([true, false], mouse.RightButtonStates);
    }

    [Fact]
    public void Apply_DefaultRightStickControlsMiddleButton()
    {
        FakeMouseController mouse = new();
        ControllerMouseMapper mapper = new(mouse, Options());

        mapper.Apply(Snapshot(buttons: [GamepadButton.RightStick]), TimeSpan.Zero);
        mapper.Apply(Snapshot(), TimeSpan.Zero);

        Assert.Equal([true, false], mouse.MiddleButtonStates);
    }

    [Fact]
    public void Apply_DragDropStartsWithModifierAndLeftButton_ThenLeftStopsIt()
    {
        FakeMouseController mouse = new();
        ControllerMouseMapper mapper = new(mouse, Options());

        mapper.Apply(Snapshot(leftTrigger: 1, buttons: [GamepadButton.RightShoulder]), TimeSpan.Zero);
        mapper.Apply(Snapshot(buttons: [GamepadButton.RightShoulder]), TimeSpan.Zero);
        Assert.True(mapper.IsDragDropActive);

        mapper.Apply(Snapshot(leftTrigger: 1), TimeSpan.Zero);

        Assert.False(mapper.IsDragDropActive);
        Assert.Equal([true, false], mouse.LeftButtonStates);
    }

    [Fact]
    public void Apply_AnyActionDropsBeforeSendingShortcut()
    {
        FakeMouseController mouse = new();
        ControllerMouseMapper mapper = new(mouse, Options());
        mapper.Apply(Snapshot(leftTrigger: 1, buttons: [GamepadButton.RightShoulder]), TimeSpan.Zero);
        mapper.Apply(Snapshot(), TimeSpan.Zero);

        mapper.Apply(Snapshot(buttons: [GamepadButton.DPadLeft]), TimeSpan.Zero);

        Assert.False(mapper.IsDragDropActive);
        Assert.Equal([true, false], mouse.LeftButtonStates);
        Assert.Equal("ALT+TAB", mouse.Shortcuts.Single().Expression);
    }

    [Fact]
    public void Apply_BoostButtonMultipliesSpeed()
    {
        FakeMouseController mouse = new();
        ControllerMouseMapper mapper = new(mouse, Options());

        mapper.Apply(Snapshot(rightX: 1, buttons: [GamepadButton.RightShoulder]), TimeSpan.FromSeconds(0.1));

        Assert.Equal((240, 0), mouse.Movements.Single());
    }

    [Fact]
    public void SynchronizeButtons_DoesNotCreateClickForAlreadyHeldTrigger()
    {
        FakeMouseController mouse = new();
        ControllerMouseMapper mapper = new(mouse, Options());
        GamepadSnapshot held = Snapshot(leftTrigger: 1);

        mapper.SynchronizeButtons(held);
        mapper.Apply(held, TimeSpan.Zero);

        Assert.Empty(mouse.LeftButtonStates);
    }

    [Fact]
    public void SynchronizeButtons_SuppressesAllHeldClicksUntilRelease()
    {
        FakeMouseController mouse = new();
        ControllerMouseMapper mapper = new(mouse, Options());
        GamepadSnapshot held = Snapshot(leftTrigger: 1, rightTrigger: 1, buttons: [GamepadButton.RightStick]);

        mapper.SynchronizeButtons(held);
        mapper.Apply(held, TimeSpan.Zero);
        mapper.Apply(Snapshot(), TimeSpan.Zero);
        mapper.Apply(held, TimeSpan.Zero);

        Assert.Equal([true], mouse.LeftButtonStates);
        Assert.Equal([true], mouse.RightButtonStates);
        Assert.Equal([true], mouse.MiddleButtonStates);
    }

    private static ControllerOptions Options(
        PrimaryStick primaryStick = PrimaryStick.Right,
        bool invertedX = false,
        bool invertedY = false) => new(
        primaryStick,
        GamepadBinding.Parse("LeftTrigger", "test", 0.5f),
        GamepadBinding.Parse("RightTrigger", "test", 0.5f),
        GamepadBinding.Parse("RightStick", "test", 0.5f),
        GamepadBinding.Parse("RightShoulder", "test", 0.5f),
        0.5f, 1200, 0, invertedX, invertedY,
        GamepadBinding.Parse("RightShoulder", "test", 0.5f),
        2, MappedAction.CreateDefaults(0.5f), false, VirtualKeyboardOptions.Default);

    private static GamepadSnapshot Snapshot(
        float leftX = 0,
        float leftY = 0,
        float rightX = 0,
        float rightY = 0,
        float leftTrigger = 0,
        float rightTrigger = 0,
        GamepadButton[]? buttons = null)
    {
        float[] axes = new float[Enum.GetValues<GamepadAxis>().Length];
        bool[] pressed = new bool[Enum.GetValues<GamepadButton>().Length];
        axes[(int)GamepadAxis.LeftX] = leftX;
        axes[(int)GamepadAxis.LeftY] = leftY;
        axes[(int)GamepadAxis.RightX] = rightX;
        axes[(int)GamepadAxis.RightY] = rightY;
        axes[(int)GamepadAxis.LeftTrigger] = leftTrigger;
        axes[(int)GamepadAxis.RightTrigger] = rightTrigger;
        foreach (GamepadButton button in buttons ?? []) pressed[(int)button] = true;
        return new(axes, pressed);
    }

    private sealed class FakeMouseController : IMouseController
    {
        public List<(int X, int Y)> Movements { get; } = [];
        public List<bool> LeftButtonStates { get; } = [];
        public List<bool> RightButtonStates { get; } = [];
        public List<bool> MiddleButtonStates { get; } = [];
        public List<KeyboardShortcut> Shortcuts { get; } = [];
        public void MoveRelative(int deltaX, int deltaY) => Movements.Add((deltaX, deltaY));
        public void SetLeftButton(bool pressed) => LeftButtonStates.Add(pressed);
        public void SetRightButton(bool pressed) => RightButtonStates.Add(pressed);
        public void SetMiddleButton(bool pressed) => MiddleButtonStates.Add(pressed);
        public void PerformShortcut(KeyboardShortcut shortcut) => Shortcuts.Add(shortcut);
        public void SendText(string text) { }
        public void Dispose() { }
    }
}
