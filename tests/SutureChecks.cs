using System;
using CasualtiesSurgery.Procedures;
static class SutureChecks
{
    static int Draw(float[] progress)
    {
        var s = new SutureStroke(); s.Begin(-1, 0);
        for (int i = 0; i < progress.Length; i++)
            if (!s.Move(i%2 == 0 ? 1 : -1, progress[i])) throw new Exception("Valid test stroke rejected");
        if (!s.End(progress.Length%2 == 0 ? -1 : 1, 1, out int score)) throw new Exception("Test stroke did not complete");
        return score;
    }
    public static void Run(Action<bool,string> check)
    {
        var s = new SutureStroke();
        check(!s.End(-1,1,out _), "Suture requires a grip at the start");
        check(!s.Begin(1,0) && !s.Begin(-1,0.5f), "Suture rejects wrong side and middle starts");
        check(!s.Begin(float.NaN,0), "Nonfinite suture start rejected");
        check(Draw(new[] {1f/6,2f/6,3f/6,4f/6,5f/6,1f}) >= 99, "Even efficient zigzag scores highly");
        check(Draw(new[] {0.08f,0.35f,0.43f,0.72f,0.80f,1f}) < 90, "Uneven stitch spacing reduces quality");
        check(Draw(new[] {0.25f,0.5f,0.75f,1f}) >= 99, "Four full crossings accepted without tracing exact guide dots");
        s.Begin(-1,0); s.Move(1,1);
        check(!s.End(1,1,out _), "Single diagonal cannot suture a whole wound");
        s.Begin(-1,0); s.Move(1,0.15f); s.Move(-1,0.3f); s.Move(1,0.45f); s.Move(-1,0.6f);
        check(!s.End(-1,0.6f,out _), "Early release grants no completed suture");
        s.Begin(-1,0);
        s.Move(1,0.01f);
        check(!s.Move(-1,0.02f), "Side-to-side scribble without wound coverage rejected");
        s.Begin(-1,0); s.Move(1,0.3f);
        check(!s.Move(0,0.1f), "Large backwards stroke rejected");
        s.Begin(-1,0);
        check(!s.Move(2,0.2f) && !s.End(-1,1,out _), "Leaving wound invalidates the whole stroke");
        s.Begin(-1,0); s.Reset();
        check(!s.End(-1,1,out _), "Tool switch cannot resume an abandoned stroke");
        s.Begin(-1,0);
        check(!s.Move(float.PositiveInfinity,0.2f), "Nonfinite suture movement rejected");
        s.Begin(-1,0);
        for (int i=1; i<=6; i++) s.Move(i%2==1?1:-1,i/6f);
        check(s.End(-1,1,out _) && !s.End(-1,1,out _), "Suture release cannot be replayed");
        // Same geometric stroke sampled at different frame rates must keep the same result.
        s.Begin(-1,0); float previousX=-1, previousY=0;
        for (int i=1; i<=6; i++)
        {
            float nextX=i%2==1?1:-1, nextY=i/6f;
            for(int n=1;n<=20;n++) s.Move(previousX+(nextX-previousX)*n/20f,previousY+(nextY-previousY)*n/20f);
            previousX=nextX; previousY=nextY;
        }
        check(s.End(-1,1,out int sampled) && sampled >= 90, "Densely sampled clean zigzag remains high quality");
    }
}
