using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.Classes;
using GatherBuddy.Interfaces;

namespace Scrounger.Utils.Extensions;

public static class GatherableExtensions
{
    public static List<Vector3> GetPositions(this Gatherable gatherable)
    {
        var positions = new List<Vector3>();
        foreach (var node in gatherable.NodeList)
        {
            var worldLocations = Scrounger.WorldData.WorldLocationsByNodeId[node.Id];
            positions.AddRange(worldLocations);
        }
        return positions;
    }

    public static List<Vector3> GetNodePositions(this GatheringNode node)
    {
        var positions = new List<Vector3>();
        foreach (var worldLocation in Scrounger.WorldData.WorldLocationsByNodeId[node.Id])
            positions.Add(worldLocation);
        return positions;
    }

    public unsafe static int GetInventoryCount(this IGatherable gatherable)
    {
        var inventory = InventoryManager.Instance();
        return inventory->GetInventoryItemCount(gatherable.ItemId, false, false, false, (short)(gatherable.ItemData.IsCollectable ? 1 : 0));
    }

    public static bool IsCrystal(this IGatherable gatherable)
    {
        return gatherable.ItemData.FilterGroup == 11;
    }

    public static bool IsTreasureMap(this IGatherable gatherable)
    {
        return gatherable.ItemData.FilterGroup == 18;
    }
}