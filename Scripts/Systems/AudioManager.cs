using System;
using System.Collections.Generic;
using Godot;

namespace TopDownDemo.Systems;

public enum SoundType
{
    RadioChime,
    DoorOpen,
    GeneratorStart,
    AlarmKlaxon,
    TerminalData,
    PistolShot,
    ShotgunShot,
    RifleShot,
    ZombieGroan,
    BossRoar,
    PickupCollect,
    PlayerHurt,
    ItemUnlock
}

public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    private readonly Dictionary<SoundType, AudioStreamWav> _soundCache = new();
    private readonly List<AudioStreamPlayer2D> _spatialPlayers = new();
    private AudioStreamPlayer _uiPlayer;
    private AudioStreamPlayer _alarmPlayer;

    private const int SampleRate = 22050;

    public override void _Ready()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        _uiPlayer = new AudioStreamPlayer { Name = "UIPlayer" };
        AddChild(_uiPlayer);

        _alarmPlayer = new AudioStreamPlayer { Name = "AlarmPlayer", VolumeDb = -6.0f };
        AddChild(_alarmPlayer);

        for (int i = 0; i < 8; i++)
        {
            var p = new AudioStreamPlayer2D { MaxDistance = 1400.0f };
            _spatialPlayers.Add(p);
            AddChild(p);
        }

        GenerateSoundCache();
    }

    private void GenerateSoundCache()
    {
        _soundCache[SoundType.RadioChime] = SynthesizeRadioChime();
        _soundCache[SoundType.DoorOpen] = SynthesizeHydraulicDoor();
        _soundCache[SoundType.GeneratorStart] = SynthesizeGeneratorStart();
        _soundCache[SoundType.AlarmKlaxon] = SynthesizeAlarmKlaxon();
        _soundCache[SoundType.TerminalData] = SynthesizeTerminalData();
        _soundCache[SoundType.PistolShot] = SynthesizeGunshot(320f, 0.18f, 0.45f);
        _soundCache[SoundType.ShotgunShot] = SynthesizeGunshot(180f, 0.32f, 0.85f);
        _soundCache[SoundType.RifleShot] = SynthesizeGunshot(440f, 0.14f, 0.55f);
        _soundCache[SoundType.ZombieGroan] = SynthesizeZombieGroan(110f, 0.65f);
        _soundCache[SoundType.BossRoar] = SynthesizeZombieGroan(65f, 1.2f, true);
        _soundCache[SoundType.PickupCollect] = SynthesizePickupChime();
        _soundCache[SoundType.PlayerHurt] = SynthesizeImpactThud();
        _soundCache[SoundType.ItemUnlock] = SynthesizeClick();
    }

    public void PlaySound(SoundType type, Vector2? worldPos = null, float pitchScale = 1.0f)
    {
        if (!_soundCache.TryGetValue(type, out var stream) || stream == null)
            return;

        if (worldPos.HasValue)
        {
            AudioStreamPlayer2D player = null;
            for (int i = 0; i < _spatialPlayers.Count; i++)
            {
                if (!_spatialPlayers[i].Playing)
                {
                    player = _spatialPlayers[i];
                    break;
                }
            }
            player ??= _spatialPlayers[0];

            player.GlobalPosition = worldPos.Value;
            player.Stream = stream;
            player.PitchScale = pitchScale;
            player.Play();
        }
        else
        {
            if (_uiPlayer != null)
            {
                _uiPlayer.Stream = stream;
                _uiPlayer.PitchScale = pitchScale;
                _uiPlayer.Play();
            }
        }
    }

    public void PlayAlarm(bool loop)
    {
        if (_soundCache.TryGetValue(SoundType.AlarmKlaxon, out var stream) && _alarmPlayer != null)
        {
            _alarmPlayer.Stream = stream;
            _alarmPlayer.Play();
        }
    }

    public void StopAlarm()
    {
        _alarmPlayer?.Stop();
    }

    private static AudioStreamWav BuildWav(short[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short s = samples[i];
            bytes[i * 2] = (byte)(s & 0xFF);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }

        var wav = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = SampleRate,
            Stereo = false,
            Data = bytes
        };
        return wav;
    }

    private static AudioStreamWav SynthesizeRadioChime()
    {
        int totalSamples = (int)(SampleRate * 0.45f);
        short[] samples = new short[totalSamples];
        int half = totalSamples / 2;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float freq = i < half ? 784f : 1046.5f; // G5 then C6
            float env = Mathf.Exp(-((i % half) / (float)half) * 4.5f);
            float wave = Mathf.Sin(Mathf.Tau * freq * t) * env;
            samples[i] = (short)(wave * 14000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizePickupChime()
    {
        int totalSamples = (int)(SampleRate * 0.35f);
        short[] samples = new short[totalSamples];
        int segment = totalSamples / 3;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float freq = (i / segment) switch
            {
                0 => 523.25f, // C5
                1 => 659.25f, // E5
                _ => 783.99f  // G5
            };
            float env = Mathf.Exp(-((i % segment) / (float)segment) * 5.0f);
            float wave = Mathf.Sin(Mathf.Tau * freq * t) * env;
            samples[i] = (short)(wave * 12000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeHydraulicDoor()
    {
        int totalSamples = (int)(SampleRate * 0.8f);
        short[] samples = new short[totalSamples];
        var rng = new Random(1234);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;
            float noise = ((float)rng.NextDouble() * 2.0f - 1.0f);
            float hum = Mathf.Sin(Mathf.Tau * (95f + progress * 40f) * t);
            float env = Mathf.Sin(progress * Mathf.Pi) * 0.7f;
            float val = (noise * 0.45f + hum * 0.55f) * env;
            samples[i] = (short)(val * 16000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeGeneratorStart()
    {
        int totalSamples = (int)(SampleRate * 1.5f);
        short[] samples = new short[totalSamples];
        var rng = new Random(5678);

        for (int i = 0; i < totalSamples; i++)
        {
            float progress = (float)i / totalSamples;
            float freq = Mathf.Lerp(45f, 220f, progress * progress);
            float t = (float)i / SampleRate;
            float wave = Mathf.Sin(Mathf.Tau * freq * t);
            float noise = ((float)rng.NextDouble() * 2.0f - 1.0f) * 0.25f;
            float env = Mathf.Min(1.0f, progress * 3.0f) * (1.0f - progress * 0.2f);
            samples[i] = (short)((wave + noise) * env * 18000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeAlarmKlaxon()
    {
        int totalSamples = (int)(SampleRate * 1.2f);
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float cycle = (t * 2.5f) % 1.0f;
            float freq = cycle < 0.5f ? 750f : 550f;
            float wave = Mathf.Sin(Mathf.Tau * freq * t);
            samples[i] = (short)(wave * 14000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeTerminalData()
    {
        int totalSamples = (int)(SampleRate * 0.4f);
        short[] samples = new short[totalSamples];
        var rng = new Random(999);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float freq = 600f + rng.Next(0, 6) * 150f;
            float wave = Mathf.Sin(Mathf.Tau * freq * t);
            float env = (i % 250) < 180 ? 0.7f : 0.0f;
            samples[i] = (short)(wave * env * 9000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeGunshot(float baseFreq, float duration, float punch)
    {
        int totalSamples = (int)(SampleRate * duration);
        short[] samples = new short[totalSamples];
        var rng = new Random(4321);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * 24.0f);
            float noise = ((float)rng.NextDouble() * 2.0f - 1.0f);
            float freq = baseFreq * Mathf.Exp(-t * 30.0f);
            float body = Mathf.Sin(Mathf.Tau * freq * t);
            float val = (noise * 0.65f + body * 0.35f) * env * punch;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 26000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeZombieGroan(float basePitch, float duration, bool isBoss = false)
    {
        int totalSamples = (int)(SampleRate * duration);
        short[] samples = new short[totalSamples];
        var rng = new Random(isBoss ? 777 : 888);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;
            float env = Mathf.Sin(progress * Mathf.Pi);
            float mod = Mathf.Sin(Mathf.Tau * 6.0f * t) * 15.0f;
            float wave = Mathf.Sin(Mathf.Tau * (basePitch + mod) * t);
            float rumble = ((float)rng.NextDouble() * 2.0f - 1.0f) * (isBoss ? 0.45f : 0.25f);
            samples[i] = (short)((wave * 0.65f + rumble) * env * 17000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeImpactThud()
    {
        int totalSamples = (int)(SampleRate * 0.25f);
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * 18.0f);
            float wave = Mathf.Sin(Mathf.Tau * 85.0f * t);
            samples[i] = (short)(wave * env * 22000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeClick()
    {
        int totalSamples = (int)(SampleRate * 0.08f);
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * 80.0f);
            float wave = Mathf.Sin(Mathf.Tau * 1200.0f * t);
            samples[i] = (short)(wave * env * 16000);
        }
        return BuildWav(samples);
    }
}
