# -*- coding: utf-8 -*-
"""Mẫu 12: control PERIOD_TEXT ở đoạn "… đã tổ chức Hội nghị … chất lượng cán bộ quý.... Cụ thể như sau:" bao cả dấu chấm câu
("...." = "..." chỗ điền + "."), nên bản xuất mất dấu chấm. Tách dấu chấm ra ngoài control. Chạy lại an toàn."""
import sys, zipfile, shutil, re
p = sys.argv[1]
src = zipfile.ZipFile(p)
x = src.read('word/document.xml').decode('utf8')
pattern = re.compile(r'(<w:tag w:val="PERIOD_TEXT"/>.{0,200}?<w:sdtContent>(<w:r>(?:<w:rPr>.*?</w:rPr>)?)<w:t(?: xml:space="preserve")?>)\.\.\.\.(</w:t></w:r></w:sdtContent></w:sdt>)(<w:r>(?:<w:rPr>.*?</w:rPr>)?<w:t xml:space="preserve">) Cụ thể như sau:', re.S)
m = pattern.search(x)
if not m:
    if 'Cụ thể như sau:' in x and '....</w:t>' not in x:
        print('Đã sửa'); sys.exit(0)
    raise SystemExit('Không tìm thấy control PERIOD_TEXT "...."')
x = x[:m.start()] + m.group(1) + '...' + m.group(3) + m.group(4) + '. Cụ thể như sau:' + x[m.end():]
tmp = p + '.tmp'
with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as out:
    for it in src.infolist():
        out.writestr(it, x.encode('utf8') if it.filename == 'word/document.xml' else src.read(it.filename))
src.close()
shutil.move(tmp, p)
print('OK', p)
