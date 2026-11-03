using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using System;
using System.Collections.Generic;

namespace FfxivVR;

public unsafe class NameplateModifier(
    IGameGui gameGui,
    ITargetManager targetManager,
    Configuration configuration)
{
    public float? TargetNameplateDepth { get; private set; }

    private const float MinNameplateDistance = 0.5f;

    public float MaxNameplateDepth => VRCamera.NearPlane / MinNameplateDistance;

    public void UpdateVRNameplates(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        var reprojectsTargetCursor = configuration.UseSinglePassRender;
        if (!reprojectsTargetCursor)
        {
            HideTargetArrow();
        }
        var target = targetManager.Target;
        var softTarget = targetManager.SoftTarget;
        var namePlateArray = (NamePlateNumberArray*)context.NumberArrayDataEntryAddress;
        TargetNameplateDepth = null;
        for (int i = 0; i < handlers.Count; i++)
        {
            if (reprojectsTargetCursor)
            {
                var data = namePlateArray->ObjectData.GetPointer(handlers[i].ArrayIndex);
                data->Depth = MathF.Min(data->Depth, MaxNameplateDepth);
                if (handlers[i].GameObjectId == (target ?? softTarget)?.GameObjectId)
                {
                    TargetNameplateDepth = data->Depth;
                }
            }
            if (handlers[i].GameObjectId == target?.GameObjectId || handlers[i].GameObjectId == softTarget?.GameObjectId)
            {
                var objectData = namePlateArray->ObjectData.GetPointer(handlers[i].ArrayIndex);
                // Set this to the normal flag to prevent the target nameplate from moving around
                objectData->DrawFlags |= 1 << 3;
                if (!reprojectsTargetCursor)
                {
                    objectData->MarkerIconId = 61510;
                }
            }
        }
    }

    private void HideTargetArrow()
    {
        AtkUnitBase* targetAddon = (AtkUnitBase*)gameGui!.GetAddonByName("_TargetCursor", 1).Address;
        if (targetAddon != null)
        {
            targetAddon->Hide(true, false, 0);
        }
    }
}