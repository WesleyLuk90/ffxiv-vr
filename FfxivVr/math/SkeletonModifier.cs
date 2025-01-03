﻿using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;

namespace FfxivVR;

public unsafe class SkeletonModifier(Logger logger
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


    private static hkaPose* GetPose(Skeleton* skeleton)
    {
        var partial = skeleton->PartialSkeletons;
        if (partial == null)
        {
            return null;
        }
        return partial->GetHavokPose(0);
    }

    private Dictionary<string, SkeletonStructure> SkeletonStructures = new();
    private SkeletonStructure? GetSkeletonStructure(Skeleton* skeleton)
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
    internal void UpdateHands(Skeleton* skeleton, TrackingData trackingData, RuntimeAdjustments runtimeAdjustments, float cameraYRotation)
    {
        if (!trackingData.HasData())
        {
            return;
        }
        var pose = GetPose(skeleton);
        if (pose == null)
        {
            return;
        }
        if (GetSkeletonStructure(skeleton) is not { } structure)
        {
            return;
        }
        var skeletonRotation = MathFactory.YRotation(cameraYRotation) / skeleton->Transform.Rotation.ToQuaternion();
        if (GetHeadPosition(skeleton) is not { } head)
        {
            return;
        }
        if (trackingData.BodyData is { } data)
        {
            ApplyBodyTracking(pose, structure, skeletonRotation, data);
            return;
        }
        ResetPoseTree(pose, structure, HumanBones.ArmLeft, b => !b.Contains(HumanBones.WeaponBoneSubstring));
        ResetPoseTree(pose, structure, HumanBones.ArmRight, b => !b.Contains(HumanBones.WeaponBoneSubstring));

        if (trackingData.GetLeftHand() is HandJointLocationEXT[] leftHand)
        {
            UpdateArmIK(leftHand[(int)HandJointEXT.WristExt].Pose, pose, structure, head, HumanBones.ArmLeft, HumanBones.ForearmLeft, HumanBones.HandLeft, skeletonRotation);
            RotateHand(leftHand[(int)HandJointEXT.PalmExt].Pose, HumanBones.HandLeft, HumanBones.WristLeft, HumanBones.ForearmLeft, structure, pose, skeletonRotation);

            foreach (var joint in HandJoints)
            {
                UpdateHandBone(leftHand, joint.Joint, joint.LeftBone, pose, structure, runtimeAdjustments, skeletonRotation);
            }
        }
        else if (trackingData.GetLeftController() is Posef leftController)
        {
            var wrist = ControllerToWrist(leftController, MathFactory.ZRotation(float.DegreesToRadians(90)));
            UpdateArmIK(wrist, pose, structure, head, HumanBones.ArmLeft, HumanBones.ForearmLeft, HumanBones.HandLeft, skeletonRotation);
            RotateHand(wrist, HumanBones.HandLeft, HumanBones.WristLeft, HumanBones.ForearmLeft, structure, pose, skeletonRotation);
        }

        if (trackingData.GetRightHand() is HandJointLocationEXT[] rightHand)
        {
            UpdateArmIK(rightHand[(int)HandJointEXT.WristExt].Pose, pose, structure, head, HumanBones.ArmRight, HumanBones.ForearmRight, HumanBones.HandRight, skeletonRotation);
            RotateHand(rightHand[(int)HandJointEXT.PalmExt].Pose, HumanBones.HandRight, HumanBones.WristRight, HumanBones.ForearmRight, structure, pose, skeletonRotation);

            foreach (var joint in HandJoints)
            {
                UpdateHandBone(rightHand, joint.Joint, joint.RightBone, pose, structure, runtimeAdjustments, skeletonRotation);
            }
        }
        else if (trackingData.GetRightController() is Posef rightController)
        {
            var wrist = ControllerToWrist(rightController, MathFactory.ZRotation(float.DegreesToRadians(-90)));
            UpdateArmIK(wrist, pose, structure, head, HumanBones.ArmRight, HumanBones.ForearmRight, HumanBones.HandRight, skeletonRotation);
            RotateHand(wrist, HumanBones.HandRight, HumanBones.WristRight, HumanBones.ForearmRight, structure, pose, skeletonRotation);
        }
    }

    private void ApplyBodyTracking(hkaPose* pose, SkeletonStructure structure, Quaternion<float> skeletonRotation, BodyJointLocationFB[] data)
    {
        ResetPoseTree(pose, structure, HumanBones.SpineA, bone => !bone.Contains(HumanBones.WeaponBoneSubstring) && bone != HumanBones.Face);

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

    private Posef ControllerToWrist(Posef leftController, Quaternion<float> rotation)
    {
        var wristRotation = leftController.Orientation.ToQuaternion() * rotation;
        var wristPosition = leftController.Position.ToVector3D() - Vector3D.Transform(new Vector3D<float>(0, 0, -0.08f), wristRotation);
        return new Posef(wristRotation.ToQuaternionf(), wristPosition.ToVector3f());
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

    class HandJoint(HandJointEXT joint, string leftBone, string rightBone)
    {
        public HandJointEXT Joint { get; } = joint;
        public string LeftBone { get; } = leftBone;
        public string RightBone { get; } = rightBone;
    }

    private void ResetPoseTree(hkaPose* pose, SkeletonStructure structure, string bone, Func<string, bool> filter)
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

    private void RotateHand(Posef palmPose, string handBoneName, string wristBoneName, string forearmBoneName, SkeletonStructure structure, hkaPose* pose, Quaternion<float> skeletonRotation)
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

    private void UpdateArmIK(Posef wrist, hkaPose* pose, SkeletonStructure structure, Vector3D<float> head, string arm, string forearm, string hand, Quaternion<float> skeletonRotation)
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

    private InverseKinematics ik = new InverseKinematics();

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
            * y180
            * adjustments
            * MathFactory.YRotation(float.DegreesToRadians(-90))
            ).ToQuaternion();
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

        var cameraRotation = y180;

        localTransforms->Rotation = (localTransforms->Rotation.ToQuaternion()
            * Quaternion<float>.Inverse(globalRotation)
            * cameraRotation
            * jointRotation
            * quaternion
            ).ToQuaternion();
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