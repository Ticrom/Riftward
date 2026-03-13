using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftward.Patches;

/// <summary>
/// Transpiler patch on BlockEntityStaticTranslocator.OnServerGameTick
/// Replaces the vanilla IsValidPos check with our border-aware check,
/// preventing translocators from generating exit positions outside the border.
/// </summary>
[HarmonyPatch(typeof(BlockEntityStaticTranslocator), "OnServerGameTick")]
internal class TranslocatorExitPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Patch(IEnumerable<CodeInstruction> instructions)
    {
        var list = instructions.ToList();

        int isValidPosIdx = list.FindIndex(ins =>
            ins.opcode == OpCodes.Callvirt &&
            (ins.operand as MethodInfo)?.Name == "IsValidPos");

        if (isValidPosIdx < 0)
            return list;

        string worldGetterName = AccessTools.PropertyGetter(typeof(ICoreServerAPI), "World").Name;

        // Walk backwards to find the World property access
        int startIdx = isValidPosIdx;
        do { startIdx--; }
        while (startIdx > 0 &&
               !(list[startIdx].opcode == OpCodes.Callvirt &&
                 (list[startIdx].operand as MethodInfo)?.Name == worldGetterName));

        if (list[startIdx].opcode == OpCodes.Callvirt)
        {
            // Remove World.BlockAccessor access (2 instructions)
            list.RemoveRange(startIdx, 2);
            // Remove the Ldc_I4_1 (dimension arg)
            int ldc = list.FindIndex(startIdx, ins => ins.opcode == OpCodes.Ldc_I4_1);
            if (ldc >= 0) list.RemoveAt(ldc);

            isValidPosIdx -= 3;
            list[isValidPosIdx] = new CodeInstruction(OpCodes.Call,
                typeof(TranslocatorExitPatch).GetMethod(nameof(IsValidExit), BindingFlags.Static | BindingFlags.Public));
        }

        return list;
    }

    public static bool IsValidExit(ICoreServerAPI api, int x, int z)
    {
        var system = api.ModLoader.GetModSystem<RiftwardSystem>(true);
        if (system != null && system.IsReady)
            return system.IsValidTranslocatorExit(x, z);
        return false;
    }
}
