using System;
using CasualtiesSurgery.Procedures;
static class SutureChecks
{
    public static void Run(Action<bool,string> check)
    {
        var s=new SutureStroke(2);
        check(!s.End(1,0.5f,out _),"Release without grip does not score");
        check(s.Begin(0,0.5f),"Suture can start in the middle of the wound");
        s.Move(1,0.5f);s.Move(-1,0.6f);s.Move(1,0.7f);s.Move(-1,0.8f);
        check(s.End(0,1.5f,out _) && s.Crossings==3,"Three crossings score with a center start and outside end");
        check(!s.End(0,1.5f,out _),"Suture release cannot score twice");
        s.Begin(1,0.8f);s.Move(-1,0.7f);s.Move(1,0.6f);s.Move(-1,0.5f);
        check(s.End(-3,-0.5f,out _),"Reverse-direction suture and arbitrary endpoint accepted");
        s.Begin(-1,0.2f);s.Move(1,0.3f);s.Move(-1,0.4f);
        check(!s.End(-1,0.5f,out _),"Two crossings do not score");
        s.Begin(-1,2);s.Move(1,2);s.Move(-1,2);s.Move(1,2);
        check(!s.End(1,2,out _),"Crossing an imaginary extension beyond the wound does not count");
        s.Begin(-1,2);s.Move(-1,0.4f);s.Move(1,0.4f);s.Move(-1,0.6f);s.Move(1,0.8f);
        check(s.End(3,2,out _),"Stroke may enter wound from elsewhere and leave after three crossings");
        s.Begin(-0.1f,0.5f);
        for(int i=0;i<20;i++)s.Move(i%2==0?0.1f:-0.1f,0.5f);
        check(!s.End(0.1f,0.5f,out _),"Tiny center jitter is not three full crossings");
        s.Begin(-1,0.5f);s.Move(0,0.5f);s.Move(1,0.5f);s.Move(0,0.5f);s.Move(-1,0.5f);s.Move(0,0.5f);s.Move(1,0.5f);
        check(s.End(1,0.5f,out _) && s.Crossings==3,"Samples landing exactly on the center count once each");
        s.Begin(-1,0.5f);s.Move(1,0.5f);s.Move(-1,0.5f);s.Move(1,0.5f);
        check(s.End(1,0.5f,out _),"Three deliberate retraced crossings satisfy the current simple rule");
        s.Begin(-1,0.2f);s.Move(1,0.4f);s.Reset();
        check(!s.End(-1,0.5f,out _),"Interrupted suture cannot finish");
        check(!s.Begin(float.NaN,0),"Invalid suture start rejected");
        s.Begin(-1,0.2f);check(!s.Move(float.PositiveInfinity,0),"Invalid pointer terminates stroke");
        int turns=0;s=new SutureStroke(2);s.TurnCommitted+=(x,y)=>turns++;
        s.Begin(-1,0);s.Move(1,0.2f);check(turns==0,"Straight motion does not commit noisy trace segments");
        s.Move(0.7f,0.22f);check(turns==1,"Direction reversal commits the prior turn");
        s=new SutureStroke(2);s.Begin(-1,0);float px=-1,py=0;
        for(int i=1;i<=4;i++)
        {float x=i%2==1?1:-1,y=i/4f;for(int n=1;n<=20;n++)s.Move(px+(x-px)*n/20f,py+(y-py)*n/20f);px=x;py=y;}
        check(s.End(-1,1,out int quality) && s.Crossings==4 && quality>=90,"Dense sampling preserves crossing count and clean score");
    }
}
