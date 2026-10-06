# -*- coding: utf-8 -*-
"""
Sửa bố cục template Mẫu 9D (Phụ lục kết quả thực hiện nhiệm vụ) cho giống bản Mẫu 9D chuẩn (bản nộp Quý III/2026):

- "Mẫu 9D" → "Mẫu số 09D" (đậm nghiêng, cỡ 12, góc phải).
- Tiêu đề trang: bỏ các đoạn căn bằng phím Tab (vỡ dòng khi tên dài), thay bằng bảng 2 cột không viền như bản chuẩn — cột trái
  "ĐẢNG BỘ …" (ORG_PARTY_NAME, chữ thường) / "CHI BỘ …" (PARTY_CELL, đậm) và gạch ngắn bên dưới; cột phải "ĐẢNG CỘNG SẢN VIỆT NAM"
  (đậm) và gạch ngắn bên dưới. Gạch ngắn là đường viền trên của một đoạn trống thụt hai bên (không dùng hình vẽ).
- Bỏ dòng địa danh – ngày tháng (ORG_LOCATION, DATE_LINE) và dấu "*" thừa: bản chuẩn Mẫu 9D không có.
- Chức vụ: thay đoạn "Chức vụ (Đảng, Chính quyền, Đoàn thể): POSITIONS" bằng 3 dòng như bản chuẩn — "Chức vụ: Chức vụ Đảng:
  PARTY_POSITION" / "Chức vụ chính quyền: ADMIN_POSITION" / "Chức vụ đoàn thể: MASS_POSITION" (hai dòng sau thẳng hàng với
  "Chức vụ Đảng:"; dòng đầu dài thì xuống dòng cũng thẳng hàng).
- "Đơn vị công tác: DEPARTMENT" → "Đơn vị công tác: DEPARTMENT, ORG_COMPANY_NAME".
- Lề trang và số trang ở đầu trang (header có sẵn trong template) như bản chuẩn.

Chạy lại an toàn (bước nào đã làm thì bỏ qua). Chạy sau build_axis_reports.py.
Cách chạy: py -3 tools/templates/fix_mau9d_layout.py <thư mục Templates/Word>
"""
import sys, os, copy, re, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
W14 = '{http://schemas.microsoft.com/office/word/2010/wordml}'
RNS = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'

FILE_9D = 'Mau_9D_PhuLucKetQuaNhiemVu.docx'

# Bảng tiêu đề như bản chuẩn: lệch trái 567, cột 6946 + 9639 (twip).
HEADER_INDENT = -567
LEFT_COL = 6946
RIGHT_COL = 9639
CELL_MARGIN = 108
# Gạch ngắn dưới tên Chi bộ và dưới "ĐẢNG CỘNG SẢN VIỆT NAM" (độ dài như bản chuẩn).
LEFT_RULE = 2190
RIGHT_RULE = 2745
# "Chức vụ: " cỡ 14 rộng ~1089 twip: dòng chức vụ thứ 2, 3 thẳng hàng với "Chức vụ Đảng:".
POSITION_FIRST_LINE = 567
POSITION_LABEL = 1089


def sdt_tag(e):
    t = e.find(W + 'sdtPr/' + W + 'tag')
    return t.get(W + 'val') if t is not None else None


def find_sdt(root, tag):
    for s in root.iter(W + 'sdt'):
        if sdt_tag(s) == tag:
            return s
    return None


def para_of(e):
    while e is not None and e.tag != W + 'p':
        e = e.getparent()
    return e


def text_of(p):
    return ''.join(t.text or '' for t in p.iter(W + 't'))


class Ids:
    def __init__(self, root):
        ids = [int(i.get(W + 'val')) for i in root.iter(W + 'id') if (i.get(W + 'val') or '').lstrip('-').isdigit()]
        self.next = max(ids + [900000]) + 1

    def take(self):
        self.next += 1
        return str(self.next - 1)


def el(tag, attrs=None, children=()):
    e = etree.Element(W + tag)
    for k, v in (attrs or {}).items():
        e.set(W + k, str(v))
    for c in children:
        e.append(c)
    return e


def rpr(bold=False, italic=False, size=28):
    r = el('rPr')
    r.append(el('rFonts', {'ascii': 'Times New Roman', 'hAnsi': 'Times New Roman', 'cs': 'Times New Roman'}))
    if bold:
        r.append(el('b'))
    if italic:
        r.append(el('i'))
    r.append(el('sz', {'val': size}))
    r.append(el('szCs', {'val': size}))
    return r


def run(text, **fmt):
    r = el('r', children=[rpr(**fmt)])
    t = el('t')
    t.text = text
    t.set(XML_SPACE, 'preserve')
    r.append(t)
    return r


def ppr(jc=None, before=0, after=0, ind=None, border_top=False, line=240, line_rule='auto', size=28):
    p = el('pPr')
    if border_top:
        b = el('pBdr')
        b.append(el('top', {'val': 'single', 'sz': 6, 'space': 1, 'color': '000000'}))
        p.append(b)
    p.append(el('spacing', {'before': before, 'after': after, 'line': line, 'lineRule': line_rule}))
    if ind:
        p.append(el('ind', ind))
    if jc:
        p.append(el('jc', {'val': jc}))
    p.append(el('rPr', children=[el('sz', {'val': size}), el('szCs', {'val': size})]))
    return p


def sdt_field(ids, tag, placeholder, **fmt):
    s = el('sdt')
    pr = el('sdtPr')
    pr.append(el('alias', {'val': tag}))
    pr.append(el('tag', {'val': tag}))
    pr.append(el('id', {'val': ids.take()}))
    s.append(pr)
    s.append(el('sdtContent', children=[run(placeholder, **fmt)]))
    return s


def set_sdt_format(sdt, **fmt):
    """Đặt lại định dạng mọi run trong control (giữ chữ mặc định)."""
    for r in sdt.find(W + 'sdtContent').iter(W + 'r'):
        old = r.find(W + 'rPr')
        if old is not None:
            r.remove(old)
        r.insert(0, rpr(**fmt))


# ---------------------------------------------------------------- các bước

def fix_form_number(root):
    p = next((p for p in root.find(W + 'body').iter(W + 'p') if text_of(p).strip() in ('Mẫu 9D', 'Mẫu số 09D')), None)
    if p is None:
        raise SystemExit('Không thấy đoạn "Mẫu 9D"')
    if text_of(p).strip() == 'Mẫu số 09D' and p.find(W + 'pPr/' + W + 'spacing').get(W + 'after') == '200':
        return False
    for r in list(p.findall(W + 'r')):
        p.remove(r)
    old = p.find(W + 'pPr')
    if old is not None:
        p.remove(old)
    p.insert(0, ppr(jc='right', after=200, line=276, size=24))
    p.append(run('Mẫu số 09D', bold=True, italic=True, size=24))
    return True


def rule_paragraph(width, column):
    side = max(0, (column - 2 * CELL_MARGIN - width) // 2)
    # Giãn trước 3pt: gạch cách dòng chữ phía trên như bản chuẩn.
    return el('p', children=[ppr(jc='center', before=60, ind={'left': side, 'right': side}, border_top=True,
                                 line=120, line_rule='exact', size=8)])


def cell(width, paragraphs):
    tc = el('tc', children=[el('tcPr', children=[el('tcW', {'w': width, 'type': 'dxa'})])])
    for p in paragraphs:
        tc.append(p)
    return tc


def fix_header(root):
    party = find_sdt(root, 'ORG_PARTY_NAME')
    if party is None:
        raise SystemExit('Không thấy control ORG_PARTY_NAME')
    party_p = para_of(party)
    if party_p.getparent().tag == W + 'tc':
        return False
    body = root.find(W + 'body')
    cell_sdt = find_sdt(root, 'PARTY_CELL')
    cell_p = para_of(cell_sdt)
    star_p = cell_p.getnext()
    if star_p is None or star_p.tag != W + 'p' or text_of(star_p).strip() != '*':
        raise SystemExit('Không thấy đoạn "*" sau dòng Chi bộ')

    party.getparent().remove(party)
    cell_sdt.getparent().remove(cell_sdt)
    set_sdt_format(party, size=26)
    set_sdt_format(cell_sdt, bold=True, size=26)

    left = cell(LEFT_COL, [
        el('p', children=[ppr(jc='center', size=26), party]),
        el('p', children=[ppr(jc='center', size=26), cell_sdt]),
        rule_paragraph(LEFT_RULE, LEFT_COL),
    ])
    right = cell(RIGHT_COL, [
        el('p', children=[ppr(jc='center', size=26), run('ĐẢNG CỘNG SẢN VIỆT NAM', bold=True, size=26)]),
        rule_paragraph(RIGHT_RULE, RIGHT_COL),
    ])

    borders = el('tblBorders')
    for side in ('top', 'left', 'bottom', 'right', 'insideH', 'insideV'):
        borders.append(el(side, {'val': 'nil'}))
    tbl = el('tbl', children=[
        el('tblPr', children=[
            el('tblW', {'w': LEFT_COL + RIGHT_COL, 'type': 'dxa'}),
            el('tblInd', {'w': HEADER_INDENT, 'type': 'dxa'}),
            borders,
            el('tblLayout', {'type': 'fixed'}),
            el('tblCellMar', children=[el('left', {'w': CELL_MARGIN, 'type': 'dxa'}), el('right', {'w': CELL_MARGIN, 'type': 'dxa'})]),
            el('tblLook', {'val': '0000', 'firstRow': 0, 'lastRow': 0, 'firstColumn': 0, 'lastColumn': 0, 'noHBand': 0, 'noVBand': 0}),
        ]),
        el('tblGrid', children=[el('gridCol', {'w': LEFT_COL}), el('gridCol', {'w': RIGHT_COL})]),
        el('tr', children=[el('trPr', children=[el('trHeight', {'val': 710})]), left, right]),
    ])
    idx = body.index(party_p)
    for p in (party_p, cell_p, star_p):
        body.remove(p)
    body.insert(idx, tbl)
    # Đoạn trống sau bảng tiêu đề (trước "PHỤ LỤC") như bản chuẩn: không giãn trước.
    nxt = tbl.getnext()
    if nxt is not None and nxt.tag == W + 'p' and text_of(nxt).strip() == '':
        sp = nxt.find(W + 'pPr/' + W + 'spacing')
        if sp is not None and W + 'before' in sp.attrib:
            del sp.attrib[W + 'before']
    return True


def fix_positions(root):
    if find_sdt(root, 'PARTY_POSITION') is not None:
        return False
    old = find_sdt(root, 'POSITIONS')
    if old is None:
        raise SystemExit('Không thấy control POSITIONS')
    p = para_of(old)
    ids = Ids(root)
    body = p.getparent()
    idx = body.index(p)
    label = POSITION_FIRST_LINE + POSITION_LABEL
    first = el('p', children=[
        ppr(before=120, ind={'left': label, 'hanging': POSITION_LABEL}),
        run('Chức vụ: '), run('Chức vụ Đảng: '), sdt_field(ids, 'PARTY_POSITION', '…'),
    ])
    second = el('p', children=[
        ppr(ind={'left': label}),
        run('Chức vụ chính quyền: '), sdt_field(ids, 'ADMIN_POSITION', '…'),
    ])
    third = el('p', children=[
        ppr(ind={'left': label}),
        run('Chức vụ đoàn thể: '), sdt_field(ids, 'MASS_POSITION', '…'),
    ])
    body.remove(p)
    for i, new in enumerate((first, second, third)):
        body.insert(idx + i, new)
    return True


def fix_department(root):
    if find_sdt(root, 'ORG_COMPANY_NAME') is not None:
        return False
    dept = find_sdt(root, 'DEPARTMENT')
    if dept is None:
        raise SystemExit('Không thấy control DEPARTMENT')
    ids = Ids(root)
    parent = dept.getparent()
    idx = parent.index(dept)
    parent.insert(idx + 1, run(', '))
    parent.insert(idx + 2, sdt_field(ids, 'ORG_COMPANY_NAME', '…'))
    return True


def fix_section(root, rels):
    sect = root.find(W + 'body/' + W + 'sectPr')
    changed = False
    mar = sect.find(W + 'pgMar')
    for k, v in (('top', '851'), ('bottom', '851'), ('left', '1021'), ('right', '1021')):
        if mar.get(W + k) != v:
            mar.set(W + k, v)
            changed = True
    if sect.find(W + 'headerReference') is None:
        rid = rels.get('header')
        if rid is None:
            raise SystemExit('Template không có phần header số trang')
        ref = etree.Element(W + 'headerReference')
        ref.set(W + 'type', 'default')
        ref.set('{%s}id' % RNS, rid)
        sect.insert(0, ref)
        changed = True
    return changed


def page_number_header(src):
    """Id quan hệ của header có số trang (trường PAGE)."""
    rels = etree.fromstring(src.read('word/_rels/document.xml.rels'))
    for r in rels:
        if r.get('Type', '').endswith('/header'):
            part = 'word/' + r.get('Target')
            if b'PAGE' in src.read(part):
                return {'header': r.get('Id')}
    return {}


def process(path):
    src = zipfile.ZipFile(path)
    root = etree.fromstring(src.read('word/document.xml'))
    rels = page_number_header(src)
    changed = False
    for step in (fix_form_number, fix_header, fix_positions, fix_department):
        changed = step(root) or changed
    changed = fix_section(root, rels) or changed
    if not changed:
        src.close()
        print('SKIP (da sua)', os.path.basename(path))
        return
    out_xml = etree.tostring(root, xml_declaration=True, encoding='UTF-8', standalone=True)
    tmp = path + '.tmp'
    with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as out:
        for it in src.infolist():
            out.writestr(it, out_xml if it.filename == 'word/document.xml' else src.read(it.filename))
    src.close()
    shutil.move(tmp, path)
    print('OK', os.path.basename(path))


if __name__ == '__main__':
    process(os.path.join(sys.argv[1], FILE_9D))
