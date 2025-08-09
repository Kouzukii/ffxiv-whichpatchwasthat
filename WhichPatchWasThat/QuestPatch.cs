using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace WhichPatchWasThat;

public class QuestPatch(WhichPatchWasThatPlugin plugin) : IDisposable {
    private const int NodeId = 58327;

    public static unsafe void AddPatchToJournalAccept(AtkUnitBase* journalAccept) {
        var insertNode = journalAccept->GetNodeById(8);
        if (insertNode == null)
            return;

        var patchNode = FindPatchNode(journalAccept);

        var questId = ((uint*)journalAccept)[174];
        var patch = QuestPatchMapper.GetPatch(questId);
        if (patch == null) {
            if (patchNode != null)
                patchNode->AtkResNode.ToggleVisibility(false);
            return;
        }

        if (patchNode == null) {
            patchNode = CreatePatchNode(journalAccept, 40, insertNode);
            if (patchNode == null)
                return;
        }

        patchNode->AtkResNode.ToggleVisibility(true);
        patchNode->SetText($"Patch {patch}");
    }

    public static unsafe void AddPatchToJournalDetail(AtkUnitBase* journalDetail) {
        var insertNode = journalDetail->GetNodeById(9);
        if (insertNode == null)
            return;

        var patchNode = FindPatchNode(journalDetail);

        var questId = ((uint*)journalDetail)[142];
        var questType = ((uint*)journalDetail)[143];
        var patch = QuestPatchMapper.GetPatch(questId);
        if (patch == null || questType != 1) {
            if (patchNode != null)
                patchNode->AtkResNode.ToggleVisibility(false);
            return;
        }

        if (patchNode == null) {
            patchNode = CreatePatchNode(journalDetail, 43, insertNode);
            if (patchNode == null)
                return;
        }

        patchNode->AtkResNode.ToggleVisibility(true);
        patchNode->SetText($"Patch {patch}");
    }


    private static unsafe AtkTextNode* FindPatchNode(AtkUnitBase* unitBase) {
        for (var i = 0; i < unitBase->UldManager.NodeListCount; i++) {
            var node = unitBase->UldManager.NodeList[i];
            if (node == null || node->NodeId != NodeId)
                continue;
            return (AtkTextNode*)node;
        }

        return null;
    }

    private static unsafe AtkTextNode* CreatePatchNode(AtkUnitBase* unitBase, uint journalCanvasId, AtkResNode* insertNode) {
        var componentNode = (AtkComponentNode*)unitBase->GetNodeById(journalCanvasId);
        if (componentNode == null)
            return null;
        var baseNode = componentNode->Component->GetTextNodeById(7);
        if (baseNode == null)
            return null;
        var patchNode = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
        patchNode->AtkResNode.Type = NodeType.Text;
        patchNode->AtkResNode.NodeId = NodeId;
        patchNode->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
        patchNode->AtkResNode.DrawFlags = 0;
        patchNode->AtkResNode.X = 15;
        patchNode->AtkResNode.Y = 40;
        patchNode->AtkResNode.Width = 50;
        patchNode->AtkResNode.Color = baseNode->AtkResNode.Color;
        patchNode->TextColor = baseNode->TextColor;
        patchNode->EdgeColor = baseNode->EdgeColor;
        patchNode->LineSpacing = 18;
        patchNode->AlignmentFontType = 0x00;
        patchNode->FontSize = 12;
        patchNode->TextFlags = baseNode->TextFlags;
        var prev = insertNode->PrevSiblingNode;
        patchNode->AtkResNode.ParentNode = insertNode->ParentNode;
        insertNode->PrevSiblingNode = (AtkResNode*)patchNode;
        if (prev != null)
            prev->NextSiblingNode = (AtkResNode*)patchNode;
        patchNode->AtkResNode.PrevSiblingNode = prev;
        patchNode->AtkResNode.NextSiblingNode = insertNode;
        unitBase->UldManager.UpdateDrawNodeList();
        return patchNode;
    }

    public void Dispose() {
        unsafe {
            void DisposePatchNode(string addonName) {
                var atkUnitBase = (AtkUnitBase*)plugin.GameGui.GetAddonByName(addonName).Address;
                if (atkUnitBase == null)
                    return;
                for (var n = 0; n < atkUnitBase->UldManager.NodeListCount; n++) {
                    var node = atkUnitBase->UldManager.NodeList[n];
                    if (node == null)
                        continue;
                    if (node->NodeId != NodeId)
                        continue;
                    if (node->ParentNode != null && node->ParentNode->ChildNode == node)
                        node->ParentNode->ChildNode = node->PrevSiblingNode;
                    if (node->PrevSiblingNode != null)
                        node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
                    if (node->NextSiblingNode != null)
                        node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
                    atkUnitBase->UldManager.UpdateDrawNodeList();
                    node->Destroy(true);
                    break;
                }
            }
            DisposePatchNode("JournalAccept");
            DisposePatchNode("JournalDetail");
        }
    }
}
