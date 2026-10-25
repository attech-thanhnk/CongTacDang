# -*- coding: utf-8 -*-
"""
Dựng template Mau_06_BaoCaoKetQuaTapThe.docx từ đúng phần "Mẫu số 06" của file biểu mẫu gốc HD03
(docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx): giữ nguyên gói (styles, numbering, theme…), thân tài liệu
chỉ gồm các khối của Mẫu 06, gắn Content Control bao đúng đoạn chữ mặc định (so khớp nguyên văn, sai là dừng).
Cách chạy: py -3 build_mau06.py <file biểu mẫu gốc> <file template đích>
"""
import sys, zipfile, copy, re
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
W14 = '{http://schemas.microsoft.com/office/word/2010/wordml}'
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'

src_path, dst_path = sys.argv[1], sys.argv[2]
src = zipfile.ZipFile(src_path)
root = etree.fromstring(src.read('word/document.xml'))
body = root.find(W + 'body')
els = list(body)

# Phạm vi Mẫu 06: từ đoạn "Mẫu số 06" tới đoạn chứa sectPr ngay sau bảng chữ ký.
def text_of(el):
    return ''.join(t.text or '' for t in el.iter(W + 't'))

start = next(i for i, e in enumerate(els) if e.tag == W + 'p' and text_of(e).replace(' ', '') == 'Mẫusố06')
end = next(i for i in range(start, len(els)) if els[i].tag == W + 'p' and els[i].find('.//' + W + 'sectPr') is not None)
part = [copy.deepcopy(e) for e in els[start:end + 1]]
assert text_of(part[0]).replace(' ', '') == 'Mẫusố06'

next_id = [906000]


def new_id():
    next_id[0] += 1
    return str(next_id[0])


def el(tag, **attrs):
    e = etree.Element(W + tag)
    for k, v in attrs.items():
        e.set(W + k, v)
    return e


def sdt(tag, children):
    s = el('sdt')
    pr = etree.SubElement(s, W + 'sdtPr')
    etree.SubElement(pr, W + 'alias').set(W + 'val', tag)
    etree.SubElement(pr, W + 'tag').set(W + 'val', tag)
    etree.SubElement(pr, W + 'id').set(W + 'val', new_id())
    content = etree.SubElement(s, W + 'sdtContent')
    for c in children:
        content.append(c)
    return s


def strip_ids(e):
    for x in e.iter():
        for a in list(x.attrib):
            if a.startswith(W14) or a.startswith(W + 'rsid'):
                del x.attrib[a]
    return e


def tokens(p):
    """Chuỗi ký tự của đoạn: (ký tự, rPr) — tab là '\t'."""
    out = []
    for r in p.findall(W + 'r'):
        rpr = r.find(W + 'rPr')
        for c in r:
            if c.tag == W + 't':
                for ch in (c.text or ''):
                    out.append((ch, rpr))
            elif c.tag == W + 'tab':
                out.append(('\t', rpr))
            elif c.tag == W + 'rPr':
                pass
            else:
                raise SystemExit('Run có phần tử không hỗ trợ: ' + c.tag)
    return out


def make_runs(toks):
    runs = []
    cur = None
    cur_key = None
    for ch, rpr in toks:
        key = etree.tostring(rpr) if rpr is not None else b''
        if cur is None or key != cur_key:
            cur = el('r')
            if rpr is not None:
                cur.append(copy.deepcopy(rpr))
            runs.append(cur)
            cur_key = key
        if ch == '\t':
            cur.append(el('tab'))
        else:
            last = cur[-1] if len(cur) else None
            if last is None or last.tag != W + 't':
                last = etree.SubElement(cur, W + 't')
                last.set(XML_SPACE, 'preserve')
                last.text = ''
            last.text += ch
    return runs


def tag_paragraph(p, specs):
    """specs: danh sách (đoạn chữ mặc định, tag) theo thứ tự xuất hiện; mỗi đoạn tìm từ sau đoạn trước."""
    toks = tokens(p)
    text = ''.join(ch for ch, _ in toks)
    pos = 0
    spans = []
    for needle, tag in specs:
        i = text.find(needle, pos)
        if i < 0:
            raise SystemExit(f'Không tìm thấy "{needle}" trong đoạn "{text}"')
        spans.append((i, i + len(needle), tag))
        pos = i + len(needle)
    ppr = p.find(W + 'pPr')
    for c in list(p):
        if c is not ppr:
            p.remove(c)
    cursor = 0
    for a, b, tag in spans:
        if a > cursor:
            for r in make_runs(toks[cursor:a]):
                p.append(r)
        p.append(sdt(tag, make_runs(toks[a:b])))
        cursor = b
    if cursor < len(toks):
        for r in make_runs(toks[cursor:]):
            p.append(r)
    return p


def find_p(pred, where=None):
    for e in (where if where is not None else part):
        if e.tag == W + 'p' and pred(text_of(e)):
            return e
    raise SystemExit('Không tìm thấy đoạn')


def para_index(e):
    return part.index(e)


def blank_run_sdt(tag, rpr):
    r = el('r')
    if rpr is not None:
        r.append(copy.deepcopy(rpr))
    t = etree.SubElement(r, W + 't')
    t.set(XML_SPACE, 'preserve')
    t.text = ''
    return sdt(tag, [r])



# --- Tiêu đề trái và dòng địa danh – ngày
header_tbl = part[1]
assert header_tbl.tag == W + 'tbl'
hdr_ps = list(header_tbl.iter(W + 'p'))


def set_default(p, text):
    """Chữ mặc định của control đầu tiên trong đoạn (không ghi cứng tên đơn vị)."""
    s = p.find('.//' + W + 'sdtContent')
    ts = list(s.iter(W + 't'))
    ts[0].text = text
    for t in ts[1:]:
        t.text = ''


p_parent = find_p(lambda t: t == 'ĐẢNG BỘ TCT QUẢN LÝ BAY VIỆT NAM', hdr_ps)
tag_paragraph(p_parent, [('ĐẢNG BỘ TCT QUẢN LÝ BAY VIỆT NAM', 'PARTY_PARENT')])
set_default(p_parent, 'ĐẢNG BỘ…')
p_org = find_p(lambda t: t == 'ĐẢNG ỦY (CHI BỘ) ………', hdr_ps)
tag_paragraph(p_org, [('ĐẢNG ỦY (CHI BỘ) ………', 'PARTY_ORG')])
set_default(p_org, 'ĐẢNG ỦY (CHI BỘ)…')
tag_paragraph(find_p(lambda t: t.startswith('……, ngày'), hdr_ps), [('……', 'ORG_LOCATION'), ('ngày …… tháng …… năm 20…', 'DATE_LINE')])


def after(prefix):
    """Chữ mặc định ngay sau tiền tố (phần dấu chấm cuối đoạn)."""
    p = find_p(lambda t: t.startswith(prefix))
    return p, text_of(p)[len(prefix):]


tag_paragraph(find_p(lambda t: t.startswith('Kỳ đánh giá: Quý')), [('……', 'QUARTER'), ('20…', 'YEAR')])
for prefix, tag in [
    ('Tập thể tự đánh giá: Đảng ủy / Chi ủy / Tập thể Lãnh đạo ', 'SUBJECT_NAME'),
    ('Họ và tên người đứng đầu: ', 'HEAD_NAME'),
    ('Chức vụ: ', 'HEAD_TITLE'),
    ('5. Công tác bảo đảm an toàn hoạt động bay, an ninh hàng không: ', 'SAFETY'),
    ('6. Công tác xây dựng Đảng, đoàn kết nội bộ, kỷ luật: ', 'PARTY_BUILDING'),
]:
    p, rest = after(prefix)
    assert set(rest) <= set('…. '), rest
    tag_paragraph(p, [(rest, tag)])

tag_paragraph(find_p(lambda t: t.startswith('1. Tổng số nhiệm vụ')), [('……', 'TASKS_ASSIGNED')])
tag_paragraph(find_p(lambda t: t.startswith('2. Số nhiệm vụ hoàn thành')), [('……', 'TASKS_ONTIME'), ('……', 'ONTIME_PERCENT')])
tag_paragraph(find_p(lambda t: t.startswith('3. Số nhiệm vụ chậm tiến độ')), [('……', 'TASKS_LATE'), ('……', 'LATE_PERCENT')])
tag_paragraph(find_p(lambda t: t.startswith('4. Số nhiệm vụ chưa hoàn thành')), [('……', 'TASKS_OBJECTIVE')])
tag_paragraph(find_p(lambda t: t.startswith('Tổng điểm tự chấm:')), [('……', 'TOTAL_SCORE')])
for label, tag in [('Hoàn thành xuất sắc nhiệm vụ', 'CHECK_EXCELLENT'), ('Hoàn thành tốt nhiệm vụ', 'CHECK_GOOD'),
                   ('Hoàn thành nhiệm vụ', 'CHECK_DONE'), ('Không hoàn thành nhiệm vụ', 'CHECK_FAILED')]:
    tag_paragraph(find_p(lambda t, l=label: t.startswith('☐') and t[1:].strip() == l), [('☐', tag)])

# sectPr cuối: chuyển từ đoạn cuối lên thân tài liệu.
last_p = part[-1]
sect = last_p.find('.//' + W + 'sectPr')
sect.getparent().remove(sect)
while part and part[-1].tag == W + 'p' and not text_of(part[-1]).strip():
    part = part[:-1]

for c in list(body):
    body.remove(c)
for e in part:
    body.append(strip_ids(e))
body.append(sect)

xml = etree.tostring(root, xml_declaration=True, encoding='UTF-8', standalone=True)
with zipfile.ZipFile(dst_path, 'w', zipfile.ZIP_DEFLATED) as out:
    for info in src.infolist():
        data = xml if info.filename == 'word/document.xml' else src.read(info.filename)
        out.writestr(info, data)
print('OK', dst_path)

