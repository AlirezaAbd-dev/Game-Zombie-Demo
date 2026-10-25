import os
from PIL import Image, ImageDraw

PORTRAITS_DIR = "Assets/textures/portraits"
os.makedirs(PORTRAITS_DIR, exist_ok=True)

SIZE = 128

def create_base(bg_color, border_color):
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # Background circle/rounded box
    draw.rounded_rectangle([4, 4, SIZE - 5, SIZE - 5], radius=16, fill=bg_color, outline=border_color, width=3)
    return img, draw

def generate_sara():
    img, draw = create_base((15, 25, 38, 245), (70, 180, 230, 255))
    # Head & hair (dark hair, ponytail)
    draw.ellipse([42, 28, 86, 78], fill=(235, 195, 165)) # Face
    draw.ellipse([38, 22, 90, 52], fill=(25, 20, 22)) # Hair top
    draw.polygon([(36, 38), (44, 76), (36, 68)], fill=(25, 20, 22)) # Hair left
    draw.polygon([(92, 38), (84, 76), (92, 68)], fill=(25, 20, 22)) # Hair right
    # Tactical headset
    draw.rectangle([34, 42, 42, 60], fill=(45, 55, 65))
    draw.arc([36, 20, 92, 65], start=180, end=360, fill=(70, 180, 230), width=3) # Headband
    # Eyes (sharp, focused)
    draw.rectangle([50, 48, 56, 52], fill=(30, 40, 50))
    draw.rectangle([72, 48, 78, 52], fill=(30, 40, 50))
    # Eyebrows
    draw.line([(48, 44), (58, 45)], fill=(25, 20, 22), width=2)
    draw.line([(70, 45), (80, 44)], fill=(25, 20, 22), width=2)
    # Nose & mouth
    draw.line([(64, 54), (64, 60)], fill=(195, 155, 125), width=2)
    draw.line([(58, 67), (70, 67)], fill=(175, 115, 95), width=2)
    # Tactical turtleneck & vest
    draw.polygon([(32, 88), (64, 76), (96, 88), (110, 122), (18, 122)], fill=(35, 42, 52)) # Vest
    draw.rectangle([54, 74, 74, 88], fill=(25, 28, 35)) # Turtleneck collar
    img.save(os.path.join(PORTRAITS_DIR, "portrait_sara.png"))

def generate_sadeghi():
    img, draw = create_base((38, 30, 18, 245), (230, 175, 65, 255))
    # Face (older, tired)
    draw.ellipse([42, 28, 86, 80], fill=(225, 190, 160))
    # Grey receding hair
    draw.ellipse([38, 20, 90, 48], fill=(135, 130, 128))
    # Glasses
    draw.rectangle([46, 46, 60, 56], outline=(210, 190, 120), width=2)
    draw.rectangle([68, 46, 82, 56], outline=(210, 190, 120), width=2)
    draw.line([(60, 50), (68, 50)], fill=(210, 190, 120), width=2)
    # Eyes behind glasses
    draw.ellipse([51, 49, 55, 53], fill=(45, 55, 65))
    draw.ellipse([73, 49, 77, 53], fill=(45, 55, 65))
    # Wrinkles & beard stubble
    draw.line([(54, 42), (74, 42)], fill=(180, 145, 120), width=1)
    draw.line([(58, 68), (70, 68)], fill=(160, 110, 90), width=2)
    # Lab coat collar
    draw.polygon([(26, 88), (64, 78), (102, 88), (114, 122), (14, 122)], fill=(215, 220, 225))
    draw.polygon([(52, 80), (64, 98), (76, 80)], fill=(45, 85, 55)) # Green shirt under lab coat
    # Blood spatter on collar
    draw.ellipse([82, 94, 88, 102], fill=(140, 25, 25))
    draw.ellipse([88, 104, 92, 108], fill=(140, 25, 25))
    img.save(os.path.join(PORTRAITS_DIR, "portrait_sadeghi.png"))

def generate_leila():
    img, draw = create_base((18, 38, 25, 245), (65, 220, 130, 255))
    # Face (younger)
    draw.ellipse([44, 28, 84, 76], fill=(240, 205, 175))
    # Dark brown hair with fringe
    draw.ellipse([40, 22, 88, 48], fill=(45, 28, 20))
    draw.polygon([(38, 38), (45, 72), (40, 65)], fill=(45, 28, 20))
    draw.polygon([(90, 38), (83, 72), (88, 65)], fill=(45, 28, 20))
    # Eyes
    draw.ellipse([51, 48, 57, 54], fill=(35, 65, 45))
    draw.ellipse([71, 48, 77, 54], fill=(35, 65, 45))
    draw.line([(59, 66), (69, 66)], fill=(190, 110, 100), width=2)
    # Yellow hazmat collar & respirator mask around neck
    draw.polygon([(28, 86), (64, 76), (100, 86), (112, 122), (16, 122)], fill=(225, 185, 35))
    draw.rounded_rectangle([48, 80, 80, 102], radius=6, fill=(55, 65, 75), outline=(180, 150, 30), width=2)
    img.save(os.path.join(PORTRAITS_DIR, "portrait_leila.png"))

def generate_ghorbani():
    img, draw = create_base((42, 18, 18, 245), (230, 55, 55, 255))
    # Stern weathered face
    draw.ellipse([42, 34, 86, 82], fill=(215, 175, 145))
    # Black military beret tilted
    draw.ellipse([34, 18, 94, 46], fill=(30, 32, 35))
    draw.polygon([(82, 28), (98, 42), (88, 48)], fill=(25, 26, 28)) # Beret fold
    draw.rectangle([46, 32, 54, 40], fill=(215, 175, 45)) # Officer insignia badge
    # Harsh eyes
    draw.line([(48, 50), (58, 52)], fill=(20, 20, 22), width=3)
    draw.line([(70, 52), (80, 50)], fill=(20, 20, 22), width=3)
    # Scar down right cheek
    draw.line([(76, 44), (82, 68)], fill=(160, 75, 75), width=2)
    # Firm mouth
    draw.line([(56, 72), (72, 72)], fill=(120, 70, 60), width=2)
    # Tactical black commander coat & red lapel
    draw.polygon([(24, 88), (64, 78), (104, 88), (116, 122), (12, 122)], fill=(22, 24, 28))
    draw.line([(42, 88), (56, 122)], fill=(185, 35, 35), width=3)
    draw.line([(86, 88), (72, 122)], fill=(185, 35, 35), width=3)
    img.save(os.path.join(PORTRAITS_DIR, "portrait_ghorbani.png"))

def generate_archivist():
    img, draw = create_base((28, 18, 42, 245), (180, 120, 240, 255))
    # Ghostly translucent face
    draw.ellipse([44, 30, 84, 80], fill=(185, 170, 225, 180))
    # Hood / shroud
    draw.polygon([(32, 18), (64, 10), (96, 18), (104, 95), (24, 95)], fill=(55, 35, 75, 210))
    # Glowing ghostly eyes
    draw.ellipse([50, 48, 58, 56], fill=(235, 200, 255))
    draw.ellipse([70, 48, 78, 56], fill=(235, 200, 255))
    # Ethereal wisps
    draw.line([(40, 85), (64, 115)], fill=(160, 110, 220, 140), width=4)
    draw.line([(88, 85), (64, 115)], fill=(160, 110, 220, 140), width=4)
    img.save(os.path.join(PORTRAITS_DIR, "portrait_archivist.png"))

def generate_radio():
    img, draw = create_base((20, 30, 40, 245), (100, 180, 240, 255))
    # Radio headset / waveform icon
    draw.ellipse([38, 38, 90, 90], outline=(100, 180, 240), width=3)
    # Signal wave bars
    bars = [(46, 64, 10), (54, 64, 20), (64, 64, 32), (74, 64, 20), (82, 64, 10)]
    for x, y, h in bars:
        draw.line([(x, y - h//2), (x, y + h//2)], fill=(120, 220, 255), width=3)
    img.save(os.path.join(PORTRAITS_DIR, "portrait_radio.png"))

if __name__ == "__main__":
    generate_sara()
    generate_sadeghi()
    generate_leila()
    generate_ghorbani()
    generate_archivist()
    generate_radio()
    print("Portraits generated successfully!")
