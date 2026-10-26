"""Generate the original, seamless Vulture-9 rotor loop (standard library only)."""
from pathlib import Path
import math
import struct
import wave

rate = 22050
samples = []
for index in range(rate * 2):
    t = index / rate
    pulse = (0.55 + 0.45 * math.cos(2 * math.pi * 18 * t)) ** 3
    engine = 0.5 * math.sin(2 * math.pi * 35 * t) + 0.22 * math.sin(2 * math.pi * 70 * t)
    engine += 0.08 * math.sin(2 * math.pi * 210 * t)
    air = sum(math.sin(2 * math.pi * frequency * t + frequency * 0.13) for frequency in (311, 457, 613, 829, 1061)) / 5
    samples.append(int(32767 * (engine * (0.3 + 0.7 * pulse) * 0.55 + air * 0.06)))
with wave.open(str(Path(__file__).with_name('helicopter_rotor.wav')), 'wb') as stream:
    stream.setnchannels(1)
    stream.setsampwidth(2)
    stream.setframerate(rate)
    stream.writeframes(struct.pack('<' + 'h' * len(samples), *samples))
