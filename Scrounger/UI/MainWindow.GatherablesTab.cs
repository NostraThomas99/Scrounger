using System.Numerics;
using ECommons.DalamudServices;
using GatherBuddy.Classes;
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
        using var id = ImRaii.PushId("GatherablesTab");
        using var tab = ImRaii.TabItem("Gatherables");
        if (!tab)
            return;
        ImGuiUtil.HoverTooltip("A list of every gatherable in the game");
        _selector.Draw(100);
        ImGui.SameLine();
        DrawGatherList(_selector.Current);
    }

    private void DrawGatherList(AutoGatherList? selectorCurrent)
    {
        using var id = ImRaii.PushId("GatherList");
        ImGui.BeginGroup();
        if (selectorCurrent == null)
        {
            ImGui.Text("No gather list selected");
        }
        else
        {
            DrawGatherListInternal(selectorCurrent);
        }
        ImGui.EndGroup();
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

        ImGui.BeginTable("##gatherablesTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Inventory Amount");
        ImGui.TableSetupColumn("Desired Quantity");

        ImGui.TableHeadersRow();
        foreach (var item in selectorCurrent.Items)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(item.Name.ToString());
            ImGui.TableSetColumnIndex(1);
            ImGui.Text(item.GetInventoryCount().ToString());
            ImGui.TableSetColumnIndex(2);
            ImGui.SetNextItemWidth(100);
            var quantity = (int)selectorCurrent.Quantities[item];
            if (ImGui.InputInt($"##quantity{item.ItemId}", ref quantity))
            {
                _plugin.AutoGatherListsManager.ChangeQuantity(selectorCurrent, item, (uint)quantity);
            }
        }
        ImGui.EndTable();

        ImGui.EndGroup();
    }

    private string _searchTerm = string.Empty;
    private Gatherable? _selectedItem;
    private void DrawItemAdd(AutoGatherList selectorCurrent)
    {
        ImGui.Text("Item Search");
        ImGui.InputText("##searchBar", ref _searchTerm, 100);

        var popUpId = "Item Search Popup";
        if (!string.IsNullOrEmpty(_searchTerm) && _selectedItem is null)
        {
            ImGui.OpenPopup(popUpId);
            var matchingItems = Scrounger.WorldData.Gatherables.Where(item =>
                item.Value.Name.ToString().Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));

            if (matchingItems.Any())
            {
                ImGui.BeginChild($"ItemList", new Vector2(0, 150), true);
                foreach (var item in matchingItems)
                {
                    if (ImGui.Selectable(item.Value.Name.ToString()))
                    {
                        _selectedItem = item.Value;
                        _searchTerm = item.Value.Name.ToString();
                    }
                }

                ImGui.EndChild();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Add"))
        {
            if (_selectedItem is null)
            {
                Svc.Log.Warning("No item to add to shopping list");
                return;
            }
            selectorCurrent.Add(_selectedItem);
            _searchTerm = string.Empty;
            _selectedItem = null;
            _plugin.AutoGatherListsManager.Save();
            Svc.Log.Debug($"Added shopping list item: {_selectedItem?.Name}");
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            _selectedItem = null;
            _searchTerm = string.Empty;
        }
    }
}