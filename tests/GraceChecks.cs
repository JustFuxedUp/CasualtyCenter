using System;
using CasualtiesSurgery.Procedures;
static class GraceChecks
{
    public static void Run(Action<bool,string> check)
    {
        var pull=new FragmentPull(2);
        check(!pull.Begin(0,0.8f), "Fragment must be gripped near upper area");
        check(pull.Begin(0.3f,0.5f), "Doubled fragment grip margin accepts offset hand");
        check(pull.Move(0.6f,0.1f) && pull.Travel>0.39f, "Parallel pull tolerates doubled lateral deviation");
        check(pull.End(0.6f,-0.3f), "Full outward pull completes");
        check(!pull.End(0.6f,-0.3f), "Fragment release cannot be replayed");
        pull.Begin(0,0.38f);
        check(!pull.End(0,0.1f) && pull.Travel==0, "Early release resets fragment travel");
        pull.Begin(0,0.38f); pull.Move(0,-0.1f);
        check(!pull.Move(0,0.2f) && pull.Travel==0, "Large inward reversal resets fragment");
        pull.Begin(0,0.38f);
        check(!pull.Move(0.5f,0) && pull.Travel==0, "Off-axis extraction resets");
        pull.Begin(0,0.38f); pull.Move(0,0); pull.Reset();
        check(!pull.Active && pull.Travel==0, "Tool switch clears fragment displacement");
        check(!pull.Begin(float.NaN,0.38f), "Invalid fragment pointer rejected");
        var generous=new SutureStroke(2);
        check(generous.Begin(-0.4f,0.5f), "Suture starts away from prescribed endpoint");
        check(generous.Move(-2.1f,0.1f), "Messy outside suture travel accepted");
        generous.Begin(-1,0); generous.Move(1,0.4f);
        check(generous.Move(0.5f,0.2f), "Suture can reverse along wound");
        int turns=0; float turnX=0;
        generous=new SutureStroke(2); generous.TurnCommitted += (x,y)=>{turns++;turnX=x;};
        generous.Begin(-1,0); generous.Move(0,0.1f); generous.Move(1,0.2f);
        check(turns==0, "Thread does not commit on every movement sample");
        generous.Move(0.7f,0.22f);
        check(turns==1 && turnX==1, "Thread commits the outer turning point on reversal");
        generous.Reset();
        check(!generous.End(-1,1,out _), "Reset curve cannot reward treatment");
        generous.Begin(-1,0);
        for(int i=1;i<=4;i++) generous.Move(i%2==1?1:-1,i/4f);
        check(generous.End(-1,1,out int quality) && quality>=99, "Generous suture still scores a clean full stroke");
    }
}
