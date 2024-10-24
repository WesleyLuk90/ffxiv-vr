using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System.Collections.Generic;
using static FfxivVR.Plugin;

namespace FfxivVR;
unsafe internal class GameVisibility
{
    private readonly Logger logger;

    public GameVisibility(Logger logger)
    {
        this.logger = logger;
    }
    public void UpdateVisibility()
    {
        Character* character = getCharacter();
        if (character == null)
        {
            return;
        }
        if (character->GameObject.DrawObject != null)
        {
            character->GameObject.DrawObject->Flags = (byte)ModelCullTypes.Visible;

            if (character->Mount.MountObject != null)
            {
                if (character->Mount.MountObject->DrawObject != null)
                {
                    character->Mount.MountObject->DrawObject->Flags = (byte)ModelCullTypes.Visible;
                }
            }
            if (character->OrnamentData.OrnamentObject != null)
            {
                if (character->OrnamentData.OrnamentObject->DrawObject != null)
                {
                    character->OrnamentData.OrnamentObject->DrawObject->Flags = (byte)ModelCullTypes.Visible;
                }
            }
        }
    }

    private Character* getCharacter()
    {
        var player = ClientState.LocalPlayer;
        if (player == null)
        {
            return null;
        }
        return (Character*)player!.Address;
    }

    public void HideHeadMesh()
    {
        Character* character = getCharacter();
        if (character == null)
        {
            return;
        }
        var characterBase = (CharacterBase*)character->GameObject.DrawObject;
        if (characterBase == null)
        {
            return;
        }
        var skeleton = characterBase->Skeleton;
        if (skeleton == null)
        {
            return;
        }
        var neckChildren = new List<int>();
        int? neckIndex = null;
        if (skeleton->SkeletonResourceHandles != null && skeleton->SkeletonResourceHandles[0] != null)
        {
            var havokSkeleton = skeleton->SkeletonResourceHandles[0]->HavokSkeleton;
            for (int currentBone = 0; currentBone < havokSkeleton->Bones.Length; currentBone++)
            {
                var bone = havokSkeleton->Bones[currentBone];
                var parent = havokSkeleton->ParentIndices[currentBone];
                var name = bone!.Name!.String;
                if (name == "j_kubi")
                {
                    neckIndex = currentBone;
                }
                if (parent == neckIndex)
                {
                    neckChildren.Add(currentBone);
                }
            }
        }
        if (neckIndex != null)
        {
            for (int i = 0; i < skeleton->PartialSkeletonCount; i++)
            {
                var partial = skeleton->PartialSkeletons[i].GetHavokPose(0);
                if (partial == null)
                {
                    continue;
                }
                var transform = partial->LocalPose[(int)neckIndex!];
                transform.Scale.X = 0.00001f;
                transform.Scale.Y = 0.00001f;
                transform.Scale.Z = 0.00001f;
                transform.Scale.W = 0;
                partial->LocalPose[(int)neckIndex!] = transform;

                neckChildren.ForEach((c) =>
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
                });
            }
        }
        else
        {
            logger.Debug("Neck bone not found");
        }
    }
}

class Bone
{
    public List<Bone> Children;
    public string name;
    public Bone(string name)
    {
        this.name = name;
    }
}
