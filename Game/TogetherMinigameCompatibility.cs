using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace CasualtiesSurgery.Game
{
    // Surgery owns its network session. Keep native Start/EndMinigame and Together's
    // start-prefix input reset, but do not send surgery through Together's native IDs.
    internal static class TogetherMinigameCompatibility
    {
        const BindingFlags Static=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
        internal static Action<string> Log;
        static void Report(string message){try{Log?.Invoke(message);}catch{/* Diagnostics must never change native startup. */}}
        public static void ObserveStartup(Harmony harmony,Assembly together)
        {
            var type=together.GetType("Together.MinigameBase_StartMinigame_MultiplayerPatch");
            var method=type?.GetMethod("Prefix",Static);
            if(method==null || method.ReturnType!=typeof(bool) ||
                !method.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(MinigameBase),typeof(Minigame),typeof(Item),typeof(bool).MakeByRefType()}))
                throw new NotSupportedException("Together startup observation signature mismatch.");
            var observer=typeof(TogetherMinigameCompatibility).GetMethod(nameof(ObserveDecision),Static);
            if(Harmony.GetPatchInfo(method)?.Postfixes.Any(p=>p.PatchMethod==observer)!=true)
                harmony.Patch(method,prefix:new HarmonyMethod(typeof(TogetherMinigameCompatibility).GetMethod(nameof(ObserveBefore),Static)),postfix:new HarmonyMethod(observer));
        }
        static void ObserveBefore(object __0,object __1,MethodBase __originalMethod,out string __state)
        {
            __state=null;if(!(__1 is ISurgery))return;
            try {__state="occupied="+((__0 as MinigameBase)?.currentMinigame!=null)+", ignoreNext="+
                (__originalMethod.DeclaringType.GetField("ignore_next_mgstart_cuz_its_not_me",Static)?.GetValue(null)?.ToString()??"unknown");}
            catch{__state="state unavailable";}
        }
        static void ObserveDecision(object __1,bool __result,string __state)
        {if(__1 is ISurgery)Report("Surgery Together startup decision: allowed="+__result+"; before "+__state);}
        public static void Install(Harmony harmony,Assembly together)
        {
            var start=together.GetType("Together.MinigameBase_StartMinigame_MultiplayerPatch")?.GetMethod("Postfix",Static);
            var end=together.GetType("Together.MinigameBase_EndMinigame_MultiplayerPatch")?.GetMethod("Prefix",Static);
            var target=together.GetType("Together.MinigameMPManager")?.GetMethod("GetMinigameTargetObjectNetId",Static);
            if(target==null || !target.ReturnType.IsValueType || target.ReturnType.FullName!="Together.AnyObjectNetId" ||
                !target.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(Minigame)}))
                throw new NotSupportedException("Together minigame target signature changed; surgery transport was not enabled.");
            if(start==null || end==null || start.ReturnType!=typeof(void) || end.ReturnType!=typeof(bool) ||
                !start.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(MinigameBase),typeof(Minigame),typeof(Item),typeof(bool).MakeByRefType()}) ||
                !end.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(MinigameBase)}))
                throw new NotSupportedException("Together minigame lifecycle signatures changed; surgery transport was not enabled.");
            var startGuard=typeof(TogetherMinigameCompatibility).GetMethod(nameof(StartNotification),Static);
            var endGuard=typeof(TogetherMinigameCompatibility).GetMethod(nameof(EndNotification),Static);
            var targetGuard=typeof(TogetherMinigameCompatibility).GetMethod(nameof(NativeTarget),Static).MakeGenericMethod(target.ReturnType);
            bool addedStart=false,addedEnd=false,addedTarget=false;
            try
            {
                if(!Installed(start,startGuard)) {harmony.Patch(start,prefix:new HarmonyMethod(startGuard));addedStart=true;}
                if(!Installed(end,endGuard)){harmony.Patch(end,prefix:new HarmonyMethod(endGuard));addedEnd=true;}
                if(!Installed(target,targetGuard)){harmony.Patch(target,prefix:new HarmonyMethod(targetGuard));addedTarget=true;}
            }
            catch {if(addedTarget)harmony.Unpatch(target,targetGuard);if(addedEnd)harmony.Unpatch(end,endGuard);if(addedStart)harmony.Unpatch(start,startGuard);throw;}
        }
        // Custom surgery has its own patient identity/protocol, never a vanilla wound-item target.
        // The empty native target leaves Together's occupied/ignore-next/reset logic intact.
        static bool NativeTarget<T>(object __0,ref T __result) where T:struct
        {if(!(__0 is ISurgery))return true;__result=default(T);return false;}
        static bool Installed(MethodBase method,MethodInfo guard)=>Harmony.GetPatchInfo(method)?.Prefixes.Any(p=>p.PatchMethod==guard)==true;
        // Positional arguments belong to Together's patch method, not the game method.
        static bool StartNotification(object __1)=>!(__1 is ISurgery);
        static bool EndNotification(object __0,ref bool __result)
        {
            if(!((__0 as MinigameBase)?.currentMinigame is ISurgery))return true;
            __result=true; // Together's prefix must still allow the original game cleanup.
            return false;
        }
    }
}
