﻿using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GatherBuddy.Classes;
using System.Linq;
using System.Runtime.InteropServices;
using ECommons.Automation.UIInput;
using ItemSlot = Scrounger.AutoGather.GatheringTracker.ItemSlot;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using Scrounger.AutoGather.Lists;
using Scrounger.Utils.Extensions;

//Credit: https://github.com/FFXIV-CombatReborn/GatherBuddyReborn/blob/main/GatherBuddy/AutoGather/AutoGather.Gather.cs
namespace Scrounger.AutoGather
{
    public partial class AutoGather
    {
        private unsafe void EnqueueNodeInteraction(IGameObject gameObject, Gatherable targetItem)
        {
            var targetSystem = TargetSystem.Instance();
            if (targetSystem == null)
                return;

            TaskManager.Enqueue(() => targetSystem->OpenObjectInteraction((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address));
            TaskManager.Enqueue(() => Svc.Condition[ConditionFlag.Gathering], 500);
        }

        private unsafe void EnqueueGatherItem(ItemSlot slot, Guid presetId)
        {
            var gatheringAddon = GatheringAddon;
            if (gatheringAddon == null)
                return;

            if (slot.Item.ItemData.IsCollectable)
            {
                // Since it's possible that we are not gathering the top item in the list,
                // we need to remember what we are going to gather inside MasterpieceAddon
                CurrentCollectableRotation = new CollectableRotation(GetConfigPreset(presetId), slot.Item, _activeItemList.FirstOrDefault(x => x.Item == slot.Item).Quantity);
            }

            var itemIndex           = slot.Index;
            var receiveEventAddress = new nint(gatheringAddon->AtkUnitBase.AtkEventListener.VirtualTable->ReceiveEvent);
            var eventDelegate       = Marshal.GetDelegateForFunctionPointer<ClickHelper.ReceiveEventDelegate>(receiveEventAddress);

            var target    = AtkStage.Instance();
            var eventData = EventData.ForNormalTarget(target, &gatheringAddon->AtkUnitBase);
            var inputData = InputData.Empty();

            EnqueueActionWithDelay(() => eventDelegate.Invoke(&gatheringAddon->AtkUnitBase.AtkEventListener, EventType.CHANGE, (uint)itemIndex, eventData.Data, inputData.Data));

            if (slot.Item.IsTreasureMap())
            {
                TaskManager.Enqueue(() => Svc.Condition[ConditionFlag.Gathering42], 1000);
                TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.Gathering42]);
                TaskManager.Enqueue(DiscipleOfLand.RefreshNextTreasureMapAllowance);
            }
        }

        /// <summary>
        /// Checks if desired item could or should be gathered and may change it to something more suitable
        /// </summary>
        /// <returns>UseSkills: True if the selected item is in the gathering list; false if we gather a collectable or some unneeded junk
        /// Slot: ItemSlot of item to gather</returns>
        private (bool UseSkills, ItemSlot Slot) GetItemSlotToGather(GatherTarget gatherTarget)
        {
            var available = NodeTracker.Available
                .Where(CheckItemOvercap)
                .ToList();

            var target = gatherTarget.Item != null ? available.Where(s => s.Item == gatherTarget.Item).FirstOrDefault() : null;

            //Gather crystals when using The Giving Land
            if (HasGivingLandBuff && (target == null || !target.Item.IsCrystal()))
            {
                var crystal = GetAnyCrystalInNode();
                if (crystal != null)
                    return (true, crystal);
            }

            if (target != null && gatherTarget.Item!.GetInventoryCount() < gatherTarget.Quantity)
            {
                //The target item is found in the node, would not overcap and we need to gather more of it
                return (!target.Collectable, target);
            }

            //Items in the gathering list
            var gatherList = ItemsToGather
                //Join node slots, retaining list order
                .Join(available, i => i.Item, s => s.Item, (i, s) => (Slot: s, i.Quantity))
                //And we need more of them
                .Where(x => x.Slot.Item.GetInventoryCount() < x.Quantity)
                .Select(x => x.Slot);

            //Items in the fallback list
            var fallbackList = _plugin.AutoGatherListsManager.FallbackItems
                //Join node slots, retaining list order
                .Join(available, i => i.Item, s => s.Item, (i, s) => (Slot: s, i.Quantity))
                //And we need more of them
                .Where(x => x.Slot.Item.GetInventoryCount() < x.Quantity)
                .Select(x => x.Slot);

            var fallbackSkills = Scrounger.Config.UseSkillsForFallbackItems;

            //If there is any other item that we want in the node, gather it
            var slot = gatherList.FirstOrDefault();
            if (slot != null)
            {
                return (!slot.Collectable, slot);
            }

            //If there is any fallback item, gather it
            slot = fallbackList.FirstOrDefault();
            if (slot != null)
            {
                return (fallbackSkills && !slot.Collectable, slot);
            }

            //Check if we should and can abandon the node
            if (Scrounger.Config.AbandonNodes)
                throw new NoGatherableItemsInNodeException();

            if (target != null)
            {
                //Gather unneeded target item as a fallback
                return (false, target);
            }

            //Gather any crystals
            slot = GetAnyCrystalInNode();
            if (slot != null)
            {
                return (false, slot);
            }
            //If there are no crystals, gather anything which is not treasure map nor collectable
            slot = available.FirstOrDefault(s => !s.Item.IsTreasureMap() && !s.Collectable);
            if (slot != null)
            {
                return (false, slot);
            }
            //Abort if there are no items we can gather
            throw new NoGatherableItemsInNodeException();
        }

        private bool CheckItemOvercap(ItemSlot s)
        {
            //If it's a treasure map, we can have only one in the inventory
            if (s.Item.IsTreasureMap() && s.Item.GetInventoryCount() != 0)
                return false;
            //If it's a crystal, we can't have more than 9999
            if (s.Item.IsCrystal() && s.Item.GetInventoryCount() > 9999 - s.Yield)
                return false;
            return true;
        }
        
        private ItemSlot? GetAnyCrystalInNode()
        {
            return NodeTracker.Available
                .Where(s => s.Item.IsCrystal())
                .Where(CheckItemOvercap)
                //Prioritize crystals in the gathering list
                .GroupJoin(_activeItemList.Where(i => i.Item.IsCrystal()), s => s.Item, i => i.Item, (s, x) => (Slot: s, Order: x.Any()?1:0))
                .OrderBy(x => x.Order)
                //Prioritize crystals with a lower amount in the inventory
                .ThenBy(x => x.Slot.Item.GetInventoryCount())
                .Select(x => x.Slot)
                .FirstOrDefault();
        }
    }
}
