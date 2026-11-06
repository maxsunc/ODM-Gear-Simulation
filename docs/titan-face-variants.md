# Titan identities: bald/bearded and blonde/smiling

Unity2021.3.25f1 / URP12.1.11. Revised to the user's anime reference
`ArtSource/Titan/FaceVariants/Reference/titansFromAnime.webp`.

The earlier Broad/Gaunt **proportion-only sketches are superseded**, not two extra
choices. The three runtime choices are now:

1. **Original** — approved dark-haired reference Titan, unchanged.
2. **Bald / gray beard** — old hair removed completely; rounded exposed scalp,
   heavy lids, more prominent nose, gray brows, moustache, cheek beard and full
   pointed chin beard.
3. **Blonde / smiling** — new short golden cap/fringe/side locks, golden brows,
   squinting eyes and an upturned wider toothy smile, not a width-only morph.

Start a fresh Play session. Placed/spawned gameplay Titans choose once, equally,
using a private System.Random. A small group is not guaranteed all3 appearances.
The same Titan retains its choice across enable/disable, limb loss and player
respawn. No saved prefab/scene/spawner/nav/size/AI tuning was rebuilt or changed.

## Same animation/gameplay ownership

`TitanFaceAppearance` swaps ONLY the existing Head/Jaw sharedMesh and their shared
material slots once; no Update, root/bone writer, extra renderer, second skeleton,
Animator rebind, new clip, physics hair/beard, joints or colliders. The gray chin
beard is weighted to the original Jaw; moustache/side beard/hair/brows to Head.
All22 original rest bones/matrices and single-bone skin bindings are retained.
Original0 restores BOTH original meshes AND original material arrays.

All choices use the same Animator/controller/clips, body/limb skins,15skins,
17RB/16joints/14damage triggers, sockets, HP/damage/grab/QTE/stomp/death/respawn/
AI/nav logic. New materials are shared asset instances, not instantiated per
Titan. Existing corpse decomposition still clones/restores the selected materials.
Appearance remains cosmetic, including the larger beard silhouette: no new damage
or collision geometry. This is NOT comprehensive combat or mesh-clipping certification.

## Materials and topology

Two NEW albedo atlases recolor ONLY the original hair UV islands; remaining
skin/eye/teeth pixels are copied byte-for-byte. Original atlas/materials and all
body renderers remain unchanged. Each identity has one new URPLit face material
cloned from the actual prefab's original Head material, with only its albedo map
changed; original normal and metallic/smoothness maps/settings retained.
New beard/hair geometry samples those hair atlas islands; existing skin UVs stay.

Different topology is intentional: delete the old hair components and add closed
beard or new haircut geometry. Bald Head8/Jaw5 and Blonde Head9/Jaw3 submesh slots
are populated with the corresponding shared face material. Unlike the obsolete
sketches, no same-topology or same-slot-count claim is made. Native mesh assets
are rebuilt with CreateAsset (not CopySerialized native mesh buffers after topology
changes), checking that existing mesh GUIDs remain identical.

## Paths

- Packed editable studio, exports, reference, source masks/atlases, Blender and
  native Unity lineups/import logs/report: `ArtSource/Titan/FaceVariants/`.
- `Faces_Unity_Lineup.png`: **Original / Bald bearded / Blonde smiling**, left-right.
- `Faces_Unity_JawOpen.png`: isolated original28degree jaw articulation preview,
  not a full feeding animation or beard/body-occlusion certification.
- Runtime FBXs/meshes/materials/textures: `Assets/Art/Titans/FaceVariants/`.
- Catalog: `Assets/Resources/TitanFacePalette.asset` (human-readable identity names).
- Runtime: `Assets/Titan/Prototype/TitanFace{Appearance,Palette}.cs`.
- Existing integration: one Install call in RuntimeActor.Initialize and old
  Shiganshina.Start. No full-body prefab variants required.

Internal FBX/mesh identifiers `Broad`/`Gaunt` are retained as stable asset keys
only: they now mean **BaldBeard / BlondeSmile**, preserving all prior GUIDs.

## Owned authoring / checks

Blender4.5 GUI worker `ODM-TitanFaces-48n0z869`, PID18816, MCP addon port9877.
All authoring used the addon through project transport tools/blender_request.py;
no file-open/save/input/scene operation targeted the user's Blender on9876.
Original Reference.blend hash unchanged. All105 protected current asset/source
hashes unchanged, preserving user's prefab/scene/audio tuning.

Tools: start_titan_face_workspace.py, blender/build_titan_face_variants.py,
blender/render_titan_face_variants.py, bake_titan_face_identity_atlases.py,
Editor/TitanFaceVariantBuilder.cs and publish_titan_face_variants.py.
Only marker-owned Unity2021 worker can rebuild; explicit environment flag
ODM_FACE_REBUILD_OWNED=1 is required to replace existing variant outputs there.
Publisher checks current-source equality, pre-revision hashes and every existing
variant/catalog GUID; prior uncommitted sketches backed up in
Library/TitanFaceIdentityBackup. No deletion/rebuild of original Titan assets.

Blender reports zero boundary/nonmanifold edges, original22 bone matrices and
Head/Jaw groups. Source vertices: bald15142/2266, blonde18702/1650 (Head/Jaw).
Unity imported vertices: bald19901/4773, blonde28233/2430; exact original Head/Jaw
bind matrices. Native graphics exercised actual runtime installer + explicit0/1/2,
with15skins/17RB/16joints each; rest and jaw-open screenshots reviewed. Unity compile
and import completed. Failed draft native-mesh-buffer preview was fixed/replaced
BEFORE publication; do not treat the failed draft as final art.

No new/changed test code or fresh full-suite/current-map/crowd/WebGL/physical-bound
claim. Subjective appearance and animated beard/body occlusion need fresh gameplay
review. Earlier work already pushed9cb2396e. The user subsequently approved these
identities and requested commit + push on titan-combat-updates. This document is
included in that identity checkpoint; use Git log/remote branch for publication
status. Implementation-report commit flags describe the earlier art-capture time.
