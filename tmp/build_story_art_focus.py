"""Finish the local focus-book reader without rerunning the 18-chapter audit.

Uses the existing review script's small Markdown rendering approach, not its
module-level asset scan or writes. Only the focus reader is written.
"""
from pathlib import Path
from html.parser import HTMLParser
from fractions import Fraction
import base64
import hashlib
import html
import json
import re

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "outputs"
SOURCE = OUT / "Kingdom_三张定调插画深化制作书_2026-09-15.md"
TARGET = SOURCE.with_suffix(".html")
STORY_IDS = ["PrologueAshes_00", "WorkshopMemory_09", "FrontierSectors_14"]


def inline(text):
    text = html.escape(text, quote=False)
    text = re.sub(r"`([^`]+)`", r"<code>\1</code>", text)
    return re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)


def heading_id(title):
    match = re.match(r"(\d\d)｜", title)
    return "section-" + (match[1] if match else hashlib.sha256(title.encode()).hexdigest()[:12])


def render_markdown(source):
    lines = source.splitlines()
    result, paragraph = [], []
    current_list = None
    i = 0

    def flush():
        if paragraph:
            result.append("<p>" + "<br>".join(inline(p) for p in paragraph) + "</p>")
            paragraph.clear()

    def close_list():
        nonlocal current_list
        if current_list:
            result.append("</" + current_list + ">")
            current_list = None

    while i < len(lines):
        line = lines[i]
        if not line.strip():
            flush()
            close_list()
            i += 1
            continue
        if line.startswith("|") and i + 1 < len(lines) and re.fullmatch(r"\|[\s:|\-]+\|", lines[i + 1]):
            flush()
            close_list()
            cells = [cell.strip() for cell in line.strip("|").split("|")]
            result.append('<div class="table-wrap" role="region" aria-label="可横向滚动的数据表" tabindex="0"><table><thead><tr>' + "".join("<th scope=\"col\">" + inline(c) + "</th>" for c in cells) + "</tr></thead><tbody>")
            i += 2
            while i < len(lines) and lines[i].startswith("|"):
                row = [cell.strip() for cell in lines[i].strip("|").split("|")]
                assert len(row) == len(cells), (i + 1, "table column mismatch")
                result.append("<tr>" + "".join("<td>" + inline(c) + "</td>" for c in row) + "</tr>")
                i += 1
            result.append("</tbody></table></div>")
            continue
        head = re.match(r"^(#{1,6}) (.+)$", line)
        if head:
            flush()
            close_list()
            level = min(len(head[1]) + 1, 6)
            result.append(f'<h{level} id="{heading_id(head[2])}">{inline(head[2])}</h{level}>')
        elif line.startswith("> "):
            flush()
            close_list()
            result.append('<aside class="note">' + inline(line[2:]) + "</aside>")
        elif line == "---":
            flush()
            close_list()
            result.append("<hr>")
        else:
            item = re.match(r"^(- |\d+\. )(.+)$", line)
            if item:
                flush()
                kind = "ul" if item[1] == "- " else "ol"
                if current_list != kind:
                    close_list()
                    result.append("<" + kind + ">")
                    current_list = kind
                result.append("<li>" + inline(item[2]) + "</li>")
            else:
                close_list()
                paragraph.append(line)
        i += 1
    flush()
    close_list()
    return "\n".join(result)


class AuditHTML(HTMLParser):
    VOID = {"meta", "br", "hr", "input", "img", "link", "source", "wbr"}

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.ids, self.anchors, self.stack, self.words = [], [], [], []
        self.article = False

    def handle_starttag(self, tag, attrs):
        props = dict(attrs)
        if "id" in props:
            self.ids.append(props["id"])
        if props.get("href", "").startswith("#"):
            self.anchors.append(props["href"][1:])
        if tag not in self.VOID:
            self.stack.append(tag)
        if tag == "article":
            self.article = True

    def handle_endtag(self, tag):
        assert self.stack and self.stack[-1] == tag, (tag, self.stack[-4:])
        self.stack.pop()
        if tag == "article":
            self.article = False

    def handle_data(self, data):
        if self.article:
            self.words.append(data)


def source_plain_text(source):
    result = []
    for line in source.splitlines():
        if re.fullmatch(r"\|[\s:|\-]+\|", line) or line == "---":
            continue
        line = re.sub(r"^(?:#{1,6} |\> |- |\d+\. )", "", line)
        if line.startswith("|"):
            line = "".join(cell.strip() for cell in line.strip("|").split("|"))
        result.append(line.replace("**", "").replace("`", ""))
    return re.sub(r"\s+", "", "".join(result))


def validate_content(text):
    for story_id in STORY_IDS:
        asset = ROOT / "Assets/Resources/Datas/Story" / (story_id + ".asset")
        assert re.search(r"^  id: " + re.escape(story_id) + r"$", asset.read_text(encoding="utf-8-sig"), re.M)
        assert story_id in text
    for label in ["中文正式提示词（独立可用）：", "English final prompt (standalone):", "专属负面提示："]:
        assert text.count(label) == 3, label
    decisions = re.findall(r"^\| (D-\d\d) \|(.+)$", text, re.M)
    assert len(decisions) == 7
    assert all("待确认" in row for _, row in decisions)
    boxes = re.findall(r"^\| ([^|]+?) \| \((\d+),(\d+)\) \| \((\d+),(\d+)\) \|", text, re.M)
    assert len(boxes) == 23
    environment = {"匿名废墟", "桌面主体", "地面近景"}
    required = []
    for label, *numbers in boxes:
        x1, y1, x2, y2 = map(int, numbers)
        assert 0 <= x1 < x2 <= 2560 and 0 <= y1 < y2 <= 1440
        if label not in environment:
            assert 256 <= x1 < x2 <= 2304 and 288 <= y1 < y2 <= 1152, label
            required.append(label)
    assert len(required) == 20
    assert Fraction(2540 - 20, 1245 - 195) == Fraction(12, 5)
    assert 20 <= 256 < 2304 <= 2540 and 195 <= 288 < 1152 <= 1245
    return {"story_ids_checked": STORY_IDS, "standalone_prompts": 6, "pending_designs": 7,
            "planned_boxes": len(boxes), "protected_narrative_boxes": len(required),
            "crop_ratio": "2.4:1", "real_illustrations_checked": 0}


CSS = """
:root{color-scheme:light;--paper:#f5f3ed;--panel:#fffefa;--ink:#27362f;--muted:#5d6d63;--line:#d7dcd2;--accent:#7b4d2c;--soft:#e9eee5}
*{box-sizing:border-box}html{scroll-padding-top:24px}body{margin:0;background:var(--paper);color:var(--ink);font:16px/1.85 "Microsoft YaHei","PingFang SC",system-ui,sans-serif}
a{color:var(--accent);text-underline-offset:4px}a:focus-visible,button:focus-visible,summary:focus-visible,.table-wrap:focus-visible{outline:3px solid #8d5e37;outline-offset:4px}
.skip{position:absolute;left:16px;top:-100px;background:var(--panel);padding:12px;z-index:10}.skip:focus{top:12px}
header{max-width:1330px;margin:auto;padding:64px 48px 34px;border-bottom:1px solid var(--line)}.eyebrow{font-size:12px;letter-spacing:.16em;color:var(--accent);font-weight:700}
h1{font-size:clamp(30px,4vw,50px);letter-spacing:-.025em;line-height:1.28;margin:18px 0}header>p{max-width:820px;color:var(--muted);margin:18px 0}
.badges{display:flex;gap:8px;flex-wrap:wrap}.badge{font-size:12px;background:var(--soft);border:1px solid var(--line);border-radius:4px;padding:4px 10px}
.actions{display:flex;flex-wrap:wrap;align-items:center;gap:12px 20px;margin-top:22px}button{font:inherit;font-size:14px;border:1px solid var(--accent);border-radius:5px;padding:7px 15px;color:var(--accent);background:var(--panel);cursor:pointer}
.cards{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;margin-top:28px}.card{background:var(--panel);border:1px solid var(--line);border-top:3px solid var(--accent);border-radius:6px;padding:20px}
.card small{font-size:12px;color:var(--accent);font-weight:700}.card h2{font-size:20px;line-height:1.5;margin:10px 0}.card p{font-size:14px;color:var(--muted);margin:10px 0}.card a{font-size:14px}
.layout{max-width:1330px;margin:30px auto;display:grid;grid-template-columns:228px minmax(0,1fr);gap:30px;padding:0 40px}.contents{position:sticky;top:20px;align-self:start;max-height:92vh;overflow:auto;padding:15px 8px}
.contents summary{font-size:14px;font-weight:700;cursor:pointer}.contents nav{margin-top:14px}.contents a{display:block;text-decoration:none;color:var(--muted);font-size:13px;padding:8px 5px;border-bottom:1px solid var(--line)}.contents a:hover{background:var(--soft);color:var(--accent)}
main{min-width:0;background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:28px 38px}.intro{padding:16px 20px;border-left:4px solid var(--accent);background:var(--soft);font-size:14px}.intro p{margin:5px 0}
article h2{font-size:25px;line-height:1.55;padding-bottom:12px;border-bottom:2px solid var(--accent);margin:50px 0 22px;overflow-wrap:anywhere}article h3{font-size:20px;line-height:1.6;margin-top:32px}article h4{font-size:18px}
p{margin:16px 0}li{margin:7px 0}ul,ol{padding-left:24px}.note{background:#f1f3ec;border-left:3px solid #c0c8ba;padding:10px 15px;margin:8px 0;font-size:14px;color:var(--muted)}
code{font:0.86em/1.7 Consolas,monospace;background:#eef0e9;padding:2px 4px;border-radius:3px;overflow-wrap:anywhere}strong{font-weight:700}article p,article li{overflow-wrap:anywhere}
.table-wrap{overflow-x:auto;max-width:100%;margin:20px 0;border:1px solid var(--line);border-radius:5px}table{width:100%;border-collapse:collapse;min-width:540px;font-size:14px;line-height:1.75}th,td{padding:11px 13px;text-align:left;vertical-align:top;border-bottom:1px solid var(--line);overflow-wrap:anywhere}th{background:var(--soft);font-weight:700}tr:last-child td{border-bottom:0}td:first-child{font-weight:500;min-width:100px}tbody tr:nth-child(even){background:#f8f8f2}
footer{max-width:1200px;padding:18px 32px 45px;margin:auto;font-size:12px;color:var(--muted)}.return{display:block;margin-top:35px}hr{border:0;border-top:1px solid var(--line);margin:30px 0}
@media(max-width:1000px){header{padding:38px 24px 28px}.layout{padding:0 24px;grid-template-columns:1fr;gap:16px}.contents{position:static;max-height:none;border:1px solid var(--line);background:var(--panel);border-radius:6px;padding:12px 18px}.contents nav{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:0 20px}main{padding:22px 26px}}
@media(max-width:600px){body{font-size:15px}header{padding:28px 18px 22px}.cards{grid-template-columns:1fr;gap:12px}.card{padding:16px}.card h2{font-size:19px}.layout{padding:0 12px;margin-top:18px}.contents nav{grid-template-columns:1fr}.contents a{padding:10px 0}main{padding:18px 16px}article h2{font-size:23px}article h3{font-size:18px}.intro{padding:12px}th,td{padding:10px}table{font-size:13px}.actions{font-size:14px}}
@page{size:A4;margin:15mm 13mm}
@media print{body{background:white;color:#17251d;font-size:10pt;line-height:1.65}.skip,.actions,.contents,.return{display:none}header{padding:0 0 18px}h1{font-size:27pt}.layout{display:block;margin:20px 0;padding:0}main{border:0;padding:0}.cards{gap:10px}.card{padding:12px;break-inside:avoid}.card a{display:none}.table-wrap{overflow:visible;border:0}table{min-width:0;table-layout:fixed;font-size:8.5pt;line-height:1.6}td:first-child{min-width:0}th,td{padding:6px}thead{display:table-header-group}tr{break-inside:avoid}h2,h3,h4{break-after:avoid}.note{break-inside:avoid}footer{padding:14px 0}a{color:inherit;text-decoration:none}article h2{font-size:19pt}article h3{font-size:14pt}}
"""


def main():
    protected = [p for p in OUT.glob("*插画*.md")] + [OUT / "Kingdom_十八章剧情插画策划_2026-09-15.html"]
    protected += [ROOT / "Assets/Resources/Datas/Story" / (story_id + ".asset") for story_id in STORY_IDS]
    before = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
    text = SOURCE.read_text(encoding="utf-8")
    checks = validate_content(text)
    article = render_markdown(text)
    titles = re.findall(r"^# (\d\d｜.+)$", text, re.M)
    assert len(titles) == 10
    nav = "".join(f'<a href="#{heading_id(t)}">{html.escape(t)}</a>' for t in titles)
    download = base64.b64encode(text.encode()).decode("ascii")
    page = f'''<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="description" content="Kingdom三张剧情定调插画文字制作书：角色、动作、道具、裁切和验收。设计待确认，不是完成插画。">
<title>Kingdom · 三张定调插画深化制作书</title><style>{CSS}</style></head>
<body id="top" data-sp-mode="scroll"><a class="skip" href="#source-book">跳至制作书正文</a>
<header><div class="eyebrow">KINGDOM / STORY ART / 2026.09.15</div>
<h1>三张定调插画<br>一条文明的连续表达</h1>
<p>护住余火，记录经验，维护远方。把十八章总策划中的三张基准图细化为可交给画师的文字制作说明；先定角色与动作，再进入成图制作。</p>
<div class="badges"><span class="badge">00 / 09 / 14 三章深化</span><span class="badge">6段中英文正式提示词</span><span class="badge">7项设计待确认</span><span class="badge">文字制作书 · 无新插画</span></div>
<div class="actions"><button type="button" onclick="window.print()">打印 / 保存阅读版</button><a href="#section-09">先看需要确认什么</a><a href="data:text/markdown;charset=utf-8;base64,{download}" download="{SOURCE.name}">下载完整文字稿</a></div>
<div class="cards">
<section class="card"><small>00 / 脆弱的希望</small><h2>还没有熄灭</h2><p>两名鼠族护住石缝里的余火。先看双手与微光，再看匿名废墟；不解释灾变原因。</p><a href="#section-05">阅读序章制作说明</a></section>
<section class="card"><small>09 / 推荐优先试制</small><h2>成功，可以交给别人</h2><p>阿栎测量铜环，同伴记录，桌边保留失败件。用测量与记录锁定造型及材质。</p><a href="#section-06">阅读工坊制作说明</a></section>
<section class="card"><small>14 / 谨慎的可靠</small><h2>住下来，比抵达更难</h2><p>两名维修者共同更换信标模块。密封装备、暖窗与备用件，证明基地已经驻留。</p><a href="#section-07">阅读月面制作说明</a></section>
</div></header>
<div class="layout"><details class="contents" open><summary>阅读目录 · 10个部分</summary><nav aria-label="制作书章节目录">{nav}<a href="#top">返回摘要</a></nav></details>
<main><section class="intro" aria-label="阅读边界"><p><strong>本次补齐的是阅读版，不是三张完成插画。</strong></p><p>以下正文完整保留制作书。4.25头身、四指化、阿栎配色、太空服与裁切均为提案，没有替你批准。制作建议顺序为 <strong>09 → 00 → 14</strong>。</p><p>坐标校核仅针对纸面计划：20个叙事框在保护区内，3个环境框允许超出；这不等于成图已通过裁切或结构验收。</p></section>
<article id="source-book">{article}</article><a class="return" href="#top">返回开头</a></main></div>
<footer>本地阅读版，无外部网络依赖。未上传或发布项目素材，未生成新插画，未修改游戏代码或资产。未执行真实 Unity 编译。</footer></body></html>'''
    parser = AuditHTML()
    parser.feed(page)
    parser.close()
    assert not parser.stack
    assert len(parser.ids) == len(set(parser.ids)), "Duplicate HTML IDs"
    assert set(parser.anchors) <= set(parser.ids), "Broken navigation anchors"
    assert re.sub(r"\s+", "", "".join(parser.words)) == source_plain_text(text), "Markdown/HTML text mismatch"
    assert base64.b64decode(download).decode() == text
    assert not re.search(r"(?:src|href)=[\"'](?:https?:)?//", page)
    assert "\ufffd" not in page
    TARGET.write_text(page, encoding="utf-8")
    assert all(hashlib.sha256(p.read_bytes()).hexdigest() == checksum for p, checksum in before.items())
    checks.update({"source_sha256": before[SOURCE], "navigation_sections": len(titles),
                   "html_ids": len(parser.ids), "local_anchor_references": len(parser.anchors),
                   "markdown_html_text_parity": "passed", "tag_nesting": "passed",
                   "embedded_source_download": "passed", "input_files_unchanged": len(before),
                   "html_bytes": TARGET.stat().st_size, "output": str(TARGET)})
    print(json.dumps(checks, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
