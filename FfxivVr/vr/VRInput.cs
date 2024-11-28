using Dalamud.Game.ClientState.GamePad;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public unsafe class VRInput(XR xr, VRSystem system, Logger logger, VRSpace vrSpace, VRState vrState) : IDisposable
{
    private readonly XR xr = xr;
    private readonly VRSystem system = system;
    private readonly Logger logger = logger;
    private readonly VRSpace vrSpace = vrSpace;
    private readonly VRState vrState = vrState;
    private ActionSet actionSet = new ActionSet();
    private ulong leftHandPath;
    private ulong rightHandPath;
    private Silk.NET.OpenXR.Action leftPalmPose;
    private Silk.NET.OpenXR.Action rightPalmPose;
    private Silk.NET.OpenXR.Action aButton;
    private Silk.NET.OpenXR.Action bButton;
    private Silk.NET.OpenXR.Action xButton;
    private Silk.NET.OpenXR.Action yButton;
    private Silk.NET.OpenXR.Action startButton;
    private Silk.NET.OpenXR.Action selectButton;
    private Silk.NET.OpenXR.Action leftTrigger;
    private Silk.NET.OpenXR.Action rightTrigger;
    private Silk.NET.OpenXR.Action leftBumper;
    private Silk.NET.OpenXR.Action rightBumper;
    private Silk.NET.OpenXR.Action leftStickPush;
    private Silk.NET.OpenXR.Action rightStickPush;
    private Silk.NET.OpenXR.Action leftAnalog;
    private Silk.NET.OpenXR.Action rightAnalog;
    private Space leftHandSpace = new Space();
    private Space rightHandSpace = new Space();



    public void Dispose()
    {
        xr.DestroyActionSet(actionSet).LogResult("DestroyActionSet", logger);
    }

    public void Initialize()
    {
        CreateActionSet();
        leftHandPath = CreatePath("/user/hand/left");
        rightHandPath = CreatePath("/user/hand/right");
        leftPalmPose = CreateAction(actionType: ActionType.PoseInput, "left-palm-pose");
        rightPalmPose = CreateAction(actionType: ActionType.PoseInput, "right-palm-pose");

        aButton = CreateAction(actionType: ActionType.BooleanInput, "a-button");
        bButton = CreateAction(actionType: ActionType.BooleanInput, "b-button");
        xButton = CreateAction(actionType: ActionType.BooleanInput, "x-button");
        yButton = CreateAction(actionType: ActionType.BooleanInput, "y-button");
        startButton = CreateAction(actionType: ActionType.BooleanInput, "start-button");
        selectButton = CreateAction(actionType: ActionType.BooleanInput, "select-button");
        leftTrigger = CreateAction(actionType: ActionType.BooleanInput, "left-trigger");
        rightTrigger = CreateAction(actionType: ActionType.BooleanInput, "right-trigger");
        leftBumper = CreateAction(actionType: ActionType.BooleanInput, "left-bumper");
        rightBumper = CreateAction(actionType: ActionType.BooleanInput, "right-bumper");
        leftStickPush = CreateAction(actionType: ActionType.BooleanInput, "left-stick-push");
        rightStickPush = CreateAction(actionType: ActionType.BooleanInput, "right-stick-push");

        leftAnalog = CreateAction(actionType: ActionType.Vector2fInput, "left-analog");
        rightAnalog = CreateAction(actionType: ActionType.Vector2fInput, "right-analog");

        SuggestBindings([
            CreateSuggestedBinding(leftPalmPose, "/user/hand/left/input/grip/pose"),
            CreateSuggestedBinding(rightPalmPose, "/user/hand/right/input/grip/pose"),

            CreateSuggestedBinding(leftAnalog, "/user/hand/left/input/thumbstick"),
            CreateSuggestedBinding(rightAnalog, "/user/hand/right/input/thumbstick"),

            CreateSuggestedBinding(aButton, "/user/hand/right/input/a/click"),
            CreateSuggestedBinding(bButton, "/user/hand/right/input/b/click"),
            CreateSuggestedBinding(xButton, "/user/hand/left/input/x/click"),
            CreateSuggestedBinding(yButton, "/user/hand/left/input/y/click"),

            CreateSuggestedBinding(leftTrigger, "/user/hand/left/input/trigger/value"),
            CreateSuggestedBinding(rightTrigger, "/user/hand/right/input/trigger/value"),
            CreateSuggestedBinding(leftBumper, "/user/hand/left/input/squeeze/value"),
            CreateSuggestedBinding(rightBumper, "/user/hand/right/input/squeeze/value"),
            CreateSuggestedBinding(leftStickPush, "/user/hand/left/input/thumbstick/click"),
            CreateSuggestedBinding(rightStickPush, "/user/hand/right/input/thumbstick/click"),
            CreateSuggestedBinding(selectButton, "/user/hand/left/input/menu/click"),
            CreateSuggestedBinding(startButton, "/user/hand/right/input/system/click"),
        ]);

        CreateActionPoses();
        AttachActionSet();
    }

    public VrInputState PollActions(long predictedTime)
    {
        var input = new VrInputState();
        var activeActionSet = new ActiveActionSet(
            actionSet: actionSet,
            subactionPath: null
        );
        var syncInfo = new ActionsSyncInfo(
            countActiveActionSets: 1,
            activeActionSets: &activeActionSet
        );
        xr.SyncAction(system.Session, ref syncInfo).CheckResult("SyncAction");
        if (GetActionPose(leftHandSpace, predictedTime, leftPalmPose) is Posef leftPose)
        {
            Debugging.DebugShow("Palm Left", leftPose.Position.ToVector3D());
        }
        else
        {
            Debugging.DebugShow("Palm Right", null);
        }
        if (GetActionPose(rightHandSpace, predictedTime, rightPalmPose) is Posef rightPose)
        {
            Debugging.DebugShow("Palm Right", rightPose.Position.ToVector3D());
        }
        else
        {
            Debugging.DebugShow("Palm Right", null);
        }
        GetActionBool(aButton, input, GamepadButtons.South, setChange: true);
        GetActionBool(bButton, input, GamepadButtons.East, setChange: true);
        GetActionBool(xButton, input, GamepadButtons.West, setChange: true);
        GetActionBool(yButton, input, GamepadButtons.North, setChange: true);
        GetActionBool(startButton, input, GamepadButtons.Start, setChange: true);
        GetActionBool(selectButton, input, GamepadButtons.Select, setChange: true);
        GetActionBool(leftTrigger, input, GamepadButtons.L2, setChange: false);
        GetActionBool(rightTrigger, input, GamepadButtons.R2, setChange: false);
        GetActionBool(leftBumper, input, GamepadButtons.L1, setChange: true);
        GetActionBool(rightBumper, input, GamepadButtons.R1, setChange: true);
        GetActionBool(leftStickPush, input, GamepadButtons.L3, setChange: true);
        GetActionBool(rightStickPush, input, GamepadButtons.R3, setChange: true);

        var left = GetActionVector2f(leftAnalog);
        var right = GetActionVector2f(rightAnalog);
        input.LeftStick = left;
        input.RightStick = right;
        return input;
    }

    private void GetActionBool(Silk.NET.OpenXR.Action action, VrInputState inputState, GamepadButtons button, bool setChange)
    {
        var getInfo = new ActionStateGetInfo(
            action: action
        );
        var state = new ActionStateBoolean(next: null);
        xr.GetActionStateBoolean(system.Session, ref getInfo, ref state).CheckResult("GetActionStateBoolean");
        Debugging.DebugShow(button.ToString(), state.IsActive == 1);
        if (state.CurrentState == 1)
        {
            inputState.ButtonsRaw |= button;
        }
        if (state.ChangedSinceLastSync == 1 && setChange)
        {
            if (state.CurrentState == 1)
            {
                inputState.ButtonsPressed |= button;
            }
            else
            {
                inputState.ButtonsReleased |= button;
            }
        }
    }
    private Vector2D<float> GetActionVector2f(Silk.NET.OpenXR.Action action)
    {
        var getInfo = new ActionStateGetInfo(
            action: action
        );
        var state = new ActionStateVector2f(next: null);
        xr.GetActionStateVector2(system.Session, ref getInfo, ref state).CheckResult("GetActionStateBoolean");
        return new Vector2D<float>(state.CurrentState.X, state.CurrentState.Y);
    }

    private Posef? GetActionPose(Space space, long predictedTime, Silk.NET.OpenXR.Action action)
    {
        var getInfo = new ActionStateGetInfo(
            action: action
        );
        var statePose = new ActionStatePose(next: null);
        xr.GetActionStatePose(system.Session, ref getInfo, ref statePose).CheckResult("GetActionStatePose");
        if (statePose.IsActive == 1)
        {
            var spaceLocation = new SpaceLocation(next: null);
            xr.LocateSpace(space, vrSpace.LocalSpace, predictedTime, ref spaceLocation).CheckResult("LocateSpace");
            return spaceLocation.Pose;
        }
        else
        {
            return null;
        }
    }

    private void AttachActionSet()
    {
        var attachInfo = new SessionActionSetsAttachInfo(
            countActionSets: 1
        );
        fixed (ActionSet* ptr = &actionSet)
        {
            attachInfo.ActionSets = ptr;

            xr.AttachSessionActionSets(system.Session, ref attachInfo).CheckResult("AttachSessionActionSets");
        }
    }
    private void CreateActionPoses()
    {
        var leftCreateInfo = new ActionSpaceCreateInfo(
            poseInActionSpace: new Posef(
                position: new Vector3f(0, 0, 0),
                orientation: new Quaternionf(0, 0, 0, 1)
            ),
            action: leftPalmPose
        );
        xr.CreateActionSpace(system.Session, ref leftCreateInfo, ref leftHandSpace).CheckResult("CreateActionSpace");
        var rightCreateInfo = new ActionSpaceCreateInfo(
            poseInActionSpace: new Posef(
                position: new Vector3f(0, 0, 0),
                orientation: new Quaternionf(0, 0, 0, 1)
            ),
            action: leftPalmPose
        );
        xr.CreateActionSpace(system.Session, ref rightCreateInfo, ref rightHandSpace).CheckResult("CreateActionSpace");
    }

    private ActionSuggestedBinding CreateSuggestedBinding(Silk.NET.OpenXR.Action action, string actionPath)
    {
        return new ActionSuggestedBinding(
            action: action,
            binding: CreatePath(actionPath)
        );
    }

    private void SuggestBindings(ActionSuggestedBinding[] bindings)
    {
        var span = new Span<ActionSuggestedBinding>(bindings);
        fixed (ActionSuggestedBinding* ptr = span)
        {
            var suggestedBinding = new InteractionProfileSuggestedBinding(
                interactionProfile: CreatePath("/interaction_profiles/oculus/touch_controller"),
                countSuggestedBindings: (uint?)bindings.Length,
                suggestedBindings: ptr
            );
            var result = xr.SuggestInteractionProfileBinding(system.Instance, ref suggestedBinding);
            if (result != Result.Success)
            {
                logger.Debug($"Failed to suggest bindings {result}");
            }
        }
    }

    private Silk.NET.OpenXR.Action CreateAction(ActionType actionType, string name)
    {
        var actionCreateInfo = new ActionCreateInfo(next: null, actionType: actionType);
        Native.WriteCString(actionCreateInfo.ActionName, name, 64);
        Native.WriteCString(actionCreateInfo.LocalizedActionName, name, 128);
        var action = new Silk.NET.OpenXR.Action();
        actionCreateInfo.CountSubactionPaths = 0;
        actionCreateInfo.SubactionPaths = null;
        xr.CreateAction(actionSet, in actionCreateInfo, ref action).CheckResult("CreateAction");
        return action;
    }

    private void CreateActionSet()
    {
        var createInfo = new ActionSetCreateInfo(next: null);
        Native.WriteCString(createInfo.ActionSetName, "action-set", 64);
        Native.WriteCString(createInfo.LocalizedActionSetName, "action-set", 128);
        xr.CreateActionSet(
            system.Instance,
            in createInfo,
            ref actionSet).CheckResult("CreateActionSet");
    }

    private ulong CreatePath(string path)
    {
        ulong xrPath = 0;
        Native.WithStringPointer(path, (ptr) =>
        {
            xr.StringToPath(system.Instance, (byte*)ptr, ref xrPath).CheckResult("StringToPath");
        });
        return xrPath;
    }

    private string? GetPath(ulong path)
    {
        if (path == 0)
        {
            return null;
        }
        uint bufferSize = 0;
        xr.PathToString(system.Instance, path, ref bufferSize, null).CheckResult("PathToString");
        var buffer = new byte[bufferSize];
        xr.PathToString(system.Instance, path, ref bufferSize, buffer).CheckResult("PathToString");
        return Native.ReadCString(buffer);
    }

    private bool ResetController = false;
    private bool VRControllerActive = false;
    internal void InteractionProfileChanged()
    {
        var leftProfile = new InteractionProfileState(next: null);
        xr.GetCurrentInteractionProfile(system.Session, leftHandPath, ref leftProfile).CheckResult("GetCurrentInteractionProfile");
        var rightProfile = new InteractionProfileState(next: null);
        xr.GetCurrentInteractionProfile(system.Session, leftHandPath, ref rightProfile).CheckResult("GetCurrentInteractionProfile");

        logger.Debug($"Interaction profile changed left:{GetPath(leftProfile.InteractionProfile)} right:{GetPath(leftProfile.InteractionProfile)}");

        VRControllerActive = leftProfile.InteractionProfile != 0 || rightProfile.InteractionProfile != 0;
        if (leftProfile.InteractionProfile == 0 || rightProfile.InteractionProfile == 0)
        {
            ResetController = true;
        }
    }

    internal void UpdateGamepad(GamepadInput* gamepadInput, bool handTrackingActive)
    {
        if (vrState.State != SessionState.Focused || ResetController)
        {
            gamepadInput->LeftStickX = 0;
            gamepadInput->LeftStickY = 0;
            gamepadInput->RightStickX = 0;
            gamepadInput->RightStickY = 0;
            gamepadInput->ButtonsPressed = 0;
            gamepadInput->ButtonsReleased = 0;
            gamepadInput->ButtonsRaw = 0;
            ResetController = false;
        }
        // Virtual seems to emulate the controller if hand tracking is active so disable VR controllers in this case
        else if (PollActions(system.Now()) is VrInputState input && !handTrackingActive && VRControllerActive)
        {
            gamepadInput->LeftStickX = (int)(input.LeftStick.X * 99);
            gamepadInput->LeftStickY = (int)(input.LeftStick.Y * 99);
            gamepadInput->RightStickX = (int)(input.RightStick.X * 99);
            gamepadInput->RightStickY = (int)(input.RightStick.Y * 99);

            gamepadInput->ButtonsPressed = (ushort)input.ButtonsPressed;
            gamepadInput->ButtonsReleased = (ushort)input.ButtonsReleased;
            gamepadInput->ButtonsRaw = (ushort)input.ButtonsRaw;
        }
    }
}