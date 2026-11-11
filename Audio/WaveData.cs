using System;
using System.IO;
using System.Text;
namespace CasualtiesSurgery.Audio
{
    public sealed class WaveData
    {
        public int Channels, Rate;
        public float[] Samples;
        public static WaveData Read(byte[] bytes)
        {
            using(var stream=new MemoryStream(bytes)) using(var reader=new BinaryReader(stream))
            {
                if(bytes.Length<12 || Encoding.ASCII.GetString(reader.ReadBytes(4))!="RIFF") throw new InvalidDataException("Not RIFF WAV.");
                reader.ReadUInt32(); if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="WAVE") throw new InvalidDataException("Not WAV.");
                int format=0,channels=0,rate=0,bits=0;byte[] data=null;
                while(stream.Position+8<=stream.Length)
                {
                    string id=Encoding.ASCII.GetString(reader.ReadBytes(4));uint length=reader.ReadUInt32();
                    long end=stream.Position+length;if(end>stream.Length)throw new InvalidDataException("Truncated WAV chunk.");
                    if(id=="fmt ")
                    {if(length<16)throw new InvalidDataException("Invalid format.");format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadUInt32();reader.ReadUInt16();bits=reader.ReadUInt16();}
                    else if(id=="data")data=reader.ReadBytes(checked((int)length));
                    stream.Position=Math.Min(stream.Length,end+(length&1));
                }
                if(format!=1 || bits!=16 || channels<1 || channels>2 || rate<8000 || rate>192000 || data==null || data.Length==0 || data.Length%(channels*2)!=0)
                    throw new InvalidDataException("Expected 16-bit mono/stereo PCM WAV.");
                var samples=new float[data.Length/2];for(int i=0;i<samples.Length;i++)samples[i]=(short)(data[i*2]|data[i*2+1]<<8)/32768f;
                return new WaveData { Channels=channels,Rate=rate,Samples=samples };
            }
        }
    }
    public static class ResultRating
    {
        public static string Label(int score)=>score>=75?"Great":score<=40?"Bad":"Good";
    }
}
