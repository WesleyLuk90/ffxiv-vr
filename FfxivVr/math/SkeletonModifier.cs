using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.Havok.Animation.Rig;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using static FfxivVR.SkeletonStructure;

namespace FfxivVR;

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
        if (hideNeck)
        {
            var neckLocal = neck.GetLocalTransforms(pose);
            neckLocal->Scale.X = 0.001f;
            neckLocal->Scale.Y = 0.001f;
            neckLocal->Scale.Z = 0.001f;
        }
    }
    internal void UpdateHands(Skeleton* skeleton, HandTrackerExtension.HandData hands, RuntimeAdjustments runtimeAdjustments, float cameraYRotation)
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
        var skeletonRotation = MathFactory.YRotation(cameraYRotation) / skeleton->Transform.Rotation.ToQuaternion();
        var maybeHead = GetHeadPosition(skeleton);
        if (maybeHead is Vector3D<float> head)
        {
            ResetPoseTree(pose, structure, structure.GetBone(BoneType.ArmLeft));
            ResetPoseTree(pose, structure, structure.GetBone(BoneType.ArmRight));

            UpdateArmIK(hands.LeftHand, pose, structure, head, BoneType.ArmLeft, BoneType.ForearmLeft, BoneType.HandLeft, skeletonRotation);
            UpdateArmIK(hands.RightHand, pose, structure, head, BoneType.ArmRight, BoneType.ForearmRight, BoneType.HandRight, skeletonRotation);
            RotateHand(hands.LeftHand, BoneType.HandLeft, BoneType.WristLeft, BoneType.ForearmLeft, structure, pose, skeletonRotation);
            RotateHand(hands.RightHand, BoneType.HandRight, BoneType.WristRight, BoneType.ForearmRight, structure, pose, skeletonRotation);

            foreach (var joint in HandJoints)
            {
                UpdateHandBone(hands.LeftHand, joint.Joint, joint.LeftBone, pose, structure, runtimeAdjustments, skeletonRotation);
                UpdateHandBone(hands.RightHand, joint.Joint, joint.RightBone, pose, structure, runtimeAdjustments, skeletonRotation);
            }
        }
    }

    // https://docs.unity3d.com/Packages/com.unity.xr.hands@1.1/manual/hand-data/xr-hand-data-model.html
    private List<HandJoint> HandJoints = new List<HandJoint>() {
        new HandJoint(HandJointEXT.LittleProximalExt,  BoneType.PinkyFingerLeftA, BoneType.PinkyFingerRightA),
        new HandJoint(HandJointEXT.RingProximalExt,  BoneType.RingFingerLeftA, BoneType.RingFingerRightA),
        new HandJoint(HandJointEXT.MiddleProximalExt,  BoneType.MiddleFingerLeftA, BoneType.MiddleFingerRightA),
        new HandJoint(HandJointEXT.IndexProximalExt,  BoneType.IndexFingerLeftA, BoneType.IndexFingerRightA),
        new HandJoint(HandJointEXT.ThumbProximalExt,  BoneType.ThumbLeftA, BoneType.ThumbRightA),

        new HandJoint(HandJointEXT.LittleIntermediateExt,  BoneType.PinkyFingerLeftB, BoneType.PinkyFingerRightB),
        new HandJoint(HandJointEXT.RingIntermediateExt,  BoneType.RingFingerLeftB, BoneType.RingFingerRightB),
        new HandJoint(HandJointEXT.MiddleIntermediateExt,  BoneType.MiddleFingerLeftB, BoneType.MiddleFingerRightB),
        new HandJoint(HandJointEXT.IndexIntermediateExt,  BoneType.IndexFingerLeftB, BoneType.IndexFingerRightB),
        new HandJoint(HandJointEXT.ThumbDistalExt,  BoneType.ThumbLeftB, BoneType.ThumbRightB),
    };

    class HandJoint(HandJointEXT joint, BoneType leftBone, BoneType rightBone)
    {
        public HandJointEXT Joint { get; } = joint;
        public BoneType LeftBone { get; } = leftBone;
        public BoneType RightBone { get; } = rightBone;
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

    private void RotateHand(HandJointLocationEXT[] joints, BoneType handBone, BoneType wristBone, BoneType forearmBone, SkeletonStructure structure, hkaPose* pose, Quaternion<float> skeletonRotation)
    {
        var desiredRotation = joints[(int)HandJointEXT.PalmExt].Pose.Orientation.ToQuaternion();

        var hand = structure.GetBone(handBone);
        var globalHandRotation = hand.GetModelTransforms(pose)->Rotation.ToQuaternion();
        var flipHand = handBone == BoneType.HandLeft ? -90 : 90;
        var handTransforms = hand.GetLocalTransforms(pose);
        handTransforms->Rotation = (handTransforms->Rotation.ToQuaternion()
            * Quaternion<float>.Inverse(globalHandRotation)
            * skeletonRotation
            * desiredRotation
            * y180
            * MathFactory.YRotation(float.DegreesToRadians(-90))
            * MathFactory.XRotation(float.DegreesToRadians(flipHand))).ToQuaternion();

        var wrist = structure.GetBone(wristBone).GetLocalTransforms(pose);
        var forearm = structure.GetBone(forearmBone).GetLocalTransforms(pose);

        var half = Quaternion<float>.Slerp(handTransforms->Rotation.ToQuaternion(), forearm->Rotation.ToQuaternion(), 0.5f);
        var half2 = Quaternion<float>.Normalize(new Quaternion<float>(half.X, 0, 0, half.W));
        wrist->Rotation = half2.ToQuaternion();
    }

    private Quaternion<float> y180 = MathFactory.YRotation(float.DegreesToRadians(180));

    private void UpdateArmIK(HandJointLocationEXT[] joints, hkaPose* pose, SkeletonStructure structure, Vector3D<float> head, BoneType arm, BoneType forearm, BoneType hand, Quaternion<float> skeletonRotation)
    {
        var wrist = joints[(int)HandJointEXT.WristExt];
        var targetHandPosition = Vector3D.Transform(wrist.Pose.Position.ToVector3D(), skeletonRotation) + head;

        var armBone = structure.GetBone(arm);
        var forearmBone = structure.GetBone(forearm);
        var handBone = structure.GetBone(hand);

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

    private InverseKinematics ik = new InverseKinematics();

    private void UpdateHandBone(HandJointLocationEXT[] hand, HandJointEXT joint, BoneType type, hkaPose* pose, SkeletonStructure skeletonStructure, RuntimeAdjustments runtimeAdjustments, Quaternion<float> skeletonRotation)
    {
        var jointRotation = hand[(int)joint].Pose.Orientation.ToQuaternion();

        var adjustments = Quaternion<float>.Identity;
        if (type == BoneType.ThumbLeftA || type == BoneType.ThumbLeftB)
        {
            adjustments = runtimeAdjustments.ThumbRotation;
        }
        else if (type == BoneType.ThumbRightA || type == BoneType.ThumbRightB)
        {
            adjustments = runtimeAdjustments.ThumbRotation.Inverse();
        }

        var bone = skeletonStructure.GetBone(type);
        var globalRotation = bone.GetModelTransforms(pose)->Rotation.ToQuaternion();
        var localTransforms = bone.GetLocalTransforms(pose);

        localTransforms->Rotation = (localTransforms->Rotation.ToQuaternion()
            * Quaternion<float>.Inverse(globalRotation)
            * skeletonRotation
            * jointRotation
            * y180
            * adjustments
            * MathFactory.YRotation(float.DegreesToRadians(-90))
            ).ToQuaternion();
    }
}