using System;
using System.Collections.Generic;

namespace CasualtiesSurgery.Procedures
{
    public interface ISurgeryCommandTarget
    {
        OperationKey Key { get; }
        bool CanAccess(Guid authenticatedConnection);
        CommandStatus Apply(Guid authenticatedConnection, SurgeryCommand command, double hostTime);
    }
    public interface ISurgeryStateTarget : ISurgeryCommandTarget
    {
        string CaptureState();
        bool Advance(float seconds,bool operated);
        void ResetInput();
    }
    public interface ISurgeryLeaseTarget { void BeginLease(Guid token,double hostTime); }
    public interface ISurgeryFeedbackTarget { TreatmentFeedback Feedback { get; } }
    public interface ISurgeryViewSettings { float ViewScale { get; } }
    public sealed class SurgerySnapshot
    {
        public OperationKey Operation { get; }
        public long Revision { get; }
        public long Sample { get; }
        public double HostTime { get; }
        public string State { get; }
        public float ViewScale { get; }
        public SurgerySnapshot(OperationKey operation,long revision,long sample,double hostTime,string state,float viewScale=.4f)
        { Operation=operation;Revision=revision;Sample=sample;HostTime=hostTime;State=state;ViewScale=viewScale; }
    }

    // Host-owned registry. No client can register targets or supply the clock. Invoke only
    // from the game thread after resolving a connection from the transport sender.
    public sealed class SurgeryDispatcher
    {
        sealed class Entry
        {
            public ISurgeryCommandTarget Target;
            public PatientLease Lease;
            public SurgeryCommandJournal Journal;
            public long Revision;
            public bool Faulted;
            public double LastTick;
            public long Sample;
        }
        readonly Dictionary<OperationKey,Entry> entries = new Dictionary<OperationKey,Entry>();
        readonly PatientLeases leases;
        readonly Guid world;
        readonly Func<double> clock;
        public SurgeryDispatcher(Guid world, Func<double> hostClock)
        { this.world=world; clock=hostClock ?? throw new ArgumentNullException(nameof(hostClock)); leases=new PatientLeases(world); }
        public void Register(ISurgeryCommandTarget target)
        {
            if(target==null || target.Key==null || target.Key.World!=world || entries.ContainsKey(target.Key))
                throw new ArgumentException("Invalid or duplicate authoritative target.");
            entries.Add(target.Key,new Entry { Target=target,LastTick=clock() });
        }
        public PatientLease Acquire(OperationKey key, Guid connection)
        {
            if(key==null || !entries.TryGetValue(key,out var entry) || entry.Faulted || !entry.Target.CanAccess(connection))return null;
            var lease=leases.Acquire(key,connection,clock(),15);
            if(lease==null)return null;
            entry.Journal?.Invalidate();
            (entry.Target as ISurgeryStateTarget)?.ResetInput();
            (entry.Target as ISurgeryLeaseTarget)?.BeginLease(lease.Token,clock());
            entry.Lease=lease;
            entry.Journal=new SurgeryCommandJournal(key,lease.Token,connection,entry.Revision);
            return lease;
        }
        public CommandReceipt Execute(Guid authenticatedConnection, SurgeryCommand command)
        {
            if(command==null || !command.Valid)return new CommandReceipt(CommandStatus.Invalid,0,1);
            if(!entries.TryGetValue(command.Operation,out var entry) || entry.Lease==null)
                return new CommandReceipt(CommandStatus.Unauthorized,0,1);
            double now=clock();
            if(!leases.Validate(command.Operation,authenticatedConnection,command.Lease,now) ||
                !entry.Target.CanAccess(authenticatedConnection))return new CommandReceipt(CommandStatus.Unauthorized,entry.Revision,1);
            var result=entry.Journal.Execute(authenticatedConnection,command,c=>entry.Target.Apply(authenticatedConnection,c,now),
                ()=>(entry.Target as ISurgeryFeedbackTarget)?.Feedback);
            entry.Revision=entry.Journal.Revision;
            if(result.Status==CommandStatus.Faulted)entry.Faulted=true;
            return result;
        }
        public bool Renew(OperationKey key, Guid connection, Guid token) => key!=null && entries.TryGetValue(key,out var entry) &&
            !entry.Faulted && entry.Target.CanAccess(connection) && leases.Renew(key,connection,token,clock(),15);
        public void Release(OperationKey key, Guid connection, Guid token)
        {
            if(!leases.Release(key,connection,token))return;
            if(entries.TryGetValue(key,out var entry)) { entry.Journal?.Invalidate(); entry.Lease=null; (entry.Target as ISurgeryStateTarget)?.ResetInput(); }
        }
        public void Disconnect(Guid connection)
        {
            leases.Disconnect(connection);
            foreach(var entry in entries.Values)
                if(entry.Lease?.Connection==connection) { entry.Journal?.Invalidate(); entry.Lease=null; (entry.Target as ISurgeryStateTarget)?.ResetInput(); }
        }
        public SurgerySnapshot Snapshot(OperationKey key,Guid connection)
        {
            if(key==null || !entries.TryGetValue(key,out var entry) || entry.Faulted || !entry.Target.CanAccess(connection) ||
                !(entry.Target is ISurgeryStateTarget target))return null;
            return new SurgerySnapshot(key,entry.Revision,++entry.Sample,clock(),target.CaptureState(),(target as ISurgeryViewSettings)?.ViewScale ?? .4f);
        }
        public void Tick()
        {
            double now=clock();
            if(double.IsNaN(now) || double.IsInfinity(now) || now<0)return;
            foreach(var pair in entries)
            {
                var entry=pair.Value;
                if(entry.Faulted || !(entry.Target is ISurgeryStateTarget target) || now<entry.LastTick)continue;
                float seconds=(float)Math.Min(float.MaxValue,now-entry.LastTick);entry.LastTick=now;
                bool operated=entry.Lease!=null && leases.Validate(pair.Key,entry.Lease.Connection,entry.Lease.Token,now) && entry.Target.CanAccess(entry.Lease.Connection);
                if(!operated && entry.Lease!=null)
                {
                    leases.Release(pair.Key,entry.Lease.Connection,entry.Lease.Token);
                    entry.Journal?.Invalidate();entry.Lease=null;target.ResetInput();
                }
                try { if(target.Advance(seconds,operated))StateChanged(pair.Key); }
                catch { entry.Faulted=true;entry.Journal?.Invalidate(); }
            }
        }
        public bool HasLease(PatientLease lease)=>lease!=null && leases.Validate(lease.Operation,lease.Connection,lease.Token,clock());
        public void StateChanged(OperationKey key)
        {
            var entry=entries[key];
            if(entry.Faulted)throw new InvalidOperationException("Faulted target requires reconciliation and a new episode.");
            if(entry.Journal!=null && entry.Lease!=null) { entry.Journal.AdvanceRevision(); entry.Revision=entry.Journal.Revision; }
            else if(entry.Revision<long.MaxValue-1)entry.Revision++;
            else { entry.Faulted=true; throw new InvalidOperationException("Revision exhausted."); }
        }
        public void Remove(OperationKey key)
        {
            if(key==null || !entries.TryGetValue(key,out var entry))return;
            if(entry.Lease!=null)leases.Release(key,entry.Lease.Connection,entry.Lease.Token);
            entry.Journal?.Invalidate(); entries.Remove(key);
        }
    }
}
