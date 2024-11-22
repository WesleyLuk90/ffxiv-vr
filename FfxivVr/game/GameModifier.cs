using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Silk.NET.Maths;
using System.Collections.Generic;

namespace FfxivVR;
unsafe public class GameModifier
{
    private readonly Logger logger;
    private readonly GameState gameState;
    private readonly IGameGui gameGui;
    private readonly ITargetManager targetManager;
    private readonly IClientState clientState;
    private readonly NameplateModifier nameplateModifier;
    private readonly SkeletonModifier skeletonModifier;

    public enum ModelCullTypes : byte
    {
        None = 0,
        InsideCamera = 66,
        OutsideCullCone = 67,
        Visible = 75
    }

    public GameModifier(Logger logger, GameState gameState, IGameGui gameGui, ITargetManager targetManager, IClientState clientState)
    {
        this.logger = logger;
        this.gameState = gameState;
        this.gameGui = gameGui;
        this.targetManager = targetManager;
        this.clientState = clientState;
        this.nameplateModifier = new NameplateModifier(logger, gameGui, targetManager);
        this.skeletonModifier = new SkeletonModifier(logger);
    }

    public void ForceFirstPersonBodyVisible()
    {
        if (gameState.IsInCutscene())
        {
            return;
        }
        UpdateVisbility(targetManager.Target, true);
        if (gameState.IsGPosing())
        {
            UpdateVisbility(clientState.LocalPlayer, false);
        }
        Character* character = getCharacterOrGpose();
        if (character == null)
        {
            return;
        }
        if (character->GameObject.DrawObject != null)
        {
            character->GameObject.DrawObject->Flags = (byte)ModelCullTypes.Visible;

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
            var drawData = character->DrawData;
            if (character->IsWeaponDrawn || !drawData.IsWeaponHidden || Conditions.IsCrafting || Conditions.IsGathering)
            {
                var mainHand = drawData.Weapon(DrawDataContainer.WeaponSlot.MainHand);
                if (mainHand.DrawObject != null)
                {
                    mainHand.DrawObject->Flags = (byte)ModelCullTypes.Visible;
                }
                var offHand = drawData.Weapon(DrawDataContainer.WeaponSlot.OffHand);
                if (offHand.DrawObject != null)
                {
                    offHand.DrawObject->Flags = (byte)ModelCullTypes.Visible;
                }
                var unknown = drawData.Weapon(DrawDataContainer.WeaponSlot.Unk);
                if (unknown.DrawObject != null)
                {
                    unknown.DrawObject->Flags = (byte)ModelCullTypes.Visible;
                }
            }
        }
    }

    private void UpdateVisbility(IGameObject? gameObject, bool visible)
    {
        if (gameObject == null)
        {
            return;
        }
        GameObject* realObject = (GameObject*)gameObject.Address;
        if (realObject == null)
        {
            return;
        }
        if (realObject->DrawObject == null)
        {
            return;
        }
        realObject->DrawObject->Flags = visible ? (byte)ModelCullTypes.Visible : (byte)ModelCullTypes.InsideCamera;
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


    private PositionSmoother positionSmoother = new();

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
        var isDismounting = (character->Mount.Flags & 1) != 0;
        Matrix4X4<float> headTransforms;
        if (character->Mount.MountObject != null && character->Mount.MountObject->DrawObject != null && !isDismounting)
        {
            var smoothedPosition = positionSmoother.GetSmoothedPosition(character->Mount.MountObject);
            var mountTransform = skeletonModifier.GetMountTransform(character->Mount.MountObject) ?? Matrix4X4.CreateFromQuaternion(characterBase->DrawObject.Rotation.ToQuaternion());
            var worldTransform = MathFactory.CreateScaleRotationTranslationMatrix(characterBase->Scale.ToVector3D(), Quaternion<float>.Identity, smoothedPosition);
            headTransforms = mountTransform * worldTransform;
        }
        else
        {
            positionSmoother.Reset();
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



    internal void UpdateMotionControls(HandTrackerExtension.HandData hands, RuntimeAdjustments runtimeAdjustments, float cameraYRotation)
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
        skeletonModifier.UpdateHands(skeleton, hands, runtimeAdjustments, cameraYRotation);
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
        nameplateModifier.PinTargetNameplate(context, handlers);
    }

    internal void UpdateLetterboxing(InternalLetterboxing* internalLetterboxing)
    {
        internalLetterboxing->ShouldLetterBox &= ~LetterBoxingOption.EnableLetterboxing;
    }
}