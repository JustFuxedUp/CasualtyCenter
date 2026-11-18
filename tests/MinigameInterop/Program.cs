using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasualtiesSurgery.Game;
using HarmonyLib;

// Minimal lifecycle doubles; actual HarmonyX generates and executes the patched calls.
// No Unity renderer, health state or network socket is simulated by this harness.
public class Item { }
public class Minigame { }
public class MinigameBase
{
    public Minigame currentMinigame;
    public int Starts,Ends;
    public bool ThrowOnStart;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void StartMinigame(Minigame minigame,Item item){if(ThrowOnStart)throw new InvalidOperationException("native failure");currentMinigame=minigame;Starts++;}
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void EndMinigame(){currentMinigame=null;Ends++;}
}
namespace CasualtiesSurgery.Game { public interface ISurgery { } }
sealed class SurgeryView:Minigame,ISurgery { public object limb=new object(); }
sealed class NativeView:Minigame { public object limb; }
namespace Together
{
    public struct AnyObjectNetId { public object Limb; }
    public static class MinigameMPManager
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static AnyObjectNetId GetMinigameTargetObjectNetId(Minigame mg)=>new AnyObjectNetId {Limb=mg.GetType().GetField("limb")?.GetValue(mg)};
    }
    public static class MinigameBase_StartMinigame_MultiplayerPatch
    {
        public static readonly Dictionary<Type,byte> Ids=new Dictionary<Type,byte>{{typeof(NativeView),7}};
        public static int Resets,Notifications;
        public static bool ignore_next_mgstart_cuz_its_not_me;
        public static object applying_on_limb;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Prefix(MinigameBase __instance,Minigame minigame,Item item,ref bool __state)
        {__state=__instance.currentMinigame==null;var target=MinigameMPManager.GetMinigameTargetObjectNetId(minigame);if(!__state)return false;if(ignore_next_mgstart_cuz_its_not_me){ignore_next_mgstart_cuz_its_not_me=false;return false;}if(target.Limb!=null && applying_on_limb==null)return false;Resets++;return true;}
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Postfix(MinigameBase __instance,Minigame minigame,Item item,ref bool __state)
        {if(__instance.currentMinigame==null)return;byte id=Ids[minigame.GetType()];if(__state)Notifications++;}
    }
    public static class MinigameBase_EndMinigame_MultiplayerPatch
    {
        public static int Notifications;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Prefix(MinigameBase __instance){Notifications++;return true;}
    }
}
static class Program
{
    static int checks;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    static void Main()
    {
        var native=new Harmony("test.together");var surgery=new Harmony("test.surgery");
        var start=typeof(MinigameBase).GetMethod("StartMinigame");var end=typeof(MinigameBase).GetMethod("EndMinigame");
        var hooks=typeof(Together.MinigameBase_StartMinigame_MultiplayerPatch);
        native.Patch(start,new HarmonyMethod(hooks.GetMethod("Prefix")),new HarmonyMethod(hooks.GetMethod("Postfix")));
        native.Patch(end,new HarmonyMethod(typeof(Together.MinigameBase_EndMinigame_MultiplayerPatch).GetMethod("Prefix")));
        var host=new MinigameBase();bool reproduced=false;
        host.StartMinigame(new SurgeryView(),null);
        Check(host.currentMinigame==null && host.Starts==0,"Cold start reproduces reflected-limb vanilla prerequisite refusal.");
        Together.MinigameBase_StartMinigame_MultiplayerPatch.applying_on_limb=new object();
        try{host.StartMinigame(new SurgeryView(),null);}catch(KeyNotFoundException){reproduced=true;}
        Check(reproduced,"Pre-fix startup reproduces the missing surgery type ID after native Start executes.");host.EndMinigame();
        Together.MinigameBase_StartMinigame_MultiplayerPatch.applying_on_limb=null;
        TogetherMinigameCompatibility.Install(surgery,typeof(Program).Assembly);
        int starts=host.Starts,ends=host.Ends,resets=Together.MinigameBase_StartMinigame_MultiplayerPatch.Resets;
        int startPackets=Together.MinigameBase_StartMinigame_MultiplayerPatch.Notifications,endPackets=Together.MinigameBase_EndMinigame_MultiplayerPatch.Notifications;
        host.StartMinigame(new SurgeryView(),null);
        Check(host.currentMinigame is SurgeryView && host.Starts==starts+1,"Already-patched native startup succeeds after installing the production guard.");
        Check(Together.MinigameBase_StartMinigame_MultiplayerPatch.applying_on_limb==null,"Cold surgery startup never seeds a fake vanilla wound-item limb.");
        Check(Together.MinigameBase_StartMinigame_MultiplayerPatch.Resets==resets+1,"Together's start-prefix reset still runs.");
        Check(Together.MinigameBase_StartMinigame_MultiplayerPatch.Notifications==startPackets,"Surgery never enters Together's native type lookup/notification.");
        host.EndMinigame();
        Check(host.currentMinigame==null && host.Ends==ends+1,"Native end cleanup still runs for surgery.");
        Check(Together.MinigameBase_EndMinigame_MultiplayerPatch.Notifications==endPackets,"Surgery does not send an unmatched native end notification.");
        var vanillaLimb=new object();var vanilla=new NativeView {limb=vanillaLimb};
        Check(ReferenceEquals(Together.MinigameMPManager.GetMinigameTargetObjectNetId(vanilla).Limb,vanillaLimb),"Native limb-target discovery remains unchanged.");
        host.StartMinigame(vanilla,null);Check(host.currentMinigame==null,"Unprepared native wound-item games retain their prerequisite veto.");
        host.StartMinigame(new NativeView(),null);host.EndMinigame();
        Check(Together.MinigameBase_StartMinigame_MultiplayerPatch.Notifications==startPackets+1 && Together.MinigameBase_EndMinigame_MultiplayerPatch.Notifications==endPackets+1,"Unrelated native minigames retain both Together notifications.");
        TogetherMinigameCompatibility.Install(surgery,typeof(Program).Assembly);host.StartMinigame(new SurgeryView(),null);host.EndMinigame();
        Check(Harmony.GetPatchInfo(hooks.GetMethod("Postfix")).Prefixes.Count==1,"Reconnect installation does not stack duplicate guards.");
        Check(Together.MinigameBase_StartMinigame_MultiplayerPatch.Ids.Count==1 && Together.MinigameBase_StartMinigame_MultiplayerPatch.Ids[typeof(NativeView)]==7,"Existing native IDs are unchanged.");
        host.StartMinigame(new NativeView(),null);var existing=host.currentMinigame;starts=host.Starts;
        host.StartMinigame(new SurgeryView(),null);
        Check(ReferenceEquals(existing,host.currentMinigame) && host.Starts==starts,"Together's occupied-host veto is preserved.");host.EndMinigame();
        host.ThrowOnStart=true;bool nativeFailure=false;
        try{host.StartMinigame(new SurgeryView(),null);}catch(InvalidOperationException e){nativeFailure=e.Message=="native failure";}
        Check(nativeFailure,"The guard does not swallow unrelated native startup exceptions.");host.ThrowOnStart=false;
        bool mismatch=false;
        try{TogetherMinigameCompatibility.Install(surgery,typeof(object).Assembly);}catch(NotSupportedException){mismatch=true;}
        Check(mismatch,"Missing Together lifecycle signatures fail closed before installing patches.");
        var messages=new List<string>();TogetherMinigameCompatibility.Log=messages.Add;
        TogetherMinigameCompatibility.ObserveStartup(surgery,typeof(Program).Assembly);
        TogetherMinigameCompatibility.ObserveStartup(surgery,typeof(Program).Assembly);
        Together.MinigameBase_StartMinigame_MultiplayerPatch.ignore_next_mgstart_cuz_its_not_me=true;
        starts=host.Starts;host.StartMinigame(new SurgeryView(),null);
        Check(host.currentMinigame==null && host.Starts==starts,"Observed ignore-next veto does not start a surgery.");
        Check(messages.Count==1 && messages[0].Contains("allowed=False") && messages[0].Contains("ignoreNext=True"),"Veto logs capture pre-consumption state once across repeated installation.");
        host.StartMinigame(new SurgeryView(),null);
        Check(host.currentMinigame is SurgeryView && messages.Count==2 && messages[1].Contains("allowed=True"),"A subsequent attempt is allowed and logged after the native flag is consumed.");host.EndMinigame();
        int logged=messages.Count;host.StartMinigame(new NativeView(),null);host.EndMinigame();
        Check(messages.Count==logged,"Unrelated native games do not produce surgery startup logs.");
        TogetherMinigameCompatibility.Log=_=>throw new Exception("logger failure");
        host.StartMinigame(new SurgeryView(),null);
        Check(host.currentMinigame is SurgeryView,"Logging failure cannot change native startup behavior.");host.EndMinigame();
        TogetherMinigameCompatibility.Log=null;
        surgery.UnpatchSelf();reproduced=false;
        host.StartMinigame(new SurgeryView(),null);
        Check(host.currentMinigame==null,"Unpatch restores the original cold-start prerequisite.");
        Together.MinigameBase_StartMinigame_MultiplayerPatch.applying_on_limb=new object();
        try{host.StartMinigame(new SurgeryView(),null);}catch(KeyNotFoundException){reproduced=true;}
        Check(reproduced,"Unloading the surgery guard restores the original Together behavior.");
        native.UnpatchSelf();Console.WriteLine(checks+" real-Harmony minigame lifecycle checks passed.");
    }
}
