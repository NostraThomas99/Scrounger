using System.Numerics;
using ECommons.DalamudServices;
using GatherBuddy.Classes;
using GatherBuddy.Enums;
using Newtonsoft.Json;
using Scrounger.Data;

namespace Scrounger;

public class Config
{
    public static Config Load()
    {
        try
        {
            var file = Svc.PluginInterface.ConfigFile.FullName;
            if (!File.Exists(file))
                return new Config();
            
            var json = File.ReadAllText(file);
            var config = JsonConvert.DeserializeObject<Config>(json);
            if (config == null)
                return new Config();
            
            return config;
        }
        catch
        {
            return new Config();
        }
    }

    public void Save()
    {
        var file = Svc.PluginInterface.ConfigFile.FullName;
        File.WriteAllText(file, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public bool UseGivingLandOnCooldown { get; set; } = false;
    public bool AbandonNodes { get; set; } = false;
    public bool UseSkillsForFallbackItems { get; set; } = false;
    public bool DoReduce { get; set; } = true;
    public uint AutoGatherMountId { get; set; } = 0;
    public int MountUpDistance { get; set; } = 25;
    public bool DoRepair { get; set; } = true;
    public int RepairThreshold { get; set; } = 50;
    public bool DoMaterialize { get; set; } = true;
    public int ExecutionDelay { get; set; } = 1000;
    public Dictionary<uint, List<Vector3>> BlacklistedNodesByTerritoryId { get; set; } = [];
    public bool DisableFlagPathing { get; set; } = false;
    public bool ForceWalking { get; set; } = false;
    public int TimedNodePrecog { get; set; } = 0;
    public bool DoGathering { get; set; } = true;
    public bool GoHomeWhenIdle { get; set; } = true;
    public bool HonkMode { get; set; } = true;
    public bool GoHomeWhenDone { get; set; } = true;
    public string MinerSetName { get; set; } = "Miner";
    public string BotanistSetName { get; set; } = "Botanist";
    public double NavResetThreshold { get; set; } = 3;
    public GatheringType PreferredGatheringType { get; set; } = GatheringType.Miner;
    public ConfigTypes.AetherytePreference AetherytePreference { get; set; } = ConfigTypes.AetherytePreference.Cost;
    public ConfigTypes.SortingType SortingMethod { get; set; } = ConfigTypes.SortingType.Location;
    public double NavResetCooldown { get; set; } = 2;
    public bool DrawDebugTab { get; set; } = false;
}