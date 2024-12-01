using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using System.Collections.Generic;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonNamePlate;

namespace FfxivVR;
unsafe internal class NameplateModifier(Logger logger, IGameGui gameGui, ITargetManager targetManager)
{
    private ITargetManager targetManager = targetManager;
    private Logger logger = logger;

    public void UpdateVRNameplates(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        HideTargetArrow();
        var target = targetManager.Target;
        var softTarget = targetManager.SoftTarget;
        for (int i = 0; i < handlers.Count; i++)
        {
            if (handlers[i].GameObjectId == target?.GameObjectId || handlers[i].GameObjectId == softTarget?.GameObjectId)
            {
                var namePlateArray = (AddonNamePlateNumberArray*)context.NumberArrayDataEntryAddress;
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