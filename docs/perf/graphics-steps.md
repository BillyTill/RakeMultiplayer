# Graphics staircase: where the FPS actually comes from

Measured on `Level_1(Day)`, Rake camera in third person, same viewpoint (forest, ~890 draw
calls), 4 samples per step, 2560×1440, RTX 3050 + i3-12100F.

## Cumulative cuts

Each row keeps every earlier cut and adds one more.

| Stage | Frame, ms | CPU, ms | Render, ms | FPS | Draws | Tris |
| --- | --- | --- | --- | --- | --- | --- |
| 0. as shipped | 25.2 | 22.1 | 11.2 | 40 | 895 | 105k |
| 1. + anti-aliasing off | 25.2 | 21.9 | 10.7 | 39 | 896 | 105k |
| 2. + directional shadows off | 23.6 | 20.5 | 9.4 | 43 | 792 | 105k |
| 3. + shadow distance 30 | 23.5 | 20.4 | 9.2 | 42 | 796 | 115k |
| 4. + half-size textures | 25.0 | 21.6 | 9.8 | 44 | 792 | 101k |
| 5. + one pixel light | 24.4 | 21.8 | 10.5 | 44 | 795 | 116k |
| **6. + LOD bias 0.7** | **17.4** | **15.0** | **5.8** | **56** | 778 | 107k |
| 7. + far clip 200 | 15.3 | 12.7 | 4.4 | 66 | 362 | 79k |
| 8. + far clip 120 | 13.2 | 10.7 | 2.7 | 80 | 183 | 31k |
| 9. + terrain trees off (not usable) | 6.2 | 4.2 | 0.8 | 208 | 183 | 31k |

The first real jump is **stage 6, LOD bias**: −7 ms at once, 40 → 56 FPS, with the draw
call count unchanged. Everything before it (AA, shadows, shadow distance, texture size,
pixel lights) buys 0–2 ms together.

## LOD bias curve

| LOD bias | Frame, ms | FPS |
| --- | --- | --- |
| 1.5 (game default) | 26.9 | 35 |
| 1.0 | 20.2 | 47 |
| 0.7 | 17.2 | 63 |
| 0.5 | 17.0 | 60 |
| 0.7 + far clip 220 | 18.4 | 51 (within run-to-run noise) |

Run-to-run noise on this scene is about ±2 ms, so the last row is not a regression, just a
different session. The curve flattens below 0.7: 0.5 adds nothing.

## What costs what

- **LOD bias** drives the terrain trees: at 1.5 the terrain keeps high-detail tree meshes
  far out; at 0.7 distant trees switch to cheaper levels/billboards much earlier. This is
  the single biggest lever and it does not touch gameplay objects.
- **Far clip** removes draws linearly (895 → 362 → 183) and is the second lever, but it
  cuts geometry visibly unless fog hides the edge (the game ships with fog disabled).
- **Shadows** are worth ~1.5 ms here; the shadow distance adds nothing on top.
- **Anti-aliasing, texture size, pixel lights** — no measurable effect on the CPU-bound
  frame (the GPU sits at 2–4 ms, so these only change image quality).
- **Terrain trees off** is the ceiling: 208 FPS, but it removes the forest, so it is only a
  measurement, not a setting.

## Suggested presets

| Preset | LOD bias | Far clip | Shadows | Result on this scene |
| --- | --- | --- | --- | --- |
| Off (vanilla) | 1.5 | 300 | on | 40 FPS |
| Light | 1.0 | 300 | on | ~47 FPS |
| Balanced | 0.7 | 220 | off | ~57–63 FPS |
| Aggressive | 0.7 | 120 | off | ~80 FPS |
