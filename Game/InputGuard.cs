using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesSurgery.Game
{
    [HarmonyPatch]
    internal static class InputGuard
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            // Verified on the installed full-game assembly. Fail at startup if a hook moves.
            foreach (string name in new[] { "HandleInput", "HandleMouseOver", "HandleDragging", "HandleRadialMenu" })
                yield return AccessTools.Method(typeof(PlayerCamera), name)
                    ?? throw new MissingMethodException(typeof(PlayerCamera).FullName, name);
        }
        static bool Prefix(PlayerCamera __instance)
        {
            if (!Plugin.BlockInput || __instance != PlayerCamera.main) return true;
            if (__instance.body != null) __instance.body.moveDir = Vector2.zero;
            __instance.wantsJumpInput = false;
            return false;
        }
    }
}
