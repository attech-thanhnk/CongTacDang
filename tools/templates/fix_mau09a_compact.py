# -*- coding: utf-8 -*-
"""
Gọn template Mẫu 09A để phiếu thường còn 3 trang (không để trang cuối chỉ còn cam kết và chữ ký):

- Đoạn chỉ đặt khoảng cách sau mà không đặt giãn dòng thì đang nhận giãn dòng 1,3 mặc định của tài liệu:
  đặt giãn dòng đơn; đoạn trong bảng thu khoảng cách sau còn 1 pt.
- Đoạn giãn dòng 1,25 (dòng "Mẫu số", dòng trống dưới tiêu đề) về giãn dòng đơn.
- Đoạn trống ngay trước/sau bảng tiêu chí chung thu thấp (6 pt).
- Lề trên/dưới 1,8 cm còn 1,5 cm.
- Khối "B. CAM KẾT TRÁCH NHIỆM" + lời cam đoan + bảng ký liền nhau (keepNext, hàng bảng ký không tách trang).

Không đổi chữ, không đổi tag. Chạy lại an toàn.
Cách chạy: py -3 fix_mau09a_compact.py <thư mục Templates/Word>
"""
import sys, os, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS

FILE_09A = 'Mau_09A_PhieuTuCham.docx'

PPR_ORDER = ['pStyle', 'keepNext', 'keepLines', 'pageBreakBefore', 'framePr', 'widowControl', 'numPr', 'suppressLineNumbers',
             'pBdr', 'shd', 'tabs', 'suppressAutoHyphens', 'kinsoku', 'wordWrap', 'overflowPunct', 'topLinePunct', 'autoSpaceDE',
             'autoSpaceDN', 'bidi', 'adjustRightInd', 'snapToGrid', 'spacing', 'ind', 'contextualSpacing', 'mirrorIndents',
             'suppressOverlap', 'jc', 'textDirection', 'textAlignment', 'textboxTightWrap', 'outlineLvl', 'divId', 'cnfStyle',
             'rPr', 'sectPr', 'pPrChange']
TRPR_ORDER = ['cnfStyle', 'divId', 'gridBefore', 'gridAfter', 'wBefore', 'wAfter', 'cantSplit', 'trHeight', 'tblHeader',
              'tblCellSpacing', 'jc', 'hidden', 'ins', 'del', 'trPrChange']


def text_of(e):
    return ''.join(t.text or '' for t in e.iter(W + 't'))


def child(parent, name, order):
    """Lấy (hoặc tạo đúng vị trí theo lược đồ) phần tử con."""
    found = parent.find(W + name)
    if found is not None:
        return found
    new = etree.Element(W + name)
    idx = order.index(name)
    pos = 0
    for i, c in enumerate(parent):
        local = etree.QName(c).localname
        if local in order and order.index(local) < idx:
            pos = i + 1
    parent.insert(pos, new)
    return new


def ppr(p):
    pp = p.find(W + 'pPr')
    if pp is None:
        pp = etree.Element(W + 'pPr')
        p.insert(0, pp)
    return pp


def set_attr(e, name, value):
    if e.get(W + name) != value:
        e.set(W + name, value)


def single_line(p, after=None):
    sp = child(ppr(p), 'spacing', PPR_ORDER)
    if sp.get(W + 'lineRule') in (None, 'auto'):
        set_attr(sp, 'line', '240')
        set_attr(sp, 'lineRule', 'auto')
    if after is not None:
        set_attr(sp, 'after', after)


def fix_09a(root):
    body = root.find(W + 'body')
    blocks = list(body)

    for p in root.iter(W + 'p'):
        sp = p.find(W + 'pPr/' + W + 'spacing')
        in_table = any(etree.QName(a).localname == 'tc' for a in p.iterancestors())
        # Đoạn không ghi giãn dòng nhận 1,3 của tài liệu; đoạn 1,25 cũng thu về đơn.
        if sp is None or sp.get(W + 'line') is None or sp.get(W + 'line') == '300':
            single_line(p, '20' if in_table else None)

    # Đoạn trống bao quanh bảng tiêu chí chung chỉ để tách bảng, không cần cao cả dòng chữ.
    tables = [b for b in blocks if b.tag == W + 'tbl']
    criteria = next(t for t in tables if 'Nội dung tiêu chí' in text_of(t))
    i = blocks.index(criteria)
    for p in (blocks[i - 1], blocks[i + 1]):
        if p.tag == W + 'p' and not text_of(p).strip():
            sp = child(ppr(p), 'spacing', PPR_ORDER)
            set_attr(sp, 'before', '0')
            set_attr(sp, 'after', '0')
            set_attr(sp, 'line', '120')
            set_attr(sp, 'lineRule', 'exact')

    # Cam kết + lời cam đoan + bảng ký luôn cùng trang.
    head = next(b for b in blocks if b.tag == W + 'p' and text_of(b).strip().startswith('B. CAM KẾT'))
    j = blocks.index(head)
    pledge = blocks[j + 1]
    sign = blocks[j + 2]
    assert pledge.tag == W + 'p' and sign.tag == W + 'tbl'
    for p in (head, pledge):
        child(ppr(p), 'keepNext', PPR_ORDER)
    rows = sign.findall(W + 'tr')
    for k, tr in enumerate(rows):
        trpr = tr.find(W + 'trPr')
        if trpr is None:
            trpr = etree.Element(W + 'trPr')
            tcs = tr.find(W + 'tblPrEx')
            tr.insert(1 if tcs is not None else 0, trpr)
        child(trpr, 'cantSplit', TRPR_ORDER)
        if k < len(rows) - 1:
            for p in tr.iter(W + 'p'):
                child(ppr(p), 'keepNext', PPR_ORDER)

    # Lề trên/dưới 1,5 cm.
    for mar in root.iter(W + 'pgMar'):
        for side in ('top', 'bottom'):
            if int(mar.get(W + side, '0')) > 851:
                mar.set(W + side, '851')


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
    process(os.path.join(sys.argv[1], FILE_09A), fix_09a)
