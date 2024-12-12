using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Silk.NET.Maths;
using System.Collections.Generic;

namespace FfxivVR;
unsafe public class GameModifier(
    GameState gameState,
    ITargetManager targetManager,
    IClientState clientState,
    NameplateModifier nameplateModifier,
    SkeletonModifier skeletonModifier,
    GameVisibililty gameVisibililty)
{
    public void UpdateCharacterVisibility(bool showInFirstPerson)
    {
        if (gameState.IsInCutscene() || gameState.IsBetweenAreas())
        {
            return;
        }
        if (gameState.IsFirstPerson() && !showInFirstPerson)
        {
            return;
        }
        gameVisibililty.UpdateVisbility(targetManager.Target, true);

        if (gameState.IsGPosing())
        {
            gameVisibililty.UpdateVisbility(clientState.LocalPlayer, false);
        }
        Character* character = getCharacterOrGpose();

        SetCharacterVisible(character);

        var manager = CharacterManager.Instance();
        if (clientState.LocalPlayer is IPlayerCharacter localPlayer)
        {
            // If we're a passenger in someone elses mount then make it visible
            var owner = manager->LookupBattleCharaByEntityId(localPlayer.OwnerId);
            if (owner != null)
            {
                SetCharacterVisible((Character*)owner);
            }
            // If someone else is our passenger, then make them visible
            for (int i = 0; i < manager->BattleCharas.Length; i++)
            {
                var chara = manager->BattleCharas[i];
                if (chara.Value != null)
                {
                    if (chara.Value->OwnerId == localPlayer.EntityId)
                    {
                        SetCharacterVisible((Character*)chara.Value);
                    }
                }
            }
        }
    }

    private void SetCharacterVisible(Character* character)
    {
        if (character == null)
        {
            return;
        }
        gameVisibililty.SetVisible(character, true);

        gameVisibililty.SetVisible(character->Mount.MountObject, true);
        gameVisibililty.SetVisible(character->OrnamentData.OrnamentObject, true);
        var drawData = character->DrawData;
        if (character->IsWeaponDrawn || !drawData.IsWeaponHidden || Conditions.IsCrafting || Conditions.IsGathering)
        {
            gameVisibililty.SetVisible(drawData.Weapon(DrawDataContainer.WeaponSlot.MainHand), true);
            gameVisibililty.SetVisible(drawData.Weapon(DrawDataContainer.WeaponSlot.OffHand), true);
            gameVisibililty.SetVisible(drawData.Weapon(DrawDataContainer.WeaponSlot.Unk), true);
        }
    }


    public Character* getCharacterOrGpose()
    {
        Character* character = gameState.GetGposeTarget();
        if (gameState.IsGPosing() && character == null)
        {
            return null;
        }
        if (character != null)
        {
            return character;
        }
        var player = clientState.LocalPlayer;
        if (player == null)
        {
            return null;
        }
        return (Character*)player!.Address;
    }

    public void HideHeadMesh(bool force = false)
    {
        if (gameState.IsInCutscene() && !force)
        {
            return;
        }
        if (!gameState.IsFirstPerson() && !force)
        {
            return;
        }
        var characterBase = GetCharacterBase();
        if (characterBase == null)
        {
            return;
        }
        var skeleton = characterBase->Skeleton;
        skeletonModifier.HideHead(skeleton, Conditions.IsMounted);
    }

    public CharacterBase* GetCharacterBase()
    {
        Character* character = getCharacterOrGpose();
        if (character == null)
        {
            return null;
        }
        return (CharacterBase*)character->GameObject.DrawObject;
    }


    // Test with Alte Roite mount
    // Stand on a slope with a mount
    public Vector3D<float>? GetHeadPosition()
    {
        var characterBase = GetCharacterBase();
        if (characterBase == null)
        {
            return null;
        }
        var character = getCharacterOrGpose();
        if (character == null)
        {
            return null;
        }
        var isDismounting = (character->Mount.Flags & 1) != 0;
        Matrix4X4<float> headTransforms;
        if (character->Mount.MountObject != null && character->Mount.MountObject->DrawObject != null && !isDismounting)
        {
            var mountPosition = character->Mount.MountObject->DrawObject->Position.ToVector3D();
            var mountTransform = skeletonModifier.GetMountTransform(character->Mount.MountObject) ?? Matrix4X4.CreateFromQuaternion(characterBase->DrawObject.Rotation.ToQuaternion());
            var worldTransform = MathFactory.CreateScaleRotationTranslationMatrix(characterBase->Scale.ToVector3D(), Quaternion<float>.Identity, mountPosition);
            headTransforms = mountTransform * worldTransform;
        }
        else
        {
            headTransforms = MathFactory.CreateScaleRotationTranslationMatrix(characterBase->Scale.ToVector3D(), characterBase->DrawObject.Rotation.ToQuaternion(), characterBase->Position.ToVector3D());
        }
        var actorModel = InternalCharacterBase.FromCharacterBase(characterBase);
        var actorScale = Matrix4X4.CreateScale(actorModel->Height);
        var skeleton = characterBase->Skeleton;
        var headPosition = skeletonModifier.GetHeadPosition(skeleton);
        if (headPosition is Vector3D<float> head)
        {
            return Vector3D.Transform(head, actorScale * headTransforms);
        }
        else
        {
            return null;
        }
    }

    internal void UpdateMotionControls(TrackingData trackingData, RuntimeAdjustments runtimeAdjustments, float cameraYRotation)
    {
        Character* character = getCharacterOrGpose();
        if (character == null)
        {
            return;
        }
        var characterBase = (CharacterBase*)character->GameObject.DrawObject;
        if (characterBase == null)
        {
            return;
        }
        var actorModel = InternalCharacterBase.FromCharacterBase(characterBase);
        var skeleton = characterBase->Skeleton;
        skeletonModifier.UpdateHands(skeleton, trackingData, runtimeAdjustments, cameraYRotation);
    }

    internal void ResetVerticalCameraRotation(float rotation)
    {
        var rawCamera = gameState.GetInternalSceneCamera();
        if (rawCamera != null)
        {
            rawCamera->CurrentVRotation = rotation;
        }
    }

    internal void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        nameplateModifier.UpdateVRNameplates(context, handlers);
    }

    internal void UpdateLetterboxing(InternalLetterboxing* internalLetterboxing)
    {
        internalLetterboxing->ShouldLetterBox &= ~LetterBoxingOption.EnableLetterboxing;
    }

    internal void SetCameraRotation(float rotation)
    {
        var rawCamera = gameState.GetInternalSceneCamera();
        if (rawCamera != null)
        {
            rawCamera->CurrentHRotation = rotation;
        }
    }
}