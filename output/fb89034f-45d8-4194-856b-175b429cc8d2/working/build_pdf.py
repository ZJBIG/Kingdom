from pathlib import Path
import re, html, json
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, LongTable, TableStyle, KeepTogether
from reportlab.lib.enums import TA_CENTER, TA_LEFT

ROOT = Path('D:/GitHub/Kingdom/output/fb89034f-45d8-4194-856b-175b429cc8d2')
OUT = Path('D:/GitHub/Kingdom/outputs')
NAME = 'Kingdom-音效审查与改进建议-2026-09-15'
md = (ROOT / 'stage1/final_draft.md').read_text(encoding='utf-8')
md = md.replace('## 三、按优先级排序的音效机会\n', '## 三、按优先级排序的音效机会\n\n按发展反馈、重要变化与日常操作三个层次安排声音，优先选择高辨识、低频率的触发时机。\n')
md = md.replace('## 四、实施前必须解决的触发问题\n', '## 四、实施前必须解决的触发问题\n\n增加素材之前，先保证播放时机正确，避免失败误报、刷新重复和离线事件集中补响。\n')
md = md.replace('## 六、推荐实施顺序与验收清单\n', '## 六、推荐实施顺序与验收清单\n\n先建立最小可用的结果反馈，再以试玩证据决定是否扩展环境和细节声音；以下均为后续实施建议。\n')
md_path = OUT / (NAME + '.md')
pdf_path = OUT / (NAME + '.pdf')
if md_path.exists() or pdf_path.exists():
    raise RuntimeError('Output exists; refusing overwrite')
md_path.write_text(md, encoding='utf-8')
pdfmetrics.registerFont(TTFont('YaHei', 'C:/Windows/Fonts/msyh.ttc', subfontIndex=0))
pdfmetrics.registerFont(TTFont('YaHeiBold', 'C:/Windows/Fonts/msyhbd.ttc', subfontIndex=0))
pdfmetrics.registerFontFamily('YaHei', normal='YaHei', bold='YaHeiBold', italic='YaHei', boldItalic='YaHeiBold')
blue = colors.HexColor('#2453A4')
ink = colors.HexColor('#172339')
muted = colors.HexColor('#5B6574')
styles = {
    'body': ParagraphStyle('body', fontName='YaHei', fontSize=10, leading=16.3, textColor=ink, wordWrap='CJK', spaceAfter=7.5),
    'h2': ParagraphStyle('h2', fontName='YaHeiBold', fontSize=15, leading=22, textColor=blue, spaceBefore=15, spaceAfter=9, keepWithNext=True, wordWrap='CJK'),
    'h3': ParagraphStyle('h3', fontName='YaHeiBold', fontSize=11.5, leading=18, textColor=ink, spaceBefore=10, spaceAfter=6, keepWithNext=True, wordWrap='CJK'),
    'cell': ParagraphStyle('cell', fontName='YaHei', fontSize=8.6, leading=13.5, textColor=ink, wordWrap='CJK', splitLongWords=True),
    'th': ParagraphStyle('th', fontName='YaHeiBold', fontSize=8.8, leading=14, textColor=blue, wordWrap='CJK'),
    'label': ParagraphStyle('label', fontName='YaHeiBold', fontSize=12, leading=20, textColor=blue, alignment=TA_CENTER),
    'title': ParagraphStyle('title', fontName='YaHeiBold', fontSize=27, leading=43, textColor=ink, alignment=TA_CENTER),
    'subtitle': ParagraphStyle('subtitle', fontName='YaHei', fontSize=12, leading=21, textColor=muted, alignment=TA_CENTER),
    'small': ParagraphStyle('small', fontName='YaHei', fontSize=9, leading=16, textColor=muted, alignment=TA_CENTER),
    'toc': ParagraphStyle('toc', fontName='YaHei', fontSize=9.5, leading=17, textColor=blue, spaceAfter=3),
}

def fmt(s):
    return re.sub(r'\*\*(.*?)\*\*', r'<b>\1</b>', html.escape(s))

def para(s, sty='body'):
    return Paragraph(fmt(s), styles[sty])

story = [Spacer(1, 93), para('KINGDOM / AUDIO REVIEW', 'label'), Spacer(1, 31),
         para('Kingdom 音效审查<br/>与改进建议'.replace('<br/>','\n'), 'title')]
# Use explicit line break only for the cover title.
story[-1] = Paragraph('Kingdom 音效审查<br/>与改进建议', styles['title'])
story += [Spacer(1, 29), para('让声音回应发展成果，而不只是按钮点击', 'subtitle'),
          Spacer(1, 29), para('2026年9月15日', 'subtitle'), Spacer(1, 45),
          para('只读审查 · 优先级建议 · 实施边界', 'small'),
          para('未修改游戏代码或资产<br/>未进行真实 Unity 编译与听感验收'.replace('<br/>',' / '), 'small'), PageBreak()]
sections = [s[3:] for s in md.splitlines() if s.startswith('## ')]
story.append(para('目录', 'h2'))
for label in sections:
    story.append(para(label, 'toc'))
story.append(Spacer(1, 7))
lines = md.splitlines()
i = 0
width = A4[0] - 102
while i < len(lines):
    s = lines[i].strip()
    if not s or s.startswith('# '):
        i += 1
        continue
    if s.startswith('## '):
        story.append(para(s[3:], 'h2'))
    elif s.startswith('### '):
        story.append(para(s[4:], 'h3'))
    elif s.startswith('|'):
        rows = []
        while i < len(lines) and lines[i].strip().startswith('|'):
            row = [x.strip() for x in lines[i].strip().strip('|').split('|')]
            if not all(re.fullmatch(r':?-+:?', x) for x in row):
                rows.append(row)
            i += 1
        count = len(rows[0])
        ratios = [0.23, 0.40, 0.37] if count == 3 else [0.32, 0.68]
        data = [[para(c, 'th' if r == 0 else 'cell') for c in row] for r,row in enumerate(rows)]
        table = LongTable(data, colWidths=[width*x for x in ratios], repeatRows=1, hAlign='LEFT', splitByRow=1)
        table.setStyle(TableStyle([
            ('BACKGROUND',(0,0),(-1,0),colors.HexColor('#EAF0FA')),
            ('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#F7F9FC')]),
            ('GRID',(0,0),(-1,-1),0.45,colors.HexColor('#D9E1ED')),
            ('VALIGN',(0,0),(-1,-1),'TOP'),
            ('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),
            ('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7),
        ]))
        story += [table, Spacer(1,9)]
        continue
    else:
        story.append(para(s))
    i += 1

def page(canvas, doc):
    canvas.setTitle('Kingdom 音效审查与改进建议')
    canvas.setAuthor('Kingdom 项目审查')
    if doc.page > 1:
        canvas.setStrokeColor(colors.HexColor('#D9E1ED'))
        canvas.line(51, 43, A4[0]-51, 43)
        canvas.setFont('YaHei', 8)
        canvas.setFillColor(muted)
        canvas.drawString(51, 28, 'Kingdom · 音效审查与改进建议')
        canvas.drawRightString(A4[0]-51, 28, str(doc.page - 1))

doc = SimpleDocTemplate(str(pdf_path), pagesize=A4, rightMargin=51, leftMargin=51, topMargin=48, bottomMargin=59, title='Kingdom 音效审查与改进建议', author='Kingdom 项目审查', allowSplitting=True)
doc.build(story, onFirstPage=page, onLaterPages=page)
print(json.dumps({'markdown':str(md_path),'pdf':str(pdf_path),'characters':len(md)}, ensure_ascii=False))
