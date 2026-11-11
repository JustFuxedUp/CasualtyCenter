using System;
using System.IO;
using System.Text;
using CasualtiesSurgery.Audio;
using CasualtiesSurgery.Procedures;
static class FeedbackChecks
{
    public static void Run(Action<bool,string> check)
    {
        check(ResultRating.Label(40)=="Bad" && ResultRating.Label(0)=="Bad", "Bad includes 40");
        check(ResultRating.Label(41)=="Good" && ResultRating.Label(74)=="Good", "Good spans 41 through 74");
        check(ResultRating.Label(75)=="Great" && ResultRating.Label(100)=="Great", "Great starts at 75");
        check(FragmentPull.CanGrip(0,.38f) && FragmentPull.CanGrip(.35f,.55f), "Exposed tip and grace area are grippable");
        check(!FragmentPull.CanGrip(.37f,.38f) && !FragmentPull.CanGrip(0,.57f) && !FragmentPull.CanGrip(float.NaN,.38f), "Outside tip and invalid positions rejected");
        byte[] bytes;
        using(var stream=new MemoryStream())
        {
            using(var w=new BinaryWriter(stream,Encoding.ASCII,true))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(52);w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("JUNK"));w.Write(1);w.Write((byte)7);w.Write((byte)0);
                w.Write(Encoding.ASCII.GetBytes("fmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(22050);w.Write(44100);w.Write((short)2);w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));w.Write(6);w.Write(short.MinValue);w.Write((short)0);w.Write(short.MaxValue);
            }
            bytes=stream.ToArray();
        }
        var wave=WaveData.Read(bytes);
        check(wave.Channels==1 && wave.Rate==22050 && wave.Samples.Length==3, "PCM with odd padded chunk decodes");
        check(wave.Samples[0]==-1 && wave.Samples[1]==0 && wave.Samples[2]>.999f, "Signed PCM amplitude preserved");
        bool rejected=false;try {WaveData.Read(new byte[3]);}catch(InvalidDataException){rejected=true;}
        check(rejected,"Invalid WAV rejected");
        rejected=false;try {WaveData.Read(bytes.AsSpan(0,bytes.Length-1).ToArray());}catch(InvalidDataException){rejected=true;}
        check(rejected,"Truncated WAV rejected");
        string folder=Environment.GetEnvironmentVariable("SURGERY_AUDIO_TEST_DIR");
        if(!string.IsNullOrEmpty(folder)) foreach(string name in new[]{"Bad-Ok","Great","Mistake","SutureStitch","ForcepsGrab","Scalpel"})
        {
            wave=WaveData.Read(File.ReadAllBytes(Path.Combine(folder,name+".wav")));
            check(wave.Samples.Length>0 && wave.Rate==22050 && wave.Channels==1,"Local sound decoded: "+name);
        }
    }
}
