from pathlib import Path
import json, re, html, shutil

ROOT = Path('D:/GitHub/Kingdom/output/fb89034f-45d8-4194-856b-175b429cc8d2')
PLUGIN = Path('C:/Users/19603/.workbuddy-ai/plugins/cache/workbuddy-builtin/tencent-docx/5.5.2-wb.37849279.g910352f0.h1a8f7c37fe76')
source = (ROOT / 'stage1/final_draft.md').read_text(encoding='utf-8')
assert source.count('\n## ') == 7 and '<table' not in source
bundle = json.loads((PLUGIN / 'skills/design-token/tokens/compiled/general.json').read_text(encoding='utf-8'))
bundle['css_variables']['--layout-contentWidth'] = '16cm'
bundle['css_variables']['--typography-fontFamily-body'] = 'Microsoft YaHei, 微软雅黑, sans-serif'
bundle['css_variables']['--typography-fontFamily-heading'] = 'Microsoft YaHei, 微软雅黑, sans-serif'
bundle['css_variables']['--typography-fontWeight-heading'] = '700'
bundle['css_variables'].update({'--cover-top':'3.0cm','--cover-gap':'1.0cm','--table-font':'9pt','--table-line':'1.45','--border-width':'0.5pt','--zero':'0','--full-width':'100%','--cell-pad':'5pt','--section-before':'16pt','--section-after':'8pt'})
(ROOT / 'stage2/design_tokens.json').write_text(json.dumps(bundle, ensure_ascii=False, indent=2), encoding='utf-8')

def inline(s):
    s = html.escape(s)
    return re.sub(r'\*\*(.*?)\*\*', r'<strong>\1</strong>', s)

lines = source.splitlines()
blocks, sections = [], []
i = 0
while i < len(lines):
    s = lines[i].strip()
    if not s or s.startswith('# '):
        i += 1
        continue
    if s.startswith('## '):
        label = s[3:]
        aid = 'section-' + str(len(sections) + 1)
        sections.append((aid, label))
        blocks.append(f'<h2 id="{aid}">{inline(label)}</h2>')
    elif s.startswith('### '):
        blocks.append(f'<h3>{inline(s[4:])}</h3>')
    elif s.startswith('|'):
        rows = []
        while i < len(lines) and lines[i].strip().startswith('|'):
            row = [x.strip() for x in lines[i].strip().strip('|').split('|')]
            if not all(re.fullmatch(r':?-+:?', x) for x in row):
                rows.append(row)
            i += 1
        table = '<table><thead><tr>' + ''.join('<th>' + inline(x) + '</th>' for x in rows[0]) + '</tr></thead><tbody>'
        for row in rows[1:]:
            table += '<tr>' + ''.join('<td>' + inline(x) + '</td>' for x in row) + '</tr>'
        blocks.append(table + '</tbody></table>')
        continue
    elif re.match(r'^\d+\. ', s):
        blocks.append('<p>' + inline(s) + '</p>')
    else:
        blocks.append('<p>' + inline(s) + '</p>')
    i += 1
vars_css = '\n'.join(f'  {k}: {v};' for k,v in bundle['css_variables'].items())
css = ''':root {
TOKENS
  --page-content-width: var(--layout-contentWidth);
  --ff-body: var(--typography-fontFamily-body);
  --ff-heading: var(--typography-fontFamily-heading);
  --fs-body: var(--typography-fontSize-body);
  --fs-h1: var(--typography-fontSize-title);
  --fs-h2: var(--typography-fontSize-h2);
  --fs-h3: var(--typography-fontSize-h3);
  --fs-small: var(--typography-fontSize-small);
  --lh-body: var(--typography-lineHeight-body);
  --fw-bold: var(--typography-fontWeight-heading);
}
body { max-width: var(--page-content-width); margin: auto; background: var(--color-background); color: var(--color-text); font-family: var(--ff-body); font-size: var(--fs-body); }
p, li { font-family: var(--ff-body); font-size: var(--fs-body); color: var(--color-text); line-height: var(--lh-body); margin-top: var(--zero); margin-bottom: var(--spacing-paragraph); text-align: justify; }
h1, h2, h3 { font-family: var(--ff-heading); color: var(--color-heading); font-weight: var(--fw-bold); line-height: var(--typography-lineHeight-heading); margin-top: var(--section-before); margin-bottom: var(--section-after); text-align: left; break-after: avoid; }
h1 { font-size: var(--fs-h1); }
h2 { font-size: var(--fs-h2); color: var(--color-primary); }
h3 { font-size: var(--fs-h3); }
table { width: var(--full-width); border-collapse: collapse; margin-top: var(--spacing-paragraph); margin-bottom: var(--section-after); table-layout: fixed; }
th, td { border: var(--border-width) solid var(--color-border); padding: var(--cell-pad); font-family: var(--ff-body); font-size: var(--table-font); color: var(--color-text); line-height: var(--table-line); text-align: left; vertical-align: top; overflow-wrap: anywhere; }
th { background: var(--color-divider); font-weight: var(--fw-bold); }
tr { break-inside: avoid; }
a { color: var(--color-primary); text-decoration: none; }
.cover-label { font-family: var(--ff-heading); font-size: var(--fs-h3); font-weight: var(--fw-bold); color: var(--color-primary); text-align: center; margin-top: var(--cover-top); margin-bottom: var(--cover-gap); }
.cover-title { font-size: var(--fs-h1); font-family: var(--ff-heading); color: var(--color-heading); text-align: center; margin-bottom: var(--cover-gap); }
.cover-subtitle, .cover-date, .cover-note { font-family: var(--ff-body); font-size: var(--fs-body); color: var(--color-textSecondary); text-align: center; line-height: var(--lh-body); margin-bottom: var(--cover-gap); }
.cover-note { font-size: var(--fs-small); }
.toc-title { font-weight: var(--fw-bold); color: var(--color-heading); font-size: var(--fs-h3); text-align: left; }
.toc-list, .toc-list ol, .toc-list ul { list-style: none; list-style-type: none; padding-left: var(--zero); }
.toc-list li { text-align: left; font-size: var(--fs-small); }
.doc-toc { margin-bottom: var(--section-before); }
@page { @bottom-center { content: counter(page); } }
@page cover { @bottom-center { content: none; } }
section[role="cover"] { page: cover; }
'''.replace('TOKENS', vars_css)
toc = '<nav class="doc-toc" aria-label="文档目录"><p class="toc-title">目录</p><ol class="toc-list">' + ''.join(f'<li><a href="#{aid}">{inline(label)}</a></li>' for aid,label in sections) + '</ol></nav>'
cover = '<section role="cover"><p class="cover-label">KINGDOM · AUDIO REVIEW</p><h1 class="cover-title">Kingdom 音效审查<br>与改进建议</h1><p class="cover-subtitle">让声音回应发展成果，而不只是按钮点击</p><p class="cover-date">2026年9月15日</p><p class="cover-note">只读审查 · 优先级建议 · 实施边界<br>未修改游戏代码或资产 · 未进行真实 Unity 编译与听感验收</p></section>'
output = '<!DOCTYPE html>\n<html lang="zh-CN"><head><meta charset="UTF-8"><meta name="docx-page-size" content="A4"><title>Kingdom 音效审查与改进建议</title><style>' + css + '</style></head><body>' + cover + '<section role="body" data-page-restart="1">' + toc + '\n'.join(blocks) + '</section></body></html>'
(ROOT / 'stage2/formatted-audio-review.html').write_text(output, encoding='utf-8')
print(json.dumps({'source_characters':len(source),'sections':len(sections),'tables':output.count('<table>'),'html':str(ROOT / 'stage2/formatted-audio-review.html')},ensure_ascii=False))
