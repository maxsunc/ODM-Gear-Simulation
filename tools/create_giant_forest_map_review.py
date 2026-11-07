"""Publish labelled native Unity captures only; no generated/concept imagery."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,shutil,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[1]
worker=Path(json.loads((root/'Library/GiantForest2BuildRun.json').read_text())['project'])
assert (worker/'.giant-forest-owned').read_text()==str(root)
source=worker/'Library/GiantForest2';out=root/'ArtSource/Environment/GiantForest2/MapReview';out.mkdir(exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',24);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
shots=[('RuntimeEntry','Forest entrance / real Scout and Titans'),('Gameplay','Player camera / warmed native rendering'),('RuntimeOverview','Whole enclosure / overview fog temporarily off'),('RuntimeGate','Double-height walls / actual open arch'),('RuntimeRocks','Mixed rock sizes / largest ~30m mesh height')]
sheet=Image.new('RGB',(2040,2160),(20,30,27));draw=ImageDraw.Draw(sheet)
draw.text((25,18),'GIANT FOREST 2.0 | UNITY 2021.3.25f1',font=font,fill='white')
draw.text((25,52),'208 trees ~150-216m | 72m largest Titan reference | native owned Play Mode captures',font=small,fill=(190,210,196))
for i,(name,caption) in enumerate(shots):
 shutil.copy2(source/(name+'.png'),out/(name+'.png'))
 image=Image.open(source/(name+'.png')).convert('RGB');image.thumbnail((990,625),Image.Resampling.LANCZOS)
 x=20+(i%2)*1020;y=102+(i//2)*680;sheet.paste(image,(x,y));draw.text((x,y+630),caption,font=small,fill='white')
draw.text((1040,1485),'ROCK / WALL SCALE ITERATION',font=font,fill='white')
draw.text((1040,1530),'40 rocks: 8 small, 16 medium, 16 large',font=small,fill=(190,210,196))
draw.text((1040,1565),'Measured rock mesh heights: ~4.3-30.1m',font=small,fill=(190,210,196))
draw.text((1040,1600),'Walls: 2x height (~121m), same width and base',font=small,fill=(190,210,196))
draw.text((25,2130),'Actual game assets. Camera.Render captures omit screen-space HUD. No full-speed traversal/FPS or WebGL build claim.',font=small,fill=(190,210,196))
sheet.save(out/'GiantForest2_MapPreview.png')
shutil.copy2(source/'build-report.json',out/'build-report.json')
validation={}
for action in ['test','graphics']:
 path=worker/('gf2-'+action+'-results.xml');validation[action]=ET.parse(path).getroot().attrib;shutil.copy2(path,out/path.name)
(out/'validation-summary.json').write_text(json.dumps(validation,indent=2))
print(out/'GiantForest2_MapPreview.png')
