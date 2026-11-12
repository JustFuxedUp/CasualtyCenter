using System;
using CasualtiesSurgery.Procedures;
using CasualtiesSurgery.Game;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;
static class GelChecks
{
    static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
    public static void Run(Action<bool,string> check)
    {
        check(GelRules.Deposit(1,10,2)==2 && GelRules.Deposit(1,-2,2)==1,"Gel dose caps and rejects negative addition");
        check(GelRules.InfectionRisk(1,0,0,1)==0,"Full coverage suppresses surgery infection even at worst immunity and accuracy");
        check(GelRules.InfectionRisk(0,200,100,1)<GelRules.InfectionRisk(0,0,100,1),"Stronger immunity lowers unprepared surgery risk");
        check(GelRules.InfectionRisk(0,100,100,1)<GelRules.InfectionRisk(0,100,0,1),"Higher operation accuracy lowers infection risk");
        check(GelRules.InfectionRisk(.5f,100,50,1)<GelRules.InfectionRisk(0,100,50,1),"Partial disinfection lowers risk proportionally");
        check(GelRules.InfectionRisk(0,0,0,0)==0,"Disabled native infection chance remains disabled");
        var body=new Body();var limb=new Limb{body=body,skinHealth=80,broken=true,boneHealTimer=100};body.limbs=new[]{limb};PlayerCamera.main=new PlayerCamera{body=body};MinigameBase.main=null;
        var state=FractureStore.Get(limb);state.Disinfect(2,.2f);check(Near(state.GelCoverage[2],.2f)&&state.Coverage>0 && !state.Started,"Preparation before cutting does not open or damage skin");
        var provider=new FractureStore();var json=provider.Capture(limb,0).ToString();var restored=new Limb{body=body,skinHealth=80,broken=true,boneHealTimer=100};body.limbs=new[]{restored};provider.Restore(restored,0,JToken.Parse(json),1,new SaveRestoreContext());state=FractureStore.Get(restored);
        check(Near(state.GelCoverage[2],.2f) && !state.Started,"Partial gel coverage survives save/load");
        for(int i=0;i<9;i++){state.Disinfect(i,2);restored.skinHealth-=state.Cut(i,restored.skinHealth);}state.Angles[0]=0;state.Align(0);state.Angles[1]=0;state.Align(1);
        check(Near(state.Coverage,1)&&!state.Disinfect(0,.1f),"All prepared sections cap at full coverage");
        UnityEngine.Random.value=0;check(FractureStore.TryClose(restored,80)&&!restored.infected,"Disinfected closure cannot roll surgery infection");
        check(!FractureStore.TryClose(restored,80)&&!restored.infected,"Duplicate closure cannot reroll infection");
        SurgeryInfection.Apply(restored,0,0);check(restored.infected&&restored.infectionAmount==5,"Unprepared surgery can start native infection");
        SurgeryInfection.Apply(restored,0,100);check(restored.infectionAmount==6,"Unprepared surgery can increase existing infection");
        SurgeryInfection.Apply(restored,1,0);check(restored.infectionAmount==6,"Prepared surgery does not worsen existing infection");
        WorldGeneration.InfectionChance=0;SurgeryInfection.Apply(restored,0,0);check(restored.infectionAmount==6,"Adapter respects native infection settings");WorldGeneration.InfectionChance=1;UnityEngine.Random.value=.99f;
        var board=new FragmentBoard(7,1);board.Sites[0].Extracted=true;board.Sites[0].GelMl=1.1f;
        var reloaded=JObject.FromObject(board).ToObject<FragmentBoard>();reloaded.Validate();check(Near(reloaded.Sites[0].GelMl,1.1f)&&!reloaded.Sites[0].Completed,"Partial fragment gel dose persists without granting completion");
        var legacy=JObject.FromObject(new FractureState());legacy.Remove("GelCoverage");legacy.Remove("BoneQualitySum");legacy.Remove("BoneQualityCount");var old=legacy.ToObject<FractureState>();old.Validate();check(old.Coverage==0,"Older surgery saves start unprepared without migration errors");
        limb=new Limb{body=body,skinHealth=60,bleedAmount=5,shrapnel=1};body.limbs=new[]{limb};var realBoard=FragmentBoardStore.Get(limb);var site=realBoard.Sites[0];
        var session=new OperationSession(body,limb,5,1);session.Extract();site.Extracted=true;site.GelMl=1;
        check(!session.CompleteGelClosure(site)&&limb.shrapnel==1&&limb.skinHealth==60,"Insufficient gel cannot close or heal a fragment wound");
        site.GelMl=2;check(session.CompleteGelClosure(site)&&site.Completed&&limb.shrapnel==0&&limb.skinHealth==65&&limb.bleedAmount==4,"Full gel dose uses normal fragment closure reward exactly once");
        check(!session.CompleteGelClosure(site)&&limb.skinHealth==65,"Repeated gel application cannot duplicate healing");
        var foreign=new FragmentSite{Extracted=true,GelMl=2};check(!session.CompleteGelClosure(foreign),"Gel closure rejects a wound from another board");
        bool invalid=false;try {new FractureState{GelCoverage=new[]{float.NaN}}.Validate();}catch(ArgumentException){invalid=true;}check(invalid,"Malformed gel state rejected");
    }
}
