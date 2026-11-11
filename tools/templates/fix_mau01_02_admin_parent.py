# -*- coding: utf-8 -*-
"""
Dòng 1 tiêu đề trái Mẫu 01, 02 là cơ quan chủ quản trực tiếp của đơn vị hồ sơ:
control ORG_COMPANY_NAME_UPPER (tên công ty dùng chung) bọc trong ifnot:HAS_ADMIN_PARENT, chèn trước nó
if:HAS_ADMIN_PARENT chứa control ADMIN_PARENT_UPPER cùng định dạng và chữ mặc định. Đơn vị trực thuộc Công ty in như cũ.
Hồ sơ thuộc chính đơn vị gốc (lãnh đạo Công ty, ifnot:HAS_DEPARTMENT): trước tên công ty chèn tên đơn vị chủ quản
ORG_PARENT_COMPANY_NAME_UPPER và ngắt dòng, bọc trong if:ORG_HAS_PARENT_COMPANY (cài đặt chưa có thì vẫn một dòng).
Độ rộng ô tiêu đề (tcW) đặt bằng cột lưới để Word không ép tên tổ chức xuống hai dòng như LibreOffice.

Chạy lại an toàn. Cách chạy: py -3 fix_mau01_02_admin_parent.py <thư mục Templates/Word>
"""
import sys, os, copy, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS

FILES = ['Mau_01_PhieuGiaoNhiemVu.docx', 'Mau_02_TuDanhGia.docx']
COMPANY, PARENT = 'ORG_COMPANY_NAME_UPPER', 'ADMIN_PARENT_UPPER'
IF_PARENT, IFNOT_PARENT = 'if:HAS_ADMIN_PARENT', 'ifnot:HAS_ADMIN_PARENT'
GROUP, NO_DEPT, IF_GROUP = 'ORG_PARENT_COMPANY_NAME_UPPER', 'ifnot:HAS_DEPARTMENT', 'if:ORG_HAS_PARENT_COMPANY'
# Id riêng cho control mới, không trùng id đang có trong hai template.
IDS = {IF_PARENT: '900101', PARENT: '900102', IFNOT_PARENT: '900103', NO_DEPT: '900104', IF_GROUP: '900105', GROUP: '900106'}


def tag_of(sdt):
    t = sdt.find(W + 'sdtPr/' + W + 'tag')
    return t.get(W + 'val') if t is not None else None


def new_sdt(tag, children):
    sdt = etree.Element(W + 'sdt')
    pr = etree.SubElement(sdt, W + 'sdtPr')
    etree.SubElement(pr, W + 'alias').set(W + 'val', tag)
    etree.SubElement(pr, W + 'tag').set(W + 'val', tag)
    etree.SubElement(pr, W + 'id').set(W + 'val', IDS[tag])
    content = etree.SubElement(sdt, W + 'sdtContent')
    for c in children:
        content.append(c)
    return sdt


def wrap(root):
    if any(tag_of(s) == PARENT for s in root.iter(W + 'sdt')):
        return
    company = next(s for s in root.iter(W + 'sdt') if tag_of(s) == COMPANY)
    parent_node = company.getparent()
    index = parent_node.index(company)

    # Control mới chép nguyên control tên công ty (định dạng, chữ mặc định), chỉ đổi tag.
    parent_ctl = copy.deepcopy(company)
    pr = parent_ctl.find(W + 'sdtPr')
    pr.find(W + 'alias').set(W + 'val', PARENT)
    pr.find(W + 'tag').set(W + 'val', PARENT)
    pr.find(W + 'id').set(W + 'val', IDS[PARENT])

    parent_node.remove(company)
    parent_node.insert(index, new_sdt(IFNOT_PARENT, [company]))
    parent_node.insert(index, new_sdt(IF_PARENT, [parent_ctl]))


def add_group(root):
    if any(tag_of(s) == GROUP for s in root.iter(W + 'sdt')):
        return
    company = next(s for s in root.iter(W + 'sdt') if tag_of(s) == COMPANY)
    holder = company.getparent()
    index = holder.index(company)
    run = company.find(W + 'sdtContent/' + W + 'r')

    group = copy.deepcopy(company)
    pr = group.find(W + 'sdtPr')
    for name in ('alias', 'tag'):
        pr.find(W + name).set(W + 'val', GROUP)
    pr.find(W + 'id').set(W + 'val', IDS[GROUP])
    group.find(W + 'sdtContent/' + W + 'r/' + W + 't').text = 'TỔNG CÔNG TY'
    br = etree.Element(W + 'r')
    if run is not None and run.find(W + 'rPr') is not None:
        br.append(copy.deepcopy(run.find(W + 'rPr')))
    etree.SubElement(br, W + 'br')
    holder.insert(index, new_sdt(NO_DEPT, [new_sdt(IF_GROUP, [group, br])]))


def fit_header(root):
    company = next(s for s in root.iter(W + 'sdt') if tag_of(s) == COMPANY)
    table = next(a for a in company.iterancestors() if a.tag == W + 'tbl')
    grid = [g.get(W + 'w') for g in table.find(W + 'tblGrid')]
    for row in table.findall(W + 'tr'):
        cells = row.findall(W + 'tc')
        if len(cells) != len(grid):
            continue
        for cell, width in zip(cells, grid):
            tcw = cell.find(W + 'tcPr/' + W + 'tcW')
            if tcw is not None and tcw.get(W + 'w') != width:
                tcw.set(W + 'w', width)
                tcw.set(W + 'type', 'dxa')


def fix(root):
    wrap(root)
    add_group(root)
    fit_header(root)


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
    for name in FILES:
        process(os.path.join(sys.argv[1], name), fix)
