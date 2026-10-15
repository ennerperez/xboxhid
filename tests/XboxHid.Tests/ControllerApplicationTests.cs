namespace XboxHid.Tests;

public sealed class ControllerApplicationTests
{
    [Fact]
    public void IsExitChordPressed_RequiresBackAndStartTogether()
    {
        Assert.False(ControllerApplication.IsExitChordPressed(Snapshot()));
        Assert.False(ControllerApplication.IsExitChordPressed(Snapshot(GamepadButton.Back)));
        Assert.False(ControllerApplication.IsExitChordPressed(Snapshot(GamepadButton.Start)));
        Assert.True(ControllerApplication.IsExitChordPressed(Snapshot(GamepadButton.Back, GamepadButton.Start)));
    }

    [Fact]
    public void Observe_StartTogglesPauseOnlyOncePerPress()
    {
        ControllerInterceptionState state = new();

        Assert.Equal(ControllerInterceptionCommand.Pause, state.Observe(Snapshot(GamepadButton.Start)));
        Assert.True(state.IsPaused);
        Assert.Equal(ControllerInterceptionCommand.None, state.Observe(Snapshot(GamepadButton.Start)));
        Assert.Equal(ControllerInterceptionCommand.None, state.Observe(Snapshot()));
        Assert.Equal(ControllerInterceptionCommand.Resume, state.Observe(Snapshot(GamepadButton.Start)));
        Assert.False(state.IsPaused);
    }

    [Fact]
    public void Observe_BackAndStartAlwaysRequestsExit()
    {
        ControllerInterceptionState state = new();
        state.Observe(Snapshot(GamepadButton.Start));
        state.Observe(Snapshot());

        ControllerInterceptionCommand command = state.Observe(Snapshot(GamepadButton.Back, GamepadButton.Start));

        Assert.True(state.IsPaused);
        Assert.Equal(ControllerInterceptionCommand.Exit, command);
    }

    [Fact]
    public void Observe_BackAloneTogglesOverlayOncePerPress()
    {
        ControllerInterceptionState state = new();

        Assert.Equal(ControllerInterceptionCommand.ToggleOverlay, state.Observe(Snapshot(GamepadButton.Back)));
        Assert.Equal(ControllerInterceptionCommand.None, state.Observe(Snapshot(GamepadButton.Back)));
        Assert.Equal(ControllerInterceptionCommand.None, state.Observe(Snapshot()));
        Assert.Equal(ControllerInterceptionCommand.ToggleOverlay, state.Observe(Snapshot(GamepadButton.Back)));
        Assert.False(state.IsPaused);
    }

    [Fact]
    public void Observe_BackIsIgnoredWhilePaused()
    {
        ControllerInterceptionState state = new();
        state.Observe(Snapshot(GamepadButton.Start));
        state.Observe(Snapshot());

        Assert.Equal(ControllerInterceptionCommand.None, state.Observe(Snapshot(GamepadButton.Back)));
    }

    private static GamepadSnapshot Snapshot(params GamepadButton[] pressedButtons)
    {
        bool[] buttons = new bool[Enum.GetValues<GamepadButton>().Length];
        foreach (GamepadButton button in pressedButtons)
        {
            buttons[(int)button] = true;
        }

        return new(new float[Enum.GetValues<GamepadAxis>().Length], buttons);
    }
}
