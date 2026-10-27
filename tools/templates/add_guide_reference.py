# -*- coding: utf-8 -*-
"""Gắn Content Control ORG_GUIDE_NO / ORG_GUIDE_DATE vào đoạn "… Hướng dẫn số …-HD/TVĐU ngày … của …" của các template
(Mẫu 07, 12, 13, 16, Quyết định). Giữ nguyên chữ in sẵn (dòng chấm) làm nội dung mặc định, chỉ bọc chỗ điền.
Cách chạy: py -3 -I -X utf8 add_guide_reference.py <thư mục Templates/Word>. Chạy lại an toàn (đã gắn thì bỏ qua)."""
import sys, os, re, zipfile, shutil, copy
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'
FILES = ['Mau_07_BaoCaoTuDanhGiaTapThe.docx', 'Mau_12_BienBanHoiNghi.docx', 'Mau_13_BienBanKiemPhieu.docx',
         'Mau_16_BaoCaoKetQuaDanhGia.docx', 'Mau_QD_QuyetDinhXepLoai.docx']
# Số: chỗ chấm trước "-HD/TVĐU"; ngày: chỗ điền sau "-HD/TVĐU ngày" tới trước " của".
PATTERN = re.compile(r'(?P<no>[….]+-HD/TVĐU) ?ngày ?(?P<date>[….\d/ ]*?[….\d/])(?= ?của)')


def run_text(r):
    return ''.join(t.text or '' for t in r.findall(W + 't'))


def split_run(par, run, offset):
    """Tách run tại vị trí offset (ký tự), trả về run đứng sau; run chỉ chứa w:t."""
    text = run_text(run)
    if offset <= 0 or offset >= len(text):
        return run
    tail = copy.deepcopy(run)
    for t in run.findall(W + 't'):
        run.remove(t)
    for t in tail.findall(W + 't'):
        tail.remove(t)
    for r, value in ((run, text[:offset]), (tail, text[offset:])):
        t = etree.SubElement(r, W + 't')
        t.text = value
        t.set(XML_SPACE, 'preserve')
    run.addnext(tail)
    return tail


def wrap(par, runs, tag, sdt_id):
    first = runs[0]
    keep = copy.deepcopy(first)
    for t in keep.findall(W + 't'):
        keep.remove(t)
    t = etree.SubElement(keep, W + 't')
    t.text = ''.join(run_text(r) for r in runs)
    t.set(XML_SPACE, 'preserve')
    sdt = etree.Element(W + 'sdt')
    pr = etree.SubElement(sdt, W + 'sdtPr')
    etree.SubElement(pr, W + 'alias').set(W + 'val', tag)
    etree.SubElement(pr, W + 'tag').set(W + 'val', tag)
    etree.SubElement(pr, W + 'id').set(W + 'val', str(sdt_id))
    etree.SubElement(sdt, W + 'sdtContent').append(keep)
    first.addprevious(sdt)
    for r in runs:
        r.getparent().remove(r)


def cut(par, start, end):
    """Các run bao đúng khoảng ký tự [start, end) của đoạn (sau khi tách ở hai đầu)."""
    for boundary in (start, end):
        pos = 0
        for r in list(par.findall(W + 'r')):
            n = len(run_text(r))
            if pos < boundary < pos + n:
                split_run(par, r, boundary - pos)
                break
            pos += n
    pos, picked = 0, []
    for r in par.findall(W + 'r'):
        n = len(run_text(r))
        if n and pos >= start and pos + n <= end:
            picked.append(r)
        pos += n
    return picked


def process(xml_bytes, base_id):
    root = etree.fromstring(xml_bytes)
    done = 0
    for par in root.iter(W + 'p'):
        # Chỉ xét run trực tiếp; đoạn đã gắn control thì run nằm trong w:sdt nên không khớp lại.
        runs = par.findall(W + 'r')
        if any(s.find(W + 'sdtPr/' + W + 'tag').get(W + 'val') in ('ORG_GUIDE_NO', 'ORG_GUIDE_DATE')
               for s in par.iter(W + 'sdt') if s.find(W + 'sdtPr/' + W + 'tag') is not None):
            continue
        text = ''.join(run_text(r) for r in runs)
        m = PATTERN.search(text)
        if not m:
            continue
        # Làm từ cuối về đầu để vị trí ký tự phía trước không đổi.
        for key, tag in (('date', 'ORG_GUIDE_DATE'), ('no', 'ORG_GUIDE_NO')):
            picked = cut(par, m.start(key), m.end(key))
            assert picked and ''.join(run_text(r) for r in picked) == m.group(key), (tag, m.group(key))
            wrap(par, picked, tag, base_id + done)
            done += 1
    return etree.tostring(root, xml_declaration=True, encoding='UTF-8', standalone=True), done


def ensure_spacing(xml_bytes):
    """Chữ in sẵn đôi khi liền "số…" / "ngày…"; có giá trị thì cần dấu cách trước chỗ điền."""
    root = etree.fromstring(xml_bytes)
    changed = False
    for sdt in root.iter(W + 'sdt'):
        tag = sdt.find(W + 'sdtPr/' + W + 'tag')
        if tag is None or tag.get(W + 'val') not in ('ORG_GUIDE_NO', 'ORG_GUIDE_DATE'):
            continue
        prev = sdt.getprevious()
        if prev is None or prev.tag != W + 'r':
            continue
        ts = prev.findall(W + 't')
        if ts and ts[-1].text and not ts[-1].text.endswith(' '):
            ts[-1].text += ' '
            ts[-1].set(XML_SPACE, 'preserve')
            changed = True
    return etree.tostring(root, xml_declaration=True, encoding='UTF-8', standalone=True), changed


def main():
    folder = sys.argv[1]
    for i, name in enumerate(FILES):
        path = os.path.join(folder, name)
        src = zipfile.ZipFile(path)
        xml, done = process(src.read('word/document.xml'), 914000 + i * 10)
        xml, spaced = ensure_spacing(xml)
        if done == 0 and not spaced:
            src.close()
            print('Bỏ qua (đã gắn hoặc không có dòng căn cứ):', name)
            continue
        tmp = path + '.tmp'
        with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as out:
            for it in src.infolist():
                out.writestr(it, xml if it.filename == 'word/document.xml' else src.read(it.filename))
        src.close()
        shutil.move(tmp, path)
        print('OK', name, done)


if __name__ == '__main__':
    main()
