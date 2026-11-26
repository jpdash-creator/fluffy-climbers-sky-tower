"""Owner picks 2026-11-26: the four older special-type badges (No Wall Touch, No Power-ups, One Life, Keep Moving) from
Art/_candidates/fc6_type_icons2/I1_icons.png (cells 1-4, reading order) -> Assets/Resources/Art/Ui/icon_special_{nowall,nopowerups,onelife,keepmoving}.png
(same 256 px round-badge pipeline as bake_fc9_icons.py).   set PYTHONPATH=E:/pylibs ; python Tools/art_pipeline/bake_fc6b_icons.py"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bake_fc9_icons as b  # noqa: E402

NAMES = ["nowall", "nopowerups", "onelife", "keepmoving"]


def main():
    cells = b.boxes(os.path.join(b.CAND, "fc6_type_icons2", "I1_icons.png"), lo=300, hi=900)
    assert len(cells) == 4, len(cells)
    for n, a in zip(NAMES, cells):
        b.save_icon(a, "icon_special_" + n)


if __name__ == "__main__":
    main()
