using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.Havok.Animation.Rig;
using Silk.NET.Maths;
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
}
