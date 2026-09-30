# -*- coding: utf-8 -*-
"""
Bổ sung Content Control cho các template Word đã có (đợt rà soát xuất biểu mẫu):

- DATE_LINE: phần "ngày … tháng … năm …" ngay sau ORG_LOCATION (Mẫu 01, 02, 07, 08, 09A, 09B, 09C, 9D, 10, 16, 17) — lớp dữ
  liệu mẫu điền ngày lập/duyệt; null giữ dòng chấm.
- CHAIR_SIGN_TITLE / CHAIR_SIGN_NOTE: chức danh "BÍ THƯ" và ghi chú "(ký, ghi rõ họ tên, đóng dấu)" dưới "CHỦ TRÌ HỘI NGHỊ"
  (Mẫu 12, 13) — lấy theo chức vụ chủ trì đã lưu trên biên bản.
- BASE_AUTHORITY: "ĐẢNG ỦY/CHI ỦY CƠ SỞ…" ở tiêu đề mục II bảng kết quả Mẫu 13.
- Mẫu 17: ngắt dòng đầu control ORG_COMPANY_NAME_UPPER đưa ra ngoài control (điền giá trị không làm mất ngắt dòng, tên công ty
  không dính vào tên đơn vị cấp trên).
- Mẫu 10: "Mẫu số10" → "Mẫu số 10"; dòng chấm sau control DEPARTMENT đưa vào làm chữ mặc định của control (điền tên đơn vị
  không còn dòng chấm phía sau).

Chạy lại an toàn (bước nào đã làm thì bỏ qua). Mẫu 13 dựng bằng build_mau13.py — chạy script này sau build_mau13.py.
Cách chạy: py -3 fix_form_tags.py <thư mục Templates/Word>
"""
import sys, os, re, copy, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'
SENTINEL = '\x00'
IGNORABLE = {W + 'proofErr', W + 'bookmarkStart', W + 'bookmarkEnd', W + 'pPr'}


def el(tag):
    return etree.Element(W + tag)


def text_of(e):
    return ''.join(t.text or '' for t in e.iter(W + 't'))


def sdt_tag(e):
    t = e.find(W + 'sdtPr/' + W + 'tag')
    return t.get(W + 'val') if t is not None else None


def has_tag(root, tag):
    return any(sdt_tag(s) == tag for s in root.iter(W + 'sdt'))


class Doc:
    def __init__(self, root):
        self.root = root
        ids = [int(i.get(W + 'val')) for i in root.iter(W + 'id') if (i.get(W + 'val') or '').lstrip('-').isdigit()]
        self.next_id = max(ids + [900000]) + 1
        self.changed = False

    def sdt(self, tag, runs):
        s = el('sdt')
        pr = etree.SubElement(s, W + 'sdtPr')
        etree.SubElement(pr, W + 'alias').set(W + 'val', tag)
        etree.SubElement(pr, W + 'tag').set(W + 'val', tag)
        etree.SubElement(pr, W + 'id').set(W + 'val', str(self.next_id))
        self.next_id += 1
        content = etree.SubElement(s, W + 'sdtContent')
        for r in runs:
            content.append(r)
        return s


def tokens(p):
    """(ký tự, rPr, chỉ số phần tử con, tag control) của các run trực tiếp trong đoạn; control/phần tử khác là SENTINEL."""
    out = []
    for i, c in enumerate(p):
        if c.tag == W + 'r':
            rpr = c.find(W + 'rPr')
            for x in c:
                if x.tag == W + 't':
                    out.extend((ch, rpr, i, None) for ch in (x.text or ''))
                elif x.tag == W + 'tab':
                    out.append(('\t', rpr, i, None))
                elif x.tag == W + 'br':
                    out.append(('\n', rpr, i, None))
                elif x.tag in (W + 'rPr', W + 'lastRenderedPageBreak'):
                    pass
                else:
                    raise SystemExit('Run có phần tử không hỗ trợ: ' + x.tag)
        elif c.tag in IGNORABLE:
            pass
        else:
            out.append((SENTINEL, None, i, sdt_tag(c) if c.tag == W + 'sdt' else None))
    return out


def make_runs(toks):
    runs, cur, key0 = [], None, None
    for ch, rpr, _, _ in toks:
        key = etree.tostring(rpr) if rpr is not None else b''
        if cur is None or key != key0:
            cur = el('r')
            if rpr is not None:
                cur.append(copy.deepcopy(rpr))
            runs.append(cur)
            key0 = key
        if ch == '\t':
            cur.append(el('tab'))
        elif ch == '\n':
            cur.append(el('br'))
        else:
            last = cur[-1] if len(cur) else None
            if last is None or last.tag != W + 't':
                last = etree.SubElement(cur, W + 't')
                last.set(XML_SPACE, 'preserve')
                last.text = ''
            last.text += ch
    return runs


def wrap(doc, p, pattern, tag, after_tag=None):
    """Bao đoạn chữ khớp <pattern> (trong các run trực tiếp của đoạn, sau control <after_tag> nếu có) bằng control <tag>."""
    toks = tokens(p)
    text = ''.join(t[0] for t in toks)
    start = 0
    if after_tag is not None:
        start = next(i for i, t in enumerate(toks) if t[3] == after_tag) + 1
    m = re.compile(pattern).search(text, start)
    if not m:
        raise SystemExit(f'Không tìm thấy /{pattern}/ trong đoạn "{text}"')
    a, b = m.start(), m.end()
    while b > a and text[b - 1] in ' \t':
        b -= 1
    if SENTINEL in text[a:b]:
        raise SystemExit(f'Đoạn cần bao cắt ngang control khác: "{text[a:b]}"')
    i0, i1 = toks[a][2], toks[b - 1][2]
    before = [t for t in toks[:a] if t[2] == i0]
    after = [t for t in toks[b:] if t[2] == i1]
    children = list(p)
    anchor = children[i0]
    new = make_runs(before) + [doc.sdt(tag, make_runs(toks[a:b]))] + make_runs(after)
    for e in new:
        anchor.addprevious(e)
    doc.changed = True
    for c in children[i0:i1 + 1]:
        p.remove(c)


def paragraphs(root, pred):
    return [p for p in root.iter(W + 'p') if pred(p)]


def one(items, what):
    if len(items) != 1:
        raise SystemExit(f'Cần đúng 1 {what}, tìm thấy {len(items)}')
    return items[0]


def add_date_line(doc):
    if has_tag(doc.root, 'DATE_LINE'):
        return
    p = one(paragraphs(doc.root, lambda p: any(sdt_tag(c) == 'ORG_LOCATION' for c in p if c.tag == W + 'sdt')),
            'đoạn có ORG_LOCATION')
    wrap(doc, p, r'ngày[^,\x00]*?năm[ …\.\d]*', 'DATE_LINE', after_tag='ORG_LOCATION')


def add_chair_sign(doc):
    if not has_tag(doc.root, 'CHAIR_SIGN_TITLE'):
        p = one(paragraphs(doc.root, lambda p: text_of(p).strip() == 'BÍ THƯ'), 'đoạn "BÍ THƯ"')
        wrap(doc, p, r'BÍ THƯ', 'CHAIR_SIGN_TITLE')
    if not has_tag(doc.root, 'CHAIR_SIGN_NOTE'):
        note = '(ký, ghi rõ họ tên, đóng dấu)'
        p = one(paragraphs(doc.root, lambda p: text_of(p).strip() == note), 'đoạn ghi chú chữ ký chủ trì')
        wrap(doc, p, re.escape(note), 'CHAIR_SIGN_NOTE')


def add_base_authority(doc):
    if has_tag(doc.root, 'BASE_AUTHORITY'):
        return
    p = one(paragraphs(doc.root, lambda p: text_of(p).endswith('CỦA ĐẢNG ỦY/CHI ỦY CƠ SỞ…')), 'tiêu đề mục II bảng kết quả')
    wrap(doc, p, re.escape('ĐẢNG ỦY/CHI ỦY CƠ SỞ…'), 'BASE_AUTHORITY')


def move_company_break(doc):
    s = one([s for s in doc.root.iter(W + 'sdt') if sdt_tag(s) == 'ORG_COMPANY_NAME_UPPER'], 'control ORG_COMPANY_NAME_UPPER')
    run = s.find(W + 'sdtContent/' + W + 'r')
    first = next((x for x in run if x.tag != W + 'rPr'), None)
    if first is None or first.tag != W + 'br':
        return
    run.remove(first)
    br_run = el('r')
    rpr = run.find(W + 'rPr')
    if rpr is not None:
        br_run.append(copy.deepcopy(rpr))
    br_run.append(el('br'))
    s.addprevious(br_run)
    doc.changed = True


def fix_mau10(doc):
    for t in doc.root.iter(W + 't'):
        if t.text == 'ẫu số':
            t.text = 'ẫu số '
            doc.changed = True
            t.set(XML_SPACE, 'preserve')
    s = one([s for s in doc.root.iter(W + 'sdt') if sdt_tag(s) == 'DEPARTMENT'], 'control DEPARTMENT')
    inner = s.find(W + 'sdtContent/' + W + 'r/' + W + 't')
    nxt = s.getnext()
    if inner is None or (inner.text or '') != '' or nxt is None or nxt.tag != W + 'r':
        return
    t = nxt.find(W + 't')
    m = re.match(r'(…+)(.*)$', t.text or '', re.S)
    if not m:
        return
    inner.text = m.group(1)
    inner.set(XML_SPACE, 'preserve')
    t.text = m.group(2)
    t.set(XML_SPACE, 'preserve')
    doc.changed = True


STEPS = {
    'Mau_01_PhieuGiaoNhiemVu.docx': [add_date_line],
    'Mau_02_TuDanhGia.docx': [add_date_line],
    'Mau_07_BaoCaoTuDanhGiaTapThe.docx': [add_date_line],
    'Mau_08_TongHopKetQuaNhiemVu.docx': [add_date_line],
    'Mau_09A_PhieuTuCham.docx': [add_date_line],
    'Mau_09B_PhieuTuChamTheoTruc.docx': [add_date_line],
    'Mau_09C_BanTuDanhGia.docx': [add_date_line],
    'Mau_9D_PhuLucKetQuaNhiemVu.docx': [add_date_line],
    'Mau_10_PhieuThamDinh.docx': [add_date_line, fix_mau10],
    'Mau_12_BienBanHoiNghi.docx': [add_chair_sign],
    'Mau_13_BienBanKiemPhieu.docx': [add_chair_sign, add_base_authority],
    'Mau_16_BaoCaoKetQuaDanhGia.docx': [add_date_line],
    'Mau_17_KeHoachHoTro.docx': [add_date_line, move_company_break],
}


def process(path, steps):
    src = zipfile.ZipFile(path)
    raw = src.read('word/document.xml')
    root = etree.fromstring(raw)
    doc = Doc(root)
    for step in steps:
        step(doc)
    out_xml = etree.tostring(root, xml_declaration=True, encoding='UTF-8', standalone=True)
    if not doc.changed:
        src.close()
        print('SKIP (da sua)', os.path.basename(path))
        return
    tmp = path + '.tmp'
    with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as out:
        for it in src.infolist():
            out.writestr(it, out_xml if it.filename == 'word/document.xml' else src.read(it.filename))
    src.close()
    shutil.move(tmp, path)
    print('OK', os.path.basename(path))


if __name__ == '__main__':
    folder = sys.argv[1]
    for name, steps in STEPS.items():
        process(os.path.join(folder, name), steps)
