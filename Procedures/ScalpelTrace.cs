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
    public enum FractureStage { Incision, Alignment, Closure, Complete }
    public sealed class FractureState
    {
        public FractureStage Stage;
        public bool[] Cuts=new bool[9],Aligned=new bool[2];
        public float[] Angles={-32,38};
        public float SkinDebt, PlannedCost, CutSeconds;
        public bool Started;
        public int CutsDone {get {int n=0;foreach(bool c in Cuts)if(c)n++;return n;}}
        public int CutScore => Math.Max(0,Math.Min(100,(int)(100-Math.Max(0,CutSeconds-2)*8)));
        public float Cut(int index,float skin)
        {
            if(Stage!=FractureStage.Incision || Cuts[index])return 0;
            if(!Started){Started=true;PlannedCost=skin*.6f;}
            float cost=Math.Min(skin,PlannedCost/Cuts.Length); SkinDebt+=cost;Cuts[index]=true;
            if(CutsDone==Cuts.Length)Stage=FractureStage.Alignment;
            return cost;
        }
        public static float AngleError(float angle){return Math.Abs(((angle%360)+540)%360-180);}
        public bool Align(int index)
        {
            if(Stage!=FractureStage.Alignment || Aligned[index] || AngleError(Angles[index])>12)return false;
            Aligned[index]=true;Angles[index]=0;
            if(Array.TrueForAll(Aligned,a=>a))Stage=FractureStage.Closure;
            return true;
        }
        public float Close(int quality)
        {
            if(Stage!=FractureStage.Closure)return 0;
            float refund=SkinDebt*(quality>=41?1:.5f+.5f*Math.Max(0,quality)/41);
            Stage=FractureStage.Complete;SkinDebt=0;return refund;
        }
        public static float ReducedTimer(float timer)=>Math.Min(timer,Math.Max(.1f,timer*.25f));
        public void Validate()
        {
            if(Stage<FractureStage.Incision || Stage>FractureStage.Complete || Cuts==null || Cuts.Length!=9 || Aligned==null || Aligned.Length!=2 || Angles==null || Angles.Length!=2 ||
                !Finite(SkinDebt) || SkinDebt<0 || SkinDebt>60.01f || !Finite(PlannedCost) || PlannedCost<0 || PlannedCost>60 || !Finite(CutSeconds) || CutSeconds<0 || Array.Exists(Angles,a=>!Finite(a)))throw new ArgumentException("Invalid fracture operation save.");
            if((!Started && (CutsDone!=0 || SkinDebt!=0 || PlannedCost!=0 || Stage!=FractureStage.Incision)) ||
                (Stage>=FractureStage.Alignment && CutsDone!=Cuts.Length) ||
                (Stage==FractureStage.Complete && SkinDebt!=0))throw new ArgumentException("Inconsistent fracture operation save.");
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    }
}
