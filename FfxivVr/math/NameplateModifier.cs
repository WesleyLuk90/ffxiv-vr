﻿using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using System.Collections.Generic;

namespace FfxivVR;
public unsafe class NameplateModifier(
    IGameGui gameGui,
    ITargetManager targetManager)
{
    public void UpdateVRNameplates(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        HideTargetArrow();
        var target = targetManager.Target;
        var softTarget = targetManager.SoftTarget;
        for (int i = 0; i < handlers.Count; i++)
        {
            if (handlers[i].GameObjectId == target?.GameObjectId || handlers[i].GameObjectId == softTarget?.GameObjectId)
            {
                var namePlateArray = (NamePlateNumberArray*)context.NumberArrayDataEntryAddress;
                var objectData = namePlateArray->ObjectData.GetPointer(handlers[i].ArrayIndex);
                // Set this to the normal flag to prevent the target nameplate from moving around
                objectData->DrawFlags |= 1 << 3;
                objectData->MarkerIconId = 61510;
            }
        }
    }

    private void HideTargetArrow()
    {
        AtkUnitBase* targetAddon = (AtkUnitBase*)gameGui!.GetAddonByName("_TargetCursor", 1);
        if (targetAddon != null)
        {
            targetAddon->Hide(true, false, 0);
        }
    }
}