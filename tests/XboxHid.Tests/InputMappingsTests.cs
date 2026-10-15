namespace XboxHid.Tests;

public sealed class InputMappingsTests
{
    [Fact]
    public void Alternatives_ActivateFromAnyConfiguredControl()
    {
        GamepadBindingMatcher matcher = GamepadBinding.Parse("A/B/X", "test", 0.5f).CreateMatcher(0.5f);
        Assert.True(matcher.Update(Snapshot(GamepadButton.B), TimeSpan.Zero));
    }

    [Fact]
    public void Simultaneous_RequiresAllControls()
    {
        GamepadBindingMatcher matcher = GamepadBinding.Parse("A-B-X", "test", 0.5f).CreateMatcher(0.5f);
        Assert.False(matcher.Update(Snapshot(GamepadButton.A, GamepadButton.B), TimeSpan.Zero));
        Assert.True(matcher.Update(Snapshot(GamepadButton.A, GamepadButton.B, GamepadButton.X), TimeSpan.Zero));
    }

    [Fact]
    public void Ordered_RequiresPressAndHoldInExactOrder()
    {
        GamepadBindingMatcher matcher = GamepadBinding.Parse("A+Y+B", "test", 0.5f).CreateMatcher(0.5f);
        Assert.False(matcher.Update(Snapshot(GamepadButton.A), TimeSpan.Zero));
        Assert.False(matcher.Update(Snapshot(GamepadButton.A, GamepadButton.Y), TimeSpan.Zero));
        Assert.True(matcher.Update(Snapshot(GamepadButton.A, GamepadButton.Y, GamepadButton.B), TimeSpan.Zero));

        matcher.Reset();
        Assert.False(matcher.Update(Snapshot(GamepadButton.Y), TimeSpan.Zero));
        Assert.False(matcher.Update(Snapshot(GamepadButton.Y, GamepadButton.A), TimeSpan.Zero));
    }

    [Fact]
    public void HoldThenPress_MultipliesDefaultThresholdByStarCount()
    {
        GamepadBindingMatcher matcher = GamepadBinding.Parse("DPadUp**B", "test", 0.5f).CreateMatcher(0.5f);
        for (int index = 0; index < 9; index++)
            Assert.False(matcher.Update(Snapshot(GamepadButton.DPadUp), TimeSpan.FromMilliseconds(100)));
        Assert.False(matcher.Update(Snapshot(GamepadButton.DPadUp), TimeSpan.FromMilliseconds(100)));
        Assert.True(matcher.Update(Snapshot(GamepadButton.DPadUp, GamepadButton.B), TimeSpan.Zero));
    }

    [Theory]
    [InlineData("Back")]
    [InlineData("Guide-A")]
    [InlineData("Start+Y")]
    public void ReservedButtons_AreRejected(string expression) =>
        Assert.Throws<InvalidOperationException>(() => GamepadBinding.Parse(expression, "test", 0.5f));

    [Fact]
    public void KeyboardShortcut_ParsesGenericChord()
    {
        KeyboardShortcut shortcut = KeyboardShortcut.Parse("CTRL+ALT+DEL", "test");
        Assert.Equal([KeyboardKey.Ctrl, KeyboardKey.Alt, KeyboardKey.Delete], shortcut.Keys);
    }

    [Theory]
    [InlineData("CTRL+")]
    [InlineData("+TAB")]
    public void MalformedKeyboardShortcuts_AreRejected(string expression) =>
        Assert.Throws<InvalidOperationException>(() => KeyboardShortcut.Parse(expression, "test"));

    [Theory]
    [InlineData("CONTROL+A", KeyboardKey.Ctrl)]
    [InlineData("DEL", KeyboardKey.Delete)]
    [InlineData("WIN+1", KeyboardKey.Meta)]
    [InlineData("CMD+A", KeyboardKey.Meta)]
    [InlineData("SUPER+A", KeyboardKey.Meta)]
    public void KeyboardShortcut_AcceptsDocumentedAliases(string expression, KeyboardKey firstKey) =>
        Assert.Equal(firstKey, KeyboardShortcut.Parse(expression, "test").Keys[0]);

    [Theory]
    [InlineData("")]
    [InlineData("F1")]
    [InlineData("CTRL+CTRL")]
    public void InvalidKeyboardShortcuts_AreRejected(string expression) =>
        Assert.Throws<InvalidOperationException>(() => KeyboardShortcut.Parse(expression, "test"));

    [Theory]
    [InlineData("A/")]
    [InlineData("/A")]
    [InlineData("A++B")]
    [InlineData("A--B")]
    public void MalformedMappings_AreRejected(string expression) =>
        Assert.Throws<InvalidOperationException>(() => GamepadBinding.Parse(expression, "test", 0.5f));

    [Theory]
    [InlineData("")]
    [InlineData("A+B-C")]
    [InlineData("A*B+C")]
    [InlineData("A-A")]
    [InlineData("A*B*C")]
    [InlineData("LeftX")]
    [InlineData("UnknownButton")]
    public void InvalidBindings_AreRejected(string expression) =>
        Assert.Throws<InvalidOperationException>(() => GamepadBinding.Parse(expression, "test", 0.5f));

    [Fact]
    public void TriggerControl_UsesConfiguredAnalogThreshold()
    {
        GamepadControl trigger = GamepadControl.Parse("LeftTrigger", "test");
        float[] axes = new float[Enum.GetValues<GamepadAxis>().Length];
        axes[(int)GamepadAxis.LeftTrigger] = 0.6f;

        Assert.True(trigger.IsPressed(new GamepadSnapshot(axes, new bool[Enum.GetValues<GamepadButton>().Length]), 0.5f));
        Assert.Equal("LeftTrigger", trigger.ToString());
    }

    private static GamepadSnapshot Snapshot(params GamepadButton[] buttons)
    {
        bool[] pressed = new bool[Enum.GetValues<GamepadButton>().Length];
        foreach (GamepadButton button in buttons) pressed[(int)button] = true;
        return new(new float[Enum.GetValues<GamepadAxis>().Length], pressed);
    }
}
