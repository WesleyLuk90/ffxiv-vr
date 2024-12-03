
namespace FfxivVR.Tests;

using Dalamud.Game.ClientState.GamePad;
using FfxivVR;
using Silk.NET.Maths;

public class InputManagerTests
{

    class TestInput : IVRInput
    {
        public VrInputState vrInputState = new();
        public VrInputState? GetVrInputState()
        {
            return vrInputState;
        }
    }
    [Test]
    public unsafe void NoButtons()
    {
        var config = new Configuration();
        var input = new TestInput();
        var inputManager = new InputManager(config, input);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, 0, 0, 0, 0);
    }

    [Test]
    public unsafe void SimpleButtons()
    {
        var config = new Configuration();
        var input = new TestInput();
        input.vrInputState.Pressed.Add(VRButton.A);
        var inputManager = new InputManager(config, input);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, GamepadButtons.South, GamepadButtons.South, 0, 0);

        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, GamepadButtons.South, 0, 0, 0);

        input.vrInputState.Pressed.Clear();

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
        var input = new TestInput();
        input.vrInputState.Pressed.Add(VRButton.A);
        input.vrInputState.LeftStick = new Vector2D<float>(1, 0);
        var inputManager = new InputManager(config, input);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, GamepadButtons.DpadRight, GamepadButtons.DpadRight, 0, 0);
        input.vrInputState.LeftStick = new Vector2D<float>(0, 1);
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
        var input = new TestInput();
        input.vrInputState.Pressed.Add(VRButton.A);
        var inputManager = new InputManager(config, input);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput);
        AssertGamepad(gamepadInput, 0, 0, 0, 0);
        input.vrInputState.Pressed.Add(VRButton.B);
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