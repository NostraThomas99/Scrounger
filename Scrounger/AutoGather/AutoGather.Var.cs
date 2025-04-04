﻿using Dalamud.Game.ClientState.Conditions;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Scrounger.AutoGather.Lists;
using GatherBuddy.Classes;
using GatherBuddy.Enums;
using GatherBuddy.Interfaces;
using GatherBuddy.Time;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;
using ECommons.DalamudServices;
using Scrounger.Ipc;

namespace Scrounger.AutoGather
{
    public partial class AutoGather
    {
        public bool IsPathing
            => VNavmesh.Path.IsRunning();

        public bool IsPathGenerating
            => VNavmesh.Nav.PathfindInProgress();

        public bool NavReady
            => VNavmesh.Nav.IsReady();

        private bool IsBlacklisted(Vector3 g)
        {
            var blacklisted = Scrounger.Config.BlacklistedNodesByTerritoryId.ContainsKey(Svc.ClientState.TerritoryType)
             && Scrounger.Config.BlacklistedNodesByTerritoryId[Svc.ClientState.TerritoryType].Contains(g);
            return blacklisted;
        }

        public bool IsGathering
            => Svc.Condition[ConditionFlag.Gathering] || Svc.Condition[ConditionFlag.Gathering42];

        public bool? LastNavigationResult { get; set; } = null;
        public Vector3 CurrentDestination { get; private set; } = default;
        private ILocation? CurrentFarNodeLocation;

        public static IReadOnlyList<InventoryType> InventoryTypes { get; } =
        [
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
        ];

        public GatheringType JobAsGatheringType
        {
            get
            {
                var job = Player.Job;
                switch (job)
                {
                    case Job.MIN: return GatheringType.Miner;
                    case Job.BTN: return GatheringType.Botanist;
                    case Job.FSH: return GatheringType.Fisher;
                    default:      return GatheringType.Unknown;
                }
            }
        }

        public bool ShouldUseFlag
            => Scrounger.Config.FlagPathing;

        public bool ShouldFly(Vector3 destination)
        {
            if (Svc.Condition[ConditionFlag.InFlight] || Svc.Condition[ConditionFlag.Diving])
                return true;

            if (!Scrounger.Config.UseFlying || Svc.ClientState.LocalPlayer == null)
            {
                return false;
            }

            return Vector3.Distance(Svc.ClientState.LocalPlayer.Position, destination)
                >= Scrounger.Config.MountUpDistance;
        }

        public unsafe Vector2? TimedNodePosition
        {
            get
            {
                var map = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
                var markers = map->MiniMapGatheringMarkers;
                if (markers == null)
                    return null;
                Vector2? result = null;
                foreach (var miniMapGatheringMarker in markers)
                {
                    if (miniMapGatheringMarker.MapMarker.X != 0 && miniMapGatheringMarker.MapMarker.Y != 0)
                    {
                        // ReSharper disable twice PossibleLossOfFraction
                        result = new Vector2(miniMapGatheringMarker.MapMarker.X / 16, miniMapGatheringMarker.MapMarker.Y / 16);
                        break;
                    }
                    // GatherBuddy.Log.Information(miniMapGatheringMarker.MapMarker.IconId +  " => X: " + miniMapGatheringMarker.MapMarker.X / 16 + " Y: " + miniMapGatheringMarker.MapMarker.Y / 16);
                }

                return result;
            }
        }

        public string AutoStatus { get; private set; } = "Idle";
        public int LastCollectability = 0;
        public int LastIntegrity = 0;
        private BitVector32 LuckUsed;
        private bool WentHome;

        internal IEnumerable<GatherTarget> ItemsToGather => _activeItemList;
        internal ReadOnlyDictionary<GatheringNode, TimeInterval> DebugVisitedTimedLocations => _activeItemList.DebugVisitedTimedLocations;
        public readonly HashSet<Vector3> FarNodesSeenSoFar = [];
        public readonly LinkedList<uint> VisitedNodes = [];

        private IEnumerator<Actions.BaseAction?>? ActionSequence;

        private static unsafe T* GetAddon<T>(string name) where T : unmanaged
        {
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName(name);
            if (addon != null && addon->IsFullyLoaded() && addon->IsReady)
                return (T*)addon;
            else
                return null;
        }
        public static unsafe AddonGathering* GatheringAddon
            => GetAddon<AddonGathering>("Gathering");

        public static unsafe AddonGatheringMasterpiece* MasterpieceAddon
            => GetAddon<AddonGatheringMasterpiece>("GatheringMasterpiece");

        public static unsafe AddonMaterializeDialog* MaterializeAddon
            => GetAddon<AddonMaterializeDialog>("Materialize");

        public static unsafe AddonMaterializeDialog* MaterializeDialogAddon
            => GetAddon<AddonMaterializeDialog>("MaterializeDialog");

        public static unsafe AddonSelectYesno* SelectYesnoAddon
            => GetAddon<AddonSelectYesno>("SelectYesno");

        public static unsafe AtkUnitBase* PurifyItemSelectorAddon
            => GetAddon<AtkUnitBase>("PurifyItemSelector");

        public static unsafe AtkUnitBase* PurifyResultAddon
            => GetAddon<AtkUnitBase>("PurifyResult");

        public static unsafe AddonRepair* RepairAddon
            => GetAddon<AddonRepair>("Repair");

        public IEnumerable<IGatherable> ItemsToGatherInZone
            => _activeItemList.Where(i => i.Node?.Territory.Id == Svc.ClientState.TerritoryType).Select(i => i.Item);

        private bool LocationMatchesJob(ILocation loc)
            => loc.GatheringType.ToGroup() == JobAsGatheringType;

        public bool CanAct
        {
            get
            {
                if (Svc.ClientState.LocalPlayer == null)
                    return false;
                if (Svc.Condition[ConditionFlag.BetweenAreas]
                 || Svc.Condition[ConditionFlag.BetweenAreas51]
                 || Svc.Condition[ConditionFlag.OccupiedInQuestEvent]
                 || Svc.Condition[ConditionFlag.OccupiedSummoningBell]
                 || Svc.Condition[ConditionFlag.BeingMoved]
                 || Svc.Condition[ConditionFlag.Casting]
                 || Svc.Condition[ConditionFlag.Casting87]
                 || Svc.Condition[ConditionFlag.Jumping]
                 || Svc.Condition[ConditionFlag.Jumping61]
                 || Svc.Condition[ConditionFlag.LoggingOut]
                 || Svc.Condition[ConditionFlag.Occupied]
                 || Svc.Condition[ConditionFlag.Occupied39]
                 || Svc.Condition[ConditionFlag.Unconscious]
                 || Svc.Condition[ConditionFlag.Gathering42]
                 || Svc.Condition[ConditionFlag.Unknown57] // Mounting up
                 //Node is open? Fades off shortly after closing the node, can't use items (but can mount) while it's set
                 || Svc.Condition[85] && !Svc.Condition[ConditionFlag.Gathering]
                 || Svc.ClientState.LocalPlayer.IsDead
                 || Player.IsAnimationLocked)
                    return false;

                return true;
            }
        }

        private static unsafe bool HasGivingLandBuff
            => Svc.ClientState.LocalPlayer?.StatusList.Any(s => s.StatusId == 1802) ?? false;

        public static unsafe bool IsGivingLandOffCooldown
            => ActionManager.Instance()->IsActionOffCooldown(ActionType.Action, Actions.GivingLand.ActionId);

        //Should be near the upper bound to reduce the probability of overcapping.
        private const int GivingLandYield = 30;

        private static unsafe uint FreeInventorySlots
            => InventoryManager.Instance()->GetEmptySlotsInBag();

        public static TimeStamp AdjustedServerTime
            => Scrounger.Time.ServerTime.AddSeconds(Scrounger.Config.TimedNodePrecog);

        private ConfigPreset GetConfigPreset(Gatherable node)
        {
            return GetConfigPreset(Guid.Empty);
        }
        private ConfigPreset GetConfigPreset(Guid id)
        {
            return _plugin.GetPreset(id);
        }
    }
}
