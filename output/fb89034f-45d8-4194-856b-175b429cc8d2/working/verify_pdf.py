from pathlib import Path
import json,re,pymupdf as fitz
from PIL import Image,ImageOps,ImageDraw
ROOT=Path('D:/GitHub/Kingdom/output/fb89034f-45d8-4194-856b-175b429cc8d2')
OUT=Path('D:/GitHub/Kingdom/outputs')
NAME='Kingdom-音效审查与改进建议-2026-09-15'
md=(OUT/(NAME+'.md')).read_text(encoding='utf-8')
doc=fitz.open(OUT/(NAME+'.pdf'))
texts=[]
thumbnails=[]
issues=[]
for i,p in enumerate(doc):
    text=p.get_text(clip=fitz.Rect(0,0,p.rect.width,p.rect.height-48))
    texts.append(text)
    for line in p.get_text('dict')['blocks']:
        if line.get('type')!=0: continue
        for l in line['lines']:
            for span in l['spans']:
                x0,y0,x1,y1=span['bbox']
                if x0<0 or x1>p.rect.width+1 or y0<0 or y1>p.rect.height+1:
                    issues.append({'page':i+1,'text':span['text'],'bbox':span['bbox']})
    pix=p.get_pixmap(matrix=fitz.Matrix(.75,.75),alpha=False)
    im=Image.frombytes('RGB',[pix.width,pix.height],pix.samples)
    im.thumbnail((298,421))
    thumbnails.append(im)
alltext=''.join(texts)
normalize=lambda s: re.sub(r'\s+','',s)
assert len(doc)>1 and all(len(t.strip())>25 for t in texts)
assert '\ufffd' not in alltext
assert all(normalize(s[3:]) in normalize(alltext) for s in md.splitlines() if s.startswith('## '))
missing=[]
for line in md.splitlines():
    s=line.strip()
    if not s or s.startswith('# ') or re.fullmatch(r'[|:\- ]+',s): continue
    if s.startswith('|'): items=s.strip('|').split('|')
    else: items=[re.sub(r'^#{2,3} ','',s)]
    for item in items:
        plain=item.strip().replace('**','')
        if normalize(plain) not in normalize(alltext): missing.append(plain[:90])
assert not missing, missing
assert not issues, issues
cols=3; rows=(len(thumbnails)+cols-1)//cols
contact=Image.new('RGB',(cols*318,rows*451),(225,229,235))
draw=ImageDraw.Draw(contact)
for i,im in enumerate(thumbnails):
    x=(i%cols)*318+10; y=(i//cols)*451+20
    contact.paste(im,(x,y)); draw.text((x,y-15),str(i+1),fill=(30,40,50))
contact.save(ROOT/'working/pdf-contact-sheet.png')
summary={'pages':len(doc),'text_lengths':[len(t) for t in texts],'content_match':'pass','out_of_page_text':issues,'replacement_characters':False,'files':{ext:(OUT/(NAME+ext)).stat().st_size for ext in ['.md','.pdf']}}
(ROOT/'working/verification.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False))
