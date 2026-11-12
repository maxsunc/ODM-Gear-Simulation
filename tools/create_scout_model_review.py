"""Labelled comparison sheets from actual owned Unity captures; no generated imagery."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'ArtSource/Player/Scout/ModelOverhaul'
FONT=Path('C:/Windows/Fonts/segoeui.ttf')
BOLD=Path('C:/Windows/Fonts/segoeuib.ttf')
def font(size,bold=False):
    path=BOLD if bold else FONT
    return ImageFont.truetype(str(path),size) if path.exists() else ImageFont.load_default()
def sheet(name,views,columns,size,title,footer):
    w,h=size;gap=16;header=92;label=34;bottom=58
    rows=(len(views)+columns-1)//columns
    image=Image.new('RGB',(gap+(w+gap)*columns,header+rows*(h+label+gap)+bottom),(20,27,33))
    d=ImageDraw.Draw(image)
    d.text((20,12),title,font=font(29,True),fill=(229,232,229))
    d.text((20,53),'Unity 2021.3 / URP 12 | Existing rig and animation clips | Matched studio lighting',font=font(17),fill=(161,182,185))
    for i,(file,caption) in enumerate(views):
        x=gap+(i%columns)*(w+gap);y=header+(i//columns)*(h+label+gap)
        d.text((x,y),caption,font=font(20,True),fill=(205,219,213))
        with Image.open(OUT/file) as source:
            tile=source.convert('RGB').resize((w,h),Image.Resampling.LANCZOS)
        image.paste(tile,(x,y+label))
    d.text((20,image.height-bottom+10),footer,font=font(16),fill=(163,181,182))
    image.save(OUT/name)
sheet('Compare_Front.png',[('Before_Front.png','BEFORE'),('After_Front.png','AFTER')],2,(720,880),'SCOUT | MODEL-ONLY OVERHAUL','Static authored pose, not a gameplay capture. Cape simulation is not running.')
sheet('Compare_Face.png',[('Before_Face.png','BEFORE'),('After_Face.png','AFTER')],2,(800,800),'SCOUT | FACE AND HAIR','Original model on the left; refined body mesh/material on the right. No facial animation added.')
sheet('Overview.png',[(f'After_{name}.png',label) for name,label in [('Front','FRONT'),('ThreeQuarter','THREE-QUARTER'),('Back','BACK — ORIGINAL STATIC CAPE'),('Run','EXISTING RUN'),('Flight','EXISTING GAS / FLIGHT'),('Sword','EXISTING SWORD POSE')]],3,(420,514),'SCOUT | MODEL REVIEW','Static samples of existing clips. Runtime simulated cape, motion feel and collisions need fresh Play review.')
print(OUT/'Compare_Front.png')
print(OUT/'Compare_Face.png')
print(OUT/'Overview.png')
