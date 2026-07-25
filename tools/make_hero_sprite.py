#!/usr/bin/env python3
"""Generate a Final Fantasy VI style overworld character sprite.

FF6 map sprites are 16x24 with a tight palette and a dark outline on every
silhouette edge. This script hand-plots the pixels and writes both a 1x PNG
(for use in-game) and an 8x nearest-neighbour PNG (for eyeballing it).

No third-party deps: the PNG encoder below is ~30 lines of zlib + struct.
"""

import struct
import zlib
from pathlib import Path

# --- palette -----------------------------------------------------------
# Deliberately small, the way a SNES 4bpp sprite tile would be: one outline,
# three ramps of three, plus accents.
PALETTE = {
    ".": None,                  # transparent
    "K": (32, 24, 40),          # outline
    "i": (248, 224, 144),       # hair highlight
    "h": (216, 168, 64),        # hair mid
    "j": (160, 104, 32),        # hair shadow
    "s": (248, 216, 176),       # skin
    "d": (216, 160, 120),       # skin shadow
    "e": (56, 56, 96),          # eye
    "t": (232, 88, 80),         # tunic highlight
    "r": (200, 56, 64),         # tunic mid
    "n": (128, 32, 48),         # tunic shadow
    "g": (248, 208, 96),        # gold trim / belt
    "p": (96, 120, 200),        # trousers
    "q": (56, 72, 136),         # trousers shadow
    "b": (160, 104, 64),        # boot
    "v": (96, 56, 32),          # boot shadow
}

WIDTH, HEIGHT = 16, 24

# Front-facing idle pose: oversized head, four-pixel eyes, tunic over
# trousers, gold belt, cuffed boots.
SPRITE = [
    "......KKKK......",
    "....KKiiiiKK....",
    "...KiiiiiiiiK...",
    "..KiihhhhhhiiK..",
    "..KhssssssssjK..",
    "..KhseesseesjK..",
    "..KhssssssddjK..",
    "..KhsssKKsddjK..",
    "...KdssssssdK...",
    ".....KsddsK.....",
    "..KttttggttttK..",
    ".KttrrrrrrrrttK.",
    ".KttrrrrrrrrttK.",
    ".KttrrnnnnrrttK.",
    ".KttggggggggttK.",
    ".KsKnnnnnnnnKsK.",
    "..KnnnnnnnnnnK..",
    "...KppppppppK...",
    "...KpppKKpppK...",
    "...KppK..KppK...",
    "...KqqK..KqqK...",
    "...KbbK..KbbK...",
    "..KbbbK..KbbbK..",
    "..KKKKK..KKKKK..",
]


def validate(rows):
    if len(rows) != HEIGHT:
        raise SystemExit(f"expected {HEIGHT} rows, got {len(rows)}")
    for y, row in enumerate(rows):
        if len(row) != WIDTH:
            raise SystemExit(f"row {y} is {len(row)} px wide, expected {WIDTH}: {row!r}")
        for ch in row:
            if ch not in PALETTE:
                raise SystemExit(f"row {y} uses undefined palette key {ch!r}")


def to_rgba(rows, scale=1):
    """Flatten the char grid to raw RGBA rows, scaled by nearest neighbour."""
    out = []
    for row in rows:
        line = bytearray()
        for ch in row:
            rgb = PALETTE[ch]
            px = bytes(rgb) + b"\xff" if rgb else b"\x00\x00\x00\x00"
            line += px * scale
        out.extend([bytes(line)] * scale)
    return out


def write_png(path, rgba_rows):
    width = len(rgba_rows[0]) // 4
    height = len(rgba_rows)
    raw = b"".join(b"\x00" + row for row in rgba_rows)  # filter type 0 per scanline

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    Path(path).write_bytes(png)
    return width, height


def main():
    validate(SPRITE)
    out_dir = Path(__file__).resolve().parent.parent / "SlimeDefense/Assets/Art/Sprites"
    out_dir.mkdir(parents=True, exist_ok=True)

    for scale, name in ((1, "hero_ff6.png"), (8, "hero_ff6_preview_8x.png")):
        w, h = write_png(out_dir / name, to_rgba(SPRITE, scale))
        print(f"wrote {out_dir / name} ({w}x{h})")


if __name__ == "__main__":
    main()
