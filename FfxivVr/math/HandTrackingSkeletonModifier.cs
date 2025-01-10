using FFXIVClientStructs.Havok.Animation.Rig;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System.Collections.Generic;

namespace FfxivVR;

public unsafe class HandTrackingSkeletonModifier(
    SkeletonModifier skeletonModifier
)
{
    class HandJoint(HandJointEXT joint, string leftBone, string rightBone)
    {
        public HandJointEXT Joint { get; } = joint;
        public string LeftBone { get; } = leftBone;
        public string RightBone { get; } = rightBone;
    }
    // https://docs.unity3d.com/Packages/com.unity.xr.hands@1.1/manual/hand-data/xr-hand-data-model.html
    private List<HandJoint> HandJoints = new List<HandJoint>() {
        new HandJoint(HandJointEXT.LittleProximalExt,  HumanBones.PinkyFingerLeftA, HumanBones.PinkyFingerRightA),
        new HandJoint(HandJointEXT.RingProximalExt,  HumanBones.RingFingerLeftA, HumanBones.RingFingerRightA),
        new HandJoint(HandJointEXT.MiddleProximalExt,  HumanBones.MiddleFingerLeftA, HumanBones.MiddleFingerRightA),
        new HandJoint(HandJointEXT.IndexProximalExt,  HumanBones.IndexFingerLeftA, HumanBones.IndexFingerRightA),
        new HandJoint(HandJointEXT.ThumbProximalExt,  HumanBones.ThumbLeftA, HumanBones.ThumbRightA),

        new HandJoint(HandJointEXT.LittleIntermediateExt,  HumanBones.PinkyFingerLeftB, HumanBones.PinkyFingerRightB),
        new HandJoint(HandJointEXT.RingIntermediateExt,  HumanBones.RingFingerLeftB, HumanBones.RingFingerRightB),
        new HandJoint(HandJointEXT.MiddleIntermediateExt,  HumanBones.MiddleFingerLeftB, HumanBones.MiddleFingerRightB),
        new HandJoint(HandJointEXT.IndexIntermediateExt,  HumanBones.IndexFingerLeftB, HumanBones.IndexFingerRightB),
        new HandJoint(HandJointEXT.ThumbDistalExt,  HumanBones.ThumbLeftB, HumanBones.ThumbRightB),
    };
    public void Apply(hkaPose* pose, SkeletonStructure structure, Quaternion<float> skeletonRotation, Vector3D<float> head, HandPose handPose, RuntimeAdjustments runtimeAdjustments)
    {
        skeletonModifier.ResetPoseTree(pose, structure, HumanBones.ArmLeft, b => !b.Contains(HumanBones.WeaponBoneSubstring));
        skeletonModifier.ResetPoseTree(pose, structure, HumanBones.ArmRight, b => !b.Contains(HumanBones.WeaponBoneSubstring));

        if (handPose.LeftHand is HandJointLocationEXT[] leftHand)
        {
            skeletonModifier.UpdateArmIK(leftHand[(int)HandJointEXT.WristExt].Pose, pose, structure, head, HumanBones.ArmLeft, HumanBones.ForearmLeft, HumanBones.HandLeft, skeletonRotation);
            skeletonModifier.RotateHand(leftHand[(int)HandJointEXT.PalmExt].Pose, HumanBones.HandLeft, HumanBones.WristLeft, HumanBones.ForearmLeft, structure, pose, skeletonRotation);

            foreach (var joint in HandJoints)
            {
                UpdateHandBone(leftHand, joint.Joint, joint.LeftBone, pose, structure, runtimeAdjustments, skeletonRotation);
            }
        }
        if (handPose.RightHand is HandJointLocationEXT[] rightHand)
        {
            skeletonModifier.UpdateArmIK(rightHand[(int)HandJointEXT.WristExt].Pose, pose, structure, head, HumanBones.ArmRight, HumanBones.ForearmRight, HumanBones.HandRight, skeletonRotation);
            skeletonModifier.RotateHand(rightHand[(int)HandJointEXT.PalmExt].Pose, HumanBones.HandRight, HumanBones.WristRight, HumanBones.ForearmRight, structure, pose, skeletonRotation);

            foreach (var joint in HandJoints)
            {
                UpdateHandBone(rightHand, joint.Joint, joint.RightBone, pose, structure, runtimeAdjustments, skeletonRotation);
            }
        }
    }
    private void UpdateHandBone(HandJointLocationEXT[] hand, HandJointEXT joint, string type, hkaPose* pose, SkeletonStructure skeletonStructure, RuntimeAdjustments runtimeAdjustments, Quaternion<float> skeletonRotation)
    {
        var jointRotation = hand[(int)joint].Pose.Orientation.ToQuaternion();

        var adjustments = Quaternion<float>.Identity;
        if (type == HumanBones.ThumbLeftA || type == HumanBones.ThumbLeftB)
        {
            adjustments = runtimeAdjustments.ThumbRotation;
        }
        else if (type == HumanBones.ThumbRightA || type == HumanBones.ThumbRightB)
        {
            adjustments = runtimeAdjustments.ThumbRotation.Inverse();
        }

        if (skeletonStructure.GetBone(type) is not Bone bone)
        {
            return;
        }
        var globalRotation = bone.GetModelTransforms(pose)->Rotation.ToQuaternion();
        var localTransforms = bone.GetLocalTransforms(pose);

        localTransforms->Rotation = (localTransforms->Rotation.ToQuaternion()
            * Quaternion<float>.Inverse(globalRotation)
            * skeletonRotation
            * jointRotation
            * MathFactory.YRotation(float.DegreesToRadians(180))
            * adjustments
            * MathFactory.YRotation(float.DegreesToRadians(-90))
            ).ToQuaternion();
    }
}