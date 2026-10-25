from PIL import Image, ImageDraw

def create_bandage():
    # 64x64 sterile rolled gauze bandage with medical red cross
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Drop shadow
    draw.ellipse([14, 46, 50, 58], fill=(0, 0, 0, 90))
    
    # Rolled gauze body (cylinder/roll angle)
    draw.rounded_rectangle([16, 16, 48, 46], radius=8, fill=(235, 235, 230, 255), outline=(175, 175, 170, 255), width=2)
    
    # Gauze wrapping bands
    draw.line([24, 16, 24, 46], fill=(210, 210, 205, 255), width=2)
    draw.line([32, 16, 32, 46], fill=(210, 210, 205, 255), width=2)
    draw.line([40, 16, 40, 46], fill=(210, 210, 205, 255), width=2)
    
    # Texture hatch lines
    for y in range(20, 44, 4):
        draw.line([18, y, 46, y], fill=(245, 245, 240, 180), width=1)
        
    # Medical Red Cross in center
    # Vertical bar
    draw.rectangle([30, 23, 34, 39], fill=(220, 35, 35, 255))
    # Horizontal bar
    draw.rectangle([24, 29, 40, 33], fill=(220, 35, 35, 255))
    
    img.save("Assets/textures/bandage.png")
    print("Created Assets/textures/bandage.png")

def create_double_herb():
    # Twin bundled green herbs tied with twine
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Drop shadow
    draw.ellipse([12, 48, 52, 60], fill=(0, 0, 0, 90))
    
    # Left herb bunch
    draw.ellipse([14, 14, 34, 42], fill=(45, 165, 75, 255), outline=(25, 95, 45, 255), width=2)
    draw.ellipse([18, 10, 32, 34], fill=(65, 195, 95, 255))
    # Stem
    draw.line([24, 40, 28, 54], fill=(30, 120, 50, 255), width=3)
    
    # Right herb bunch
    draw.ellipse([30, 14, 50, 42], fill=(45, 165, 75, 255), outline=(25, 95, 45, 255), width=2)
    draw.ellipse([32, 10, 46, 34], fill=(65, 195, 95, 255))
    # Stem
    draw.line([40, 40, 36, 54], fill=(30, 120, 50, 255), width=3)
    
    # Central tall leaf
    draw.ellipse([26, 6, 38, 30], fill=(75, 215, 110, 255), outline=(25, 95, 45, 255), width=1)
    
    # Twine wrap binding both stems together
    draw.rounded_rectangle([25, 42, 39, 48], radius=2, fill=(190, 150, 85, 255), outline=(130, 95, 45, 255), width=1)
    draw.line([25, 45, 39, 45], fill=(220, 180, 110, 255), width=1)
    
    img.save("Assets/textures/herb_green_double.png")
    print("Created Assets/textures/herb_green_double.png")

def create_first_aid_spray():
    # Resident Evil style First Aid Spray aerosol canister
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Drop shadow
    draw.ellipse([18, 50, 46, 60], fill=(0, 0, 0, 100))
    
    # Canister metal body
    draw.rounded_rectangle([20, 18, 44, 54], radius=4, fill=(230, 235, 240, 255), outline=(120, 130, 140, 255), width=2)
    
    # Shading on metallic body
    draw.line([23, 20, 23, 52], fill=(255, 255, 255, 220), width=2) # highlight
    draw.line([41, 20, 41, 52], fill=(160, 170, 180, 220), width=2) # shadow
    
    # Green medical cross label on canister
    draw.rectangle([22, 28, 42, 44], fill=(40, 135, 75, 255))
    # White cross inside green band
    draw.rectangle([30, 30, 34, 42], fill=(255, 255, 255, 255))
    draw.rectangle([26, 34, 38, 38], fill=(255, 255, 255, 255))
    
    # Cap / Nozzle collar
    draw.rectangle([26, 12, 38, 18], fill=(180, 190, 200, 255), outline=(100, 110, 120, 255), width=1)
    # Spray actuator button
    draw.rounded_rectangle([28, 6, 36, 12], radius=2, fill=(45, 175, 85, 255), outline=(25, 105, 50, 255), width=1)
    # Nozzle hole
    draw.point([35, 9], fill=(20, 20, 20, 255))
    
    img.save("Assets/textures/first_aid_spray.png")
    print("Created Assets/textures/first_aid_spray.png")

def create_medkit_hq():
    # 64x64 standard military paramedic case
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Drop shadow
    draw.ellipse([10, 48, 54, 60], fill=(0, 0, 0, 100))
    
    # Briefcase body
    draw.rounded_rectangle([12, 16, 52, 50], radius=6, fill=(240, 240, 240, 255), outline=(110, 120, 130, 255), width=2)
    
    # Handle on top
    draw.rounded_rectangle([26, 8, 38, 16], radius=3, fill=(0, 0, 0, 0), outline=(70, 75, 85, 255), width=2)
    
    # Latches
    draw.rectangle([18, 14, 22, 18], fill=(160, 165, 175, 255))
    draw.rectangle([42, 14, 46, 18], fill=(160, 165, 175, 255))
    
    # Center Red / Green Paramedic Cross
    # Red cross
    draw.rectangle([29, 22, 35, 44], fill=(225, 40, 40, 255))
    draw.rectangle([21, 30, 43, 36], fill=(225, 40, 40, 255))
    
    # Inner subtle bevel
    draw.rectangle([30, 23, 34, 43], fill=(245, 70, 70, 255))
    draw.rectangle([22, 31, 42, 35], fill=(245, 70, 70, 255))
    
    img.save("Assets/textures/medkit.png")
    print("Updated Assets/textures/medkit.png")

if __name__ == "__main__":
    create_bandage()
    create_double_herb()
    create_first_aid_spray()
    create_medkit_hq()
