using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Silk.NET.Maths;
using System.Collections.Generic;

namespace FfxivVR;
unsafe internal class SkeletonModifier(Logger logger)
{
    private readonly Logger logger = logger;

    public Vector3D<float>? GetHeadPosition(Skeleton* skeleton)
    {
        var neckBone = GetNeckBone(skeleton);
        if (neckBone != null)
        {
            for (int i = 0; i < skeleton->PartialSkeletonCount; i++)
            {
                var partial = skeleton->PartialSkeletons[i].GetHavokPose(0);
                if (partial == null)
                {
                    continue;
                }
                if (neckBone.BoneIndex >= partial->ModelPose.Length)
                {
                    continue;
                }
                var translation = partial->ModelPose[(int)neckBone.BoneIndex].Translation;
                return new Vector3D<float>(translation.X, translation.Y, translation.Z);
            }
        }
        return null;
    }
    internal void HideHead(Skeleton* skeleton)
    {
        if (skeleton == null)
        {
            return;
        }

        var neckBone = GetNeckBone(skeleton);
        if (neckBone != null)
        {
            for (int i = 0; i < skeleton->PartialSkeletonCount; i++)
            {
                var partial = skeleton->PartialSkeletons[i].GetHavokPose(0);
                if (partial == null)
                {
                    continue;
                }
                if (neckBone.BoneIndex >= partial->LocalPose.Length)
                {
                    continue;
                }
                var transform = partial->LocalPose[(int)neckBone.BoneIndex!];
                transform.Scale.X = 0.00001f;
                transform.Scale.Y = 0.00001f;
                transform.Scale.Z = 0.00001f;
                transform.Scale.W = 0;
                partial->LocalPose[(int)neckBone.BoneIndex!] = transform;

                neckBone.Children.ForEach((c) =>
                {
                    if (c < partial->LocalPose.Length)
                    {
                        var transform = partial->LocalPose[(int)c];
                        transform.Translation.X *= -1;
                        transform.Translation.Y *= -1;
                        transform.Translation.Z *= -1;
                        transform.Translation.W *= -1;
                        transform.Scale.X = 0.00001f;
                        transform.Scale.Y = 0.00001f;
                        transform.Scale.Z = 0.00001f;
                        transform.Scale.W = 0.00001f;
                        partial->LocalPose[(int)c] = transform;
                    }
                });
            }
        }
        else
        {
            logger.Debug("Neck bone not found");
        }
    }

    private Bone? neck = null;
    private Bone? GetNeckBone(Skeleton* skeleton)
    {
        if (neck != null)
        {
            return neck;
        }
        if (skeleton->SkeletonResourceHandles != null && skeleton->SkeletonResourceHandles[0] != null)
        {
            int? index = null;
            var children = new List<int>();
            var havokSkeleton = skeleton->SkeletonResourceHandles[0]->HavokSkeleton;
            for (int currentBone = 0; currentBone < havokSkeleton->Bones.Length; currentBone++)
            {
                var bone = havokSkeleton->Bones[currentBone];
                var parent = havokSkeleton->ParentIndices[currentBone];
                var name = bone!.Name!.String;
                if (name == "j_kubi")
                {
                    index = currentBone;
                }
                if (parent == index)
                {
                    children.Add(currentBone);
                }
            }
            if (index is int boneIndex)
            {
                neck = new Bone(boneIndex, children);
            }
        }
        return neck;
    }

    class Bone(int boneIndex, List<int> children)
    {
        public List<int> Children = children;

        public int BoneIndex { get; } = boneIndex;
    }
}
