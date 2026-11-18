using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace CasualtiesSurgery.Game
{
    // Optional Together 5.5 adapter. No compile-time Together dependency or automatic
    // registration: a runtime coordinator must explicitly install, pump and dispose it.
    internal sealed class TogetherSurgeryTransport : IDisposable
    {
        const string RequestChannel="casualtycenter.surgery.request.v1", ReplyChannel="casualtycenter.surgery.reply.v1";
        const int MaxBytes=65536,MaxQueued=128;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        static TogetherSurgeryTransport active;
        static bool serverRegistered,clientRegistered;
        readonly ConcurrentQueue<Tuple<object,string>> incoming=new ConcurrentQueue<Tuple<object,string>>();
        readonly MethodInfo writerFactory,putLength,putBytes,clientSend,serverSend,getLength,getBytes;
        readonly PropertyInfo available,isServer;
        readonly object reliable;
        readonly Action<object,string> receiveRequest;
        readonly Action<string> receiveReply,log;
        int queued;
        bool disposed;
        TogetherSurgeryTransport(Assembly assembly,Action<object,string> request,Action<string> reply,Action<string> log)
        {
            receiveRequest=request;receiveReply=reply;this.log=log;
            var mp=assembly.GetType("Together.Multiplayer",true);var net=assembly.GetType("Together.Net",true);var player=assembly.GetType("Together.ScavPlayer",true);
            var registerServer=mp.GetMethod("RegisterCustomServerReceiver",BindingFlags.Public|BindingFlags.Static);
            var registerClient=mp.GetMethod("RegisterCustomClientReceiver",BindingFlags.Public|BindingFlags.Static);
            writerFactory=mp.GetMethod("CreateNamedWriter",new[]{typeof(string)});
            var writer=writerFactory.ReturnType;
            var reader=registerClient.GetParameters()[1].ParameterType.GetGenericArguments()[0];
            putLength=writer.GetMethod("Put",new[]{typeof(int)});putBytes=writer.GetMethod("Put",new[]{typeof(byte[])});
            getLength=reader.GetMethod("GetInt",Type.EmptyTypes);getBytes=reader.GetMethod("GetBytesSegment",new[]{typeof(int)});available=reader.GetProperty("AvailableBytes");
            isServer=net.GetProperty("IsServer",BindingFlags.Public|BindingFlags.Static);
            clientSend=net.GetMethods().Single(m=>m.Name=="Client_Send" && m.GetParameters().Length==2 && Unwrap(m.GetParameters()[1].ParameterType)==writer);
            var delivery=Unwrap(clientSend.GetParameters()[0].ParameterType);reliable=Enum.Parse(delivery,"ReliableOrdered");
            serverSend=net.GetMethods().Single(m=>m.Name=="Server_SendTo" && m.GetParameters().Length==3 && Unwrap(m.GetParameters()[1].ParameterType)==writer && Unwrap(m.GetParameters()[2].ParameterType)==player);
            if(putLength==null || putBytes==null || getLength==null || getBytes==null || available==null || isServer==null)throw new NotSupportedException("Together packet methods missing.");
            if(!serverRegistered || !clientRegistered)
            {
                var flags=BindingFlags.NonPublic|BindingFlags.Static;
                var serverCallback=Delegate.CreateDelegate(registerServer.GetParameters()[1].ParameterType,typeof(TogetherSurgeryTransport).GetMethod(nameof(ServerReceive),flags));
                var clientCallback=Delegate.CreateDelegate(registerClient.GetParameters()[1].ParameterType,typeof(TogetherSurgeryTransport).GetMethod(nameof(ClientReceive),flags));
                if(!serverRegistered) {registerServer.Invoke(null,new object[]{RequestChannel,serverCallback});serverRegistered=true;}
                if(!clientRegistered) {registerClient.Invoke(null,new object[]{ReplyChannel,clientCallback});clientRegistered=true;}
            }
        }
        static Type Unwrap(Type type)=>type.IsByRef?type.GetElementType():type;
        public static TogetherSurgeryTransport Install(Action<object,string> request,Action<string> reply,Action<string> log)
        {
            if(active!=null)throw new InvalidOperationException("Surgery transport already installed.");
            var assembly=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="CasualtiesMP");
            if(assembly==null)return null;
            return InstallAssembly(assembly,request,reply,log);
        }
        internal static TogetherSurgeryTransport InstallAssembly(Assembly assembly,Action<object,string> request,Action<string> reply,Action<string> log)
        {
            if(active!=null)throw new InvalidOperationException("Surgery transport already installed.");
            active=new TogetherSurgeryTransport(assembly,request,reply,log);return active;
        }
        static void ServerReceive(object authenticatedPlayer,object reader) { active?.Read(authenticatedPlayer,reader); }
        static void ClientReceive(object reader) { active?.Read(null,reader); }
        void Read(object sender,object reader)
        {
            if(disposed)return;
            try
            {
                int remaining=(int)available.GetValue(reader,null);
                if(remaining<4 || remaining>MaxBytes+4)return;
                int length=(int)getLength.Invoke(reader,null);
                if(length<=0 || length>MaxBytes || length!=(int)available.GetValue(reader,null))return;
                var bytes=(ArraySegment<byte>)getBytes.Invoke(reader,new object[]{length});
                string text=Utf8.GetString(bytes.Array,bytes.Offset,bytes.Count);
                if(Interlocked.Increment(ref queued)>MaxQueued) {Interlocked.Decrement(ref queued);return;}
                incoming.Enqueue(Tuple.Create(sender,text));
            }
            catch(Exception e) when(e is ArgumentException || e is TargetInvocationException || e is InvalidCastException)
            { /* Malformed traffic is dropped without per-packet log spam. */ }
        }
        // Main-thread pump: no network callback is allowed to mutate Unity state directly.
        public void Pump()
        {
            if(disposed)return;
            for(int i=0;i<MaxQueued && incoming.TryDequeue(out var message);i++)
            {
                Interlocked.Decrement(ref queued);
                bool server=(bool)isServer.GetValue(null,null);
                try { if(message.Item1!=null && server)receiveRequest?.Invoke(message.Item1,message.Item2);else if(message.Item1==null)receiveReply?.Invoke(message.Item2); }
                catch(Exception e) {log?.Invoke("Surgery packet handling failed: "+e.GetType().Name);}
            }
        }
        public bool SendRequest(string text)=>Send(null,RequestChannel,text);
        public bool SendReply(object authenticatedPlayer,string text)=>authenticatedPlayer!=null && Send(authenticatedPlayer,ReplyChannel,text);
        bool Send(object player,string channel,string text)
        {
            if(disposed || text==null || text.Length>MaxBytes)return false;
            try
            {
                byte[] bytes=Utf8.GetBytes(text);if(bytes.Length==0 || bytes.Length>MaxBytes)return false;
                bool server=(bool)isServer.GetValue(null,null);if(player!=null && !server)return false;
                var writer=writerFactory.Invoke(null,new object[]{channel});putLength.Invoke(writer,new object[]{bytes.Length});putBytes.Invoke(writer,new object[]{bytes});
                if(player==null)clientSend.Invoke(null,new[]{reliable,writer});else serverSend.Invoke(null,new[]{reliable,writer,player});
                return true;
            }
            catch(Exception e) when(e is ArgumentException || e is TargetInvocationException) {log?.Invoke("Surgery packet send failed: "+e.GetType().Name);return false;}
        }
        public void Dispose()
        {
            disposed=true;if(ReferenceEquals(active,this))active=null;
            while(incoming.TryDequeue(out _))Interlocked.Decrement(ref queued);
            // Together exposes registration but no public unregister. Static callbacks remain
            // inert and can route to a replacement instance without duplicate registration.
        }
    }
}
