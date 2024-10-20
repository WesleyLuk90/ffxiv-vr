using FFXIVClientStructs.FFXIV.Client.Game.Character;
using static FfxivVR.Plugin;

namespace FfxivVR;
unsafe internal class GameVisibility
{
    public void UpdateVisibility()
    {
        var player = ClientState.LocalPlayer;
        if (player == null)
        {
            return;
        }
        var character = (Character*)player!.Address;
        if (character == null)
        {
            return;
        }
        if (character->GameObject.DrawObject != null)
        {
            character->GameObject.DrawObject->Flags = (byte)ModelCullTypes.Visible;
        }

        if (character->Mount.MountObject != null)
        {
            if (character->Mount.MountObject->DrawObject != null)
            {
                character->Mount.MountObject->DrawObject->Flags = (byte)ModelCullTypes.Visible;
            }
        }
        if (character->OrnamentData.OrnamentObject != null)
        {
            if (character->OrnamentData.OrnamentObject->DrawObject != null)
            {
                character->OrnamentData.OrnamentObject->DrawObject->Flags = (byte)ModelCullTypes.Visible;
            }
        }
    }
}
