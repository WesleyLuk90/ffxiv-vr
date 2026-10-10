using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace FfxivVR;

public unsafe class TalkPositionManager(
    IAddonLifecycle addonLifecycle,
    IGameGui gameGui,
    Configuration configuration,
    VRLifecycle vrLifecycle,
    ExceptionHandler exceptionHandler
) : IDisposable
{
    private const string AddonName = "Talk";
    private short? originalY = null;
    private short? appliedY = null;

    public void Initialize()
    {
        addonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, OnPreDraw);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, OnPreFinalize);
    }

    private void OnPreDraw(AddonEvent type, AddonArgs args)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (addon == null)
            {
                return;
            }
            if (appliedY != addon->Y)
            {
                originalY = addon->Y;
                appliedY = null;
            }
            if (originalY is not short y)
            {
                return;
            }
            var targetY = vrLifecycle.IsEnabled() ? (short)Math.Max(0, y - GetOffset()) : y;
            if (addon->Y != targetY)
            {
                addon->SetPosition(addon->X, targetY);
            }
            appliedY = targetY;
        });
    }

    private void OnPreFinalize(AddonEvent type, AddonArgs args)
    {
        originalY = null;
        appliedY = null;
    }

    private int GetOffset()
    {
        var device = Device.Instance();
        if (device == null)
        {
            return 0;
        }
        return (int)(device->Height * configuration.TalkHeightOffset);
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(AddonEvent.PreDraw, AddonName, OnPreDraw);
        addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, AddonName, OnPreFinalize);
        var addon = (AtkUnitBase*)gameGui.GetAddonByName(AddonName).Address;
        if (addon != null && originalY is short y && addon->Y != y)
        {
            addon->SetPosition(addon->X, y);
        }
    }
}