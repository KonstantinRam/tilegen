"""Side-by-side demo: the same procedural brick layout rendered two ways.

A: pixel-art style, designed at 32x32 with a small palette, scaled 4x nearest.
B: smooth texture, generated natively at 128x128 with soft noise and tints.
Each rendering is shown at garbage coverage 0% and 50%, tiled 2x2 so seams
would be visible. The point of the image is to let the friend point at one.
"""
import numpy as np
from PIL import Image, ImageDraw


def hash2(ix, iy, seed):
    """Integer hash to [0,1), deterministic across platforms."""
    h = (ix.astype(np.uint32) * np.uint32(374761393)
         + iy.astype(np.uint32) * np.uint32(668265263)
         + (np.full_like(ix.astype(np.uint32), np.uint32(seed)) * np.uint32(2246822519)))
    h = (h ^ (h >> np.uint32(13))) * np.uint32(1274126177)
    h = h ^ (h >> np.uint32(16))
    return (h & np.uint32(0xFFFFFF)).astype(np.float64) / float(0x1000000)


def value_noise(n, cells, seed):
    """Tileable value noise on an n x n grid with `cells` lattice cells per axis."""
    y, x = np.mgrid[0:n, 0:n].astype(np.float64)
    fx, fy = x * cells / n, y * cells / n
    x0, y0 = np.floor(fx).astype(np.int64), np.floor(fy).astype(np.int64)
    tx, ty = fx - x0, fy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    def v(dx, dy):
        return hash2((x0 + dx) % cells, (y0 + dy) % cells, seed)
    a = v(0, 0) * (1 - tx) + v(1, 0) * tx
    b = v(0, 1) * (1 - tx) + v(1, 1) * tx
    return a * (1 - ty) + b * ty


def fbm(n, base_cells, octaves, seed):
    out = np.zeros((n, n))
    amp, total, cells = 1.0, 0.0, base_cells
    for o in range(octaves):
        out += amp * value_noise(n, cells, seed + o * 101)
        total += amp
        amp *= 0.5
        cells *= 2
    return out / total


def brick_layout(n, rows, bricks_per_row, mortar, seed):
    """Return (is_mortar, brick_id, dist_to_edge) arrays, all tileable."""
    y, x = np.mgrid[0:n, 0:n]
    row_h = n // rows
    brick_w = n // bricks_per_row
    row = y // row_h
    offset = (row % 2) * (brick_w // 2)
    xs = (x + offset) % n
    col = xs // brick_w
    u = xs % brick_w
    v = y % row_h
    is_mortar = (u < mortar) | (v < mortar)
    brick_id = hash2(col, row, seed)
    dist = np.minimum.reduce([u - mortar, brick_w - 1 - u, v - mortar, row_h - 1 - v]).astype(np.float64)
    dist = np.clip(dist, 0, None)
    return is_mortar, brick_id, dist, u, v


def garbage_mask(n, coverage, seed, cells):
    """Boolean mask whose covered fraction equals `coverage`, from thresholded noise."""
    if coverage <= 0:
        return np.zeros((n, n), dtype=bool)
    field = fbm(n, cells, 3, seed)
    thr = np.quantile(field, 1 - coverage)
    return field >= thr


def render_pixel_art(coverage, seed):
    n = 32
    pal_brick = np.array([[72, 72, 72], [84, 84, 84], [96, 96, 96], [108, 108, 108]])
    pal_light = np.array([130, 130, 130])
    pal_mortar = np.array([30, 30, 30])
    pal_garbage = np.array([[46, 70, 40], [60, 90, 48]])
    is_mortar, bid, dist, u, v = brick_layout(n, 8, 4, 1, seed)
    img = np.zeros((n, n, 3), dtype=np.float64)
    shade = (bid * 4).astype(int).clip(0, 3)
    img[:] = pal_brick[shade]
    # 1px highlight on the top row of each brick, like the sample
    top = (v == 1) & ~is_mortar
    img[top] = pal_light
    # a little per-pixel speckle so bricks are not flat
    speck = hash2(np.arange(n)[None, :].repeat(n, 0), np.arange(n)[:, None].repeat(n, 1), seed + 7)
    img[(speck > 0.85) & ~is_mortar & ~top] -= 12
    img[is_mortar] = pal_mortar
    g = garbage_mask(n, coverage, seed + 99, 6)
    gshade = (hash2(np.arange(n)[None, :].repeat(n, 0), np.arange(n)[:, None].repeat(n, 1), seed + 5) > 0.5).astype(int)
    img[g] = pal_garbage[gshade][g]
    im = Image.fromarray(img.clip(0, 255).astype(np.uint8))
    return im.resize((128, 128), Image.NEAREST)


def render_texture(coverage, seed):
    n = 128
    is_mortar, bid, dist, u, v = brick_layout(n, 8, 4, 3, seed)
    base = np.array([96, 94, 92], dtype=np.float64)
    tint = (bid[..., None] - 0.5) * np.array([26, 24, 22])
    grain = (fbm(n, 16, 4, seed + 3)[..., None] - 0.5) * 36
    img = base + tint + grain
    # soft edge darkening without a light direction
    edge = np.clip(dist / 3.0, 0, 1)[..., None]
    img = img * (0.72 + 0.28 * edge)
    mortar_col = np.array([38, 36, 34]) + (fbm(n, 32, 2, seed + 11)[..., None] - 0.5) * 20
    img = np.where(is_mortar[..., None], mortar_col, img)
    g = garbage_mask(n, coverage, seed + 99, 4)
    moss = np.array([58, 86, 48]) + (fbm(n, 24, 3, seed + 13)[..., None] - 0.5) * 40
    gf = fbm(n, 8, 3, seed + 99)
    thr = np.quantile(gf, 1 - coverage) if coverage > 0 else 2.0
    blend = np.clip((gf - thr) / 0.08, 0, 1)[..., None] if coverage > 0 else 0.0
    img = img * (1 - blend) + moss * blend
    return Image.fromarray(img.clip(0, 255).astype(np.uint8))


def tile2x2(im):
    w, h = im.size
    out = Image.new("RGB", (w * 2, h * 2))
    for y in range(2):
        for x in range(2):
            out.paste(im, (x * w, y * h))
    return out


def main():
    seed = 1234
    cell = 256
    pad, label_h = 24, 28
    sheet = Image.new("RGB", (pad * 3 + cell * 2, label_h * 2 + pad * 3 + cell * 2), (235, 235, 235))
    d = ImageDraw.Draw(sheet)
    cols = [("garbage 0%", 0.0), ("garbage 50%", 0.5)]
    rows = [("A: pixel-art style (32px design x4)", render_pixel_art),
            ("B: smooth texture (native 128px)", render_texture)]
    for ci, (ctext, _) in enumerate(cols):
        d.text((pad + ci * (cell + pad) + 4, 6), ctext, fill=(20, 20, 20))
    for ri, (rtext, fn) in enumerate(rows):
        y0 = label_h + ri * (cell + pad + label_h)
        d.text((pad, y0 - 2), rtext, fill=(20, 20, 20))
        for ci, (_, cov) in enumerate(cols):
            im = tile2x2(fn(cov, seed))
            sheet.paste(im, (pad + ci * (cell + pad), y0 + label_h - 6))
    sheet.save("brick_demo.png")
    render_pixel_art(0.0, seed).save("A_pixel_128.png")
    render_texture(0.0, seed).save("B_texture_128.png")
    print(sheet.size)


if __name__ == "__main__":
    main()
