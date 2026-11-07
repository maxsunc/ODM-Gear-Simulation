"""Deterministic original placeholder combat foley. No downloaded/copyrighted sounds."""
from pathlib import Path
import math,random,struct,wave
root=Path(__file__).resolve().parents[1]/'Assets/Audio/TitanCombat'
root.mkdir(parents=True,exist_ok=True)
for index,(name,duration,tone) in enumerate([('SwordWhoosh',.21,0),('FleshHit',.16,105),('LimbSever',.27,78),('NapeKill',.36,62)]):
 rng=random.Random(171+index);samples=[];low=0;rate=44100
 for i in range(int(rate*duration)):
  t=i/rate;p=t/duration;n=rng.uniform(-1,1);low=low*.88+n*.12
  if tone:
   env=min(1,t/.005)*math.exp(-p*7);v=(math.sin(2*math.pi*(tone*t-25*t*t))*.5+low*.9+n*.16)*env
  else:
   env=math.sin(math.pi*p)**1.7;v=(n*.18+low*.65)*env
  samples.append(struct.pack('<h',round(max(-.95,min(.95,v))*.75*32767)))
 with wave.open(str(root/(name+'.wav')),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(b''.join(samples))
 print(root/(name+'.wav'))
