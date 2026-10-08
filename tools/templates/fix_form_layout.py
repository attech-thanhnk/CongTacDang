# -*- coding: utf-8 -*-
"""
Thêm các khối theo kiểu trình bày biểu mẫu (danh mục "Kiểu trình bày biểu mẫu") vào template Mẫu 09B và 9D, theo các bản đã nộp
quý III/2026 trong docs/tai-lieu-chuan (Trưởng phòng, Giám đốc, Chủ tịch):

- Mẫu 09B, 9D: chức vụ một dòng "Chức vụ (Đảng, Đoàn thể, Chính quyền): POSITIONS" trong khối if:POSITIONS_ONE_LINE; ba dòng
  chức vụ hiện có bọc trong ifnot:POSITIONS_ONE_LINE.
- Mẫu 09B: phần ký (một đoạn căn bằng dấu cách) đổi thành bảng 2 cột không viền — trái "XÁC NHẬN CỦA CHI BỘ" (trong
  if:HAS_CELL_CONFIRMATION, bỏ được mà không xô lệch cột phải), phải "NGƯỜI TỰ ĐÁNH GIÁ".
- Mẫu 9D: dòng "ORG_LOCATION, DATE_LINE" dưới "ĐẢNG CỘNG SẢN VIỆT NAM" (cột phải bảng tiêu đề) trong khối if:HAS_DATE_LINE.
- Giữ phần ký trên cùng một trang: 09B — dòng bảng chữ ký không tách trang; 9D — dòng "XÁC NHẬN CỦA CHI BỘ … CÁ NHÂN TỰ
  ĐÁNH GIÁ" đi liền dòng "(Ký, ghi rõ họ tên)".

Chạy lại an toàn (bước nào đã làm thì bỏ qua). Chạy sau fix_department_company.py.
Cách chạy: py -3 tools/templates/fix_form_layout.py <thư mục Templates/Word>
"""
import sys, os, copy, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'

FILE_09B = 'Mau_09B_PhieuTuChamTheoTruc.docx'
FILE_9D = 'Mau_9D_PhuLucKetQuaNhiemVu.docx'
ONE_LINE_LABEL = 'Chức vụ (Đảng, Đoàn thể, Chính quyền): '


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


def text_of(e):
    return ''.join(t.text or '' for t in e.iter(W + 't'))


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


def run(text, rpr):
    r = el('r')
    if rpr is not None:
        r.append(copy.deepcopy(rpr))
    t = el('t')
    t.text = text
    t.set(XML_SPACE, 'preserve')
    r.append(t)
    return r


def sdt(ids, tag, children):
    s = el('sdt')
    pr = el('sdtPr')
    pr.append(el('alias', {'val': tag}))
    pr.append(el('tag', {'val': tag}))
    pr.append(el('id', {'val': ids.take()}))
    s.append(pr)
    s.append(el('sdtContent', children=children))
    return s


def first_run_rpr(p):
    r = p.find(W + 'r')
    return r.find(W + 'rPr') if r is not None else None


def one_line_positions(root):
    """Ba dòng chức vụ → ifnot:POSITIONS_ONE_LINE; thêm một dòng if:POSITIONS_ONE_LINE ngay trước."""
    if find_sdt(root, 'if:POSITIONS_ONE_LINE') is not None:
        return False
    paras = [para_of(find_sdt(root, t)) for t in ('PARTY_POSITION', 'ADMIN_POSITION', 'MASS_POSITION')]
    if any(p is None for p in paras):
        raise SystemExit('Không thấy đủ 3 dòng chức vụ')
    body = paras[0].getparent()
    idx = [body.index(p) for p in paras]
    if idx != list(range(idx[0], idx[0] + 3)):
        raise SystemExit('Ba dòng chức vụ không liền nhau')
    ids = Ids(root)

    # Dòng mới lấy định dạng của dòng "Họ và tên" ngay trên.
    name_para = paras[0].getprevious()
    ppr = name_para.find(W + 'pPr')
    rpr = first_run_rpr(name_para)
    line = el('p')
    if ppr is not None:
        line.append(copy.deepcopy(ppr))
    line.append(run(ONE_LINE_LABEL, rpr))
    line.append(sdt(ids, 'POSITIONS', [run('…', rpr)]))

    three = sdt(ids, 'ifnot:POSITIONS_ONE_LINE', [])
    body.insert(idx[0], three)
    for p in paras:
        three.find(W + 'sdtContent').append(p)
    body.insert(idx[0], sdt(ids, 'if:POSITIONS_ONE_LINE', [line]))
    return True


def signature_table(root):
    """Đoạn chữ ký 09B → bảng 2 cột không viền; cột trái trong if:HAS_CELL_CONFIRMATION."""
    if find_sdt(root, 'if:HAS_CELL_CONFIRMATION') is not None:
        return False
    para = None
    for p in root.iter(W + 'p'):
        if 'XÁC NHẬN CỦA CHI BỘ' in text_of(p) and 'NGƯỜI TỰ ĐÁNH GIÁ' in text_of(p):
            para = p
            break
    if para is None:
        raise SystemExit('Không thấy đoạn chữ ký "XÁC NHẬN CỦA CHI BỘ … NGƯỜI TỰ ĐÁNH GIÁ"')

    bold = italic = None
    for r in para.iter(W + 'r'):
        t = text_of(r)
        if bold is None and 'XÁC NHẬN' in t:
            bold = r.find(W + 'rPr')
        if italic is None and t.startswith('(X'):
            italic = r.find(W + 'rPr')

    sect = root.find(W + 'body/' + W + 'sectPr')
    width = int(sect.find(W + 'pgSz').get(W + 'w'))
    mar = sect.find(W + 'pgMar')
    usable = width - int(mar.get(W + 'left')) - int(mar.get(W + 'right'))
    half = usable // 2
    ids = Ids(root)

    def cell_para(text, rpr, condition):
        p = el('p', children=[el('pPr', children=[el('spacing', {'before': 0, 'after': 0}), el('jc', {'val': 'center'})])])
        r = run(text, rpr)
        p.append(sdt(ids, condition, [r]) if condition else r)
        return p

    def cell(paragraphs):
        tc = el('tc', children=[el('tcPr', children=[el('tcW', {'w': half, 'type': 'dxa'})])])
        for p in paragraphs:
            tc.append(p)
        return tc

    none = {'val': 'nil'}
    tbl = el('tbl', children=[
        el('tblPr', children=[
            el('tblW', {'w': half * 2, 'type': 'dxa'}),
            el('tblBorders', children=[el(b, none) for b in ('top', 'left', 'bottom', 'right', 'insideH', 'insideV')]),
            el('tblLayout', {'type': 'fixed'}),
            el('tblLook', {'val': '0000', 'firstRow': 0, 'lastRow': 0, 'firstColumn': 0, 'lastColumn': 0, 'noHBand': 1, 'noVBand': 1}),
        ]),
        el('tblGrid', children=[el('gridCol', {'w': half}), el('gridCol', {'w': half})]),
        el('tr', children=[
            cell([cell_para('XÁC NHẬN CỦA CHI BỘ', bold, 'if:HAS_CELL_CONFIRMATION'),
                  cell_para('(Xác lập thời điểm, ký, ghi rõ họ tên)', italic, 'if:HAS_CELL_CONFIRMATION')]),
            cell([cell_para('NGƯỜI TỰ ĐÁNH GIÁ', bold, None),
                  cell_para('(Ký, ghi rõ họ tên)', italic, None)]),
        ]),
    ])
    para.addprevious(tbl)
    para.getparent().remove(para)
    return True


def date_line_9d(root):
    """Dòng địa danh – ngày dưới "ĐẢNG CỘNG SẢN VIỆT NAM" (cột phải bảng tiêu đề) trong if:HAS_DATE_LINE."""
    if find_sdt(root, 'if:HAS_DATE_LINE') is not None:
        return False
    cell = None
    for tc in root.iter(W + 'tc'):
        if text_of(tc).strip() == 'ĐẢNG CỘNG SẢN VIỆT NAM':
            cell = tc
            break
    if cell is None:
        raise SystemExit('Không thấy ô "ĐẢNG CỘNG SẢN VIỆT NAM" của bảng tiêu đề')
    ids = Ids(root)
    rpr = el('rPr', children=[
        el('rFonts', {'ascii': 'Times New Roman', 'hAnsi': 'Times New Roman', 'cs': 'Times New Roman'}),
        el('i'), el('sz', {'val': 26}), el('szCs', {'val': 26})])
    p = el('p', children=[el('pPr', children=[
        el('spacing', {'before': 120, 'after': 0, 'line': 240, 'lineRule': 'auto'}),
        el('jc', {'val': 'center'}),
        el('rPr', children=[el('i'), el('sz', {'val': 26}), el('szCs', {'val': 26})])])])
    p.append(sdt(ids, 'ORG_LOCATION', [run('…', rpr)]))
    p.append(run(', ', rpr))
    p.append(sdt(ids, 'DATE_LINE', [run('ngày … tháng … năm …', rpr)]))
    cell.append(sdt(ids, 'if:HAS_DATE_LINE', [p]))
    return True


def keep_signature_together(root):
    """Phần ký không bị tách: dòng bảng chữ ký (09B) không tách trang; đoạn tiêu đề ký (9D) đi liền đoạn sau."""
    changed = False
    for tr in root.iter(W + 'tr'):
        if 'NGƯỜI TỰ ĐÁNH GIÁ' in text_of(tr) and tr.find(W + 'trPr/' + W + 'cantSplit') is None:
            trpr = tr.find(W + 'trPr')
            if trpr is None:
                trpr = el('trPr')
                prex = tr.find(W + 'tblPrEx')
                tr.insert(0 if prex is None else tr.index(prex) + 1, trpr)
            trpr.append(el('cantSplit'))
            changed = True
    for p in root.iter(W + 'p'):
        if 'XÁC NHẬN CỦA CHI BỘ' in text_of(p) and 'CÁ NHÂN TỰ ĐÁNH GIÁ' in text_of(p):
            ppr = p.find(W + 'pPr')
            if ppr is None:
                ppr = el('pPr')
                p.insert(0, ppr)
            if ppr.find(W + 'keepNext') is None:
                style = ppr.find(W + 'pStyle')
                ppr.insert(0 if style is None else 1, el('keepNext'))
                changed = True
    return changed


def process(path, steps):
    src = zipfile.ZipFile(path)
    root = etree.fromstring(src.read('word/document.xml'))
    changed = False
    for step in steps:
        changed = step(root) or changed
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
    folder = sys.argv[1]
    process(os.path.join(folder, FILE_09B), (one_line_positions, signature_table, keep_signature_together))
    process(os.path.join(folder, FILE_9D), (one_line_positions, date_line_9d, keep_signature_together))
