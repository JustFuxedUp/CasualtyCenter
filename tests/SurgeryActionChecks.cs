using System;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;

static class SurgeryActionChecks
{
    public static void Run(Action<bool,string> check)
    {
        var body = new Body();
        var limb = new Limb { body = body, skinHealth = 100, shrapnel = 1, broken = true, boneHealTimer = 100 };
        body.limbs = new[] { limb };
        PlayerCamera.main = new PlayerCamera { body = body };
        MinigameBase.main = null;
        string invalid = null;
        var actions = new SurgeryActions(limb, null, () => invalid, ProcedureKind.Fracture);
        var fracture = FractureStore.Get(limb);
        check(actions.Cut(fracture,0) && limb.skinHealth < 100, "Application cut commits skin and objective together");
        float skin = limb.skinHealth;
        check(!actions.Cut(fracture,0) && limb.skinHealth == skin, "Duplicate application cut has no extra debt");
        invalid = "interrupted";
        check(!actions.Cut(fracture,1) && !fracture.Cuts[1], "Interrupted application cannot cut");
        actions.Damage(1);
        check(limb.skinHealth == skin, "Interrupted application cannot damage");
        invalid = null;
        check(!actions.Disinfect(fracture,0,float.NaN) && limb.disinfectionTime == 0, "Invalid dose cannot refresh antiseptic");
        check(actions.Disinfect(fracture,0,.4f) && limb.disinfectionTime == 60, "Shared application refreshes antiseptic on productive dose");
        check(!actions.Cut(new FractureState(),1), "Detached state rejected by application");
        var foreignPool=new BloodPools(); foreignPool.Spill();
        check(!actions.Suction(foreignPool,0,1) && foreignPool.Blocking,"Unowned pool cannot be suctioned");
        fracture.Pools[1].Spill();
        check(!actions.Suction(fracture.Pools[1],0,1) && fracture.Pools[1].Blocking,"Future-stage pool cannot be suctioned");
        fracture.Pools[0].Spill();
        check(actions.Suction(fracture.Pools[0],0,1) && !fracture.Pools[0].Blocking,"Current procedure pool accepted");
        check(!actions.Disinfect(fracture,-1,1),"Application rejects invalid gel target without throwing");
        actions = new SurgeryActions(limb, null, () => invalid, ProcedureKind.Fragment);
        var board = FragmentBoardStore.Get(limb);
        var site = board.Sites[0];
        var session = new OperationSession(body,limb,5,1);
        check(actions.Extract(session,site,90) && site.Extracted, "Extraction and persisted site accepted together");
        check(!actions.Extract(session,site,90), "Duplicate extraction rejected");
        check(actions.FragmentGel(board,site,2), "Fragment dose accepted through application");
        check(actions.Complete(session,site,100,true) && site.Completed && limb.shrapnel == 0, "Fragment completion commits board and native count");
        skin = limb.skinHealth;
        check(!actions.Complete(session,site,100,true) && limb.skinHealth == skin, "Duplicate completion cannot refund twice");
        PlayerCamera.main = null;
        check(OperationSession.ClinicalIneligible(body,limb,false) == null, "Clinical checks independent of local camera");
        check(OperationSession.Ineligible(body,limb,null,false) != null, "Local application still requires local operator");
        PlayerCamera.main = new PlayerCamera { body = body };
    }
}
