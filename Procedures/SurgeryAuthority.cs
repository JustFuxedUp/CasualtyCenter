using System;
using System.Collections.Generic;

namespace CasualtiesSurgery.Procedures
{
    public enum ProcedureKind { Fragment, Fracture, InternalBleeding }
    public enum CommandStatus { Applied, NoChange, Rejected, Duplicate, StaleRevision, OutOfOrder, Expired, Unauthorized, Invalid, Faulted }

    // Network identities are separate from limb-save field names and local Unity references.
    public sealed class OperationKey : IEquatable<OperationKey>
    {
        public Guid World { get; }
        public Guid Episode { get; }
        public string Patient { get; }
        public int Limb { get; }
        public ProcedureKind Procedure { get; }
        public OperationKey(Guid world, Guid episode, string patient, int limb, ProcedureKind procedure)
        {
            if (world == Guid.Empty || episode == Guid.Empty || string.IsNullOrWhiteSpace(patient) || patient.Length > 128 ||
                limb < 0 || limb > 255 || !Enum.IsDefined(typeof(ProcedureKind),procedure)) throw new ArgumentException("Invalid operation key.");
            World=world; Episode=episode; Patient=patient; Limb=limb; Procedure=procedure;
        }
        public bool Equals(OperationKey other) => other != null && World == other.World && Episode == other.Episode &&
            Patient == other.Patient && Limb == other.Limb && Procedure == other.Procedure;
        public override bool Equals(object obj) => Equals(obj as OperationKey);
        public override int GetHashCode() => World.GetHashCode() ^ Episode.GetHashCode() ^ Patient.GetHashCode() ^ Limb ^ (int)Procedure;
    }

    // Payload is bounded serialized action data. A registered procedure handler must validate
    // its schema and gesture evidence; the journal alone does not validate gameplay.
    public sealed class SurgeryCommand
    {
        public const int ProtocolVersion = 1, MaxPayloadCharacters = 8192;
        public int Version { get; }
        public OperationKey Operation { get; }
        public Guid Lease { get; }
        public long Sequence { get; }
        public long Revision { get; }
        public string Action { get; }
        public string Payload { get; }
        public SurgeryCommand(int version, OperationKey operation, Guid lease, long sequence, long revision, string action, string payload)
        { Version=version; Operation=operation; Lease=lease; Sequence=sequence; Revision=revision; Action=action; Payload=payload; }
        public bool Valid => Version == ProtocolVersion && Operation != null && Lease != Guid.Empty && Sequence > 0 && Sequence < long.MaxValue &&
            Revision >= 0 && Revision < long.MaxValue && !string.IsNullOrWhiteSpace(Action) && Action.Length <= 64 &&
            Payload != null && Payload.Length <= MaxPayloadCharacters;
        public bool SameRequest(SurgeryCommand other) => other != null && Version == other.Version && Operation.Equals(other.Operation) &&
            Lease == other.Lease && Sequence == other.Sequence && Revision == other.Revision && Action == other.Action && Payload == other.Payload;
    }

    public enum TreatmentRating { Good, Scored, Mistake }
    // A host-computed presentation result. Target is a stable wound/site/bone index (-1
    // for the outer incision); slot identifies an individual pool. No health is replicated here.
    public sealed class TreatmentFeedback
    {
        public TreatmentRating Rating { get; }
        public int Quality { get; }
        public int Target { get; }
        public int Slot { get; }
        public TreatmentFeedback(TreatmentRating rating,int quality=0,int target=-1,int slot=-1)
        {
            if(!Enum.IsDefined(typeof(TreatmentRating),rating) || quality<0 || quality>100 || target< -1 || slot< -1 || slot>1)
                throw new ArgumentException("Invalid treatment feedback.");
            Rating=rating;Quality=quality;Target=target;Slot=slot;
        }
    }
    public sealed class CommandReceipt
    {
        public CommandStatus Status { get; }
        public CommandStatus OriginalStatus { get; }
        public long Revision { get; }
        public long NextSequence { get; }
        public TreatmentFeedback Feedback { get; }
        public CommandReceipt(CommandStatus status, long revision, long nextSequence, CommandStatus? originalStatus = null,TreatmentFeedback feedback=null)
        { Status=status; OriginalStatus=originalStatus ?? status; Revision=revision; NextSequence=nextSequence;Feedback=feedback; }
    }

    // One journal per lease. Call on the authoritative game thread only. Sender connection
    // identity comes from transport, never from SurgeryCommand. No network registration here.
    public sealed class SurgeryCommandJournal
    {
        const int Window = 64;
        readonly OperationKey operation;
        readonly Guid lease, connection;
        readonly Dictionary<long, Tuple<SurgeryCommand,CommandReceipt>> receipts = new Dictionary<long, Tuple<SurgeryCommand,CommandReceipt>>();
        long nextSequence=1;
        bool faulted, executing;
        public long Revision { get; private set; }
        public SurgeryCommandJournal(OperationKey operation, Guid lease, Guid authenticatedConnection, long revision=0)
        {
            if (operation == null || lease == Guid.Empty || authenticatedConnection == Guid.Empty || revision < 0 || revision == long.MaxValue)
                throw new ArgumentException("Invalid journal identity.");
            this.operation=operation; this.lease=lease; connection=authenticatedConnection; Revision=revision;
        }
        CommandReceipt Result(CommandStatus status) => new CommandReceipt(status,Revision,nextSequence);

        public CommandReceipt Execute(Guid authenticatedConnection, SurgeryCommand command, Func<SurgeryCommand,CommandStatus> apply,Func<TreatmentFeedback> feedback=null,Func<SurgeryCommand,bool> canRebase=null)
        {
            if (authenticatedConnection != connection) return Result(CommandStatus.Unauthorized);
            if (command == null || !command.Valid || apply == null) return Result(CommandStatus.Invalid);
            if (!operation.Equals(command.Operation) || command.Lease != lease) return Result(CommandStatus.Unauthorized);
            if (faulted || executing) return Result(CommandStatus.Faulted);
            if (receipts.TryGetValue(command.Sequence,out var previous))
                return command.SameRequest(previous.Item1)
                    ? new CommandReceipt(CommandStatus.Duplicate,Revision,nextSequence,previous.Item2.OriginalStatus,previous.Item2.Feedback)
                    : Result(CommandStatus.Invalid);
            if (command.Sequence < nextSequence) return Result(CommandStatus.Expired);
            if (command.Sequence != nextSequence) return Result(CommandStatus.OutOfOrder);
            if (command.Revision != Revision && !(command.Revision<Revision && canRebase!=null && canRebase(command))) return Result(CommandStatus.StaleRevision);
            if (Revision == long.MaxValue-1) { faulted=true; return Result(CommandStatus.Faulted); }
            CommandStatus outcome;
            TreatmentFeedback presentation=null;
            executing=true;
            try
            {
                outcome=apply(command);
                if (outcome != CommandStatus.Applied && outcome != CommandStatus.NoChange && outcome != CommandStatus.Rejected)
                    throw new InvalidOperationException("Invalid application result.");
                if(outcome==CommandStatus.Applied)presentation=feedback?.Invoke();
            }
            catch
            {
                // A handler might have partially written clinical state. Never automatically
                // retry it; quarantine this lease and require authoritative reconciliation.
                faulted=true;
                return Result(CommandStatus.Faulted);
            }
            finally { executing=false; }
            if (outcome == CommandStatus.Applied) Revision++;
            nextSequence++;
            var receipt=new CommandReceipt(outcome,Revision,nextSequence,feedback:presentation);
            receipts.Add(command.Sequence,Tuple.Create(command,receipt));
            receipts.Remove(command.Sequence-Window);
            return receipt;
        }

        // Host timer/external reconciliation changes invalidate an in-flight expected revision.
        public void AdvanceRevision()
        {
            if (executing || faulted || Revision >= long.MaxValue-1) throw new InvalidOperationException("Journal cannot advance.");
            Revision++;
        }
        public void SynchronizeRevision(long revision)
        {
            if(executing || faulted || revision<Revision || revision>=long.MaxValue)throw new InvalidOperationException("Invalid shared revision.");
            Revision=revision;
        }
        public void Invalidate() { faulted=true; }
    }

    public sealed class PatientLease
    {
        public OperationKey Operation { get; }
        public Guid Connection { get; }
        public Guid Token { get; } = Guid.NewGuid();
        public double ExpiresAt { get; internal set; }
        internal PatientLease(OperationKey operation, Guid connection, double expiresAt)
        { Operation=operation; Connection=connection; ExpiresAt=expiresAt; }
    }

    // Independent limbs may proceed together. Internal bleeding conflicts with other
    // operations on the patient; explicit joins share only the exact same episode.
    public sealed class PatientLeases
    {
        readonly Guid world;
        readonly Dictionary<Guid,PatientLease> leases=new Dictionary<Guid,PatientLease>();
        double lastTime;
        public PatientLeases(Guid world) {if(world==Guid.Empty)throw new ArgumentException("Missing world epoch.");this.world=world;}
        bool Time(double now) {if(double.IsNaN(now)||double.IsInfinity(now)||now<lastTime)return false;lastTime=now;return true;}
        static bool Conflicts(OperationKey a,OperationKey b)=>a.Patient==b.Patient &&
            (a.Limb==b.Limb || a.Procedure==ProcedureKind.InternalBleeding || b.Procedure==ProcedureKind.InternalBleeding);
        public PatientLease Acquire(OperationKey operation,Guid connection,double now,double lifetime,bool shared=false)
        {
            if(operation==null || operation.World!=world || connection==Guid.Empty || !Time(now) || !(lifetime>0 && lifetime<=60))return null;
            var expired=new List<Guid>();
            foreach(var pair in leases)
            {
                var current=pair.Value;
                if(current.ExpiresAt<=now)expired.Add(pair.Key);
                else if(Conflicts(operation,current.Operation) && !(shared && operation.Equals(current.Operation) && current.Connection!=connection))return null;
            }
            foreach(var token in expired)leases.Remove(token);
            var lease=new PatientLease(operation,connection,now+lifetime);leases.Add(lease.Token,lease);return lease;
        }
        public bool Validate(OperationKey operation,Guid connection,Guid token,double now)=>operation!=null && operation.World==world && Time(now) &&
            leases.TryGetValue(token,out var lease) && lease.Operation.Equals(operation) && lease.Connection==connection && lease.ExpiresAt>now;
        public bool Renew(OperationKey operation,Guid connection,Guid token,double now,double lifetime)
        {if(!(lifetime>0 && lifetime<=60)||!Validate(operation,connection,token,now))return false;leases[token].ExpiresAt=now+lifetime;return true;}
        public bool Release(OperationKey operation,Guid connection,Guid token)
        {
            if(operation==null || !leases.TryGetValue(token,out var lease) || !lease.Operation.Equals(operation) || lease.Connection!=connection)return false;
            return leases.Remove(token);
        }
        public void Disconnect(Guid connection)
        {
            var remove=new List<Guid>();foreach(var pair in leases)if(pair.Value.Connection==connection)remove.Add(pair.Key);
            foreach(var token in remove)leases.Remove(token);
        }
    }
}
