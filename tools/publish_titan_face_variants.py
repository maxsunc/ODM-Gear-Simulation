"""Publish reviewed identity revision ONLY. Hash-guard the previous uncommitted variants; preserve GUIDs."""
from pathlib import Path
import hashlib,json,shutil,re
root=Path(__file__).resolve().parents[1]
state=json.loads((root/'Library/TitanFaceVariantRun.json').read_text());blender=Path(state['worker']);assert (blender/'.titan-face-owned').read_text()==str(root)
unity=Path(json.loads((root/'Library/TitanValidationRun.json').read_text())['project']);assert (unity/'.titan-validation-owned').read_text()==str(root)
lock=unity/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
protected=json.loads((root/'Library/TitanFaceProtectedBaseline.json').read_text())
for p,h in protected.items():assert hashlib.sha256((root/p).read_bytes()).hexdigest()==h,('Protected edit; stop/re-audit',p)
baseline=json.loads((root/'Library/TitanFaceIdentityBaseline.json').read_text())
for p,h in baseline.items():assert hashlib.sha256((root/p).read_bytes()).hexdigest()==h,('Concurrent face edit; stop/re-audit',p)
for p in ['Assets/Titan/Prototype/TitanFacePalette.cs','Assets/Titan/Prototype/TitanFaceAppearance.cs','Assets/Editor/TitanFaceVariantBuilder.cs','Assets/Titan/Integration/TitanRuntimeActor.cs','Assets/Titan/Integration/TitanShiganshinaIntegration.cs']:assert (root/p).read_bytes()==(unity/p).read_bytes(),('Stale worker',p)
assert hashlib.sha256((root/'ArtSource/Titan/Reference/TitanReference.blend').read_bytes()).hexdigest()==state['sourceSha256']
assert 'baldBeardAndBlondeSmile=2' in (unity/'titan-faces-build.log').read_text(errors='replace')
assert 'TITAN_FACE_NATIVE_PREVIEW_OK' in (unity/'titan-faces-preview.log').read_text(errors='replace')
folder=Path('Assets/Art/Titans/FaceVariants')
# Verify every pre-existing variant GUID BEFORE replacing any art data.
for p in baseline:
 if p.startswith('Assets/') and p.endswith('.meta'):
  a=re.search(r'^guid: (\w+)',(root/p).read_text(),re.M).group(1);b=re.search(r'^guid: (\w+)',(unity/p).read_text(),re.M).group(1);assert a==b,('GUID mismatch',p)
shutil.copytree(unity/folder,root/folder,dirs_exist_ok=True)
for p in ['Assets/Resources/TitanFacePalette.asset','Assets/Resources/TitanFacePalette.asset.meta']:shutil.copy2(unity/p,root/p)
source=blender/'FaceVariants';dest=root/'ArtSource/Titan/FaceVariants'
for name in ['TitanFaceVariants.blend','TitanFace_Broad.fbx','TitanFace_Gaunt.fbx','face-manifest.json','hair-uv-mask.json','Faces_Blender_Lineup.png']:shutil.copy2(source/name,dest/name)
shutil.copytree(source/'Textures',dest/'Textures',dirs_exist_ok=True)
for name in ['Faces_Unity_Lineup.png','Faces_Unity_JawOpen.png']:shutil.copy2(unity/'Library/TitanFaceReview'/name,dest/name)
for name in ['titan-faces-build.log','titan-faces-preview.log','titan-faces-jaw-preview.log']:shutil.copy2(unity/name,dest/name)
reference=Path('C:/Users/maxsu/Downloads/titansFromAnime.webp');(dest/'Reference').mkdir(exist_ok=True);shutil.copy2(reference,dest/'Reference/titansFromAnime.webp')
report={'identities':['Original dark hair','Bald gray beard and moustache; heavy lids / prominent nose / bare rounded scalp','Blonde short fringe; squinting eyes / wider upturned toothy grin'],'supersedes':'Earlier proportion-only Broad/Gaunt sketches; internal filenames retained to preserve asset GUIDs','authoring':'Blender4.5 MCP addon9877 in separate marker-owned GUI worker; original user instance9876 read-only','unity':'2021.3.25f1 URP12.1.11 compile/import + native graphics reviewed; original/bald/blonde left-to-right; existing bite28degree jaw-axis preview','newArt':'Remove old hair geometry; build closed gray beard/head-sidebeard/moustache and golden cap/fringe/side-lock meshes; head/jaw single original bone weights. No extra renderer or physics','unchanged':'22 rest bones, Animator/controller/allclips, sockets,15skins/17RB/16joints,14damage triggers, HP/AI/nav/combat/size tuning and original art/audio assets','textures':'2 new face-only albedo copies recolor original hair UV islands only; skin/eye/teeth pixels outside mask unchanged.2 shared URPLit face materials clone original. Original0 restores original meshes AND material arrays','runtime':'Same once-per-enemy private-RNG choice; assign existing head/jaw mesh + shared material slots only; beard bottom follows original Jaw; no per-frame writer','protectedCurrentHashesUnchanged':len(protected),'previousUncommittedVariantsBackup':'Library/TitanFaceIdentityBackup','assetGUIDs':'Preserved all previous variant and palette GUIDs. Native Mesh.CreateAsset used instead of CopySerialized after topology change; failed draft preview superseded, not published','noNewTests':True,'limits':'No new full-suite/gameplay/large-crowd/WebGL/physical-bound certification; approved existing animation rig retained, native rest+jaw-open art previews only','committedAtArtCapture':False,'pushedAtArtCapture':False}
(dest/'implementation-report.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
