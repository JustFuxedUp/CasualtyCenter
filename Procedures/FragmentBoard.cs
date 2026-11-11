using System;
using System.Collections.Generic;
using System.Linq;
namespace CasualtiesSurgery.Procedures
{
    public sealed class FragmentSite
    {
        public int Id;
        public bool Extracted, Completed;
    }
    public sealed class FragmentBoard
    {
        public int Seed, NextId;
        public List<FragmentSite> Sites = new List<FragmentSite>();
        public int Remaining => Sites.Count(s => !s.Completed);
        public FragmentBoard() { }
        public FragmentBoard(int seed, int count) { Seed=seed; Reconcile(count); }
        public void Validate()
        {
            if (Sites==null || Sites.Count>10000 || NextId<0 || NextId>10000 ||
                Sites.Any(s=>s==null || s.Id<0 || s.Id>=NextId) || Sites.Select(s=>s.Id).Distinct().Count()!=Sites.Count)
                throw new ArgumentException("Invalid saved surgery board.");
        }
        public void Reconcile(int count)
        {
            if (count<0 || count>10000) throw new ArgumentOutOfRangeException(nameof(count));
            int delta=count-Remaining;
            while(delta-->0)
            {
                if(NextId>=10000) throw new InvalidOperationException("Surgery board ID limit reached.");
                Sites.Add(new FragmentSite { Id=NextId++ });
            }
            // Vanilla stores only a count. Resolve external removals in a stable order.
            int remove=Remaining-count;
            foreach(var site in Sites.Where(s=>!s.Completed).OrderBy(s=>s.Extracted).ThenByDescending(s=>s.Id))
            { if(remove--<=0) break; site.Completed=true; }
        }
        public bool Complete(int id)
        {
            var site=Sites.Find(s=>s.Id==id);
            if(site==null || site.Completed || !site.Extracted) return false;
            site.Completed=true; return true;
        }
        public SiteLayout Layout(int id)
        {
            uint state=unchecked((uint)Seed ^ ((uint)id+1)*2654435761u);
            if(state==0) state=1;
            return new SiteLayout {
                Page=id/12, X=-337.5f+(id%4)*225f+(Next(ref state)-0.5f)*16,
                Y=230-(id%12/4)*230+(Next(ref state)-0.5f)*16,
                Angle=-35+Next(ref state)*70, Length=300+Next(ref state)*45, Width=55+Next(ref state)*10
            };
        }
        static float Next(ref uint s) { s^=s<<13; s^=s>>17; s^=s<<5; return (s & 0xffffff)/16777216f; }
    }
    public struct SiteLayout { public int Page; public float X,Y,Angle,Length,Width; }
}
