"""Start a marker-owned Blender MCP art worker; never switch/save the user's Blender."""
from pathlib import Path
import hashlib,json,shutil,subprocess,tempfile,time,socket
root=Path(__file__).resolve().parents[1]
try:
 with socket.create_connection(('127.0.0.1',9877),timeout=.3):pass
except OSError:pass
else:raise RuntimeError('Port9877 is already in use; reuse/audit the existing owned workspace instead')
worker=Path(tempfile.mkdtemp(prefix='ODM-TitanFaces-'))
(worker/'.titan-face-owned').write_text(str(root))
source=root/'ArtSource/Titan/Reference/TitanReference.blend'
shutil.copy2(source,worker/'TitanFaces.blend')
protected=json.loads((root/'Library/HeldStruggleSourceBaseline.json').read_text())
protected={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in protected if (root/p).is_file()}
(root/'Library/TitanFaceProtectedBaseline.json').write_text(json.dumps(protected,indent=2)+'\n')
bootstrap=worker/'bootstrap.py'
bootstrap.write_text("import bpy,sys\nfrom pathlib import Path\nassert Path(bpy.data.filepath).parent.joinpath('.titan-face-owned').is_file()\nif hasattr(bpy.context.scene,'blendermcp_auto_start_server'):bpy.context.scene.blendermcp_auto_start_server=False\nimport blender_mcp\nserver=blender_mcp.BlenderMCPServer(host='127.0.0.1',port=9877)\nserver.start()\nbpy.app.driver_namespace['ODM_FACE_SERVER']=server\n")
log=(worker/'blender.log').open('wb')
p=subprocess.Popen(['C:/Program Files/Blender Foundation/Blender 4.5/blender.exe','--no-window-focus',str(worker/'TitanFaces.blend'),'--python',str(bootstrap)],stdin=subprocess.DEVNULL,stdout=log,stderr=subprocess.STDOUT)
state={'root':str(root),'worker':str(worker),'pid':p.pid,'port':9877,'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest()}
(root/'Library/TitanFaceVariantRun.json').write_text(json.dumps(state,indent=2)+'\n')
for i in range(90):
 if p.poll() is not None:raise RuntimeError('Owned Blender exited; inspect '+str(worker/'blender.log'))
 try:
  with socket.create_connection(('127.0.0.1',9877),timeout=.2):break
 except OSError:time.sleep(1)
else:raise RuntimeError('Owned MCP startup timed out')
print(json.dumps(state,indent=2))
