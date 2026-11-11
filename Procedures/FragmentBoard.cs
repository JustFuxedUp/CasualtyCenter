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
        public int LayoutVersion; // Missing in older saves: keep their existing layout.
        public List<FragmentSite> Sites = new List<FragmentSite>();
        public int Remaining => Sites.Count(s => !s.Completed);
        public FragmentBoard() { }
        public FragmentBoard(int seed, int count) { Seed=seed; LayoutVersion=2; Reconcile(count); }
        public void Validate()
        {
            if (LayoutVersion<0 || LayoutVersion>2 || Sites==null || Sites.Count>10000 || NextId<0 || NextId>10000 ||
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
            if(LayoutVersion>=1)
            {
                var positions=Scatter(id/12);
                return new SiteLayout { Page=id/12,X=positions[id%12].X,Y=positions[id%12].Y,
                    Angle=LayoutVersion>=2 ? Next(ref state)*360 : -35+Next(ref state)*70,Length=300+Next(ref state)*45,Width=55+Next(ref state)*10 };
            }
            return new SiteLayout {
                Page=id/12, X=-337.5f+(id%4)*225f+(Next(ref state)-0.5f)*16,
                Y=230-(id%12/4)*230+(Next(ref state)-0.5f)*16,
                Angle=-35+Next(ref state)*70, Length=300+Next(ref state)*45, Width=55+Next(ref state)*10
            };
        }
        List<SiteLayout> Scatter(int page)
        {
            uint state=unchecked((uint)Seed ^ ((uint)page+1)*2246822519u); if(state==0)state=1;
            var points=new List<SiteLayout>();
            for(int i=0;i<12;i++)
            {
                SiteLayout best=default(SiteLayout); float bestDistance=-1;
                // Bounded best-candidate placement: random positions with center spacing.
                for(int candidate=0;candidate<256;candidate++)
                {
                    var point=new SiteLayout { X=-310+Next(ref state)*620,Y=-210+Next(ref state)*420 };
                    float nearest=float.MaxValue;
                    foreach(var existing in points)
                    {float dx=point.X-existing.X,dy=point.Y-existing.Y;nearest=Math.Min(nearest,dx*dx+dy*dy);}
                    if(nearest>bestDistance){best=point;bestDistance=nearest;}
                    if(nearest>=125*125) break;
                }
                points.Add(best);
            }
            return points;
        }
        static float Next(ref uint s) { s^=s<<13; s^=s>>17; s^=s<<5; return (s & 0xffffff)/16777216f; }
    }
    public struct SiteLayout { public int Page; public float X,Y,Angle,Length,Width; }
}
