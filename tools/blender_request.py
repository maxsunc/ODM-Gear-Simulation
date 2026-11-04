"""Local Blender addon transport, never clears user scenes. Run --file script.py.
Prints Blender's reply; execute_code scripts must explicitly scope owned objects.
"""
import argparse,json,socket
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--file');p.add_argument('--code');p.add_argument('--timeout',type=float,default=180);a=p.parse_args()
code=Path(a.file).read_text(encoding='utf-8') if a.file else a.code
if code:code='SCOUT_PROJECT_ROOT='+repr(str(Path(__file__).resolve().parents[1]))+'\n'+code
command={'type':'execute_code','params':{'code':code}} if code else {'type':'get_scene_info','params':{}}
with socket.create_connection(('127.0.0.1',9876),timeout=5) as s:
 s.settimeout(a.timeout);s.sendall(json.dumps(command).encode());chunks=[]
 while True:
  chunk=s.recv(65536)
  if not chunk:break
  chunks.append(chunk)
  try:
   result=json.loads(b''.join(chunks));break
  except json.JSONDecodeError:pass
 print(json.dumps(result,indent=2))
 if result.get('status')!='success' or result.get('result',{}).get('status')=='error':raise SystemExit(1)
