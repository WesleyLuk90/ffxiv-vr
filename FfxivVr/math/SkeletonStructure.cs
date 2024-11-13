using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using System;
using System.Collections.Generic;

namespace FfxivVR;

public unsafe class SkeletonStructure
{
    // https://docs.google.com/spreadsheets/d/1kIKvVsW3fOnVeTi9iZlBDqJo6GWVn6K6BCUIRldEjhw/edit?gid=489002051#gid=489002051
    private static Dictionary<string, BoneType> BoneTypesByName = new Dictionary<string, BoneType>()
    {
        { "j_kubi", BoneType.Neck },
        { "j_kao", BoneType.Face },
        { "j_sebo_c", BoneType.SpineC },

        { "j_ude_a_l", BoneType.ArmLeft },
        { "j_ude_b_l", BoneType.ForearmLeft },
        { "j_te_l", BoneType.HandLeft },
        { "n_hte_l", BoneType.WristLeft },
        { "j_oya_a_l", BoneType.ThumbLeftA },
        { "j_oya_b_l", BoneType.ThumbLeftB },
        { "j_hito_a_l", BoneType.IndexFingerLeftA },
        { "j_hito_b_l", BoneType.IndexFingerLeftB },
        { "j_naka_a_l", BoneType.MiddleFingerLeftA },
        { "j_naka_b_l", BoneType.MiddleFingerLeftB },
        { "j_kusu_a_l", BoneType.RingFingerLeftA },
        { "j_kusu_b_l", BoneType.RingFingerLeftB },
        { "j_ko_a_l", BoneType.PinkyFingerLeftA },
        { "j_ko_b_l", BoneType.PinkyFingerLeftB },

        { "j_ude_a_r", BoneType.ArmRight },
        { "j_ude_b_r", BoneType.ForearmRight },
        { "j_te_r", BoneType.HandRight },
        { "n_hte_r", BoneType.WristRight },
        { "j_oya_a_r", BoneType.ThumbRightA },
        { "j_oya_b_r", BoneType.ThumbRightB },
        { "j_hito_a_r", BoneType.IndexFingerRightA },
        { "j_hito_b_r", BoneType.IndexFingerRightB },
        { "j_naka_a_r", BoneType.MiddleFingerRightA },
        { "j_naka_b_r", BoneType.MiddleFingerRightB },
        { "j_kusu_a_r", BoneType.RingFingerRightA },
        { "j_kusu_b_r", BoneType.RingFingerRightB },
        { "j_ko_a_r", BoneType.PinkyFingerRightA },
        { "j_ko_b_r", BoneType.PinkyFingerRightB },

    };
    private List<Bone> bonesByInternalIndex;
    private List<Bone> bonesByType;

    public class BoneNotFound(BoneType boneType) : Exception($"A bone was not found {boneType}") { }
    public SkeletonStructure(hkaSkeleton* skeleton)
    {
        if (skeleton == null)
        {
            throw new ArgumentNullException("skeleton was null");
        }
        bonesByInternalIndex = new List<Bone>();
        bonesByType = new List<Bone>();
        foreach (var item in Enum.GetNames(typeof(BoneType)))
        {
            bonesByType.Add(null!);
        }
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
                bonesByInternalIndex[parent].Children.Add(boneIndex);
            }
            bonesByInternalIndex.Add(bone);
            if (name != null && BoneTypesByName.ContainsKey(name))
            {
                bonesByType[(int)BoneTypesByName[name]] = bone;
            }
        }
        for (int i = 0; i < bonesByType.Count; i++)
        {
            if (bonesByType[i] == null)
            {
                throw new BoneNotFound((BoneType)(i));
            }
        }
    }

    internal Bone GetBone(BoneType type)
    {
        return bonesByType[(int)type];
    }

    public List<Bone> GetChildren(Bone bone)
    {
        var children = bonesByInternalIndex[bone.Index].Children;
        var list = new List<Bone>(children.Count);
        foreach (var child in children)
        {
            list.Add(bonesByInternalIndex[child]);
        }
        return list;
    }

    public List<Bone> GetDescendants(Bone bone)
    {
        var bones = GetChildren(bone);
        for (int i = 0; i < bones.Count; i++)
        {
            var children = bonesByInternalIndex[bones[i].Index].Children;
            bones.EnsureCapacity(bones.Count + children.Count);
            foreach (var child in children)
            {
                bones.Add(bonesByInternalIndex[child]);
            }
        }
        return bones;
    }
}

public enum BoneType
{
    Neck,
    Face,
    SpineC,

    ArmLeft,
    ForearmLeft,
    HandLeft,
    WristLeft,
    ThumbLeftA,
    ThumbLeftB,
    IndexFingerLeftA,
    IndexFingerLeftB,
    MiddleFingerLeftA,
    MiddleFingerLeftB,
    RingFingerLeftA,
    RingFingerLeftB,
    PinkyFingerLeftA,
    PinkyFingerLeftB,

    ArmRight,
    ForearmRight,
    HandRight,
    WristRight,
    ThumbRightA,
    ThumbRightB,
    IndexFingerRightA,
    IndexFingerRightB,
    MiddleFingerRightA,
    MiddleFingerRightB,
    RingFingerRightA,
    RingFingerRightB,
    PinkyFingerRightA,
    PinkyFingerRightB,
}
public unsafe class Bone(int index, int parent, hkQsTransformf referencePose, string name)
{
    internal readonly int Index = index;
    internal readonly int Parent = parent;
    internal List<int> Children = new List<int>();
    internal readonly hkQsTransformf ReferencePose = referencePose;
    public readonly String Name = name;

    public hkQsTransformf* GetModelTransforms(hkaPose* pose)
    {
        return pose->AccessBoneModelSpace(Index, hkaPose.PropagateOrNot.DontPropagate);
    }
    public hkQsTransformf* GetLocalTransforms(hkaPose* pose)
    {
        return pose->AccessBoneLocalSpace(Index);
    }
}