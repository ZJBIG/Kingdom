from pathlib import Path
import re
import json
import html
import base64
import hashlib
from collections import Counter
import struct

ROOT = Path('D:/GitHub/Kingdom')
OUT = ROOT / 'outputs'
REPORT = OUT / 'Kingdom_十八章剧情插画策划_2026-09-15.md'
APPENDIX = OUT / 'Kingdom_剧情插画_原文与条件附录_2026-09-15.md'
PAGE = OUT / 'Kingdom_十八章剧情插画策划_2026-09-15.html'
DATA = ROOT / 'Assets/Resources/Datas'


def scalar(raw):
    raw = raw.strip()
    if raw.startswith('"'):
        return json.loads(raw)
    return raw


def fields(path):
    result = {}
    key = None
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        match = re.match(r'^  ([A-Za-z_][A-Za-z_0-9]*):\s*(.*)$', line)
        if match:
            key, raw = match.groups()
            decoded_keys = {'Title', 'Summary', 'Body', 'Label', 'id', 'RequiredEra', 'RequiredTutorialStepId'}
            result[key] = scalar(raw) if raw and key in decoded_keys else raw
        elif key and re.match(r'^  - ', line):
            if not isinstance(result.get(key), list):
                result[key] = []
            result[key].append(line[4:].strip())
    return result


guid_map = {}
for meta in DATA.rglob('*.meta'):
    match = re.search(r'^guid: ([a-f0-9]+)$', meta.read_text(encoding='utf-8-sig'), re.M)
    if match:
        guid_map[match[1]] = Path(str(meta)[:-5])
archive = DATA / 'Story/StoryArchive.asset'
archive_fields = fields(archive)
refs = archive_fields['Chapters']
assert len(refs) == 18
chapters = []
input_paths = {archive}
labels = {
    'RequiredResearch': '全部研究完成',
    'RequiredBuildings': '全部建筑已拥有',
    'RequiredWorkshops': '全部工坊升级已购买',
    'RequiredUnlockedSectors': '全部星区已解锁',
    'RequiredOccupiedSectors': '全部星区已占领',
}
for i, ref in enumerate(refs):
    guid = re.search(r'guid: ([a-f0-9]+)', ref)[1]
    path = guid_map[guid]
    f = fields(path)
    assert f['id'].endswith(f'_{i:02d}')
    assert 200 <= len(f['Body'].strip()) <= 300
    assert '\ufffd' not in f['Body']
    input_paths.add(path)
    input_paths.add(Path(str(path) + '.meta'))
    conditions = {}
    for key in labels:
        values = f.get(key, [])
        resolved = []
        if isinstance(values, list):
            for value in values:
                rguid = re.search(r'guid: ([a-f0-9]+)', value)[1]
                rp = guid_map[rguid]
                rf = fields(rp)
                resolved.append({'id': rf.get('id', rp.stem), 'label': rf.get('Label', rp.stem), 'path': rp.relative_to(ROOT).as_posix(), 'guid': rguid})
                input_paths.add(rp)
                input_paths.add(Path(str(rp) + '.meta'))
        conditions[key] = resolved
    f.update({'path': path.relative_to(ROOT).as_posix(), 'guid': guid, 'conditions': conditions})
    chapters.append(f)

text = REPORT.read_text(encoding='utf-8')
for i, chapter in enumerate(chapters):
    assert chapter['id'] in text
    assert re.search(r'^## ' + f'{i:02d}' + r'｜', text, re.M)
assert len(re.findall(r'^\*\*中文场景提示词：\*\*', text, re.M)) == 18
assert len(re.findall(r'^\*\*English scene prompt:', text, re.M)) == 18
assert len(re.findall(r'^\*\*专属负面：', text, re.M)) == 18
assert len(re.findall(r'^\*\*验收重点：', text, re.M)) == 18

art_files = [
    ('Resource/Mineral/StoneChunk.png', '石块：清晰体块与硬面'),
    ('Resource/WoodLog/WoodLog.png', '木材：断面和树皮识别'),
    ('Resource/Basic/Cloth.png', '织物：暖浅折面'),
    ('Resource/Industrial/Engine.png', '发动机：动力结构识别'),
    ('Resource/Industrial/IndustrialCeramic.png', '工业陶瓷：块状烧结件'),
    ('Resource/Industrial/Chemical.png', '化学物料：容器语言'),
    ('Resource/Spacer/DualPhaseTitanium_c.png', '后期钛材：仅作形体参考'),
    ('Resource/Spacer/RocketFuel.png', '火箭燃料：补给容器语言'),
    ('Resource/Spacer/Microchips.png', '芯片：后期电子器件'),
    ('Resource/CastleSavage.png', '城堡标识：不等于建筑设定'),
    ('Pawn/Descent/BDragon_south.png', '龙类纹理：不纳入鼠族主线'),
    ('Pawn/Golem/GolemSteel.png', '傀儡纹理：不作鼠族比例参考'),
    ('UI/KingdomAppIcon.png', '应用标志：不推导新正史'),
    ('ResearchTree/ResearchTreeBackground.png', '研究树背景：非叙事风景'),
]
all_images = [p for p in (ROOT / 'Assets').rglob('*') if p.suffix.lower() in {'.png','.jpg','.jpeg','.psd','.tga','.svg'}]
resource_images = list((ROOT / 'Assets/Resources/Texture/Resource').rglob('*.png'))
assert len(all_images) == 132
assert len(resource_images) == 67
sizes = Counter(struct.unpack('>II',p.read_bytes()[16:24]) for p in resource_images)
assert sizes[(256,256)] == 65

gallery = []
for rel, caption in art_files:
    path = ROOT / 'Assets/Resources/Texture' / rel
    blob = path.read_bytes()
    width, height = struct.unpack('>II', blob[16:24])
    data = base64.b64encode(blob).decode('ascii')
    gallery.append(f'<figure><div class="sample"><img loading="lazy" src="data:image/png;base64,{data}" alt="{html.escape(caption)}"></div><figcaption><strong>{html.escape(caption)}</strong><small>{width} × {height} · 已有素材原样嵌入</small><code>{html.escape(rel)}</code></figcaption></figure>')
    input_paths.add(path)

append = [
    '# Kingdom｜剧情插画原文与条件附录',
    '',
    '> 2026-09-15 · 当前本地资产的只读摘录；以StoryArchive实际引用顺序解析。',
    '> 本附录保存事实，不把插画创作建议写回剧情。全部条件均为AND，且需先完成前章；RequiredEra是最低时代。这里只做静态引用核验，没有运行Unity测试。',
    '',
    '## 章节与解锁原始数据',
    '',
]
era_names = {0:'Animal',1:'StoneAge',2:'Medieval',3:'Industrial',4:'Spacer',5:'Ultra',6:'Archotech'}
for i, ch in enumerate(chapters):
    era = int(ch['RequiredEra'])
    append.extend([
        f"## {i:02d}｜{ch['Title']}", '',
        f"- 稳定ID：`{ch['id']}`", f"- 原资产：`{ch['path']}`",
        f"- 章节GUID：`{ch['guid']}`", f"- 最低时代：`{era}` / {era_names.get(era, str(era))}",
        f"- 正文长度：{len(ch['Body'].strip())}字符（含换行）", '',
        '**原摘要：** ' + ch['Summary'], '', '**原正文：**', '', ch['Body'], '',
        '**全部条件：**', '',
    ])
    append.append('- 教程步骤：' + (f"`{ch['RequiredTutorialStepId']}`" if ch.get('RequiredTutorialStepId') else '无'))
    for key, label in labels.items():
        items = ch['conditions'][key]
        append.append(f'- {label}：' + ('无' if not items else '；'.join(f"{v['label']}（`{v['id']}`）" for v in items)))
    append.extend(['', '**条件引用路径：**', ''])
    condition_items = [v for items in ch['conditions'].values() for v in items]
    if not condition_items:
        append.append('本章无研究/建筑/工坊/星区资产引用条件。')
    else:
        for v in condition_items:
            append.append(f"- `{v['path']}` · GUID `{v['guid']}`")
    append.append('')
append.extend(['## 静态校核摘要', '',
               '- Archive中的18项全部解析到章节资产，ID末尾00–17与读取顺序一致。',
               '- 18章正文字符数均在200–300要求内；未发现正文替换字符U+FFFD。',
               '- 本稿18个章节均含中英文场景提示词、专属避错项和验收重点。',
               '- 素材统计：Assets范围132张PNG；Resource子树67张，65张为256×256。',
               '- 14张已查看代表图原样嵌入配套HTML。原图未修改，未生成新插画。',
               '- 以上只是静态事实，未执行真实 Unity 编译，未进行运行时或设备验收。', '',
               '## 输入文件内容指纹', '',
               '用于将本次审查与后续工作树区分；不是素材授权证明或运行测试。SHA-256如下。', ''])
for path in sorted(input_paths, key=lambda p: str(p)):
    append.append(f"- `{path.relative_to(ROOT).as_posix()}` · `{hashlib.sha256(path.read_bytes()).hexdigest()}`")
append_text = '\n'.join(append) + '\n'
APPENDIX.write_text(append_text, encoding='utf-8')


def inline(s):
    s = html.escape(s, quote=False)
    s = re.sub(r'`([^`]+)`', r'<code>\1</code>', s)
    s = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', s)
    return s


def slug(s, prefix):
    m = re.match(r'(\d\d)｜', s)
    if m:
        return prefix + 'chapter-' + m[1]
    return prefix + hashlib.sha1(s.encode()).hexdigest()[:10]


def markdown(source, prefix=''):
    lines = source.splitlines()
    result = []
    paragraph = []
    list_open = None
    i = 0
    def flush():
        if paragraph:
            result.append('<p>' + '<br>'.join(inline(x) for x in paragraph) + '</p>')
            paragraph.clear()
    def close_list():
        nonlocal list_open
        if list_open:
            result.append('</' + list_open + '>')
            list_open = None
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            flush(); close_list(); i += 1; continue
        if line.startswith('|') and i + 1 < len(lines) and re.match(r'^\|[\s:|\-]+\|$', lines[i+1]):
            flush(); close_list()
            cells = [x.strip() for x in line.strip('|').split('|')]
            result.append('<div class="table-wrap"><table><thead><tr>' + ''.join('<th>'+inline(c)+'</th>' for c in cells) + '</tr></thead><tbody>')
            i += 2
            while i < len(lines) and lines[i].startswith('|'):
                cells = [x.strip() for x in lines[i].strip('|').split('|')]
                result.append('<tr>' + ''.join('<td>'+inline(c)+'</td>' for c in cells) + '</tr>')
                i += 1
            result.append('</tbody></table></div>')
            continue
        head = re.match(r'^(#{1,6}) (.+)$', line)
        if head:
            flush(); close_list()
            level = min(len(head[1])+1, 6)
            title = head[2]
            css = ' class="chapter-heading"' if re.match(r'^\d\d｜', title) else ''
            result.append(f'<h{level} id="{slug(title,prefix)}"{css}>'+inline(title)+f'</h{level}>')
            i += 1; continue
        if line == '---':
            flush(); close_list(); result.append('<hr>'); i += 1; continue
        if line.startswith('> '):
            flush(); close_list(); result.append('<aside class="note">'+inline(line[2:])+'</aside>'); i += 1; continue
        li = re.match(r'^(- |\d+\. )(.+)$', line)
        if li:
            flush()
            kind = 'ul' if li[1] == '- ' else 'ol'
            if list_open != kind:
                close_list(); result.append('<'+kind+'>'); list_open = kind
            result.append('<li>'+inline(li[2])+'</li>'); i += 1; continue
        close_list(); paragraph.append(line); i += 1
    flush(); close_list()
    return '\n'.join(result)

nav = ''.join(f'<a href="#chapter-{i:02d}"><span>{i:02d}</span>{html.escape(ch["Title"].split("：")[-1])}</a>' for i,ch in enumerate(chapters))
report_html = markdown(text)
appendix_html = markdown(append_text.split('## 输入文件内容指纹')[0], 'evidence-')
page = '''<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Kingdom · 十八章剧情插画策划</title>
<style>
:root{--paper:#f6f4ef;--card:#fffefa;--ink:#263330;--muted:#63716a;--line:#d9ddd3;--copper:#865327;--sage:#e6ece2}*{box-sizing:border-box}html{scroll-behavior:smooth;scroll-padding-top:35px}body{margin:0;background:var(--paper);color:var(--ink);font-family:"Microsoft YaHei","PingFang SC",system-ui,sans-serif;font-size:16px;line-height:1.9}a{color:var(--copper)}header{max-width:1220px;margin:auto;padding:70px 48px 38px;border-bottom:1px solid var(--line)}.eyebrow{letter-spacing:.2em;font-size:12px;color:var(--copper);font-weight:700}header h1{font-size:clamp(32px,4vw,54px);line-height:1.22;letter-spacing:-.025em;margin:20px 0}header p{max-width:800px;color:var(--muted)}.badge{display:inline-block;background:var(--sage);padding:4px 12px;margin:4px 7px 4px 0;border-radius:4px;font-size:13px}.layout{display:grid;grid-template-columns:230px minmax(0,1fr);gap:42px;max-width:1220px;margin:34px auto;padding:0 40px}.sidebar{position:sticky;top:22px;align-self:start;max-height:93vh;overflow:auto;padding-right:14px}.sidebar strong{font-size:13px;letter-spacing:.08em}.sidebar a{display:block;text-decoration:none;color:var(--muted);font-size:12px;padding:5px 4px;border-bottom:1px solid #e9ebe3}.sidebar a:hover{color:var(--copper);background:#eceee6}.sidebar span{display:inline-block;width:27px;color:var(--copper);font-variant-numeric:tabular-nums}main{min-width:0;background:var(--card);border:1px solid var(--line);padding:35px 40px;border-radius:8px}main h2{font-size:27px;line-height:1.5;margin:42px 0 22px;border-bottom:2px solid var(--copper);padding-bottom:13px}main h3{font-size:22px;line-height:1.6;margin-top:36px}main h4{font-size:18px}.chapter-heading{padding:21px 20px;background:var(--sage);border-left:4px solid var(--copper);border-radius:0 5px 5px 0;margin-top:68px!important}p{margin:16px 0}li{margin:7px 0}ul,ol{padding-left:23px}code{font-family:Consolas,monospace;font-size:.84em;background:#eff1e9;border-radius:3px;padding:2px 4px;overflow-wrap:anywhere}strong{font-weight:650}.note{font-size:14px;padding:10px 16px;margin:6px 0;background:#f1f3ec;border-left:3px solid #a3af97}.table-wrap{overflow-x:auto;margin:24px 0}table{border-collapse:collapse;width:100%;font-size:13px;line-height:1.7}th,td{border:1px solid var(--line);padding:10px 12px;text-align:left;vertical-align:top}th{background:#e9eee3;white-space:nowrap}tr:nth-child(even){background:#f8f9f4}hr{border:0;border-top:1px solid var(--line);margin:45px 0}.gallery{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:15px}figure{margin:0;border:1px solid var(--line);border-radius:6px;overflow:hidden}.sample{height:170px;background:repeating-conic-gradient(#e7e9e2 0% 25%,#f6f7f1 0% 50%) 50%/20px 20px;display:flex;align-items:center;justify-content:center}.sample img{max-width:94%;max-height:150px;object-fit:contain}figcaption{padding:12px;line-height:1.6;font-size:12px}figcaption strong{display:block}figcaption small{display:block;color:var(--muted);margin:5px 0}figcaption code{font-size:10px}details{margin:28px 0;border:1px solid var(--line);padding:20px;border-radius:6px}summary{cursor:pointer;font-weight:700;color:var(--copper)}.tools{display:flex;gap:12px;flex-wrap:wrap;margin:22px 0}button,.tools a{border:1px solid var(--line);background:#fffefa;color:var(--ink);font:inherit;font-size:13px;padding:8px 13px;border-radius:4px;text-decoration:none;cursor:pointer}.toplink{display:inline-block;margin-top:25px;font-size:13px}.endnote{font-size:12px;color:var(--muted);max-width:1220px;margin:20px auto 45px;padding:0 40px}.appendix .chapter-heading{margin-top:25px!important}#gallery-section{margin-bottom:45px}@media(max-width:980px){.layout{grid-template-columns:1fr;padding:0 20px}.sidebar{position:static;max-height:230px;display:grid;grid-template-columns:repeat(3,1fr);gap:4px}.sidebar strong{grid-column:1/-1}main{padding:25px}header{padding:40px 25px}.gallery{grid-template-columns:repeat(2,1fr)}}@media(max-width:560px){body{font-size:15px}.sidebar{grid-template-columns:repeat(2,1fr)}.gallery{grid-template-columns:1fr}main{padding:18px}.layout{padding:0 10px}.sample{height:160px}}@media print{body{background:white;font-size:10pt}header{padding:0 0 20px}.layout{display:block;padding:0;margin:0}.sidebar,.tools,.toplink{display:none}main{border:0;padding:0}h2,h3,h4{break-after:avoid}.chapter-heading{break-before:page}p,li{orphans:3;widows:3}figure,tr{break-inside:avoid}.gallery{grid-template-columns:repeat(3,1fr)}.sample{height:100px}.sample img{max-height:90px}details:not([open]){display:none}}
</style></head><body id="top">
<header><div class="eyebrow">KINGDOM / STORY ART DIRECTION / 2026.09.15</div><h1>从一簇余火<br>到远方仍亮着的灯</h1><p>十八章剧情插画策划与素材审查。以鼠族的劳动、记录、修复与协作为核心，把现有资源美术延伸为统一的文明群像。</p><div><span class="badge">18章 · 含序章与终章</span><span class="badge">14张现有素材实看</span><span class="badge">36段中英文场景提示词</span><span class="badge">文字预演 · 未生成插画</span></div><div class="tools"><button onclick="window.print()">打印 / 保存阅读版</button><a href="#gallery-section">看已有素材</a><a href="#chapter-00">进入逐章方案</a><a href="#evidence">查看原文证据</a></div></header>
<div class="layout"><nav class="sidebar" aria-label="章节导航"><strong>CHAPTER INDEX / 章节</strong>''' + nav + '''<a href="#gallery-section">现有素材样本</a><a href="#evidence">原文与全部解锁条件</a><a href="#top">回到开头</a></nav><main>
<section id="gallery-section"><h2>审查样本：不是新生成的插画</h2><p>以下14张是项目已有PNG的原样嵌入，用于说明参考和排除依据。透明区域以棋盘底展示，不代表原图有背景。部分素材只能作为排除样本，不应混入鼠族主线。</p><div class="gallery">''' + '\n'.join(gallery) + '''</div></section>
<article>''' + report_html + '''</article><details id="evidence"><summary>展开事实附录：18章原文、全部条件与引用路径</summary><div class="appendix">''' + appendix_html + '''</div></details><a class="toplink" href="#top">回到开头</a></main></div><footer class="endnote">本地只读审查与美术策划提案。没有上传或发布项目素材，没有生成新插画，没有改动游戏源文件。未执行真实 Unity 编译。</footer></body></html>'''
PAGE.write_text(page, encoding='utf-8')
assert page.count('<figure>') == 14
assert page.count('class="chapter-heading"') == 36
ids = re.findall(r'\bid="([^"]+)"', page)
assert len(ids) == len(set(ids)), 'Duplicate HTML IDs'
anchors = re.findall(r'href="#([^"]+)"', page)
assert all(a in ids for a in anchors), 'Broken local anchor'
assert not re.search(r'<(?:script|link)[^>]+(?:src|href)="https?://', page)
print(json.dumps({
    'chapters': len(chapters), 'body_length_range': [min(len(c['Body'].strip()) for c in chapters), max(len(c['Body'].strip()) for c in chapters)],
    'resolved_condition_references': sum(len(v) for c in chapters for v in c['conditions'].values()),
    'existing_images_embedded': len(art_files), 'source_files_fingerprinted': len(input_paths),
    'main_report_characters': len(text), 'appendix_characters': len(append_text),
    'html_bytes': PAGE.stat().st_size, 'html_anchor_check': 'passed',
    'outputs': [str(PAGE), str(REPORT), str(APPENDIX)]
}, ensure_ascii=False, indent=2))
