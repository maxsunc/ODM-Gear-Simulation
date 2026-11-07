"""Recolor ONLY original hair UV islands in new face-only atlas copies. Original files read-only."""
from pathlib import Path
import hashlib,json,statistics
from PIL import Image,ImageDraw,ImageFilter
root=Path(__file__).resolve().parents[1];state=json.loads((root/'Library/TitanFaceVariantRun.json').read_text());worker=Path(state['worker']);assert (worker/'.titan-face-owned').read_text()==str(root)
folder=worker/'FaceVariants';source=root/'Assets/Art/Titans/Prototype/Textures/TitanDetailed_BaseColor.png';reference=root/'ArtSource/Titan/Reference/Textures/TitanReference_BaseColor.png'
assert source.read_bytes()==reference.read_bytes(),'Atlas changed; re-audit source UV layout'
image=Image.open(source).convert('RGBA');w,h=image.size;mask=Image.new('L',(w,h));draw=ImageDraw.Draw(mask)
for p in json.loads((folder/'hair-uv-mask.json').read_text())['polygons']:draw.polygon([(x*(w-1),(1-y)*(h-1)) for x,y in p],fill=255)
mask=mask.filter(ImageFilter.MaxFilter(5));m=mask.load();original=image.load();active=[(x,y) for y in range(h) for x in range(w) if m[x,y]]
median=statistics.median(sum(original[x,y][:3])/3 for x,y in active)
dest=folder/'Textures';dest.mkdir(exist_ok=True)
for identity,color in [('BaldBeard',(199,205,190)),('BlondeSmile',(190,148,35))]:
 pixels=image.copy();target=pixels.load()
 for x,y in active:
  variation=max(.76,min(1.12,.88+(sum(original[x,y][:3])/3-median)/200))
  target[x,y]=tuple(int(c*variation) for c in color)+(original[x,y][3],)
 pixels.save(dest/(identity+'_BaseColor.png'))
print('FACE_ATLASES:2 original skin/eye/teeth pixels retained outside original hair UV islands; original atlas unchanged',hashlib.sha256(source.read_bytes()).hexdigest())
