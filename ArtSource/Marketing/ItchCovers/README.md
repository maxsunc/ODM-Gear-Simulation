# Itch.io cover choices

Four local promotional compositions made from this game's original Scout, Titan and environment assets. No official Attack on Titan logo, external character artwork, or copied promotional art is used. These are **posed promotional composites**, not gameplay screenshots.

## Choose a direction

Open **`Choose_A_Cover.png`** for the large comparison, or **`Small_Card_Comparison.png`** to compare actual 315×250 cards.

| Choice | Upload image | Direction |
| --- | --- | --- |
| 01 / Rooftop Run | `01_Rooftop_Run.png` | Shiganshina rooftops, airborne Scout, dual blades, warm-gold title. Recommended general-purpose cover. |
| 02 / Giant Hunt | `02_Giant_Hunt.png` | Looming bald/beard Titan, forest, attacking Scout, olive/parchment palette. |
| 03 / Red Alert | `03_Red_Alert.png` | Smiling blonde Titan close-up, red/black diagonal panel, Scout winding up. Boldest combat/threat option. |
| 04 / Skybound | `04_Skybound.png` | Flying Scout and long cape over the forest, cleaner teal movement-first look. |

Each numbered PNG is **1260×1000** (4× the 315×250 cover ratio), opaque RGB, lossless PNG. The matching `_315x250.png` file is a small-size preview. Option labels appear only in comparison sheets, never on the upload artwork.

Matching **`.ora` OpenRaster files** contain full-resolution named raster layers, including scene plate, color treatment, subjects, shadows, motion accents and title. Open these in Krita or GIMP for adjustments. Typography is rasterized; change/re-render it with the composition script. Windows Impact and Agency Bold fonts are used but no font files are redistributed.

`Subjects/` contains the native neutral-studio renders and extracted transparent cutouts for reuse. `cover-manifest.json` records source-image hashes, layer names, dimensions and source preservation. Motion strokes and atmosphere are graphic treatments, not promises of additional gameplay or rendering effects.

## Recreate

From the repository root:

```bash
# Native subject render: requires the existing CLOSED marker-owned map worker,
# Unity 2021.3.25f1 and current approved project models. Never targets live Unity.
python tools/render_itch_cover_subjects.py

# Composition-only environment, isolated under ignored Library/.
python -m venv Library/ItchCoverTools
Library/ItchCoverTools/Scripts/python.exe -m pip install Pillow numpy
Library/ItchCoverTools/Scripts/python.exe tools/create_itch_covers.py
```

Subject renders use native URP materials, existing aerial-slash/flight/charge clips, approved shared-rig face variants, and render-only cape posing. Unity ran closed and batch-owned on Intel Iris Xe, with no Play mode, native physics stepping, live-editor input, scene save, source asset edits or import-time authoring tool added to the game. The final composition checked all **1573** pre-existing `Assets`/`ProjectSettings` files against the initial artwork snapshot and found no byte changes. Existing unrelated local edits were retained.

No itch.io account/page was accessed and nothing was uploaded there. Repository publication includes the editable cover deliverables; it does not publish a cover to itch.io.
