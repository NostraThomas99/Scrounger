using System.Numerics;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using GatherBuddy.Classes;
using GatherBuddy.Time;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Scrounger.AutoGather.Lists;
using Scrounger.Utils.Extensions;

namespace Scrounger.UI;

public partial class MainWindow
{
    private GatherListSelector _selector;
    public void DrawGatherablesTab()
    {
        using var id = ImRaii.PushId("AutoGatherTab");
        using var tab = ImRaii.TabItem("Auto-Gather");
        if (!tab)
            return;
        ImGuiUtil.HoverTooltip("Get dem goodies");
        ImGui.BeginChild("GatherTab", new Vector2(0,0), false);
        _selector.Draw(SelectorWidth);
        ImGui.SameLine();
        DrawGatherList(_selector.Current);
        ImGui.EndChild();
    }

    private void DrawGatherList(AutoGatherList? selectorCurrent)
    {
        using var id = ImRaii.PushId("GatherList");
        ImGui.BeginChild("GatherList", new Vector2(0,0), true);
        if (selectorCurrent == null)
        {
            ImGui.Text("No gather list selected");
        }
        else
        {
            DrawGatherListInternal(selectorCurrent);
        }
        ImGui.EndChild();
    }

    private void DrawGatherListInternal(AutoGatherList selectorCurrent)
    {
        DrawItemAdd(selectorCurrent);
        ImGui.Separator();
        ImGui.BeginGroup();
        var enabled = selectorCurrent.Enabled;
        if (ImGui.Checkbox($"Enabled##list", ref enabled))
        {
            _plugin.AutoGatherListsManager.ToggleList(selectorCurrent);
        }
        ImGui.Separator();
        ImGui.BeginGroup();

        ImGui.BeginTable("##gatherablesTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Availability");
        ImGui.TableSetupColumn("Preferred Location");
        ImGui.TableSetupColumn("Inventory Amount");
        ImGui.TableSetupColumn("Desired Quantity");
        ImGui.TableSetupColumn("Remove");

        ImGui.TableHeadersRow();
        foreach (var item in selectorCurrent.Items)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(item.Name.ToString());
            ImGui.TableSetColumnIndex(1);
            ImGui.Text(GetUptimeString(item));
            ImGui.TableSetColumnIndex(2);
            ImGui.Text("(Coming soon...)");
            ImGui.TableSetColumnIndex(3);
            ImGui.Text(item.GetInventoryCount().ToString());
            ImGui.TableSetColumnIndex(4);
            ImGui.SetNextItemWidth(100);
            var quantity = (int)selectorCurrent.Quantities[item];
            if (ImGui.InputInt($"##quantity{item.ItemId}", ref quantity))
            {
                _plugin.AutoGatherListsManager.ChangeQuantity(selectorCurrent, item, (uint)quantity);
            }
            ImGui.TableSetColumnIndex(5);
            if (ImGui.Button("Remove"))
            {
                _plugin.AutoGatherListsManager.RemoveItem(selectorCurrent, item);
            }

        }
        ImGui.EndTable();

        ImGui.EndGroup();
    }

    private string GetUptimeString(Gatherable item)
    {
        var time = (uint)Scrounger.Time.ServerTime.Time;
        foreach (var node in item.NodeList)
        {
            if (node.Times.AlwaysUp())
                return "Always Available";

            return node.Times.PrintHours();
        }
        return "Never Available";
    }

    private string _searchTerm = string.Empty;
    private void DrawItemAdd(AutoGatherList selectorCurrent)
    {
        ImGui.Text("Item Search");
        ImGui.InputText("##searchBar", ref _searchTerm, 100);

        ImGui.BeginChild($"ItemList", new Vector2(0, 100), true);
        if (!string.IsNullOrEmpty(_searchTerm))
        {
            var matchingItems = Scrounger.WorldData.Gatherables.Where(item =>
                item.Value.Name.ToString().Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));

            if (matchingItems.Any())
            {
                foreach (var item in matchingItems)
                {
                    if (ImGui.Selectable(item.Value.Name.ToString()))
                    {
                        selectorCurrent.Add(item.Value, 1);
                        _plugin.AutoGatherListsManager.Save();
                        Svc.Log.Debug($"Added shopping list item: {item.Value.Name}");
                        _searchTerm = string.Empty;
                    }
                }
            }
        }
        else
        {
            ImGui.Text("Items will appear here when you enter a search term.");
        }
        ImGui.EndChild();
    }
}