using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using System.Collections.Generic;
using System.Numerics;

namespace FfxivVR;

public unsafe class NameplateModifier(
    IGameGui gameGui,
    ITargetManager targetManager,
    Configuration configuration)
{
    public NameplateAnchor[] Anchors { get; private set; } = [];

    public float? TargetNameplateDepth { get; private set; }

    private static Vector4 ComputeWorldPosition(INamePlateUpdateHandler handler)
    {
        var gameObject = (GameObject*)(handler.GameObject?.Address ?? 0);
        if (gameObject == null)
        {
            return Vector4.Zero;
        }
        FFXIVClientStructs.FFXIV.Common.Math.Vector3 position;
        gameObject->GetNamePlateWorldPosition(&position);
        return new Vector4(position, 1);
    }

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
        var anchors = new NameplateAnchor[reprojectsTargetCursor ? handlers.Count : 0];
        TargetNameplateDepth = null;
        for (int i = 0; i < handlers.Count; i++)
        {
            var objectData = namePlateArray->ObjectData.GetPointer(handlers[i].ArrayIndex);
            if (reprojectsTargetCursor)
            {
                if (handlers[i].GameObjectId == (target ?? softTarget)?.GameObjectId)
                {
                    TargetNameplateDepth = objectData->Depth;
                }
                anchors[i] = new NameplateAnchor(
                    new Vector3(objectData->X, objectData->Y, objectData->Depth),
                    ComputeWorldPosition(handlers[i]));
            }
            if (handlers[i].GameObjectId == target?.GameObjectId || handlers[i].GameObjectId == softTarget?.GameObjectId)
            {
                // Set this to the normal flag to prevent the target nameplate from moving around
                objectData->DrawFlags |= 1 << 3;
                if (!reprojectsTargetCursor)
                {
                    objectData->MarkerIconId = 61510;
                }
            }
        }
        Anchors = anchors;
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