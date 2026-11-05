# Giant Forest 2.0 — approved source art / assembled map

**New: [native Unity map preview](MapReview/GiantForest2_MapPreview.png)** ·
**[map setup / validation](../../../docs/giant-forest-2-map.md)**

User approved the art and authorized assembly, with varied trees ~2–3x the largest
Titan. `Assets/Scenes/GiantForest2.unity` now contains the new map:208 trees enlarged
to ~150–216m, physical heightfield, walls/gateway, props and own3x/5x/6x navigation.
Latest iteration: walls2x taller (~121m) with original width/base;40 mixed rocks,
8 small/16 medium/16 large, actual mesh heights~4.3–30.1m. Native active-actor captures/
results are in `MapReview/`, including `RuntimeRocks.png`; overview temporarily fog-off.
6/6 headless map cases +1/1 graphics pass. No full-speed traversal/FPS/WebGL build claim.
User authorized a LOCAL forest checkpoint commit after this iteration; no push requested.

**Original source-art pass: [six-view review sheet](Review/GiantForest2_ReviewSheet.png).**

- [Tree lineup and scale](Review/Trees_Lineup.png)
- [Root/bark close-up](Review/Roots_Closeup.png)
- [Canopy close-up](Review/Canopy_Closeup.png)
- [Wall/gateway lineup](Review/Wall_Modules.png)
- [Stone close-up](Review/Stone_Closeup.png)
- [Ground, log and rocks](Review/Ground_Props.png)

Open **GiantForest2_ReviewStudios.blend** in Blender4.5 for the six camera studios.
Scene dropdown `GF2_Source_*` exposes individual EDITABLE source meshes; `GF2_View_*`
provides review views. All material images packed. Grey mannequin is ONLY a60m height
guide, gold figure a3.6m player guide; neither is proposed replacement character art.

**13 individual model exports** in `Exports/`, baked maps in `Textures/`. FBX exports
are one mesh per asset with material submeshes; sources keep editable branch/stone parts.
The original models/studio sizes here remain editable and unchanged. Approved
runtime derivatives, three tree LODs, wood-only colliders and shared URP materials
are now imported under `Assets/Art/Environment/GiantForest2`. The old copied map was
replaced ONLY in GiantForest2; original FreeRoam is unchanged. Further ground/foliage,
lighting/LOD/wind/performance refinement remains open to playtesting feedback.

See [full scope, dimensions, caveats and reproduction](../../../docs/giant-forest-2-art-review.md).
`asset-report.json`: bounds/triangles/materials. `validation-report.json`:13/13 owned
FBX roundtrips and source UV/paint/closed-component checks; NOT Unity gameplay tests.

Studio renders are genuine model renders, NOT conceptual images or new-map gameplay.
Original studio review was art-direction evidence, not runtime approval. The new
map now has LOD/collision/terrain/material/nav validation, but not wind/FPS/full-speed
traversal certification. Existing Unity2021.3.25f1 pipeline and gameplay art remain intact.
