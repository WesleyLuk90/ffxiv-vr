using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonNamePlate;

namespace FfxivVR;
unsafe internal class GameVisibility
{
    private readonly Logger logger;
    private GameState gameState;
    private readonly IGameGui gameGui;
    private readonly ITargetManager targetManager;
    private readonly IClientState clientState;

    public enum ModelCullTypes : byte
    {
        None = 0,
        InsideCamera = 66,
        OutsideCullCone = 67,
        Visible = 75
    }

    public GameVisibility(Logger logger, GameState gameState, IGameGui gameGui, ITargetManager targetManager, IClientState clientState)
    {
        this.logger = logger;
        this.gameState = gameState;
        this.gameGui = gameGui;
        this.targetManager = targetManager;
        this.clientState = clientState;
    }


    public void ForceFirstPersonBodyVisible()
    {
        if (gameState.IsInCutscene())
        {
            return;
        }
        UpdateVisbility(targetManager.Target, true);
        if (gameState.IsGPosing())
        {
            UpdateVisbility(clientState.LocalPlayer, false);
        }
        Character* character = getCharacterOrGpose();
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
            var drawData = character->DrawData;
            if (character->IsWeaponDrawn || !drawData.IsWeaponHidden)
            {
                var mainHand = drawData.Weapon(DrawDataContainer.WeaponSlot.MainHand);
                if (mainHand.DrawObject != null)
                {
                    mainHand.DrawObject->Flags = (byte)ModelCullTypes.Visible;
                }
                var offHand = drawData.Weapon(DrawDataContainer.WeaponSlot.OffHand);
                if (offHand.DrawObject != null)
                {
                    offHand.DrawObject->Flags = (byte)ModelCullTypes.Visible;
                }
                var unknown = drawData.Weapon(DrawDataContainer.WeaponSlot.Unk);
                if (unknown.DrawObject != null)
                {
                    unknown.DrawObject->Flags = (byte)ModelCullTypes.Visible;
                }
            }
        }
    }

    private void UpdateVisbility(IGameObject? gameObject, bool visible)
    {
        if (gameObject == null)
        {
            return;
        }
        GameObject* realObject = (GameObject*)gameObject.Address;
        if (realObject == null)
        {
            return;
        }
        if (realObject->DrawObject == null)
        {
            return;
        }
        realObject->DrawObject->Flags = visible ? (byte)ModelCullTypes.Visible : (byte)ModelCullTypes.InsideCamera;
    }

    private Character* getCharacterOrGpose()
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
        var player = clientState.LocalPlayer;
        if (player == null)
        {
            return null;
        }
        return (Character*)player!.Address;
    }

    public void HideHeadMesh()
    {
        if (gameState.IsInCutscene())
        {
            return;
        }
        if (!gameState.IsFirstPerson())
        {
            return;
        }
        Character* character = getCharacterOrGpose();
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

    internal void UpdateNamePlates(AddonNamePlate* namePlate)
    {
        //----
        // Disables the target arrow until it can be put in the world
        //----
        AtkUnitBase* targetAddon = (AtkUnitBase*)gameGui!.GetAddonByName("_TargetCursor", 1);
        if (targetAddon != null)
        {
            targetAddon->Alpha = 1;
            targetAddon->Hide(true, false, 0);
            //targetAddon->RootNode->SetUseDepthBasedPriority(true);
        }

        fixed (AtkTextNode** pvrTargetCursor = &vrTargetCursor)
            SetupVRTargetCursor(pvrTargetCursor, 10);

        for (byte i = 0; i < 50; i++)
        {
            NamePlateObject* npObj = &namePlate->NamePlateObjectArray[i];
            AtkComponentBase* npComponent = npObj->RootComponentNode->Component;

            for (int j = 0; j < npComponent->UldManager.NodeListCount; j++)
            {
                AtkResNode* child = npComponent->UldManager.NodeList[j];
                child->SetUseDepthBasedPriority(true);
            }

            npObj->RootComponentNode->Component->UldManager.UpdateDrawNodeList();
        }

        NamePlateObject* selectedNamePlate = null;
        var framework = Framework.Instance();
        UI3DModule* ui3DModule = framework->GetUIModule()->GetUI3DModule();

        IGameObject targObj = targetManager.Target!;
        if (targObj != null)
        {
            TargetSystem* targSys = (TargetSystem*)targObj.Address;
            for (int i = 0; i < ui3DModule->NamePlateObjectInfoCount; i++)
            {
                UI3DModule.ObjectInfo* objectInfo = ui3DModule->NamePlateObjectInfoPointers[i];
                if (objectInfo->GameObject == targSys->Target)
                {
                    selectedNamePlate = &namePlate->NamePlateObjectArray[objectInfo->NamePlateIndex];
                    break;
                }
            }
        }

        fixed (AtkTextNode** pvrTargetCursor = &vrTargetCursor)
        {
            UpdateVRCursorSize(pvrTargetCursor, 10);
            SetVRCursor(pvrTargetCursor, selectedNamePlate);
        }
    }
    private AtkTextNode* vrTargetCursor = null;
    private NamePlateObject* currentNPTarget = null;

    public unsafe bool SetupVRTargetCursor(AtkTextNode** vrTrgCursor, int targetCursorSize)
    {
        if ((*vrTrgCursor) != null)
        {
            return true;
        }

        (*vrTrgCursor) = (AtkTextNode*)Marshal.AllocHGlobal(sizeof(AtkTextNode));
        if ((*vrTrgCursor) == null)
        {
            logger.Info("Failed to allocate memory for text node");
            return false;
        }
        IMemorySpace.Memset((*vrTrgCursor), 0, (ulong)sizeof(AtkTextNode));
        (*vrTrgCursor)->Ctor();

        (*vrTrgCursor)->AtkResNode.Type = NodeType.Text;
        (*vrTrgCursor)->AtkResNode.NodeFlags = NodeFlags.UseDepthBasedPriority;
        (*vrTrgCursor)->AtkResNode.DrawFlags = 12;

        (*vrTrgCursor)->LineSpacing = 12;
        (*vrTrgCursor)->AlignmentFontType = 4;
        (*vrTrgCursor)->FontSize = (byte)targetCursorSize;
        (*vrTrgCursor)->TextFlags = (byte)(TextFlags.AutoAdjustNodeSize | TextFlags.Edge);
        (*vrTrgCursor)->TextFlags2 = 0;

        (*vrTrgCursor)->SetText("↓");

        (*vrTrgCursor)->AtkResNode.ToggleVisibility(true);

        (*vrTrgCursor)->AtkResNode.SetPositionShort(90, -23);
        ushort outWidth = 0;
        ushort outHeight = 0;
        (*vrTrgCursor)->GetTextDrawSize(&outWidth, &outHeight);
        (*vrTrgCursor)->AtkResNode.SetWidth((ushort)(outWidth));
        (*vrTrgCursor)->AtkResNode.SetHeight((ushort)(outHeight));

        // white fill
        (*vrTrgCursor)->TextColor.R = 255;
        (*vrTrgCursor)->TextColor.G = 255;
        (*vrTrgCursor)->TextColor.B = 255;
        (*vrTrgCursor)->TextColor.A = 255;

        // yellow/golden glow
        (*vrTrgCursor)->EdgeColor.R = 235;
        (*vrTrgCursor)->EdgeColor.G = 185;
        (*vrTrgCursor)->EdgeColor.B = 7;
        (*vrTrgCursor)->EdgeColor.A = 255;

        return true;
    }

    public void SetVRCursor(AtkTextNode** vrTrgCursor, NamePlateObject* nameplate)
    {
        // nothing to do!
        if (currentNPTarget == nameplate)
            return;

        if ((*vrTrgCursor) != null)
        {
            if (currentNPTarget != null)
            {
                RemoveVRCursor(vrTrgCursor, currentNPTarget);
                currentNPTarget = null;
            }

            if (nameplate != null)
            {
                AddVRCursor(vrTrgCursor, nameplate);
                currentNPTarget = nameplate;
            }
        }
    }
    public void AddVRCursor(AtkTextNode** vrTrgCursor, NamePlateObject* nameplate)
    {
        if (nameplate != null && (*vrTrgCursor) != null)
        {
            var npComponent = nameplate->RootComponentNode->Component;

            var lastChild = npComponent->UldManager.RootNode;
            while (lastChild->PrevSiblingNode != null) lastChild = lastChild->PrevSiblingNode;

            lastChild->PrevSiblingNode = (AtkResNode*)(*vrTrgCursor);
            (*vrTrgCursor)->AtkResNode.NextSiblingNode = lastChild;
            (*vrTrgCursor)->AtkResNode.ParentNode = (AtkResNode*)nameplate->RootComponentNode;

            npComponent->UldManager.UpdateDrawNodeList();
        }
    }

    public void RemoveVRCursor(AtkTextNode** vrTrgCursor, NamePlateObject* nameplate)
    {
        if (nameplate != null && (*vrTrgCursor) != null)
        {
            var npComponent = nameplate->RootComponentNode->Component;

            var lastChild = npComponent->UldManager.RootNode;
            while (lastChild->PrevSiblingNode != null) lastChild = lastChild->PrevSiblingNode;

            if (lastChild == (*vrTrgCursor))
            {
                lastChild->NextSiblingNode->PrevSiblingNode = null;

                (*vrTrgCursor)->AtkResNode.NextSiblingNode = null;
                (*vrTrgCursor)->AtkResNode.ParentNode = null;

                npComponent->UldManager.UpdateDrawNodeList();
            }
            else
            {
                logger.Error("RemoveVRCursor: lastChild != vrTargetCursor");
            }
        }
    }
    public void UpdateVRCursorSize(AtkTextNode** vrTrgCursor, int targetCursorSize)
    {
        if ((*vrTrgCursor) == null) return;

        (*vrTrgCursor)->FontSize = (byte)targetCursorSize;
        ushort outWidth = 0;
        ushort outHeight = 0;
        (*vrTrgCursor)->GetTextDrawSize(&outWidth, &outHeight);
        (*vrTrgCursor)->AtkResNode.SetWidth(outWidth);
        (*vrTrgCursor)->AtkResNode.SetHeight(outHeight);

        // explanation of these numbers
        // Some setup info:
        // 1. The ↓ character output from GetTextDrawSize is always 1:1 with the
        //    requested font. Font size 100 results in outWidth 100 and outHeight 100.
        // 2. The anchor point for text fields are the upper left corner of the frame.
        // 3. The hand-tuned position of the default font size 100 is x 90, y -23.
        // 
        // Adding the inverted delta offset (and div by 2 for x) correctly moves the ancor
        // from upper left to bottom center. However I noticed that as the font scales
        // up and down, the point of the arrow drifts slightly along the x and y. This
        // is the reason for the * 1.10 and * 1.15. This corrects for the drift and keeps
        // the point of the arrow exactly where it should be.

        const float DriftOffset_X = 1.10f;
        const float DriftOffset_Y = 1.15f;

        short xpos = (short)(90 + ((100 - outWidth) / 2 * DriftOffset_X));
        short ypos = (short)(-23 + (100 - outWidth) * DriftOffset_Y);
        (*vrTrgCursor)->AtkResNode.SetPositionShort(xpos, ypos);
    }
}
