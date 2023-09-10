using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace WhichPatchWasThat;

public class Hooks : IDisposable {
    private readonly WhichPatchWasThatPlugin plugin;

    private unsafe delegate void* AddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData* nums, StringArrayData* strings);
    
    private unsafe delegate void* AddonOnSetup(AtkUnitBase* atkUnitBase, uint a2, void* a3);

    [Signature("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 42 20", DetourName = nameof(ItemDetailOnUpdateDetour))]
    private Hook<AddonOnUpdate>? ItemDetailOnUpdateHook { get; init; }
    
    [Signature("E8 ?? ?? ?? ?? 48 8B D5 48 8B CF E8 ?? ?? ?? ?? 41 8D 45 FF 83 F8 01 77 6D", DetourName = nameof(ActionTooltipDetour))]
    private Hook<AddonOnUpdate>? ActionTooltipHook { get; init; }
    
    [Signature("85 D2 0F 84 CB 05 00 00 53 55 41 57 48 83 EC 50 49 8B E8 44 8B FA 48 8B D9 4D 85 C0 0F 84 A9 05", DetourName = nameof(JournalAcceptOnSetupDetour))]
    private Hook<AddonOnSetup>? JournalAcceptOnSetup { get; init; }
    
    [Signature("4C 8B DC 53 41 54 41 56 48 81 EC 30 01 00 00 48 8B 05 AA C7 27 01 48 33 C4 48 89 84 24 00 01 00", DetourName = nameof(JournalDetailOnRefreshDetour))]
    private Hook<AddonOnSetup>? JournalDetailOnRefresh { get; init; }

    private readonly nint mem = Marshal.AllocHGlobal(4096);

    public Hooks(WhichPatchWasThatPlugin plugin) {
        this.plugin = plugin;
        SignatureHelper.Initialise(this);
        ItemDetailOnUpdateHook?.Enable();
        ActionTooltipHook?.Enable();
        JournalAcceptOnSetup?.Enable();
        JournalDetailOnRefresh?.Enable();
    }

    private static unsafe SeString GetTooltipString(StringArrayData* stringArrayData, int field) {
        var stringAddress = new nint(stringArrayData->StringArray[field]);
        return stringAddress != nint.Zero ? MemoryHelper.ReadSeStringNullTerminated(stringAddress) : new SeString();
    }

    private unsafe void SetTooltipString(StringArrayData* stringArrayData, int field, SeString seString) {
        var bytes = seString.Encode();
        Marshal.Copy(bytes, 0, mem, bytes.Length);
        Marshal.WriteByte(mem, bytes.Length, 0);
        stringArrayData->StringArray[field] = (byte*)mem;
    }

    private unsafe void* ItemDetailOnUpdateDetour(AtkUnitBase* atkUnitBase, NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        try {
            var seStr = GetTooltipString(stringArrayData, 14);
            if (plugin.UpdateTooltip(seStr)) {
                SetTooltipString(stringArrayData, 14, seStr);
            }
        } catch (Exception e) {
            PluginLog.LogError(e, "Could not determine item patch");
        }

        return ItemDetailOnUpdateHook!.Original(atkUnitBase, numberArrayData, stringArrayData);
    }
    
    private unsafe void* ActionTooltipDetour(AtkUnitBase* addon, NumberArrayData* nums, StringArrayData* strings) {
        try {
            var seStr = GetTooltipString(strings, 1);
            if (plugin.UpdateActionToolTip(seStr)) {
                SetTooltipString(strings, 1, seStr);
            }
        } catch (Exception e) {
            PluginLog.LogError(e, "Could not determine item patch");
        }
        
        return ActionTooltipHook!.Original(addon, nums, strings);
    }

    private unsafe void* JournalAcceptOnSetupDetour(AtkUnitBase* atkUnitBase, uint a2, void* a3) {
        var result = JournalAcceptOnSetup!.Original(atkUnitBase, a2, a3);
        try {
            plugin.QuestPatch.AddPatchToJournalAccept(atkUnitBase);
        } catch (Exception e) {
            PluginLog.LogError(e, "Failed to update JournalAccept");
        }

        return result;
    }

    private unsafe void* JournalDetailOnRefreshDetour(AtkUnitBase* atkUnitBase, uint a2, void* a3) {
        var result = JournalDetailOnRefresh!.Original(atkUnitBase, a2, a3);
        try {
            plugin.QuestPatch.AddPatchToJournalDetail(atkUnitBase);
        } catch (Exception e) {
            PluginLog.LogError(e, "Failed to update JournalAccept");
        }
        return result;
    }
    
    public void Dispose() {
        ItemDetailOnUpdateHook?.Dispose();
        ActionTooltipHook?.Dispose();
        JournalAcceptOnSetup?.Dispose();
        JournalDetailOnRefresh?.Dispose();
        Marshal.FreeHGlobal(mem);
    }
}
