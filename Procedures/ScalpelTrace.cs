using System;
namespace CasualtiesSurgery.Procedures
{
    public sealed class ScalpelTrace
    {
        float idle,px,py; bool previous;
        // Returns seconds of active cutting outside productive objectives after grace expires.
        public float Step(bool active,bool productive,float dt)
        {
            if(!active){idle=0;previous=false;return 0;}
            if(productive){idle=0;return 0;}
            float before=idle;idle+=Math.Max(0,dt);
            return Math.Max(0,idle-.4f)-Math.Max(0,before-.4f);
        }
        public bool Touch(float x,float y,float tx,float ty,float radius)
        {
            float dx=x-px,dy=y-py,t=0;
            if(previous && dx*dx+dy*dy>0)t=Math.Max(0,Math.Min(1,((tx-px)*dx+(ty-py)*dy)/(dx*dx+dy*dy)));
            float cx=previous?px+t*dx:x,cy=previous?py+t*dy:y;
            return (tx-cx)*(tx-cx)+(ty-cy)*(ty-cy)<=radius*radius;
        }
        public void Sample(float x,float y){px=x;py=y;previous=true;}
        public void Reset(){idle=0;previous=false;}
    }
    internal static class IncisionSkin
    {
        public static float Plan(float skin) => Math.Min(60,skin * .6f);
        public static float CutCost(float skin, float planned, int cuts) => Math.Min(skin, planned / cuts);
        public static float Refund(float debt, int quality) => debt * (quality >= 41 ? 1 : .5f + .5f * Math.Max(0, quality) / 41);
    }
    public enum FractureStage { Incision, Alignment, Closure, Complete }
    public sealed class FractureState : IOpenSurgeryState
    {
        public const float AlignmentToleranceDegrees = 5f;
        public FractureStage Stage;
        public bool[] Cuts=new bool[9],Aligned=new bool[2];
        public float[] Angles={-32,38};
        public float SkinDebt, PlannedCost, CutSeconds;
        public BloodPools[] Pools = { new BloodPools(), new BloodPools(), new BloodPools() };
        public float[] GelCoverage=new float[GelRules.IncisionSections];
        public float BoneQualitySum;
        public int BoneQualityCount;
        public float Coverage => GelRules.IncisionCoverage(GelCoverage);
        public bool Disinfect(int index,float ml) => OpenSurgery.Disinfect(this,index,ml);
        OpenSurgeryPhase IOpenSurgeryState.Phase => (OpenSurgeryPhase)Stage;
        bool[] IOpenSurgeryState.IncisionCuts => Cuts;
        float[] IOpenSurgeryState.Preparation => GelCoverage;
        float IOpenSurgeryState.IncisionPlan => PlannedCost;
        float IOpenSurgeryState.IncisionDebt { get => SkinDebt; set => SkinDebt = value; }
        BloodPools IOpenSurgeryState.ActivePools => Stage == FractureStage.Complete ? null : Pools[(int)Stage];
        void IOpenSurgeryState.EnterRepair() { Stage = FractureStage.Alignment; }
        void IOpenSurgeryState.FinishClosure() { Stage = FractureStage.Complete; }
        public float OverallQuality(int closure)=>(CutScore+BoneQualitySum+closure)/(2+BoneQualityCount);
        public bool Started;
        public int CutsDone {get {int n=0;foreach(bool c in Cuts)if(c)n++;return n;}}
        public int CutScore => Math.Max(0,Math.Min(100,(int)(100-Math.Max(0,CutSeconds-2)*8)));
        public float Cut(int index,float skin)
        {
            if(!OpenSurgery.CanCut(this,index,skin))return 0;
            if(!Started){Started=true;PlannedCost=IncisionSkin.Plan(skin);}
            return OpenSurgery.Cut(this,index,skin);
        }
        public static float AngleError(float angle){return Math.Abs(((angle%360)+540)%360-180);}
        public bool Align(int index)
        {
            if(Stage!=FractureStage.Alignment || Aligned[index] || AngleError(Angles[index])>AlignmentToleranceDegrees)return false;
            BoneQualitySum+=100*(1-AngleError(Angles[index])/24);BoneQualityCount++;
            Aligned[index]=true;Angles[index]=0;
            if(Array.TrueForAll(Aligned,a=>a))Stage=FractureStage.Closure;
            return true;
        }
        public float Close(int quality)
        {
            return OpenSurgery.Close(this,0,quality,out float refund) ? refund : 0;
        }
        public static float ReducedTimer(float timer)=>Math.Min(timer,Math.Max(.1f,timer*.25f));
        public void Validate()
        {
            if(Stage<FractureStage.Incision || Stage>FractureStage.Complete || Cuts==null || Cuts.Length!=9 || Aligned==null || Aligned.Length!=2 || Angles==null || Angles.Length!=2 ||
                !Finite(SkinDebt) || SkinDebt<0 || SkinDebt>60.01f || !Finite(PlannedCost) || PlannedCost<0 || PlannedCost>60.01f || !Finite(CutSeconds) || CutSeconds<0 || Array.Exists(Angles,a=>!Finite(a)))throw new ArgumentException("Invalid fracture operation save.");
            if(!GelRules.ValidIncisionCoverage(GelCoverage) || BoneQualityCount<0 || BoneQualityCount>2 || !Finite(BoneQualitySum) || BoneQualitySum<0 || BoneQualitySum>200)throw new ArgumentException("Invalid gel/quality state.");
            if(Pools==null || Pools.Length!=3 || Array.Exists(Pools,p=>p==null || !p.Valid()))throw new ArgumentException("Invalid fracture blood pool state.");
            if((!Started && (CutsDone!=0 || SkinDebt!=0 || PlannedCost!=0 || Stage!=FractureStage.Incision)) ||
                (Stage>=FractureStage.Alignment && CutsDone!=Cuts.Length) ||
                (Stage==FractureStage.Complete && SkinDebt!=0))throw new ArgumentException("Inconsistent fracture operation save.");
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    }
}
