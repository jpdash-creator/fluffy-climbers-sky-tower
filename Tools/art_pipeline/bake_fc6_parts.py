"""Final Change 6: cuts the owner-approved overlay parts out of the OpenArt parts sheets (Art/_candidates/reg00N_fc6_platforms/P1, P2) into
Assets/Resources/Art/Fc6/r<N>/<name>.png (transparent, one object per file, never stretched; the view scales each with ONE uniform factor).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_fc6_parts.py

Grid lines of the sheets are skipped (inset), the grey is keyed by the cell's own border colour, loose specks are dropped. Parts are shrunk (uniformly) to at most
MAXSIDE px on the long side to keep the install small. Also writes Art/_candidates/fc6_parts_contact.png.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402

MAXSIDE = 512

# region -> name -> (sheet file, row, column)   (rows and columns counted from 0, top left)
PARTS = {
    1: {"dewdrop": ("P1_parts.png", 0, 0), "dragonfly_perched": ("P2_parts.png", 0, 1), "dragonfly_buzz": ("P2_parts.png", 0, 2),
        "web_wide": ("P2_parts.png", 1, 0), "web_small": ("P2_parts.png", 1, 1)},
    2: {"spore_cloud_1": ("P2_parts.png", 0, 1), "spore_cloud_2": ("P2_parts.png", 0, 2), "spore_cloud_3": ("P2_parts.png", 1, 0),
        "spore_cone_narrow_a": ("P1_parts.png", 1, 1), "spore_cone_narrow_b": ("P1_parts.png", 1, 2),
        "spore_cone_wide_a": ("P1_parts.png", 2, 0), "spore_cone_wide_b": ("P1_parts.png", 2, 1)},
    3: {"funnel_a": ("P1_parts.png", 0, 0), "funnel_b": ("P1_parts.png", 0, 1), "foam_heap_1": ("P1_parts.png", 0, 2), "foam_heap_2": ("P1_parts.png", 1, 0)},
    4: {"magnet_idle": ("P2_parts.png", 0, 0), "magnet_pulse": ("P2_parts.png", 0, 1), "sour_burst_a": ("P2_parts.png", 0, 2), "sour_burst_b": ("P2_parts.png", 1, 0),
        "core_stub": ("P1_parts.png", 1, 1)},
    5: {"scroll_roll": ("P1_parts.png", 0, 0), "torii": ("P2_parts.png", 0, 1), "incense_mist_a": ("P2_parts.png", 0, 2), "incense_mist_b": ("P2_parts.png", 1, 0)},
}


def cut_all():
    out = {}
    for region, parts in PARTS.items():
        for name, (sheet, r, c) in parts.items():
            a = fc.cell_part(fc.sheet_path(region, sheet), r, c, global_key=name.startswith("web") or name == "dewdrop")
            if name == "dewdrop":   # the dewdrop sits on its tuft: drop the loose water drop hanging below the tuft
                wide = (a[:, :, 3] > 128).sum(axis=1)
                keep = int(np.nonzero(wide > 0.25 * wide.max())[0].max()) + 6
                a = fc.trim(a[:keep])
            long = max(a.shape[:2])
            if long > MAXSIDE:
                a = fc.resample(a, MAXSIDE / long)
            a = fc.pad(a, 4)
            path = os.path.join(fc.RES_ART, "Fc6", "r%d" % region, name + ".png")
            fc.save_png(a, path)
            out[(region, name)] = path
            print("r%d %-22s %4dx%-4d" % (region, name, a.shape[1], a.shape[0]))
    return out


def main():
    out = cut_all()
    import qa_art
    bad = 0
    for (region, name), path in out.items():
        _, issues, _ = qa_art.analyse(path, None)
        issues = [i for i in issues if not i.startswith("extreme")]
        if issues:
            print("QA", region, name, issues)
            bad += 1
    print("parts QA: %d parts, %d with findings" % (len(out), bad))
    fc.contact_sheet([("r%d %s" % k, v) for k, v in out.items()], os.path.join(fc.CAND, "fc6_parts_contact.png"), ppu_game=100.0, bg=(150, 160, 175), cols=6)


if __name__ == "__main__":
    main()
