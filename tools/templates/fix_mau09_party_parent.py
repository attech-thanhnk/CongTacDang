# -*- coding: utf-8 -*-
"""
Dòng 1 tiêu đề trái Mẫu 09A, 09C, 9D là tổ chức Đảng cấp trên trực tiếp của Chi bộ hồ sơ:
đổi tag ORG_PARTY_NAME (tên Đảng bộ trong cài đặt đơn vị) thành PARTY_PARENT_UPPER. Giữ nguyên chữ mặc định và định dạng.

Chạy lại an toàn. Cách chạy: py -3 fix_mau09_party_parent.py <thư mục Templates/Word>
"""
import sys, os, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS

FILES = ['Mau_09A_PhieuTuCham.docx', 'Mau_09C_BanTuDanhGia.docx', 'Mau_9D_PhuLucKetQuaNhiemVu.docx']
OLD, NEW = 'ORG_PARTY_NAME', 'PARTY_PARENT_UPPER'


def retag(root):
    for pr in root.iter(W + 'sdtPr'):
        tag = pr.find(W + 'tag')
        if tag is None or tag.get(W + 'val') != OLD:
            continue
        tag.set(W + 'val', NEW)
        alias = pr.find(W + 'alias')
        if alias is not None and alias.get(W + 'val') == OLD:
            alias.set(W + 'val', NEW)


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
        process(os.path.join(sys.argv[1], name), retag)
