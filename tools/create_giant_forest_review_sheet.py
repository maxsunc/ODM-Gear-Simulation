"""Caption the real Blender renders without altering model pixels. Requires Pillow."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'ArtSource/Environment/GiantForest2/Review'
def font(size):
    try:return ImageFont.truetype('C:/Windows/Fonts/arial.ttf',size)
    except OSError:return ImageFont.load_default()
views=[
 ('Trees_Lineup','GIANT TREES / silhouette and scale','Left: Sentinel / middle: Leaning / right: Old Fork. Grey: 60 m guide only. Gold: 3.6 m player.'),
 ('Wall_Modules','STONE WALL KIT / straight, corner, gateway','~60 m walls. 24 m straight bay / 24 m corner grid / 48 m gate module. Real arched opening.'),
 ('Roots_Closeup','TREE BASE / roots and bark','Large fluted trunk and buttress roots. Gold figure = 3.6 m player height.'),
 ('Canopy_Closeup','CROWN DETAIL / curved limbs and foliage','Textured branch-spray leaf cards over actual woody branches. First-pass foliage, not final wind/LOD.'),
 ('Ground_Props','FOREST FLOOR / ground and prop palette','32 m loam, moss bank, worn-soil patches / 32 m snapped trunk / three boulders. Sample panels, not a map.'),
 ('Stone_Closeup','STONE DETAIL / masonry and weathering','Bevelled limestone courses, pilasters, damp base and belt course. First art direction for review.')]
for name,title,caption in views:
    raw=Image.open(OUT/(name+'_Raw.png')).convert('RGB');frame=Image.new('RGB',(raw.width,raw.height+140),(20,28,30));frame.paste(raw,(0,76));d=ImageDraw.Draw(frame)
    d.text((22,18),title,font=font(29),fill=(232,236,224));d.text((22,raw.height+94),caption,font=font(20),fill=(194,207,195));frame.save(OUT/(name+'.png'))
W=2200;CW=1100;CH=800;sheet=Image.new('RGB',(W,CH*3+150),(16,23,25));d=ImageDraw.Draw(sheet)
d.text((30,18),'GIANT FOREST 2.0 / FIRST MODEL REVIEW',font=font(38),fill=(232,236,224))
d.text((30,67),'Original mesh + texture assets. Studio renders only; no gameplay map assembly yet.',font=font(23),fill=(184,201,183))
for i,(name,title,caption) in enumerate(views):
    x=(i%2)*CW;y=120+(i//2)*CH;image=Image.open(OUT/(name+'.png')).convert('RGB');image=ImageOps.contain(image,(CW-16,CH-12),Image.Resampling.LANCZOS);sheet.paste(image,(x+(CW-image.width)//2,y))
sheet.save(OUT/'GiantForest2_ReviewSheet.png');print(OUT/'GiantForest2_ReviewSheet.png')
