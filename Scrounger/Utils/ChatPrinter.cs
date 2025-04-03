using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;

namespace Scrounger.Utils;

public static class ChatPrinter
{
    public static string Prefix => "[Scrounger] ";

    public static void PrintError(string message)
    {
        Svc.Chat.PrintError(Prefix + message);
    }

    public static void Print(string message)
    {
        Svc.Chat.Print(Prefix + message);
    }

    public static void Print(SeString message)
    {
        Svc.Chat.Print(Prefix + message);
    }
}