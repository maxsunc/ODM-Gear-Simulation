"""Distinct bald/bearded and blonde/smiling identities. Execute through OWNED Blender MCP only."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
worker=Path(bpy.data.filepath).parent
assert (worker/'.titan-face-owned').is_file(),'Owned face workspace only'
root=Path((worker/'.titan-face-owned').read_text());out=worker/'FaceVariants';out.mkdir(exist_ok=True)
source=bpy.data.collections['TitanReference_v3'];rig0=next(o for o in source.objects if o.type=='ARMATURE')
rest={b.name:[list(row) for row in b.matrix_local] for b in rig0.data.bones}
head0=bpy.data.objects['TitanReference_Head'];hair_ids=[i for i,m in enumerate(head0.data.materials) if any(t in m.name for t in ['CharcoalHair','HairStrand'])]
uv=head0.data.uv_layers.active.data
polys={i:[[list(uv[k].uv) for k in p.loop_indices] for p in head0.data.polygons if p.material_index==i] for i in hair_ids}
# Pick a real polygon interior in each original atlas hair island, not a guessed skin UV.
points={}
for i,faces in polys.items():
 face=max(faces,key=lambda p:abs(sum(p[j][0]*p[(j+1)%len(p)][1]-p[(j+1)%len(p)][0]*p[j][1] for j in range(len(p)))))
 points[i]=tuple(sum(v[a] for v in face)/len(face) for a in [0,1])
(out/'hair-uv-mask.json').write_text(json.dumps({'polygons':[p for faces in polys.values() for p in faces],'sampleUVs':points}))
def g(v,w):return math.exp(-(v/w)**2)
def hairmat(src,name,color):
 mat=src.copy();mat.name=name;mat.diffuse_color=(*color,1)
 for node in mat.node_tree.nodes:
  if node.type=='BSDF_PRINCIPLED':node.inputs['Base Color'].default_value=(*color,1);node.inputs['Roughness'].default_value=.8
 return mat
def remove_hair(ob):
 bm=bmesh.new();bm.from_mesh(ob.data);ids=[i for i,m in enumerate(ob.data.materials) if any(t in m.name for t in ['CharcoalHair','HairStrand'])]
 verts=[v for v in bm.verts if v.link_faces and all(f.material_index in ids for f in v.link_faces)]
 bmesh.ops.delete(bm,geom=verts,context='VERTS');bm.to_mesh(ob.data);bm.free()
def tube(name,centers,radii,mat,slot,part,rig,col,depth=.6,sides=12):
 centers=[Vector(c) for c in centers];verts=[];faces=[]
 for j,(p,r) in enumerate(zip(centers,radii)):
  tangent=(centers[min(j+1,len(centers)-1)]-centers[max(j-1,0)]).normalized();width=tangent.cross(Vector((0,-1,0))).normalized()
  if width.length<.1:width=Vector((1,0,0))
  normal=tangent.cross(width).normalized()
  for k in range(sides):
   a=k*2*math.pi/sides;verts.append(tuple(p+width*(math.cos(a)*r)+normal*(math.sin(a)*r*depth)))
 for j in range(len(centers)-1):
  for k in range(sides):a=j*sides+k;b=j*sides+(k+1)%sides;faces.append((a,b,b+sides,a+sides))
 verts.extend([tuple(centers[0]),tuple(centers[-1])])
 for k in range(sides):faces.append((len(verts)-2,(k+1)%sides,k));a=(len(centers)-1)*sides;faces.append((len(verts)-1,a+k,a+(k+1)%sides))
 return mesh_object(name,verts,faces,mat,slot,part,rig,col)
def mesh_object(name,verts,faces,mat,slot,part,rig,col):
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.materials.append(mat);mesh.update()
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 uv=mesh.uv_layers.new(name='UVMap')
 for p in mesh.polygons:
  p.use_smooth=True
  for j,k in enumerate(p.loop_indices):
   a=2*math.pi*j/len(p.loop_indices);uv.data[k].uv=(points[slot][0]+.00003*math.cos(a),points[slot][1]+.00003*math.sin(a))
 ob=bpy.data.objects.new(name,mesh);col.objects.link(ob);ob.parent=rig;vg=ob.vertex_groups.new(name=part);vg.add(list(range(len(mesh.vertices))),1,'REPLACE')
 mod=ob.modifiers.new('TitanSkin','ARMATURE');mod.object=rig
 return ob
report={'sourceRig':rest,'variants':[],'reference':'User titansFromAnime.webp; bald gray beard and golden blonde smiling, NOT proportion-only variants'}
for key,identity in [('Broad','BaldBeard'),('Gaunt','BlondeSmile')]:
 col=bpy.data.collections.new('TitanFace_'+key);bpy.context.scene.collection.children.link(col)
 rig=rig0.copy();rig.data=rig0.data.copy();rig.animation_data_clear();rig.name='TitanRig_'+key;col.objects.link(rig)
 colors=[(.46,.48,.43),(.60,.62,.55)] if key=='Broad' else [(.40,.28,.04),(.53,.40,.09)]
 mats={i:hairmat(head0.data.materials[i],identity+('_Hair' if j==0 else '_Strand'),colors[j]) for j,i in enumerate(hair_ids)}
 meshes=[]
 for part in ['Head','Jaw']:
  src=bpy.data.objects['TitanReference_'+part];ob=src.copy();ob.data=src.data.copy();ob.animation_data_clear();ob.name='Titan'+key+'_'+part;col.objects.link(ob)
  if ob.parent is not None:ob.parent=rig
  for mod in ob.modifiers:
   if mod.type=='ARMATURE':mod.object=rig
  if part=='Head':remove_hair(ob)
  for vertex in ob.data.vertices:
   x,y,z=vertex.co;front=max(0,min(1,(.16-y)/.42))
   if key=='Broad':
    # Exposed rounded crown, mature broad nose, narrower heavy-lidded eyes.
    if part=='Head' and z>10.95:
     t=(z+.38-10.22)/1.60;rx=(.75-.15*g(t,.14)-.32*g(t-1,.13))*1.12;ry=.59-.11*g(t,.14)-.25*g(t-1,.13)
     u=max(0,min(1,(z-10.95)/.49));radius=math.sqrt(max(.0001,1-u*u))
     vertex.co.x=x/rx*.84*radius;vertex.co.y=.015+(y-.015)/ry*.60*radius;vertex.co.z=10.95+.70*u
    vertex.co.y-=front*.12*g(x,.17)*g(z-10.34,.24)
    vertex.co.z-=front*.26*(z-10.665)*g(z-10.665,.23)*g(abs(x)-.353,.24)
   else:
    # Real upturned mouth/lips/teeth and smiling squinted eyes, not a narrower head.
    vertex.co.x=x*(1+front*.12*g(z-9.98,.21))
    vertex.co.z+=front*.18*(abs(x)/.64)**2*g(z-9.99,.23)
    vertex.co.z-=front*.42*(z-10.665)*g(z-10.665,.24)*g(abs(x)-.353,.25)
    vertex.co.y-=front*.035*g(abs(x)-.5,.22)*g(z-10.40,.20)
    if part=='Jaw':vertex.co.z-=front*.04*g(z-9.93,.12)*g(x,.42)
  ob.data.update();extras=[]
  # Retain the old atlas slots for skin/eye/teeth; new hair uses those same UV islands.
  for i,m in enumerate(ob.data.materials):
   for old,mat in mats.items():
    if m==head0.data.materials[old]:ob.data.materials[i]=mat
  if part=='Head':
   for s in [-1,1]:
    brow=[(s*.13,-.665,10.91),(s*.34,-.70,10.95 if key=='Gaunt' else 10.97),(s*.57,-.64,10.91)]
    extras.append(tube('CharacterBrow',brow,[.012,.030 if key=='Broad' else .018,.006],mats[6],6,part,rig,col,.40))
  if key=='Broad':
   if part=='Head':
    for s in [-1,1]:
     extras.append(tube('WhiteSideBeard',[(s*.76,-.24,10.48),(s*.76,-.42,10.22),(s*.65,-.62,9.90),(s*.54,-.72,9.78)],[.09,.13,.14,.06],mats[6],6,part,rig,col,.72))
     extras.append(tube('WhiteMoustache',[(s*.02,-.79,10.15),(s*.23,-.81,10.14),(s*.46,-.77,10.09),(s*.61,-.73,9.99)],[.025,.075,.065,.012],mats[6],6,part,rig,col,.58))
   else:
    extras.append(tube('FullWhiteBeard',[(0,-.60,9.86),(0,-.68,9.70),(0,-.79,9.43),(0,-.90,9.15),(0,-.92,9.08)],[.67,.63,.44,.15,.008],mats[6],6,part,rig,col,.38,24))
    for j in range(19):
     x=(j-9)/9*.61;end=9.15+.66*(abs(x)/.65)**1.1
     extras.append(tube('BeardLock',[(x,-.84,9.80),(x*.89,-.96,9.55+(abs(x)*.2)),(x*.71,-.99,end)],[.048,.055,.003],mats[8 if j%3==0 else 6],8 if j%3==0 else 6,part,rig,col,.48,8))
  elif part=='Head':
   # New short golden cap, not a recolor of the old long side tufts.
   sides=80;rows=18;verts=[];faces=[]
   for inner in [False,True]:
    for j in range(rows):
     t=.02+.98*j/(rows-1)
     for i in range(sides):
      a=2*math.pi*i/sides;front=max(0,-math.sin(a));bottom=10.48+.55*front**5;z=11.66-(11.66-bottom)*t-(.035 if inner else 0)
      radius=min(1,math.sqrt(max(.001,(11.67-z)/.38)));verts.append(((.88-(.035 if inner else 0))*math.cos(a)*radius,.04+(.68-(.035 if inner else 0))*math.sin(a)*radius,z))
    o=sides*rows if inner else 0
    for j in range(rows-1):
     for i in range(sides):f=(o+j*sides+i,o+j*sides+(i+1)%sides,o+(j+1)*sides+(i+1)%sides,o+(j+1)*sides+i);faces.append(tuple(reversed(f)) if inner else f)
   verts.extend([(0,.04,11.66),(0,.04,11.625)]);n=sides*rows
   for i in range(sides):
    faces.extend([(len(verts)-2,i,(i+1)%sides),(len(verts)-1,n+(i+1)%sides,n+i)]);a=(rows-1)*sides+i;b=(rows-1)*sides+(i+1)%sides;faces.append((a,b,n+b,n+a))
   extras.append(mesh_object('GoldenShortCap',verts,faces,mats[6],6,part,rig,col))
   for j in range(13):
    x=(j-6)/6*.78;end=10.98+.12*abs(x)/.8
    extras.append(tube('GoldenFringe',[(-.16+x*.32,-.27,11.59),(x*.78,-.61,11.36),(x,-.68,end+.07),(x*.98,-.65,end)],[.025,.10,.075,.003],mats[8 if j%3==0 else 6],8 if j%3==0 else 6,part,rig,col,.38))
   for s in [-1,1]:
    for j in range(3):extras.append(tube('GoldenSideLock',[(s*.75,.04,11.24),(s*(.86+j*.01),-.06,10.87),(s*(.83+j*.02),-.10,10.43+j*.07)],[.09,.09,.003],mats[6],6,part,rig,col,.4))
  bpy.ops.object.select_all(action='DESELECT');ob.select_set(True)
  for extra in extras:extra.select_set(True)
  bpy.context.view_layer.objects.active=ob
  if extras:bpy.ops.object.join()
  # Identical single original bone; no new beard/hair physics or joints.
  ob.vertex_groups.clear();vg=ob.vertex_groups.new(name=part);vg.add(list(range(len(ob.data.vertices))),1,'REPLACE')
  bm=bmesh.new();bm.from_mesh(ob.data);boundary=sum(e.is_boundary for e in bm.edges);nonmanifold=sum(not e.is_manifold for e in bm.edges);bm.free()
  assert boundary==0 and nonmanifold==0,(identity,part,boundary,nonmanifold)
  meshes.append(ob)
 assert all([list(row) for row in b.matrix_local]==rest[b.name] for b in rig.data.bones)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 for ob in meshes:ob.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(out/('TitanFace_'+key+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},global_scale=1,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',use_armature_deform_only=False,path_mode='STRIP')
 report['variants'].append({'key':key,'identity':identity,'bones':22,'meshes':[{'name':ob.name,'vertices':len(ob.data.vertices),'closed':True,'groups':list(ob.vertex_groups.keys())} for ob in meshes]})
for image in bpy.data.images:
 if image.source=='FILE' and image.filepath:
  texture=root/'ArtSource/Titan/Reference/Textures'/Path(bpy.path.abspath(image.filepath)).name
  if texture.is_file():image.filepath=str(texture);image.reload();image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'TitanFaceVariants.blend'))
(out/'face-manifest.json').write_text(json.dumps(report,indent=2)+'\n');print('TITAN_FACE_IDENTITIES_OK',json.dumps(report['variants']))
