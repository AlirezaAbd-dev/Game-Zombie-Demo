"""Deterministic original mechanical foley: handling, catches, magazine seating and actions."""
from pathlib import Path
import math
import random
import struct
import wave

ROOT = Path(__file__).resolve().parent
RATE = 44100

def render(name, duration, seed, events):
    rng = random.Random(seed)
    samples = [0.0] * int(duration * RATE)
    for start, length, gain, frequency, scrape in events:
        previous = 0.0
        for i in range(int(length * RATE)):
            index = int(start * RATE) + i
            if index >= len(samples): break
            t = i / RATE
            noise = rng.uniform(-1, 1)
            filtered = noise - previous * .75
            previous = noise
            envelope = (math.sin(math.pi * t / length) ** .7 if scrape else math.exp(-t * 48))
            attack = min(1, t * 2500)
            metal = math.sin(2 * math.pi * frequency * t) * math.exp(-t * 70)
            body = math.sin(2 * math.pi * frequency * .29 * t) * math.exp(-t * 30)
            samples[index] += gain * attack * envelope * (.5 * filtered + .25 * metal + .25 * body)
    with wave.open(str(ROOT / (name + '.wav')), 'wb') as output:
        output.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        output.writeframes(b''.join(struct.pack('<h', int(max(-.95, min(.95, x)) * 32767)) for x in samples))

for index, weapon in enumerate(('pistol', 'shotgun', 'rifle')):
    pitch = (1400, 840, 1100)[index]
    opening, insertion, closing = ((.55, 1.1, .55), (.45, .65, .55), (.775, 1.55, .775))[index]
    render(weapon + '_open', opening - .02, 10 + index,
           [(0,.08,.55,pitch,False),(.06,opening*.5,.16,pitch,True),(opening*.7,.08,.3,pitch*.7,False)])
    render(weapon + '_insert', insertion - .02, 20 + index,
           [(.03,insertion*.55,.13,pitch,True),(insertion*.72,.09,.75,pitch*.6,False),(insertion*.84,.07,.32,pitch,False)])
    render(weapon + '_close', closing - .02, 30 + index,
           [(0,closing*.45,.25,pitch,True),(closing*.46,.09,.65,pitch*.7,False),(closing*.73,.09,.8,pitch,False)])
