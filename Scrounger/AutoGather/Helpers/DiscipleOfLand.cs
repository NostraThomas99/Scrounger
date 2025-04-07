using ECommons.ExcelServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using ECommons.DalamudServices;

//Credit: https://github.com/FFXIV-CombatReborn/GatherBuddyReborn/blob/main/GatherBuddy/AutoGather/Helpers/DiscipleOfLand.cs
namespace Scrounger.AutoGather
{
    public static class DiscipleOfLand
    {
        private static readonly sbyte _minerExpArrayIndex = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>().GetRow((uint)Job.MIN).ExpArrayIndex;
        private static readonly sbyte _botanistExpArrayIndex = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>().GetRow((uint)Job.BTN).ExpArrayIndex;

        public static unsafe int MinerLevel => PlayerState.Instance()->ClassJobLevels[_minerExpArrayIndex];
        public static unsafe int BotanistLevel => PlayerState.Instance()->ClassJobLevels[_botanistExpArrayIndex];
        public static unsafe int Gathering => PlayerState.Instance()->Attributes[72];
        public static unsafe int Perception => PlayerState.Instance()->Attributes[73];
        public static unsafe DateTime NextTreasureMapAllowance => UIState.Instance()->GetNextMapAllowanceDateTime();

        public static unsafe void RefreshNextTreasureMapAllowance()
        {
            if (EzThrottler.Throttle("RequestResetTimestamps", 1000))
            {
                UIState.Instance()->RequestResetTimestamps();
            }
        }

    }
}
