# -*- coding: utf-8 -*-
"""
Mẫu 10 (Phiếu thẩm định, nhận xét, đề xuất xếp loại và ghi nhận giải trình) — trình bày gọn khi đã điền dữ liệu:

- Dòng "Đơn vị công tác": bỏ phần "Kỳ đánh giá: Quý … / Năm …" lặp lại (đã có ở dòng đầu phiếu).
- "Ý kiến của Người trực tiếp sử dụng cán bộ", "Nội dung giải trình…", "Kết luận của Cơ quan thẩm định": dòng chấm đưa vào làm chữ
  mặc định của control — có nội dung thì không còn dòng chấm thừa phía sau.
- "(TC   /30đ)" → "(TC / 30đ)".
- Mức đề xuất: nhãn như biểu mẫu gốc "Mức xếp loại đề xuất trình cấp có thẩm quyền:" và đánh dấu ☒ vào ô mức (CHECK_EXCELLENT,
  CHECK_GOOD, CHECK_DONE, CHECK_FAILED) thay cho in chữ mức (bỏ PROPOSED_GRADE).

Chạy lại an toàn (bước nào đã làm thì bỏ qua). Chạy sau fix_form_tags.py.
Cách chạy: py -3 fix_mau10_layout.py <thư mục Templates/Word>
"""
import sys, os, copy, zipfile, shutil
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'
FILE_10 = 'Mau_10_PhieuThamDinh.docx'

GRADES = [('CHECK_EXCELLENT', 'Hoàn thành xuất sắc'), ('CHECK_GOOD', 'Hoàn thành tốt'),
          ('CHECK_DONE', 'Hoàn thành nhiệm vụ'), ('CHECK_FAILED', 'Không hoàn thành')]


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


def make_run(rpr, text):
    r = etree.Element(W + 'r')
    if rpr is not None:
        r.append(copy.deepcopy(rpr))
    t = etree.SubElement(r, W + 't')
    t.text = text
    t.set(XML_SPACE, 'preserve')
    return r


def make_sdt(ids, tag, rpr, placeholder):
    s = etree.Element(W + 'sdt')
    pr = etree.SubElement(s, W + 'sdtPr')
    etree.SubElement(pr, W + 'alias').set(W + 'val', tag)
    etree.SubElement(pr, W + 'tag').set(W + 'val', tag)
    etree.SubElement(pr, W + 'id').set(W + 'val', ids.take())
    etree.SubElement(s, W + 'sdtContent').append(make_run(rpr, placeholder))
    return s


def set_text(run, text):
    for t in run.findall(W + 't'):
        run.remove(t)
    t = etree.SubElement(run, W + 't')
    t.text = text
    t.set(XML_SPACE, 'preserve')


def drop_duplicate_period(root):
    dept = find_sdt(root, 'DEPARTMENT')
    changed = False
    nxt = dept.getnext()
    if nxt is not None and 'Kỳ đánh giá' in text_of(nxt):
        while dept.getnext() is not None:
            dept.getparent().remove(dept.getnext())
        changed = True
    return changed


def dots_into_controls(root):
    changed = False
    for tag in ('SUPERVISOR_COMMENT', 'APPRAISAL_EXPLANATION', 'APPRAISAL_COMMENT'):
        sdt = find_sdt(root, tag)
        nxt = sdt.getnext()
        if nxt is None or nxt.tag != W + 'r':
            continue
        dots = text_of(nxt)
        if not dots.strip() or dots.strip().strip('….') != '':
            continue
        run = next(sdt.find(W + 'sdtContent').iter(W + 'r'))
        set_text(run, text_of(run).strip() + '…………………………………………………………')
        nxt.getparent().remove(nxt)
        changed = True
    return changed


def fix_tc_label(root):
    for t in root.iter(W + 't'):
        if t.text and '(TC   /30đ)' in t.text:
            t.text = t.text.replace('(TC   /30đ)', '(TC / 30đ)')
            return True
    return False


def fix_grade(root, ids):
    if find_sdt(root, 'CHECK_EXCELLENT') is not None:
        return False
    grade = find_sdt(root, 'PROPOSED_GRADE')
    p = para_of(grade)
    label = next(r for r in p.findall(W + 'r') if 'Mức xếp loại đề xuất' in text_of(r))
    set_text(label, 'Mức xếp loại đề xuất trình cấp có thẩm quyền:')
    grade.getparent().remove(grade)
    boxes = next(q for q in root.iter(W + 'p') if '☐ Hoàn thành tốt' in text_of(q) and 'Không hoàn thành' in text_of(q))
    # Ô ☐ dùng font ký hiệu, chữ mức dùng font văn bản — giữ đúng định dạng từng loại run của biểu mẫu.
    runs = boxes.findall(W + 'r')
    box_rpr = next(r.find(W + 'rPr') for r in runs if '☐' in text_of(r))
    text_rpr = next(r.find(W + 'rPr') for r in runs if '☐' not in text_of(r) and text_of(r).strip())
    for r in runs:
        boxes.remove(r)
    for i, (tag, name) in enumerate(GRADES):
        boxes.append(make_sdt(ids, tag, box_rpr, '☐'))
        boxes.append(make_run(text_rpr, ' ' + name + ('      ' if i < len(GRADES) - 1 else '')))
    return True


def fix_10(root):
    ids = Ids(root)
    changed = drop_duplicate_period(root)
    changed |= dots_into_controls(root)
    changed |= fix_tc_label(root)
    changed |= fix_grade(root, ids)
    return changed


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
    process(os.path.join(sys.argv[1], FILE_10), fix_10)
