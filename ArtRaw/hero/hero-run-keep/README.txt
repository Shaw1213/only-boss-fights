DreamLayer sprite sheet
Action: run
7 frames, row-major order, 256 x 256 cells, 3 columns.
Duration: 2000 ms. Sampling: cycle. A sequence is not guaranteed to loop. Shared scale; alignment: ground_anchor. Enlarged: False; enlargement adds no source detail.
Unity: slice the sheet into 256 x 256 cells.
Godot: create SpriteFrames using a 3 x 3 grid; use only the 7 atlas entries.
Phaser: load sheet.png and atlas.json with load.atlas.
