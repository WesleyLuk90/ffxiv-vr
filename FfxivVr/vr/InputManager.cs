using Dalamud.Game.ClientState.GamePad;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using static FfxivVR.Configuration;

namespace FfxivVR;

public class InputManager(
    Configuration configuration,
    VRInput vrInput,
    ResolutionManager resolutionManager
)
{

    public unsafe void UpdateGamepad(GamepadInput* gamepadInput)
    {
        if (vrInput.GetVrInputState() is VrInputState state)
        {
            var pressedActions = ApplyBindings(state);
            ApplyStates(pressedActions, gamepadInput);
            UpdateMousePosition(pressedActions);
        }
    }

    private List<Tuple<VRAction, MOUSE_EVENT_FLAGS, MOUSE_EVENT_FLAGS>> ButtonMappings = new() {
        Tuple.Create(VRAction.MouseButton1, MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP),
        Tuple.Create(VRAction.MouseButton2, MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP),
        Tuple.Create(VRAction.MouseButton3, MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP),
    };
    private unsafe void UpdateMousePosition(VRActionsState pressedActions)
    {
        // Release buttons regardless of mouse position
        foreach (var (action, _, up) in ButtonMappings)
        {
            if (actionStates.GetValueOrDefault(action)?.WasReleased == true)
            {
                SendMouseEvent(up);
            }
        }
        Vector2D<float>? maybePosition = null;
        if (pressedActions.VRActions.Contains(VRAction.EnableRightMouseHold))
        {
            maybePosition = vrInput.GetViewportPosition(AimType.RightHand);
        }
        if (pressedActions.VRActions.Contains(VRAction.EnableLeftMouseHold) && maybePosition == null)
        {
            maybePosition = vrInput.GetViewportPosition(AimType.LeftHand);
        }
        if (configuration.HeadMouseControl && maybePosition == null)
        {
            maybePosition = vrInput.GetViewportPosition(AimType.Head);
        }
        if (maybePosition is not { } position)
        {
            return;
        }
        if (resolutionManager.WindowToScreen(position) is not { } screenCoordinates)
        {
            return;
        }
        PInvoke.SetCursorPos(screenCoordinates.X, screenCoordinates.Y);
        // Only trigger mouse buttons after we've checked that the mouse is on screen
        foreach (var (action, down, _) in ButtonMappings)
        {
            if (actionStates.GetValueOrDefault(action)?.WasPressed == true)
            {
                SendMouseEvent(down);
            }
        }
    }

    private unsafe void SendMouseEvent(MOUSE_EVENT_FLAGS flags)
    {
        var input = new INPUT
        {
            type = INPUT_TYPE.INPUT_MOUSE,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = flags
                }
            }
        };
        if (PInvoke.SendInput([input], sizeof(INPUT)) != 1)
        {
            throw new Exception("Failed to send input");
        }
    }

    private VRActionsState ApplyBindings(VrInputState state)
    {
        var layer = GetLayer(state);
        var actions = new VRActionsState();
        actions.LeftStick = state.LeftStick;
        actions.RightStick = state.RightStick;
        foreach (var pressedButton in state.Pressed)
        {
            var pressedAction = layer.GetAction(pressedButton);
            actions.VRActions.Add(pressedAction);
            if (pressedAction == VRAction.LeftStickDPad)
            {
                if (StickToDPad(state.LeftStick) is VRAction action)
                {
                    actions.VRActions.Add(action);
                }
                actions.LeftStick = new();
            }
            if (pressedAction == VRAction.RightStickDPad)
            {
                if (StickToDPad(state.RightStick) is VRAction action)
                {
                    actions.VRActions.Add(action);
                }
                actions.RightStick = new();
            }
        }
        return actions;
    }

    private VRAction? StickToDPad(Vector2D<float> stick)
    {
        if (stick.Length < 0.9)
        {
            return null;
        }
        var rotation = MathF.Atan2(stick.Y, stick.X);
        if (rotation > MathF.PI * 3 / 4 || rotation < -MathF.PI * 3 / 4)
        {
            return VRAction.Left;
        }
        else if (rotation > MathF.PI * 1 / 4)
        {
            return VRAction.Up;
        }
        else if (rotation < -MathF.PI * 1 / 4)
        {
            return VRAction.Down;
        }
        else
        {
            return VRAction.Right;
        }
    }

    private List<VRButton> LayerStack = new();
    private ControlLayer GetLayer(VrInputState state)
    {
        int layer = 0;
        for (int i = 0; i < LayerStack.Count; i++)
        {
            var button = LayerStack[i];
            if (!state.Pressed.Contains(button))
            {
                LayerStack.RemoveRange(i, LayerStack.Count - i);
                break;
            }
            var action = configuration.Controls[layer].GetAction(button);
            switch (action)
            {
                // Only allow moving to a higher layer to avoid loops
                case VRAction.Layer2: layer = int.Max(layer, 1); continue;
                case VRAction.Layer3: layer = int.Max(layer, 2); continue;
                case VRAction.Layer4: layer = int.Max(layer, 3); continue;
                default:
                    LayerStack.RemoveRange(i, LayerStack.Count - i);
                    break;
            }
        }

        bool ApplyNextLayer(VRButton button)
        {
            var pressedAction = configuration.Controls[layer].GetAction(button);
            switch (pressedAction)
            {
                case VRAction.Layer2:
                    layer = int.Max(layer, 1);
                    LayerStack.Add(button);
                    return true;
                case VRAction.Layer3:
                    layer = int.Max(layer, 2);
                    LayerStack.Add(button);
                    return true;
                case VRAction.Layer4:
                    layer = int.Max(layer, 3);
                    LayerStack.Add(button);
                    return true;
                default: return false;
            }
        }
        foreach (var pressedButton in state.Pressed)
        {
            if (ApplyNextLayer(pressedButton))
            {
                break;
            }
        }

        return configuration.Controls[layer];
    }

    class VRActionsState
    {
        public Vector2D<float> LeftStick = new();
        public Vector2D<float> RightStick = new();
        public HashSet<VRAction> VRActions = new();
    }

    class ActionState
    {
        Stopwatch stopwatch = new();
        int nextRepeat = 1;
        public bool IsActive = false;
        public bool WasPressed = false;
        public bool WasReleased = false;

        private int NextRepeatMillis()
        {
            return 210 + nextRepeat * 50;
        }

        public unsafe void Update(GamepadInput* gamepadInput, bool isPressed, GamepadButtons bit)
        {
            WasPressed = false;
            WasReleased = false;
            if (isPressed)
            {
                if (!IsActive)
                {
                    gamepadInput->ButtonsPressed |= (ushort)bit;
                    stopwatch.Restart();
                    nextRepeat = 1;
                    WasPressed = true;
                }

                gamepadInput->ButtonsRaw |= (ushort)bit;
                if (DoRepeat())
                {
                    gamepadInput->ButtonsRepeat |= (ushort)bit;
                }
            }
            else
            {
                if (IsActive)
                {
                    gamepadInput->ButtonsReleased = (ushort)bit;
                    WasReleased = true;
                }
            }
            IsActive = isPressed;
        }
        private bool DoRepeat()
        {
            if (stopwatch.ElapsedMilliseconds > NextRepeatMillis())
            {
                nextRepeat++;
                return true;
            }
            return false;
        }
    }

    private Dictionary<VRAction, ActionState> actionStates = new();
    private unsafe void ApplyStates(VRActionsState vrActions, GamepadInput* gamepadInput)
    {
        gamepadInput->LeftStickX = (int)(vrActions.LeftStick.X * 99);
        gamepadInput->LeftStickY = (int)(vrActions.LeftStick.Y * 99);
        gamepadInput->RightStickX = (int)(vrActions.RightStick.X * 99);
        gamepadInput->RightStickY = (int)(vrActions.RightStick.Y * 99);

        gamepadInput->ButtonsPressed = 0;
        gamepadInput->ButtonsReleased = 0;
        gamepadInput->ButtonsRaw = 0;
        gamepadInput->ButtonsRepeat = 0;
        foreach (var action in Enum.GetValues<VRAction>())
        {
            if (!actionStates.ContainsKey(action))
            {
                actionStates[action] = new();
            }
            actionStates[action].Update(gamepadInput, vrActions.VRActions.Contains(action), GetButton(action));
        }
    }

    private GamepadButtons GetButton(VRAction action)
    {
        switch (action)
        {
            case VRAction.A: return GamepadButtons.South;
            case VRAction.B: return GamepadButtons.East;
            case VRAction.X: return GamepadButtons.West;
            case VRAction.Y: return GamepadButtons.North;
            case VRAction.Up: return GamepadButtons.DpadUp;
            case VRAction.Down: return GamepadButtons.DpadDown;
            case VRAction.Left: return GamepadButtons.DpadLeft;
            case VRAction.Right: return GamepadButtons.DpadRight;
            case VRAction.L1: return GamepadButtons.L1;
            case VRAction.L2: return GamepadButtons.L2;
            case VRAction.L3: return GamepadButtons.L3;
            case VRAction.R1: return GamepadButtons.R1;
            case VRAction.R2: return GamepadButtons.R2;
            case VRAction.R3: return GamepadButtons.R3;
            case VRAction.Start: return GamepadButtons.Start;
            case VRAction.Select: return GamepadButtons.Select;
            default: return 0;
        }
    }

    class LineAndPosition(
        Vector2D<int> screenPosition,
        Line line
    )
    {
        public Vector2D<int> ScreenPosition { get; } = screenPosition;
        public Line Line { get; } = line;
    }
    internal List<AimType> GetActiveAimTypes()
    {
        var aimTypes = new List<AimType>();
        if (actionStates.GetValueOrDefault(VRAction.EnableLeftMouseHold)?.IsActive ?? false)
        {
            aimTypes.Add(AimType.LeftHand);
        }
        if (actionStates.GetValueOrDefault(VRAction.EnableRightMouseHold)?.IsActive ?? false)
        {
            aimTypes.Add(AimType.RightHand);
        }
        return aimTypes;
    }


    private LineAndPosition? GetLineAndPosition(AimType hand)
    {
        if (vrInput.GetViewportPosition(hand) is not { } position)
        {
            return null;
        }
        if (resolutionManager.WindowToScreen(position) is not { } screenCoordinates)
        {
            return null;
        }
        if (vrInput.GetAimLine(hand) is not { } aimLine)
        {
            return null;
        }
        return new LineAndPosition(screenCoordinates, aimLine);
    }

    internal Line? GetAimLine(AimType exclude)
    {
        return GetActiveAimTypes()
            .Where(h => h != exclude)
            .Select(h => GetLineAndPosition(h))
            .OfType<LineAndPosition>()
            .Select(l => l.Line)
            .FirstOrDefault();
    }
}