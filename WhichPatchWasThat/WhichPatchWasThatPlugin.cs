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
}
