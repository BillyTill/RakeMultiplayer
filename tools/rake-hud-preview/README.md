# Rake HUD preview

Standalone concept viewer for the Rake's in-game HUD. Dark forest-monster
direction: bone, ember, blood, claw marks, organic plates. No paper, tape,
cassettes or handwriting.

The HUD is authored in 1920×1080 layout units, rendered at half resolution and
upscaled with nearest-neighbour, which gives the coarse pixel blending.

## Run

```text
run.cmd
```

or

```text
python app.py
```

Requires Python 3 with Pillow and PyQt5 (both already installed on this machine).

## Keys

| Key | Action |
|---|---|
| `H` | cycle health (2500 / 1850 / 900 / 320) |
| `C` | toggle cooldowns |
| `D` | damage burst (blood vignette + splatter) |
| `N` | day / night |
| `M` | sense markers |
| `B` | background |
| `S` | save `rake-hud.png` (1920×1080) |
| `F` | fullscreen |
| `Esc` | quit |

## Files

- `hud.py` — the renderer (all shapes, glyphs, animation).
- `app.py` — the PyQt5 window and key handling.
- `preview.py` — headless renderer, writes `preview-*.png`.
- `assets/` — game screenshots used as backdrops.

## Elements

- **Health**: organic plate with a ribbed wound; fills with blood, veins crawl
  out at low HP, heartbeat pulse.
- **Phase**: scratched moon with an ember ring showing the remaining time.
- **Abilities**: four dark sockets with claw / paw / eye / leap glyphs; cooldown
  is an ember arc, ready state is a dim ember ring.
- **Sense**: torn ring + eye slit for hunters, chevrons for animals, jagged X for
  traps, distance in metres.
- **Damage**: blood vignette from the edges plus splatter.
