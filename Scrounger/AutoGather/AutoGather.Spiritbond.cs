using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Scrounger.Ipc;
using Scrounger.Utils;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

//Credit: https://github.com/FFXIV-CombatReborn/GatherBuddyReborn/blob/main/GatherBuddy/AutoGather/AutoGather.Spiritbond.cs
namespace Scrounger.AutoGather;

public partial class AutoGather
{

    unsafe int SpiritbondMax
    {
        get
        {
            if (!Scrounger.Config.DoMaterialize) return 0;

            var inventory = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            var result    = 0;
            for (var slot = 0; slot < inventory->Size; slot++)
            {
                var inventoryItem = inventory->GetInventorySlot(slot);
                if (inventoryItem == null || inventoryItem->ItemId <= 0)
                    continue;

                //Scrounger.Log.Debug("Slot " + slot + " has " + inventoryItem->Spiritbond + " Spiritbond");
                if (inventoryItem->SpiritbondOrCollectability == 10000)
                {
                    result++;
                }
            }

            return result;
        }
    }

    unsafe void DoMateriaExtraction()
    {
        if (!QuestManager.IsQuestComplete(66174))
        {
            Scrounger.Config.DoMaterialize = false;
            ChatPrinter.PrintError("[Scrounger] Materia Extraction enabled but relevant quest not complete yet. Feature disabled.");
            return;
        }
        if (MaterializeAddon == null)
        {
            TaskManager.Enqueue(StopNavigation);
            EnqueueActionWithDelay(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 14));
            TaskManager.Enqueue(() => MaterializeAddon != null);
            return;
        }

        TaskManager.Enqueue(YesAlready.Lock);
        EnqueueActionWithDelay(() => { if (MaterializeAddon is var addon and not null) Callback.Fire(&addon->AtkUnitBase, true, 2, 0); });
        TaskManager.Enqueue(() => MaterializeDialogAddon != null, 1000);
        EnqueueActionWithDelay(() => { if (MaterializeDialogAddon is var addon and not null) new MaterializeDialog(addon).Materialize(); });
        TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.Occupied39]);

        if (SpiritbondMax == 1) 
        {
            EnqueueActionWithDelay(() => { if (MaterializeAddon is var addon and not null) Callback.Fire(&addon->AtkUnitBase, true, -1); });
            TaskManager.Enqueue(YesAlready.Unlock);
        }
    }
}
