# -*- coding: utf-8 -*-
"""
Trình bày template Mẫu 09C theo bản tự đánh giá đã nộp (docs/tai-lieu-chuan, Mẫu 9C quý III/2026):

- "Mẫu số 09C" in nghiêng; tiêu đề "BẢN TỰ ĐÁNH GIÁ, XẾP LOẠI CỦA CÁ NHÂN" và dòng quý màu đen; dòng quý "Quý III Năm 2026"
  (bỏ dấu phẩy).
- "Họ và tên: … <tab> Ngày sinh: …": điểm dừng tab cho "Ngày sinh", dòng chấm ngày sinh thành control DATE_OF_BIRTH.
- "Đơn vị công tác: <DEPARTMENT>, <ORG_COMPANY_NAME>." (tên công ty là tag dùng chung thông tin đơn vị).
- Mục I: đoạn S_INTRO (câu dẫn, hoặc nhận xét chung thay câu dẫn) bọc trong if:S_HAS_INTRO; đoạn dòng chấm if:S_SHOW_CONTENT đổi
  thành if:S_SHOW_DOTS, bỏ control S_CONTENT (chỉ còn dòng chấm để viết tay khi chưa nhập gì).
- Tiêu đề trục AX_TITLE không thụt đầu dòng; dòng nhiệm vụ thụt 0,75 cm, thêm control L_LEVEL (câu nối mức, in nghiêng) ngay sau
  L_TEXT trong cùng đoạn.
- Giãn dòng 1,1 cho phần thông tin, mục I và phần II như bản đã nộp; căn đều hai bên đoạn nội dung.
- Ô "CÁ NHÂN TỰ ĐÁNH GIÁ (Ký, ghi rõ họ tên)": sau khoảng trống để ký thêm đoạn SIGNER_NAME (đậm, nghiêng, căn giữa).

Chạy sau build_axis_reports.py. Chạy lại an toàn (bước nào đã làm thì bỏ qua).
Cách chạy: py -3 fix_mau09c_layout.py <thư mục Templates/Word>
"""
import sys, os, copy, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
W14 = '{http://schemas.microsoft.com/office/word/2010/wordml}'
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'

FILE_09C = 'Mau_09C_BanTuDanhGia.docx'

# Thứ tự phần tử con theo lược đồ OpenXML (pPr, rPr) — chèn đúng chỗ để tài liệu hợp lệ.
PPR_ORDER = ['pStyle', 'keepNext', 'keepLines', 'pageBreakBefore', 'framePr', 'widowControl', 'numPr', 'suppressLineNumbers',
             'pBdr', 'shd', 'tabs', 'suppressAutoHyphens', 'kinsoku', 'wordWrap', 'overflowPunct', 'topLinePunct', 'autoSpaceDE',
             'autoSpaceDN', 'bidi', 'adjustRightInd', 'snapToGrid', 'spacing', 'ind', 'contextualSpacing', 'mirrorIndents',
             'suppressOverlap', 'jc', 'textDirection', 'textAlignment', 'textboxTightWrap', 'outlineLvl', 'divId', 'cnfStyle',
             'rPr', 'sectPr', 'pPrChange']
RPR_ORDER = ['rStyle', 'rFonts', 'b', 'bCs', 'i', 'iCs', 'caps', 'smallCaps', 'strike', 'dstrike', 'outline', 'shadow', 'emboss',
             'imprint', 'noProof', 'snapToGrid', 'vanish', 'webHidden', 'color', 'spacing', 'w', 'kern', 'position', 'sz', 'szCs',
             'highlight', 'u', 'effect', 'bdr', 'shd', 'fitText', 'vertAlign', 'rtl', 'cs', 'em', 'lang', 'eastAsianLayout',
             'specVanish', 'oMath']

DOTS = '……………………………………………………………………………………'


def sdt_tag(e):
    t = e.find(W + 'sdtPr/' + W + 'tag')
    return t.get(W + 'val') if t is not None else None


def find_sdt(root, tag):
    for s in root.iter(W + 'sdt'):
        if sdt_tag(s) == tag:
            return s
    return None


def content_of(sdt):
    return sdt.find(W + 'sdtContent')


def paragraph_of(e):
    while e is not None and e.tag != W + 'p':
        e = e.getparent()
    return e


def norm(text):
    """Bỏ khoảng trắng (kể cả khoảng trắng không ngắt U+00A0 của biểu mẫu gốc) ở hai đầu."""
    return text.replace(' ', ' ').strip()


def text_of(e):
    return ''.join(t.text or '' for t in e.iter(W + 't'))


class Ids:
    def __init__(self, root):
        ids = [int(i.get(W + 'val')) for i in root.iter(W + 'id') if (i.get(W + 'val') or '').lstrip('-').isdigit()]
        self.next = max(ids + [900000]) + 1

    def take(self):
        self.next += 1
        return str(self.next - 1)


def make_sdt(ids, tag, children):
    s = etree.Element(W + 'sdt')
    pr = etree.SubElement(s, W + 'sdtPr')
    etree.SubElement(pr, W + 'alias').set(W + 'val', tag)
    etree.SubElement(pr, W + 'tag').set(W + 'val', tag)
    etree.SubElement(pr, W + 'id').set(W + 'val', ids.take())
    c = etree.SubElement(s, W + 'sdtContent')
    for ch in children:
        c.append(ch)
    return s


def retag(sdt, ids, tag):
    pr = sdt.find(W + 'sdtPr')
    pr.find(W + 'alias').set(W + 'val', tag)
    pr.find(W + 'tag').set(W + 'val', tag)
    pr.find(W + 'id').set(W + 'val', ids.take())


def ordered_child(parent, name, order):
    """Lấy (hoặc tạo đúng vị trí theo lược đồ) phần tử con w:name."""
    found = parent.find(W + name)
    if found is not None:
        return found
    e = etree.Element(W + name)
    pos = order.index(name)
    for i, ch in enumerate(parent):
        local = etree.QName(ch).localname
        if local in order and order.index(local) > pos:
            parent.insert(i, e)
            return e
    parent.append(e)
    return e


def ppr(p):
    pr = p.find(W + 'pPr')
    if pr is None:
        pr = etree.Element(W + 'pPr')
        p.insert(0, pr)
    return pr


def rpr(r):
    pr = r.find(W + 'rPr')
    if pr is None:
        pr = etree.Element(W + 'rPr')
        r.insert(0, pr)
    return pr


def make_run(rpr_source, text):
    r = etree.Element(W + 'r')
    if rpr_source is not None:
        r.append(copy.deepcopy(rpr_source))
    t = etree.SubElement(r, W + 't')
    t.text = text
    t.set(XML_SPACE, 'preserve')
    return r


def set_runs_text(container, text):
    """Giữ run đầu (định dạng), bỏ các run còn lại, đặt chữ."""
    runs = list(container.iter(W + 'r'))
    first = runs[0]
    for r in runs[1:]:
        r.getparent().remove(r)
    for ch in list(first):
        if ch.tag != W + 'rPr':
            first.remove(ch)
    t = etree.SubElement(first, W + 't')
    t.text = text
    t.set(XML_SPACE, 'preserve')


def set_italic(r):
    pr = rpr(r)
    ordered_child(pr, 'i', RPR_ORDER)
    ordered_child(pr, 'iCs', RPR_ORDER)


def remove_red(p):
    changed = False
    for c in list(p.iter(W + 'color')):
        if (c.get(W + 'val') or '').upper() in ('EE0000', 'FF0000'):
            c.getparent().remove(c)
            changed = True
    return changed


def set_line(p, line):
    sp = ordered_child(ppr(p), 'spacing', PPR_ORDER)
    if sp.get(W + 'line') == str(line):
        return False
    sp.set(W + 'line', str(line))
    sp.set(W + 'lineRule', 'auto')
    return True


def set_jc(p, val):
    jc = ordered_child(ppr(p), 'jc', PPR_ORDER)
    if jc.get(W + 'val') == val:
        return False
    jc.set(W + 'val', val)
    return True


def set_first_line(p, value):
    pr = ppr(p)
    ind = pr.find(W + 'ind')
    if value is None:
        if ind is None:
            return False
        pr.remove(ind)
        return True
    ind = ordered_child(pr, 'ind', PPR_ORDER)
    if ind.get(W + 'firstLine') == str(value) and len(ind.attrib) == 1:
        return False
    ind.attrib.clear()
    ind.set(W + 'firstLine', str(value))
    return True


def wrap_paragraph(ids, p, tag):
    """Bọc đoạn văn p trong control khối tag (nếu chưa bọc)."""
    parent = p.getparent()
    if parent.tag == W + 'sdtContent' and sdt_tag(parent.getparent()) == tag:
        return False
    idx = parent.index(p)
    parent.remove(p)
    parent.insert(idx, make_sdt(ids, tag, [p]))
    return True


def fix_header(root):
    changed = False
    for p in root.iter(W + 'p'):
        txt = text_of(p)
        if txt.replace(' ', '').startswith('Mẫusố09C'):
            for r in p.iter(W + 'r'):
                if r.find(W + 'rPr/' + W + 'i') is None:
                    set_italic(r)
                    changed = True
        if 'BẢN TỰ ĐÁNH GIÁ, XẾP LOẠI CỦA CÁ NHÂN' in txt:
            changed |= remove_red(p)
    quarter = paragraph_of(find_sdt(root, 'QUARTER'))
    changed |= remove_red(quarter)
    for t in quarter.iter(W + 't'):
        if norm(t.text or '') == ', Năm':
            t.text = ' Năm '
            changed = True
    return changed


def fix_birth_date(root, ids):
    if find_sdt(root, 'DATE_OF_BIRTH') is not None:
        return False
    name_sdt = find_sdt(root, 'FULL_NAME')
    p = paragraph_of(name_sdt)
    # Chỗ chấm họ tên ngắn lại để "Ngày sinh" đứng ở điểm dừng tab.
    set_runs_text(content_of(name_sdt), '………………………')
    tabs = ordered_child(ppr(p), 'tabs', PPR_ORDER)
    if tabs.find(W + 'tab') is None:
        tab = etree.SubElement(tabs, W + 'tab')
        tab.set(W + 'val', 'left')
        tab.set(W + 'pos', '6237')
    runs = [r for r in p if r.tag == W + 'r']
    label = next(r for r in runs if norm(text_of(r)) == 'Ngày')
    label.find(W + 't').text = 'Ngày '
    tab_run = etree.Element(W + 'r')
    tab_run.append(copy.deepcopy(label.find(W + 'rPr')))
    etree.SubElement(tab_run, W + 'tab')
    label.addprevious(tab_run)
    # Các run chấm sau "inh: " → control DATE_OF_BIRTH.
    dots = runs[runs.index(next(r for r in runs if norm(text_of(r)) == 'inh:')) + 1:]
    assert dots and all(set(text_of(r)) <= set('….') for r in dots), 'không thấy dòng chấm ngày sinh'
    anchor = dots[0]
    idx = p.index(anchor)
    for r in dots:
        p.remove(r)
    sdt = make_sdt(ids, 'DATE_OF_BIRTH', [dots[0]])
    set_runs_text(content_of(sdt), '………………')
    p.insert(idx, sdt)
    return True


def fix_department(root, ids):
    dept = find_sdt(root, 'DEPARTMENT')
    p = paragraph_of(dept)
    if any(sdt_tag(s) == 'ORG_COMPANY_NAME' for s in p.iter(W + 'sdt')):
        return False
    set_runs_text(content_of(dept), '……………………………')
    run_pr = content_of(dept).find(W + 'r/' + W + 'rPr')
    company = make_sdt(ids, 'ORG_COMPANY_NAME', [make_run(run_pr, '…………………………………')])
    dept.addnext(make_run(run_pr, ', '))
    p[-1].addnext(company)
    company.addnext(make_run(run_pr, '.'))
    return True


def fix_section(root, ids):
    changed = False
    intro = paragraph_of(find_sdt(root, 'S_INTRO'))
    changed |= set_jc(intro, 'both')
    changed |= wrap_paragraph(ids, intro, 'if:S_HAS_INTRO')

    show = find_sdt(root, 'if:S_SHOW_CONTENT')
    if show is not None:
        retag(show, ids, 'if:S_SHOW_DOTS')
        changed = True
    content = find_sdt(root, 'S_CONTENT')
    if content is not None:
        p = content.getparent()
        idx = p.index(content)
        for ch in reversed(list(content_of(content))):
            p.insert(idx, ch)
        p.remove(content)
        set_runs_text(p, DOTS)
        changed = True

    axis_title = paragraph_of(find_sdt(root, 'AX_TITLE'))
    changed |= set_first_line(axis_title, None)
    changed |= set_jc(axis_title, 'both')
    set_runs_text(content_of(find_sdt(root, 'AX_TITLE')), '1. Tên trục (trục 1)')

    text_sdt = find_sdt(root, 'L_TEXT')
    line = paragraph_of(text_sdt)
    changed |= set_first_line(line, 426)
    changed |= set_jc(line, 'both')
    if find_sdt(root, 'L_LEVEL') is None:
        set_runs_text(content_of(text_sdt), '- Nhiệm vụ 1: ….')
        run = make_run(content_of(text_sdt).find(W + 'r/' + W + 'rPr'), ' Tự đánh giá mức …')
        set_italic(run)
        text_sdt.addnext(make_sdt(ids, 'L_LEVEL', [run]))
        changed = True
    return changed


def fix_spacing(root):
    """Giãn dòng 1,1 (line=264) cho phần thông tin, mục I (trừ tiêu đề trục) và phần II như bản đã nộp."""
    body = root.find(W + 'body')
    start = paragraph_of(find_sdt(root, 'FULL_NAME'))
    end = paragraph_of(find_sdt(root, 'SELF_GRADE'))
    axis_title = paragraph_of(find_sdt(root, 'AX_TITLE'))
    paragraphs = list(body.iter(W + 'p'))
    changed = False
    for p in paragraphs[paragraphs.index(start):paragraphs.index(end) + 1]:
        if p is axis_title or p.getparent().tag == W + 'tc':
            continue
        changed |= set_line(p, 264)
    return changed


def signer_paragraph():
    """Đoạn căn giữa, cỡ 14 như các đoạn trong ô ký."""
    p = etree.Element(W + 'p')
    pr = etree.SubElement(p, W + 'pPr')
    sp = etree.SubElement(pr, W + 'spacing')
    sp.set(W + 'before', '120')
    sp.set(W + 'line', '234')
    sp.set(W + 'lineRule', 'atLeast')
    etree.SubElement(pr, W + 'jc').set(W + 'val', 'center')
    return p


def fix_signer(root, ids):
    changed = False
    sdt = find_sdt(root, 'SIGNER_NAME')
    if sdt is None:
        title = next(p for p in root.iter(W + 'p') if 'NHÂN TỰ ĐÁNH GIÁ' in text_of(p))
        cell = title.getparent()
        assert cell.tag == W + 'tc', 'ô ký không nằm trong bảng'
        last = cell.findall(W + 'p')[-1]
        p = signer_paragraph()
        r = etree.Element(W + 'r')
        r_pr = etree.SubElement(r, W + 'rPr')
        for name in ('b', 'bCs', 'i', 'iCs'):
            etree.SubElement(r_pr, W + name)
        etree.SubElement(r_pr, W + 'sz').set(W + 'val', '28')
        etree.SubElement(r_pr, W + 'szCs').set(W + 'val', '28')
        t = etree.SubElement(r, W + 't')
        t.text = ''
        t.set(XML_SPACE, 'preserve')
        sdt = make_sdt(ids, 'SIGNER_NAME', [r])
        p.append(sdt)
        last.addnext(p)
        changed = True
    # Một dòng trống sau họ tên (cách phần xác nhận của Chi bộ) như bản đã nộp.
    p = paragraph_of(sdt)
    if p.getnext() is None:
        p.addnext(signer_paragraph())
        changed = True
    return changed


def fix_09c(root):
    ids = Ids(root)
    changed = fix_header(root)
    changed |= fix_birth_date(root, ids)
    changed |= fix_department(root, ids)
    changed |= fix_section(root, ids)
    changed |= fix_spacing(root)
    changed |= fix_signer(root, ids)
    return changed


def process(path, step):
    with zipfile.ZipFile(path) as z:
        xml = z.read('word/document.xml')
    root = etree.fromstring(xml)
    before = etree.tostring(root)
    step(root)
    if etree.tostring(root) == before:
        print('bỏ qua (đã sửa):', os.path.basename(path))
        return
    data = etree.tostring(root, xml_declaration=True, encoding='UTF-8', standalone=True)
    tmp = path + '.tmp'
    with zipfile.ZipFile(path) as zin, zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as zout:
        for item in zin.infolist():
            zout.writestr(item, data if item.filename == 'word/document.xml' else zin.read(item.filename))
    shutil.move(tmp, path)
    print('đã sửa:', os.path.basename(path))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    process(os.path.join(sys.argv[1], FILE_09C), fix_09c)
