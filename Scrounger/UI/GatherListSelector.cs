using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Scrounger.AutoGather.Lists;

namespace Scrounger.UI;

public class GatherListSelector : ItemSelector<AutoGatherList>
{
    private readonly Scrounger _plugin;

    public GatherListSelector(Scrounger plugin) : base(plugin.AutoGatherListsManager.Lists, Flags.All)
    {
        _plugin = plugin;
    }

    protected override bool OnDraw(int idx)
    {
        using var id = ImRaii.PushId(idx);
        return ImGui.Selectable(Items[idx].Name, idx == CurrentIdx);
    }

    protected override bool Filtered(int idx) => Filter.Length != 0 && Items[idx].Name.Contains(Filter, StringComparison.InvariantCultureIgnoreCase);

    protected override bool OnDelete(int idx)
    {
        _plugin.AutoGatherListsManager.DeleteList(idx);
        return true;
    }

    protected override bool OnAdd(string name)
    {
        var list = new AutoGatherList();
        list.Name = name;
        _plugin.AutoGatherListsManager.AddList(list);
        return true;
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        return base.OnMove(idx1, idx2);
    }

    protected override bool OnClipboardImport(string name, string data)
    {
        return base.OnClipboardImport(name, data);
    }

    protected override bool OnDuplicate(string name, int idx)
    {
        return base.OnDuplicate(name, idx);
    }
}