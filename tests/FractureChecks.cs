using System;
using CasualtiesSurgery.Procedures;
using CasualtiesSurgery.Game;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;
static class FractureChecks
{
    static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
    static Limb Patient()
    {
        CUCoreLib.Networking.MultiplayerApi.IsRunning=false;MinigameBase.main=null;
        var body=new Body();var limb=new Limb{body=body,skinHealth=80,broken=true,boneHealTimer=100,bleedAmount=7,muscleHealth=70,pain=10};body.limbs=new[]{limb};PlayerCamera.main=new PlayerCamera{body=body};return limb;
    }
    static void Open(FractureState s,Limb limb){for(int i=8;i>=0;i--)limb.skinHealth-=s.Cut(i,limb.skinHealth);}
    static void Align(FractureState s){s.Angles[0]=8;s.Align(0);s.Angles[1]=-8;s.Align(1);}
    public static void Run(Action<bool,string> check)
    {
        var trace=new ScalpelTrace();check(trace.Step(true,false,.2f)==0 && trace.Step(true,false,.2f)==0,"Scalpel has 0.4 second grace");
        check(Near(trace.Step(true,false,.05f),.05f),"Scalpel damage starts only beyond grace");
        check(trace.Step(true,true,.1f)==0 && trace.Step(true,false,.4f)==0,"New objective contact renews grace");
        trace.Step(true,false,.6f);check(trace.Step(true,true,.016f)==0 && trace.Step(true,false,.39f)==0 && trace.Step(true,false,.02f)>0,"Objective contact after damage grants a fresh full grace interval");
        trace.Step(false,false,1);check(trace.Step(true,false,.1f)==0,"Release ends active cutting");
        trace.Reset();check(!trace.Touch(-100,0,0,0,10),"Distant point is not objective contact");trace.Sample(-100,0);
        check(trace.Touch(100,0,0,0,10),"Swept cutting catches dots between frames");
        trace.Reset();check(!trace.Touch(100,0,0,0,10),"Release/tool switch cannot cut across an old segment");
        var limb=Patient();var state=FractureStore.Get(limb);
        check(!FractureStore.HasOpen(limb),"Untouched fracture has no open-surgery marker");
        check(FractureStore.Ineligible(limb.body,limb)==null,"Fracture eligible without fragments");
        float first=state.Cut(4,limb.skinHealth);limb.skinHealth-=first;
        check(FractureStore.HasOpen(limb),"Partial incision immediately marks its own limb open");
        check(first>0 && state.Cut(4,limb.skinHealth)==0,"Each incision dot charges skin once");
        var provider=new FractureStore();var json=provider.Capture(limb,0).ToString();float savedSkin=limb.skinHealth;
        var restored=Patient();restored.skinHealth=savedSkin;provider.Restore(restored,0,JToken.Parse(json),1,new SaveRestoreContext());state=FractureStore.Get(restored);
        check(state.Cuts[4] && Near(state.SkinDebt,first) && Near(restored.skinHealth,savedSkin),"Partial incision save restores debt without charging again");
        Open(state,restored);check(state.Stage==FractureStage.Alignment && Near(restored.skinHealth,32) && Near(state.SkinDebt,48),"Any dot order opens incision for 60 percent of starting skin");
        state.Angles[0]=13;check(!state.Align(0),"Alignment outside 12 degree threshold rejected");
        state.Angles[0]=372;check(state.Align(0),"Wrapped angle within threshold accepted");
        state.Angles[1]=-12;check(state.Align(1) && state.Stage==FractureStage.Closure,"Both bone pieces required for closure");
        restored.skinHealth-=5;restored.boneHealTimer=80;
        check(FractureStore.TryClose(restored,75) && Near(restored.skinHealth,75) && Near(restored.boneHealTimer,20) && restored.broken,"Closure returns only incision debt and reduces live timer without mending");
        check(!FractureStore.HasOpen(restored),"Closed incision clears open marker");
        check(!FractureStore.TryClose(restored,75) && Near(restored.skinHealth,75) && Near(restored.boneHealTimer,20),"Repeated closure cannot duplicate healing or timer reduction");
        check(FractureStore.Ineligible(restored.body,restored)!=null,"Treated fracture cannot be farmed");
        FractureStore.NewBreak(restored);check(FractureStore.Get(restored).Stage==FractureStage.Incision,"New fracture episode permits a new operation");
        limb=Patient();state=FractureStore.Get(limb);Open(state,limb);Align(state);check(FractureStore.TryClose(limb,0)&&Near(limb.skinHealth,56),"Bad closure permanently loses part of the incision cost");
        limb=Patient();state=FractureStore.Get(limb);Open(state,limb);Align(state);check(FractureStore.TryClose(limb,41)&&Near(limb.skinHealth,80),"Good boundary refunds full incision cost");
        limb=Patient();FractureStore.AlignmentMistake(limb);
        check(Near(limb.boneHealTimer,102)&&Near(limb.pain,35)&&Near(limb.muscleHealth,68)&&Near(limb.bleedAmount,7),"Alignment mistake changes timer pain muscle but not bleeding");
        FractureStore.ScalpelDamage(limb,1);check(Near(limb.skinHealth,75)&&Near(limb.pain,45)&&Near(limb.muscleHealth,67)&&Near(limb.bleedAmount,7),"Off-target scalpel damages tissue without adding bleeding");
        state=FractureStore.Get(limb);limb.skinHealth-=state.Cut(0,limb.skinHealth);FractureStore.NewBreak(limb);check(ReferenceEquals(state,FractureStore.Get(limb)),"Reinjury cannot erase an open incision");
        Open(state,limb);Align(state);limb.broken=false;limb.boneHealTimer=0;
        check(FractureStore.TryClose(limb,80)&&limb.boneHealTimer==0,"External fracture healing still permits skin closure without rebreaking");
        check(FractureState.ReducedTimer(.05f)>0 && FractureState.ReducedTimer(.05f)<=.05f,"Near-healed fracture remains positive without being worsened");
        limb=Patient();state=FractureStore.Get(limb);Open(state,limb);state.Angles[0]=5;state.Align(0);state.Angles[1]=22;
        json=provider.Capture(limb,0).ToString();savedSkin=limb.skinHealth;
        restored=Patient();restored.skinHealth=savedSkin;provider.Restore(restored,0,JToken.Parse(json),1,new SaveRestoreContext());state=FractureStore.Get(restored);
        check(state.Stage==FractureStage.Alignment && state.Aligned[0] && !state.Aligned[1] && state.Angles[1]==22 && Near(state.SkinDebt,48),"Mid-alignment save preserves locked pieces angles and skin debt");
        limb=Patient();state=FractureStore.Get(limb);limb.skinHealth-=state.Cut(0,limb.skinHealth);
        float debt=state.SkinDebt;var other=new Limb{body=limb.body,skinHealth=60,shrapnel=1,bleedAmount=3};limb.body.limbs=new[]{limb,other};
        var fragments=FragmentBoardStore.Get(other);var extraction=new OperationSession(limb.body,other,5,1);check(extraction.Extract(),"Other limb extraction allowed during saved incision");fragments.Sites[0].Extracted=true;
        check(FractureStore.HasOpen(limb) && !FractureStore.HasOpen(other) && FragmentBoardStore.HasPendingSuture(other),"Dummy indicators distinguish incision and pending fragment by limb");
        var fractureStroke=new SutureStroke(2);fractureStroke.Begin(-1,.1f);fractureStroke.Move(1,.2f);
        var fragmentStroke=new SutureStroke(2);fragmentStroke.Begin(-1,.1f);fragmentStroke.Move(1,.2f);fragmentStroke.Move(-1,.4f);
        check(fragmentStroke.End(1,.6f,out _) && fractureStroke.Crossings==1,"Separate operation strokes cannot consume each other's crossings");
        check(extraction.CompleteContinuousSuture() && fragments.Complete(0),"Other-limb fragment suture commits while incision stays open");
        check(ReferenceEquals(state,FractureStore.Get(limb)) && state.CutsDone==1 && Near(state.SkinDebt,debt) && !FragmentBoardStore.HasPendingSuture(other),"Fragment completion preserves incision progress and clears only its own marker");
        Open(state,limb);Align(state);check(FractureStore.TryClose(limb,80),"Original fracture can resume and close after treating another limb");
        bool rejected=false;try{new FractureState{SkinDebt=float.NaN}.Validate();}catch(ArgumentException){rejected=true;}check(rejected,"Invalid saved debt rejected");
        limb=Patient();limb.dismembered=true;check(FractureStore.Ineligible(limb.body,limb)!=null,"Detached limb cannot enter fracture surgery");
        limb=Patient();CUCoreLib.Networking.MultiplayerApi.IsRunning=true;check(FractureStore.Ineligible(limb.body,limb)!=null,"Fracture operation remains gated in multiplayer");CUCoreLib.Networking.MultiplayerApi.IsRunning=false;
    }
}
