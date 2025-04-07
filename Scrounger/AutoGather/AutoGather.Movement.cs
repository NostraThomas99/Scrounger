﻿using Dalamud.Game.ClientState.Conditions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.Classes;
using GatherBuddy.Interfaces;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GatherBuddy.Data;
using ECommons.MathHelpers;
using Scrounger.Ipc;
using Scrounger.Utils;

//Credit: https://github.com/FFXIV-CombatReborn/GatherBuddyReborn/blob/main/GatherBuddy/AutoGather/AutoGather.Movement.cs
namespace Scrounger.AutoGather
{
    public partial class AutoGather
    {
        private unsafe void EnqueueDismount()
        {
            TaskManager.Enqueue(StopNavigation);

            var am = ActionManager.Instance();
            TaskManager.Enqueue(() => { if (Svc.Condition[ConditionFlag.Mounted]) am->UseAction(ActionType.Mount, 0); }, "Dismount");

            TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.InFlight] && CanAct, 1000, "Wait for not in flight");
            TaskManager.Enqueue(() => { if (Svc.Condition[ConditionFlag.Mounted]) am->UseAction(ActionType.Mount, 0); }, "Dismount 2");
            TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.Mounted] && CanAct, 1000, "Wait for dismount");
            TaskManager.Enqueue(() => { if (!Svc.Condition[ConditionFlag.Mounted]) TaskManager.DelayNextImmediate(500); } );//Prevent "Unable to execute command while jumping."
        }

        private unsafe void EnqueueMountUp()
        {
            var am = ActionManager.Instance();
            var mount = Scrounger.Config.AutoGatherMountId;
            Action doMount;

            if (IsMountUnlocked(mount) && am->GetActionStatus(ActionType.Mount, mount) == 0)
            {
                doMount = () => am->UseAction(ActionType.Mount, mount);
            }
            else
            {
                if (am->GetActionStatus(ActionType.GeneralAction, 9) != 0)
                {
                    return;
                }

                doMount = () => am->UseAction(ActionType.GeneralAction, 9);
            }

            TaskManager.Enqueue(StopNavigation);
            EnqueueActionWithDelay(doMount);
            TaskManager.Enqueue(() => Svc.Condition[ConditionFlag.Mounted], 2000);
        }

        private unsafe bool IsMountUnlocked(uint mount)
        {
            var instance = PlayerState.Instance();
            if (instance == null)
                return false;

            return instance->IsMountUnlocked(mount);
        }

        private void MoveToCloseNode(IGameObject gameObject, Gatherable targetItem, ConfigPreset config)
        {
            // We can open a node with less than 3 vertical and less than 3.5 horizontal separation
            var hSeparation = Vector2.Distance(gameObject.Position.ToVector2(), Player.Position.ToVector2());
            var vSeparation = Math.Abs(gameObject.Position.Y - Player.Position.Y);

            if (hSeparation < 3.5)
            {
                var waitGP = targetItem.ItemData.IsCollectable && Player.Object.CurrentGp < config.CollectableMinGP;
                waitGP |= !targetItem.ItemData.IsCollectable && Player.Object.CurrentGp < config.GatherableMinGP;

                if (Svc.Condition[ConditionFlag.Mounted] && (waitGP || Svc.Condition[ConditionFlag.InFlight] || GetConsumablesWithCastTime(config) > 0))
                {
                    //Try to dismount early. It would help with nodes where it is not possible to dismount at vnavmesh's provided floor point
                    EnqueueDismount();
                    TaskManager.Enqueue(() => {
                        //If early dismount failed, navigate to the nearest floor point
                        if (Svc.Condition[ConditionFlag.Mounted] && Svc.Condition[ConditionFlag.InFlight] && !Svc.Condition[ConditionFlag.Diving])
                        {
                            try
                            {
                                var floor = VNavmesh.Query.Mesh.PointOnFloor(Player.Position, false, 3);
                                Navigate(floor, true);
                                TaskManager.Enqueue(() => !IsPathGenerating);
                                TaskManager.DelayNext(50);
                                TaskManager.Enqueue(() => !IsPathing, 1000);
                                EnqueueDismount();
                            }
                            catch { }
                            //If even that fails, do advanced unstuck
                            TaskManager.Enqueue(() => { if (Svc.Condition[ConditionFlag.Mounted]) _advancedUnstuck.Force(); });
                        }
                    });
                }
                else if (waitGP)
                {
                    StopNavigation();
                    AutoStatus = "Waiting for GP to regenerate...";
                }
                else
                {
                    // Use consumables with cast time just before gathering a node when player is surely not mounted
                    if (GetConsumablesWithCastTime(config) is var consumable and > 0)
                    {
                        if (IsPathing)
                            StopNavigation();
                        else
                            EnqueueActionWithDelay(() => UseItem(consumable));
                    }
                    else
                    {
                        // Check perception requirement before interacting with node
                        if (DiscipleOfLand.Perception < targetItem.GatheringData.PerceptionReq)
                        {
                            ChatPrinter.PrintError($"Insufficient Perception to gather this item. Required: {targetItem.GatheringData.PerceptionReq}, current: {DiscipleOfLand.Perception}");
                            AbortAutoGather();
                            return;
                        }

                        if (vSeparation < 3)                        
                            EnqueueNodeInteraction(gameObject, targetItem);

                        // The node could be behind a rock or a tree and not be interactable. This happened in the Endwalker, but seems not to be reproducible in the Dawntrail.
                        // Enqueue navigation anyway, just in case.
                        // Also move if vertical separation is too large.
                        if (!Svc.Condition[ConditionFlag.Diving])
                        {
                            TaskManager.Enqueue(() => { if (!Svc.Condition[ConditionFlag.Gathering]) Navigate(gameObject.Position, false); });
                        }
                    }
                }
            }
            else if (hSeparation < Math.Max(Scrounger.Config.MountUpDistance, 5) && !Svc.Condition[ConditionFlag.Diving])
            {
                Navigate(gameObject.Position, false);
            }
            else
            {
                if (!Svc.Condition[ConditionFlag.Mounted])
                {
                    EnqueueMountUp();
                }
                else
                {
                    Navigate(gameObject.Position, ShouldFly(gameObject.Position));
                }
            }
        }

        private void StopNavigation()
        {
            // Reset navigation logic here
            // For example, reinitiate navigation to the destination
            CurrentDestination = default;
            if (VNavmesh.Enabled)
            {
                VNavmesh.Path.Stop();
            }
        }

        private void Navigate(Vector3 destination, bool shouldFly)
        {
            if (CurrentDestination == destination && (IsPathing || IsPathGenerating))
                return;

            //vnavmesh can't find a path on mesh underwater (because the character is basically flying), nor can it fly without a mount.
            //Ensure that you are always mounted when underwater.
            if (Svc.Condition[ConditionFlag.Diving] && !Svc.Condition[ConditionFlag.Mounted])
            {
                Scrounger.Log.Error("BUG: Navigate() called underwater without mounting up first");
                Enabled = false;
                return;
            }

            shouldFly |= Svc.Condition[ConditionFlag.Diving];

            StopNavigation();
            CurrentDestination = destination;
            var correctedDestination = GetCorrectedDestination(CurrentDestination);
            Scrounger.Log.Debug($"Navigating to {destination} (corrected to {correctedDestination})");

            LastNavigationResult = VNavmesh.SimpleMove.PathfindAndMoveTo(correctedDestination, shouldFly);
        }

        private static Vector3 GetCorrectedDestination(Vector3 destination)
        {
            const float MaxHorizontalSeparation = 3.0f;
            const float MaxVerticalSeparation = 2.5f;

            try
            {
                float separation;
                if (Scrounger.WorldData.NodeOffsets.TryGetValue(destination, out var offset))
                {
                    offset = VNavmesh.Query.Mesh.NearestPoint(offset, MaxHorizontalSeparation, MaxVerticalSeparation);
                    if ((separation = Vector2.Distance(offset.ToVector2(), destination.ToVector2())) > MaxHorizontalSeparation)
                        Scrounger.Log.Warning($"Offset is ignored because the horizontal separation {separation} is too large after correcting for mesh. Maximum allowed is {MaxHorizontalSeparation}.");
                    else if ((separation = Math.Abs(offset.Y - destination.Y)) > MaxVerticalSeparation)
                        Scrounger.Log.Warning($"Offset is ignored because the vertical separation {separation} is too large after correcting for mesh. Maximum allowed is {MaxVerticalSeparation}.");
                    else
                        return offset;
                }

                var correctedDestination = VNavmesh.Query.Mesh.NearestPoint(destination, MaxHorizontalSeparation, MaxVerticalSeparation);
                if ((separation = Vector2.Distance(correctedDestination.ToVector2(), destination.ToVector2())) > MaxHorizontalSeparation)
                    Scrounger.Log.Warning($"Query.Mesh.NearestPoint() returned a point with too large horizontal separation {separation}. Maximum allowed is {MaxHorizontalSeparation}.");
                else if ((separation = Math.Abs(correctedDestination.Y - destination.Y)) > MaxVerticalSeparation)
                    Scrounger.Log.Warning($"Query.Mesh.NearestPoint() returned a point with too large vertical separation {separation}. Maximum allowed is {MaxVerticalSeparation}.");
                else
                    return correctedDestination;
            }
            catch (Exception) { }

            return destination;
        }

        private void MoveToFarNode(Vector3 position)
        {
            var farNode = position;

            if (!Svc.Condition[ConditionFlag.Mounted])
            {
                EnqueueMountUp();
            }
            else
            {
                Navigate(farNode, ShouldFly(farNode));
            }
        }

        public static Aetheryte? FindClosestAetheryte(ILocation location)
        {
            var aetheryte = location.ClosestAetheryte;

            var territory = location.Territory;
            if (ForcedAetherytes.ZonesWithoutAetherytes.FirstOrDefault(x => x.ZoneId == territory.Id).AetheryteId is var alt && alt > 0)
                territory = Scrounger.WorldData.Aetherytes[alt].Territory;

            if (aetheryte == null || !Teleporter.IsAttuned(aetheryte.Id) || aetheryte.Territory != territory)
            {
                aetheryte = territory.Aetherytes
                    .Where(a => Teleporter.IsAttuned(a.Id))
                    .OrderBy(a => a.WorldDistance(territory.Id, location.IntegralXCoord, location.IntegralYCoord))
                    .FirstOrDefault();
            }

            return aetheryte;
        }

        private bool MoveToTerritory(ILocation location)
        {
            var aetheryte = FindClosestAetheryte(location);
            if (aetheryte == null)
            {
                ChatPrinter.PrintError("Couldn't find an attuned aetheryte to teleport to.");
                return false;
            }

            EnqueueActionWithDelay(() => Teleporter.Teleport(aetheryte.Id));
            TaskManager.Enqueue(() => Svc.Condition[ConditionFlag.BetweenAreas]);
            TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.BetweenAreas]);
            TaskManager.DelayNext(1500);

            return true;
        }
    }
}
