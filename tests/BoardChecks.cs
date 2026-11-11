using System;
using System.Linq;
using CasualtiesSurgery.Procedures;
using CasualtiesSurgery.Game;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;
static class BoardChecks
{
    public static void Run(Action<bool,string> check)
    {
        var board=new FragmentBoard(123,5);
        check(board.Remaining==5 && board.Sites.Count==5,"One site per real fragment");
        var other=new FragmentBoard(123,5);
        for(int i=0;i<5;i++)
        {
            var a=board.Layout(i); var b=other.Layout(i);
            check(a.X==b.X && a.Y==b.Y && a.Angle==b.Angle && a.Length==b.Length,"Seed recreates site geometry exactly");
        }
        check(board.Layout(0).X!=new FragmentBoard(124,5).Layout(0).X,"Different seed varies layout");
        check(!board.Complete(0),"Unextracted site cannot be completed");
        var original=board.Layout(3); board.Sites[0].Extracted=true;
        check(board.Complete(0) && !board.Complete(0) && board.Remaining==4,"Completion retires exactly one ID");
        check(board.Layout(3).X==original.X && board.Layout(3).Angle==original.Angle,"Completion does not rearrange survivors");
        board.Reconcile(4);
        check(board.Sites[0].Completed && board.NextId==5,"Reopen retains completed hole and IDs");
        board.Sites[1].Extracted=true;
        board.Reconcile(3);
        check(board.Sites[4].Completed && !board.Sites[1].Completed && board.Sites[1].Extracted,"External removal preserves pending suture when possible");
        board.Reconcile(6);
        check(board.NextId==8 && board.Remaining==6 && board.Layout(3).X==original.X,"New fragments append without moving survivors");
        var many=new FragmentBoard(777,30);
        check(many.Sites.Count==30 && many.Layout(12).Page==1 && many.Layout(24).Page==2,"Large fragment counts retain all sites across pages");
        check(many.Sites.All(site=>Math.Abs(many.Layout(site.Id).X)<350 && Math.Abs(many.Layout(site.Id).Y)<240),"All generated site centers stay within board");
        var provider=new FragmentBoardStore(); var limb=new Limb { shrapnel=3 };
        var saved=FragmentBoardStore.Get(limb); saved.Sites[1].Extracted=true;
        saved.Sites[0].Extracted=true; saved.Complete(0); limb.shrapnel=2;
        var token=provider.Capture(limb,0);
        // Exercise the actual JSON provider, not a parallel serialization implementation.
        var json=token.ToString(); var restoredLimb=new Limb { shrapnel=0 }; var context=new SaveRestoreContext();
        provider.Restore(restoredLimb,0,JToken.Parse(json),provider.GetVersion(),context);
        restoredLimb.shrapnel=2; context.Flush(); var restored=FragmentBoardStore.Get(restoredLimb);
        check(restored.Seed==saved.Seed && restored.NextId==3,"Save/load retains seed and next ID");
        check(restored.Sites[0].Completed && restored.Sites[1].Extracted && restored.Remaining==2,"Save/load preserves retired and extracted-unsutured sites");
        check(restored.Layout(1).Angle==saved.Layout(1).Angle,"Save/load preserves wound geometry");
        var separate=new Limb { shrapnel=2 };
        check(!ReferenceEquals(FragmentBoardStore.Get(separate),restored),"Board state is isolated per limb");
        check(ReferenceEquals(FragmentBoardStore.Get(restoredLimb),restored),"Reopening same limb returns same state");
        check(provider.Capture(new Limb(),0).Type==JTokenType.Null,"Untouched limbs do not allocate saved boards");
        bool rejected=false;
        try { new FragmentBoard { NextId=1, Sites=new System.Collections.Generic.List<FragmentSite> {new FragmentSite {Id=0},new FragmentSite {Id=0}} }.Validate(); }
        catch(ArgumentException) {rejected=true;}
        check(rejected,"Duplicate saved wound IDs rejected");
        var stroke=new SutureStroke(2); stroke.Begin(-1,0);
        check(stroke.Move(-2.6f,0.1f),"Messy suture can extend beyond the previous outer margin");
        check(!stroke.Move(-3.1f,0.2f),"Suture still has a finite outside boundary");
        var pull=new FragmentPull(2,0.2f,0.005f);
        check(pull.Begin(0,0.38f) && pull.End(0,0.18f),"Short perpendicular extraction stroke accepted");
        pull.Begin(0,0.38f);
        check(!pull.End(0,0.3f),"Still-embedded fragment cannot be released successfully");
    }
}
