using System;
namespace CasualtiesSurgery.Procedures
{
    public static class GelRules
    {
        public const float FlowMlPerSecond=2,FragmentMl=2,SectionMl=.4f;
        public static float Deposit(float current,float amount,float goal)=>Math.Min(goal,Math.Max(0,current)+Math.Max(0,amount));
        public const int IncisionSections = 9;
        public static bool Disinfect(float[] coverage, int index, float ml)
        {
            if (coverage == null || index < 0 || index >= coverage.Length || !(ml > 0) || float.IsInfinity(ml)) return false;
            float before = coverage[index];
            coverage[index] = Deposit(before, ml, SectionMl);
            return coverage[index] > before;
        }

        public static float IncisionCoverage(float[] sections)
        {
            float sum = 0;
            foreach (float amount in sections) sum += amount / SectionMl;
            return sum / sections.Length;
        }

        public static bool ValidIncisionCoverage(float[] sections) => sections != null &&
            sections.Length == IncisionSections && Array.TrueForAll(sections, amount => amount >= 0 && amount <= SectionMl + .001f);

        public static float InfectionRisk(float coverage,float immunity,float quality,float multiplier)
        {
            if(float.IsNaN(immunity)||float.IsInfinity(immunity))immunity=100;
            return Math.Max(0,Math.Min(1,.3f*(1-Math.Max(0,Math.Min(1,coverage)))*Math.Max(.25f,2-Math.Max(0,Math.Min(200,immunity))/100)*(1.5f-Math.Max(0,Math.Min(100,quality))/100)*Math.Max(0,multiplier)));
        }
    }
}
