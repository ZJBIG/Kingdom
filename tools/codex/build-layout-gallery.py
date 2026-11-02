#!/usr/bin/env python3
"""Build an offline screenshot index: python build-layout-gallery.py --root PATH."""

import argparse
import json
from pathlib import Path


HTML = r'''<!doctype html>
<html lang="zh-CN">
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Kingdom 布局截图图集</title>
<style>
:root{color-scheme:dark;font:15px/1.5 system-ui,sans-serif;background:#14191b;color:#e8eceb}
*{box-sizing:border-box}body{margin:0}header{padding:22px 28px;background:#20292c;position:sticky;top:0;z-index:1;border-bottom:1px solid #425155}
h1{font-size:23px;margin:0 0 12px}input,select,button{font:inherit;color:inherit;background:#293638;border:1px solid #627779;border-radius:6px;padding:7px 10px}button{cursor:pointer}button:disabled{opacity:.4;cursor:default}
input{width:min(460px,100%)}.controls{display:flex;gap:12px;align-items:center;flex-wrap:wrap}.muted{color:#b5c2c4}.status{margin-top:9px}
main{padding:24px;display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,420px),1fr));gap:20px;align-items:start}
.card{background:#20292c;border:1px solid #425155;border-radius:10px;padding:17px;min-width:0}.card h2{font-size:18px;margin:0 0 10px}.badge{color:#bce0bb;font-size:13px}.meta{font-size:13px;overflow-wrap:anywhere;white-space:pre-wrap}
.shots{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,200px),1fr));gap:12px;margin-top:15px}figure{margin:0}figure button{display:block;padding:0;width:100%;overflow:hidden;background:#101416}figure img{width:100%;height:160px;object-fit:contain;display:block}figcaption{font-size:12px;overflow-wrap:anywhere;padding-top:5px}a{color:#b8dfe7}details{margin-top:12px}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:13px/1.55 system-ui,sans-serif}footer{padding:15px 24px 30px;display:flex;gap:15px;justify-content:center;align-items:center}.empty{padding:25px}
dialog{padding:0;border:1px solid #627779;border-radius:10px;background:#14191b;color:inherit;width:96vw;max-width:none;height:94vh;max-height:none}dialog::backdrop{background:#000c}.modal-shell{display:flex;flex-direction:column;height:100%}.modal-tools{padding:10px 15px;display:flex;gap:10px;align-items:center;flex-wrap:wrap;border-bottom:1px solid #425155}.modal-title{flex:1;min-width:160px}.image-area{overflow:auto;flex:1;min-height:0;display:grid;place-items:center;padding:10px}.image-area img{max-width:100%;max-height:100%;object-fit:contain}.image-area.full{display:block}.image-area.full img{max-width:none;max-height:none}.modal-caption{padding:8px 15px;overflow-wrap:anywhere;font-size:13px}
</style>
<header><h1>Kingdom 布局截图图集</h1><div class="controls">
<label>分类 <select id="category"><option value="">全部分类</option></select></label>
<label>检索 <input id="search" type="search" placeholder="编号、名称、ID、按钮路径或详情文字"></label>
<button id="clear" type="button">清除筛选</button></div><div id="status" class="status muted" aria-live="polite"></div>
</header>
<main id="cards"></main>
<footer><button id="page-prev">上一页</button><span id="page-state"></span><button id="page-next">下一页</button></footer>
<dialog id="viewer" aria-label="截图预览"><div class="modal-shell">
<div class="modal-tools"><button id="image-prev">上一张</button><button id="image-next">下一张</button><span id="modal-title" class="modal-title"></span><button id="size-toggle">全尺寸</button><a id="original" target="_blank" rel="noopener">打开原图</a><button id="close">关闭</button></div>
<div id="image-area" class="image-area"><img id="full-image" alt=""></div><div id="modal-caption" class="modal-caption"></div>
</div></dialog>
<script id="manifest" type="application/json">__MANIFEST__</script>
<script>
'use strict';
const items=JSON.parse(document.getElementById('manifest').textContent).items;
const $=id=>document.getElementById(id);
const text=value=>value==null?'':String(value);
const imageUrl=path=>text(path).replaceAll('\\','/').split('/').map(encodeURIComponent).join('/');
const categories=[...new Set(items.map(item=>text(item.category)))].sort((a,b)=>a.localeCompare(b,'zh-CN'));
for(const category of categories){const option=document.createElement('option');option.value=category;option.textContent=category||'未分类';$('category').append(option);}
const sorted=items.map((item,index)=>({...item,_index:index})).sort((a,b)=>text(a.number).localeCompare(text(b.number),'zh-CN',{numeric:true}));
let filtered=[],sequence=[],page=0,current=0,fullSize=false;
const pageSize=40;
function element(tag,className,value){const node=document.createElement(tag);if(className)node.className=className;if(value!==undefined)node.textContent=value;return node;}
function filter(){
 const category=$('category').value,query=$('search').value.trim().toLocaleLowerCase();
 filtered=sorted.filter(item=>(!category||text(item.category)===category)&&(!query||[item.number,item.category,item.label,item.id,item.kind,item.page,item.source,item.buttonPath,item.detailText,...item.files].map(text).join('\n').toLocaleLowerCase().includes(query)));
 sequence=[];for(const item of filtered)for(let fileIndex=0;fileIndex<item.files.length;fileIndex++)sequence.push({item,fileIndex});
 page=0;render();
}
function render(){
 const pages=Math.max(1,Math.ceil(filtered.length/pageSize));page=Math.max(0,Math.min(page,pages-1));$('cards').replaceChildren();
 $('status').textContent=`${filtered.length} / ${items.length} 项 · ${sequence.length} 张截图 · 点击预览，双击预览图或使用链接打开原图`;
 for(const item of filtered.slice(page*pageSize,(page+1)*pageSize)){
  const card=element('article','card');card.append(element('div','badge',text(item.category)+' · '+text(item.kind)),element('h2','',`#${text(item.number)} ${text(item.label)}`));
  card.append(element('div','meta muted',[['ID',item.id],['页面',item.page],['来源',item.source],['按钮',item.buttonPath]].filter(pair=>text(pair[1])).map(pair=>pair[0]+'：'+text(pair[1])).join('\n')));
  const shots=element('div','shots');
  item.files.forEach((file,fileIndex)=>{
   const figure=element('figure'),button=element('button');button.type='button';button.setAttribute('aria-label',`预览 #${text(item.number)} 第 ${fileIndex+1} 张`);
   const img=element('img');img.src=imageUrl(file);img.loading='lazy';img.decoding='async';img.alt=`${text(item.label)} · ${fileIndex+1}/${item.files.length}`;button.append(img);
   button.addEventListener('click',()=>openImage(item,fileIndex));
   const caption=element('figcaption','',`截图 ${fileIndex+1}/${item.files.length} · `),link=element('a','',text(file));link.href=imageUrl(file);link.target='_blank';link.rel='noopener';caption.append(link);figure.append(button,caption);shots.append(figure);
  });
  if(!item.files.length)shots.append(element('div','muted','此项未生成截图'));
  card.append(shots);
  if(text(item.detailText)){const details=element('details');details.append(element('summary','','详情文字'),element('pre','',text(item.detailText)));card.append(details);}
  $('cards').append(card);
 }
 if(!filtered.length)$('cards').append(element('div','empty','没有符合条件的项目。'));
 $('page-state').textContent=`第 ${page+1} / ${pages} 页`;$('page-prev').disabled=page===0;$('page-next').disabled=page+1>=pages;
}
function openImage(item,fileIndex){current=sequence.findIndex(shot=>shot.item._index===item._index&&shot.fileIndex===fileIndex);fullSize=false;showImage();if(!$('viewer').open)$('viewer').showModal();}
function showImage(){
 const shot=sequence[current];if(!shot)return;const {item,fileIndex}=shot,file=item.files[fileIndex];
 $('modal-title').textContent=`#${text(item.number)} ${text(item.label)} · 截图 ${fileIndex+1}/${item.files.length}`;
 $('full-image').src=imageUrl(file);$('full-image').alt=text(item.label);$('original').href=imageUrl(file);
 $('modal-caption').textContent=`${text(item.category)} · ${text(item.kind)} · ${text(item.source)} · ${text(file)} · 筛选结果 ${current+1}/${sequence.length}`;
 $('image-prev').disabled=current===0;$('image-next').disabled=current+1>=sequence.length;
 $('image-area').classList.toggle('full',fullSize);$('size-toggle').textContent=fullSize?'适应窗口':'全尺寸';$('image-area').scrollTop=0;$('image-area').scrollLeft=0;
}
function step(delta){if(current+delta>=0&&current+delta<sequence.length){current+=delta;showImage();}}
$('category').addEventListener('change',filter);$('search').addEventListener('input',filter);
$('clear').onclick=()=>{$('category').value='';$('search').value='';filter();};
$('page-prev').onclick=()=>{page--;render();window.scrollTo(0,0);};$('page-next').onclick=()=>{page++;render();window.scrollTo(0,0);};
$('image-prev').onclick=()=>step(-1);$('image-next').onclick=()=>step(1);$('close').onclick=()=>$('viewer').close();
$('size-toggle').onclick=()=>{fullSize=!fullSize;showImage();};
$('full-image').ondblclick=()=>window.open($('original').href,'_blank','noopener');
$('viewer').addEventListener('click',event=>{if(event.target===$('viewer'))$('viewer').close();});
$('viewer').addEventListener('keydown',event=>{if(event.key==='ArrowLeft'){event.preventDefault();step(-1);}if(event.key==='ArrowRight'){event.preventDefault();step(1);}});
filter();
</script>
</html>
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True, type=Path,
                        help='Directory containing manifest.json and relative screenshot paths')
    args = parser.parse_args()
    root = args.root.resolve()
    manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8-sig'))
    if not isinstance(manifest, dict) or not isinstance(manifest.get('items'), list):
        parser.error('manifest.json must contain an items array')
    for index, item in enumerate(manifest['items']):
        if not isinstance(item, dict) or not isinstance(item.get('files'), list):
            parser.error(f'items[{index}] must be an object with a files array')
        for file in item['files']:
            if not isinstance(file, str):
                parser.error(f'items[{index}].files must contain relative path strings')
            relative = Path(file.replace('\\', '/'))
            if relative.is_absolute() or ':' in file or '..' in relative.parts or file.startswith(('\\', '/')):
                parser.error(f'items[{index}] has a non-relative screenshot path: {file}')
    # Prevent manifest strings from closing the script element; UI uses textContent.
    payload = json.dumps(manifest, ensure_ascii=False).replace('&', '\\u0026').replace('<', '\\u003c').replace('>', '\\u003e')
    output = root / 'index.html'
    output.write_text(HTML.replace('__MANIFEST__', payload), encoding='utf-8')
    print(f'Created {output} ({len(manifest["items"])} items)')


if __name__ == '__main__':
    main()
