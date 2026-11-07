# Main PR validation checkpoint

This is a pre-commit run of the final workspace runtime sources and Titan prefab
using the existing, unfiltered Titan validation runner in its closed,
marker-owned Unity **2021.3.25f1** worker. URP remains **12.1.11**.

```bash
python tools/run_titan_validation.py --reuse \
  'C:/Users/maxsu/AppData/Local/Temp/ODM-TitanValidation-fh_v2o9d' --wait
```

**458 total: 445 passed, 12 failed, 1 skipped; Unity exit code 2.**

The runner stages a scoped project containing `Assets/Tests/TitanPrototype` and
its dependencies. This is the complete suite in that Titan worker, **not every
project/map test assembly**, a standalone/WebGL build, or a gameplay release
certification. No live user Editor was controlled/stopped and no tests or
assertions were added/edited in this checkpoint.

## Failed assertions retained for review

Names below omit the shared `PlayModeTests` suffix from suite names only for
readability. Exact full names, messages and stacks are retained in the XML/JSON.

| Suite / test | Observed assertion |
| --- | --- |
| ModularTitan / IntactBodyHasWorkingAnimationAndKinematicPhysics | Face-zone bounds expected <2; actual 3.32189631. Current saved face-hitbox tuning retained. |
| ScoutHeldStruggle / HeldLoopHasNoSeamSnapAt120HzAndDoesNotModifyTheModelRoot | Seam step expected <2 degrees; actual 3.93702865. Remains an unresolved review item. |
| ScoutHeldStruggle / HeldLoopMovesRealSkinAndRestrainedLimbsWithoutTranslatingOrScalingAnyBone | Clip duration expected 1.2s; current approved Held clip is 1.8s. |
| ScoutPlayer / NarutoRunLoopsWithoutBoneSnapAndCadenceMatchesAuthoredFootTravel | Cadence expected 2 +/-0.05; actual 0.987232029. |
| ScoutPlayer / NarutoRunPlantsTheStanceFootAndTrailsArmsBehindForwardTorso | Toe-off expected -0.0652145371 +/-0.004; actual -0.0750658065. |
| TitanAudio / BlindScreamsOnlyOnNewBlindnessRandomizeBothClipsAndLeaveGameplayRandomUntouched | Expected 2 scream choices; current palette has 1. |
| TitanAudio / PaletteReferencesExactSuppliedFilesAndAllNewActorSourcesUseSfxWithoutPhysics | Expected both historical scream clips; current palette references titanScream.wav only. |
| TitanGrab / BlindnessAndEitherLegLossCancelAndReleaseImmediately | Re-grab begin expected true; actual false. Historical fixture uses a 5s blindness window versus saved 12s tuning. |
| TitanMotionStyle / GaitAndCadenceFollowMeasuredSpeedRatherThanRequestedPace | Cadence expected 0.9-1.1; actual 0.487501413. |
| TitanMotionStyle / HeadLeadsSharpPivotThenRootCommitsAndInjuryStopsTurning | Head lead expected >15 degrees; actual 4.26534796. |
| TitanODMIntegration / EscapeQteBlockedExitCancelsBiteImmediatelyAndKeepsSuspensionUntilSceneryClears | Expected 1s re-grab grace; approved current value 4s. |
| TitanODMIntegration / EscapeQteGraceBlocksAllHandsForOneSecondButNeverBlocksDamage | Expected 1s re-grab grace; approved current value 4s. |

These are recorded failures, not a blanket declaration that all failing tests
are obsolete or harmless. Pose, seam, bounds and cadence assertions still need
review against intended behaviour. Approved tuning, the protected Scout Run clip
and existing assertions were not rewritten to hide the failures.

The skipped check is
`ScoutCapeClothPlayModeTests.NativeGraphicsShowLongerSimulatedCapeWithOriginalScoutArt`:
native capture is explicitly opt-in, whereas this runner uses `-nographics`.

## Other receipts and remaining review

- `ArtSource/Camera/Response/`: latest focused camera/respawn/hold/audio integration
  selection, **26 passed / 0 failed / 0 skipped**, before this broader run.
- `ArtSource/UI/InGameMinimal/`: focused wind/UI integration selection **26/0**,
  broader historical selection **165 passed / 2 failed** (the two 1s-vs-4s grace
  assertions), and explicitly static native UI previews.
- Earlier subsystem receipts remain historical evidence, not a fresh all-project
  result for this checkpoint.
- Camera feel/collision edge cases, wind seam/balance, menu pointer/keyboard
  interactions, actual district routes, crowd performance and device/WebGL/DSP
  behaviour still require hands-on review. Static art does not certify these.

`titan-suite.xml` and `titan-suite.log` are unmodified worker outputs.
`validation-summary.json` records the failed cases and source hashes. Git history
records subsequent commit/push/PR publication; this receipt predates publication.
