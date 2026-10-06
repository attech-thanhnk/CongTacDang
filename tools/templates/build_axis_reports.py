# -*- coding: utf-8 -*-
"""
Dựng lại phần theo trục của template Mẫu 09C và Mẫu 9D theo biểu mẫu gốc (nhập một lần theo trục trên phiếu tự chấm):

- Mẫu 9D: bảng đúng một dòng mỗi trục ("Trục n" + các cột (2)–(7)). Bỏ khối lặp dòng nhiệm vụ repeat:ROWS; các ô (3)–(7) của
  dòng nhiệm vụ chuyển lên dòng trục, Tag R_DEADLINE… đổi thành A_DEADLINE, A_STATUS, A_PRODUCT, A_PROGRESS, A_NOTE.
- Mẫu 09C: trong khối repeat:SECTIONS, đoạn S_CONTENT bọc trong if:S_SHOW_CONTENT (ẩn khi mục chỉ có nhiệm vụ theo trục); sau đó
  thêm khối repeat:S_AXES gồm đoạn tiêu đề trục AX_TITLE (in đậm như tiêu đề mục) và khối lặp repeat:AX_LINES, mỗi dòng nhiệm vụ /
  ý con một đoạn L_TEXT (định dạng như đoạn nội dung). Bỏ đoạn S_NOTE (lưu ý hướng dẫn viết, chữ đỏ) — chỉ hiện trên màn hình
  nhập, bản nộp không in.

Chạy lại an toàn (bước nào đã làm thì bỏ qua).
Cách chạy: py -3 build_axis_reports.py <thư mục Templates/Word>
"""
import sys, os, copy, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
W14 = '{http://schemas.microsoft.com/office/word/2010/wordml}'

FILE_09C = 'Mau_09C_BanTuDanhGia.docx'
FILE_9D = 'Mau_9D_PhuLucKetQuaNhiemVu.docx'


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


class Ids:
    def __init__(self, root):
        ids = [int(i.get(W + 'val')) for i in root.iter(W + 'id') if (i.get(W + 'val') or '').lstrip('-').isdigit()]
        self.next = max(ids + [900000]) + 1

    def take(self):
        self.next += 1
        return str(self.next - 1)


def block_sdt(ids, tag, children):
    s = etree.Element(W + 'sdt')
    pr = etree.SubElement(s, W + 'sdtPr')
    etree.SubElement(pr, W + 'alias').set(W + 'val', tag)
    etree.SubElement(pr, W + 'tag').set(W + 'val', tag)
    etree.SubElement(pr, W + 'id').set(W + 'val', ids.take())
    c = etree.SubElement(s, W + 'sdtContent')
    for ch in children:
        c.append(ch)
    return s


def strip_para_ids(e):
    for n in e.iter():
        for a in (W14 + 'paraId', W14 + 'textId'):
            if a in n.attrib:
                del n.attrib[a]


def retag(sdt, ids, tag):
    pr = sdt.find(W + 'sdtPr')
    pr.find(W + 'alias').set(W + 'val', tag)
    pr.find(W + 'tag').set(W + 'val', tag)
    pr.find(W + 'id').set(W + 'val', ids.take())


def set_placeholder(sdt, text):
    """Giữ run đầu của control (định dạng), đặt chữ mặc định."""
    runs = list(content_of(sdt).iter(W + 'r'))
    first = runs[0]
    for r in runs[1:]:
        r.getparent().remove(r)
    for ch in list(first):
        if ch.tag != W + 'rPr':
            first.remove(ch)
    t = etree.SubElement(first, W + 't')
    t.text = text
    t.set('{http://www.w3.org/XML/1998/namespace}space', 'preserve')


def fix_9d(root):
    rows = find_sdt(root, 'repeat:ROWS')
    if rows is None:
        return False
    ids = Ids(root)
    task_row = content_of(rows).find(W + 'tr')
    axis_row = rows.getprevious()
    assert axis_row is not None and axis_row.tag == W + 'tr', 'không thấy dòng trục trước repeat:ROWS'
    axis_cells = axis_row.findall(W + 'tc')
    task_cells = task_row.findall(W + 'tc')
    names = {'R_DEADLINE': 'A_DEADLINE', 'R_STATUS': 'A_STATUS', 'R_PRODUCT': 'A_PRODUCT', 'R_PROGRESS': 'A_PROGRESS', 'R_NOTE': 'A_NOTE'}
    for i in range(2, 7):
        cell = copy.deepcopy(task_cells[i])
        strip_para_ids(cell)
        for s in cell.iter(W + 'sdt'):
            tag = sdt_tag(s)
            retag(s, ids, names[tag])
        axis_row.replace(axis_cells[i], cell)
    rows.getparent().remove(rows)
    return True


def remove_09c_note(root):
    note = find_sdt(root, 'S_NOTE')
    if note is None:
        return False
    p = note.getparent()
    while p.tag != W + 'p':
        p = p.getparent()
    p.getparent().remove(p)
    return True


def fix_09c(root):
    if find_sdt(root, 'repeat:S_AXES') is not None:
        return remove_09c_note(root)
    ids = Ids(root)
    title = find_sdt(root, 'S_TITLE').getparent()      # đoạn tiêu đề mục (in đậm)
    content_sdt = find_sdt(root, 'S_CONTENT')
    content = content_sdt.getparent()                  # đoạn nội dung mục

    # Đoạn nội dung chỉ hiện khi có nhận xét chung hoặc mục không có nhiệm vụ theo trục (giữ dòng chấm để viết tay).
    parent = content.getparent()
    idx = parent.index(content)
    parent.remove(content)
    show = block_sdt(ids, 'if:S_SHOW_CONTENT', [content])
    parent.insert(idx, show)

    # Tiêu đề trục: bản sao đoạn tiêu đề mục, control AX_TITLE.
    axis_title = copy.deepcopy(title)
    strip_para_ids(axis_title)
    t_sdt = next(axis_title.iter(W + 'sdt'))
    retag(t_sdt, ids, 'AX_TITLE')
    set_placeholder(t_sdt, '1. Tên trục')
    spacing = axis_title.find(W + 'pPr/' + W + 'spacing')
    if spacing is not None and spacing.get(W + 'before') is not None:
        del spacing.attrib[W + 'before']

    # Dòng nhiệm vụ / ý con: bản sao đoạn nội dung, control L_TEXT.
    line = copy.deepcopy(content)
    strip_para_ids(line)
    l_sdt = next(line.iter(W + 'sdt'))
    retag(l_sdt, ids, 'L_TEXT')
    set_placeholder(l_sdt, 'Nhiệm vụ 1: …')

    lines = block_sdt(ids, 'repeat:AX_LINES', [line])
    axes = block_sdt(ids, 'repeat:S_AXES', [axis_title, lines])
    parent.insert(idx + 1, axes)
    remove_09c_note(root)
    return True


def process(path, step):
    with zipfile.ZipFile(path) as z:
        xml = z.read('word/document.xml')
    root = etree.fromstring(xml)
    if not step(root):
        print('bỏ qua (đã dựng):', os.path.basename(path))
        return
    data = etree.tostring(root, xml_declaration=True, encoding='UTF-8', standalone=True)
    tmp = path + '.tmp'
    with zipfile.ZipFile(path) as zin, zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as zout:
        for item in zin.infolist():
            zout.writestr(item, data if item.filename == 'word/document.xml' else zin.read(item.filename))
    shutil.move(tmp, path)
    print('đã dựng:', os.path.basename(path))


if __name__ == '__main__':
    folder = sys.argv[1]
    process(os.path.join(folder, FILE_9D), fix_9d)
    process(os.path.join(folder, FILE_09C), fix_09c)
