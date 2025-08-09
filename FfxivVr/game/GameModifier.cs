using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Game;
using Silk.NET.Maths;
using System.Collections.Generic;

namespace FfxivVR;

public unsafe class GameModifier(
    GameState gameState,
    NameplateModifier nameplateModifier,
    SkeletonModifier skeletonModifier,
    BodySkeletonModifier bodySkeletonModifier,
    HandTrackingSkeletonModifier handTrackingSkeletonModifier,
    ControllerTrackingSkeletonModifier controllerTrackingSkeletonModifier,
    Configuration configuration,
    FirstPersonManager firstPersonManager
)
{
    public void HideHeadMesh(bool force = false)
    {
        if (gameState.IsInCutscene() && !force)
        {
            return;
        }
        if (!firstPersonManager.IsFirstPerson && !force)
        {
            return;
        }
        var characterBase = gameState.GetCharacterBase();
        if (characterBase == null)
        {
            return;
        }
        var skeleton = characterBase->Skeleton;
        skeletonModifier.HideHead(skeleton, Conditions.Instance()->Mounted);
    }


    // Test with Alte Roite mount
    // Stand on a slope with a mount
    public Matrix4X4<float>? GetCharacterPositionTransform()
    {
        var characterBase = gameState.GetCharacterBase();
        if (characterBase == null)
        {
            return null;
        }
        var character = gameState.getCharacterOrGpose();
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
        var actorModel = gameState.GetCharacterBaseExtended();
        var actorScale = Matrix4X4.CreateScale(actorModel->Height);
        var skeleton = characterBase->Skeleton;
        var headPosition = skeletonModifier.GetHeadPosition(skeleton);
        return actorScale * headTransforms;
    }

    internal Vector3D<float>? GetHeadOffset()
    {
        var characterBase = gameState.GetCharacterBase();
        if (characterBase == null)
        {
            return null;
        }
        var skeleton = characterBase->Skeleton;
        return skeletonModifier.GetHeadPosition(skeleton);
    }

    internal void UpdateMotionControls(VRInputData vrInputData, RuntimeAdjustments runtimeAdjustments, float cameraYRotation)
    {
        var characterBase = gameState.GetCharacterBase();
        if (characterBase == null)
        {
            return;
        }
        var actorModel = gameState.GetCharacterBaseExtended();
        var skeleton = characterBase->Skeleton;
        var pose = SkeletonModifier.GetPose(skeleton);
        if (pose == null)
        {
            return;
        }
        if (skeletonModifier.GetSkeletonStructure(skeleton) is not { } structure)
        {
            return;
        }
        var skeletonRotation = MathFactory.YRotation(cameraYRotation) / skeleton->Transform.Rotation.ToQuaternion();
        if (skeletonModifier.GetHeadPosition(skeleton) is not { } head)
        {
            return;
        }
        if (configuration.BodyTracking && vrInputData.BodyJoints is { } bodyJoints)
        {
            bodySkeletonModifier.Apply(pose, structure, skeletonRotation, bodyJoints, vrInputData.HandPose);
        }
        else if (configuration.HandTracking && vrInputData.HandPose.HasData())
        {
            handTrackingSkeletonModifier.Apply(pose, structure, skeletonRotation, head, vrInputData.HandPose, runtimeAdjustments);
        }
        else if (configuration.ControllerTracking && vrInputData.PalmPose.HasData())
        {
            controllerTrackingSkeletonModifier.Apply(pose, structure, skeletonRotation, head, vrInputData.PalmPose);
        }
    }

    internal void ResetVerticalCameraRotation(float rotation)
    {
        var rawCamera = gameState.GetSceneCameraExtended();
        if (rawCamera != null)
        {
            rawCamera->CurrentVRotation = rotation;
        }
    }

    internal void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        nameplateModifier.UpdateVRNameplates(context, handlers);
    }

    internal void UpdateLetterboxing(LetterboxingExtended* internalLetterboxing)
    {
        internalLetterboxing->ShouldLetterBox &= ~LetterBoxingOption.EnableLetterboxing;
    }

    internal void SetCameraRotation(float rotation)
    {
        var rawCamera = gameState.GetSceneCameraExtended();
        if (rawCamera != null)
        {
            rawCamera->CurrentHRotation = rotation;
        }
    }
}