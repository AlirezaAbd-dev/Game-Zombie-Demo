using System;
using System.Collections.Generic;
using Godot;

public partial class AudioManager : Node
{
    public static AudioManager? Instance { get; private set; }

    private readonly Dictionary<string, AudioStreamWav> _cachedWaves = new();
    private readonly List<AudioStreamPlayer2D> _pool2D = new();
    private readonly List<AudioStreamPlayer> _poolGlobal = new();
    private int _index2D = 0;
    private int _indexGlobal = 0;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        for (int i = 0; i < 16; i++)
        {
            var p2D = new AudioStreamPlayer2D
            {
                MaxDistance = 1400f,
                Attenuation = 1.5f,
                ProcessMode = ProcessModeEnum.Always
            };
            AddChild(p2D);
            _pool2D.Add(p2D);
        }

        for (int i = 0; i < 4; i++)
        {
            var pGlobal = new AudioStreamPlayer
            {
                ProcessMode = ProcessModeEnum.Always
            };
            AddChild(pGlobal);
            _poolGlobal.Add(pGlobal);
        }

        PrewarmWaves();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    private void PrewarmWaves()
    {
        // 1. SfxPistol
        _cachedWaves["SfxPistol"] = BuildWave(0.11f, t =>
        {
            float noise = (float)GD.RandRange(-1.0, 1.0);
            return (noise * Mathf.Exp(-t * 35f) * 0.7f) + (Mathf.Sin(Mathf.Tau * (180f * Mathf.Exp(-t * 25f)) * t) * 0.5f);
        });

        // 2. SfxShotgun
        _cachedWaves["SfxShotgun"] = BuildWave(0.22f, t =>
        {
            float noise = (float)GD.RandRange(-1.0, 1.0);
            return (noise * Mathf.Exp(-t * 18f) * 0.85f) + (Mathf.Sin(Mathf.Tau * (110f * Mathf.Exp(-t * 15f)) * t) * 0.6f);
        });

        // 3. SfxSmg
        _cachedWaves["SfxSmg"] = BuildWave(0.08f, t =>
        {
            float noise = (float)GD.RandRange(-1.0, 1.0);
            return (noise * Mathf.Exp(-t * 45f) * 0.6f) + (Mathf.Sin(Mathf.Tau * (240f * Mathf.Exp(-t * 30f)) * t) * 0.4f);
        });

        // 4. SfxReload
        _cachedWaves["SfxReload"] = BuildWave(0.14f, t =>
        {
            float localT = t < 0.08f ? t : (t - 0.08f);
            return Mathf.Sin(Mathf.Tau * 1400f * t) * Mathf.Exp(-localT * 80f);
        });

        // 5. SfxDryFire
        _cachedWaves["SfxDryFire"] = BuildWave(0.05f, t =>
        {
            return Mathf.Sin(Mathf.Tau * 900f * t) * Mathf.Exp(-t * 95f);
        });

        // 6. SfxExplosion
        _cachedWaves["SfxExplosion"] = BuildWave(0.55f, t =>
        {
            float noise = (float)GD.RandRange(-1.0, 1.0);
            return (noise * Mathf.Exp(-t * 6f) * 0.6f) + (Mathf.Sin(Mathf.Tau * (95f * Mathf.Exp(-t * 8f)) * t) * Mathf.Exp(-t * 4.5f) * 0.8f);
        });

        // 7. SfxDash
        _cachedWaves["SfxDash"] = BuildWave(0.16f, t =>
        {
            float noise = (float)GD.RandRange(-1.0, 1.0);
            return noise * Mathf.Sin(Mathf.Pi * (t / 0.16f)) * 0.45f;
        });

        // 8. SfxZombieHit
        _cachedWaves["SfxZombieHit"] = BuildWave(0.09f, t =>
        {
            return Mathf.Sin(Mathf.Tau * (140f * Mathf.Exp(-t * 30f)) * t) * Mathf.Exp(-t * 35f);
        });

        // 9. SfxPickup
        _cachedWaves["SfxPickup"] = BuildWave(0.18f, t =>
        {
            float freq = t < 0.09f ? 587.33f : 880.0f;
            float localT = t < 0.09f ? t : (t - 0.09f);
            return Mathf.Sin(Mathf.Tau * freq * t) * Mathf.Exp(-localT * 18f);
        });
    }

    private static AudioStreamWav BuildWave(float duration, Func<float, float> generator, int sampleRate = 22050)
    {
        int totalSamples = (int)(duration * sampleRate);
        byte[] buffer = new byte[totalSamples * 2];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sampleVal = Mathf.Clamp(generator(t), -1.0f, 1.0f);
            short pcm = (short)(sampleVal * 32767f);
            buffer[i * 2] = (byte)(pcm & 0xFF);
            buffer[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        var wav = new AudioStreamWav
        {
            Data = buffer,
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = false
        };
        return wav;
    }

    public void PlayAt(string key, Vector2 globalPos, float pitchMin = 0.92f, float pitchMax = 1.08f, float volumeDb = 0f)
    {
        if (!_cachedWaves.TryGetValue(key, out var wave))
            return;

        AudioStreamPlayer2D? target = null;
        for (int i = 0; i < _pool2D.Count; i++)
        {
            int idx = (_index2D + i) % _pool2D.Count;
            if (!_pool2D[idx].Playing)
            {
                target = _pool2D[idx];
                _index2D = (idx + 1) % _pool2D.Count;
                break;
            }
        }

        if (target == null)
        {
            target = _pool2D[_index2D];
            _index2D = (_index2D + 1) % _pool2D.Count;
        }

        target.GlobalPosition = globalPos;
        target.Stream = wave;
        target.PitchScale = (float)GD.RandRange(pitchMin, pitchMax);
        target.VolumeDb = volumeDb;
        target.Play();
    }

    public void PlayGlobal(string key, float pitch = 1.0f, float volumeDb = 0f)
    {
        if (!_cachedWaves.TryGetValue(key, out var wave))
            return;

        AudioStreamPlayer? target = null;
        for (int i = 0; i < _poolGlobal.Count; i++)
        {
            int idx = (_indexGlobal + i) % _poolGlobal.Count;
            if (!_poolGlobal[idx].Playing)
            {
                target = _poolGlobal[idx];
                _indexGlobal = (idx + 1) % _poolGlobal.Count;
                break;
            }
        }

        if (target == null)
        {
            target = _poolGlobal[_indexGlobal];
            _indexGlobal = (_indexGlobal + 1) % _poolGlobal.Count;
        }

        target.Stream = wave;
        target.PitchScale = pitch;
        target.VolumeDb = volumeDb;
        target.Play();
    }
}
