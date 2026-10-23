# -*- coding: utf-8 -*-
"""
Dòng "Đơn vị công tác: DEPARTMENT, ORG_COMPANY_NAME" của Mẫu 09C, 9D: bọc cụm "DEPARTMENT, " trong khối điều kiện
if:HAS_DEPARTMENT. Cán bộ thuộc đơn vị gốc (lãnh đạo Công ty) thì chỉ in tên công ty, như bản đã nộp của Chủ tịch,
Giám đốc trong docs/tai-lieu-chuan ("Đơn vị công tác: Công ty TNHH Kỹ thuật Quản lý bay").

Chạy lại an toàn (template đã có khối if:HAS_DEPARTMENT thì bỏ qua). Chạy sau fix_mau09b_layout.py, fix_mau09c_layout.py,
fix_mau9d_layout.py.
Cách chạy: py -3 tools/templates/fix_department_company.py <thư mục Templates/Word>
"""
import sys, os, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS

FILES = ['Mau_09C_BanTuDanhGia.docx', 'Mau_9D_PhuLucKetQuaNhiemVu.docx']
CONDITION = 'if:HAS_DEPARTMENT'


def sdt_tag(e):
    t = e.find(W + 'sdtPr/' + W + 'tag')
    return t.get(W + 'val') if t is not None else None


def find_sdt(root, tag):
    for s in root.iter(W + 'sdt'):
        if sdt_tag(s) == tag:
            return s
    return None


def el(tag, attrs=None):
    e = etree.Element(W + tag)
    for k, v in (attrs or {}).items():
        e.set(W + k, str(v))
    return e


def next_id(root):
    ids = [int(i.get(W + 'val')) for i in root.iter(W + 'id') if (i.get(W + 'val') or '').lstrip('-').isdigit()]
    return str(max(ids + [900000]) + 1)


def wrap_department(root):
    if find_sdt(root, CONDITION) is not None:
        return False
    dept = find_sdt(root, 'DEPARTMENT')
    if dept is None:
        raise SystemExit('Không thấy control DEPARTMENT')
    sep = dept.getnext()
    text = ''.join(t.text or '' for t in sep.iter(W + 't')) if sep is not None else ''
    if sep is None or sep.tag != W + 'r' or text != ', ':
        raise SystemExit('Sau control DEPARTMENT không phải run ", "')

    parent = dept.getparent()
    wrapper = el('sdt')
    pr = el('sdtPr')
    pr.append(el('alias', {'val': CONDITION}))
    pr.append(el('tag', {'val': CONDITION}))
    pr.append(el('id', {'val': next_id(root)}))
    wrapper.append(pr)
    content = el('sdtContent')
    wrapper.append(content)
    parent.insert(parent.index(dept), wrapper)
    content.append(dept)
    content.append(sep)
    return True


def process(path):
    src = zipfile.ZipFile(path)
    root = etree.fromstring(src.read('word/document.xml'))
    if not wrap_department(root):
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
    for name in FILES:
        process(os.path.join(sys.argv[1], name))
