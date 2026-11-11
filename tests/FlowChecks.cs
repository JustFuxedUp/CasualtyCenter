using System;
using CasualtiesSurgery.Procedures;
using CasualtiesSurgery.Game;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;
static class FlowChecks
{
    public static void Run(Action<bool,string> check)
    {
        var pull=new FragmentPull(2,0.2f,0.005f);
        pull.Begin(0,0.38f);check(pull.Move(0,0.17f)&&pull.Loose,"Clearing cut latches loose state");
        check(pull.Move(20,20)&&pull.Loose,"Loose fragment tolerates free movement back over the cut");
        check(pull.End(-20,40)&&pull.Quality==100,"Loose fragment release anywhere preserves score");
        check(!pull.End(0,0),"Loose fragment cannot be scored twice");
        pull.Begin(0,0.38f);check(!pull.End(0,0.32f)&&!pull.Loose,"Releasing while embedded still fails");
        pull.Begin(0,0.38f);pull.Move(0,0.17f);pull.Reset();check(!pull.Loose&&!pull.Active,"Cancellation resets unsaved loose gesture");
        bool spaced=true;
        for(int seed=0;seed<200;seed++)
        {
            var board=new FragmentBoard(seed,12);
            for(int i=0;i<12;i++)for(int j=0;j<i;j++)
            {var a=board.Layout(i);var b=board.Layout(j);float dx=a.X-b.X,dy=a.Y-b.Y;if(dx*dx+dy*dy<110*110)spaced=false;}
        }
        check(spaced,"Scattered centers remain at least 110 board units apart across 200 seeds");
        var original=new FragmentBoard(1234,3);var point=original.Layout(2);original.Reconcile(9);
        check(original.Layout(2).X==point.X&&original.Layout(2).Y==point.Y,"Adding fragments does not relocate scattered sites");
        var old=new FragmentBoard(55,2){LayoutVersion=0};old.Sites[0].Extracted=true;
        var token=JObject.FromObject(old);token.Remove("LayoutVersion");
        var limb=new Limb {shrapnel=2};var context=new SaveRestoreContext();
        new FragmentBoardStore().Restore(limb,0,token,1,context);context.Flush();
        var restored=FragmentBoardStore.Get(limb);
        check(restored.LayoutVersion==1&&restored.Seed==55&&restored.Sites[0].Extracted,"Legacy grid save migrates layout while preserving seed and pending suture");
    }
}
