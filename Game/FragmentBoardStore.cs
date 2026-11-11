using System;
using System.Runtime.CompilerServices;
using CUCoreLib.Saving;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;
namespace CasualtiesSurgery.Game
{
    internal sealed class FragmentBoardStore : ILimbSaveProvider
    {
        static readonly ConditionalWeakTable<Limb,FragmentBoard> boards=new ConditionalWeakTable<Limb,FragmentBoard>();
        public static FragmentBoard Get(Limb limb)
        {
            var board=boards.GetValue(limb, key=>new FragmentBoard(Guid.NewGuid().GetHashCode(),key.shrapnel));
            board.Reconcile(limb.shrapnel); return board;
        }
        public int GetVersion() => 1;
        public JToken Capture(Limb limb,int index)
        {
            if(!boards.TryGetValue(limb,out var board)) return JValue.CreateNull();
            board.Reconcile(limb.shrapnel); return JObject.FromObject(board);
        }
        public void Restore(Limb limb,int index,JToken data,int version,SaveRestoreContext context)
        {
            if(version!=1 || data==null || data.Type==JTokenType.Null) return;
            var board=data.ToObject<FragmentBoard>();
            if(board==null) return;
            board.Validate(); boards.Remove(limb); boards.Add(limb,board);
            // Counts may be restored by another provider later in the same restore pass.
            context.Defer(()=> { if(limb!=null) board.Reconcile(limb.shrapnel); });
        }
    }
}
