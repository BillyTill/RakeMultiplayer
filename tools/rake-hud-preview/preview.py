"""Headless renderer: writes preview PNGs without opening the window."""

import os
import sys

from PIL import Image

import hud

HERE = os.path.dirname(os.path.abspath(__file__))


def base_state():
    return {
        "hp": 1850,
        "max_hp": 2500,
        "night": True,
        "night_no": 1,
        "time_left": 252,
        "time_total": 300,
        "abilities": [("GRAB", 0, 20), ("ANIMAL", 7, 10), ("CAMERA", 0, 50), ("LEAP", 41, 50)],
        "markers": [(640, 470, "prey", 12), (1390, 420, "animal", 34), (980, 300, "trap", 8)],
        "damage": 0.0,
    }


def render(name, state, background, t=0.8):
    bg = hud.prepare_background(os.path.join(HERE, "assets", background))
    frame = hud.compose(state, bg, t).convert("RGB")
    frame = frame.resize((hud.W, hud.H), Image.NEAREST)
    out = os.path.join(HERE, "preview-%s.png" % name)
    frame.save(out)
    print(out, os.path.getsize(out))


def main():
    if "--still" in sys.argv:
        render("night", base_state(), "night-camp.jpg", 0.8)
        return

    render("night", base_state(), "night-camp.jpg", 0.8)

    s2 = base_state()
    s2.update(hp=480, damage=0.75)
    s2["abilities"] = [("GRAB", 14, 20), ("ANIMAL", 0, 10), ("CAMERA", 33, 50), ("LEAP", 0, 50)]
    render("hurt", s2, "night-camp.jpg", 0.3)

    s3 = base_state()
    s3.update(night=False, time_left=184)
    s3["markers"] = [(700, 500, "prey", 21)]
    render("day", s3, "forest-day.jpg", 0.5)


if __name__ == "__main__":
    main()
