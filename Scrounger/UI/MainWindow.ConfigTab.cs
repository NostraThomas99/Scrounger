using OtterGui;
using OtterGui.Raii;

namespace Scrounger.UI;

public partial class MainWindow
{
    public void DrawConfigTab()
    {
        if (!Scrounger.Config.DrawDebugTab) return;
        using var id = ImRaii.PushId("ConfigTab");
        using var tab = ImRaii.TabItem("Configuration");
        if (!tab)
            return;
        ImGuiUtil.HoverTooltip("Settings 'n' such");
    }
}