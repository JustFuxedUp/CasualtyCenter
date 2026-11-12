using System;
using CasualtiesSurgery.Game;
using CUCoreLib.Networking;

static class SessionChecks
{
    static OperationSession NewSession()
    {
        MultiplayerApi.IsRunning = false; MinigameBase.main = null;
        var body = new Body();
        var limb = new Limb { body = body, shrapnel = 2, skinHealth = 50, bleedAmount = 4 };
        body.limbs = new[] { limb }; PlayerCamera.main = new PlayerCamera { body = body };
        return new OperationSession(body, limb, 5, 1);
    }
    public static void Run(Action<bool, string> check)
    {
        var s = NewSession();
        check(s.InvalidReason() == null, "Healthy conscious local patient with fragments is eligible");
        check(!s.Stitch(out _), "Adapter rejects stitch before extraction");
        check(s.Extract() && s.Limb.shrapnel == 2 && s.Limb.skinHealth == 50, "Extraction defers game writes");
        check(s.Stitch(out var done) && !done && s.Stitch(out done) && !done && s.Limb.shrapnel == 2,
            "First two stitches do not grant reward");
        s.Limb.skinHealth = 40; s.Limb.bleedAmount = 8;
        check(s.Stitch(out done) && done && s.Limb.shrapnel == 1 && s.Limb.skinHealth == 45 && s.Limb.bleedAmount == 7,
            "Final stitch uses live wound state and applies combined reward");
        check(!s.Stitch(out _) && !s.Slip() && s.Limb.shrapnel == 1 && s.Limb.skinHealth == 45,
            "Completed session cannot reward or damage twice");
        s = NewSession(); s.Extract(); s.Stitch(out _); s.Cancel();
        check(!s.Stitch(out _) && !s.Slip() && s.Limb.shrapnel == 2 && s.Limb.skinHealth == 50,
            "Cancellation leaves wound unchanged and rejects further actions");
        s = NewSession(); s.Extract(); s.Limb.shrapnel++;
        check(!s.Stitch(out _) && !s.Slip(), "External fragment change invalidates session");
        s = NewSession(); s.Limb.dismembered = true;
        check(!s.Extract() && !s.Slip(), "Detached limb cannot be treated");
        s = NewSession(); s.Limb.prostheticInfo.hasProsthetic = true;
        check(!s.Extract(), "Prosthetic cannot be treated");
        s = NewSession(); s.Patient.conscious = false;
        check(!s.Extract(), "Unconscious patient cannot be treated");
        s = NewSession(); s.Patient.alive = false;
        check(!s.Extract(), "Dead patient cannot be treated");
        s = NewSession(); PlayerCamera.main.body = new Body();
        check(!s.Extract(), "Player replacement invalidates old session");
        s = NewSession(); s.Patient.limbs = new Limb[0];
        check(!s.Extract(), "Removed limb invalidates old session");
        s = NewSession(); s.Patient.transform.position = new UnityEngine.Vector3 { x = 2 };
        check(!s.Extract(), "Moving away invalidates session");
        s = NewSession(); s.Patient.gameObject.scene = new UnityEngine.Scene { handle = 1 };
        check(!s.Extract(), "Scene change invalidates session");
        s = NewSession(); MultiplayerApi.IsRunning = true;
        check(!s.Extract() && !s.Slip(), "Multiplayer blocks treatment and damage");
        s = NewSession(); MinigameBase.main = new MinigameBase { currentMinigame = new Minigame() };
        check(!s.Extract(), "Native minigame excludes surgery");
        s = NewSession(); s.Limb.skinHealth = float.NaN;
        check(!s.Extract(), "Nonfinite wound state rejected by adapter");
        s = NewSession();
        check(s.Slip() && s.Limb.skinHealth == 49 && System.Math.Abs(s.Limb.bleedAmount - 4.2f) < 0.001f
            && s.Limb.shrapnel == 2, "Slip damages wound without removing fragment");
        s = NewSession();
        var owner = new Minigame();
        var owned = new OperationSession(s.Patient, s.Limb, 5, 1, owner);
        check(!owned.Extract(), "Native operation cannot act before host assigns ownership");
        MinigameBase.main = new MinigameBase { currentMinigame = owner };
        check(OperationSession.Ineligible(s.Patient, s.Limb) != null, "New surgery cannot start while native operation runs");
        check(owned.Extract(), "Native operation accepts its own minigame slot");
        MinigameBase.main.currentMinigame = new Minigame();
        check(!owned.Stitch(out _) && !owned.Slip(), "Replacing the native minigame revokes treatment authority");
        MinigameBase.main.currentMinigame = null;
        check(!owned.Stitch(out _) && s.Limb.shrapnel == 2, "Closing native host cannot grant a reward");
        s = NewSession(); owner = new Minigame();
        owned = new OperationSession(s.Patient, s.Limb, 5, 1, owner);
        MinigameBase.main = new MinigameBase { currentMinigame = owner };
        owned.Extract(); owned.Stitch(out _); owned.Stitch(out _);
        check(owned.Stitch(out var nativeComplete) && nativeComplete && s.Limb.shrapnel == 1,
            "Owned native operation can complete treatment");
        owned.Cancel();
        check(!owned.Stitch(out _) && s.Limb.shrapnel == 1 && s.Limb.skinHealth == 55,
            "Native exit cleanup preserves exactly one completed reward");
        s = NewSession();
        check(!s.CompleteContinuousSuture(), "Continuous suture requires extraction");
        s.Extract(); s.Limb.skinHealth = 40; s.Limb.bleedAmount = 8;
        check(s.CompleteContinuousSuture() && s.Limb.shrapnel == 1 && s.Limb.skinHealth == 45 && s.Limb.bleedAmount == 7,
            "Continuous suture applies one atomic treatment using fresh stats");
        check(!s.CompleteContinuousSuture(), "Continuous treatment cannot be repeated");
        s = NewSession(); s.Extract(); s.Cancel();
        check(!s.CompleteContinuousSuture() && s.Limb.shrapnel == 2, "Cancelled continuous suture grants no reward");
        s=NewSession(); s.Extract();
        check(s.Slip() && s.Limb.skinHealth==50 && s.Limb.bleedAmount==4 && s.Limb.shrapnel==2,
            "Failed suture has no damage, bleeding, removal or reward");
        check(s.CompleteContinuousSuture() && s.Limb.shrapnel==1, "Harmless suture retry can complete later");
        s = NewSession(); s.Limb.skinHealth = 0; s.Limb.bleedAmount = 100;
        check(s.Slip() && s.Limb.skinHealth == 0 && s.Limb.bleedAmount == 100, "Slip respects wound boundaries");
    }
}
