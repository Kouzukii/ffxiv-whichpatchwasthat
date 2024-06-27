using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace WhichPatchWasThat;

public class WhichPatchWasThatPlugin : IDalamudPlugin {
    internal IGameGui GameGui { get; }
    internal IPluginLog PluginLog { get; }
    internal QuestPatch QuestPatch { get; }

    private unsafe delegate void AddonOnRefresh(AtkUnitBase* addon, uint valueCount, AtkValue* values);

    [Signature("4C 8B DC 53 41 54 41 56 48 81 EC 40 01 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4", DetourName = nameof(JournalDetailOnRefreshDetour))]
    private readonly Hook<AddonOnRefresh> journalDetailOnRefresh = null!;

    public WhichPatchWasThatPlugin(IGameGui gameGui, IAddonLifecycle addonLifecycle, IGameInteropProvider gameInteropProvider, IPluginLog pluginLog) {
        GameGui = gameGui;
        PluginLog = pluginLog;
        QuestPatch = new QuestPatch(this);
        gameInteropProvider.InitializeFromAttributes(this);
        journalDetailOnRefresh.Enable();
        addonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "ItemDetail", ItemDetailOnRequestedUpdate);
        addonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "ActionDetail", ActionDetailOnRequestedUpdate);
        addonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalAccept", JournalAcceptOnSetup);
    }

    private unsafe void ActionDetailOnRequestedUpdate(AddonEvent type, AddonArgs args) {
        if (args is not AddonRequestedUpdateArgs requestedUpdateArgs) return;
        var numberArrayData = ((NumberArrayData**)requestedUpdateArgs.NumberArrayData)[31];
        var stringArrayData = ((StringArrayData**)requestedUpdateArgs.StringArrayData)[28];
        if ((numberArrayData->IntArray[7] & 1) == 0) return;

        var seStr = GetTooltipString(stringArrayData, 1);
        if (UpdateActionToolTip(seStr)) {
            stringArrayData->SetValue(1, seStr.Encode(), false, true, true);
        }
    }

    private unsafe void JournalDetailOnRefreshDetour(AtkUnitBase* atkUnitBase, uint valueCount, AtkValue* values) {
        journalDetailOnRefresh.Original(atkUnitBase, valueCount, values);
        try {
            QuestPatch.AddPatchToJournalDetail(atkUnitBase);
        } catch (Exception e) {
            PluginLog.Error(e, "Failed to patch JournalDetail");
        }
    }

    private unsafe void JournalAcceptOnSetup(AddonEvent type, AddonArgs args) {
        QuestPatch.AddPatchToJournalAccept((AtkUnitBase*)args.Addon);
    }

    private unsafe void ItemDetailOnRequestedUpdate(AddonEvent type, AddonArgs args) {
        if (args is not AddonRequestedUpdateArgs requestedUpdateArgs) return;
        var numberArrayData = ((NumberArrayData**)requestedUpdateArgs.NumberArrayData)[29];
        var stringArrayData = ((StringArrayData**)requestedUpdateArgs.StringArrayData)[26];
        if ((numberArrayData->IntArray[2] & 1) == 0) return;

        var seStr = GetTooltipString(stringArrayData, 14);
        if (UpdateTooltip(seStr)) {
            stringArrayData->SetValue(14, seStr.Encode(), false, true, true);
        }
    }

    private static unsafe SeString GetTooltipString(StringArrayData* stringArrayData, int field) {
        var stringAddress = new nint(stringArrayData->StringArray[field]);
        return stringAddress != nint.Zero ? MemoryHelper.ReadSeStringNullTerminated(stringAddress) : new SeString();
    }

    public void Dispose() {
        journalDetailOnRefresh.Dispose();
        QuestPatch.Dispose();
    }

    public bool UpdateTooltip(SeString seStr) {
        if (seStr.TextValue.StartsWith("["))
            return false;

        var id = GameGui.HoveredItem;
        if (id < 2000000)
            id %= 500000;

        var patch = ItemPatchMapper.GetPatch(id);
        if (patch == null)
            return false;

        seStr.Payloads.Insert(0, new UIForegroundPayload(3));
        seStr.Payloads.Insert(1, new TextPayload($"[Patch {patch}]   "));
        seStr.Payloads.Insert(2, new UIForegroundPayload(0));
        return true;
    }

    private string? GetActionPatch() {
        var item = (int)GameGui.HoveredAction.ActionKind switch {
            32 => ActionToItemMapper.GetItemOfMinion(GameGui.HoveredAction.ActionID),
            37 => ActionToItemMapper.GetItemOfMount(GameGui.HoveredAction.ActionID),
            52 => ActionToItemMapper.GetItemOfFashionAccessory(GameGui.HoveredAction.ActionID),
            _ => null
        };
        return item is { } id ? ItemPatchMapper.GetPatch(id) : null;
    }

    public bool UpdateActionToolTip(SeString seStr) {
        if (seStr.TextValue.StartsWith('['))
            return false;
        var patch = GetActionPatch();
        if (patch == null)
            return false;
        if (seStr.Payloads.Count >= 1) {
            seStr.Payloads.Add(new TextPayload("   "));
        }

        seStr.Payloads.Add(new UIForegroundPayload(3));
        seStr.Payloads.Add(new TextPayload($"[Patch {patch}]"));
        seStr.Payloads.Add(new UIForegroundPayload(0));

        return true;
    }
}
