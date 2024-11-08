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
            catch (BoneNotFound)
            {
                logger.Debug($"Not a character skeleton, ignoring");
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

    public Vector3D<float>? LastHead { get; private set; }

    // Z +90 degrees rotates fingers right
    // Y +90 degrees rotates fingers backwards
    // Z +90 degrees rotates on axis
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
            DoHandIK(hands.LeftHand, pose, structure, head, Tuple.Create(BoneType.ArmLeft, BoneType.ForeArmLeft, BoneType.HandLeft));
            DoHandIK(hands.RightHand, pose, structure, head, Tuple.Create(BoneType.ArmRight, BoneType.ForeArmRight, BoneType.HandRight));
        }
    }

    private void DoHandIK(HandJointLocationEXT[] joints, hkaPose* pose, SkeletonStructure structure, Vector3D<float> head, Tuple<BoneType, BoneType, BoneType> boneTypes)
    {
        var palm = joints[(int)HandJointEXT.PalmExt];
        var targetHandPosition = Vector3D.Transform(palm.Pose.Position.ToVector3D(), MathFactory.YRotation(float.DegreesToRadians(180))) + head;

        var armBone = structure.GetBone(boneTypes.Item1);
        var forearmBone = structure.GetBone(boneTypes.Item2);
        var handBone = structure.GetBone(boneTypes.Item3);

        ResetPose(armBone, pose);
        structure.GetDescendants(armBone).ForEach(b => ResetPose(b, pose));

        var armTransforms = armBone.GetModelTransforms(pose);
        var forearmTransforms = forearmBone.GetModelTransforms(pose);
        var handTransforms = handBone.GetModelTransforms(pose);
        //var hand = joints[(int)HandJointEXT.WristExt];
        //handTransforms->Rotation = (MathFactory.ZRotation(float.DegreesToRadians(90)) * MathFactory.XRotation(float.DegreesToRadians(180)) * hand.Pose.Orientation.ToQuaternion()).ToQuaternion();

        Debugging.DebugInfo = $"arm {armTransforms->Translation.ToVector3D()}\n" +
            $"2 {forearmTransforms->Translation.ToVector3D()}\n" +
            $"3 {handTransforms->Translation.ToVector3D()}\n" +
            $"target {targetHandPosition}\n";
        var currentHandPosition = handTransforms->Translation.ToVector3D();
        var armRotation = armTransforms->Rotation.ToQuaternion();
        var forearmRotation = forearmTransforms->Rotation.ToQuaternion();
        var (a, b) = ik.Calculate2Bone(
            armTransforms->Translation.ToVector3D(),
            forearmTransforms->Translation.ToVector3D(),
            currentHandPosition,
            targetHandPosition,
            new Vector3D<float>(0, 0, -1));

        var armLocal = armBone.GetLocalTransforms(pose);
        armLocal->Rotation = (armLocal->Rotation.ToQuaternion() * Quaternion<float>.Inverse(armRotation) * a * armRotation).ToQuaternion();
        var forearmLocal = forearmBone.GetLocalTransforms(pose);
        forearmLocal->Rotation = (forearmLocal->Rotation.ToQuaternion() * Quaternion<float>.Inverse(forearmRotation) * b * forearmRotation).ToQuaternion();
    }

    private void ResetPose(FfxivVR.Bone bone, hkaPose* pose)
    {
        var local = bone.GetLocalTransforms(pose);
        *local = bone.ReferencePose;
    }

    private InverseKinematics ik = new InverseKinematics();

    public float Degrees = 0;
    private void UpdateHandBones(HandJointLocationEXT[] hand, HandJointEXT joint, BoneType type, hkaPose* pose, SkeletonStructure skeletonStructure, Vector3D<float> head)
    {
        var handJoint = hand[(int)joint];
        var bone = skeletonStructure.GetBone(type);
        var localTransforms = bone.GetLocalTransforms(pose);
        localTransforms->Rotation = bone.ReferencePose.Rotation;
        var transforms = bone.GetModelTransforms(pose);

        // Flip front to back and left to right
        var toModelSpace = MathFactory.YRotation(float.DegreesToRadians(180));
        Vector3D<float> position = Vector3D.Transform(handJoint.Pose.Position.ToVector3D(), toModelSpace);

        var rotation = handJoint.Pose.Orientation.ToQuaternion() * toModelSpace;
        transforms->Rotation = rotation.ToQuaternion();
        transforms->Translation = (position + head).ToHkVector4();
        // new strategy, set entire arm to reference pose
        // get absolute positions
        // Run IK
        // Set hand rotations based on local ones
    }

}
