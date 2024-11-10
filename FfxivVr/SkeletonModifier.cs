using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.System.IO.OStream;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static FfxivVR.SkeletonStructure;

namespace FfxivVR;

public static class SkeletonExtensions
{
    public static Quaternion<float> ToModelQuaternion(this Quaternion<float> quaternion)
    {
        return new Quaternion<float>(-quaternion.X, quaternion.Z, quaternion.Y, quaternion.W);
    }

}
unsafe internal class SkeletonModifier(Logger logger)
{
    private readonly Logger logger = logger;

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
        var transforms = structure.GetBone(BoneType.Neck).GetModelTransforms(pose);
        if (transforms == null)
        {
            return null;
        }
        var neckLength = 0.2f; // Hacky way to center the head
        var neckVector = Vector3D.Transform(new Vector3D<float>(neckLength, 0, 0), transforms->Rotation.ToQuaternion());
        return transforms->Translation.ToVector3D() + neckVector;
    }


    private static hkaPose* GetPose(Skeleton* skeleton)
    {
        var partial = skeleton->PartialSkeletons;
        if (partial == null)
        {
            return null;
        }
        return partial->GetHavokPose(0);
    }

    private Dictionary<IntPtr, SkeletonStructure?> SkeletonStructures = new Dictionary<IntPtr, SkeletonStructure?>();
    private SkeletonStructure? GetSkeletonStructure(Skeleton* skeleton)
    {
        var havokSkeleton = GetHavokSkeleton(skeleton);
        if (havokSkeleton == null)
        {
            return null;
        }
        var ptr = (IntPtr)havokSkeleton;
        if (!SkeletonStructures.ContainsKey(ptr))
        {
            // Test in Alexander - The Fist of the Son for the morph
            logger.Debug($"New skeleton found with {havokSkeleton->Bones.Length} bones");
            try
            {
                SkeletonStructures[ptr] = new SkeletonStructure(havokSkeleton);
            }
            catch (BoneNotFound e)
            {
                logger.Debug($"{e} Not a character skeleton, ignoring");
                SkeletonStructures[ptr] = null;
            }
        }
        return SkeletonStructures[ptr];
    }

    private hkaSkeleton* GetHavokSkeleton(Skeleton* skeleton)
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

    internal void HideHead(Skeleton* skeleton)
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
        var spine = structure.GetBone(BoneType.SpineC);
        var spineTransform = spine.GetModelTransforms(pose);
        var neck = structure.GetBone(BoneType.Neck);
        var children = structure.GetDescendants(neck);
        foreach (var child in children)
        {
            var childTransform = child.GetModelTransforms(pose);
            childTransform->Translation = spineTransform->Translation;
            childTransform->Scale.X = 0.001f;
            childTransform->Scale.Y = 0.001f;
            childTransform->Scale.Z = 0.001f;
        }
    }
    record JointBoneMapping(HandJointEXT joint, BoneType leftType, BoneType rightType) { }

    // https://docs.unity3d.com/Packages/com.unity.xr.hands@1.1/manual/hand-data/xr-hand-data-model.html
    private List<JointBoneMapping> JointToBone = [
        new JointBoneMapping(HandJointEXT.WristExt, BoneType.HandLeft, BoneType.HandRight),

        new JointBoneMapping(HandJointEXT.ThumbMetacarpalExt, BoneType.ThumbLeftA, BoneType.ThumbRightA),
        new JointBoneMapping(HandJointEXT.ThumbDistalExt, BoneType.ThumbLeftB, BoneType.ThumbRightB),

        new JointBoneMapping(HandJointEXT.IndexProximalExt, BoneType.IndexFingerLeftA, BoneType.IndexFingerRightA),
        new JointBoneMapping(HandJointEXT.IndexDistalExt, BoneType.IndexFingerLeftB, BoneType.IndexFingerRightB),

        new JointBoneMapping(HandJointEXT.MiddleProximalExt, BoneType.MiddleFingerLeftA, BoneType.MiddleFingerRightA),
        new JointBoneMapping(HandJointEXT.MiddleDistalExt, BoneType.MiddleFingerLeftB, BoneType.MiddleFingerRightB),

        new JointBoneMapping(HandJointEXT.RingProximalExt, BoneType.RingFingerLeftA, BoneType.RingFingerRightA),
        new JointBoneMapping(HandJointEXT.RingDistalExt, BoneType.RingFingerLeftB, BoneType.RingFingerRightB),

        new JointBoneMapping(HandJointEXT.LittleProximalExt, BoneType.PinkyFingerLeftA, BoneType.PinkyFingerRightA),
        new JointBoneMapping(HandJointEXT.LittleDistalExt, BoneType.PinkyFingerLeftB, BoneType.PinkyFingerRightB),
    ];
    internal void UpdateHands(Skeleton* skeleton, HandTrackerExtension.HandData hands)
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
        var maybeHead = GetHeadPosition(skeleton);
        if (maybeHead is Vector3D<float> head)
        {
            ResetPoseTree(pose, structure, structure.GetBone(BoneType.ArmLeft));
            ResetPoseTree(pose, structure, structure.GetBone(BoneType.ArmRight));

            UpdateArmIK(hands.LeftHand, pose, structure, head, BoneType.ArmLeft, BoneType.ForearmLeft, BoneType.HandLeft);
            UpdateArmIK(hands.RightHand, pose, structure, head, BoneType.ArmRight, BoneType.ForearmRight, BoneType.HandRight);
            RotateHand(hands.LeftHand, BoneType.HandLeft, BoneType.WristLeft, BoneType.ForearmLeft, structure, pose, 0);
            RotateHand(hands.RightHand, BoneType.HandRight, BoneType.WristRight, BoneType.ForearmRight, structure, pose, float.DegreesToRadians(180));

            foreach (var joint in HandJoints)
            {
                UpdateHandBone(hands.LeftHand, joint.Joint, joint.Parent, joint.LeftBone, pose, structure, joint.LeftRotationOffset);
                UpdateHandBone(hands.RightHand, joint.Joint, joint.Parent, joint.RightBone, pose, structure, joint.RightRotationOffset);
            }
        }
    }

    // https://docs.unity3d.com/Packages/com.unity.xr.hands@1.1/manual/hand-data/xr-hand-data-model.html
    private List<HandJoint> HandJoints = new List<HandJoint>() {
        new HandJoint(HandJointEXT.LittleProximalExt, HandJointEXT.PalmExt, BoneType.PinkyFingerLeftA, BoneType.PinkyFingerRightA),
        new HandJoint(HandJointEXT.RingProximalExt, HandJointEXT.PalmExt, BoneType.RingFingerLeftA, BoneType.RingFingerRightA),
        new HandJoint(HandJointEXT.MiddleProximalExt, HandJointEXT.PalmExt, BoneType.MiddleFingerLeftA, BoneType.MiddleFingerRightA),
        new HandJoint(HandJointEXT.IndexProximalExt, HandJointEXT.PalmExt, BoneType.IndexFingerLeftA, BoneType.IndexFingerRightA),
        new HandJoint(HandJointEXT.ThumbProximalExt, HandJointEXT.PalmExt, BoneType.ThumbLeftA, BoneType.ThumbRightA,
         Quaternion<float>.CreateFromYawPitchRoll(float.DegreesToRadians(50), float.DegreesToRadians(-50), float.DegreesToRadians(-40)),
         Quaternion<float>.CreateFromYawPitchRoll(float.DegreesToRadians(-50), float.DegreesToRadians(50), float.DegreesToRadians(-40))),

        new HandJoint(HandJointEXT.LittleIntermediateExt, HandJointEXT.LittleProximalExt, BoneType.PinkyFingerLeftB, BoneType.PinkyFingerRightB),
        new HandJoint(HandJointEXT.RingIntermediateExt, HandJointEXT.RingProximalExt, BoneType.RingFingerLeftB, BoneType.RingFingerRightB),
        new HandJoint(HandJointEXT.MiddleIntermediateExt, HandJointEXT.MiddleProximalExt, BoneType.MiddleFingerLeftB, BoneType.MiddleFingerRightB),
        new HandJoint(HandJointEXT.IndexIntermediateExt, HandJointEXT.IndexProximalExt, BoneType.IndexFingerLeftB, BoneType.IndexFingerRightB),
        new HandJoint(HandJointEXT.ThumbDistalExt, HandJointEXT.ThumbProximalExt, BoneType.ThumbLeftB, BoneType.ThumbRightB),
    };

    class HandJoint(HandJointEXT joint, HandJointEXT parent, BoneType leftBone, BoneType rightBone, Quaternion<float>? leftRotationOffset = null, Quaternion<float>? rightRotationOffset = null)
    {
        public HandJointEXT Joint { get; } = joint;
        public HandJointEXT Parent { get; } = parent;
        public BoneType LeftBone { get; } = leftBone;
        public BoneType RightBone { get; } = rightBone;
        public Quaternion<float> LeftRotationOffset { get; } = leftRotationOffset ?? Quaternion<float>.Identity;
        public Quaternion<float> RightRotationOffset { get; } = rightRotationOffset ?? Quaternion<float>.Identity;
    }

    private void ResetPoseTree(hkaPose* pose, SkeletonStructure structure, Bone armBone)
    {
        ResetPose(armBone, pose);
        structure.GetDescendants(armBone).ForEach(b => ResetPose(b, pose));
    }
    private void ResetPose(Bone bone, hkaPose* pose)
    {
        var local = bone.GetLocalTransforms(pose);
        *local = bone.ReferencePose;
    }

    private void RotateHand(HandJointLocationEXT[] joints, BoneType handBone, BoneType wristBone, BoneType forearmBone, SkeletonStructure structure, hkaPose* pose, float pitch)
    {
        var desiredRotation = joints[(int)HandJointEXT.PalmExt].Pose.Orientation.ToQuaternion();
        var palmRotation = // Not sure why this works
          MathFactory.XRotation(float.DegreesToRadians(-90)) *
           desiredRotation.ToModelQuaternion() *
            MathFactory.ZRotation(float.DegreesToRadians(-90)) *
            MathFactory.XRotation(pitch);

        var hand = structure.GetBone(handBone);
        var globalHandRotation = hand.GetModelTransforms(pose)->Rotation.ToQuaternion();
        var handTransforms = hand.GetLocalTransforms(pose);
        handTransforms->Rotation = (handTransforms->Rotation.ToQuaternion() * Quaternion<float>.Inverse(globalHandRotation) * palmRotation).ToQuaternion();

        var wrist = structure.GetBone(wristBone).GetLocalTransforms(pose);
        var forearm = structure.GetBone(forearmBone).GetLocalTransforms(pose);

        var half = Quaternion<float>.Slerp(handTransforms->Rotation.ToQuaternion(), forearm->Rotation.ToQuaternion(), 0.5f);
        var half2 = Quaternion<float>.Normalize(new Quaternion<float>(half.X, 0, 0, half.W));
        wrist->Rotation = half2.ToQuaternion();
    }

    private void UpdateArmIK(HandJointLocationEXT[] joints, hkaPose* pose, SkeletonStructure structure, Vector3D<float> head, BoneType arm, BoneType forearm, BoneType hand)
    {
        var palm = joints[(int)HandJointEXT.PalmExt];
        var targetHandPosition = Vector3D.Transform(palm.Pose.Position.ToVector3D(), MathFactory.YRotation(float.DegreesToRadians(180))) + head;

        var armBone = structure.GetBone(arm);
        var forearmBone = structure.GetBone(forearm);
        var handBone = structure.GetBone(hand);

        var armTransforms = armBone.GetModelTransforms(pose);
        var forearmTransforms = forearmBone.GetModelTransforms(pose);
        var handTransforms = handBone.GetModelTransforms(pose);

        var globalArmRotation = armTransforms->Rotation.ToQuaternion();
        var globalForearmRotation = forearmTransforms->Rotation.ToQuaternion();
        var currentHandPosition = handTransforms->Translation.ToVector3D();

        var (rotation1, rotation2) = ik.Calculate2Bone(
            armTransforms->Translation.ToVector3D(),
            forearmTransforms->Translation.ToVector3D(),
            currentHandPosition,
            targetHandPosition,
            new Vector3D<float>(0, 0, -1));

        var armLocal = armBone.GetLocalTransforms(pose);
        armLocal->Rotation = (armLocal->Rotation.ToQuaternion() * globalArmRotation.Inverse() * rotation1 * globalArmRotation).ToQuaternion();

        var forearmLocal = forearmBone.GetLocalTransforms(pose);
        forearmLocal->Rotation = (forearmLocal->Rotation.ToQuaternion() * globalForearmRotation.Inverse() * rotation2 * globalForearmRotation).ToQuaternion();
    }

    private InverseKinematics ik = new InverseKinematics();

    private void UpdateHandBone(HandJointLocationEXT[] hand, HandJointEXT joint, HandJointEXT relativeParent, BoneType type, hkaPose* pose, SkeletonStructure skeletonStructure, Quaternion<float> rotationOffset)
    {
        var jointRotation = hand[(int)joint].Pose.Orientation.ToQuaternion();
        var parentRotation = hand[(int)relativeParent].Pose.Orientation.ToQuaternion();
        var relativeRotation = parentRotation.Inverse() * jointRotation;

        var bone = skeletonStructure.GetBone(type);
        var localTransforms = bone.GetLocalTransforms(pose);

        var swapped = new Quaternion<float>(-relativeRotation.Z, relativeRotation.Y, relativeRotation.X, relativeRotation.W);
        var swapXZ = MathFactory.AxisAngle(1, 0, 1, 180);
        localTransforms->Rotation = (localTransforms->Rotation.ToQuaternion() * rotationOffset * swapped).ToQuaternion();
    }
}
