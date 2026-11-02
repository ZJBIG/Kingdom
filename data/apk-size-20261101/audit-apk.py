from pathlib import Path
import zipfile,json,hashlib,io
from PIL import Image, ImageChops, ImageStat
root=Path(__file__).resolve().parents[2]
out=root/'data/apk-size-20261101'
old=root/'.codex/archive/apk-size-20261101/2-Kingdom.apk'
new=root/'Builds/Android/Kingdom.apk'
def inspect(path):
 with zipfile.ZipFile(path) as z:
  assert z.testzip() is None
  names=z.namelist()
  libs=[n for n in names if n.startswith('lib/')]
  assert libs and all(n.startswith('lib/arm64-v8a/') for n in libs)
  music=[e for e in z.infolist() if e.filename.startswith('assets/aa/Android/') and e.filename.endswith('.bundle')]
  main=z.getinfo('assets/bin/Data/data.unity3d')
  return {'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'music_bundle_count':len(music),'music_bundle_bytes':sum(e.compress_size for e in music),'main_data_bytes':main.compress_size,'native_libraries':libs,'zip_entries':len(names)}
before=inspect(old);after=inspect(new)
assert after['sha256']!=before['sha256']
assert after['music_bundle_count']==before['music_bundle_count']
assert after['music_bundle_bytes']==before['music_bundle_bytes']
assert after['bytes']<before['bytes']
source=Image.open(root/'Assets/Resources/Texture/UI/KingdomAppIcon.png').convert('RGB')
candidates=[]
with zipfile.ZipFile(new) as z:
 for n in z.namelist():
  if not n.startswith('res/') or not n.endswith('.png'):continue
  try: im=Image.open(io.BytesIO(z.read(n))).convert('RGB')
  except Exception:continue
  if im.width!=im.height or im.width<48:continue
  delta=sum(ImageStat.Stat(ImageChops.difference(im,source.resize(im.size,Image.Resampling.LANCZOS))).mean)/3
  candidates.append((delta,n,im.width))
 assert candidates
 match=min(candidates)
 assert match[0]<15,match
 Image.open(io.BytesIO(z.read(match[1]))).save(out/'packaged-icon.png')
result={'before':before,'after':after,'saved_bytes':before['bytes']-after['bytes'],'saved_percent':100*(before['bytes']-after['bytes'])/before['bytes'],'packaged_icon':{'entry':match[1],'size':match[2],'mean_rgb_difference':match[0]}}
(out/'apk-audit.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
