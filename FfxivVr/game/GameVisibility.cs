using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace FfxivVR;

public unsafe class GameVisibililty
{
    public void SetVisible(GameObject* gameObject, bool visible)
    {
        if (gameObject == null)
        {
            return;
        }
        SetVisible(gameObject->DrawObject, visible);
    }
    public void SetVisible(Character* character, bool visible)
    {
        if (character == null)
        {
            return;
        }
        SetVisible(character->DrawObject, visible);
    }

    public void SetVisible(Ornament* ornament, bool visible)
    {
        if (ornament == null)
        {
            return;
        }
        SetVisible(ornament->DrawObject, visible);
    }

    public void SetVisible(DrawObjectData drawObjectData, bool visible)
    {
        SetVisible(drawObjectData.DrawObject, visible);
    }

    public void SetVisible(DrawObject* drawObject, bool visible)
    {
        if (drawObject == null)
        {
            return;
        }
        drawObject->Flags = visible ? (byte)ModelCullTypes.Visible : (byte)ModelCullTypes.InsideCamera;
    }

    public void UpdateVisbility(IGameObject? gameObject, bool visible)
    {
        if (gameObject == null)
        {
            return;
        }
        SetVisible((GameObject*)gameObject.Address, visible);
    }
}