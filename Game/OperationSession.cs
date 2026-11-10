using System;
using CUCoreLib.Networking;
using CasualtiesSurgery.Procedures;
using UnityEngine;

namespace CasualtiesSurgery.Game
{
    // Owns one self-treatment. No cached health values are written back.
    public sealed class OperationSession
    {
        public readonly Body Patient;
        public readonly Limb Limb;
        public readonly FragmentProcedure Procedure = new FragmentProcedure();
        readonly Minigame owner;
        readonly int fragmentsAtStart;
        readonly Vector3 origin;
        readonly int scene;
        readonly float skinRecovery, bleedingReduction;
        public int Stitches { get; private set; }
        public OperationSession(Body patient, Limb limb, float skinRecovery, float bleedingReduction, Minigame owner = null)
        {
            this.owner = owner;
            Patient = patient; Limb = limb; fragmentsAtStart = limb.shrapnel;
            origin = patient.transform.position; scene = patient.gameObject.scene.handle;
            this.skinRecovery = skinRecovery; this.bleedingReduction = bleedingReduction;
        }
        public static string Ineligible(Body patient, Limb limb, Minigame allowedMinigame = null)
        {
            if (MultiplayerApi.IsRunning) return "Surgery MVP is singleplayer only.";
            if (patient == null || PlayerCamera.main == null || PlayerCamera.main.body != patient)
                return "No local patient.";
            if (!patient.alive || !patient.conscious) return "You must be alive and conscious.";
            if (limb == null || limb.body != patient || patient.limbs == null || Array.IndexOf(patient.limbs, limb) < 0)
                return "Limb no longer belongs to this patient.";
            if (limb.dismembered) return "Detached limbs cannot be treated.";
            if (limb.prostheticInfo.hasProsthetic) return "This procedure treats organic skin only.";
            if (limb.shrapnel <= 0) return "No fragments in this limb.";
            if (float.IsNaN(limb.skinHealth) || float.IsInfinity(limb.skinHealth) || limb.skinHealth < 0 || limb.skinHealth > 100 ||
                float.IsNaN(limb.bleedAmount) || float.IsInfinity(limb.bleedAmount) || limb.bleedAmount < 0)
                return "Invalid wound values; treatment stopped.";
            if (MinigameBase.main != null && MinigameBase.main.currentMinigame != null &&
                !ReferenceEquals(MinigameBase.main.currentMinigame, allowedMinigame))
                return "Finish the current game minigame first.";
            return null;
        }
        public string InvalidReason()
        {
            if (owner != null && (MinigameBase.main == null || !ReferenceEquals(MinigameBase.main.currentMinigame, owner)))
                return "This operation no longer owns the native minigame.";
            string reason = Ineligible(Patient, Limb, owner);
            if (reason != null) return reason;
            if (Patient.gameObject.scene.handle != scene || Vector3.Distance(Patient.transform.position, origin) > 1.5f)
                return "Patient moved too far or changed scene.";
            if (Limb.shrapnel != fragmentsAtStart) return "Fragment count changed; start a new operation.";
            return null;
        }
        public bool Extract() => InvalidReason() == null && Procedure.RecordSuccessfulExtraction();
        public bool Stitch(out bool completed)
        {
            completed = false;
            if (InvalidReason() != null || Procedure.Stage != FragmentStage.Suture || Stitches >= 3) return false;
            if (Stitches < 2) { Stitches++; return true; }
            if (!Procedure.TryCompleteSuture(Limb.shrapnel, Limb.skinHealth, Limb.bleedAmount,
                skinRecovery, bleedingReduction, out var outcome)) return false;
            // One main-thread transaction, after the last validation. No yielding or callbacks.
            Limb.shrapnel = outcome.Fragments;
            Limb.skinHealth = outcome.SkinHealth;
            Limb.bleedAmount = outcome.Bleeding;
            Stitches++;
            completed = true;
            return true;
        }
        public bool Slip()
        {
            if (InvalidReason() != null || Procedure.Stage == FragmentStage.Completed || Procedure.Stage == FragmentStage.Cancelled) return false;
            Limb.skinHealth = Mathf.Max(0, Limb.skinHealth - 1f);
            Limb.bleedAmount = Mathf.Min(100 - Limb.skinHealth, Limb.bleedAmount + 0.2f);
            return true;
        }
        public void Cancel() { Procedure.Cancel(); }
    }
}
