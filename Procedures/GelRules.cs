using System;
namespace CasualtiesSurgery.Procedures
{
    public static class GelRules
    {
        public const float FlowMlPerSecond=2,FragmentMl=2,SectionMl=.4f;
        public static float Deposit(float current,float amount,float goal)=>Math.Min(goal,Math.Max(0,current)+Math.Max(0,amount));
        public static float InfectionRisk(float coverage,float immunity,float quality,float multiplier)
        {
            if(float.IsNaN(immunity)||float.IsInfinity(immunity))immunity=100;
            return Math.Max(0,Math.Min(1,.3f*(1-Math.Max(0,Math.Min(1,coverage)))*Math.Max(.25f,2-Math.Max(0,Math.Min(200,immunity))/100)*(1.5f-Math.Max(0,Math.Min(100,quality))/100)*Math.Max(0,multiplier)));
        }
    }
}
