
namespace FfxivVR.Tests;

using Dalamud.Game;
using Dalamud.Game.ClientState.GamePad;
using FfxivVR;
using Moq;
using Silk.NET.Maths;

public class InputManagerTests
{

    [Test]
    public unsafe void NoButtons()
    {
        var config = new Configuration();
        var input = MockVRInput();
        var state = new VrInputState();
        input.Setup(m => m.GetVrInputState()).Returns(state);
        var inputManager = MockInputManager(config, input);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, 0, 0, 0, 0);
    }

    private static unsafe InputManager MockInputManager(Configuration config, Mock<VRInput> input)
    {
        return new InputManager(config, input.Object, new Mock<ResolutionManager>(null!, null!, new Mock<ISigScanner>().Object).Object);
    }

    private static unsafe Mock<VRInput> MockVRInput()
    {
        return new Mock<VRInput>(null!, null!, null!, null!, null!, null!);
    }

    [Test]
    public unsafe void SimpleButtons()
    {
        var config = new Configuration();
        var input = MockVRInput();
        var state = new VrInputState();
        input.Setup(m => m.GetVrInputState()).Returns(state);
        state.Pressed.Add(VRButton.A);
        var inputManager = MockInputManager(config, input);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, GamepadButtons.South, GamepadButtons.South, 0, 0);

        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, GamepadButtons.South, 0, 0, 0);

        state.Pressed.Clear();

        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, 0, 0, GamepadButtons.South, 0);

        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, 0, 0, 0, 0);
    }

    [Test]
    public unsafe void DPadSticks()
    {
        var config = new Configuration();
        config.Controls[0].AButton = VRAction.LeftStickDPad;
        var input = MockVRInput();
        var state = new VrInputState();
        input.Setup(m => m.GetVrInputState()).Returns(state);
        state.Pressed.Add(VRButton.A);
        state.LeftStick = new Vector2D<float>(1, 0);
        var inputManager = MockInputManager(config, input);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, GamepadButtons.DpadRight, GamepadButtons.DpadRight, 0, 0);
        state.LeftStick = new Vector2D<float>(0, 1);
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, GamepadButtons.DpadUp, GamepadButtons.DpadUp, GamepadButtons.DpadRight, 0);
    }

    [Test]
    public unsafe void Layer()
    {
        var config = new Configuration();
        config.Controls[0].AButton = VRAction.Layer2;
        config.Controls[1].AButton = VRAction.None;
        config.Controls[1].BButton = VRAction.Start;
        var input = MockVRInput();
        var state = new VrInputState();
        input.Setup(m => m.GetVrInputState()).Returns(state);
        state.Pressed.Add(VRButton.A);
        var inputManager = MockInputManager(config, input);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, 0, 0, 0, 0);
        state.Pressed.Add(VRButton.B);
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, GamepadButtons.Start, GamepadButtons.Start, 0, 0);
    }
    private void AssertGamepad(GamepadInput gamepadInput, GamepadButtons raw, GamepadButtons pressed, GamepadButtons released, GamepadButtons repeat)
    {
        Assert.That((GamepadButtons)gamepadInput.ButtonsRaw, Is.EqualTo(raw));
        Assert.That((GamepadButtons)gamepadInput.ButtonsPressed, Is.EqualTo(pressed));
        Assert.That((GamepadButtons)gamepadInput.ButtonsReleased, Is.EqualTo(released));
        Assert.That((GamepadButtons)gamepadInput.ButtonsRepeat, Is.EqualTo(repeat));
    }
}