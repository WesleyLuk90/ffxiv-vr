using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public unsafe partial class VRInput(
    XR xr,
    VRSystem system,
    Logger logger,
    VRSpace vrSpace,
    Configuration config,
    VRUI vrUI
) : IDisposable
{
    private ActionSet actionSet = new ActionSet();
    private ulong leftHandPath;
    private ulong rightHandPath;
    private Silk.NET.OpenXR.Action palmPose;
    private Silk.NET.OpenXR.Action aimPose;
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
    private Space leftSpace = new Space();
    private Space rightSpace = new Space();


    public void Dispose()
    {
        xr.DestroyActionSet(actionSet).LogResult("DestroyActionSet", logger);
    }

    public void Initialize()
    {
        CreateActionSet();
        leftHandPath = CreatePath("/user/hand/left");
        rightHandPath = CreatePath("/user/hand/right");
        palmPose = CreateAction(actionType: ActionType.PoseInput, "palm-pose", [leftHandPath, rightHandPath]);
        aimPose = CreateAction(actionType: ActionType.PoseInput, "aim-pose", [leftHandPath, rightHandPath]);

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
            CreateSuggestedBinding(palmPose, "/user/hand/left/input/palm_ext/pose"),
            CreateSuggestedBinding(palmPose, "/user/hand/right/input/palm_ext/pose"),

            CreateSuggestedBinding(aimPose, "/user/hand/left/input/aim/pose"),
            CreateSuggestedBinding(aimPose, "/user/hand/right/input/aim/pose"),

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

    public PalmPose? GetPalmPose()
    {
        return lastPalmPose;
    }

    // Need to be careful to only call this in one place because SyncAction is stateful
    private VrInputState PollActions(long predictedTime)
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
        var result = xr.SyncAction(system.Session, ref syncInfo);
        if (result == Result.SessionNotFocused)
        {
            return new VrInputState();
        }
        result.CheckResult("SyncAction");
        lastPalmPose = new PalmPose(
            leftPalm: GetActionPose(leftSpace, predictedTime, palmPose, leftHandPath),
            rightPalm: GetActionPose(rightSpace, predictedTime, palmPose, rightHandPath)
        );
        lastAimPose = new AimPose(
            leftAim: GetActionPose(leftSpace, predictedTime, aimPose, leftHandPath),
            rightAim: GetActionPose(rightSpace, predictedTime, aimPose, rightHandPath),
            headAim: GetHeadAim(predictedTime)
        );
        GetActionBool(aButton, input, VRButton.A);
        GetActionBool(bButton, input, VRButton.B);
        GetActionBool(xButton, input, VRButton.X);
        GetActionBool(yButton, input, VRButton.Y);
        GetActionBool(startButton, input, VRButton.Start);
        GetActionBool(selectButton, input, VRButton.Select);
        GetActionBool(leftTrigger, input, VRButton.LeftTrigger);
        GetActionBool(rightTrigger, input, VRButton.RightTrigger);
        GetActionBool(leftBumper, input, VRButton.LeftGrip);
        GetActionBool(rightBumper, input, VRButton.RightGrip);
        GetActionBool(leftStickPush, input, VRButton.LeftStick);
        GetActionBool(rightStickPush, input, VRButton.RightStick);

        var left = GetActionVector2f(leftAnalog);
        var right = GetActionVector2f(rightAnalog);
        if (left.Length > config.LeftStickDeadzone)
        {
            input.LeftStick = left;
        }

        if (right.Length > config.RightStickDeadzone)
        {
            input.RightStick = right;
        }
        return input;
    }

    private Posef? GetHeadAim(long predictedTime)
    {
        var headPose = new SpaceLocation(next: null);
        xr.LocateSpace(vrSpace.ViewSpace, vrSpace.LocalSpace, predictedTime, ref headPose).CheckResult("LocateSpace");
        return headPose.Pose;
    }

    private void GetActionBool(Silk.NET.OpenXR.Action action, VrInputState inputState, VRButton vrButton)
    {
        var getInfo = new ActionStateGetInfo(
            action: action
        );
        var state = new ActionStateBoolean(next: null);
        xr.GetActionStateBoolean(system.Session, ref getInfo, ref state).CheckResult("GetActionStateBoolean");
        if (state.CurrentState == 1)
        {
            inputState.Pressed.Add(vrButton);
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

    private Posef? GetActionPose(Space space, long predictedTime, Silk.NET.OpenXR.Action action, ulong path)
    {
        var getInfo = new ActionStateGetInfo(
            action: action,
            subactionPath: path
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
            action: palmPose,
            subactionPath: leftHandPath
        );
        xr.CreateActionSpace(system.Session, ref leftCreateInfo, ref leftSpace).CheckResult("CreateActionSpace");
        var rightCreateInfo = new ActionSpaceCreateInfo(
            poseInActionSpace: new Posef(
                position: new Vector3f(0, 0, 0),
                orientation: new Quaternionf(0, 0, 0, 1)
            ),
            action: palmPose,
            subactionPath: rightHandPath
        );
        xr.CreateActionSpace(system.Session, ref rightCreateInfo, ref rightSpace).CheckResult("CreateActionSpace");
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

    private Silk.NET.OpenXR.Action CreateAction(ActionType actionType, string name, ulong[]? paths = null)
    {
        var actionCreateInfo = new ActionCreateInfo(next: null, actionType: actionType);
        Native.WriteCString(actionCreateInfo.ActionName, name, 64);
        Native.WriteCString(actionCreateInfo.LocalizedActionName, name, 128);
        var action = new Silk.NET.OpenXR.Action();
        if (paths is ulong[] p)
        {
            fixed (ulong* pointer = new Span<ulong>(p))
            {
                actionCreateInfo.CountSubactionPaths = (uint)p.Length;
                actionCreateInfo.SubactionPaths = pointer;
                xr.CreateAction(actionSet, in actionCreateInfo, ref action).CheckResult("CreateAction");
            }
        }
        else
        {
            actionCreateInfo.CountSubactionPaths = 0;
            actionCreateInfo.SubactionPaths = null;
            xr.CreateAction(actionSet, in actionCreateInfo, ref action).CheckResult("CreateAction");
        }
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

    class CurrentController
    {
        public bool IsPhysicalController = false;
    }
    private CurrentController? currentController = null;
    private PalmPose? lastPalmPose = null;
    private AimPose? lastAimPose = null;

    internal void InteractionProfileChanged()
    {
        var leftProfile = new InteractionProfileState(next: null);
        xr.GetCurrentInteractionProfile(system.Session, leftHandPath, ref leftProfile).CheckResult("GetCurrentInteractionProfile");
        var rightProfile = new InteractionProfileState(next: null);
        xr.GetCurrentInteractionProfile(system.Session, leftHandPath, ref rightProfile).CheckResult("GetCurrentInteractionProfile");

        logger.Debug($"Interaction profile changed left:{GetPath(leftProfile.InteractionProfile)} right:{GetPath(leftProfile.InteractionProfile)}");

        if (leftProfile.InteractionProfile != 0 || rightProfile.InteractionProfile != 0)
        {
            currentController = new CurrentController();
        }
    }

    public virtual VrInputState? GetVrInputState()
    {
        if (PollActions(system.Now()) is VrInputState input && currentController is CurrentController controller)
        {
            // Virtual Desktop tries to emulate a controller with hand tracking but we want to ignore those inputs so detect that by waiting for a non emulated input
            if (!controller.IsPhysicalController && input.IsPhysicalController())
            {
                if (input.LeftStick.LengthSquared == 0 && input.RightStick.LengthSquared == 0)
                {
                    logger.Debug($"Physical controller detected {string.Join(", ", input.Pressed)}");
                }
                controller.IsPhysicalController = true;
            }
            if (controller.IsPhysicalController)
            {
                return input;
            }
        }
        return null;
    }

    public Line? GetAimLine(AimType aimType)
    {
        var ray = GetAimRay(aimType);
        if (ray == null)
        {
            return null;
        }
        return vrUI.Intersect(ray);
    }
    public Vector2D<float>? GetViewportPosition(AimType aimType)
    {
        var ray = GetAimRay(aimType);
        if (ray == null)
        {
            return null;
        }
        var line = vrUI.Intersect(ray);
        return vrUI.GetViewportPosition(line);
    }

    private Ray? GetAimRay(AimType aimType)
    {
        Posef? maybeAim = null;
        switch (aimType)
        {
            case AimType.LeftHand:
                {
                    maybeAim = lastAimPose?.LeftAim;
                    break;
                }
            case AimType.RightHand:
                {
                    maybeAim = lastAimPose?.RightAim;
                    break;
                }
            case AimType.Head:
                {
                    maybeAim = lastAimPose?.HeadAim;
                    break;
                }
        }
        if (maybeAim is not { } aim)
        {
            return null;
        }
        var start = aim.Position.ToVector3D();
        var rotation = aim.Orientation.ToQuaternion();
        var direction = Vector3D.Transform(new Vector3D<float>(0, 0, -1), rotation);
        return new Ray(start, direction);
    }
}