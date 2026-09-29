# -*- coding: utf-8 -*-
"""
Dựng template Mau_13_BienBanKiemPhieu.docx từ đúng phần "Mẫu số 13" của file biểu mẫu gốc HD03
(docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx): giữ nguyên gói (styles, numbering, theme…), thân tài liệu
chỉ gồm các khối của Mẫu 13, gắn Content Control bao đúng đoạn chữ mặc định (so khớp nguyên văn, sai là dừng).
Cách chạy: py -3 build_mau13.py <file biểu mẫu gốc> <file template đích>
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

# Phạm vi Mẫu 13: từ đoạn "Mẫu số 13" tới đoạn chứa sectPr ngay sau bảng chữ ký.
def text_of(el):
    return ''.join(t.text or '' for t in el.iter(W + 't'))

start = next(i for i, e in enumerate(els) if e.tag == W + 'p' and text_of(e).replace(' ', '') == 'Mẫusố13')
end = next(i for i in range(start, len(els)) if els[i].tag == W + 'p' and els[i].find('.//' + W + 'sectPr') is not None)
part = [copy.deepcopy(e) for e in els[start:end + 1]]
assert text_of(part[0]).replace(' ', '') == 'Mẫusố13'

next_id = [913000]


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


# --- Tiêu đề trái
header_tbl = part[1]
assert header_tbl.tag == W + 'tbl'
tag_paragraph(find_p(lambda t: t == 'ĐẢNG BỘ…', header_tbl.iter(W + 'p')), [('ĐẢNG BỘ…', 'PARTY_PARENT')])
tag_paragraph(find_p(lambda t: t == 'ĐẢNG ỦY (CHI BỘ)…', header_tbl.iter(W + 'p')), [('ĐẢNG ỦY (CHI BỘ)…', 'PARTY_ORG')])

NAME_DEFAULT = 'Hội nghị tập thể lãnh đạo, quản lý… (ghi tên Phòng/Trung tâm, Ban/Đơn vị) hoặc Hội nghị Đảng ủy/Chi ủy cơ sở… (ghi tên đảng ủy/chi ủy cơ sở)'
tag_paragraph(find_p(lambda t: t.startswith(NAME_DEFAULT)), [(NAME_DEFAULT, 'MEETING_NAME'), ('……../…..', 'QUARTER_YEAR')])
tag_paragraph(find_p(lambda t: t.startswith('Căn cứ Quy chế làm việc')), [('… nhiệm kỳ…', 'WORKING_RULES')])
tag_paragraph(find_p(lambda t: t.startswith('Tập thể lãnh đạo, quản lý…')), [
    ('Tập thể lãnh đạo, quản lý… (ghi tên Phòng/Trung tâm, Ban/Đơn vị) hoặc Đảng ủy/Chi ủy cơ sở… (ghi tên đảng ủy/chi ủy cơ sở)', 'ORGANIZER'),
    ('nhận xét, đánh giá, bỏ phiếu đề xuất mức xếp loại (hoặc bỏ phiếu quyết định, phê duyệt mức xếp loại)', 'PURPOSE'),
    ('...', 'PERIOD_TEXT')])
tag_paragraph(find_p(lambda t: t.startswith('1. Thời gian:')), [('…..h..…', 'START_TIME'), ('..…/…../..……..', 'START_DATE')])
tag_paragraph(find_p(lambda t: t.startswith('2. Địa điểm:')), [('………………….', 'LOCATION')])
tag_paragraph(find_p(lambda t: t.startswith('- Tổng số triệu tập:')), [('…..', 'INVITED')])
tag_paragraph(find_p(lambda t: t.startswith('- Số có mặt:')), [('…..', 'PRESENT')])
tag_paragraph(find_p(lambda t: t.startswith('- Số vắng mặt:')), [('…..', 'ABSENT')])

TITLE_DEFAULT = '… (ghi rõ chức vụ Đảng, chính quyền)'

# Mục 3.2: dòng (1) là mẫu lặp, dòng "…" hiện khi không có người.
p_att = find_p(lambda t: t.startswith('(1) Đồng chí') and t.endswith('chính quyền).'))
i_att = para_index(p_att)
tag_paragraph(p_att, [('1', 'A_STT'), ('…', 'A_NAME'), (TITLE_DEFAULT, 'A_TITLE')])
p_att_none = part[i_att + 1]
assert text_of(p_att_none) == '…'
part[i_att] = sdt('repeat:ATTENDEES', [p_att])
part[i_att + 1] = sdt('ifnot:HAS_ATTENDEES', [p_att_none])

tag_paragraph(find_p(lambda t: t.startswith('4. Chủ trì Hội nghị:')), [('…', 'CHAIR_NAME'), (TITLE_DEFAULT, 'CHAIR_TITLE')])
tag_paragraph(find_p(lambda t: t.startswith('5. Thư ký Hội nghị:')), [('…', 'SECRETARY_NAME'), (TITLE_DEFAULT, 'SECRETARY_TITLE')])

tag_paragraph(find_p(lambda t: t.startswith('Sau khi nghe báo cáo')), [
    ('…', 'PERIOD_TEXT'),
    ('… (ghi tên cơ quan, đơn vị, tổ chức)', 'REPORTING_UNIT'),
    ('…', 'PERIOD_TEXT'),
    ('đề xuất mức xếp loại (hoặc bỏ phiếu kín quyết định, phê duyệt mức xếp loại)', 'VOTE_PURPOSE'),
    ('…', 'PERIOD_TEXT')])

# Tổ kiểm phiếu: dòng (1) làm mẫu lặp; không có thành viên → giữ ba dòng của biểu mẫu.
p_c1 = find_p(lambda t: t.startswith('(1) Đồng chí') and t.endswith('Tổ trưởng.'))
i_c1 = para_index(p_c1)
p_c2 = part[i_c1 + 1]
p_c3 = part[i_c1 + 2]
assert text_of(p_c2).startswith('(2) Đồng chí') and text_of(p_c3) == '…'
repeat_p = copy.deepcopy(p_c1)
tag_paragraph(repeat_p, [('1', 'C_STT'), ('…', 'C_NAME'), (TITLE_DEFAULT, 'C_TITLE'), ('Tổ trưởng', 'C_ROLE')])
part[i_c1] = sdt('repeat:COUNTERS', [repeat_p])
part[i_c1 + 1] = sdt('ifnot:HAS_COUNTERS', [p_c1, p_c2, p_c3])
del part[i_c1 + 2]

tag_paragraph(find_p(lambda t: t.startswith('- Tổng số phiếu phát ra:')), [('…..', 'BALLOTS_ISSUED')])
tag_paragraph(find_p(lambda t: t.startswith('- Tổng số phiếu thu về:')), [('…..', 'BALLOTS_COLLECTED')])
tag_paragraph(find_p(lambda t: t.startswith('+ Số phiếu hợp lệ:')), [('…..', 'BALLOTS_VALID')])
tag_paragraph(find_p(lambda t: t.startswith('+ Số phiếu không hợp lệ:')), [('…..', 'BALLOTS_INVALID')])

# Bảng kết quả: mục I (BTVĐUTCT), mục II (Đảng ủy/Chi ủy cơ sở) — dòng trống của biểu mẫu thành dòng lặp.
tables = [e for e in part if e.tag == W + 'tbl']
result_tbl = next(t for t in tables if 'Số phiếu bầu' in text_of(t))
rows = result_tbl.findall(W + 'tr')
assert text_of(rows[3]).startswith('I') and 'BTVĐUTCT' in text_of(rows[3])
assert text_of(rows[5]).startswith('II') and 'CƠ SỞ' in text_of(rows[5])
cols = ['STT', 'NAME', 'POSITION', 'EXC', 'GOOD', 'SAT', 'UNSAT', 'NONE', 'NOTE']


def row_block(row, prefix, name, cond):
    blank = copy.deepcopy(row)
    cells = row.findall(W + 'tc')
    assert len(cells) == len(cols), len(cells)
    for cell, col in zip(cells, cols):
        p = cell.find(W + 'p')
        rpr = p.find(W + 'pPr/' + W + 'rPr')
        for c in list(p):
            if c.tag != W + 'pPr':
                p.remove(c)
        p.append(blank_run_sdt(f'{prefix}_{col}', rpr))
    parent = row.getparent()
    idx = parent.index(row)
    parent.remove(row)
    parent.insert(idx, sdt(f'ifnot:{cond}', [blank]))
    parent.insert(idx, sdt(f'repeat:{name}', [row]))


row_block(rows[6], 'R', 'BASE_ROWS', 'HAS_BASE_ROWS')
row_block(rows[4], 'R', 'SUPERIOR_ROWS', 'HAS_SUPERIOR_ROWS')

tag_paragraph(find_p(lambda t: t.startswith('Hội nghị kết thúc vào hồi')), [('…h…', 'END_TIME'), ('Đảng ủy/Chi bộ…', 'ARCHIVE_UNIT')])

# Chữ ký: thêm khoảng ký và dòng họ tên (như Mẫu 12).
sign_tbl = tables[-1]
cells = sign_tbl.findall('.//' + W + 'tc')


def add_sign(cell, tag):
    last = cell.findall(W + 'p')[-1]
    ppr = copy.deepcopy(last.find(W + 'pPr'))
    name_rpr = el('rPr')
    name_rpr.append(el('b'))
    name_rpr.append(el('sz', val='28'))
    name_rpr.append(el('szCs', val='28'))
    for _ in range(5):
        blank = el('p')
        blank.append(copy.deepcopy(ppr))
        cell.append(blank)
    p = el('p')
    p.append(copy.deepcopy(ppr))
    p.find(W + 'pPr').remove(p.find(W + 'pPr/' + W + 'rPr'))
    p.find(W + 'pPr').append(copy.deepcopy(name_rpr))
    r = el('r')
    r.append(copy.deepcopy(name_rpr))
    t = etree.SubElement(r, W + 't')
    t.set(XML_SPACE, 'preserve')
    t.text = ' '
    p.append(sdt(tag, [r]))
    cell.append(p)


assert 'TỔ KIỂM PHIẾU' in text_of(cells[0]) and 'CHỦ TRÌ' in text_of(cells[2])
add_sign(cells[0], 'COUNTER_HEAD_SIGN')
add_sign(cells[2], 'CHAIR_SIGN')

# sectPr cuối: chuyển từ đoạn cuối lên thân tài liệu.
last_p = part[-1]
sect = last_p.find('.//' + W + 'sectPr')
sect.getparent().remove(sect)
if not text_of(last_p).strip():
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
