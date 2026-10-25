from PIL import Image, ImageDraw, ImageFilter
import math

def create_padlock_textures():
    # 1. Padlock Housing / Body (Center frame with shackle)
    # 512x360 high-detail antique brass padlock casing inspired by RE4 Village Chief padlock
    width, height = 560, 380
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Shackle (heavy curved iron bar behind the padlock body)
    shackle_box = [150, 20, 410, 260]
    draw.arc(shackle_box, 180, 0, fill=(70, 65, 60, 255), width=28)
    draw.line([150, 140, 150, 220], fill=(70, 65, 60, 255), width=28)
    draw.line([410, 140, 410, 220], fill=(70, 65, 60, 255), width=28)
    
    # Shackle metallic highlights
    draw.arc([154, 24, 406, 256], 190, 350, fill=(130, 125, 120, 180), width=6)
    draw.line([154, 140, 154, 215], fill=(130, 125, 120, 180), width=6)
    
    # Left & Right heavy brass mounting brackets
    # Left bracket
    draw.rounded_rectangle([60, 150, 160, 310], radius=14, fill=(185, 145, 75, 255), outline=(90, 65, 30, 255), width=4)
    # Right bracket
    draw.rounded_rectangle([400, 150, 500, 310], radius=14, fill=(185, 145, 75, 255), outline=(90, 65, 30, 255), width=4)
    
    # Central cylinder chamber cradle (dark recessed cavity where the 3 dials sit)
    draw.rounded_rectangle([150, 160, 410, 300], radius=8, fill=(35, 28, 20, 255), outline=(80, 60, 30, 255), width=3)
    
    # Ornate filigree scrollwork on brackets
    for bracket_x in [60, 400]:
        # Corner rivets
        for rx, ry in [(bracket_x + 18, 168), (bracket_x + 82, 168), (bracket_x + 18, 292), (bracket_x + 82, 292)]:
            draw.ellipse([rx - 6, ry - 6, rx + 6, ry + 6], fill=(120, 90, 40, 255), outline=(60, 45, 20, 255), width=2)
            draw.ellipse([rx - 3, ry - 3, rx + 1, ry + 1], fill=(230, 190, 110, 200))
        # Filigree engravings
        draw.arc([bracket_x + 20, 190, bracket_x + 80, 270], 45, 225, fill=(110, 80, 35, 255), width=3)
        draw.arc([bracket_x + 25, 195, bracket_x + 75, 265], 225, 45, fill=(130, 95, 45, 255), width=2)
        
    # Central alignment marker arrows (pointers showing the active row)
    # Left pointer
    draw.polygon([(142, 230), (156, 222), (156, 238)], fill=(230, 195, 110, 255), outline=(80, 55, 20, 255))
    # Right pointer
    draw.polygon([(418, 230), (404, 222), (404, 238)], fill=(230, 195, 110, 255), outline=(80, 55, 20, 255))

    img.save("Assets/textures/padlock_frame.png")
    print("Created Assets/textures/padlock_frame.png")

def create_dial_cylinder_background():
    # 80x130 rotating brass cylinder segment with 3D gradient shading (top/bottom shadows, central highlight)
    w, h = 84, 136
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Vertical gradient simulating rounded cylinder curvature
    for y in range(h):
        # Normal [-1.0 at top, 0.0 at center, +1.0 at bottom]
        norm = (y - h / 2.0) / (h / 2.0)
        # Cosine shading
        shade = math.cos(norm * (math.pi / 2.0))
        r = int(70 + 130 * shade)
        g = int(50 + 100 * shade)
        b = int(25 + 55 * shade)
        draw.line([0, y, w, y], fill=(r, g, b, 255))
        
    # Metallic edge rims
    draw.line([0, 0, w, 0], fill=(220, 180, 100, 255), width=2)
    draw.line([0, h-1, w, h-1], fill=(60, 40, 15, 255), width=2)
    # Side ribbed knurling
    draw.line([0, 0, 0, h], fill=(220, 180, 100, 180), width=2)
    draw.line([w-1, 0, w-1, h], fill=(50, 35, 15, 220), width=2)
    
    # Horizontal separator grooves
    draw.line([0, 26, w, 26], fill=(45, 30, 10, 200), width=2)
    draw.line([0, 28, w, 28], fill=(180, 140, 70, 140), width=1)
    
    draw.line([0, h-26, w, h-26], fill=(45, 30, 10, 200), width=2)
    draw.line([0, h-24, w, h-24], fill=(180, 140, 70, 140), width=1)

    img.save("Assets/textures/padlock_dial_bg.png")
    print("Created Assets/textures/padlock_dial_bg.png")

def create_symbols():
    # 64x64 antique dark carved pictogram symbols matching RE4 padlock
    symbols = {
        "symbol_crop": draw_crop,
        "symbol_beast": draw_beast,
        "symbol_child": draw_child,
        "symbol_fish": draw_fish,
        "symbol_eagle": draw_eagle,
        "symbol_skull": draw_skull,
        "symbol_chalice": draw_chalice,
        "symbol_serpent": draw_serpent,
    }
    for name, func in symbols.items():
        img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)
        func(draw)
        img.save(f"Assets/textures/{name}.png")
        print(f"Created Assets/textures/{name}.png")

def draw_crop(draw):
    # Two wheat stalks with grain kernels (matching the crop in RE4 padlock image)
    color = (40, 25, 10, 255)
    shadow = (90, 70, 40, 200)
    
    # Left stalk
    draw.line([22, 12, 22, 54], fill=color, width=3)
    # Right stalk
    draw.line([42, 12, 42, 54], fill=color, width=3)
    
    # Grains on left stalk
    for y in range(16, 44, 7):
        draw.ellipse([12, y - 4, 22, y + 4], outline=color, fill=shadow, width=2)
        draw.ellipse([22, y - 4, 32, y + 4], outline=color, fill=shadow, width=2)
        
    # Grains on right stalk
    for y in range(16, 44, 7):
        draw.ellipse([32, y - 4, 42, y + 4], outline=color, fill=shadow, width=2)
        draw.ellipse([42, y - 4, 52, y + 4], outline=color, fill=shadow, width=2)

def draw_beast(draw):
    # Swine / Pig / 4-legged beast pictogram (matching RE4 padlock center dial)
    color = (40, 25, 10, 255)
    shadow = (90, 70, 40, 200)
    
    # Body (barrel oval)
    draw.ellipse([14, 20, 50, 42], outline=color, fill=shadow, width=3)
    # Vertical rib markings inside body
    for x in [22, 28, 34, 40]:
        draw.line([x, 22, x, 40], fill=color, width=2)
        
    # Snout / Head on left
    draw.polygon([(16, 26), (6, 30), (10, 36), (18, 34)], outline=color, fill=color)
    # Ear
    draw.line([14, 24, 10, 16], fill=color, width=2)
    
    # 4 legs
    draw.line([18, 42, 18, 54], fill=color, width=3)
    draw.line([26, 42, 26, 54], fill=color, width=3)
    draw.line([36, 42, 36, 54], fill=color, width=3)
    draw.line([44, 42, 44, 54], fill=color, width=3)
    
    # Curly tail on right
    draw.arc([48, 22, 58, 34], 270, 90, fill=color, width=2)

def draw_child(draw):
    # Infant / Baby pictogram (matching RE4 padlock right dial)
    color = (40, 25, 10, 255)
    shadow = (90, 70, 40, 200)
    
    # Head (circle)
    draw.ellipse([24, 10, 40, 26], outline=color, fill=shadow, width=3)
    # Body
    draw.line([32, 26, 32, 44], fill=color, width=3)
    # Arms reaching forward (crawling/reaching baby)
    draw.line([32, 32, 20, 30], fill=color, width=3)
    draw.line([20, 30, 18, 38], fill=color, width=3)
    
    draw.line([32, 35, 24, 38], fill=color, width=3)
    
    # Bent baby legs
    draw.line([32, 44, 22, 48], fill=color, width=3)
    draw.line([22, 48, 24, 56], fill=color, width=3)
    
    draw.line([32, 44, 38, 52], fill=color, width=3)
    draw.line([38, 52, 44, 50], fill=color, width=3)

def draw_fish(draw):
    # Ancient aquatic fish pictogram
    color = (40, 25, 10, 255)
    draw.ellipse([12, 22, 46, 42], outline=color, width=3)
    # Tail fin
    draw.polygon([(44, 32), (56, 20), (56, 44)], outline=color, fill=color)
    # Eye
    draw.ellipse([18, 28, 22, 32], fill=color)
    # Gills
    draw.arc([24, 24, 30, 40], 270, 90, fill=color, width=2)

def draw_eagle(draw):
    # Raptor / Eagle spreading wings
    color = (40, 25, 10, 255)
    # Head & beak
    draw.polygon([(32, 10), (26, 16), (32, 20)], fill=color)
    # Wings
    draw.polygon([(32, 18), (10, 22), (24, 38), (32, 32), (40, 38), (54, 22)], outline=color, fill=(90, 70, 40, 180), width=2)
    # Tail feathers
    draw.polygon([(30, 32), (28, 52), (36, 52), (34, 32)], fill=color)

def draw_skull(draw):
    # Chiseled skull pictogram
    color = (40, 25, 10, 255)
    draw.ellipse([16, 12, 48, 42], outline=color, fill=(90, 70, 40, 180), width=3)
    # Jaw
    draw.rectangle([24, 38, 40, 50], outline=color, fill=(90, 70, 40, 180), width=2)
    # Eye sockets
    draw.ellipse([22, 24, 30, 34], fill=color)
    draw.ellipse([34, 24, 42, 34], fill=color)
    # Teeth marks
    for x in [28, 32, 36]:
        draw.line([x, 42, x, 50], fill=color, width=2)

def draw_chalice(draw):
    # Ancient holy goblet / chalice
    color = (40, 25, 10, 255)
    draw.polygon([(16, 14), (48, 14), (44, 34), (20, 34)], outline=color, fill=(90, 70, 40, 180), width=3)
    draw.line([32, 34, 32, 50], fill=color, width=4)
    draw.ellipse([20, 48, 44, 54], fill=color)

def draw_serpent(draw):
    # Coiled serpent
    color = (40, 25, 10, 255)
    draw.arc([16, 16, 48, 48], 0, 360, fill=color, width=4)
    # Head
    draw.polygon([(46, 24), (56, 18), (50, 32)], fill=color)
    # Tongue
    draw.line([54, 20, 60, 16], fill=(160, 40, 40, 255), width=2)

def create_wood_door_background():
    # 800x500 antique heavy carved oak door texture (like the door in the RE4 image)
    w, h = 800, 500
    img = Image.new("RGBA", (w, h), (45, 24, 14, 255))
    draw = ImageDraw.Draw(img)
    
    # Wood grain planks
    for x in range(0, w, 8):
        v = int(24 + 12 * math.sin(x * 0.2))
        draw.line([x, 0, x, h], fill=(v + 20, v, max(0, v - 10), 255), width=8)
        
    # Vertical panel seams
    draw.line([w // 2 - 2, 0, w // 2 - 2, h], fill=(15, 8, 4, 255), width=4)
    draw.line([w // 4, 0, w // 4, h], fill=(20, 10, 5, 200), width=3)
    draw.line([3 * w // 4, 0, 3 * w // 4, h], fill=(20, 10, 5, 200), width=3)
    
    # Carved gothic arch relief patterns (matching RE4 background)
    draw.arc([w // 2 - 220, 40, w // 2 + 220, 460], 180, 0, fill=(25, 14, 8, 220), width=8)
    draw.arc([w // 2 - 180, 60, w // 2 + 180, 440], 180, 0, fill=(65, 36, 20, 160), width=4)
    
    # Dark vignette border
    for b in range(40):
        alpha = int((1.0 - b / 40.0) * 200)
        draw.rectangle([b, b, w - b, h - b], outline=(0, 0, 0, alpha), width=1)
        
    img.save("Assets/textures/puzzle_door_wood_bg.png")
    print("Created Assets/textures/puzzle_door_wood_bg.png")

if __name__ == "__main__":
    create_padlock_textures()
    create_dial_cylinder_background()
    create_symbols()
    create_wood_door_background()
