using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.Server;
using Vintagestory.Server.Systems;

namespace Riftward.Patches;

/// <summary>
/// Patches ServerUdpNetwork.HandlePlayerPosition.
/// After the client position packet is applied, checks if the player
/// is outside the border and teleports them back to the edge.
/// </summary>
[HarmonyPatch(typeof(ServerUdpNetwork), "HandlePlayerPosition")]
internal class PlayerPositionPatch
{
    [HarmonyPostfix]
    internal static void Postfix(ServerPlayer player)
    {
        EntityPlayer entity = player.Entity;
        if (entity == null)
            return;

        var system = ((ICoreAPI)entity.Api).ModLoader.GetModSystem<RiftwardSystem>(true);
        if (system == null || !system.IsReady)
            return;

        double x = entity.ServerPos.X;
        double z = entity.ServerPos.Z;

        if (system.ClampToBorder(ref x, ref z))
        {
            entity.TeleportToDouble(x, entity.ServerPos.Y, z);
        }
    }
}
