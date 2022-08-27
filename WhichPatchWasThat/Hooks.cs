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

    [Signature("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 42 20", DetourName = nameof(ItemDetailOnUpdateDetour))]
    private Hook<AddonOnUpdate>? ItemDetailOnUpdateHook { get; init; }

    private readonly IntPtr mem = Marshal.AllocHGlobal(4096);

    public Hooks(WhichPatchWasThatPlugin plugin) {
        this.plugin = plugin;
        SignatureHelper.Initialise(this);
        ItemDetailOnUpdateHook?.Enable();
    }

    private static unsafe SeString? GetTooltipString(StringArrayData* stringArrayData, int field) {
        var stringAddress = new IntPtr(stringArrayData->StringArray[field]);
        return stringAddress != IntPtr.Zero ? MemoryHelper.ReadSeStringNullTerminated(stringAddress) : null;
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
            if (seStr != null && plugin.UpdateTooltip(seStr)) {
                SetTooltipString(stringArrayData, 14, seStr);
            }
        } catch (Exception e) {
            PluginLog.LogError(e, "Could not determine item patch");
        }

        return ItemDetailOnUpdateHook!.Original(atkUnitBase, numberArrayData, stringArrayData);
    }

    public void Dispose() {
        ItemDetailOnUpdateHook?.Dispose();
        Marshal.FreeHGlobal(mem);
    }
}
