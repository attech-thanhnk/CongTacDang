# Rà soát dòng chấm, chữ mẫu trên biểu mẫu in

Ngày 11/11/2026, CSDL `congtacdang_dev`, kỳ Quý I/2027 (54 hồ sơ đã công bố).

Cách làm: xuất mọi biểu mẫu Word và Excel rồi tìm dòng chấm (`…`, `....`), chữ mẫu trong ngoặc vuông, ngày tháng bỏ trống.

- Hồ sơ cá nhân:
  - diện cơ sở: Lê Mai Duy (Trưởng phòng), Nguyễn Thị Anh Đào và Nguyễn Tiến Hùng (Phó Trưởng phòng, có Mẫu 04/05);
  - diện cấp trên: Đỗ Công Đà;
  - diện Phó Giám đốc: Phan Quốc Hưng;
  - diện Trưởng đơn vị đề xuất: Nguyễn Tuấn Cường (có Mẫu 17).
- Mẫu tập thể, báo cáo: Đảng bộ Công ty, Đảng bộ bộ phận Trung tâm Bảo đảm kỹ thuật, Chi bộ Phòng Tài chính - Kế toán.

Phân loại:

- **a**: hệ thống đã có dữ liệu nhưng mẫu chưa lấy. Đã sửa.
- **b**: hệ thống chưa thu thập dữ liệu, hoặc dữ liệu thử chưa nhập. Chưa sửa, cần quyết định.
- **c**: cố ý để ghi tay. Giữ nguyên.

Không liệt kê các dòng "(Ký, ghi rõ họ tên)" và "(nếu có)" vì đó là chữ của mẫu gốc.

## Kết quả

| Mẫu | Ô / vị trí | Loại | Nguồn dữ liệu | Đã sửa? |
|---|---|---|---|---|
| 01 (Word, Excel) | "Người quản lý trực tiếp: … Chức vụ: …" | a | Trưởng đơn vị đã gửi danh mục. Nếu chủ hồ sơ tự gửi hoặc bước gửi tắt thì lấy người duyệt danh mục. Chức vụ lấy theo chức danh trên hồ sơ cán bộ. | Đã sửa: tag `MANAGER_NAME`, `MANAGER_POSITION` (script `tools/templates/fix_dong_cham.cs`) |
| 01 (Word, Excel) | Như trên, diện cấp trên (cấp trên duyệt bằng văn bản) | c | Không có người duyệt trong hệ thống | Giữ dòng chấm |
| 01 (Word, Excel) | "Mã số cán bộ: …" | b | Mã số cán bộ (mã nhân viên QLNS) trên hồ sơ cán bộ: sửa ở Quản trị → Tài khoản hoặc cột "Mã số cán bộ" khi nhập cán bộ. Dev đã điền từ danh sách QLNS (59/63 tài khoản). | Đã sửa: tag `EMPLOYEE_CODE`; chưa có mã → dòng chấm |
| 01 | Họ tên ở các ô ký: "Người trực tiếp sử dụng" (Trưởng phòng, Phó Giám đốc tự gửi); cả 3 ô, trừ ô cá nhân, ở diện cấp trên | c | Bước tắt, tự bỏ qua hoặc cấp trên thực hiện | Giữ |
| 02 (Word, Excel) | Họ tên ở ô ký "Cấp có thẩm quyền duyệt" | b | Người duyệt danh mục như Mẫu 01; cấp trên duyệt thì tên cơ quan cấp trên đã ghi nhận | Đã sửa: tag `APPROVER_NAME` (Word), ô tên (Excel) |
| 02 | Họ tên ở ô ký "Đại diện lãnh đạo đơn vị" khi bước gửi tắt | c | Không có người gửi | Giữ |
| 04 | "Hồ sơ, minh chứng kèm theo": in chữ hướng dẫn "[Số văn bản cảnh báo, báo cáo giao ban, nhật ký kỹ thuật...]" | a / b | Đã có tệp văn bản phê duyệt của cấp trên (`AttachmentId`), nay in tên tệp. Người đề nghị điều chỉnh / chuyển tiếp / loại trừ đính kèm được một tệp minh chứng khi gửi. | Đã sửa: in tên tệp (văn bản cấp trên hoặc minh chứng người đề nghị), không có thì để dòng chấm thay chữ hướng dẫn |
| 04, 05 | Họ tên ở ô ký "CQ tổ chức cán bộ", "Thủ trưởng đơn vị" | c | Luồng đề nghị không qua hai bước này | Giữ |
| 05 | "Đơn vị / Cá nhân phối hợp" | b | Ô nhập tùy chọn khi ghi việc đột xuất (lưu trong nội dung đề nghị) | Đã sửa: tag `COORDINATION`; trống → dòng chấm |
| 05 | "☐ Thay thế nhiệm vụ số …" | c | Phương án không chọn | Giữ |
| 09A, 09C, 9D (Word, Excel) | "Chức vụ đoàn thể: …" | b | Cán bộ không có chức vụ đoàn thể trong danh mục. Bố cục "Cán bộ phòng ban" (mặc định) chưa đặt "Chức vụ trống ghi là". Khởi tạo dữ liệu đặt "Không" cho bố cục mặc định "Cán bộ phòng ban" khi còn trống; máy chủ đặt ở Quản trị → Bố cục biểu mẫu. | Đã sửa (dev); máy chủ cần đặt qua giao diện |
| 09A, 09C | "Ngày sinh: …" (Đỗ Công Đà) | b | Hồ sơ cán bộ chưa nhập ngày sinh; dev còn 2/63 người | Chưa (nhập dữ liệu) |
| 10 (Word, Excel) | "Nội dung giải trình của cá nhân (khi chênh lệch ≥ 5 điểm): …" khi không phải giải trình | a | Điểm tự chấm, điểm thẩm định và ngưỡng giải trình của bộ tiêu chí | Đã sửa: in "Không có" khi đã thẩm định mà chênh lệch chưa tới ngưỡng. Tới ngưỡng mà chưa giải trình thì vẫn để dòng chấm. |
| 10 (Word, Excel) | Mục 5, cột "Cá nhân tự chấm": "……………" | b | Cán bộ chưa đánh dấu cam kết "không thuộc trường hợp bị khống chế" (`SelfDeclaresNoCap`; dev: 50/54 hồ sơ chưa đánh dấu). Đã tự chấm mà không đánh dấu → in "Không cam kết"; chưa tự chấm → dòng chấm. | Đã sửa |
| 10 (Word, Excel) | Họ tên ở ô ký "Cấp có thẩm quyền duyệt" | b | Người ký Quyết định (chủ trì hội nghị B4, chưa có thì Bí thư theo chức vụ); cấp trên quyết định → tên cơ quan cấp trên đã ghi nhận, chưa ghi thì "Ban Thường vụ &lt;Đảng ủy cấp trên&gt;" theo cài đặt đơn vị | Đã sửa: tag `APPROVER_NAME` |
| 08 (Word, Excel) | Nhóm 1–7: "- Nhiệm vụ 1: … - …" (Chi bộ TCKT, Đảng bộ bộ phận BĐKT) | b | Hồ sơ Mẫu 08 chưa nhập nội dung các nhóm này; đã có ô nhập | Chưa (nhập dữ liệu) |
| 11 (theo kỳ) | "Hà Nội, ngày…… tháng… … năm……" | c | Phiếu trắng in trước hội nghị. Xuất theo cuộc họp thì đã điền ngày họp. | Giữ |
| 11 | "Ý kiến khác (nếu có): …" | c | Người bỏ phiếu ghi | Giữ |
| 12 (B3a) | "Quy chế làm việc của … nhiệm kỳ …" | b | Biên bản họp tập thể lãnh đạo chưa ghi quy chế; đã có ô nhập | Chưa (nhập dữ liệu) |
| 12 (B3a) | Địa điểm, chủ trì, thư ký (kèm chức vụ), giờ kết thúc | b | Các cuộc họp B3a trong dev chưa nhập; đã có ô nhập trên biên bản | Chưa (nhập dữ liệu) |
| 12, 13 | Mục 3.2 "(1) Đồng chí … - …" | b | Chưa ghi người được cử dự (Mẫu 13: người duy nhất được ghi là chủ trì nên không in lại) | Chưa (nhập dữ liệu) |
| 16 | Họ tên ở ô ký Bí thư | a | Cấp Đảng bộ cơ sở: cùng người ký Quyết định (chủ trì hội nghị B4, chưa có thì Bí thư theo chức vụ). Chi bộ, Đảng bộ bộ phận: Bí thư theo chức vụ. | Đã sửa: bản nháp chưa ghi người ký thì in theo cách trên |
| 16 | "Số …..-BC/……" | c | Văn thư cấp số sau khi ký | Giữ |
| 16 | "Hà Nội, ngày … tháng … năm …" | b | Lấy theo lần lưu bản nháp Mẫu 16; dev chưa lưu bản nháp | Chưa (nhập dữ liệu) |
| 16 (Chi bộ, Đảng bộ bộ phận) | "Ngày…, … đã tổ chức Hội nghị" | b | Chỉ có gợi ý từ hội nghị B4. Tổ chức trực thuộc không có hội nghị B4 nên phải nhập trong bản nháp. | Chưa |
| 16 | Nơi nhận "- …", "- Lưu ..." | b | Mặc định theo cấp: Đảng bộ cơ sở → Ban Thường vụ Đảng ủy cấp trên; Các Ủy viên BCH; Lưu Văn phòng Đảng ủy. Chi bộ, Đảng bộ bộ phận → Đảng ủy cơ sở; Các Chi ủy viên / Các Ủy viên BCH; Lưu. | Đã sửa: tag `RECEIVERS` thay `MEMBERS_RECEIVER` |
| QĐ | "Số …..-QĐ/……", ngày ở trang 1 và Phụ lục | c | Văn thư cấp số. Nếu mọi hồ sơ cùng một số thì đã in. Dev đang có hai số khác nhau (01-QĐ/ĐU, 15-QĐ/ĐU) nên để dòng chấm. | Giữ |

Các mẫu 03, 06, 07, 09C (phần nội dung), 13 (số liệu), 14, 15A, 15B, 17, 19, 20 không còn dòng chấm hay chữ mẫu, trừ dòng ký.

## Ghi chú

- Người ký cấp Đảng bộ dùng chung một cách tìm cho QĐ, Mẫu 16 và ô duyệt Mẫu 10: chủ trì hội nghị B4 (dev: Nguyễn Hoàng Giang), chưa có hội nghị thì Bí thư theo chức vụ.
- Hồ sơ Nguyễn Tiến Hùng ghi người gửi danh mục là Giám đốc, nên Mẫu 01 in người quản lý trực tiếp là Giám đốc.
- Mẫu 06, 07, 08 in họ tên dưới ô ký Bí thư (`SECRETARY_NAME`, Bí thư theo chức vụ của tổ chức Đảng). Mẫu 06 in thêm họ tên dưới ô Thủ trưởng đơn vị (`HEAD_SIGN_NAME`: người đứng đầu ghi trong báo cáo, chưa ghi thì người giữ chức vụ đứng đầu đơn vị chính quyền). Không tìm được thì để trống như trước.
