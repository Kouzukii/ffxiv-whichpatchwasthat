using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;

namespace WhichPatchWasThat;

public class WhichPatchWasThatPlugin : IDalamudPlugin {
    public string Name => "Which Patch Was That?";

    internal GameGui GameGui { get; }
    internal Hooks Hooks { get; }

    public WhichPatchWasThatPlugin(GameGui gameGui) {
        GameGui = gameGui;
        Hooks = new Hooks(this);
    }

    public void Dispose() {
        Hooks.Dispose();
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
        if (seStr.TextValue.StartsWith("["))
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
