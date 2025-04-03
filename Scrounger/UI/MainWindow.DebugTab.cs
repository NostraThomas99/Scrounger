using ImGuiNET;
using OtterGui;
using OtterGui.Raii;

namespace Scrounger.UI;

public partial class MainWindow
{
    public void DrawDebugTab()
    {
        if (!Scrounger.Config.DrawDebugTab) return;
        using var id = ImRaii.PushId("DebugTab");
        using var tab = ImRaii.TabItem("Debug");
        if (!tab)
            return;
        ImGuiUtil.HoverTooltip("Debuggeratingsfunktioner");
        DrawWorldLocations();
    }

    private void DrawWorldLocations()
    {
        using var id = ImRaii.PushId("WorldLocations");
        if (ImGui.CollapsingHeader("World Locations"))
        {
            foreach (var node in Scrounger.WorldData.NodeLocations)
            {
                ImGui.Text(node.Key.ToString());
                ImGui.Indent();
                foreach (var location in node.Value)
                {
                    ImGui.Indent();
                    ImGui.Text(location.Key.ToString());
                    foreach (var item in location.Value)
                    {
                        ImGui.Text(item.ToString());
                    }
                    ImGui.Unindent();
                }
                ImGui.Unindent();
            }
        }
    }
}