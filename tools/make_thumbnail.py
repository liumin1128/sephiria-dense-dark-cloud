import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

mod = Path(r"E:\SteamLibrary\steamapps\common\Sephiria\WorkshopLocal\dense-dark-cloud")

W = H = 512
img = Image.new("RGB", (W, H), (10, 10, 20))
d = ImageDraw.Draw(img)

# stormy night gradient
for y in range(H):
    t = y / (H - 1)
    d.line([(0, y), (W, y)], fill=(int(10 + 26 * t), int(12 + 30 * t), int(26 + 58 * t)))

# clouds
clouds = [
    (150, 250, 95, 55),
    (250, 215, 120, 70),
    (355, 260, 85, 50),
    (200, 300, 110, 45),
    (315, 305, 100, 40),
]
for cx, cy, rx, ry in clouds:
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=(70, 74, 96))
for cx, cy, rx, ry in clouds:
    d.ellipse([cx - rx + 6, cy - ry + 6, cx + rx - 6, cy + ry - 6], fill=(44, 46, 64))

# lightning bolts below the clouds
def bolt(points, color):
    d.line(points, fill=color, width=6, joint="curve")

bolt([(250, 300), (225, 350), (250, 348), (215, 410)], (255, 236, 140))
bolt([(300, 300), (285, 340), (305, 338), (280, 385)], (255, 216, 96))

def font(size, bold=True):
    for name in ("msyhbd.ttc", "msyh.ttc", "simhei.ttf", "arialbd.ttf"):
        p = Path(r"C:\Windows\Fonts") / name
        if p.exists():
            try:
                return ImageFont.truetype(str(p), size)
            except OSError:
                continue
    return ImageFont.load_default()

d.rectangle([20, 20, W - 20, H - 20], outline=(255, 214, 110), width=3)
d.text((W // 2, 120), "乌云密布", font=font(62), fill=(240, 244, 255), anchor="mm")
d.text((W // 2, 178), "DENSE DARK CLOUD", font=font(24), fill=(150, 190, 240), anchor="mm")
d.text((W // 2, 448), "1 → 7 朵独立乌云", font=font(26), fill=(255, 214, 110), anchor="mm")
d.text((W // 2, 480), "传说 · 乌云连击", font=font(17), fill=(255, 110, 160), anchor="mm")

img.save(mod / "thumbnail.png", optimize=True)
print("thumbnail bytes:", (mod / "thumbnail.png").stat().st_size)

meta = json.loads((mod / "metadata.json").read_text(encoding="utf-8"))
print("metadata ok:", meta["mod"]["modId"], "| modules:", meta["mod"]["modules"])
print("dll present:", (mod / "bin" / "DenseDarkCloud.dll").exists())
