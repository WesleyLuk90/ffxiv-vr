using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using System;
using System.Collections.Generic;

namespace FfxivVR;

public unsafe class SkeletonStructure
{
    // https://docs.google.com/spreadsheets/d/1kIKvVsW3fOnVeTi9iZlBDqJo6GWVn6K6BCUIRldEjhw/edit?gid=489002051#gid=489002051
    private List<Bone> bonesByInternalIndex;
    private Dictionary<string, Bone> bonesByType;
    public SkeletonStructure(hkaSkeleton* skeleton)
    {
        if (skeleton == null)
        {
            throw new ArgumentNullException("skeleton was null");
        }
        bonesByInternalIndex = new List<Bone>();
        bonesByType = new Dictionary<string, Bone>();
        for (int boneIndex = 0; boneIndex < skeleton->Bones.Length; boneIndex++)
        {
            var name = skeleton->Bones[boneIndex].Name.String ?? "unknown";
            var bone = new Bone(
                index: boneIndex,
                parent: skeleton->ParentIndices[boneIndex],
                referencePose: skeleton->ReferencePose[boneIndex],
                name: name!
            );
            if (boneIndex != 0)
            {
                var parent = skeleton->ParentIndices[boneIndex];
                if (parent >= 0 && parent < bonesByInternalIndex.Count)
                {
                    bonesByInternalIndex[parent].Children.Add(boneIndex);
                }
            }
            bonesByInternalIndex.Add(bone);
            bonesByType[name] = bone;
        }
    }

    internal Bone? GetBone(string boneName)
    {
        return bonesByType.GetValueOrDefault(boneName);
    }

    public List<Bone> GetChildren(Bone bone, Func<string, bool>? filter)
    {
        var children = bonesByInternalIndex[bone.Index].Children;
        var list = new List<Bone>(children.Count);
        foreach (var child in children)
        {
            if (filter?.Invoke(bonesByInternalIndex[child].Name) ?? true)
            {
                list.Add(bonesByInternalIndex[child]);
            }
        }
        return list;
    }

    public List<Bone> GetDescendants(Bone bone, Func<string, bool>? filter)
    {
        var bones = GetChildren(bone, filter);
        for (int i = 0; i < bones.Count; i++)
        {
            var children = bonesByInternalIndex[bones[i].Index].Children;
            bones.EnsureCapacity(bones.Count + children.Count);
            foreach (var child in children)
            {
                if (filter?.Invoke(bonesByInternalIndex[child].Name) ?? true)
                {
                    bones.Add(bonesByInternalIndex[child]);
                }
            }
        }
        return bones;
    }
}
public unsafe class Bone(int index, int parent, hkQsTransformf referencePose, string name)
{
    internal readonly int Index = index;
    internal readonly int Parent = parent;
    internal List<int> Children = new List<int>();
    internal readonly hkQsTransformf ReferencePose = referencePose;
    public readonly string Name = name;

    public hkQsTransformf* GetModelTransforms(hkaPose* pose)
    {
        return pose->AccessBoneModelSpace(Index, hkaPose.PropagateOrNot.DontPropagate);
    }
    public hkQsTransformf* GetLocalTransforms(hkaPose* pose)
    {
        return pose->AccessBoneLocalSpace(Index);
    }
}