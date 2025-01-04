using FFXIVClientStructs.Havok.Animation.Rig;
using Silk.NET.Maths;
using Silk.NET.OpenXR;

namespace FfxivVR;

public unsafe class BodySkeletonModifier(
    SkeletonModifier skeletonModifier
)
{
    public void Apply(hkaPose* pose, SkeletonStructure structure, Quaternion<float> skeletonRotation, BodyJointLocationFB[] data)
    {
        skeletonModifier.ResetPoseTree(pose, structure, HumanBones.SpineA, bone => !bone.Contains(HumanBones.WeaponBoneSubstring) && bone != HumanBones.Face);

        void DoRotation(BodyJointFB joint, string bone, float y, float roll = 0)
        {
            if (data[(int)joint].LocationFlags.IsValidOrientation())
            {
                ApplyBoneRotation(data[(int)joint].Pose, bone, pose, structure, skeletonRotation, Quaternion<float>.CreateFromYawPitchRoll(float.DegreesToRadians(y), float.DegreesToRadians(roll), 0));
            }
        }

        DoRotation(BodyJointFB.HipsFB, HumanBones.SpineA, 180);
        DoRotation(BodyJointFB.SpineMiddleFB, HumanBones.SpineB, 180);
        DoRotation(BodyJointFB.SpineUpperFB, HumanBones.SpineC, 180);
        DoRotation(BodyJointFB.NeckFB, HumanBones.Neck, 180);
        DoRotation(BodyJointFB.LeftScapulaFB, HumanBones.ShoulderLeft, 180, -90);
        DoRotation(BodyJointFB.LeftArmUpperFB, HumanBones.ArmLeft, 180, 180);
        DoRotation(BodyJointFB.LeftArmLowerFB, HumanBones.ForearmLeft, 180, 180);
        DoRotation(BodyJointFB.LeftHandWristFB, HumanBones.HandLeft, 90, -90);
        DoRotation(BodyJointFB.LeftHandWristTwistFB, HumanBones.WristLeft, 180, 180);
        DoRotation(BodyJointFB.LeftHandLittleProximalFB, HumanBones.PinkyFingerLeftA, 90);
        DoRotation(BodyJointFB.LeftHandLittleIntermediateFB, HumanBones.PinkyFingerLeftB, 90);
        DoRotation(BodyJointFB.LeftHandRingProximalFB, HumanBones.RingFingerLeftA, 90);
        DoRotation(BodyJointFB.LeftHandRingIntermediateFB, HumanBones.RingFingerLeftB, 90);
        DoRotation(BodyJointFB.LeftHandMiddleProximalFB, HumanBones.MiddleFingerLeftA, 90);
        DoRotation(BodyJointFB.LeftHandMiddleIntermediateFB, HumanBones.MiddleFingerLeftB, 90);
        DoRotation(BodyJointFB.LeftHandIndexProximalFB, HumanBones.IndexFingerLeftA, 90);
        DoRotation(BodyJointFB.LeftHandIndexIntermediateFB, HumanBones.IndexFingerLeftB, 90);
        DoRotation(BodyJointFB.LeftHandThumbProximalFB, HumanBones.ThumbLeftA, 90);
        DoRotation(BodyJointFB.LeftHandThumbDistalFB, HumanBones.ThumbLeftB, 90);

        DoRotation(BodyJointFB.RightScapulaFB, HumanBones.ShoulderRight, 0, -90);
        DoRotation(BodyJointFB.RightArmUpperFB, HumanBones.ArmRight, 0);
        DoRotation(BodyJointFB.RightArmLowerFB, HumanBones.ForearmRight, 0);
        DoRotation(BodyJointFB.RightHandWristFB, HumanBones.HandRight, 90, 90);
        DoRotation(BodyJointFB.RightHandWristTwistFB, HumanBones.WristRight, 0);
        DoRotation(BodyJointFB.RightHandLittleProximalFB, HumanBones.PinkyFingerRightA, 90);
        DoRotation(BodyJointFB.RightHandLittleIntermediateFB, HumanBones.PinkyFingerRightB, 90);
        DoRotation(BodyJointFB.RightHandRingProximalFB, HumanBones.RingFingerRightA, 90);
        DoRotation(BodyJointFB.RightHandRingIntermediateFB, HumanBones.RingFingerRightB, 90);
        DoRotation(BodyJointFB.RightHandMiddleProximalFB, HumanBones.MiddleFingerRightA, 90);
        DoRotation(BodyJointFB.RightHandMiddleIntermediateFB, HumanBones.MiddleFingerRightB, 90);
        DoRotation(BodyJointFB.RightHandIndexProximalFB, HumanBones.IndexFingerRightA, 90);
        DoRotation(BodyJointFB.RightHandIndexIntermediateFB, HumanBones.IndexFingerRightB, 90);
        DoRotation(BodyJointFB.RightHandThumbProximalFB, HumanBones.ThumbRightA, 90);
        DoRotation(BodyJointFB.RightHandThumbDistalFB, HumanBones.ThumbRightB, 90);
    }
    private void ApplyBoneRotation(Posef posef, string type, hkaPose* pose, SkeletonStructure skeletonStructure, Quaternion<float> skeletonRotation, Quaternion<float> quaternion)
    {
        var jointRotation = posef.Orientation.ToQuaternion();

        if (skeletonStructure.GetBone(type) is not Bone bone)
        {
            return;
        }
        var globalRotation = bone.GetModelTransforms(pose)->Rotation.ToQuaternion();
        var localTransforms = bone.GetLocalTransforms(pose);

        var cameraRotation = MathFactory.YRotation(float.DegreesToRadians(180));

        localTransforms->Rotation = (localTransforms->Rotation.ToQuaternion()
            * Quaternion<float>.Inverse(globalRotation)
            * cameraRotation
            * jointRotation
            * quaternion
            ).ToQuaternion();
    }
}