# -*- coding: utf-8 -*-
"""
Dựng template Mau_03_PhieuYKienChuyenMon.docx, Mau_19_PhieuKiemSoat.docx và Mau_20_BienBanHieuChuan.docx từ đúng phần
"Mẫu số 03", "Mẫu số 19", "Mẫu số 20" của file biểu mẫu gốc HD03 (docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx):
giữ nguyên gói (styles, numbering, theme…), thân tài liệu chỉ gồm các khối của mẫu, gắn Content Control bao đúng đoạn chữ
mặc định (so khớp nguyên văn, sai là dừng). Tên đơn vị, địa danh trong tiêu đề là tag ORG_*.
Cách chạy: py -3 build_mau03_19_20.py <file biểu mẫu gốc> <thư mục đích>
"""
import sys, zipfile, copy, os
from lxml import etree

WNS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
W = '{%s}' % WNS
W14 = '{http://schemas.microsoft.com/office/word/2010/wordml}'
XML_SPACE = '{http://www.w3.org/XML/1998/namespace}space'

src_path, dst_dir = sys.argv[1], sys.argv[2]
src_xml = zipfile.ZipFile(src_path).read('word/document.xml')

CHECKED = '☒'
UNCHECKED = '☐'
SYMBOL_FONT = 'Segoe UI Symbol'


def text_of(el):
    return ''.join(t.text or '' for t in el.iter(W + 't'))


def el(tag, **attrs):
    e = etree.Element(W + tag)
    for k, v in attrs.items():
        e.set(W + k, v)
    return e


next_id = [905000]


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


def symbol_rpr(rpr):
    """rPr có phông ký hiệu để ô tick hiện đúng."""
    out = copy.deepcopy(rpr) if rpr is not None else el('rPr')
    if out.find(W + 'rFonts') is None:
        fonts = el('rFonts')
        for a in ('ascii', 'hAnsi', 'cs'):
            fonts.set(W + a, SYMBOL_FONT)
        out.insert(0, fonts)
    return out


def box_sdts(name, rpr):
    """Ô tick: if:NAME → ☒, ifnot:NAME → ☐ (biểu mẫu gốc dùng cả □ và ☐)."""
    def run(ch):
        r = el('r')
        r.append(symbol_rpr(rpr))
        t = etree.SubElement(r, W + 't')
        t.text = ch
        return r
    return [sdt('if:' + name, [run(CHECKED)]), sdt('ifnot:' + name, [run(UNCHECKED)])]


def tag_paragraph(p, specs):
    """specs: danh sách (đoạn chữ mặc định, tag[, chuỗi đứng trước]) theo thứ tự xuất hiện.
    tag dạng ('box', TÊN) thay ký tự ô tick bằng cặp if:/ifnot:."""
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
        if isinstance(tag, tuple):
            for s in box_sdts(tag[1], toks[a][1]):
                p.append(s)
        else:
            p.append(sdt(tag, make_runs(toks[a:b])))
        cursor = b
    if cursor < len(toks):
        for r in make_runs(toks[cursor:]):
            p.append(r)
    return p


def tag_whole(p, tag):
    """Gắn tag cho toàn bộ chữ của đoạn (dòng chấm)."""
    tag_paragraph(p, [(text_of(p), tag)])


def blank_run(rpr):
    r = el('r')
    if rpr is not None:
        r.append(copy.deepcopy(rpr))
    t = etree.SubElement(r, W + 't')
    t.set(XML_SPACE, 'preserve')
    t.text = ''
    return r


def fill_empty(p, tag, size='20'):
    """Đoạn ô trống (chỉ có ngắt dòng): thay bằng control rỗng, cỡ chữ theo ô."""
    ppr = p.find(W + 'pPr')
    for c in list(p):
        if c is not ppr:
            p.remove(c)
    rpr = el('rPr')
    rpr.append(el('sz', val=size))
    p.append(sdt(tag, [blank_run(rpr)]))


def cell(tbl, row, col):
    return tbl.findall(W + 'tr')[row].findall(W + 'tc')[col]


def cell_p(tbl, row, col, index=0):
    return cell(tbl, row, col).findall(W + 'p')[index]


def repeat_row(tbl, row_index, name, drop_rows=()):
    """Bọc một dòng bảng trong repeat:NAME và bỏ các dòng mẫu thừa."""
    rows = tbl.findall(W + 'tr')
    target = rows[row_index]
    for i in drop_rows:
        tbl.remove(rows[i])
    pos = list(tbl).index(target)
    tbl.remove(target)
    tbl.insert(pos, sdt('repeat:' + name, [target]))


def add_name_block(c, tag, blanks):
    """Thêm khoảng ký rồi dòng họ tên (control) cuối ô."""
    last = c.findall(W + 'p')[-1]
    ppr = last.find(W + 'pPr')
    for _ in range(blanks):
        blank = el('p')
        if ppr is not None:
            blank.append(copy.deepcopy(ppr))
        c.append(blank)
    name_rpr = el('rPr')
    name_rpr.append(el('b'))
    name_rpr.append(el('sz', val='24'))
    name_rpr.append(el('szCs', val='24'))
    p = el('p')
    if ppr is not None:
        p.append(copy.deepcopy(ppr))
    r = el('r')
    r.append(copy.deepcopy(name_rpr))
    t = etree.SubElement(r, W + 't')
    t.set(XML_SPACE, 'preserve')
    t.text = ' '
    p.append(sdt(tag, [r]))
    c.append(p)


def build(label, end_label, dst_name, tag_fn):
    root = etree.fromstring(src_xml)
    body = root.find(W + 'body')
    els = list(body)
    start = next(i for i, e in enumerate(els) if e.tag == W + 'p' and text_of(e).replace(' ', '') == label)
    body_sect = body.find(W + 'sectPr')
    if end_label:
        end = next(i for i in range(start + 1, len(els)) if els[i].tag == W + 'p' and text_of(els[i]).replace(' ', '') == end_label)
    else:
        end = len(els) - 1 if els[-1] is body_sect else len(els)
    part = [copy.deepcopy(e) for e in els[start:end]]
    # Khổ giấy: sectPr của phần chứa mẫu (đoạn kết phần đầu tiên sau mẫu, không có thì sectPr cuối tài liệu).
    sect_src = next((e.find('.//' + W + 'sectPr') for e in els[start:]
                     if e.tag == W + 'p' and e.find('.//' + W + 'sectPr') is not None), body_sect)
    sect = copy.deepcopy(sect_src)
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


def add_draft_banner(part):
    """Dòng cảnh báo đầu văn bản, chỉ in khi phiếu/biên bản chưa được xác nhận (if:DRAFT)."""
    p = el('p')
    ppr = etree.SubElement(p, W + 'pPr')
    etree.SubElement(ppr, W + 'jc').set(W + 'val', 'center')
    rpr = el('rPr')
    rpr.append(el('b'))
    rpr.append(el('color', val='C00000'))
    rpr.append(el('sz', val='24'))
    r = el('r')
    r.append(rpr)
    t = etree.SubElement(r, W + 't')
    t.text = 'BẢN NHÁP — CHƯA XÁC NHẬN'
    p.append(r)
    part.insert(0, sdt('if:DRAFT', [p]))


def find_p(part, pred):
    for e in part:
        if e.tag == W + 'p' and pred(text_of(e)):
            return e
    raise SystemExit('Không tìm thấy đoạn')


def find_tbl(part, needle):
    for e in part:
        if e.tag == W + 'tbl' and needle in text_of(e):
            return e
    raise SystemExit('Không tìm thấy bảng ' + needle)


def set_default(p, text):
    """Chữ mặc định của control đầu tiên trong đoạn (không ghi cứng tên đơn vị)."""
    s = p.find('.//' + W + 'sdtContent')
    ts = list(s.iter(W + 't'))
    ts[0].text = text
    for t in ts[1:]:
        t.text = ''


def party_header(part):
    """Bảng tiêu đề Mẫu 19, 20: Đảng bộ cấp trên, Đảng bộ/Đảng ủy, địa danh – ngày."""
    tbl = part[1]
    assert tbl.tag == W + 'tbl'
    ps = list(tbl.iter(W + 'p'))
    assert text_of(ps[0]) == 'ĐẢNG BỘ BỘ XÂY DỰNG' and text_of(ps[1]) == 'ĐẢNG ỦY TCT QUẢN LÝ BAY VIỆT NAM', 'tiêu đề trái khác biểu mẫu gốc'
    tag_whole(ps[0], 'ORG_SUPERIOR_PARTY_NAME')
    set_default(ps[0], 'ĐẢNG BỘ…')
    tag_whole(ps[1], 'ORG_PARTY_NAME')
    set_default(ps[1], 'ĐẢNG ỦY…')
    tag_paragraph(ps[3], [('Hà Nội', 'ORG_LOCATION'), ('ngày …… tháng …… năm 20…', 'DATE_LINE')])
    set_default(ps[3], '……')


# --- Mẫu 03
def tag_mau03(part):
    tag_mau03_body(part)
    add_draft_banner(part)


def tag_mau03_body(part):
    tbl = part[1]
    assert tbl.tag == W + 'tbl'
    ps = list(tbl.iter(W + 'p'))
    tag_paragraph(ps[0], [('CƠ QUAN QUẢN LÝ CẤP TRÊN', 'ORG_PARENT_COMPANY_NAME_UPPER'), ('BAN CHUYÊN MÔN / ĐƠN VỊ ĐỐI SOÁT', 'EXPERT_UNIT_UPPER')])
    tag_paragraph(ps[1], [('……', 'ORG_LOCATION'), ('ngày …… tháng …… năm 20…', 'DATE_LINE')])

    tag_paragraph(find_p(part, lambda t: t.startswith('Kính gửi:')),
                  [('Ban Tổ chức cán bộ - Lao động / Ban Tổ chức Đảng ủy Tổng công ty', 'ADDRESSEE')])
    tag_paragraph(find_p(part, lambda t: t.startswith('Cơ quan/Đơn vị đối soát:')), [('……………………………………………………………………………………………………', 'EXPERT_UNIT')])
    tag_paragraph(find_p(part, lambda t: t.startswith('Họ tên cán bộ được đối soát:')), [('……………………………………', 'FULL_NAME')])
    tag_paragraph(find_p(part, lambda t: t.startswith('Chức vụ/Đơn vị:')), [('…………………………………', 'POSITION_DEPT')])
    tag_paragraph(find_p(part, lambda t: t.startswith('Lĩnh vực chuyên môn đối soát:')),
                  [('☐', ('box', f)) for f in ('FIELD_SAFETY_QUALITY', 'FIELD_CNS', 'FIELD_ATM', 'FIELD_PLANNING', 'FIELD_FINANCE', 'FIELD_LEGAL')])

    items = find_tbl(part, 'Tên chỉ tiêu/nhiệm vụ')
    # Dòng chữ ký trước (chỉ số dòng còn nguyên), thêm khoảng ký và họ tên người ký.
    sign = items.findall(W + 'tr')[3]
    add_name_block(sign.findall(W + 'tc')[0], 'LEADER_NAME', 5)
    add_name_block(sign.findall(W + 'tc')[1], 'REVIEWER_NAME', 5)
    # Khoảng ký đã nằm trong ô: bỏ bốn đoạn trống đứng riêng sau bảng.
    pos = list(part).index(items)
    while pos + 1 < len(part) and part[pos + 1].tag == W + 'p' and not text_of(part[pos + 1]).strip():
        part.pop(pos + 1)

    tag_paragraph(cell_p(items, 1, 0), [('1', 'I_STT')])
    tag_paragraph(cell_p(items, 1, 1), [('[Chỉ tiêu kỹ thuật / an toàn / tiến độ]', 'I_NAME')])
    tag_paragraph(cell_p(items, 1, 2), [('[Dữ liệu tự khai]', 'I_SELF')])
    tag_paragraph(cell_p(items, 1, 3), [('[Dữ liệu hệ thống xác nhận]', 'I_SOURCE')])
    result = cell_p(items, 1, 4)
    rpr = result.find(W + 'r').find(W + 'rPr')
    tag_paragraph(result, [('☐', ('box', 'I_PASS')), ('☐', ('box', 'I_EXCEED')), ('☐', ('box', 'I_DEFECT'))])
    ppr = result.find(W + 'pPr')
    inner = [c for c in result if c is not ppr]
    for c in inner:
        result.remove(c)
    result.append(sdt('ifnot:I_NA', inner))
    na_rpr = el('rPr')
    na_rpr.append(el('sz', val='18'))
    na_run = el('r')
    na_run.append(na_rpr)
    na_text = etree.SubElement(na_run, W + 't')
    na_text.text = 'K/AD'
    result.append(sdt('if:I_NA', [na_run]))
    tag_paragraph(cell_p(items, 1, 5), [('[0 / -...đ / + ghi nhận vượt chuẩn]', 'I_ADJUST')])
    tag_paragraph(cell_p(items, 1, 6), [('Báo cáo số ... ngày ... của Ban ...', 'I_BASIS')])
    repeat_row(items, 1, 'ITEMS', drop_rows=(2,))


# --- Mẫu 19
def tag_mau19(part):
    tag_mau19_body(part)
    add_draft_banner(part)


def tag_mau19_body(part):
    party_header(part)
    info = find_tbl(part, 'Cơ quan/đơn vị:')
    tag_whole(cell_p(info, 0, 1), 'UNIT_NAME')
    period = cell_p(info, 1, 1)
    period.remove(period.findall(W + 'r')[-1])  # đoạn chấm thừa sau "đến ngày"
    tag_paragraph(period, [('....../....../......', 'PERIOD_FROM'), ('....../....../......', 'PERIOD_TO')])
    tag_whole(cell_p(info, 2, 1), 'GROUP_NAME')
    tag_whole(cell_p(info, 3, 1), 'CONTROLLER_NAME')

    items = find_tbl(part, 'Nội dung kiểm soát')
    first = items.findall(W + 'tr')[1]
    tag_paragraph(cell_p(items, 1, 0), [('1', 'I_STT')])
    tag_paragraph(cell_p(items, 1, 1), [(text_of(cell_p(items, 1, 1)), 'I_TEXT')])
    tag_paragraph(cell_p(items, 1, 2), [('□', ('box', 'I_PASS'))])
    fail = cell_p(items, 1, 3)
    fail_rpr = fail.find(W + 'r').find(W + 'rPr')
    tag_paragraph(fail, [('□', ('box', 'I_FAIL'))])
    ppr = fail.find(W + 'pPr')
    inner = [c for c in fail if c is not ppr]
    for c in inner:
        fail.remove(c)
    fail.append(sdt('ifnot:I_NA', inner))
    na_run = el('r')
    na_run.append(symbol_rpr(fail_rpr))
    na_text = etree.SubElement(na_run, W + 't')
    na_text.set(XML_SPACE, 'preserve')
    na_text.text = CHECKED + ' K/AD'
    fail.append(sdt('if:I_NA', [na_run]))
    fill_empty(cell_p(items, 1, 4), 'I_EVIDENCE')
    repeat_row(items, 1, 'ITEMS', drop_rows=tuple(range(2, 11)))

    tag_paragraph(find_p(part, lambda t: t.startswith('Kết luận kiểm soát:')),
                  [('□', ('box', 'CONCLUSION_ELIGIBLE')), ('□', ('box', 'CONCLUSION_NEEDS_WORK'))])

    sign = find_tbl(part, 'Nội dung cần hoàn thiện')
    fill_empty(cell_p(sign, 0, 1), 'TO_COMPLETE')
    tag_paragraph(cell_p(sign, 1, 1), [('....../....../......', 'CONTROLLER_DATE')])
    tag_paragraph(cell_p(sign, 2, 1), [('....../....../......', 'CONFIRMER_DATE')])
    add_name_block(cell(sign, 1, 0), 'CONTROLLER_NAME', 2)
    add_name_block(cell(sign, 2, 0), 'CONFIRMER_NAME', 2)


# --- Mẫu 20
def tag_mau20(part):
    tag_mau20_body(part)
    add_draft_banner(part)


def tag_mau20_body(part):
    party_header(part)
    info = find_tbl(part, 'Thời gian, địa điểm:')
    for row, tag in ((0, 'MEETING_TIME_PLACE'), (1, 'CHAIR'), (2, 'ATTENDEES'), (3, 'SCOPE_TEXT')):
        tag_whole(cell_p(info, row, 1), tag)

    summary = find_tbl(part, 'Nhóm/mức xếp loại')
    for col, tag in enumerate(('S_GROUP', 'S_COUNT', 'S_PCT', 'S_DIFF', 'S_CONCLUSION')):
        fill_empty(cell_p(summary, 1, col), tag)
    # Biên bản đã chốt: thêm số liệu sau hiệu chuẩn ngay sau số liệu gốc, chữ in sẵn của ô giữ nguyên.
    for col, tag in ((1, 'S_COUNT_AFTER'), (2, 'S_PCT_AFTER')):
        cp = cell_p(summary, 1, col)
        rpr = el('rPr')
        rpr.append(el('sz', val='20'))

        def after_run(text, rpr=rpr):
            r = el('r')
            r.append(copy.deepcopy(rpr))
            t = etree.SubElement(r, W + 't')
            t.set(XML_SPACE, 'preserve')
            t.text = text
            return r

        cp.append(sdt('if:S_HAS_AFTER', [after_run(' (sau hiệu chuẩn: '), sdt(tag, [blank_run(rpr)]), after_run(')')]))
    repeat_row(summary, 1, 'SUMMARY', drop_rows=(2, 3, 4))

    cases = find_tbl(part, 'Họ tên/Mã hồ sơ')
    tag_paragraph(cell_p(cases, 1, 0), [('1', 'C_STT')])
    for col, tag in enumerate(('C_NAME', 'C_BEFORE', 'C_CLARIFY', 'C_AFTER', 'C_BASIS'), start=1):
        fill_empty(cell_p(cases, 1, col), tag)
    repeat_row(cases, 1, 'CASES', drop_rows=(2, 3, 4, 5))

    # Ghi chú thêm của người lập, in ngay dưới đoạn kết luận cố định.
    fixed = find_p(part, lambda t: t.startswith('Việc hiệu chuẩn chỉ nhằm'))
    extra = el('p')
    extra_ppr = fixed.find(W + 'pPr')
    if extra_ppr is not None:
        extra.append(copy.deepcopy(extra_ppr))
    first_run = fixed.find(W + 'r')
    extra_rpr = first_run.find(W + 'rPr') if first_run is not None else None
    extra.append(sdt('EXTRA_CONCLUSION', [blank_run(extra_rpr)]))
    part.insert(part.index(fixed) + 1, sdt('if:HAS_EXTRA_CONCLUSION', [extra]))

    sign = find_tbl(part, 'Chủ trì (ký, ghi rõ họ tên):')
    tag_whole(cell_p(sign, 0, 1), 'CHAIR_NAME')
    tag_whole(cell_p(sign, 1, 1), 'SECRETARY_NAME')
    members_cell = cell(sign, 2, 1)
    dotted = members_cell.findall(W + 'p')[1]
    template_p = el('p')
    dotted_ppr = dotted.find(W + 'pPr')
    if dotted_ppr is not None:
        template_p.append(copy.deepcopy(dotted_ppr))
    rpr = el('rPr')
    rpr.append(el('sz', val='20'))

    def run(text):
        r = el('r')
        r.append(copy.deepcopy(rpr))
        t = etree.SubElement(r, W + 't')
        t.set(XML_SPACE, 'preserve')
        t.text = text
        return r

    template_p.append(sdt('M_NAME', [blank_run(rpr)]))
    template_p.append(sdt('if:M_HAS_DISSENT', [run(', ý kiến bảo lưu: '), sdt('M_DISSENT', [blank_run(rpr)])]))
    template_p.append(sdt('ifnot:M_HAS_DISSENT', [run(', thống nhất')]))
    pos = list(members_cell).index(dotted)
    members_cell.remove(dotted)
    members_cell.insert(pos, sdt('repeat:MEMBERS', [template_p]))
    members_cell.insert(pos + 1, sdt('ifnot:HAS_MEMBERS', [dotted]))


build('Mẫusố03', 'Mẫusố04', 'Mau_03_PhieuYKienChuyenMon.docx', tag_mau03)
build('Mẫusố19', 'Mẫusố20', 'Mau_19_PhieuKiemSoat.docx', tag_mau19)
build('Mẫusố20', None, 'Mau_20_BienBanHieuChuan.docx', tag_mau20)
