using System.Reflection;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui.Log;
using OtterGui.Raii;

namespace Scrounger.UI;

public partial class MainWindow : Window
{
    private readonly Scrounger _plugin;
    public MainWindow(Scrounger plugin) : base($"Scrounger {Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? ""}", ImGuiWindowFlags.None, false)
    {
        _plugin = plugin;
        Size = new(800, 600);
        SizeCondition = ImGuiCond.FirstUseEver;

        _selector = new GatherListSelector(_plugin);
    }

    public override void Draw()
    {
        var enabled = _plugin.AutoGather.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            _plugin.AutoGather.Enabled = enabled;
        }

        ImGui.Text(_plugin.AutoGather.AutoStatus);
        using var tab = ImRaii.TabBar("ScroungerTabs", ImGuiTabBarFlags.Reorderable);
        if (!tab)
            return;
        DrawGatherablesTab();
        DrawDebugTab();
    }
}