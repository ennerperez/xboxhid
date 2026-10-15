namespace XboxHid.Tests;

public sealed class GamepadSnapshotTests
{
    [Fact]
    public void Constructor_ClonesValidInput()
    {
        float[] axes = new float[Enum.GetValues<GamepadAxis>().Length];
        bool[] buttons = new bool[Enum.GetValues<GamepadButton>().Length];
        axes[(int)GamepadAxis.RightX] = 0.5f;
        buttons[(int)GamepadButton.A] = true;

        GamepadSnapshot snapshot = new(axes, buttons);
        axes[(int)GamepadAxis.RightX] = 0;
        buttons[(int)GamepadButton.A] = false;

        Assert.Equal(0.5f, snapshot.GetAxis(GamepadAxis.RightX));
        Assert.True(snapshot.IsPressed(GamepadButton.A));
    }

    [Fact]
    public void Constructor_RejectsNullArrays()
    {
        Assert.Throws<ArgumentNullException>(() => new GamepadSnapshot(null!, []));
        Assert.Throws<ArgumentNullException>(() => new GamepadSnapshot([], null!));
    }

    [Fact]
    public void Constructor_RejectsWrongArrayLengths()
    {
        Assert.Throws<ArgumentException>(() => new GamepadSnapshot([], new bool[Enum.GetValues<GamepadButton>().Length]));
        Assert.Throws<ArgumentException>(() => new GamepadSnapshot(new float[Enum.GetValues<GamepadAxis>().Length], []));
    }
}
