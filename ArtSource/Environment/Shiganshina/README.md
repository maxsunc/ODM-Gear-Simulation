# Shiganshina district art and native assembly

Original architecture made in an owned factory/background Blender4.5 process.
No live Blender/editor control, copied game assets, new/modified test code, commit
or push. Unity2021.3.25f1 / URP12.1.11 only.

- **Shiganshina_Architecture.blend** — packed editable17-archetype architecture
  library plus linked street-composition studio. Source objects remain editable;
  texture images packed. The studio ground/camera/lights are not game exports.
- **Blender_Street_Architecture.png** — native Cycles model review.
- **asset-report.json** — per-archetype3-LOD/structural-collision triangle counts.
- **map-report.json** — actual generated Unity scene/scale/placement/bake metadata.
- **MapReview/** — actual native Unity static renders, Intel Iris Xe:
  District_Overview, Gate_Avenue, Rooftop_Route, Canal_Bridges, Civic_Square,
  Outer_Gate, Town_Plan. These are Edit-mode artwork, NOT gameplay/physics tests.
  Scout rest pose in the entry plate does not represent the runtime animation.
- **native-map-build.log / native-map-preview.log / blender-build.log** — native
  authoring/import/render receipts (preview log is under MapReview).
- **existing-regressions.xml / .log** —31 existing fixture checks passed. Filtered;
  not whole-suite/new-map-route/crowd/hold/corpse or subjective swing certification.
- **preservation-summary.json** — hashes of prior work protected at publication,
  deliberate menu/build-list changes and final published artifact fingerprints.

Game exports/materials/prefabs: Assets/Art/Environment/Shiganshina/
Actual new scene: Assets/Scenes/ShiganshinaDistrict.unity
Exact3/5/6x baked navigation: Assets/Navigation/Titans/Shiganshina/
Main menu: Free Roam → Shiganshina → Deploy.

Original district composition, not an exact canonical street-plan reconstruction.
2048²ground footprint matches current forest terrain,1400×1388m rounded enclosure,
303 houses/shops/warehouses,160 wall bays,2closed gates,3bridges, civic landmarks.
Closed building exteriors/static windmills; no destructible city/interiors/swimming.

Full implementation, scale/ODM rationale, limitations and isolated authoring:
../../../docs/shiganshina-district.md

Local capture status: uncommitted, not pushed. Prior cc26cf0a is a separate eye/grace
checkpoint, not this map/frontend feature commit. Draft source backups are outside
source control in Library/ShiganshinaDraftSources, not published art authority.
