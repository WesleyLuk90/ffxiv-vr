using Dalamud.Game.ClientState.GamePad;
using FFXIVClientStructs.FFXIV.Client.System.Input;
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
    ResolutionManager resolutionManager,
    VRUI vrUI
)
{
    public unsafe void UpdateGamepad(GamepadInputData* gamepadInput, VRInputData vrInput)
    {
        var state = vrInput.GetPhysicalActionsState(configuration.DisableControllersWhenTracking);
        var pressedActions = ApplyBindings(state);
        var actionsState = ApplyStates(pressedActions, gamepadInput);
        UpdateMousePosition(actionsState, vrInput.AimPose);
    }

    private List<Tuple<VRAction, MOUSE_EVENT_FLAGS, MOUSE_EVENT_FLAGS>> ButtonMappings = new() {
        Tuple.Create(VRAction.MouseButton1, MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP),
        Tuple.Create(VRAction.MouseButton2, MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP),
        Tuple.Create(VRAction.MouseButton3, MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP),
    };
    private void UpdateMousePosition(Dictionary<VRAction, ActionState> actionsState, AimPose aimPose)
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
        if (actionsState.GetValueOrDefault(VRAction.EnableRightMouseHold)?.IsActive == true)
        {
            maybePosition = vrUI.GetViewportPosition(AimType.RightHand, aimPose);
        }
        if (actionsState.GetValueOrDefault(VRAction.EnableLeftMouseHold)?.IsActive == true && maybePosition == null)
        {
            maybePosition = vrUI.GetViewportPosition(AimType.LeftHand, aimPose);
        }
        if (configuration.HeadMouseControl && maybePosition == null)
        {
            maybePosition = vrUI.GetViewportPosition(AimType.Head, aimPose);
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

    private VRActionsState ApplyBindings(FfxivVR.VRActionsState state)
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
    private ControlLayer GetLayer(FfxivVR.VRActionsState state)
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

        public unsafe void Update(PadDevice* gamepadInput, bool isPressed, GamepadButtons buttonBit)
        {
            WasPressed = false;
            WasReleased = false;
            if (isPressed)
            {
                if (!IsActive)
                {
                    // gamepadInput->GamepadInputData.ButtonsPressed |= (ushort)buttonBit;
                    stopwatch.Restart();
                    nextRepeat = 1;
                    WasPressed = true;
                }

                // gamepadInput->GamepadInputData.ButtonsRaw |= (ushort)buttonBit;
                if (DoRepeat())
                {
                    // gamepadInput->GamepadInputData.ButtonsRepeat |= (ushort)buttonBit;
                }
            }
            else
            {
                if (IsActive)
                {
                    // gamepadInput->GamepadInputData.ButtonsReleased = (ushort)buttonBit;
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
    private unsafe Dictionary<VRAction, ActionState> ApplyStates(VRActionsState vrActions, GamepadInputData* gamepadInput)
    {
        var internalGamepadInput = InternalGamepadInput.FromGamepadInput(gamepadInput);
        // If the gamepad is not active then the values are never reset from the previous frame so we need to clear them
        if (!internalGamepadInput->IsActive)
        {
            Clear(gamepadInput);
        }
        mergeStickInput(ref gamepadInput->GamepadInputData.LeftStickX, vrActions.LeftStick.X);
        mergeStickInput(ref gamepadInput->GamepadInputData.LeftStickY, vrActions.LeftStick.Y);
        mergeStickInput(ref gamepadInput->GamepadInputData.RightStickX, vrActions.RightStick.X);
        mergeStickInput(ref gamepadInput->GamepadInputData.RightStickY, vrActions.RightStick.Y);

        var pressedGamepad = gamepadInput->GamepadInputData.Buttons;
        foreach (var action in Enum.GetValues<VRAction>())
        {
            if (!actionStates.ContainsKey(action))
            {
                actionStates[action] = new();
            }
            var gamepadButtonBit = GetButton(action);
            // var isPressed = vrActions.VRActions.Contains(action) || (pressedGamepad & (int)gamepadButtonBit) != 0;
            // actionStates[action].Update(gamepadInput, isPressed, gamepadButtonBit);
        }
        return actionStates;
    }

    private static unsafe void Clear(PadDevice* gamepadInput)
    {
        gamepadInput->GamepadInputData.ButtonsPressed = 0;
        // gamepadInput->GamepadInputData.ButtonsRaw = 0;
        // gamepadInput->GamepadInputData.ButtonsReleased = 0;
        // gamepadInput->GamepadInputData.ButtonsRepeat = 0;
        gamepadInput->GamepadInputData.LeftStickX = 0;
        gamepadInput->GamepadInputData.LeftStickY = 0;
        gamepadInput->GamepadInputData.RightStickX = 0;
        gamepadInput->GamepadInputData.RightStickY = 0;
    }

    private void mergeStickInput(ref int gamepadStick, float vrActionStick)
    {
        var vrActionStickInt = (int)(vrActionStick * 99);
        if (int.Abs(gamepadStick) < int.Abs(vrActionStickInt))
        {
            gamepadStick = vrActionStickInt;
        }
    }

    private GamepadButtonsFlags GetButton(VRAction action)
    {
        switch (action)
        {
            // FIXME
            case VRAction.A: return GamepadButtonsFlags.Circle;
            case VRAction.B: return GamepadButtonsFlags.Circle;
            case VRAction.X: return GamepadButtonsFlags.Circle;
            case VRAction.Y: return GamepadButtonsFlags.Circle;
            case VRAction.Up: return GamepadButtonsFlags.DPadUp;
            case VRAction.Down: return GamepadButtonsFlags.DPadDown;
            case VRAction.Left: return GamepadButtonsFlags.DPadLeft;
            case VRAction.Right: return GamepadButtonsFlags.DPadRight;
            case VRAction.L1: return GamepadButtonsFlags.L1;
            case VRAction.L2: return GamepadButtonsFlags.L2;
            case VRAction.L3: return GamepadButtonsFlags.L3;
            case VRAction.R1: return GamepadButtonsFlags.R1;
            case VRAction.R2: return GamepadButtonsFlags.R2;
            case VRAction.R3: return GamepadButtonsFlags.R3;
            case VRAction.Start: return GamepadButtonsFlags.Start;
            case VRAction.Select: return GamepadButtonsFlags.Select;
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


    private LineAndPosition? GetLineAndPosition(AimType hand, VRInputData vrInputData)
    {
        if (vrUI.GetViewportPosition(hand, vrInputData.AimPose) is not { } position)
        {
            return null;
        }
        if (resolutionManager.WindowToScreen(position) is not { } screenCoordinates)
        {
            return null;
        }
        if (vrUI.GetAimLine(hand, vrInputData.AimPose) is not { } aimLine)
        {
            return null;
        }
        return new LineAndPosition(screenCoordinates, aimLine);
    }

    internal Line? GetAimLine(AimType exclude, VRInputData vrInputData)
    {
        return GetActiveAimTypes()
            .Where(h => h != exclude)
            .Select(h => GetLineAndPosition(h, vrInputData))
            .OfType<LineAndPosition>()
            .Select(l => l.Line)
            .FirstOrDefault();
    }
}