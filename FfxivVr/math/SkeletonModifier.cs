using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;

namespace FfxivVR;

public unsafe class SkeletonModifier(
    Logger logger,
    InverseKinematics ik
)
{

    // This returns the position relative to the model that is where the VR local origin is
    public Vector3D<float>? GetHeadPosition(Skeleton* skeleton)
    {
        var pose = GetPose(skeleton);
        if (pose == null)
        {
            return null;
        }
        var structure = GetSkeletonStructure(skeleton);
        if (structure == null)
        {
            return null;
        }
        if (structure.GetBone(HumanBones.Neck) is not Bone neckBone)
        {
            return null;
        }
        var transforms = neckBone.GetModelTransforms(pose);
        if (transforms == null)
        {
            return null;
        }
        if (structure.GetBone(HumanBones.Face) is not Bone face)
        {
            return null;
        }
        // Use the reference pose for the face and rotate using the neck
        var neckVector = Vector3D.Transform(face.ReferencePose.Translation.ToVector3D(), transforms->Rotation.ToQuaternion());
        return transforms->Translation.ToVector3D() + neckVector;
    }


    public static hkaPose* GetPose(Skeleton* skeleton)
    {
        var partial = skeleton->PartialSkeletons;
        if (partial == null)
        {
            return null;
        }
        return partial->GetHavokPose(0);
    }

    private Dictionary<string, SkeletonStructure> SkeletonStructures = new();
    public SkeletonStructure? GetSkeletonStructure(Skeleton* skeleton)
    {
        var havokSkeleton = GetHavokSkeleton(skeleton);
        if (havokSkeleton == null)
        {
            return null;
        }
        var key = skeleton->PartialSkeletons->SkeletonResourceHandle->FileName.ToString();
        if (!SkeletonStructures.ContainsKey(key))
        {
            // Test in Alexander - The Fist of the Son for the morph
            logger.Debug($"New skeleton {key} found with {havokSkeleton->Bones.Length} bones");
            SkeletonStructures[key] = new SkeletonStructure(havokSkeleton);
        }
        return SkeletonStructures[key];
    }

    public hkaSkeleton* GetHavokSkeleton(Skeleton* skeleton)
    {
        if (skeleton == null)
        {
            return null;
        }
        var partial = skeleton->PartialSkeletons;
        if (partial == null)
        {
            return null;
        }
        var handle = partial->SkeletonResourceHandle;
        if (handle == null)
        {
            return null;
        }
        return handle->HavokSkeleton;
    }

    internal void HideHead(Skeleton* skeleton, bool hideNeck)
    {
        var pose = GetPose(skeleton);
        if (pose == null)
        {
            return;
        }
        var structure = GetSkeletonStructure(skeleton);
        if (structure == null)
        {
            return;
        }
        if (structure.GetBone(HumanBones.SpineC) is not Bone spine)
        {
            return;
        }
        if (structure.GetBone(HumanBones.Neck) is not Bone neck)
        {
            return;
        }
        var spineTransform = spine.GetModelTransforms(pose);
        var children = structure.GetDescendants(neck, null);
        foreach (var child in children)
        {
            var childTransform = child.GetModelTransforms(pose);
            childTransform->Translation = spineTransform->Translation;
            childTransform->Translation.Z -= 0.05f;
            childTransform->Scale.X = 0.001f;
            childTransform->Scale.Y = 0.001f;
            childTransform->Scale.Z = 0.001f;
        }
        if (hideNeck)
        {
            var neckLocal = neck.GetLocalTransforms(pose);
            neckLocal->Scale.X = 0.001f;
            neckLocal->Scale.Y = 0.001f;
            neckLocal->Scale.Z = 0.001f;
        }
    }

    public void ResetPoseTree(hkaPose* pose, SkeletonStructure structure, string bone, Func<string, bool> filter)
    {
        if (structure.GetBone(bone) is not Bone realBone)
        {
            return;
        }
        ResetPose(realBone, pose);
        structure.GetDescendants(realBone, filter).ForEach(b =>
        {
            ResetPose(b, pose);
        });
    }
    private void ResetPose(Bone bone, hkaPose* pose)
    {
        var local = bone.GetLocalTransforms(pose);
        *local = bone.ReferencePose;
    }

    public void RotateHand(Posef palmPose, string handBoneName, string wristBoneName, string forearmBoneName, SkeletonStructure structure, hkaPose* pose, Quaternion<float> skeletonRotation)
    {
        var desiredRotation = palmPose.Orientation.ToQuaternion();

        if (structure.GetBone(handBoneName) is not Bone hand)
        {
            return;
        }
        if (structure.GetBone(wristBoneName) is not Bone wristBone)
        {
            return;
        }
        if (structure.GetBone(forearmBoneName) is not Bone forearmBone)
        {
            return;
        }
        var globalHandRotation = hand.GetModelTransforms(pose)->Rotation.ToQuaternion();
        var flipHand = handBoneName == HumanBones.HandLeft ? -90 : 90;
        var handTransforms = hand.GetLocalTransforms(pose);
        handTransforms->Rotation = (handTransforms->Rotation.ToQuaternion()
            * Quaternion<float>.Inverse(globalHandRotation)
            * skeletonRotation
            * desiredRotation
            * y180
            * MathFactory.YRotation(float.DegreesToRadians(-90))
            * MathFactory.XRotation(float.DegreesToRadians(flipHand))).ToQuaternion();

        var wrist = wristBone.GetLocalTransforms(pose);
        var forearm = forearmBone.GetLocalTransforms(pose);

        var half = Quaternion<float>.Slerp(handTransforms->Rotation.ToQuaternion(), forearm->Rotation.ToQuaternion(), 0.5f);
        var half2 = Quaternion<float>.Normalize(new Quaternion<float>(half.X, 0, 0, half.W));
        wrist->Rotation = half2.ToQuaternion();
    }

    private Quaternion<float> y180 = MathFactory.YRotation(float.DegreesToRadians(180));

    public void UpdateArmIK(Posef wrist, hkaPose* pose, SkeletonStructure structure, Vector3D<float> head, string arm, string forearm, string hand, Quaternion<float> skeletonRotation)
    {
        var targetHandPosition = Vector3D.Transform(wrist.Position.ToVector3D(), skeletonRotation) + head;

        if (structure.GetBone(arm) is not Bone armBone)
        {
            return;
        }
        if (structure.GetBone(forearm) is not Bone forearmBone)
        {
            return;
        }
        if (structure.GetBone(hand) is not Bone handBone)
        {
            return;
        }

        var armTransforms = armBone.GetModelTransforms(pose);
        var forearmTransforms = forearmBone.GetModelTransforms(pose);
        var handTransforms = handBone.GetModelTransforms(pose);

        var globalArmRotation = armTransforms->Rotation.ToQuaternion();
        var globalForearmRotation = forearmTransforms->Rotation.ToQuaternion();

        var armPosition = armTransforms->Translation.ToVector3D();
        var forearmPosition = forearmTransforms->Translation.ToVector3D();
        var handPosition = handTransforms->Translation.ToVector3D();

        var (rotation1, rotation2) = ik.Calculate2Bone(
            armPosition,
            forearmPosition,
            handPosition,
            targetHandPosition,
            new Vector3D<float>(0, 0, -1));

        var armLocal = armBone.GetLocalTransforms(pose);
        armLocal->Rotation = (armLocal->Rotation.ToQuaternion() * globalArmRotation.Inverse() * rotation1 * globalArmRotation).ToQuaternion();

        var forearmLocal = forearmBone.GetLocalTransforms(pose);
        forearmLocal->Rotation = (forearmLocal->Rotation.ToQuaternion() * globalForearmRotation.Inverse() * rotation2 * globalForearmRotation).ToQuaternion();
    }

    public Matrix4X4<float>? GetMountTransform(Character* mountObject)
    {
        if (mountObject == null)
        {
            return null;
        }
        var mountBase = (CharacterBase*)mountObject->DrawObject;
        if (mountBase == null)
        {
            return null;
        }
        var skeleton = mountBase->Skeleton;
        if (skeleton == null)
        {
            return null;
        }
        var structure = GetSkeletonStructure(skeleton);
        if (structure == null)
        {
            return null;
        }
        if (structure.GetBone(MountBones.RiderPosition) is not Bone riderPositionBone)
        {
            return null;
        }
        var partial = skeleton->PartialSkeletons;
        if (partial == null)
        {
            return null;
        }
        var pose = partial->GetHavokPose(0);
        if (pose == null)
        {
            return null;
        }
        var transforms = riderPositionBone.GetModelTransforms(pose);
        var boneRotation = transforms->Rotation.ToQuaternion();
        var mountRotation = skeleton->Transform.Rotation.ToQuaternion();

        var scaled = Vector3D.Transform(transforms->Translation.ToVector3D(), Matrix4X4.CreateScale(mountObject->DrawObject->Scale.ToVector3D()));
        return MathFactory.CreateScaleRotationTranslationMatrix(transforms->Scale.ToVector3D(), boneRotation, scaled) *
            Matrix4X4.CreateFromQuaternion(mountRotation);
    }
}