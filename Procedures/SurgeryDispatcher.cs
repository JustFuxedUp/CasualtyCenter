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
    public interface ISurgeryResourceTarget
    {
        string CommandResource(SurgeryCommand command);
        string HeldResource { get; }
        int SharedPhase { get; }
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
        public int Participants { get; }
        public string[] BusyResources { get; }
        public SurgeryPeerVisual[] Visuals {get;}
        public SurgerySnapshot(OperationKey operation,long revision,long sample,double hostTime,string state,float viewScale=.4f,int participants=1,string[] busyResources=null,SurgeryPeerVisual[] visuals=null)
        { Operation=operation;Revision=revision;Sample=sample;HostTime=hostTime;State=state;ViewScale=viewScale;Participants=participants;BusyResources=busyResources==null?new string[0]:(string[])busyResources.Clone();Visuals=visuals??new SurgeryPeerVisual[0]; }
    }

    // Host-owned registry. No client can register targets or supply the clock. Invoke only
    // from the game thread after resolving a connection from the transport sender.
    public sealed class SurgeryDispatcher
    {
        sealed class Member
        {
            public PatientLease Lease;
            public SurgeryCommandJournal Journal;
            public ISurgeryCommandTarget Target;
            public SurgeryVisualState Visual;public double VisualAt;public byte Red,Green,Blue;
        }
        sealed class Entry
        {
            public ISurgeryCommandTarget Target;
            public Func<Guid,ISurgeryCommandTarget> Factory;
            public readonly Dictionary<Guid,Member> Members=new Dictionary<Guid,Member>();
            public long Revision,Sample,GlobalRevision;
            public readonly Dictionary<string,long> ResourceRevisions=new Dictionary<string,long>();
            public bool Faulted;
            public double LastTick;
        }
        readonly Dictionary<OperationKey,Entry> entries=new Dictionary<OperationKey,Entry>();
        readonly PatientLeases leases;
        readonly Guid world;
        readonly Func<double> clock;
        public SurgeryDispatcher(Guid world,Func<double> hostClock)
        {this.world=world;clock=hostClock??throw new ArgumentNullException(nameof(hostClock));leases=new PatientLeases(world);}
        public void Register(ISurgeryCommandTarget target,Func<Guid,ISurgeryCommandTarget> participantFactory=null)
        {
            if(target==null || target.Key==null || target.Key.World!=world || entries.ContainsKey(target.Key))throw new ArgumentException("Invalid or duplicate authoritative target.");
            entries.Add(target.Key,new Entry {Target=target,Factory=participantFactory,LastTick=clock()});
        }
        public PatientLease Acquire(OperationKey key,Guid connection)
        {
            if(key==null || !entries.TryGetValue(key,out var entry) || entry.Faulted || !entry.Target.CanAccess(connection))return null;
            Prune(key,entry,clock());
            if(entry.Members.Count>=8 || entry.Members.ContainsKey(connection))return null;
            var lease=leases.Acquire(key,connection,clock(),15,entry.Factory!=null);
            if(lease==null)return null;
            var target=entry.Factory==null?entry.Target:entry.Factory(connection);
            if(target==null || !target.Key.Equals(key) || !target.CanAccess(connection)) {leases.Release(key,connection,lease.Token);return null;}
            if(entry.Members.Count==0)(entry.Target as ISurgeryLeaseTarget)?.BeginLease(lease.Token,clock());
            (target as ISurgeryStateTarget)?.ResetInput();(target as ISurgeryLeaseTarget)?.BeginLease(lease.Token,clock());
            entry.Members.Add(connection,new Member {Lease=lease,Target=target,Journal=new SurgeryCommandJournal(key,lease.Token,connection,entry.Revision)});
            return lease;
        }
        static bool Conflicts(string a,string b)=>a!=null && b!=null && (a=="*" || b=="*" || a==b);
        public CommandReceipt Execute(Guid connection,SurgeryCommand command)
        {
            if(command==null || !command.Valid)return new CommandReceipt(CommandStatus.Invalid,0,1);
            if(!entries.TryGetValue(command.Operation,out var entry))return new CommandReceipt(CommandStatus.Unauthorized,0,1);
            double now=clock();Prune(command.Operation,entry,now);
            if(entry.Faulted || !entry.Members.TryGetValue(connection,out var member) || !leases.Validate(command.Operation,connection,command.Lease,now) || !member.Target.CanAccess(connection))
                return new CommandReceipt(CommandStatus.Unauthorized,entry.Revision,1);
            member.Journal.SynchronizeRevision(entry.Revision);
            var resources=member.Target as ISurgeryResourceTarget;
            string changedResource=resources?.CommandResource(command);
            int phase=resources?.SharedPhase??0;
            var result=member.Journal.Execute(connection,command,c=>{
                string resource=(member.Target as ISurgeryResourceTarget)?.CommandResource(c);
                foreach(var other in entry.Members)
                    if(other.Key!=connection && Conflicts(resource,(other.Value.Target as ISurgeryResourceTarget)?.HeldResource))return CommandStatus.Rejected;
                return member.Target.Apply(connection,c,now);
            },()=>(member.Target as ISurgeryFeedbackTarget)?.Feedback,c=>entry.Factory!=null &&
                (c.Action=="gesture.cancel" || (changedResource!=null && changedResource!="*" && entry.GlobalRevision<=c.Revision &&
                (!entry.ResourceRevisions.TryGetValue(changedResource,out long changedAt) || changedAt<=c.Revision))));
            entry.Revision=member.Journal.Revision;
            if(result.Status==CommandStatus.Applied)
            {
                if(changedResource==null || changedResource=="*" || resources.SharedPhase!=phase)entry.GlobalRevision=entry.Revision;
                else entry.ResourceRevisions[changedResource]=entry.Revision;
            }
            if(result.Status==CommandStatus.Faulted)entry.Faulted=true;
            return result;
        }
        public bool Renew(OperationKey key,Guid connection,Guid token)=>key!=null && entries.TryGetValue(key,out var entry) && !entry.Faulted &&
            entry.Members.TryGetValue(connection,out var member) && member.Target.CanAccess(connection) && leases.Renew(key,connection,token,clock(),15);
        void Drop(OperationKey key,Entry entry,Guid connection)
        {
            if(!entry.Members.TryGetValue(connection,out var member))return;
            leases.Release(key,connection,member.Lease.Token);member.Journal.Invalidate();(member.Target as ISurgeryStateTarget)?.ResetInput();entry.Members.Remove(connection);
        }
        void Prune(OperationKey key,Entry entry,double now)
        {
            foreach(var pair in new List<KeyValuePair<Guid,Member>>(entry.Members))
                if(!leases.Validate(key,pair.Key,pair.Value.Lease.Token,now) || !pair.Value.Target.CanAccess(pair.Key))Drop(key,entry,pair.Key);
        }
        public void Release(OperationKey key,Guid connection,Guid token)
        {
            if(key!=null && entries.TryGetValue(key,out var entry) && entry.Members.TryGetValue(connection,out var member) && member.Lease.Token==token)Drop(key,entry,connection);
        }
        public void Disconnect(Guid connection)
        {foreach(var pair in entries)Drop(pair.Key,pair.Value,connection);leases.Disconnect(connection);}
        public bool PublishVisual(OperationKey key,Guid connection,Guid token,SurgeryVisualState visual,byte red,byte green,byte blue)
        {
            double now=clock();
            if(visual==null || !visual.Valid || key==null || !entries.TryGetValue(key,out var entry) || entry.Faulted ||
                !entry.Members.TryGetValue(connection,out var member) || !leases.Validate(key,connection,token,now) || !member.Target.CanAccess(connection) ||
                (member.Visual!=null && (visual.Sequence<=member.Visual.Sequence || now-member.VisualAt<.05)))return false;
            member.Visual=visual.Copy();member.VisualAt=now;member.Red=red;member.Green=green;member.Blue=blue;return true;
        }
        public SurgerySnapshot Snapshot(OperationKey key,Guid connection)
        {
            if(key==null || !entries.TryGetValue(key,out var entry) || entry.Faulted || !entry.Target.CanAccess(connection) || !(entry.Target is ISurgeryStateTarget target))return null;
            var busy=new List<string>();
            foreach(var member in entry.Members)
                if(member.Key!=connection && member.Value.Target is ISurgeryResourceTarget resource && resource.HeldResource!=null)busy.Add(resource.HeldResource);
            var visuals=new List<SurgeryPeerVisual>();
            bool participant=entry.Members.TryGetValue(connection,out var viewer) && leases.Validate(key,connection,viewer.Lease.Token,clock());
            foreach(var member in entry.Members)
                if(participant && member.Key!=connection && member.Value.Visual!=null && clock()-member.Value.VisualAt<=1.2 && leases.Validate(key,member.Key,member.Value.Lease.Token,clock()) && member.Value.Target.CanAccess(member.Key))
                    visuals.Add(new SurgeryPeerVisual(member.Key,member.Value.Red,member.Value.Green,member.Value.Blue,Math.Max(0,clock()-member.Value.VisualAt),member.Value.Visual));
            return new SurgerySnapshot(key,entry.Revision,++entry.Sample,clock(),target.CaptureState(),(target as ISurgeryViewSettings)?.ViewScale??.4f,entry.Members.Count,busy.ToArray(),visuals.ToArray());
        }
        public void Tick()
        {
            double now=clock();if(double.IsNaN(now)||double.IsInfinity(now)||now<0)return;
            foreach(var pair in entries)
            {
                var entry=pair.Value;if(entry.Faulted || !(entry.Target is ISurgeryStateTarget target) || now<entry.LastTick)continue;
                float seconds=(float)Math.Min(float.MaxValue,now-entry.LastTick);entry.LastTick=now;Prune(pair.Key,entry,now);
                try {if(target.Advance(seconds,entry.Members.Count>0))StateChanged(pair.Key);}
                catch {entry.Faulted=true;foreach(var member in entry.Members.Values)member.Journal.Invalidate();}
            }
        }
        public bool HasLease(PatientLease lease)=>lease!=null && entries.TryGetValue(lease.Operation,out var entry) && !entry.Faulted && leases.Validate(lease.Operation,lease.Connection,lease.Token,clock());
        public void StateChanged(OperationKey key)
        {
            var entry=entries[key];if(entry.Faulted || entry.Revision>=long.MaxValue-1){entry.Faulted=true;throw new InvalidOperationException("Target revision unavailable.");}
            entry.Revision++;entry.GlobalRevision=entry.Revision;
        }
        public void Remove(OperationKey key)
        {
            if(key==null || !entries.TryGetValue(key,out var entry))return;
            foreach(var connection in new List<Guid>(entry.Members.Keys))Drop(key,entry,connection);entries.Remove(key);
        }
    }
}
