using System;
namespace CasualtiesSurgery.Procedures
{
    // Ephemeral, bounded cosmetic data. Never used as clinical gesture evidence.
    public sealed class SurgeryVisualState
    {
        public long Sequence {get;}
        public int Tool {get;} public int Sprite {get;} public int Phase {get;} public int Page {get;}
        public float X {get;} public float Y {get;} public float Angle {get;} public float Scale {get;}
        public float Width {get;} public float Height {get;}
        public bool Mirrored {get;} public bool Pressed {get;}
        public float[] Trail {get;}
        public SurgeryVisualState(long sequence,int tool,int sprite,int phase,int page,float x,float y,float angle,float scale,float width,float height,bool mirrored,bool pressed,float[] trail)
        {Sequence=sequence;Tool=tool;Sprite=sprite;Phase=phase;Page=page;X=x;Y=y;Angle=angle;Scale=scale;Width=width;Height=height;Mirrored=mirrored;Pressed=pressed;Trail=trail==null?new float[0]:(float[])trail.Clone();}
        public bool Valid
        {
            get
            {
                if(Sequence<=0 || Sequence>=long.MaxValue || Tool<0 || Tool>4 || Sprite<0 || Sprite>31 || Phase<0 || Phase>3 || Page<0 || Page>21 ||
                    !Bound(X,2048) || !Bound(Y,2048) || !Bound(Angle,360) || !(Scale>=.1f && Scale<=.4f) ||
                    !(Width>0 && Width<=2048) || !(Height>0 && Height<=2048) || Trail.Length>128 || Trail.Length%2!=0)return false;
                foreach(float value in Trail)if(!Bound(value,2048))return false;
                return true;
            }
        }
        static bool Bound(float value,float maximum)=>!float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value)<=maximum;
        public SurgeryVisualState Copy()=>new SurgeryVisualState(Sequence,Tool,Sprite,Phase,Page,X,Y,Angle,Scale,Width,Height,Mirrored,Pressed,Trail);
    }
    public sealed class SurgeryPeerVisual
    {
        public Guid Participant {get;} public byte Red {get;} public byte Green {get;} public byte Blue {get;}
        public double Age {get;} public SurgeryVisualState State {get;}
        public SurgeryPeerVisual(Guid participant,byte red,byte green,byte blue,double age,SurgeryVisualState state)
        {Participant=participant;Red=red;Green=green;Blue=blue;Age=age;State=state?.Copy();}
    }
}
