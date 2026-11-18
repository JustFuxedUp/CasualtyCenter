using System;
using System.IO;
using System.Linq;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasualtiesSurgery.Game
{
    internal static class TreatmentInput
    {
        public static JObject Parse(string text)
        {
            if(text==null || text.Length>SurgeryCommand.MaxPayloadCharacters)return null;
            try
            {
                using(var reader=new JsonTextReader(new StringReader(text)) { MaxDepth=6,DateParseHandling=DateParseHandling.None })
                { var value=JObject.Load(reader);return reader.Read() ? null : value; }
            }
            catch(JsonException) { return null; }
        }
        public static bool Fields(JObject value,params string[] names) => value!=null && value.Count==names.Length && value.Properties().All(p=>names.Contains(p.Name));
        public static bool Number(JToken value,out float number)
        {
            number=0;
            if(value==null || (value.Type!=JTokenType.Float && value.Type!=JTokenType.Integer))return false;
            try { number=value.Value<float>(); }
            catch(Exception e) when(e is OverflowException || e is FormatException || e is InvalidCastException) { return false; }
            return !float.IsNaN(number) && !float.IsInfinity(number);
        }
        public static bool Index(JToken token,int count,out int index)
        {
            index=-1;
            if(token?.Type!=JTokenType.Integer || !Number(token,out float value) || value<0 || value>=count)return false;
            index=(int)value;return true;
        }
        public static bool Point(JObject value,out float x,out float y)
        { y=0;return Number(value?["x"],out x) && Number(value["y"],out y) && Math.Abs(x)<=4096 && Math.Abs(y)<=4096; }
        public static bool Suture(JToken token,out int quality)
        {
            quality=0;
            if(!(token is JArray samples) || samples.Count<4 || samples.Count>128)return false;
            var stroke=new SutureStroke(2);
            for(int i=0;i<samples.Count;i++)
            {
                if(!(samples[i] is JObject sample) || !Fields(sample,"x","y") || !Point(sample,out float x,out float y) || Math.Abs(x)>20 || Math.Abs(y)>20)return false;
                if(i==0)stroke.Begin(x,y);
                else if(i==samples.Count-1)return stroke.End(x,y,out quality);
                else stroke.Move(x,y);
            }
            return false;
        }
        public static void Local(float x,float y,float cx,float cy,float angle,out float lx,out float ly)
        {
            double radians=angle*Math.PI/180,cos=Math.Cos(radians),sin=Math.Sin(radians);
            lx=(float)((x-cx)*cos+(y-cy)*sin);ly=(float)(-(x-cx)*sin+(y-cy)*cos);
        }
        public static bool PoolHit(BloodPools pools,int slot,float x,float y,float cx,float cy)
        {
            if(slot<0 || slot>1 || pools.Remaining(slot)<=0)return false;
            Local(x,y,cx+(slot==0?0:37),cy+(slot==0?0:-25),(slot==0?pools.FirstAngle:pools.SecondAngle)??0,out float lx,out float ly);
            float half=slot==0?34:26; // Current pool artwork is 68/52 board units.
            return Math.Abs(lx)<=half && Math.Abs(ly)<=half;
        }
    }
    // One host-time budget shared by all held tools in an operation. Lease changes cannot
    // mint time; cached requests do not invoke the application a second time.
    internal sealed class TreatmentTime
    {
        Guid lease;
        double last,credit;
        public void Begin(Guid token,double now) {lease=token;last=now;credit=0;}
        public bool Spend(Guid token,double now,float seconds)
        {
            if(!(seconds>0 && seconds<=.25f) || double.IsNaN(now) || double.IsInfinity(now) || now<0)return false;
            if(lease!=token) {lease=token;last=now;credit=0;}
            if(now<last)return false;
            credit=Math.Min(.25,credit+now-last);last=now;
            if(seconds>(float)credit)return false;
            credit=Math.Max(0,credit-seconds);return true;
        }
    }
}
