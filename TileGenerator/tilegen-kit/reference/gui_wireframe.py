"""Draws the GUI wireframe described in docs/GUI.md as a PNG.

The picture is a layout aid for Claude Code and for review. Box sizes are
approximate and the WPF layout is free to differ by a few pixels.
"""
from PIL import Image, ImageDraw, ImageFont

W, H = 1240, 820
im = Image.new("RGB", (W, H), (240, 240, 240))
d = ImageDraw.Draw(im)
try:
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
    bold = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 13)
except OSError:
    font = bold = ImageFont.load_default()


def box(x0, y0, x1, y1, title=None, fill=(252, 252, 252)):
    d.rectangle([x0, y0, x1, y1], fill=fill, outline=(90, 90, 90))
    if title:
        d.text((x0 + 6, y0 + 4), title, fill=(20, 20, 20), font=bold)


def row(x0, y, x1, label, kind="slider"):
    d.text((x0 + 6, y), label, fill=(40, 40, 40), font=font)
    if kind == "slider":
        d.line([x0 + 6, y + 24, x1 - 90, y + 24], fill=(120, 120, 120), width=2)
        d.ellipse([x0 + 70, y + 19, x0 + 80, y + 29], fill=(60, 60, 60))
        d.rectangle([x1 - 84, y + 15, x1 - 34, y + 33], outline=(90, 90, 90), fill="white")
        d.rectangle([x1 - 28, y + 15, x1 - 8, y + 33], outline=(90, 90, 90), fill="white")
        d.text((x1 - 24, y + 17), "L", fill=(40, 40, 40), font=font)
    elif kind == "ramp":
        for i, c in enumerate([(30, 10, 8), (90, 30, 20), (200, 90, 30), (255, 130, 40)]):
            d.rectangle([x0 + 6 + i * 44, y + 16, x0 + 40 + i * 44, y + 34], fill=c, outline=(90, 90, 90))
        d.text((x0 + 186, y + 17), "+ stop   - stop", fill=(40, 40, 40), font=font)
    elif kind == "combo":
        d.rectangle([x0 + 6, y + 16, x1 - 8, y + 34], outline=(90, 90, 90), fill="white")


def material_column(x0, x1, title):
    box(x0, 10, x1, 810, title)
    box(x0 + 8, 32, x0 + 8 + 256, 32 + 256, None, fill=(70, 60, 55))
    d.text((x0 + 14, 40), "1:1 preview, selected variant", fill=(230, 230, 230), font=font)
    y = 296
    d.text((x0 + 8, y), "variants:", fill=(40, 40, 40), font=font)
    for i in range(4):
        d.rectangle([x0 + 70 + i * 50, y - 4, x0 + 110 + i * 50, y + 36], fill=(70, 60, 55), outline=(90, 90, 90))
    row(x0, 340, x1, "Garbage coverage  0..100 (headline knob)")
    y = 386
    d.text((x0 + 8, y), "tabs:  [ Base ]  [ Garbage ]", fill=(40, 40, 40), font=font)
    y += 26
    row(x0, y, x1, "Structure family", "combo"); y += 44
    row(x0, y, x1, "Ramp (color stops)", "ramp"); y += 44
    for label in ["Scale", "Octaves", "Warp", "Detail scale", "Detail strength", "Contrast", "Tint"]:
        row(x0, y, x1, label); y += 44
    d.text((x0 + 8, y - 6), "(scrolls; rows come from the schema)", fill=(110, 110, 110), font=font)


material_column(10, 330, "WALL")
material_column(340, 660, "FLOOR")

box(670, 10, 1230, 540, "ROOM PREVIEW (walls around a floor, variants mixed, fit to panel)")
# simple room sketch
tx, ty, n, s = 690, 36, 12, 40
for j in range(n):
    for i in range(n):
        wall = i == 0 or j == 0 or i == n - 1 or j == n - 1
        c = (120, 100, 90) if wall else (60, 56, 52)
        if not wall and (i * 7 + j * 3) % 5 == 0:
            c = (72, 66, 60)
        if wall and (i * 5 + j * 11) % 4 == 0:
            c = (132, 112, 100)
        d.rectangle([tx + i * s, ty + j * s, tx + (i + 1) * s - 1, ty + (j + 1) * s - 1], fill=c)
d.text((1180, 36), "zoom", fill=(40, 40, 40), font=font)
d.rectangle([1180, 56, 1220, 74], outline=(90, 90, 90), fill="white")
d.text((1180, 90), "room", fill=(40, 40, 40), font=font)
d.rectangle([1180, 110, 1220, 128], outline=(90, 90, 90), fill="white")
d.text((1180, 150), "[ ] full\nsize", fill=(40, 40, 40), font=font)

box(670, 550, 940, 810, "PRESETS | FAVORITES (tabs)")
for i, name in enumerate(["Lava Cave", "Ancient Greek Ruins", "Ice Cave", "Cursed Lands"]):
    d.text((684, 580 + i * 20), name, fill=(40, 40, 40), font=font)
for i, label in enumerate(["Load", "Save as", "Delete"]):
    d.rectangle([684 + i * 84, 770, 760 + i * 84, 798], outline=(90, 90, 90), fill="white")
    d.text((692 + i * 84, 777), label, fill=(40, 40, 40), font=font)

box(950, 550, 1230, 810, "SET")
row(950, 576, 1230, "Set name  [SuperDungeon]", "combo")
row(950, 620, 1230, "Size  [256 v]      Variants  [4]", "combo")
row(950, 664, 1230, "Base seed  [1234]   (editable)", "combo")
for i, label in enumerate(["Reroll seed", "Randomize", "Roll both"]):
    d.rectangle([958 + i * 90, 712, 1040 + i * 90, 740], outline=(90, 90, 90), fill="white")
    d.text((964 + i * 90, 719), label, fill=(40, 40, 40), font=font)
for i, label in enumerate(["Add favorite", "Export set..."]):
    d.rectangle([958 + i * 136, 756, 1086 + i * 136, 798], outline=(90, 90, 90), fill="white")
    d.text((966 + i * 136, 770), label, fill=(40, 40, 40), font=font)

im.save("gui_wireframe.png")
print(im.size)
