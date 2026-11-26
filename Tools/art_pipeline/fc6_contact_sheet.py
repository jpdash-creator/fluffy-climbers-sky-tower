"""Final Change 6: one owner-approval sheet, Art/_candidates/fc6_contact_sheet.png: every new platform kind, every pose, every width, at game size (120 px per unit)
on a flat background. Run after the bake_region{1..5}_fc6_platforms.py scripts.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/fc6_contact_sheet.py
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402

KINDS = {1: ["glowwormrelay", "dewdropmirrorleaf", "dragonflydart", "silksnareweb"], 2: ["teetercap", "fogpuffdrift", "sporefunnel"],
         3: ["moonjellychain", "seafoampad", "splittingpier", "undertowpad"], 4: ["meltingscoop", "caramelmagnet", "soursprinklercannon", "sugarglassledge"],
         5: ["waterbambootipper", "prayerscrollrunner", "toriilintel", "incensemistbank"]}
SCALE = 120.0 / fc.PPU
BG = (150, 205, 245)


def main():
    lines = []   # (label, [images])
    for region, kinds in KINDS.items():
        for kind in kinds:
            d = os.path.join(fc.tiles_dir(region), kind + "_widths")
            doc = json.load(open(os.path.join(d, "platform_set.json"), encoding="utf-8"))
            by_pose = {}
            for p in doc["pieces"]:
                by_pose.setdefault(p.get("pose", ""), []).append(p)
            for pose, pcs in by_pose.items():
                ims = []
                for p in sorted(pcs, key=lambda q: q["width"]):
                    im = Image.open(os.path.join(d, p["file"])).convert("RGBA")
                    im = im.resize((max(1, round(im.width * SCALE)), max(1, round(im.height * SCALE))), Image.LANCZOS)
                    ims.append(("%gu" % p["width"], im))
                lines.append(("R%d %s  %s" % (region, kind, pose), ims))
    W = 2300
    y = 8
    layout = []
    for label, ims in lines:
        x, rowh, rows = 10, 0, []
        cur = []
        for t, im in ims:
            if x + im.width + 12 > W:
                rows.append((cur, rowh))
                cur, x, rowh = [], 10, 0
            cur.append((x, t, im))
            x += im.width + 14
            rowh = max(rowh, im.height)
        rows.append((cur, rowh))
        layout.append((label, y, rows))
        y += 18 + sum(r[1] + 14 for r in rows) + 8
    sheet = Image.new("RGBA", (W, y + 10), BG + (255,))
    dr = ImageDraw.Draw(sheet)
    for label, y0, rows in layout:
        dr.text((10, y0), label, fill=(20, 30, 60, 255))
        yy = y0 + 16
        for cur, rowh in rows:
            for x, t, im in cur:
                sheet.alpha_composite(im, (x, yy))
                dr.text((x, yy + im.height), t, fill=(60, 30, 20, 255))
            yy += rowh + 14
    out = os.path.join(fc.CAND, "fc6_contact_sheet.png")
    sheet.convert("RGB").save(out)
    print("wrote", out, sheet.size)


if __name__ == "__main__":
    main()
