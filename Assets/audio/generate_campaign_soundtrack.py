import wave as wave_module
import struct
import math
import random
import subprocess
import os

SAMPLE_RATE = 44100
TWO_PI = 2.0 * math.pi

def note_freq(semitones_from_a4):
    return 440.0 * (2.0 ** (semitones_from_a4 / 12.0))

def export_ogg(filename, left, right, duration):
    total_samples = int(duration * SAMPLE_RATE)
    max_amp = 0.0001
    for i in range(total_samples):
        max_amp = max(max_amp, abs(left[i]), abs(right[i]))
        
    gain = 0.88 / max_amp if max_amp > 0 else 1.0
    out_samples = bytearray(total_samples * 4)
    
    for i in range(total_samples):
        sl = math.tanh(left[i] * gain)
        sr = math.tanh(right[i] * gain)
        val_l = max(-32767, min(32767, int(sl * 32767.0)))
        val_r = max(-32767, min(32767, int(sr * 32767.0)))
        struct.pack_into("<hh", out_samples, i * 4, val_l, val_r)

    wav_path = f"Assets/audio/{filename}.wav"
    ogg_path = f"Assets/audio/{filename}.ogg"
    
    with wave_module.open(wav_path, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(out_samples)
        
    subprocess.run(["ffmpeg", "-y", "-i", wav_path, "-c:a", "libvorbis", "-q:a", "4", ogg_path], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if os.path.exists(wav_path):
        os.remove(wav_path)
    print(f"Generated {ogg_path} ({os.path.getsize(ogg_path)} bytes)")

def synth_gate_dread():
    duration = 24.0
    samples = int(duration * SAMPLE_RATE)
    left = [0.0] * samples
    right = [0.0] * samples
    
    f0 = 48.0 # Low G drone
    for s in range(samples):
        t = s / SAMPLE_RATE
        # Wind / filter noise
        noise = (random.random() * 2.0 - 1.0) * 0.12 * (0.8 + 0.2 * math.sin(t * 0.5))
        # Deep dark drone
        drone = math.sin(TWO_PI * f0 * t) * 0.4 + math.sin(TWO_PI * (f0 * 1.498) * t) * 0.2
        # Eerie high overtone
        whistle = math.sin(TWO_PI * (f0 * 8.02) * t + math.sin(t * 2.0)) * 0.05
        left[s] = drone + noise * 0.9 + whistle
        right[s] = drone * 0.95 + noise * 1.1 - whistle * 0.8
        
    export_ogg("music_gate_dread", left, right, duration)

def synth_checkpoint_tense():
    duration = 20.0
    samples = int(duration * SAMPLE_RATE)
    left = [0.0] * samples
    right = [0.0] * samples
    
    bpm = 84.0
    beat_sec = 60.0 / bpm
    for s in range(samples):
        t = s / SAMPLE_RATE
        beat_phase = (t % beat_sec) / beat_sec
        pulse = math.exp(-beat_phase * 6.0) * math.sin(TWO_PI * 55.0 * t) * 0.35
        # Alarm shimmer
        alarm = math.sin(TWO_PI * (880.0 + math.sin(t * 1.5) * 60.0) * t) * 0.04
        # Dissonant piano-like chord
        chord = (math.sin(TWO_PI * 146.8 * t) + math.sin(TWO_PI * 174.6 * t) + math.sin(TWO_PI * 207.65 * t)) * 0.15
        left[s] = pulse + alarm + chord
        right[s] = pulse + alarm * 0.8 + chord * 0.9
        
    export_ogg("music_checkpoint_tense", left, right, duration)

def synth_lab_pulse():
    duration = 20.0
    samples = int(duration * SAMPLE_RATE)
    left = [0.0] * samples
    right = [0.0] * samples
    
    bpm = 60.0
    beat_sec = 60.0 / bpm
    for s in range(samples):
        t = s / SAMPLE_RATE
        # Heartbeat double pulse
        phase = t % beat_sec
        p1 = math.exp(-phase * 16.0) * math.sin(TWO_PI * 52.0 * t) if phase < 0.2 else 0.0
        phase2 = phase - 0.25
        p2 = math.exp(-phase2 * 18.0) * math.sin(TWO_PI * 46.0 * t) * 0.8 if 0.0 <= phase2 < 0.2 else 0.0
        # Sterile high buzz
        buzz = math.sin(TWO_PI * 1200.0 * t) * 0.015 * (1.0 + math.sin(t * 3.0) * 0.5)
        # Low drone
        drone = math.sin(TWO_PI * 65.4 * t) * 0.2
        left[s] = (p1 + p2) * 0.5 + buzz + drone
        right[s] = (p1 + p2) * 0.5 - buzz + drone
        
    export_ogg("music_lab_pulse", left, right, duration)

def synth_archive_ghost():
    duration = 24.0
    samples = int(duration * SAMPLE_RATE)
    left = [0.0] * samples
    right = [0.0] * samples
    
    for s in range(samples):
        t = s / SAMPLE_RATE
        # Gramophone crackle
        crackle = (random.random() * 2.0 - 1.0) * 0.02 if random.random() < 0.08 else 0.0
        # Eerie high minor strings
        s1 = math.sin(TWO_PI * 349.2 * t + math.sin(t * 4.0) * 0.5) * 0.12 # F4
        s2 = math.sin(TWO_PI * 440.0 * t + math.sin(t * 3.5) * 0.5) * 0.10 # A4
        s3 = math.sin(TWO_PI * 523.2 * t + math.sin(t * 3.0) * 0.5) * 0.08 # C5
        # Drone
        bass = math.sin(TWO_PI * 87.3 * t) * 0.25
        left[s] = s1 + s2 + s3 + bass + crackle
        right[s] = s1 * 0.9 + s2 * 1.1 + s3 * 0.85 + bass - crackle
        
    export_ogg("music_archive_ghost", left, right, duration)

def synth_sub_revelation():
    duration = 20.0
    samples = int(duration * SAMPLE_RATE)
    left = [0.0] * samples
    right = [0.0] * samples
    
    for s in range(samples):
        t = s / SAMPLE_RATE
        # Low cello swell
        cello = math.sin(TWO_PI * 65.4 * t) * (0.2 + 0.15 * math.sin(t * 0.8))
        # Tritone dissonant tension (D - Ab)
        dissonance = math.sin(TWO_PI * 293.7 * t) * 0.15 + math.sin(TWO_PI * 415.3 * t) * 0.14
        left[s] = cello + dissonance
        right[s] = cello * 0.95 + dissonance * 1.05
        
    export_ogg("music_sub_revelation", left, right, duration)

def synth_boss_unit9():
    duration = 20.0
    samples = int(duration * SAMPLE_RATE)
    left = [0.0] * samples
    right = [0.0] * samples
    
    bpm = 135.0
    beat_sec = 60.0 / bpm
    sixteenth = beat_sec / 4.0
    
    for s in range(samples):
        t = s / SAMPLE_RATE
        beat_p = (t % beat_sec) / beat_sec
        # Kick on every beat
        kick = math.exp(-beat_p * 14.0) * math.sin(TWO_PI * (60.0 - beat_p * 25.0) * t) * 0.45
        # Driving 16th synth bass
        step = int((t % (beat_sec * 4.0)) / sixteenth) % 16
        pitch = 36.7 if step % 2 == 0 else (43.65 if step in (4, 12) else 38.89)
        bass = math.sin(TWO_PI * pitch * t) * 0.35
        # Alarm siren
        siren = math.sin(TWO_PI * (440.0 + math.sin(t * 5.0) * 120.0) * t) * 0.08
        left[s] = kick + bass + siren
        right[s] = kick + bass * 0.95 - siren * 0.8
        
    export_ogg("music_boss_unit9", left, right, duration)

def synth_ending_escape():
    duration = 20.0
    samples = int(duration * SAMPLE_RATE)
    left = [0.0] * samples
    right = [0.0] * samples
    
    # Warm, hopeful major / minor chord progression (D - F#m - G - A)
    for s in range(samples):
        t = s / SAMPLE_RATE
        p1 = math.sin(TWO_PI * 293.7 * t) * 0.15 # D4
        p2 = math.sin(TWO_PI * 369.99 * t) * 0.12 # F#4
        p3 = math.sin(TWO_PI * 440.0 * t) * 0.14 # A4
        bass = math.sin(TWO_PI * 73.4 * t) * 0.22 # D2
        left[s] = p1 + p2 + p3 + bass
        right[s] = p1 * 0.9 + p2 * 1.1 + p3 * 0.95 + bass
        
    export_ogg("music_ending_escape", left, right, duration)

def synth_ending_truth():
    duration = 20.0
    samples = int(duration * SAMPLE_RATE)
    left = [0.0] * samples
    right = [0.0] * samples
    
    # Dark unresolved conspiracy strings
    for s in range(samples):
        t = s / SAMPLE_RATE
        p1 = math.sin(TWO_PI * 220.0 * t) * 0.18 # A3
        p2 = math.sin(TWO_PI * 261.63 * t) * 0.15 # C4
        p3 = math.sin(TWO_PI * 311.13 * t) * 0.12 # Eb4 (diminished)
        bass = math.sin(TWO_PI * 55.0 * t) * 0.25
        left[s] = p1 + p2 + p3 + bass
        right[s] = p1 * 1.05 + p2 * 0.95 + p3 * 1.1 + bass
        
    export_ogg("music_ending_truth", left, right, duration)

def synth_intro_military():
    duration = 20.0
    samples = int(duration * SAMPLE_RATE)
    left = [0.0] * samples
    right = [0.0] * samples
    
    bpm = 90.0
    beat_sec = 60.0 / bpm
    for s in range(samples):
        t = s / SAMPLE_RATE
        beat_p = (t % beat_sec) / beat_sec
        # Marching acoustic snare/thunder
        snare = ((random.random() * 2.0 - 1.0) * math.exp(-beat_p * 12.0) * 0.25) if beat_p < 0.25 else 0.0
        # Low brass pulse
        brass = math.sin(TWO_PI * 55.0 * t) * 0.3 * (0.7 + 0.3 * math.sin(t * 2.0))
        # Wind rain noise
        rain = (random.random() * 2.0 - 1.0) * 0.06
        left[s] = snare + brass + rain
        right[s] = snare * 0.9 + brass * 1.05 + rain
        
    export_ogg("music_intro_military", left, right, duration)

if __name__ == "__main__":
    os.makedirs("Assets/audio", exist_ok=True)
    synth_intro_military()
    synth_gate_dread()
    synth_checkpoint_tense()
    synth_lab_pulse()
    synth_archive_ghost()
    synth_sub_revelation()
    synth_boss_unit9()
    synth_ending_escape()
    synth_ending_truth()
    print("All campaign soundtracks generated successfully!")
