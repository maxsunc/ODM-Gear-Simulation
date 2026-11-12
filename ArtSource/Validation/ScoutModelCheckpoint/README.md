# Scout model and current project checkpoint — publication review

The user accepted the first model overhaul and requested committing/pushing all current project work to `main`. Git history is the publication authority; local/uncommitted wording in original art receipts records capture-time state.

## Included work

- Refined Scout body mesh/material, original editable Blender 5.2 source, full art FBX export, matched native before/after images and guarded authoring tools. Original cape, runtime cloth, 43-bone rig, weapon/grip geometry and all animation assets remain unchanged by the model pass.
- Deferred Android support plan. Android module/SDK/NDK/OpenJDK directories are now present in the local Unity 2021 installation, and tracked generated C# project files gained corresponding references. No touch controls, Android build or device certification is included.
- Preserve the user's current Titan prefab edits: nape HP **1000**, run turn speed **50**, and the saved resized/repositioned `Nape_DamageZone` trigger.
- Preserve current AI defaults: sight range **258**, field of view **300**, patrol speed **8**, chase speed **20.5**, search turn speed **75**, search look angle **60**. Defaults do not automatically replace existing serialized instance overrides.
- Remove one accidentally duplicated, identical `patrolSpeed` field declaration, which otherwise prevents compilation. No other tuning was reset.

## Fresh existing checks

A closed marker-owned Unity **2021.3.25f1** worker compiled the current source, exact Scout mesh/prefab and exact Titan prefab. Selected existing tests: **69 total / 64 passed / 4 failed / 1 skipped**, duration **11.768088 seconds**. All **nine patrol/chase cases passed**. The four failures and their messages are identical to the earlier original-model control/refined-model checks:

- Run cadence: expected 2 ± .05, actual .987232029.
- Run stance support/toe-off: expected −.0652145371 ± .004, actual −.0750658065.
- Held loop seam: expected <2°, actual 3.93702865°.
- Held loop duration: fixture expects 1.2 s, current authored duration 1.8 s.

The existing native graphics cape case remained skipped because it is explicitly opt-in. No tests/assertions were added or modified. Source hashes, exact results/filter and retained failure messages are in `checkpoint-validation.json`; raw XML/log are retained alongside it.

This is not a green full suite, standalone/Android build, gameplay/crowd/contact/cloth performance or device certification. Historical broad-suite and Forest failures remain unresolved; later scoped passes do not supersede them. No live user Play session, scene or Blender file was commandeered.

Machine-local `.plastic`/`.vs` data, Python caches, Unity share settings and ignored `Library` captures/workers are deliberately excluded from publication and left intact.
