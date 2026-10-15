using XboxHid.Overlay;

namespace XboxHid.Tests;

public sealed class ControllerOptionsTests
{
    [Fact]
    public void Load_UsesNewDefaultsAndOppositeOverlayStick()
    {
        string path = WriteIni("[Mouse]\n");
        try
        {
            ControllerOptions options = ControllerOptions.Load(path);
            Assert.Equal(PrimaryStick.Right, options.PrimaryStick);
            Assert.Equal("LeftTrigger", options.LeftButton.Expression);
            Assert.Equal("RightTrigger", options.RightButton.Expression);
            Assert.Equal("RightStick", options.MiddleButton.Expression);
            Assert.Equal("RightShoulder", options.DragDropModifier.Expression);
            Assert.Equal("RightShoulder", options.BoostButton.Expression);
            Assert.Equal(0.5f, options.DefaultThreshold);
            Assert.Equal(1200, options.Speed);
            Assert.False(options.InvertedXAxis);
            Assert.False(options.InvertedYAxis);
            Assert.Equal(VirtualKeyboardStick.Left, options.Overlay.InputStick);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_LeftPrimaryStickSelectsRightOverlayStick()
    {
        string path = WriteIni("[Mouse]\nPrimaryStick=Left\n");
        try { Assert.Equal(VirtualKeyboardStick.Right, ControllerOptions.Load(path).Overlay.InputStick); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_ParsesMappingGrammarAndKeyboardShortcuts()
    {
        string path = WriteIni(
            "[Mouse]\nLeftButton=A/B\nRightButton=DPadUp-B\nMiddleButton=X+Y\nDefaultThreshold=0.25\n" +
            "[Actions]\nDPadLeft**A=CTRL+ALT+DEL\n");
        try
        {
            ControllerOptions options = ControllerOptions.Load(path);
            Assert.Equal(2, options.LeftButton.Alternatives.Count);
            Assert.Equal(BindingGestureKind.Simultaneous, options.RightButton.Alternatives.Single().Kind);
            Assert.Equal(BindingGestureKind.Ordered, options.MiddleButton.Alternatives.Single().Kind);
            Assert.Equal(TimeSpan.FromSeconds(0.5), options.Actions.Single().Binding.Alternatives.Single().RequiredHold);
            Assert.Equal([KeyboardKey.Ctrl, KeyboardKey.Alt, KeyboardKey.Delete], options.Actions.Single().Shortcut.Keys);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("MovementStick=Right")]
    [InlineData("LeftClickButton=A")]
    [InlineData("PrimaryClickAlternative=LeftTrigger")]
    [InlineData("DragDropButton=A")]
    [InlineData("InvertVertical=true")]
    [InlineData("SpeedPixelsPerSecond=500")]
    [InlineData("BoostControl=A")]
    public void Load_RejectsRemovedKeys(string setting)
    {
        string path = WriteIni($"[Mouse]\n{setting}\n");
        try { Assert.Contains("fue eliminado", Assert.Throws<InvalidOperationException>(() => ControllerOptions.Load(path)).Message); }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("LeftButton=Back")]
    [InlineData("RightButton=Guide")]
    [InlineData("MiddleButton=Start")]
    [InlineData("DragDropModifier=A/Guide")]
    [InlineData("BoostButton=Start-A")]
    public void Load_RejectsReservedMappedButtons(string setting)
    {
        string path = WriteIni($"[Mouse]\n{setting}\n");
        try { Assert.Contains("reservado", Assert.Throws<InvalidOperationException>(() => ControllerOptions.Load(path)).Message); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_RejectsReservedActionButtons()
    {
        string path = WriteIni("[Mouse]\n[Actions]\nA/Start=CTRL+SPACE\n");
        try { Assert.Contains("reservado", Assert.Throws<InvalidOperationException>(() => ControllerOptions.Load(path)).Message); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_RejectsRemovedOverlayInputStick()
    {
        string path = WriteIni("[Mouse]\n[Overlay]\nInputStick=Left\n");
        try { Assert.Contains("fue eliminado", Assert.Throws<InvalidOperationException>(() => ControllerOptions.Load(path)).Message); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ResolvePath_UsesExplicitConfigArgument()
    {
        string path = Path.GetTempFileName();
        try { Assert.Equal(Path.GetFullPath(path), ControllerOptions.ResolvePath(["--config", path])); }
        finally { File.Delete(path); }
    }

    private static string WriteIni(string contents)
    {
        string path = Path.Combine(Path.GetTempPath(), $"xboxhid-{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, contents);
        return path;
    }
}
