using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CasualtiesSurgery.Game;

// Narrow transport contract doubles, not Unity or clinical-state simulations. Method shapes
// match the installed Together/LiteNetLib signatures verified in LIBRARIES.md.
namespace Together
{
    public class ScavPlayer { }
    public enum Delivery { ReliableOrdered }
    public class TestWriter
    {
        public string Channel;
        readonly MemoryStream stream=new MemoryStream();
        public void Put(int length) {var bytes=BitConverter.GetBytes(length);stream.Write(bytes,0,bytes.Length);}
        public void Put(byte[] bytes)=>stream.Write(bytes,0,bytes.Length);
        public byte[] Bytes=>stream.ToArray();
    }
    public class TestReader
    {
        readonly byte[] data;int position;
        public TestReader(byte[] data){this.data=data;}
        public int AvailableBytes=>data.Length-position;
        public int GetInt(){int value=BitConverter.ToInt32(data,position);position+=4;return value;}
        public ArraySegment<byte> GetBytesSegment(int count){var result=new ArraySegment<byte>(data,position,count);position+=count;return result;}
    }
    public static class Multiplayer
    {
        public static readonly Dictionary<string,Action<ScavPlayer,TestReader>> Server=new Dictionary<string,Action<ScavPlayer,TestReader>>();
        public static readonly Dictionary<string,Action<TestReader>> Client=new Dictionary<string,Action<TestReader>>();
        public static TestWriter CreateNamedWriter(string name)=>new TestWriter {Channel=name};
        public static uint RegisterCustomServerReceiver(string name,Action<ScavPlayer,TestReader> receiver){Server.Add(name,receiver);return 1;}
        public static uint RegisterCustomClientReceiver(string name,Action<TestReader> receiver){Client.Add(name,receiver);return 2;}
    }
    public static class Net
    {
        public static bool IsServer=>true;
        public static ScavPlayer Sender=new ScavPlayer();
        public static void Client_Send(in Delivery delivery,in TestWriter writer)=>Multiplayer.Server[writer.Channel](Sender,new TestReader(writer.Bytes));
        public static void Server_SendTo(in Delivery delivery,in TestWriter writer,in ScavPlayer player)=>Multiplayer.Client[writer.Channel](new TestReader(writer.Bytes));
    }
}
static class TransportChecks
{
    public static void Run(Action<bool,string> check)
    {
        object sender=null;int requests=0,replies=0;string last=null;
        var transport=TogetherSurgeryTransport.InstallAssembly(typeof(TransportChecks).Assembly,(peer,text)=>{sender=peer;last=text;requests++;},text=>replies++,_=>{});
        check(transport.SendRequest("{\"test\":1}") && requests==0,"Transport queues callback without executing clinical code on receiver thread");
        transport.Pump();
        check(requests==1 && ReferenceEquals(sender,Together.Net.Sender) && last=="{\"test\":1}","Transport preserves authenticated peer separately from UTF-8 payload");
        check(transport.SendReply(sender,"{}"),"Transport sends host reply through resolved peer overload");transport.Pump();
        check(replies==1,"Client reply delivered by main-thread pump");
        check(!transport.SendRequest(new string('x',65537)),"Transport rejects oversized outgoing wire frame");
        var receive=Together.Multiplayer.Server["casualtycenter.surgery.request.v1"];
        receive(Together.Net.Sender,new Together.TestReader(BitConverter.GetBytes(int.MaxValue)));transport.Pump();
        check(requests==1,"Transport rejects forged length before allocating payload");
        receive(Together.Net.Sender,new Together.TestReader(new byte[]{1,0,0,0,255}));transport.Pump();
        check(requests==1,"Transport rejects malformed UTF-8");
        for(int i=0;i<140;i++)transport.SendRequest("{}");transport.Pump();
        check(requests==129,"Transport queue is bounded under packet flooding");
        transport.Dispose();receive(Together.Net.Sender,new Together.TestReader(new byte[]{2,0,0,0,123,125}));
        check(requests==129,"Disposed receiver callbacks remain inert");
        var replacement=TogetherSurgeryTransport.InstallAssembly(typeof(TransportChecks).Assembly,(p,t)=>requests++,t=>{},_=>{});
        replacement.SendRequest("{}");replacement.Pump();
        check(requests==130 && Together.Multiplayer.Server.Count==1,"Transport reinstalls without duplicate channel registration");replacement.Dispose();

        double now=0;var connection=Guid.NewGuid();var body=new Body();var limb=new Limb {body=body,skinHealth=80,broken=true,boneHealTimer=100};body.limbs=new[]{limb};
        var router=new SurgeryHostRouter(c=>c==connection?body:null,()=>now);
        SurgeryClientSession client=null;TogetherSurgeryTransport integrated=null;
        integrated=TogetherSurgeryTransport.InstallAssembly(typeof(TransportChecks).Assembly,(peer,text)=>
        {
            if(ReferenceEquals(peer,Together.Net.Sender))integrated.SendReply(peer,router.Handle(connection,text));
        },text=>client.Receive(text),_=>{});
        client=new SurgeryClientSession(integrated.SendRequest,()=>now);
        client.Begin(CasualtiesSurgery.Procedures.ProcedureKind.Fracture,0);integrated.Pump();
        check(client.Active && !client.Busy,"Framed transport handshake reaches production router and returns snapshot");
        client.Command("incision.cut",new Newtonsoft.Json.Linq.JObject { ["section"]=0,["x"]=0,["y"]=120 });
        check(limb.skinHealth==80,"Framed request cannot mutate patient before game-thread pump");
        integrated.Pump();
        check(!client.Busy && limb.skinHealth<80 && client.Snapshot.Revision==1,"Framed command commits once and returns authoritative state through client session");
        integrated.Dispose();
    }
}
