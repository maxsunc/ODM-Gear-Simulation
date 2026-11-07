"""Compose four original itch.io cover choices from native game art.
Run render_itch_cover_subjects.py first (closed owned Unity worker only).
No live Editor, game/scene changes, generated gameplay claims or external art.
Requires Pillow and numpy. Windows fonts are rasterized, not redistributed.
"""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageFilter,ImageEnhance,ImageOps
import numpy as np
import json,hashlib,io,zipfile,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[1];O=R/'ArtSource/Marketing/ItchCovers';SIZE=(1260,1000)
FONTS=Path('C:/Windows/Fonts');RES=Image.Resampling.LANCZOS
X,Y=np.meshgrid(np.linspace(0,1,SIZE[0]),np.linspace(0,1,SIZE[1]))
font=lambda n,s:ImageFont.truetype(str(FONTS/n),s)
manifest={'purpose':'Locally authored promotional cover options; posed composites, not gameplay screenshots.','size':list(SIZE),'previewSize':[315,250],'sources':{},'options':[]}
def source(rel):
 p=R/rel;manifest['sources'][rel]=hashlib.sha256(p.read_bytes()).hexdigest();return Image.open(p).convert('RGB')
def wash(color,amount):
 a=np.broadcast_to(np.asarray(amount),(SIZE[1],SIZE[0]));arr=np.zeros((SIZE[1],SIZE[0],4),dtype=np.uint8);arr[:,:,:3]=color;arr[:,:,3]=np.uint8(np.clip(a,0,1)*255);return Image.fromarray(arr)
def glow(color,cx,cy,radius,strength):return wash(color,np.exp(-((X-cx)**2+(Y-cy)**2)/(2*radius**2))*strength)
def grade(im,dark,light,strength=.4,brightness=1):
 colored=ImageOps.colorize(ImageOps.grayscale(im),dark,light);return ImageEnhance.Brightness(Image.blend(im,colored,strength)).enhance(brightness)
def background(rel,dark,light,blend=.4,bright=1,centering=(.5,.5)):
 return grade(ImageOps.fit(source(rel),SIZE,RES,centering=centering),dark,light,blend,bright).convert('RGBA')
def subject(name):
 rel=f'ArtSource/Marketing/ItchCovers/Subjects/{name}_Render.png';im=source(rel);a=np.asarray(im,dtype=float);bg=a[0,0];distance=np.max(abs(a-bg),axis=2)
 # Neutral preview background is exactly flat. Keep interior grayscale materials;
 # only flood-fill border-connected background so metal/eyes cannot be keyed out.
 candidate=Image.fromarray(np.uint8(distance<=3)*255).convert('RGB');ImageDraw.floodfill(candidate,(0,0),(1,0,0),thresh=0)
 connected=np.all(np.asarray(candidate)==(1,0,0),axis=2);alpha=np.where(connected,0,np.minimum(1,distance/22));rgba=np.dstack([a,np.round(alpha*255)]).astype('uint8');out=Image.fromarray(rgba)
 bbox=out.getchannel('A').getbbox();assert bbox and np.mean(connected)>.12,(name,'Invalid studio mask');out=out.crop(bbox);out.save(O/'Subjects'/f'{name}.png');return out
SUB={n:subject(n) for n in ['Scout_Slash','Scout_Flight','Scout_BackFlight','Scout_Guard','Titan_Beard','Titan_Blonde']}
def place(im,x,y,width,angle=0):
 im=im.resize((width,round(im.height*width/im.width)),RES)
 if angle:im=im.rotate(angle,Image.Resampling.BICUBIC,expand=True)
 layer=Image.new('RGBA',SIZE);layer.paste(im,(x,y));return layer

def ink_text(text,pos,ft,colors,track=0):
 mask=Image.new('L',SIZE);d=ImageDraw.Draw(mask)
 if track:
  x,y=pos
  for c in text:d.text((x,y),c,font=ft,fill=255,anchor='lt');x+=d.textlength(c,font=ft)+track
 else:d.text(pos,text,font=ft,fill=255,anchor='lt')
 top,bottom=colors;a=np.zeros((SIZE[1],SIZE[0],4),dtype=np.uint8);rows=np.arange(SIZE[1])
 yy=np.clip((rows-pos[1])/max(ft.size*.8,1),0,1)
 a[:,:,:3]=((np.asarray(top)[None,:]*(1-yy[:,None])+np.asarray(bottom)[None,:]*yy[:,None])[:,None,:]).astype('uint8');a[:,:,3]=np.asarray(mask);return Image.fromarray(a)
class Cover:
 def __init__(self,name):self.name=name;self.layers=[];self.im=Image.new('RGBA',SIZE)
 def push(self,name,im):
  self.layers.append((name,im.copy()));self.im=Image.alpha_composite(self.im,im)
 def hero(self,name,im,rim=(164,226,225),light=.9):
  alpha=im.getchannel('A');shadow=Image.new('RGBA',SIZE,(0,0,0,0));shadow.putalpha(alpha.filter(ImageFilter.GaussianBlur(19)).point(lambda p:int(p*.75)));self.push(name+' soft shadow',shadow)
  edge=Image.new('RGBA',SIZE,rim+(0,));edge.putalpha(ImageChops_sub(alpha.filter(ImageFilter.MaxFilter(7)),alpha).point(lambda p:int(p*.32)));self.push(name+' subtle rim',edge)
  arr=ImageEnhance.Brightness(im.convert('RGB')).enhance(light).convert('RGBA');arr.putalpha(alpha);self.push(name,arr)
 def title(self,x,y,size=248,white=(255,248,225),gold=(220,178,94),subtitle=52,wide=False):
  self.push('Title soft backing',wash((4,10,15),np.exp(-(((X-(x+230)/1260)/.36)**2+((Y-(y+100)/1000)/.25)**2))*.40))
  self.push('ODM title',ink_text('ODM',(x,y),font('impact.ttf',size),(white,gold),track=4 if wide else 0))
  self.push('GEAR SIMULATION subtitle',ink_text('GEAR SIMULATION',(x+4,y+size*.83),font('AGENCYB.TTF',subtitle),(white,white),track=3))
  line=Image.new('RGBA',SIZE);d=ImageDraw.Draw(line);d.polygon([(x+4,y+size*.83+subtitle+15),(x+160,y+size*.83+subtitle+15),(x+150,y+size*.83+subtitle+21),(x+4,y+size*.83+subtitle+21)],fill=gold+(255,));self.push('Title underline',line)
 def save(self,label,description):
  self.im.convert('RGB').save(O/f'{self.name}.png',optimize=True)
  self.im.convert('RGB').resize((315,250),RES).save(O/f'{self.name}_315x250.png',optimize=True)
  # OpenRaster preserves full-resolution named layers in Krita/GIMP.
  root=ET.Element('image',{'w':'1260','h':'1000','name':self.name,'version':'0.0.3'});stack=ET.SubElement(root,'stack')
  with zipfile.ZipFile(O/f'{self.name}.ora','w') as z:
   z.writestr('mimetype','image/openraster',compress_type=zipfile.ZIP_STORED)
   for i,(name,im) in reversed(list(enumerate(self.layers))):
    src=f'data/layer{i:02}.png';b=io.BytesIO();im.save(b,format='PNG');z.writestr(src,b.getvalue(),compress_type=zipfile.ZIP_DEFLATED)
    ET.SubElement(stack,'layer',{'name':name,'src':src,'opacity':'1.0','visibility':'visible','composite-op':'svg:src-over','x':'0','y':'0'})
   z.writestr('stack.xml',ET.tostring(root,encoding='utf-8',xml_declaration=True));b=io.BytesIO();self.im.save(b,format='PNG');z.writestr('mergedimage.png',b.getvalue());b=io.BytesIO();self.im.copy().resize((256,203),RES).save(b,format='PNG');z.writestr('Thumbnails/thumbnail.png',b.getvalue())
  manifest['options'].append({'file':f'{self.name}.png','label':label,'description':description,'layers':[n for n,_ in self.layers]});return self.im
from PIL.ImageChops import subtract as ImageChops_sub

def speedmarks(color,diagonal=1):
 im=Image.new('RGBA',SIZE);d=ImageDraw.Draw(im)
 for i,(x,y,length) in enumerate([(1120,100,160),(1050,210,170),(1250,350,240),(0,670,160),(10,720,115),(1120,820,200)]):
  d.line([(x,y),(x-length,y+diagonal*length*.45)],fill=color+(65,),width=2 if i%2 else 3)
 return im

# 01 — architecture, action, warmer stone vs teal atmosphere.
c=Cover('01_Rooftop_Run')
c.push('Actual Shiganshina rooftop plate',background('ArtSource/Environment/Shiganshina/MapReview/Rooftop_Route.png','#092f3c','#e3d1a0',.25,1.04))
c.push('Left and bottom readability',wash((4,19,27),np.maximum((1-X)**2*.65,Y**4*.85)))
c.push('Warm upper-right haze',glow((255,219,145),.85,.12,.34,.28))
c.push('Motion accents',speedmarks((213,241,239)))
c.hero('Scout aerial cut',place(SUB['Scout_Slash'],485,90,735,-11),light=1)
c.title(66,642,238,subtitle=52)
first=c.save('01 / ROOFTOP RUN','City action: Scout, dual blades, teal-and-gold district rooftops.')

# 02 — close Titan portrait against real forest; title and scout oppose it.
c=Cover('02_Giant_Hunt')
c.push('Actual giant-forest plate',background('ArtSource/Environment/GiantForest2/MapReview/RuntimeRocks.png','#0b1711','#b4ac79',.66,.8))
c.push('Forest atmosphere',glow((184,197,126),.72,.12,.55,.22))
titan=place(SUB['Titan_Beard'],350,-30,1250)
# Reduce the flat chest prominence; the visible portrait stays actual game geometry.
c.hero('Bald-beard Titan portrait',titan,rim=(224,200,131),light=.79)
c.push('Title-side shadow',wash((4,16,14),np.clip((.62-X)*1.2,0,.7)))
c.push('Lower fog',wash((7,19,15),np.clip((Y-.57)*1.6,0,.66)))
c.hero('Scout foreground slash',place(SUB['Scout_Slash'],-15,475,650,12),rim=(215,223,181),light=.91)
c.title(67,79,237,white=(255,251,220),gold=(214,185,112),subtitle=51)
second=c.save('02 / GIANT HUNT','Forest hunt: looming beard Titan and a smaller attacking Scout; olive and parchment.')

# 03 — bold red graphic threat; the smile is from the actual BlondeSmile asset.
c=Cover('03_Red_Alert')
c.push('Actual district plate, crimson grade',background('ArtSource/Environment/Shiganshina/MapReview/Gate_Avenue.png','#2c030a','#e57d55',.82,.68))
c.push('Red atmosphere',wash((112,8,24),.25))
face=place(SUB['Titan_Blonde'],230,-190,1400)
# High-contrast duotone treatment is confined to this promotional layout.
col=grade(face.convert('RGB'),'#230918','#f5bd8a',.68,.83).convert('RGBA');col.putalpha(face.getchannel('A'));c.hero('Smiling Titan portrait',col,rim=(238,78,64),light=1)
plane=Image.new('RGBA',SIZE);d=ImageDraw.Draw(plane);d.polygon([(0,0),(615,0),(885,1000),(0,1000)],fill=(12,10,19,222));d.line([(583,0),(853,1000)],fill=(245,76,57,255),width=10);c.push('Diagonal alert panel',plane)
c.push('Bottom vignette',wash((11,6,14),Y**4*.65))
c.hero('Scout aerial guard',place(SUB['Scout_Guard'],40,488,670,11),rim=(246,107,79),light=.94)
c.title(62,82,240,white=(255,245,218),gold=(244,175,135),subtitle=51)
third=c.save('03 / RED ALERT','Most graphic: red-black diagonal, smiling Titan close-up and armed Scout.')

# 04 — cleaner exploration/movement-first card with readable flowing cape.
c=Cover('04_Skybound')
c.push('Actual forest architecture',background('ArtSource/Environment/GiantForest2/MapReview/RuntimeRocks.png','#032d31','#b4d2ac',.53,.92,centering=(.65,.22)))
c.push('Teal depth',wash((2,30,36),np.maximum((1-X)*.40,Y**3*.86)))
c.push('Sky glow',glow((203,242,216),.75,.10,.44,.34))
c.push('Motion accents',speedmarks((201,242,235),-1))
c.hero('Scout flight and long cape',place(SUB['Scout_BackFlight'],310,162,920,18),rim=(193,241,212),light=1.02)
c.title(66,646,237,white=(246,255,233),gold=(163,222,202),subtitle=52,wide=True)
fourth=c.save('04 / SKYBOUND','Movement first: clean teal forest, flying Scout and long cape; no Titan portrait.')

# Neutral review sheets never add option labels onto the actual upload files.
photos=[first,second,third,fourth];sheet=Image.new('RGB',(1340,1216),(13,19,25));d=ImageDraw.Draw(sheet)
d.text((34,25),'ODM GEAR SIMULATION',font=font('AGENCYB.TTF',35),fill=(246,242,226));d.text((655,36),'ITCH.IO COVER DIRECTIONS',font=font('arial.ttf',19),fill=(153,179,185))
for i,im in enumerate(photos):
 x=30+(i%2)*655;y=88+(i//2)*559;sheet.paste(im.convert('RGB').resize((630,500),RES),(x,y));d.text((x+2,y+509),manifest['options'][i]['label'],font=font('AGENCYB.TTF',27),fill=(239,231,202))
sheet.save(O/'Choose_A_Cover.png',optimize=True)
small=Image.new('RGB',(690,621),(13,19,25));d=ImageDraw.Draw(small);d.text((24,15),'ACTUAL 315 x 250 CARD SIZE',font=font('arialbd.ttf',20),fill='white')
for i,im in enumerate(photos):
 x=20+(i%2)*335;y=54+(i//2)*283;small.paste(im.convert('RGB').resize((315,250),RES),(x,y));d.text((x,y+254),manifest['options'][i]['label'],font=font('arialbd.ttf',14),fill=(232,223,197))
small.save(O/'Small_Card_Comparison.png',optimize=True)
# Confirm original scenes/settings and all dirty user files are still byte-identical.
state=json.loads((R/'Library/ItchCoverRun.json').read_text())
changed=[p for p,h in state['protected'].items() if not (R/p).is_file() or hashlib.sha256((R/p).read_bytes()).hexdigest()!=h]
assert not changed,changed
manifest['protectedSourcePathsUnchanged']=len(state['protected']);manifest['nativeSubjectRenderer']='Unity 2021.3.25f1 / Intel Iris Xe / closed owned worker; no Play, native physics or source scene save.'
(O/'cover-manifest.json').write_text(json.dumps(manifest,indent=2))
print('Four covers, card previews, editable OpenRaster files and comparison sheets:',O)
print('Protected Assets/ProjectSettings unchanged:',len(state['protected']))
