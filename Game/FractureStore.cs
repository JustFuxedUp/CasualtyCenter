using System;
using System.Runtime.CompilerServices;
using CasualtiesSurgery.Procedures;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;
using HarmonyLib;
using UnityEngine;
namespace CasualtiesSurgery.Game
{
    internal sealed class FractureStore : ILimbSaveProvider
    {
        static readonly ConditionalWeakTable<Limb,FractureState> states=new ConditionalWeakTable<Limb,FractureState>();
        public static FractureState Get(Limb limb)=>states.GetValue(limb,l=>new FractureState());
        public static bool HasOpen(Limb limb)=>limb!=null && states.TryGetValue(limb,out var s) && s.Started && s.Stage!=FractureStage.Complete;
        public static void NewBreak(Limb limb)
        {
            // Never discard an open incision, even if a heal command is followed by another injury.
            if(states.TryGetValue(limb,out var s) && (s.Stage==FractureStage.Complete || !s.Started))states.Remove(limb);
        }
        public int GetVersion()=>1;
        public JToken Capture(Limb limb,int index)=>states.TryGetValue(limb,out var s)?JObject.FromObject(s):JValue.CreateNull();
        public void Restore(Limb limb,int index,JToken data,int version,SaveRestoreContext context)
        {
            if(version!=1 || data==null || data.Type==JTokenType.Null)return;
            var s=data.ToObject<FractureState>();s.Validate();states.Remove(limb);states.Add(limb,s);
        }
        public static string Ineligible(Body body,Limb limb,Minigame owner=null)
        {
            string basic=OperationSession.Ineligible(body,limb,owner,false);if(basic!=null)return basic;
            if(float.IsNaN(limb.boneHealTimer)||float.IsInfinity(limb.boneHealTimer)||float.IsNaN(limb.pain)||float.IsInfinity(limb.pain)||float.IsNaN(limb.muscleHealth)||float.IsInfinity(limb.muscleHealth))return "Invalid fracture values.";
            var state=Get(limb);
            if(state.Started && state.Stage!=FractureStage.Complete)return null;
            if(!limb.broken || limb.boneHealTimer<=0)return "No fracture to align.";
            if(state.Stage==FractureStage.Complete)return "Fracture already treated; allow natural healing.";
            if(limb.skinHealth<=0)return "No viable skin for an incision.";
            return null;
        }
        public static bool TryClose(Limb limb,int quality,Minigame owner=null)
        {
            if(quality<0 || quality>100 || Ineligible(limb.body,limb,owner)!=null)return false;
            var s=Get(limb);if(s.Stage!=FractureStage.Closure)return false;
            limb.skinHealth=Mathf.Min(100,limb.skinHealth+s.Close(quality));
            if(limb.broken && Array.TrueForAll(s.Aligned,a=>a))limb.boneHealTimer=FractureState.ReducedTimer(limb.boneHealTimer);
            return true;
        }
        public static void ScalpelDamage(Limb limb,float seconds)
        {
            limb.skinHealth=Mathf.Max(0,limb.skinHealth-5*seconds);
            limb.muscleHealth=Mathf.Max(0,limb.muscleHealth-seconds);
            limb.pain=Mathf.Min(100,limb.pain+10*seconds);
        }
        public static void AlignmentMistake(Limb limb)
        {
            if(limb.broken)limb.boneHealTimer+=2;
            limb.pain=Mathf.Min(100,limb.pain+25);limb.muscleHealth=Mathf.Max(0,limb.muscleHealth-2);
        }
    }
    [HarmonyPatch(typeof(Limb),nameof(Limb.BreakBone))]
    internal static class FractureEpisode
    {
        static void Prefix(Limb __instance,out bool __state){__state=__instance.broken;}
        static void Postfix(Limb __instance,bool __state){if(!__state && __instance.broken)FractureStore.NewBreak(__instance);}
    }
    internal enum SurgeryTool { Forceps,Suture,Scalpel }
    internal interface ISurgery
    {
        bool Ended {get;} bool BlocksToolInput {get;} float HandScale {get;}
        string InvalidReason();void Finish(string reason);void OnEnded();void RefreshGuide();
    }
}
