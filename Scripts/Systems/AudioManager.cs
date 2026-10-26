using System;
using System.Collections.Generic;
using Godot;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems;

public enum SoundType
{
    RadioChime,
    DoorOpen,
    GeneratorStart,
    AlarmKlaxon,
    TerminalData,
    PistolShot,
    PistolShot1 = PistolShot,
    PistolShot2,
    PistolShot3,
    PistolShot4,
    ShotgunShot,
    ShotgunShot1 = ShotgunShot,
    ShotgunShot2,
    ShotgunShot3,
    RifleShot,
    RifleShot1 = RifleShot,
    RifleShot2,
    RifleShot3,
    RifleShot4,
    ZombieGroan,
    BossRoar,
    PickupCollect,
    PlayerHurt,
    ItemUnlock,
    FinisherKick,
    FinisherSlash,
    EnemyStunned,
    FinisherPrompt,
    VentBreak,
    MedicalHeal,
    PadlockClick,
    PadlockUnlock
}

public partial class AudioManager : Node
{
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    [ExportGroup("Music Configuration")]
    [Export] public float DefaultMusicVolumeDb { get; set; } = -10.0f;
    [Export] public string AmbientMusicPath { get; set; } = "res://Assets/audio/music_ambient_horror.ogg";
    [Export] public string BossMusicPath { get; set; } = "res://Assets/audio/music_boss_combat.ogg";

    private readonly Dictionary<SoundType, AudioStreamWav> _soundCache = new();
    private readonly List<AudioStreamPlayer2D> _spatialPlayers = new();
    private AudioStreamPlayer _uiPlayer;
    private AudioStreamPlayer _alarmPlayer;

    private AudioStreamPlayer _musicPlayerA;
    private AudioStreamPlayer _musicPlayerB;
    private AudioStreamPlayer _activeMusicPlayer;
    private Tween _musicFadeTween;
    private string _currentTrackId = "";

    private const int SampleRate = 22050;

    public override void _EnterTree()
    {
        PlayerSettings.EnsureLoaded();
        ApplicationExit.Initialize(GetTree());
        Instance = this;
    }

    public override void _ExitTree()
    {
        StopAllPlayback();

        if (StoryDirector.Instance != null)
        {
            StoryDirector.Instance.BossSpawned -= OnStoryBossSpawned;
            StoryDirector.Instance.MissionCompleted -= OnStoryMissionCompleted;
        }

        if (_instance == this)
        {
            _instance = null;
        }
    }

    public override void _Ready()
    {
        Instance = this;
        EnsurePlayers();
        GenerateSoundCache();

        // Autoplay atmospheric survival horror background music if no track was already started
        if (string.IsNullOrEmpty(_currentTrackId))
        {
            PlayMusic("ambient", fadeDuration: 1.5f);
        }

        // Hook story events once all tree siblings are initialized
        Callable.From(HookStoryEvents).CallDeferred();
    }

    private void EnsurePlayers()
    {
        if (_musicPlayerA != null) return;

        _uiPlayer = new AudioStreamPlayer { Name = "UIPlayer", Bus = PlayerSettings.EffectsBus };
        AddChild(_uiPlayer);

        _alarmPlayer = new AudioStreamPlayer { Name = "AlarmPlayer", VolumeDb = -6.0f, Bus = PlayerSettings.EffectsBus };
        AddChild(_alarmPlayer);

        // Dedicated crossfading background music players
        _musicPlayerA = new AudioStreamPlayer { Name = "MusicPlayerA", Bus = PlayerSettings.MusicBus, ProcessMode = ProcessModeEnum.Always };
        _musicPlayerB = new AudioStreamPlayer { Name = "MusicPlayerB", Bus = PlayerSettings.MusicBus, ProcessMode = ProcessModeEnum.Always };
        AddChild(_musicPlayerA);
        AddChild(_musicPlayerB);

        for (int i = 0; i < 8; i++)
        {
            var p = new AudioStreamPlayer2D { MaxDistance = 1400.0f, Bus = PlayerSettings.EffectsBus };
            _spatialPlayers.Add(p);
            AddChild(p);
        }
    }

    private void HookStoryEvents()
    {
        if (StoryDirector.Instance != null)
        {
            StoryDirector.Instance.BossSpawned += OnStoryBossSpawned;
            StoryDirector.Instance.MissionCompleted += OnStoryMissionCompleted;
        }
    }

    private void OnStoryBossSpawned(Zombie boss)
    {
        PlayMusic("boss", fadeDuration: 1.8f);
    }

    private void OnStoryMissionCompleted()
    {
        PlayMusic("ambient", fadeDuration: 3.5f);
    }

    public void PlaySceneMusic(string sceneId, float fadeDuration = 1.8f)
    {
        string track = sceneId.ToLowerInvariant() switch
        {
            "act1_gate" or "scene_gate" => "gate_dread",
            "act2_checkpoint" or "scene_checkpoint" => "checkpoint_tense",
            "act3_lab" or "scene_lab" => "lab_pulse",
            "act4_subbasement" or "scene_subbasement" => "sub_revelation",
            "act5_arena" or "scene_arena" => "boss_unit9",
            "ending_escape" => "ending_escape",
            "ending_truth" => "ending_truth",
            "main_menu" => "intro_military",
            _ => "ambient"
        };
        PlayMusic(track, fadeDuration);
    }

    public void PlayMusic(string trackId, float fadeDuration = 2.0f)
    {
        EnsurePlayers();
        if (_currentTrackId == trackId && _activeMusicPlayer != null && _activeMusicPlayer.Playing)
            return;

        string path = trackId.ToLowerInvariant() switch
        {
            "boss" => BossMusicPath,
            "boss_unit9" => "res://Assets/audio/music_boss_unit9.ogg",
            "intro" or "intro_military" => "res://Assets/audio/music_intro_military.ogg",
            "gate" or "gate_dread" => "res://Assets/audio/music_gate_dread.ogg",
            "checkpoint" or "checkpoint_tense" => "res://Assets/audio/music_checkpoint_tense.ogg",
            "lab" or "lab_pulse" => "res://Assets/audio/music_lab_pulse.ogg",
            "archive" or "archive_ghost" => "res://Assets/audio/music_archive_ghost.ogg",
            "subbasement" or "sub_revelation" => "res://Assets/audio/music_sub_revelation.ogg",
            "ending_escape" => "res://Assets/audio/music_ending_escape.ogg",
            "ending_truth" => "res://Assets/audio/music_ending_truth.ogg",
            _ when trackId.StartsWith("res://") => trackId,
            _ => AmbientMusicPath
        };

        var stream = LoadMusicStream(path);
        if (stream == null)
        {
            GD.PrintErr($"[AudioManager] Could not load music stream for track '{trackId}' at '{path}'.");
            return;
        }

        _currentTrackId = trackId;

        var incomingPlayer = (_activeMusicPlayer == _musicPlayerA) ? _musicPlayerB : _musicPlayerA;
        var outgoingPlayer = _activeMusicPlayer;

        incomingPlayer.Stream = stream;
        incomingPlayer.VolumeDb = -80.0f;
        incomingPlayer.Play();

        _musicFadeTween?.Kill();
        _musicFadeTween = CreateTween();
        _musicFadeTween.SetParallel(true);

        _musicFadeTween.TweenProperty(incomingPlayer, "volume_db", DefaultMusicVolumeDb, fadeDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        if (outgoingPlayer != null && outgoingPlayer.Playing)
        {
            _musicFadeTween.TweenProperty(outgoingPlayer, "volume_db", -80.0f, fadeDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
        }

        _musicFadeTween.Chain().TweenCallback(Callable.From(() => ClearPlayback(outgoingPlayer)));
        _activeMusicPlayer = incomingPlayer;
    }

    public void StopMusic(float fadeDuration = 1.5f)
    {
        _currentTrackId = "";
        _musicFadeTween?.Kill();
        if ((_musicPlayerA == null || !_musicPlayerA.Playing) && (_musicPlayerB == null || !_musicPlayerB.Playing))
        {
            ClearPlayback(_musicPlayerA); ClearPlayback(_musicPlayerB);
            _activeMusicPlayer = null;
            return;
        }
        _musicFadeTween = CreateTween().SetParallel(true);
        foreach (var player in new[] { _musicPlayerA, _musicPlayerB })
            if (player != null && player.Playing)
                _musicFadeTween.TweenProperty(player, "volume_db", -80.0f, Mathf.Max(0, fadeDuration));
        _musicFadeTween.Chain().TweenCallback(Callable.From(() =>
        {
            ClearPlayback(_musicPlayerA); ClearPlayback(_musicPlayerB);
            _activeMusicPlayer = null;
        }));
    }

    public void StopAllPlayback()
    {
        _musicFadeTween?.Kill();
        _musicFadeTween = null;
        ClearPlayback(_musicPlayerA); ClearPlayback(_musicPlayerB);
        ClearPlayback(_uiPlayer); ClearPlayback(_alarmPlayer);
        foreach (var player in _spatialPlayers)
        {
            if (!GodotObject.IsInstanceValid(player)) continue;
            player.Stop(); player.Stream = null;
        }
        _activeMusicPlayer = null;
        _currentTrackId = "";
    }

    private static void ClearPlayback(AudioStreamPlayer player)
    {
        if (!GodotObject.IsInstanceValid(player)) return;
        player.Stop();
        player.Stream = null;
    }

    private static AudioStream LoadMusicStream(string resPath)
    {
        AudioStream stream = null;

        if (resPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                stream = AudioStreamOggVorbis.LoadFromFile(resPath);
            }
            catch { }
        }

        if (stream == null)
        {
            try
            {
                stream = GD.Load<AudioStream>(resPath);
            }
            catch { }
        }

        if (stream is AudioStreamOggVorbis ogg)
        {
            ogg.Loop = true;
        }

        return stream;
    }

    private void GenerateSoundCache()
    {
        _soundCache[SoundType.RadioChime] = SynthesizeRadioChime();
        _soundCache[SoundType.DoorOpen] = SynthesizeHydraulicDoor();
        _soundCache[SoundType.GeneratorStart] = SynthesizeGeneratorStart();
        _soundCache[SoundType.AlarmKlaxon] = SynthesizeAlarmKlaxon();
        _soundCache[SoundType.TerminalData] = SynthesizeTerminalData();

        // 4 distinct Pistol gunshot audio profiles
        _soundCache[SoundType.PistolShot1] = SynthesizeGunshot(101, 330f, 0.18f, 0.50f, 0.65f, 24f);
        _soundCache[SoundType.PistolShot2] = SynthesizeGunshot(202, 305f, 0.19f, 0.55f, 0.70f, 22f);
        _soundCache[SoundType.PistolShot3] = SynthesizeGunshot(303, 350f, 0.17f, 0.48f, 0.60f, 26f);
        _soundCache[SoundType.PistolShot4] = SynthesizeGunshot(404, 318f, 0.18f, 0.52f, 0.68f, 23f);

        // 3 distinct Shotgun blast audio profiles
        _soundCache[SoundType.ShotgunShot1] = SynthesizeGunshot(505, 175f, 0.35f, 0.88f, 0.78f, 16f);
        _soundCache[SoundType.ShotgunShot2] = SynthesizeGunshot(606, 195f, 0.32f, 0.92f, 0.82f, 18f);
        _soundCache[SoundType.ShotgunShot3] = SynthesizeGunshot(707, 160f, 0.36f, 0.85f, 0.75f, 15f);

        // 4 distinct Rifle shot audio profiles
        _soundCache[SoundType.RifleShot1] = SynthesizeGunshot(808, 450f, 0.15f, 0.58f, 0.58f, 32f);
        _soundCache[SoundType.RifleShot2] = SynthesizeGunshot(909, 420f, 0.16f, 0.62f, 0.62f, 30f);
        _soundCache[SoundType.RifleShot3] = SynthesizeGunshot(1010, 475f, 0.14f, 0.56f, 0.55f, 34f);
        _soundCache[SoundType.RifleShot4] = SynthesizeGunshot(1111, 435f, 0.15f, 0.60f, 0.60f, 31f);

        _soundCache[SoundType.ZombieGroan] = SynthesizeZombieGroan(110f, 0.65f);
        _soundCache[SoundType.BossRoar] = SynthesizeZombieGroan(65f, 1.2f, true);
        _soundCache[SoundType.PickupCollect] = SynthesizePickupChime();
        _soundCache[SoundType.PlayerHurt] = SynthesizeImpactThud();
        _soundCache[SoundType.ItemUnlock] = SynthesizeClick();
        _soundCache[SoundType.FinisherKick] = SynthesizeFinisherKick();
        _soundCache[SoundType.FinisherSlash] = SynthesizeFinisherSlash();
        _soundCache[SoundType.EnemyStunned] = SynthesizeEnemyStunned();
        _soundCache[SoundType.FinisherPrompt] = SynthesizeFinisherPrompt();
        _soundCache[SoundType.VentBreak] = SynthesizeVentBreak();
        _soundCache[SoundType.MedicalHeal] = SynthesizeMedicalHeal();
        _soundCache[SoundType.PadlockClick] = SynthesizePadlockClick();
        _soundCache[SoundType.PadlockUnlock] = SynthesizePadlockUnlock();
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

    private static readonly SoundType[] PistolShots = { SoundType.PistolShot1, SoundType.PistolShot2, SoundType.PistolShot3, SoundType.PistolShot4 };
    private static readonly SoundType[] ShotgunShots = { SoundType.ShotgunShot1, SoundType.ShotgunShot2, SoundType.ShotgunShot3 };
    private static readonly SoundType[] RifleShots = { SoundType.RifleShot1, SoundType.RifleShot2, SoundType.RifleShot3, SoundType.RifleShot4 };

    public void PlayGunshot(string weaponId, Vector2 worldPos)
    {
        var pool = weaponId switch
        {
            "shotgun" => ShotgunShots,
            "rifle" => RifleShots,
            _ => PistolShots
        };

        SoundType picked = pool[(int)(GD.Randi() % pool.Length)];
        float pitch = (float)GD.RandRange(0.93, 1.07);
        PlaySound(picked, worldPos, pitch);
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

    private static AudioStreamWav SynthesizeGunshot(int seed, float baseFreq, float duration, float punch, float noiseRatio, float decayRate)
    {
        int totalSamples = (int)(SampleRate * duration);
        short[] samples = new short[totalSamples];
        var rng = new Random(seed);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * decayRate);
            float noise = ((float)rng.NextDouble() * 2.0f - 1.0f);
            float freq = baseFreq * Mathf.Exp(-t * 28.0f);
            float body = Mathf.Sin(Mathf.Tau * freq * t);
            float click = (t < 0.015f) ? Mathf.Sin(Mathf.Tau * 2200f * t) * 0.35f : 0f;
            float val = (noise * noiseRatio + body * (1f - noiseRatio) + click) * env * punch;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 27000);
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

    private static AudioStreamWav SynthesizeFinisherKick()
    {
        // Heavy RE4 roundhouse kick impact: sub-bass thud + crunching burst
        int totalSamples = (int)(SampleRate * 0.38f);
        short[] samples = new short[totalSamples];
        var rng = new Random(9912);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * 14.0f);
            float subBass = Mathf.Sin(Mathf.Tau * (70.0f * Mathf.Exp(-t * 12.0f)) * t);
            float noise = ((float)rng.NextDouble() * 2.0f - 1.0f) * Mathf.Exp(-t * 30.0f);
            float body = Mathf.Sin(Mathf.Tau * 120.0f * t) * Mathf.Exp(-t * 22.0f);
            float val = (subBass * 0.65f + noise * 0.45f + body * 0.3f) * env;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 31000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeFinisherSlash()
    {
        // Sharp tactical knife execution: high metallic whoosh + wet laceration
        int totalSamples = (int)(SampleRate * 0.28f);
        short[] samples = new short[totalSamples];
        var rng = new Random(5541);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * 16.0f);
            float freq = 450.0f + 1600.0f * (1.0f - Mathf.Exp(-t * 20.0f));
            float bladeTone = Mathf.Sin(Mathf.Tau * freq * t);
            float fleshTear = ((float)rng.NextDouble() * 2.0f - 1.0f) * Mathf.Exp(-t * 25.0f);
            float val = (bladeTone * 0.55f + fleshTear * 0.45f) * env;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 28000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeEnemyStunned()
    {
        // Dazed high-pitched electric dizziness ringing
        int totalSamples = (int)(SampleRate * 0.45f);
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * 6.5f);
            float tremolo = 1.0f + 0.35f * Mathf.Sin(Mathf.Tau * 18.0f * t);
            float tone1 = Mathf.Sin(Mathf.Tau * 880.0f * t);
            float tone2 = Mathf.Sin(Mathf.Tau * 660.0f * t);
            float val = ((tone1 + tone2) * 0.5f) * tremolo * env;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 20000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeFinisherPrompt()
    {
        // RE4 tactical action button alert chime (B5 to E6 ascending)
        int totalSamples = (int)(SampleRate * 0.22f);
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float freq = t < 0.10f ? 987.77f : 1318.51f; // B5 -> E6
            float env = Mathf.Exp(-((t % 0.10f) * 22.0f));
            float wave = Mathf.Sin(Mathf.Tau * freq * t);
            float harmonic = Mathf.Sin(Mathf.Tau * freq * 2.0f * t) * 0.3f;
            float val = (wave + harmonic) * env * 0.8f;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 22000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeVentBreak()
    {
        // Metallic vent grate bang + pressurized duct steam hiss
        int totalSamples = (int)(SampleRate * 0.28f);
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * 14.0f);
            float metalClang = Mathf.Sin(Mathf.Tau * 380f * t) * 0.5f + Mathf.Sin(Mathf.Tau * 820f * t) * 0.35f;
            float noise = ((float)GD.RandRange(-1.0, 1.0)) * Mathf.Exp(-t * 8.0f) * 0.45f;
            float val = (metalClang + noise) * env;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 24000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizeMedicalHeal()
    {
        // Medical treatment: gentle antiseptic aerosol spray hiss followed by warm restorative tri-tone chime
        int totalSamples = (int)(SampleRate * 0.48f);
        short[] samples = new short[totalSamples];
        var rng = new Random(8812);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float sprayEnv = Mathf.Exp(-t * 9.0f);
            float spray = ((float)rng.NextDouble() * 2.0f - 1.0f) * sprayEnv * 0.35f;

            // Restorative ascending arpeggio (E5: 659.25Hz -> G#5: 830.61Hz -> B5: 987.77Hz)
            float chimeTime = Mathf.Max(0.0f, t - 0.08f);
            float chimeEnv = Mathf.Exp(-chimeTime * 6.0f);
            float chimeFreq = chimeTime < 0.12f ? 659.25f : (chimeTime < 0.24f ? 830.61f : 987.77f);
            float chime = Mathf.Sin(Mathf.Tau * chimeFreq * chimeTime) * chimeEnv * 0.55f;

            float val = spray + chime;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 26000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizePadlockClick()
    {
        // Crisp antique brass tumbler rotation detent click
        int totalSamples = (int)(SampleRate * 0.07f);
        short[] samples = new short[totalSamples];
        var rng = new Random(3419);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * 90.0f);
            float transient = ((float)rng.NextDouble() * 2.0f - 1.0f) * Mathf.Exp(-t * 220.0f);
            float tone1 = Mathf.Sin(Mathf.Tau * 2400.0f * t);
            float tone2 = Mathf.Sin(Mathf.Tau * 850.0f * t);
            float val = (tone1 * 0.45f + tone2 * 0.35f + transient * 0.4f) * env;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 26000);
        }
        return BuildWav(samples);
    }

    private static AudioStreamWav SynthesizePadlockUnlock()
    {
        // Heavy antique iron shackle pop release and satisfying latch clack
        int totalSamples = (int)(SampleRate * 0.42f);
        short[] samples = new short[totalSamples];
        var rng = new Random(8821);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            // Stage 1: Sharp spring trigger pop
            float snapEnv = Mathf.Exp(-t * 70.0f);
            float snap = (Mathf.Sin(Mathf.Tau * 1650.0f * t) * 0.6f + ((float)rng.NextDouble() * 2.0f - 1.0f) * 0.5f) * snapEnv;

            // Stage 2: Heavy iron shackle pop release
            float shackleVal = 0.0f;
            if (t >= 0.035f)
            {
                float t2 = t - 0.035f;
                float shackleEnv = Mathf.Exp(-t2 * 14.0f);
                float lowClunk = Mathf.Sin(Mathf.Tau * 190.0f * t2) * 0.7f;
                float ironRing = Mathf.Sin(Mathf.Tau * 620.0f * t2) * 0.35f;
                shackleVal = (lowClunk + ironRing) * shackleEnv;
            }

            float val = snap * 0.55f + shackleVal * 0.65f;
            samples[i] = (short)(Mathf.Clamp(val, -1f, 1f) * 29000);
        }
        return BuildWav(samples);
    }
}
