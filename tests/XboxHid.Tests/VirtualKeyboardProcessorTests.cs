using XboxHid.Overlay;

namespace XboxHid.Tests;

public sealed class VirtualKeyboardProcessorTests
{
    [Fact]
    public void DaisyWheel_SelectsDirectionThenFaceButton()
    {
        VirtualKeyboardProcessor processor = new(VirtualKeyboardMode.DaisyWheel);

        Assert.Empty(processor.Process(Input(y: 0.8f)));
        Assert.Equal(["a"], processor.Process(Input(y: 0.8f, xButton: true)));
    }

    [Fact]
    public void CharacterCycling_CommitsWhenStickReturnsToCenter()
    {
        VirtualKeyboardProcessor processor = new(VirtualKeyboardMode.CharacterCycling);

        processor.Process(Input(y: 0.6f));
        processor.Process(Input(y: 1f));

        Assert.Equal(["b"], processor.Process(Input()));
    }

    [Fact]
    public void GlyphStick_RecognizesCardinalPath()
    {
        VirtualKeyboardProcessor processor = new(VirtualKeyboardMode.GlyphStick);

        processor.Process(Input(y: 0.8f));
        processor.Process(Input(x: 0.8f));

        Assert.Equal(["i"], processor.Process(Input()));
    }

    [Fact]
    public void CommonDPadKeys_EmitEditingActionsAndToggleLayer()
    {
        VirtualKeyboardProcessor processor = new(VirtualKeyboardMode.DaisyWheel);

        Assert.Equal(["\b"], processor.Process(Input(dPadLeft: true)));
        processor.Process(Input());
        Assert.Equal([" "], processor.Process(Input(dPadDown: true)));
        processor.Process(Input());
        Assert.Equal(["\n"], processor.Process(Input(dPadRight: true)));
        processor.Process(Input());
        processor.Process(Input(dPadUp: true));

        Assert.True(processor.ViewState.NumberLayer);
    }

    private static VirtualKeyboardInput Input(
        float x = 0,
        float y = 0,
        bool xButton = false,
        bool dPadUp = false,
        bool dPadDown = false,
        bool dPadLeft = false,
        bool dPadRight = false) =>
        new(x, y, false, false, xButton, false, dPadUp, dPadDown, dPadLeft, dPadRight);
}
