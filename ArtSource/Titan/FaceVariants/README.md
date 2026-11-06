# Anime-reference Titan identities

The old broad/gaunt proportion-only studies have been REPLACED following user feedback.

**Faces_Unity_Lineup.png** (left to right):
1. Original approved dark-haired Titan.
2. Bald, gray-bearded Titan: exposed rounded scalp, gray moustache/brows/cheek-and-chin beard, heavy lids and prominent nose.
3. Blonde smiling Titan: new golden short fringe/side locks, squint and upturned toothy grin.

See Reference/titansFromAnime.webp supplied by the user.
Blender4.5 MCP modeling in a separate owned GUI worker; live user's Blender not altered.
TitanFaceVariants.blend is the editable packed studio; source FBXs retain the
legacy Broad/Gaunt keys ONLY to preserve existing GUIDs (BaldBeard/BlondeSmile).
Runtime catalog contains the descriptive new names. Same22-bone rig and all original
animations/gameplay; new hair/beard is render geometry, never new physics.

Textures are new face-only atlas copies, original hair UV islands recolored gray/gold;
all non-hair pixels and original project texture/material files remain intact.
Native rest and isolated28degree jaw-open previews used the actual runtime installer.
Jaw-only preview is NOT a full feeding animation: long beard can be occluded by the
body as the jaw rotates. No new animation writer or collision workaround was added.
No new test code, full-suite/crowd/WebGL or comprehensive combat-clipping certification.

See ../../../docs/titan-face-variants.md (project docs) and implementation-report.json.
Prior uncommitted sketch backup is outside source control: Library/TitanFaceIdentityBackup.
