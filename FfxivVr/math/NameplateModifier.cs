using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonNamePlate;

namespace FfxivVR;
unsafe internal class NameplateModifier(Logger logger, IGameGui gameGui, ITargetManager targetManager)
{
    private IGameGui gameGui = gameGui;
    private ITargetManager targetManager = targetManager;
    private Logger logger = logger;

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
            // Crash is here
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
