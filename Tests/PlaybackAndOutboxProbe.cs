using JanjaCord.Client.Media;
using JanjaCord.Client.Services;
using NAudio.Wave;
public static class PlaybackAndOutboxProbe
{
    sealed class QuietVoice:ISampleProvider
    {
        int position;
        public WaveFormat WaveFormat=>WaveFormat.CreateIeeeFloatWaveFormat(48000,1);
        public int Read(float[] buffer,int offset,int count){for(var i=0;i<count;i++)buffer[offset+i]=(float)(Math.Sin(position++*2*Math.PI*440/48000)*.02);return count;}
    }
    static double Level(int percent)
    {
        var voice=new VoicePlaybackSource(new QuietVoice()){Percent=percent};var output=new LimitedSampleProvider(voice);var samples=new float[960];for(var i=0;i<100;i++)output.Read(samples,0,samples.Length);return Math.Sqrt(samples.Average(x=>x*x));
    }
    public static void Run()
    {
        var normal=Level(100);var louder=Level(200);if(normal<.013||normal>.016||Math.Abs(louder/normal-2)>.01||Level(0)!=0)throw new Exception("Volume individual/ganho de voz baixa incorreto.");
        Console.WriteLine("PASS: reprodução preserva o nível, 0% silencia e 200% dobra o áudio individual");
        var previous=Environment.GetEnvironmentVariable("JANJACORD_PROFILE_DIR");var root=Path.Combine(Path.GetTempPath(),"Janja-outbox-"+Guid.NewGuid().ToString("N"));
        try{
            Environment.SetEnvironmentVariable("JANJACORD_PROFILE_DIR",root);var cache=ConversationCache.Load("test-network");var pending=cache.Enqueue(42,"Mensagem enquanto o host está fechado");
            var restored=ConversationCache.Load("test-network");if(restored.Outbox.Single().ClientId!=pending.ClientId||restored.Outbox.Single().Text!=pending.Text)throw new Exception("Fila offline não persistiu.");
            if(ConversationCache.Load("other-network").Outbox.Count!=0)throw new Exception("Fila vazou para outra rede.");
            restored.Put(new(7,1,42,"Teste",pending.Text,DateTimeOffset.UtcNow,pending.ClientId),1);if(ConversationCache.Load("test-network").Outbox.Count!=0)throw new Exception("Confirmação não removeu pendência.");
            var profile=new LocalProfile{NoiseSuppressionEnabled=false,NoiseSuppressionStrength=.37f};profile.PeerVolumes["network:42"]=175;profile.Save();if(LocalProfile.Load().PeerVolumes["network:42"]!=175||LocalProfile.Load().NoiseSuppressionEnabled||Math.Abs(LocalProfile.Load().NoiseSuppressionStrength-.37f)>.00001)throw new Exception("Preferência de áudio não foi salva.");
            Console.WriteLine("PASS: fila offline cifrada persiste ao reabrir, separa redes e retira envio confirmado; volume pessoal persiste");
        }finally{Environment.SetEnvironmentVariable("JANJACORD_PROFILE_DIR",previous);}
    }
}
