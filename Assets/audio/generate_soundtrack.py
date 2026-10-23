import wave as wave_module
import struct
import math
import random
import subprocess
import os

SAMPLE_RATE = 44100
TWO_PI = 2.0 * math.pi

def note_freq(semitones_from_a4):
    """Calculate frequency given semitones from A4 (440Hz)."""
    return 440.0 * (2.0 ** (semitones_from_a4 / 12.0))

# Note definitions (semitones from A4=0)
# A4 = 0, C5 = 3, D5 = 5, E5 = 7, F5 = 8, G5 = 10, A5 = 12
# D4 = -7, F4 = -4, A3 = -12, D3 = -19, Bb2 = -22, A2 = -24, D2 = -31
NOTE_D1 = note_freq(-43) # ~36.7 Hz
NOTE_Bb0 = note_freq(-46) # ~30.8 Hz
NOTE_C1 = note_freq(-45) # ~32.7 Hz
NOTE_A0 = note_freq(-48) # ~27.5 Hz

NOTE_D2 = note_freq(-31) # 73.4 Hz
NOTE_F2 = note_freq(-28) # 87.3 Hz
NOTE_G2 = note_freq(-26) # 98.0 Hz
NOTE_A2 = note_freq(-24) # 110.0 Hz
NOTE_Bb2 = note_freq(-22) # 123.5 Hz
NOTE_C3 = note_freq(-21) # 130.8 Hz
NOTE_D3 = note_freq(-19) # 146.8 Hz
NOTE_E3 = note_freq(-17) # 164.8 Hz
NOTE_F3 = note_freq(-16) # 174.6 Hz
NOTE_A3 = note_freq(-12) # 220.0 Hz

NOTE_D4 = note_freq(-7)  # 293.7 Hz
NOTE_E4 = note_freq(-5)  # 329.6 Hz
NOTE_F4 = note_freq(-4)  # 349.2 Hz
NOTE_G4 = note_freq(-2)  # 392.0 Hz
NOTE_Ab4 = note_freq(-1) # 415.3 Hz (Tritone dread)
NOTE_A4 = note_freq(0)   # 440.0 Hz
NOTE_Bb4 = note_freq(1)  # 466.2 Hz
NOTE_C5 = note_freq(3)   # 523.3 Hz
NOTE_D5 = note_freq(5)   # 587.3 Hz
NOTE_E5 = note_freq(7)   # 659.3 Hz
NOTE_F5 = note_freq(8)   # 698.5 Hz
NOTE_G5 = note_freq(10)  # 784.0 Hz
NOTE_A5 = note_freq(12)  # 880.0 Hz

def generate_ambient_horror():
    """Generates an 80-second looping atmospheric survival horror soundtrack."""
    bpm = 72.0
    sec_per_beat = 60.0 / bpm # ~0.8333s
    bar_sec = sec_per_beat * 4.0 # ~3.3333s
    total_bars = 24
    loop_duration = total_bars * bar_sec # exactly 80.0s
    tail_duration = 6.0 # tail for delay/reverb wrap-around
    total_duration = loop_duration + tail_duration
    
    total_samples = int(total_duration * SAMPLE_RATE)
    loop_samples = int(loop_duration * SAMPLE_RATE)
    
    left = [0.0] * total_samples
    right = [0.0] * total_samples
    
    # 1. SUB-BASS DRONE & MODULATED NOISE FLOOR
    print("Generating Sub-bass Drone & Facility Hum...")
    # Chord progression per 4 bars:
    # Bars 1-4: D1 (36.7Hz)
    # Bars 5-8: Bb0 (30.8Hz)
    # Bars 9-12: G0 / D1
    # Bars 13-16: A0 / C1
    # Bars 17-20: D1
    # Bars 21-24: Bb0 -> A0
    bass_prog = [
        (0 * bar_sec, 4 * bar_sec, NOTE_D1),
        (4 * bar_sec, 8 * bar_sec, NOTE_Bb0),
        (8 * bar_sec, 12 * bar_sec, NOTE_D1 * 0.75), # low G
        (12 * bar_sec, 16 * bar_sec, NOTE_A0),
        (16 * bar_sec, 20 * bar_sec, NOTE_D1),
        (20 * bar_sec, 22 * bar_sec, NOTE_Bb0),
        (22 * bar_sec, 24 * bar_sec, NOTE_A0),
    ]
    
    rng = random.Random(42)
    # Low-pass filter state
    lp_l, lp_r = 0.0, 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        t_loop = t % loop_duration
        
        # Current bass note
        cur_freq = NOTE_D1
        for start_t, end_t, freq in bass_prog:
            if start_t <= t_loop < end_t:
                cur_freq = freq
                break
                
        # Sub drone with stereo detune
        w_l = math.sin(TWO_PI * cur_freq * t) + 0.35 * math.sin(TWO_PI * cur_freq * 2.0 * t)
        w_r = math.sin(TWO_PI * (cur_freq * 1.003) * t) + 0.35 * math.sin(TWO_PI * (cur_freq * 2.002) * t)
        
        # Subtle sub-air hum (distant ventilation drone at 55Hz & 110Hz)
        vent_hum = 0.15 * math.sin(TWO_PI * 55.0 * t) + 0.08 * math.sin(TWO_PI * 110.0 * t)
        
        # Low rumble noise
        noise = (rng.random() * 2.0 - 1.0) * 0.04
        
        # Filter cutoff sweep
        filter_mod = 0.03 + 0.02 * math.sin(TWO_PI * (t / 16.0))
        lp_l += filter_mod * (w_l + vent_hum + noise - lp_l)
        lp_r += filter_mod * (w_r + vent_hum + noise - lp_r)
        
        left[i] += lp_l * 0.65
        right[i] += lp_r * 0.65

    # 2. SUSPENSE INDUSTRIAL HEARTBEAT PULSE
    print("Generating Industrial Heartbeat Pulse...")
    # Heartbeat occurs on beat 1 and beat 3 of each bar, with a double-thud "lub-dub"
    for b in range(total_bars):
        bar_start = b * bar_sec
        for beat in [0, 2]:
            t_hit1 = bar_start + beat * sec_per_beat
            t_hit2 = t_hit1 + 0.28 # slightly delayed second thud
            
            for t_hit, amp_mult in [(t_hit1, 1.0), (t_hit2, 0.72)]:
                idx = int(t_hit * SAMPLE_RATE)
                hit_len = int(0.45 * SAMPLE_RATE)
                for s in range(hit_len):
                    if idx + s >= total_samples: break
                    st = s / SAMPLE_RATE
                    env = math.exp(-st * 11.0)
                    # Pitch drops from 95Hz to 40Hz
                    freq = 40.0 + 55.0 * math.exp(-st * 25.0)
                    wave = math.sin(TWO_PI * freq * st)
                    # Soft distortion
                    wave = math.tanh(wave * 1.4) * 0.7
                    thud = wave * env * amp_mult * 0.42
                    left[idx + s] += thud
                    right[idx + s] += thud

    # 3. ATMOSPHERIC HORROR PADS (Cold Reverbant Strings/Brass)
    print("Generating Atmospheric Horror Pad...")
    # Pad chords (each spanning 4 bars)
    # Chord 1: Dm (D2, A2, F3)
    # Chord 2: Bbmaj7 (Bb1, F2, A2, D3)
    # Chord 3: Gm (G1, D2, Bb2, D3)
    # Chord 4: A7sus4 (A1, E2, G2, D3)
    # Chord 5: Dm (D2, A2, D3, F3)
    # Chord 6: Dm(add9) -> C#dim (dissonant climax into loop)
    pad_chords = [
        (0 * bar_sec, 4 * bar_sec, [NOTE_D2, NOTE_A2, NOTE_F3]),
        (4 * bar_sec, 8 * bar_sec, [NOTE_Bb0 * 2, NOTE_F2, NOTE_A2, NOTE_D3]),
        (8 * bar_sec, 12 * bar_sec, [NOTE_G2 * 0.5, NOTE_D2, NOTE_Bb2, NOTE_D3]),
        (12 * bar_sec, 16 * bar_sec, [NOTE_A2 * 0.5, NOTE_E3 * 0.5, NOTE_A2, NOTE_D3]),
        (16 * bar_sec, 20 * bar_sec, [NOTE_D2, NOTE_A2, NOTE_F3, NOTE_A3]),
        (20 * bar_sec, 24 * bar_sec, [NOTE_Bb0 * 2, NOTE_D2, NOTE_Ab4 * 0.5, NOTE_A2]), # Tricky tritone tension
    ]
    
    for start_t, end_t, notes in pad_chords:
        dur = end_t - start_t
        s_start = int(start_t * SAMPLE_RATE)
        s_len = int(dur * SAMPLE_RATE)
        
        for s in range(s_len):
            idx = s_start + s
            if idx >= total_samples: break
            t_note = s / SAMPLE_RATE
            
            # Pad envelope: slow attack (1.2s), slow release
            if t_note < 1.2:
                env = t_note / 1.2
            elif t_note > dur - 1.2:
                env = (dur - t_note) / 1.2
            else:
                env = 1.0
            
            pad_l, pad_r = 0.0, 0.0
            for note_idx, freq in enumerate(notes):
                # Gentle slow chorus/vibrato
                vib = 1.0 + 0.004 * math.sin(TWO_PI * 1.8 * t_note + note_idx)
                f_cur = freq * vib
                sig = math.sin(TWO_PI * f_cur * t_note) + 0.25 * math.sin(TWO_PI * f_cur * 2.0 * t_note)
                pan = 0.35 + 0.30 * (note_idx / max(1, len(notes) - 1))
                pad_l += sig * (1.0 - pan)
                pad_r += sig * pan
                
            left[idx] += pad_l * env * 0.16
            right[idx] += pad_r * env * 0.16

    # 4. HAUNTING MINOR PIANO / MUSIC BOX MOTIF (Resident Evil Style Melody)
    print("Generating Haunting Piano Melody...")
    # Sparse, beautiful, eerie single notes echoing down the dark corridors
    piano_melody = [
        # Bar 1-4
        (1 * bar_sec + 0.0, NOTE_D5, 0.85),
        (2 * bar_sec + 0.0, NOTE_F5, 0.75),
        (2 * bar_sec + 2 * sec_per_beat, NOTE_E5, 0.70),
        (3 * bar_sec + 1 * sec_per_beat, NOTE_A4, 0.90),
        
        # Bar 5-8
        (5 * bar_sec + 0.0, NOTE_D5, 0.80),
        (6 * bar_sec + 0.0, NOTE_F5, 0.75),
        (6 * bar_sec + 2 * sec_per_beat, NOTE_G5, 0.70),
        (7 * bar_sec + 1 * sec_per_beat, NOTE_E5, 0.85),
        
        # Bar 9-12 (Tension rises)
        (9 * bar_sec + 0.0, NOTE_A5, 0.90),
        (10 * bar_sec + 0.0, NOTE_G5, 0.80),
        (10 * bar_sec + 2 * sec_per_beat, NOTE_F5, 0.75),
        (11 * bar_sec + 0.0, NOTE_E5, 0.85),
        (11 * bar_sec + 2 * sec_per_beat, NOTE_Ab4, 0.70), # Eerie tritone
        
        # Bar 13-16 (Subdued despair)
        (13 * bar_sec + 0.0, NOTE_D5, 0.80),
        (14 * bar_sec + 1 * sec_per_beat, NOTE_C5, 0.70),
        (15 * bar_sec + 0.0, NOTE_A4, 0.85),
        (15 * bar_sec + 2 * sec_per_beat, NOTE_F4, 0.65),
        
        # Bar 17-20 (Melodic climax)
        (17 * bar_sec + 0.0, NOTE_D5, 0.95),
        (18 * bar_sec + 0.0, NOTE_F5, 0.85),
        (18 * bar_sec + 2 * sec_per_beat, NOTE_A5, 0.90),
        (19 * bar_sec + 1 * sec_per_beat, NOTE_G5, 0.80),
        
        # Bar 21-24 (Resolving back into dark abyss)
        (21 * bar_sec + 0.0, NOTE_F5, 0.80),
        (21 * bar_sec + 2 * sec_per_beat, NOTE_E5, 0.75),
        (22 * bar_sec + 1 * sec_per_beat, NOTE_D5, 0.85),
        (23 * bar_sec + 0.0, NOTE_A4, 0.70),
        (23 * bar_sec + 2 * sec_per_beat, NOTE_D4, 0.95),
    ]
    
    # Delay lines for piano echo
    delay_time = 0.42 # seconds
    delay_samples = int(delay_time * SAMPLE_RATE)
    delay_feedback = 0.48
    
    for note_time, freq, velocity in piano_melody:
        idx = int(note_time * SAMPLE_RATE)
        dur = 3.5 # piano ring-out duration
        dur_samples = int(dur * SAMPLE_RATE)
        
        for s in range(dur_samples):
            cur_idx = idx + s
            if cur_idx >= total_samples: break
            t = s / SAMPLE_RATE
            
            # Piano / celesta timbre: hammer impulse + fundamental + rich harmonics
            env = math.exp(-t * 2.2) + 0.3 * math.exp(-t * 6.0)
            h1 = math.sin(TWO_PI * freq * t)
            h2 = 0.38 * math.sin(TWO_PI * freq * 2.01 * t) * math.exp(-t * 3.5)
            h3 = 0.18 * math.sin(TWO_PI * freq * 3.02 * t) * math.exp(-t * 5.0)
            h4 = 0.08 * math.sin(TWO_PI * freq * 4.05 * t) * math.exp(-t * 7.5)
            
            tone = (h1 + h2 + h3 + h4) * env * velocity * 0.22
            
            # Direct hit
            left[cur_idx] += tone * 0.65
            right[cur_idx] += tone * 0.35
            
            # Stereo ping-pong echo delay
            echo1_idx = cur_idx + delay_samples
            if echo1_idx < total_samples:
                echo1 = tone * delay_feedback
                left[echo1_idx] += echo1 * 0.20
                right[echo1_idx] += echo1 * 0.80
                
            echo2_idx = cur_idx + delay_samples * 2
            if echo2_idx < total_samples:
                echo2 = tone * (delay_feedback ** 2)
                left[echo2_idx] += echo2 * 0.75
                right[echo2_idx] += echo2 * 0.25

    # 5. EERIE DISSONANT BOWED SCRAPE (Occasional distant chilling metallic swell)
    print("Generating Atmospheric Scrapes & Chills...")
    scrape_cues = [
        (7 * bar_sec, NOTE_Ab4, 4.0),
        (15 * bar_sec, NOTE_Bb4, 4.5),
        (20 * bar_sec, NOTE_Ab4 * 1.5, 3.8),
    ]
    for start_t, freq, dur in scrape_cues:
        s_start = int(start_t * SAMPLE_RATE)
        s_len = int(dur * SAMPLE_RATE)
        for s in range(s_len):
            idx = s_start + s
            if idx >= total_samples: break
            t = s / SAMPLE_RATE
            env = math.sin(math.pi * (t / dur)) ** 2
            scrape = math.sin(TWO_PI * freq * t) * math.sin(TWO_PI * (freq * 0.985) * t)
            left[idx] += scrape * env * 0.09
            right[idx] += scrape * env * 0.12

    # 6. SEAMLESS LOOP WRAP-AROUND
    print("Folding tail for seamless zero-click looping...")
    for s in range(int(tail_duration * SAMPLE_RATE)):
        src = loop_samples + s
        dst = s
        if src < total_samples and dst < loop_samples:
            left[dst] += left[src]
            right[dst] += right[src]

    # Normalize and export loop portion
    print("Exporting 80s Ambient Horror Soundtrack...")
    max_amp = 0.0001
    for i in range(loop_samples):
        max_amp = max(max_amp, abs(left[i]), abs(right[i]))
    
    gain = 0.88 / max_amp
    out_samples = bytearray(loop_samples * 4)
    
    for i in range(loop_samples):
        # Soft limiter / warm saturation
        sl = math.tanh(left[i] * gain)
        sr = math.tanh(right[i] * gain)
        
        val_l = int(sl * 32767.0)
        val_r = int(sr * 32767.0)
        
        struct.pack_into("<hh", out_samples, i * 4, val_l, val_r)

    wav_path = "Assets/audio/music_ambient_horror.wav"
    ogg_path = "Assets/audio/music_ambient_horror.ogg"
    
    with wave_module.open(wav_path, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(out_samples)
        
    print(f"Encoded WAV: {wav_path}. Converting to OGG via ffmpeg...")
    subprocess.run(["ffmpeg", "-y", "-i", wav_path, "-c:a", "libvorbis", "-q:a", "5", ogg_path], check=True)
    os.remove(wav_path)
    print(f"Generated {ogg_path} ({os.path.getsize(ogg_path)} bytes)")

def generate_boss_combat():
    """Generates a 38.4-second high-intensity tyrant combat soundtrack at 125 BPM."""
    bpm = 125.0
    sec_per_beat = 60.0 / bpm # 0.48s
    bar_sec = sec_per_beat * 4.0 # 1.92s
    total_bars = 20
    loop_duration = total_bars * bar_sec # 38.4s
    tail_duration = 3.84
    total_duration = loop_duration + tail_duration
    
    total_samples = int(total_duration * SAMPLE_RATE)
    loop_samples = int(loop_duration * SAMPLE_RATE)
    
    left = [0.0] * total_samples
    right = [0.0] * total_samples
    
    print("Generating Tyrant Boss Combat Drums & Industrial Percussion...")
    rng = random.Random(77)
    
    # 1. INDUSTRIAL WAR DRUMS (Heavy 4-on-the-floor kick, metallic snares, frantic hi-hats)
    for b in range(total_bars):
        bar_start = b * bar_sec
        for beat in range(4):
            t_beat = bar_start + beat * sec_per_beat
            idx = int(t_beat * SAMPLE_RATE)
            
            # Kick on every beat
            hit_len = int(0.35 * SAMPLE_RATE)
            for s in range(hit_len):
                if idx + s >= total_samples: break
                st = s / SAMPLE_RATE
                env = math.exp(-st * 16.0)
                freq = 45.0 + 130.0 * math.exp(-st * 32.0)
                k = math.tanh(math.sin(TWO_PI * freq * st) * 1.8) * env * 0.75
                left[idx + s] += k
                right[idx + s] += k
                
            # Snare on beats 2 and 4 (index 1 and 3)
            if beat in [1, 3]:
                snare_len = int(0.32 * SAMPLE_RATE)
                for s in range(snare_len):
                    if idx + s >= total_samples: break
                    st = s / SAMPLE_RATE
                    env = math.exp(-st * 14.0)
                    body = math.sin(TWO_PI * 190.0 * st) * 0.4
                    snap = (rng.random() * 2.0 - 1.0) * 0.7
                    metal = math.sin(TWO_PI * 840.0 * st) * 0.25
                    sn = (body + snap + metal) * env * 0.55
                    left[idx + s] += sn * 0.9
                    right[idx + s] += sn * 0.9

            # 16th note metallic hi-hats
            for sixteenth in range(4):
                t_sub = t_beat + sixteenth * (sec_per_beat / 4.0)
                sub_idx = int(t_sub * SAMPLE_RATE)
                hat_len = int(0.06 * SAMPLE_RATE)
                for s in range(hat_len):
                    if sub_idx + s >= total_samples: break
                    st = s / SAMPLE_RATE
                    env = math.exp(-st * 45.0)
                    h = (rng.random() * 2.0 - 1.0) * env * 0.18
                    pan = 0.3 if (sixteenth % 2 == 0) else 0.7
                    left[sub_idx + s] += h * (1.0 - pan)
                    right[sub_idx + s] += h * pan

    # 2. DRIVING 16TH-NOTE INDUSTRIAL BASS ARPEGGIO
    print("Generating Aggressive Bass Pulse...")
    # Bass motif: D1 - D1 - F1 - D1 - Ab1 - D1 - F1 - Eb1
    bass_notes = [NOTE_D1, NOTE_D1, NOTE_F2 * 0.5, NOTE_D1, NOTE_Ab4 * 0.125, NOTE_D1, NOTE_F2 * 0.5, NOTE_E3 * 0.25]
    for b in range(total_bars):
        bar_start = b * bar_sec
        for sixteenth in range(16):
            t_note = bar_start + sixteenth * (sec_per_beat / 4.0)
            note_freq_val = bass_notes[sixteenth % len(bass_notes)]
            idx = int(t_note * SAMPLE_RATE)
            note_len = int((sec_per_beat / 4.0) * 0.85 * SAMPLE_RATE)
            for s in range(note_len):
                if idx + s >= total_samples: break
                st = s / SAMPLE_RATE
                env = math.exp(-st * 18.0)
                saw = 2.0 * ((note_freq_val * st) % 1.0) - 1.0
                dist_saw = math.tanh(saw * 2.5) * 0.65
                sub = math.sin(TWO_PI * note_freq_val * st) * 0.5
                bass_out = (dist_saw + sub) * env * 0.38
                left[idx + s] += bass_out
                right[idx + s] += bass_out

    # 3. PANIC SIREN / DISSONANT TENSION HORNS
    print("Generating Dissonant Horror Risers & Sirens...")
    # Horn braams on bar 1, 5, 9, 13, 17
    for b in range(0, total_bars, 4):
        t_braam = b * bar_sec
        idx = int(t_braam * SAMPLE_RATE)
        dur = 3.2
        dur_samples = int(dur * SAMPLE_RATE)
        for s in range(dur_samples):
            if idx + s >= total_samples: break
            st = s / SAMPLE_RATE
            env = math.sin(math.pi * min(1.0, st / 0.3)) * math.exp(-st * 0.9)
            # Low brass braam (D2 + Tritone Ab2)
            f1, f2 = NOTE_D2, NOTE_Ab4 * 0.5
            braam = (math.sin(TWO_PI * f1 * st) + 0.6 * math.sin(TWO_PI * f2 * st))
            braam = math.tanh(braam * 2.2) * env * 0.32
            left[idx + s] += braam * 0.85
            right[idx + s] += braam * 0.85

    # 4. HIGH STRINGS COMBAT STAB MOTIF
    print("Generating Stabbing Horror Strings...")
    string_stabs = [
        # Bar 2 & 4
        (1 * bar_sec + 2 * sec_per_beat, NOTE_D5, 0.5),
        (1 * bar_sec + 3 * sec_per_beat, NOTE_Eb5 if 'NOTE_Eb5' in locals() else NOTE_E5, 0.6),
        (3 * bar_sec + 2 * sec_per_beat, NOTE_F5, 0.65),
        (3 * bar_sec + 3 * sec_per_beat, NOTE_A5, 0.75),
        # Bar 6 & 8
        (5 * bar_sec + 2 * sec_per_beat, NOTE_D5, 0.5),
        (7 * bar_sec + 2 * sec_per_beat, NOTE_Ab4, 0.7),
        # Bar 10 & 12
        (9 * bar_sec + 2 * sec_per_beat, NOTE_A5, 0.8),
        (11 * bar_sec + 2 * sec_per_beat, NOTE_G5, 0.75),
        # Bar 14 & 16
        (13 * bar_sec + 2 * sec_per_beat, NOTE_F5, 0.7),
        (15 * bar_sec + 2 * sec_per_beat, NOTE_D5, 0.8),
        # Bar 18 & 20
        (17 * bar_sec + 2 * sec_per_beat, NOTE_A5, 0.85),
        (19 * bar_sec + 1 * sec_per_beat, NOTE_Ab4 * 2.0, 0.9),
    ]
    for t_hit, freq, vel in string_stabs:
        idx = int(t_hit * SAMPLE_RATE)
        dur = 0.55
        dur_samples = int(dur * SAMPLE_RATE)
        for s in range(dur_samples):
            if idx + s >= total_samples: break
            st = s / SAMPLE_RATE
            env = math.exp(-st * 7.0)
            # Tremolo string saw
            saw = 2.0 * ((freq * st) % 1.0) - 1.0
            trem = 1.0 + 0.3 * math.sin(TWO_PI * 14.0 * st)
            stab = saw * trem * env * vel * 0.25
            left[idx + s] += stab * 0.4
            right[idx + s] += stab * 0.6

    # 5. SEAMLESS LOOP FOLDING
    print("Folding combat tail for seamless looping...")
    for s in range(int(tail_duration * SAMPLE_RATE)):
        src = loop_samples + s
        dst = s
        if src < total_samples and dst < loop_samples:
            left[dst] += left[src]
            right[dst] += right[src]

    # Normalize & export
    max_amp = 0.0001
    for i in range(loop_samples):
        max_amp = max(max_amp, abs(left[i]), abs(right[i]))
        
    gain = 0.88 / max_amp
    out_samples = bytearray(loop_samples * 4)
    
    for i in range(loop_samples):
        sl = math.tanh(left[i] * gain)
        sr = math.tanh(right[i] * gain)
        val_l = int(sl * 32767.0)
        val_r = int(sr * 32767.0)
        struct.pack_into("<hh", out_samples, i * 4, val_l, val_r)

    wav_path = "Assets/audio/music_boss_combat.wav"
    ogg_path = "Assets/audio/music_boss_combat.ogg"
    
    with wave_module.open(wav_path, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(out_samples)
        
    subprocess.run(["ffmpeg", "-y", "-i", wav_path, "-c:a", "libvorbis", "-q:a", "5", ogg_path], check=True)
    os.remove(wav_path)
    print(f"Generated {ogg_path} ({os.path.getsize(ogg_path)} bytes)")

if __name__ == "__main__":
    generate_ambient_horror()
    generate_boss_combat()
