"""Builds Assets/Resources/Art/Ui/help_frame_tall.png from the painted help frame (Final Bugs 1, 2026-11-26).

The rope on both sides repeats every ~211 px (measured by autocorrelation), so TWO periods (418 px) are inserted at a seam where the rope pattern lines up:
output rows = [0, YB) + [YA, H) with YB = YA + D. The corners, the title area and both clouds are not touched and nothing is scaled. The seam is cross-faded over a few rows.
Usage: python Tools/art_pipeline/make_help_frame_tall.py <source help_frame.png> <out help_frame_tall.png>
"""
import sys
import numpy as np
from PIL import Image

YA, D, FEATHER = 592, 418, 40

def main(src, dst):
    im = np.array(Image.open(src).convert("RGBA")).astype(np.float32)
    h, w = im.shape[:2]
    yb = YA + D
    pm = im.copy()
    pm[:, :, :3] *= pm[:, :, 3:4] / 255.0          # premultiplied, so the cross-fade never bleeds the key colour
    top = pm[0:yb]
    bottom = pm[YA:h].copy()
    for i in range(FEATHER):                        # natural continuation (row yb+i) fades into the copy (row YA+i)
        t = (i + 1) / (FEATHER + 1)
        bottom[i] = pm[yb + i] * (1.0 - t) + pm[YA + i] * t
    out = np.concatenate([top, bottom], axis=0)
    a = out[:, :, 3:4]
    rgb = np.where(a > 0.5, out[:, :, :3] * 255.0 / np.maximum(a, 1.0), 0.0)
    res = np.concatenate([np.clip(rgb, 0, 255), np.clip(a, 0, 255)], axis=2).astype(np.uint8)
    Image.fromarray(res, "RGBA").save(dst)
    print("saved", dst, res.shape[1], "x", res.shape[0], "(was", w, "x", h, ")")

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
