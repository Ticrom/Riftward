using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftward.Patches;

/// <summary>
/// Prefix patch on BlockEntityTeleporterBase.OnEntityCollide
/// Prevents players from using translocators whose target is outside the border.
/// </summary>
[HarmonyPatch(typeof(BlockEntityTeleporterBase), "OnEntityCollide")]
internal class TranslocatorUsePatch
{
    [HarmonyPrefix]
    internal static bool ShouldTeleport(Entity entity, object __instance)
    {
        if (__instance is not BlockEntityStaticTranslocator translocator)
            return true;

        BlockPos target = translocator.TargetLocation;
        if (target == null || translocator.Api.Side != EnumAppSide.Server)
            return true;

        var system = translocator.Api.ModLoader.GetModSystem<RiftwardSystem>(true);
        if (system == null || !system.IsReady)
            return true;

        if (!system.IsOutsideBorder(target.X, target.Z))
            return true;

        // Target is outside border - block the teleport
        if (entity is EntityPlayer ep && ep.Player is IServerPlayer sp)
        {
            ((ICoreServerAPI)translocator.Api).SendIngameError(sp,
                "translocator-blocked",
                Lang.Get("riftward:translocator-blocked"));
        }

        return false;
    }
}
