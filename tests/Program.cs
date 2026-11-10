using System;
using CasualtiesSurgery.Procedures;

static class Program
{
    static int checks;
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks++;
    }
    static FragmentProcedure Extracted()
    {
        var procedure = new FragmentProcedure();
        procedure.RecordSuccessfulExtraction();
        return procedure;
    }
    static void Main()
    {
        var p = new FragmentProcedure();
        Check(!p.TryCompleteSuture(2, 60, 3, 5, 1, out _), "No reward before extraction");
        Check(p.RecordSuccessfulExtraction(), "Extraction accepted");
        Check(!p.RecordSuccessfulExtraction(), "Duplicate extraction rejected");
        Check(p.TryCompleteSuture(2, 60, 3, 5, 1, out var result), "Suture accepted");
        Check(result.Fragments == 1 && result.SkinHealth == 65 && result.Bleeding == 2, "All three effects together");
        Check(!p.TryCompleteSuture(1, 65, 2, 5, 1, out _), "Duplicate reward rejected");
        p = Extracted();
        Check(p.TryCompleteSuture(1, 98, 0.25f, 5, 1, out result) && result.SkinHealth == 100 && result.Bleeding == 0, "Health and bleeding boundaries");
        p = Extracted(); p.Cancel();
        Check(!p.TryCompleteSuture(1, 60, 3, 5, 1, out _), "Cancelled operation grants nothing");
        p = Extracted();
        Check(!p.TryCompleteSuture(0, 60, 3, 5, 1, out _), "Removed fragment invalidates completion");
        Check(p.Stage == FragmentStage.Suture, "Invalid completion leaves state unchanged");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Check(!p.TryCompleteSuture(1, invalid, 3, 5, 1, out _), "Invalid skin rejected");
            Check(!p.TryCompleteSuture(1, 60, invalid, 5, 1, out _), "Invalid bleeding rejected");
            Check(!p.TryCompleteSuture(1, 60, 3, invalid, 1, out _), "Invalid recovery rejected");
            Check(!p.TryCompleteSuture(1, 60, 3, 5, invalid, out _), "Invalid reduction rejected");
        }
        Check(!p.TryCompleteSuture(1, 101, 3, 5, 1, out _), "Out-of-range skin rejected");
        Check(!p.TryCompleteSuture(1, 60, -1, 5, 1, out _), "Negative bleeding rejected");
        Check(!p.TryCompleteSuture(1, 60, 3, 0, 1, out _), "Zero recovery rejected");
        Check(!p.TryCompleteSuture(1, 60, 3, 5, -1, out _), "Negative reduction rejected");
        Check(p.TryCompleteSuture(1, 40, 8, 5, 1, out result) && result.SkinHealth == 45 && result.Bleeding == 7, "Uses latest supplied state");
        Console.WriteLine(checks + " procedure checks passed.");
    }
}
