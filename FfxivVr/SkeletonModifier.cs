using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Silk.NET.Maths;
using System.Collections.Generic;
using System.Linq;

namespace FfxivVR;
unsafe internal class SkeletonModifier(Logger logger)
{
    private readonly Logger logger = logger;

    private const string Neck = "j_kubi";
    private const string Head = "j_kao";
    public Vector3D<float>? GetHeadPosition(Skeleton* skeleton)
    {
        var neckBone = GetBoneByName(Neck, skeleton);
        if (neckBone != null)
        {
            var neckPosition = GetPosePosition(skeleton, neckBone);
            if (neckPosition != null)
            {
                return neckPosition;
            }
        }
        return null;
    }

    private static Vector3D<float>? GetPosePosition(Skeleton* skeleton, Bone bone)
    {
        for (int i = 0; i < skeleton->PartialSkeletonCount; i++)
        {
            var partial = skeleton->PartialSkeletons[i].GetHavokPose(0);
            if (partial == null)
            {
                continue;
            }
            if (bone.BoneIndex >= partial->ModelPose.Length)
            {
                continue;
            }
            var translation = partial->ModelPose[(int)bone.BoneIndex].Translation;
            var rotation = partial->ModelPose[(int)bone.BoneIndex].Rotation;
            var neckLength = 0.15f; // Hacky way to center the head
            var neckVector = Vector3D.Transform(new Vector3D<float>(neckLength, 0, 0), Matrix4X4.CreateFromQuaternion(new Quaternion<float>(rotation.X, rotation.Y, rotation.Z, rotation.W)));
            return new Vector3D<float>(translation.X, translation.Y, translation.Z) + neckVector;
        }
        return null;
    }

    internal void HideHead(Skeleton* skeleton)
    {
        if (skeleton == null)
        {
            return;
        }
        Bone? neckBone = GetBoneByName(Neck, skeleton);

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
    }


    private Dictionary<string, Bone>? bonesByName;
    private List<Bone>? bones;
    private Bone? GetBoneByName(string name, Skeleton* skeleton)
    {
        CreateBoneList(skeleton);
        return bonesByName?.GetValueOrDefault(name);
    }

    private Bone? GetBoneByIndex(int index, Skeleton* skeleton)
    {
        CreateBoneList(skeleton);
        return bones?.ElementAtOrDefault(index);
    }

    private void CreateBoneList(Skeleton* skeleton)
    {
        if (bonesByName != null && bones != null)
        {
            return;
        };
        if (skeleton->SkeletonResourceHandles == null || skeleton->SkeletonResourceHandles[0] == null)
        {
            return;
        }
        List<Bone> boneList = new List<Bone>();
        var havokSkeleton = skeleton->SkeletonResourceHandles[0]->HavokSkeleton;
        for (int currentBone = 0; currentBone < havokSkeleton->Bones.Length; currentBone++)
        {
            var bone = havokSkeleton->Bones[currentBone];
            var parent = havokSkeleton->ParentIndices[currentBone];
            var currentBoneName = bone!.Name!.String!;
            boneList.Add(new Bone(currentBoneName, currentBone, new List<int>()));

            if (currentBone != 0 && parent >= 0)
            {
                if (parent >= boneList.Count)
                {
                    logger.Error("Invalid bone");
                }
                else
                {
                    boneList[parent].Children.Add(currentBone);
                }
            }
        }
        bonesByName = new Dictionary<string, Bone>();
        foreach (var bone in boneList)
        {
            bonesByName[bone.Name] = bone;
        }
        bones = boneList;
    }

    class Bone(string name, int boneIndex, List<int> children)
    {
        public List<int> Children = children;
        public string Name { get; } = name;
        public int BoneIndex { get; } = boneIndex;
    }
}
