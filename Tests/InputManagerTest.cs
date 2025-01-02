
namespace FfxivVR.Tests;

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
        var inputManager = MockInputManager(config);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput, CreateVrInputData(new VRActionsState()));
        AssertGamepad(gamepadInput, 0, 0, 0, 0);
    }


    private static VRInputData CreateVrInputData(VRActionsState vrActionsState)
    {
        return new VRInputData(new HandTracking.HandPose(null, null), new PalmPose(null, null), null, new AimPose(null, null, null), vrActionsState);
    }
    private static InputManager MockInputManager(Configuration config)
    {
        var vrUI = new Mock<VRUI>(config, null!).Object;
        return new Mock<InputManager>(config, null!, vrUI).Object;
    }

    [Test]
    public unsafe void SimpleButtons()
    {
        var config = new Configuration();
        var state = new VRActionsState();
        state.Pressed.Add(VRButton.A);
        var inputManager = MockInputManager(config);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput, CreateVrInputData(state));
        AssertGamepad(gamepadInput, GamepadButtons.South, GamepadButtons.South, 0, 0);

        inputManager.UpdateGamepad(&gamepadInput, CreateVrInputData(state));
        AssertGamepad(gamepadInput, GamepadButtons.South, 0, 0, 0);

        state.Pressed.Clear();

        inputManager.UpdateGamepad(&gamepadInput, CreateVrInputData(state));
        AssertGamepad(gamepadInput, 0, 0, GamepadButtons.South, 0);

        inputManager.UpdateGamepad(&gamepadInput, CreateVrInputData(state));
        AssertGamepad(gamepadInput, 0, 0, 0, 0);
    }

    [Test]
    public unsafe void DPadSticks()
    {
        var config = new Configuration();
        config.Controls[0].AButton = VRAction.LeftStickDPad;
        var state = new VRActionsState();
        state.Pressed.Add(VRButton.A);
        state.LeftStick = new Vector2D<float>(1, 0);
        var inputManager = MockInputManager(config);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput, CreateVrInputData(state));
        AssertGamepad(gamepadInput, GamepadButtons.DpadRight, GamepadButtons.DpadRight, 0, 0);
        state.LeftStick = new Vector2D<float>(0, 1);
        inputManager.UpdateGamepad(&gamepadInput, CreateVrInputData(state));
        AssertGamepad(gamepadInput, GamepadButtons.DpadUp, GamepadButtons.DpadUp, GamepadButtons.DpadRight, 0);
    }

    [Test]
    public unsafe void Layer()
    {
        var config = new Configuration();
        config.Controls[0].AButton = VRAction.Layer2;
        config.Controls[1].AButton = VRAction.None;
        config.Controls[1].BButton = VRAction.Start;
        var inputManager = MockInputManager(config);
        var state = new VRActionsState();
        state.Pressed.Add(VRButton.A);
        var gamepadInput = new GamepadInput();
        inputManager.UpdateGamepad(&gamepadInput, CreateVrInputData(state));
        AssertGamepad(gamepadInput, 0, 0, 0, 0);
        state.Pressed.Add(VRButton.B);
        inputManager.UpdateGamepad(&gamepadInput, CreateVrInputData(state));
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