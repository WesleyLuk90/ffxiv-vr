using Silk.NET.OpenXR;
using System;
using System.Text;

namespace FfxivVR;

public unsafe class VRInput(XR xr, VRSystem system)
{
    private readonly XR xr = xr;

    private ActionSet actionSet = new ActionSet();
    private Silk.NET.OpenXR.Action action = new Silk.NET.OpenXR.Action();
    public void Initialize()
    {
        var createInfo = new ActionSetCreateInfo(next: null);
        Encoding.UTF8.GetBytes("controls\0", new Span<byte>(createInfo.ActionSetName, 64));
        Encoding.UTF8.GetBytes("controls\0", new Span<byte>(createInfo.LocalizedActionSetName, 64));
        xr.CreateActionSet(
            system.Instance,
            in createInfo,
            ref actionSet).CheckResult("CreateActionSet");
        var actionCreateInfo = new ActionCreateInfo(next: null);
        xr.CreateAction(actionSet, in actionCreateInfo, ref action).CheckResult("CreateAction");
    }
}