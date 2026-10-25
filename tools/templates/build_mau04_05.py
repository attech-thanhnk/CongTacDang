# -*- coding: utf-8 -*-
"""
Dựng template Mau_04_PhieuDeNghiDieuChinh.docx và Mau_05_PhieuNhiemVuDotXuat.docx từ đúng phần "Mẫu số 04", "Mẫu số 05" của file
biểu mẫu gốc HD03 (docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx): giữ nguyên gói (styles, numbering, theme…),
thân tài liệu chỉ gồm các khối của mẫu, gắn Content Control bao đúng đoạn chữ mặc định (so khớp nguyên văn, sai là dừng).
Cách chạy: py -3 build_mau04_05.py <file biểu mẫu gốc> <thư mục đích>
"""
import sys, zipfile, copy, os
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
W14 = '{http://schemas.microsoft.com/office/word/2010/wordml}'
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'

src_path, dst_dir = sys.argv[1], sys.argv[2]
src = zipfile.ZipFile(src_path)
src_xml = src.read('word/document.xml')


def text_of(el):
    return ''.join(t.text or '' for t in el.iter(W + 't'))


def el(tag, **attrs):
    e = etree.Element(W + tag)
    for k, v in attrs.items():
        e.set(W + k, v)
    return e


next_id = [904000]


def new_id():
    next_id[0] += 1
    return str(next_id[0])


def sdt(tag, children):
    s = el('sdt')
    pr = etree.SubElement(s, W + 'sdtPr')
    etree.SubElement(pr, W + 'alias').set(W + 'val', tag)
    etree.SubElement(pr, W + 'tag').set(W + 'val', tag)
    etree.SubElement(pr, W + 'id').set(W + 'val', new_id())
    content = etree.SubElement(s, W + 'sdtContent')
    for c in children:
        content.append(c)
    return s


def strip_ids(e):
    for x in e.iter():
        for a in list(x.attrib):
            if a.startswith(W14) or a.startswith(W + 'rsid'):
                del x.attrib[a]
    return e


def tokens(p):
    """Chuỗi ký tự của đoạn: (ký tự, rPr) — tab là '\t', ngắt dòng là '\n'."""
    out = []
    for r in p.findall(W + 'r'):
        rpr = r.find(W + 'rPr')
        for c in r:
            if c.tag == W + 't':
                for ch in (c.text or ''):
                    out.append((ch, rpr))
            elif c.tag == W + 'tab':
                out.append(('\t', rpr))
            elif c.tag == W + 'br':
                out.append(('\n', rpr))
            elif c.tag == W + 'rPr':
                pass
            else:
                raise SystemExit('Run có phần tử không hỗ trợ: ' + c.tag)
    return out


def make_runs(toks):
    runs = []
    cur = None
    cur_key = None
    for ch, rpr in toks:
        key = etree.tostring(rpr) if rpr is not None else b''
        if cur is None or key != cur_key:
            cur = el('r')
            if rpr is not None:
                cur.append(copy.deepcopy(rpr))
            runs.append(cur)
            cur_key = key
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


def tag_paragraph(p, specs):
    """specs: danh sách (đoạn chữ mặc định, tag[, chuỗi đứng trước]) theo thứ tự xuất hiện."""
    toks = tokens(p)
    text = ''.join(ch for ch, _ in toks)
    pos = 0
    spans = []
    for spec in specs:
        needle, tag = spec[0], spec[1]
        if len(spec) > 2:
            before = text.find(spec[2], pos)
            if before < 0:
                raise SystemExit(f'Không tìm thấy "{spec[2]}" trong đoạn "{text}"')
            pos = before + len(spec[2])
        i = text.find(needle, pos)
        if i < 0:
            raise SystemExit(f'Không tìm thấy "{needle}" trong đoạn "{text}"')
        spans.append((i, i + len(needle), tag))
        pos = i + len(needle)
    ppr = p.find(W + 'pPr')
    for c in list(p):
        if c is not ppr:
            p.remove(c)
    cursor = 0
    for a, b, tag in spans:
        if a > cursor:
            for r in make_runs(toks[cursor:a]):
                p.append(r)
        p.append(sdt(tag, make_runs(toks[a:b])))
        cursor = b
    if cursor < len(toks):
        for r in make_runs(toks[cursor:]):
            p.append(r)
    return p


def add_sign(cell, tag):
    """Thêm khoảng ký; có tag thì thêm dòng họ tên (ô tên điền theo dữ liệu)."""
    last = cell.findall(W + 'p')[-1]
    ppr = copy.deepcopy(last.find(W + 'pPr'))
    for _ in range(5):
        blank = el('p')
        blank.append(copy.deepcopy(ppr))
        cell.append(blank)
    if tag is None:
        return
    name_rpr = el('rPr')
    name_rpr.append(el('b'))
    name_rpr.append(el('sz', val='26'))
    name_rpr.append(el('szCs', val='26'))
    p = el('p')
    p.append(copy.deepcopy(ppr))
    old = p.find(W + 'pPr/' + W + 'rPr')
    if old is not None:
        p.find(W + 'pPr').remove(old)
    p.find(W + 'pPr').append(copy.deepcopy(name_rpr))
    r = el('r')
    r.append(copy.deepcopy(name_rpr))
    t = etree.SubElement(r, W + 't')
    t.set(XML_SPACE, 'preserve')
    t.text = ' '
    p.append(sdt(tag, [r]))
    cell.append(p)


def build(label, end_label, dst_name, tag_fn):
    root = etree.fromstring(src_xml)
    body = root.find(W + 'body')
    els = list(body)
    start = next(i for i, e in enumerate(els) if e.tag == W + 'p' and text_of(e).replace(' ', '') == label)
    if end_label:
        end = next(i for i in range(start + 1, len(els)) if els[i].tag == W + 'p' and text_of(els[i]).replace(' ', '') == end_label)
    else:
        end = next(i for i in range(start, len(els)) if els[i].tag == W + 'p' and els[i].find('.//' + W + 'sectPr') is not None) + 1
    part = [copy.deepcopy(e) for e in els[start:end]]
    # Khối này nằm chung một phần (section) với các mẫu liền kề: lấy sectPr của phần đó.
    sect_src = next(e for e in els[start:] if e.tag == W + 'p' and e.find('.//' + W + 'sectPr') is not None)
    sect = copy.deepcopy(sect_src.find('.//' + W + 'sectPr'))
    for e in part:
        ppr = e.find(W + 'pPr') if e.tag == W + 'p' else None
        if ppr is not None and ppr.find(W + 'sectPr') is not None:
            ppr.remove(ppr.find(W + 'sectPr'))
    while part and part[-1].tag == W + 'p' and not text_of(part[-1]).strip():
        part.pop()

    tag_fn(part)

    for c in list(body):
        body.remove(c)
    for e in part:
        body.append(strip_ids(e))
    body.append(sect)
    xml = etree.tostring(root, xml_declaration=True, encoding='UTF-8', standalone=True)
    dst = os.path.join(dst_dir, dst_name)
    # writestr đổi thông tin mục của zip nguồn, nên mở lại nguồn cho mỗi lần dựng.
    with zipfile.ZipFile(src_path) as fresh, zipfile.ZipFile(dst, 'w', zipfile.ZIP_DEFLATED) as out:
        for info in fresh.infolist():
            data = xml if info.filename == 'word/document.xml' else fresh.read(info.filename)
            out.writestr(info, data)
    print('OK', dst)


def find_p(part, pred):
    for e in part:
        if e.tag == W + 'p' and pred(text_of(e)):
            return e
    raise SystemExit('Không tìm thấy đoạn')


def header_tags(part):
    tbl = part[1]
    assert tbl.tag == W + 'tbl'
    ps = list(tbl.iter(W + 'p'))
    tag_paragraph(ps[0], [('CƠ QUAN QUẢN LÝ CẤP TRÊN', 'ORG_PARENT_COMPANY_NAME_UPPER'), ('TÊN CƠ QUAN / ĐƠN VỊ', 'ORG_COMPANY_NAME_UPPER')])
    tag_paragraph(ps[1], [('……', 'ORG_LOCATION'), ('ngày …… tháng …… năm 20…', 'DATE_LINE')])


def info_lines(part, lines):
    tag_paragraph(find_p(part, lambda t: t.startswith('Kỳ đánh giá:')), [('……', 'QUARTER'), ('20…', 'YEAR')])
    for prefix, specs in lines:
        tag_paragraph(find_p(part, lambda t, pr=prefix: t.startswith(pr)), specs)


def cell_para(tbl, row, col=1):
    return tbl.findall(W + 'tr')[row].findall(W + 'tc')[col].findall(W + 'p')[0]


def sign_blocks(tbl, row, tags):
    cells = tbl.findall(W + 'tr')[row].findall(W + 'tc')
    assert len(cells) == len(tags)
    for cell, tag in zip(cells, tags):
        add_sign(cell, tag)


def tag_mau04(part):
    header_tags(part)
    info_lines(part, [
        ('Họ và tên cán bộ:', [('………………………………………………………', 'FULL_NAME'), ('…………………………………………', 'POSITION')]),
        ('Đơn vị công tác:', [('…………………………………………………………', 'DEPARTMENT')]),
    ])
    tbl = next(e for e in part if e.tag == W + 'tbl' and 'Thông tin đề nghị' in text_of(e))
    tag_paragraph(cell_para(tbl, 1), [('[Ghi rõ tên nhiệm vụ, mã SP trong Mẫu số 01]', 'AFFECTED_TASK')])
    tag_paragraph(cell_para(tbl, 2), [('☐', 'BOX_ADJUST'), ('☐', 'BOX_CARRYOVER'), ('☐', 'BOX_EXCLUDE'), ('☐', 'BOX_WEIGHT')])
    tag_paragraph(cell_para(tbl, 3), [('[Mô tả mục tiêu, thời hạn, trọng số ban đầu]', 'CONTENT_BEFORE')])
    tag_paragraph(cell_para(tbl, 4), [('[Mô tả mục tiêu, thời hạn, trọng số mới đề xuất]', 'CONTENT_AFTER')])
    detail = '…………………………………………………………………………………………'
    tag_paragraph(cell_para(tbl, 5), [(detail, 'REASON_DETAIL', 'Chi tiết: ')])
    tag_paragraph(cell_para(tbl, 6), [('[Số văn bản cảnh báo, báo cáo giao ban, nhật ký kỹ thuật...]', 'EVIDENCE')])
    tag_paragraph(cell_para(tbl, 7), [('[Bảo đảm tổng trọng số nhóm nhiệm vụ sau điều chỉnh vẫn bằng 70 điểm]', 'IMPACT')])
    sign_blocks(tbl, 8, ['APPROVER_SIGN', None, None, 'REQUESTER_SIGN'])


def tag_mau05(part):
    header_tags(part)
    info_lines(part, [
        ('Họ và tên cán bộ được giao:', [('…………………………………………', 'FULL_NAME'), ('……………………………………', 'POSITION')]),
        ('Đơn vị công tác:', [('…………………………………………………………', 'DEPARTMENT')]),
    ])
    tbl = next(e for e in part if e.tag == W + 'tbl' and 'Thông tin nhiệm vụ đột xuất' in text_of(e))
    dots = '……………………………………………………………………………………………………'
    tag_paragraph(cell_para(tbl, 1), [('[Lãnh đạo Tổng công ty / Thủ trưởng cơ quan, đơn vị]', 'ASSIGNER')])
    tag_paragraph(cell_para(tbl, 2), [('[Văn bản chỉ đạo / Thông báo kết luận / Lệnh khẩn nguy số ...]', 'BASIS')])
    tag_paragraph(cell_para(tbl, 3), [('Ngày …… tháng …… năm 20…', 'ASSIGNED_DATE')])
    tag_paragraph(cell_para(tbl, 4), [(dots, 'TASK_NAME')])
    tag_paragraph(cell_para(tbl, 5), [('[Mô tả rõ sản phẩm hoàn thành, tiêu chuẩn nghiệm thu]', 'OUTPUT')])
    tag_paragraph(cell_para(tbl, 6), [('Ngày …… tháng …… năm 20…', 'DEADLINE')])
    tag_paragraph(cell_para(tbl, 7), [(dots, 'COORDINATION')])
    tag_paragraph(cell_para(tbl, 8), [('☐', 'BOX_REBALANCE', 'Thay thế nhiệm vụ số … trong Mẫu số 01'), ('……', 'NEW_WEIGHT', 'gán trọng số mới: ')])
    sign_blocks(tbl, 9, [None, None, None, 'ASSIGNEE_SIGN'])


build('Mẫusố04', 'Mẫusố05', 'Mau_04_PhieuDeNghiDieuChinh.docx', tag_mau04)
build('Mẫusố05', None, 'Mau_05_PhieuNhiemVuDotXuat.docx', tag_mau05)
