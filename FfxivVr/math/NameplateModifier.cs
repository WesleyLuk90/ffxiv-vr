using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.Interop;
using System.Collections.Generic;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonNamePlate;

namespace FfxivVR;
unsafe internal class NameplateModifier(Logger logger, ITargetManager targetManager)
{
    private ITargetManager targetManager = targetManager;
    private Logger logger = logger;

    public void PinTargetNameplate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        var target = targetManager.Target;
        if (target == null)
        {
            return;
        }
        for (int i = 0; i < handlers.Count; i++)
        {
            if (handlers[i].GameObjectId == target.GameObjectId)
            {
                var namePlateArray = (AddonNamePlateNumberArray*)context.NumberArrayDataEntryAddress;
                var objectData = namePlateArray->ObjectData.GetPointer(handlers[i].ArrayIndex);
                // Set this to the normal flag to prevent the target nameplate from moving around
                objectData->DrawFlags = 8;
            }
        }
    }
}
