# Minimal Stats and J menu — native art review

Actual current runtime uGUI, captured in the closed marker-owned Unity2021.3.25f1
worker on Intel Iris Xe. No Play, synthetic input, native physics, preference
writes, live Editor takeover, or saved scene modification.

- `Stats_{GiantForest,Shiganshina}_{1920x1080,1440x1080}.png`: title **Stats** and
  exactly four label/value rows: Kills, Total kills, Damage done, Limbs severed.
- `Pause_{GiantForest,Shiganshina}_{1920x1080,1440x1080}.png`: shared J-menu layout,
  current map, Resume/Main menu/Quit and Master/SFX/Music.
- Backgrounds are existing saved map photo assets; these are static authoring
  previews, not fresh gameplay screenshots. Stats are zero authoring defaults;
  slider percentages are100% authoring defaults, not the user's saved preferences.
- `source-art-summary.json` hashes the current UI inputs. Recreate with
  `python tools/capture_ingame_ui.py` after refreshing the closed worker with the
  existing validation runner. Worker-only capture code is never published in Assets.

## Existing regressions (no new/modified tests)

- `focused-existing-regressions.xml` / `.log`: **26 passed,0 failed,0 skipped**;
  existing mixer/real motor suspension/health/held-damage/player-only respawn checks.
- `broader-existing-regressions.xml` / `.log`: **165 passed,2 failed** in the broader
  audio/ODM integration selection. The two existing QTE cases still assert1second:
  `EscapeQteBlockedExitCancelsBiteImmediatelyAndKeepsSuspensionUntilSceneryClears`
  and `EscapeQteGraceBlocksAllHandsForOneSecondButNeverBlocksDamage`. The approved
  regrab grace is4seconds in the already-published source. It was not changed to
  satisfy these historical assertions, and no test code was edited.

These are not full-suite results or new UI interaction/wind-audio audition proof.
The preceding wind-loop request is implemented alongside the UI; see
`docs/player-flight-wind.md` and `docs/frontend-and-field-stats.md` for behavior,
tuning, lifecycle, boundaries and remaining hands-on checks.

At native-art capture these changes were local, uncommitted and unpushed.
Subsequent publication is recorded in Git history. Existing map scenes, user
prefab tuning, palettes, physics, rig/animation assets and unrelated frontend work
were preserved. See `preservation-summary.json` for the source check.
