using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Commands;
using ECommons.DalamudServices;
using OtterGui.Log;
using Scrounger.AutoGather;
using Scrounger.AutoGather.Lists;
using Scrounger.UI;
using Scrounger.Utils;

namespace Scrounger;

public class Scrounger :IDalamudPlugin
{
    public string Name => "Scrounger";

    private readonly WindowSystem _windowSystem;

    private readonly MainWindow _mainWindow;

    public static Config Config;
    public static Logger Log = new();
    public static WorldData WorldData = new(Svc.Data, Log);
    public static SeTime Time = new();

    public readonly AutoGatherListsManager AutoGatherListsManager;
    public readonly AutoGather.AutoGather AutoGather;

    public Scrounger(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);

        Config = Config.Load();
        AutoGatherListsManager = AutoGatherListsManager.Load();
        AutoGather = new(this);

        _windowSystem = new WindowSystem(Name);
        _mainWindow = new MainWindow(this);

        _windowSystem.AddWindow(_mainWindow);

        Svc.PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        Svc.Framework.Update += AutoGather.DoAutoGather;
    }

    public ConfigPreset GetPreset(Guid id)
    {
        return _mainWindow.GetPreset(id);
    }

    public void OpenMainUi()
    {
        _mainWindow.Toggle();
    }

    [Cmd("/scrounger", "Open main window")]
    public void OnCommand(string command, string args)
    {
        OpenMainUi();
    }

    public void Dispose()
    {
        ECommonsMain.Dispose();
    }
}