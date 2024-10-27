using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System.Collections.Generic;
using static FfxivVR.Plugin;

namespace FfxivVR;
unsafe internal class GameVisibility
{
    private readonly Logger logger;
    private GameState gameState;

    public enum ModelCullTypes : byte
    {
        None = 0,
        InsideCamera = 66,
        OutsideCullCone = 67,
        Visible = 75
    }

    public GameVisibility(Logger logger, GameState gameState)
    {
        this.logger = logger;
        this.gameState = gameState;
    }

    public void ForceFirstPersonBodyVisible()
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
            var mainHand = character->DrawData.Weapon(DrawDataContainer.WeaponSlot.MainHand);
            if (mainHand.DrawObject != null)
            {
                mainHand.DrawObject->Flags = (byte)ModelCullTypes.Visible;
            }
            var offHand = character->DrawData.Weapon(DrawDataContainer.WeaponSlot.OffHand);
            if (offHand.DrawObject != null)
            {
                offHand.DrawObject->Flags = (byte)ModelCullTypes.Visible;
            }
            var unknown = character->DrawData.Weapon(DrawDataContainer.WeaponSlot.Unk);
            if (unknown.DrawObject != null)
            {
                unknown.DrawObject->Flags = (byte)ModelCullTypes.Visible;
            }
        }
    }

    private Character* getCharacter()
    {
        Character* character = gameState.GetGposeTarget();
        if (gameState.IsGPosing() && character == null)
        {
            return null;
        }
        if (character != null)
        {
            return character;
        }
        var player = ClientState.LocalPlayer;
        if (player == null)
        {
            return null;
        }
        return (Character*)player!.Address;
    }

    public void HideHeadMesh()
    {
        if (!gameState.IsFirstPerson())
        {
            return;
        }
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
                if (neckIndex >= partial->LocalPose.Length)
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
}
