using FFXIVClientStructs.Havok.Animation.Rig;
using Silk.NET.Maths;
using Silk.NET.OpenXR;

namespace FfxivVR;

public unsafe class ControllerTrackingSkeletonModifier(
    SkeletonModifier skeletonModifier
)
{
    internal void Apply(hkaPose* pose, SkeletonStructure structure, Quaternion<float> skeletonRotation, Vector3D<float> head, PalmPose palmPose)
    {
        skeletonModifier.ResetPoseTree(pose, structure, HumanBones.ArmLeft, b => !b.Contains(HumanBones.WeaponBoneSubstring));
        skeletonModifier.ResetPoseTree(pose, structure, HumanBones.ArmRight, b => !b.Contains(HumanBones.WeaponBoneSubstring));
        if (palmPose.LeftPalm is Posef leftController)
        {
            var wrist = ControllerToWrist(leftController, MathFactory.ZRotation(float.DegreesToRadians(90)));
            skeletonModifier.UpdateArmIK(wrist, pose, structure, head, HumanBones.ArmLeft, HumanBones.ForearmLeft, HumanBones.HandLeft, skeletonRotation);
            skeletonModifier.RotateHand(wrist, HumanBones.HandLeft, HumanBones.WristLeft, HumanBones.ForearmLeft, structure, pose, skeletonRotation);
        }

        if (palmPose.RightPalm is Posef rightController)
        {
            var wrist = ControllerToWrist(rightController, MathFactory.ZRotation(float.DegreesToRadians(-90)));
            skeletonModifier.UpdateArmIK(wrist, pose, structure, head, HumanBones.ArmRight, HumanBones.ForearmRight, HumanBones.HandRight, skeletonRotation);
            skeletonModifier.RotateHand(wrist, HumanBones.HandRight, HumanBones.WristRight, HumanBones.ForearmRight, structure, pose, skeletonRotation);
        }
    }

    private Posef ControllerToWrist(Posef leftController, Quaternion<float> rotation)
    {
        var wristRotation = leftController.Orientation.ToQuaternion() * rotation;
        var wristPosition = leftController.Position.ToVector3D() - Vector3D.Transform(new Vector3D<float>(0, 0, -0.08f), wristRotation);
        return new Posef(wristRotation.ToQuaternionf(), wristPosition.ToVector3f());
    }
}