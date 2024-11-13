using SharpDX.Win32;
using Silk.NET.OpenXR;
using System;
using System.Text;

namespace FfxivVR;

public unsafe class VRInput(XR xr, VRSystem system)
{
    private readonly XR xr = xr;
    private readonly VRSystem system = system;

    private ActionSet actionSet = new ActionSet();
    public void Initialize()
    {
        var handLeft = CreatePath("/user/hand/left");
        var handRight = CreatePath("/user/hand/right");

        CreateActionSet();
        var palmPose = CreateAction(actionType: ActionType.PoseInput, "palm-pose", [handLeft, handRight]);
    }

    private Silk.NET.OpenXR.Action CreateAction(ActionType actionType, string name, ulong[] paths)
    {
        var actionCreateInfo = new ActionCreateInfo(next: null, actionType: actionType);
        Native.WriteCString(actionCreateInfo.ActionName, name, 64);
        Native.WriteCString(actionCreateInfo.LocalizedActionName, name, 128);
        var action = new Silk.NET.OpenXR.Action();
        fixed (ulong* ptr = new Span<ulong>(paths))
        {
            actionCreateInfo.CountSubactionPaths = (uint)paths.Length;
            actionCreateInfo.SubactionPaths = ptr;
            xr.CreateAction(actionSet, in actionCreateInfo, ref action).CheckResult("CreateAction");
        }
        return action;
    }

    private void CreateActionSet()
    {
        var createInfo = new ActionSetCreateInfo(next: null);
        Native.WriteCString(createInfo.ActionSetName, "controls", 64);
        Native.WriteCString(createInfo.LocalizedActionSetName, "controls", 64);
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
}