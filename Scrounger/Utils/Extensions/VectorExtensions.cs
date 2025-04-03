using System.Numerics;
using ECommons.GameHelpers;

namespace Scrounger.Utils.Extensions;

public static class VectorExtensions
{
    public static float DistanceToPlayer(this Vector3 vector)
    {
        var distance = Vector3.Distance(vector, Player.Object.Position);
        return distance;
    }
}