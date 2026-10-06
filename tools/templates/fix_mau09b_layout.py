# -*- coding: utf-8 -*-
"""
Trình bày Mẫu 09B (Phiếu tự chấm theo trục) như phiếu đã nộp trong docs/tai-lieu-chuan/ và thêm ô nhận xét theo nhóm tiêu chí
chung cho Mẫu 09A/09B:

- Mẫu 09B: bỏ dòng ghi chú soạn thảo tô vàng "(Mẫu này áp dụng cho kỳ … Quý III/2026)"; "Ngày sinh:" dùng control DATE_OF_BIRTH
  (chưa nhập → dòng chấm); chức vụ tách 3 dòng "Chức vụ: Chức vụ Đảng: …" / "Chức vụ chính quyền: …" / "Chức vụ đoàn thể: …"
  (PARTY_POSITION, ADMIN_POSITION, MASS_POSITION thay POSITIONS); "Đơn vị công tác: <phòng>, <ORG_COMPANY_NAME>"; nới cột
  "Điểm tối đa" của bảng nhóm tiêu chí chung (chữ tiêu đề cột không bị gãy).
- Mẫu 09A, 09B: ô cuối của dòng nhóm (repeat:GROUPS) có control G_NOTE — nhận xét chung của nhóm, định dạng như ô ghi chú của
  tiêu chí con (I_NOTE).

Chạy lại an toàn (bước nào đã làm thì bỏ qua). Chạy sau fix_form_tags.py.
Cách chạy: py -3 fix_mau09b_layout.py <thư mục Templates/Word>
"""
import sys, os, copy, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'

FILE_09A = 'Mau_09A_PhieuTuCham.docx'
FILE_09B = 'Mau_09B_PhieuTuChamTheoTruc.docx'

# Độ rộng nhãn "Chức vụ: " ở cỡ chữ 12 (twip) — hai dòng sau thẳng hàng với "Chức vụ Đảng:".
POSITION_LABEL = 935
# Cột "Điểm tối đa" của bảng nhóm tiêu chí chung: lấy bớt từ cột "Nội dung tiêu chí".
MAX_COL_OLD, MAX_COL_NEW = '630', '780'
TEXT_COL_OLD, TEXT_COL_NEW = '8370', '8220'


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


def make_run(rpr_source, text):
    r = etree.Element(W + 'r')
    if rpr_source is not None:
        r.append(copy.deepcopy(rpr_source))
    t = etree.SubElement(r, W + 't')
    t.text = text
    t.set(XML_SPACE, 'preserve')
    return r


def make_sdt(ids, tag, rpr_source, placeholder):
    s = etree.Element(W + 'sdt')
    pr = etree.SubElement(s, W + 'sdtPr')
    etree.SubElement(pr, W + 'alias').set(W + 'val', tag)
    etree.SubElement(pr, W + 'tag').set(W + 'val', tag)
    etree.SubElement(pr, W + 'id').set(W + 'val', ids.take())
    c = etree.SubElement(s, W + 'sdtContent')
    c.append(make_run(rpr_source, placeholder))
    return s


def first_rpr(e):
    r = next(e.iter(W + 'r'), None)
    return r.find(W + 'rPr') if r is not None else None


def set_ind(p, **attrs):
    ppr = p.find(W + 'pPr')
    ind = ppr.find(W + 'ind')
    if ind is None:
        ind = etree.Element(W + 'ind')
        jc = ppr.find(W + 'jc')
        (jc.addprevious(ind) if jc is not None else ppr.insert(len(ppr) - (1 if ppr.find(W + 'rPr') is not None else 0), ind))
    for k, v in attrs.items():
        ind.set(W + k, str(v))


# ---------------------------------------------------------------- Mẫu 09B
def remove_drafting_note(root):
    for p in list(root.iter(W + 'p')):
        if 'Mẫu này áp dụng cho kỳ' in text_of(p):
            p.getparent().remove(p)
            return True
    return False


def fix_birth_date(root, ids):
    if find_sdt(root, 'DATE_OF_BIRTH') is not None:
        return False
    p = para_of(find_sdt(root, 'FULL_NAME'))
    for r in p.findall(W + 'r'):
        if 'Ngày sinh' in text_of(r):
            rpr = r.find(W + 'rPr')
            for t in r.findall(W + 't'):
                r.remove(t)
            t = etree.SubElement(r, W + 't')
            t.text = '          Ngày sinh: '
            t.set(XML_SPACE, 'preserve')
            r.addnext(make_sdt(ids, 'DATE_OF_BIRTH', rpr, '……………………………'))
            return True
    raise SystemExit('Không thấy "Ngày sinh" ở dòng họ tên Mẫu 09B')


def fix_positions(root, ids):
    if find_sdt(root, 'PARTY_POSITION') is not None:
        return False
    old = find_sdt(root, 'POSITIONS')
    if old is None:
        raise SystemExit('Không thấy control POSITIONS ở Mẫu 09B')
    p = para_of(old)
    rpr = first_rpr(p)
    body = p.getparent()
    idx = body.index(p)
    lines = [
        (['Chức vụ: ', 'Chức vụ Đảng: '], 'PARTY_POSITION', True),
        (['Chức vụ chính quyền: '], 'ADMIN_POSITION', False),
        (['Chức vụ đoàn thể: '], 'MASS_POSITION', False),
    ]
    news = []
    for k, (labels, tag, first) in enumerate(lines):
        n = etree.Element(W + 'p')
        n.append(copy.deepcopy(p.find(W + 'pPr')))
        if not first:
            sp = n.find(W + 'pPr/' + W + 'spacing')
            if sp is not None and sp.get(W + 'before') is not None:
                del sp.attrib[W + 'before']
        for label in labels:
            n.append(make_run(rpr, label))
        n.append(make_sdt(ids, tag, rpr, '…'))
        if first:
            set_ind(n, left=POSITION_LABEL, hanging=POSITION_LABEL)
        else:
            set_ind(n, left=POSITION_LABEL)
        news.append(n)
    body.remove(p)
    for i, n in enumerate(news):
        body.insert(idx + i, n)
    return True


def fix_department(root, ids):
    if find_sdt(root, 'ORG_COMPANY_NAME') is not None:
        return False
    dept = find_sdt(root, 'DEPARTMENT')
    rpr = first_rpr(dept)
    dept.addnext(make_run(rpr, ', '))
    dept.getnext().addnext(make_sdt(ids, 'ORG_COMPANY_NAME', rpr, '…'))
    return True


def widen_max_column(root):
    groups = find_sdt(root, 'repeat:GROUPS')
    tbl = groups
    while tbl is not None and tbl.tag != W + 'tbl':
        tbl = tbl.getparent()
    changed = False
    grid = tbl.find(W + 'tblGrid').findall(W + 'gridCol')
    for col in grid:
        if col.get(W + 'w') == MAX_COL_OLD:
            col.set(W + 'w', MAX_COL_NEW)
            changed = True
        elif col.get(W + 'w') == TEXT_COL_OLD:
            col.set(W + 'w', TEXT_COL_NEW)
            changed = True
    for tcw in tbl.iter(W + 'tcW'):
        if tcw.get(W + 'w') == MAX_COL_OLD:
            tcw.set(W + 'w', MAX_COL_NEW)
            changed = True
        elif tcw.get(W + 'w') == TEXT_COL_OLD:
            tcw.set(W + 'w', TEXT_COL_NEW)
            changed = True
    return changed


# ---------------------------------------------------------------- Mẫu 09A, 09B
def add_group_note(root, ids):
    if find_sdt(root, 'G_NOTE') is not None:
        return False
    groups = find_sdt(root, 'repeat:GROUPS')
    row = groups.find(W + 'sdtContent/' + W + 'tr')
    cell = row.findall(W + 'tc')[-1]
    p = cell.findall(W + 'p')[-1]
    note = find_sdt(root, 'I_NOTE')
    rpr = first_rpr(note) if note is not None else None
    if rpr is None:
        rpr = first_rpr(find_sdt(root, 'G_NAME'))
        rpr = copy.deepcopy(rpr) if rpr is not None else None
        if rpr is not None:
            for b in rpr.findall(W + 'b') + rpr.findall(W + 'bCs'):
                rpr.remove(b)
    p.append(make_sdt(ids, 'G_NOTE', rpr, ' '))
    return True


def fix_09b(root):
    ids = Ids(root)
    changed = remove_drafting_note(root)
    changed |= fix_birth_date(root, ids)
    changed |= fix_positions(root, ids)
    changed |= fix_department(root, ids)
    changed |= widen_max_column(root)
    changed |= add_group_note(root, ids)
    return changed


def fix_09a(root):
    return add_group_note(root, Ids(root))


def process(path, step):
    with zipfile.ZipFile(path) as z:
        xml = z.read('word/document.xml')
    root = etree.fromstring(xml)
    if not step(root):
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
    folder = sys.argv[1]
    process(os.path.join(folder, FILE_09B), fix_09b)
    process(os.path.join(folder, FILE_09A), fix_09a)
