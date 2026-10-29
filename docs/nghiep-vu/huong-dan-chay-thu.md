# Hướng dẫn chạy thử một kỳ đánh giá

Dành cho người chạy thử trên máy dev. Mỗi người chỉ làm phần của mình. Làm đúng thứ tự các bước.

- Địa chỉ: `http://localhost:3001` (máy dev) hoặc địa chỉ máy được giao.
- Mật khẩu: do người phụ trách báo riêng. Lần đầu có thể phải đổi mật khẩu.
- Cách làm việc chung: đăng nhập, vào **Việc cần xử lý**. Hồ sơ nào đang chờ bạn sẽ nằm ở đó, kèm nút việc cần làm (**Đăng ký**, **Duyệt**, **Tự chấm**, **Xác nhận**, **Đề xuất**, **Thẩm định**, **Nhận xét**, **Quyết định**, **Công bố**). Bấm nút là mở đúng hồ sơ.
- Mỗi bước xong, ghi lại: làm được không, có chỗ nào khó hiểu, thông báo lỗi nói gì.

## Ai làm gì

| Người | Tên đăng nhập | Việc |
|---|---|---|
| Trưởng phòng TCCB-LĐ (Cơ quan thẩm định) | `loan.ttp` | Tạo kỳ, thêm người, mở kỳ, thẩm định, lập biên bản họp thống nhất (Mẫu 20) |
| Người được đánh giá (ví dụ Nguyễn Bình Trung) | `trung.nb` | Đăng ký danh mục sản phẩm, tự chấm |
| Giám đốc (Lãnh đạo đơn vị, Cấp trực tiếp sử dụng) | `giang.nh` | Duyệt danh mục, nhận xét |
| Chi bộ | tài khoản Bí thư hoặc Phó Bí thư chi bộ của người được đánh giá | Xác nhận phiếu tự chấm |
| Thư ký hội nghị | `long.nc` hoặc `thao.nt` | Ghi kết quả họp tập thể lãnh đạo |
| Văn phòng Đảng ủy | `hung.dt` | Ghi nhận quyết định, công bố |

## Bước 1. Tạo kỳ mới và thêm người (`loan.ttp`)

1. Vào **Kỳ đánh giá**, bấm **Tạo kỳ đánh giá**. Chọn năm 2027 và một quý chưa có kỳ. Kỳ cũ đang có mang cấu hình cũ nên không dùng để thử hạn mới.
2. Mở kỳ vừa tạo. Ở thẻ **Diện quyết định**, xem hạn từng bước (đăng ký và duyệt 5 ngày làm việc đầu quý, tự chấm ngày 9 tháng cuối quý…).
3. Ở thẻ người được đánh giá, chọn một Phòng rồi bấm **Thêm cả đơn vị**.
4. Xem khối **Chuẩn bị mở kỳ**. Bấm **Kiểm tra lại**. Phải báo "Mọi bước đều có người thực hiện". Nếu có bước chưa có người, ghi lại tên bước.
5. Bấm **Mở kỳ**.

## Bước 2. Đăng ký và duyệt danh mục

1. `trung.nb`: vào **Việc cần xử lý**, bấm **Đăng ký**, nhập 3 đến 7 sản phẩm rồi nộp.
2. `giang.nh`: bấm **Duyệt** ở hồ sơ của ông Trung. Thử cả **Trả lại** (phải nhập lý do) rồi cho `trung.nb` sửa và nộp lại.
3. **Thử mục "Ý kiến trước phê duyệt":** `loan.ttp` mở hồ sơ ông Trung, bấm **Ghi ý kiến**, chọn loại ý kiến, nhập nội dung, bấm **Lưu**. Rồi đăng nhập `trung.nb`: ông Trung **không được thấy** mục này.

## Bước 3. Tự chấm và Chi bộ xác nhận

1. `trung.nb`: bấm **Tự chấm**, điền điểm các tiêu chí và từng sản phẩm, nộp.
2. Chi bộ: Bí thư (hoặc Phó Bí thư) chi bộ nơi ông Trung sinh hoạt bấm **Xác nhận**. Thử **Trả lại** một lần.
3. Thử tải biểu mẫu Mẫu 09A từ hồ sơ. Kiểm tra có khung "Xác nhận của Chi bộ" để ký tay.

## Bước 4. Họp tập thể và đề xuất mức

1. Thư ký hội nghị (`long.nc` hoặc `thao.nt`): vào **Đánh giá tập thể**, thẻ **Biên bản hội nghị**, lập biên bản và nhập kết quả bỏ phiếu.
2. Cũng người đó: mở hồ sơ ông Trung, bấm **Đề xuất**, chọn mức, gắn biên bản vừa lập.
3. Vào **Đánh giá tập thể**, thẻ **Theo dõi**: xem tổ chức Đảng nào đã lập Mẫu 06, 07, 08 và tổ chức nào quá hạn.

## Bước 5. Thẩm định và nhận xét

1. `loan.ttp`: bấm **Thẩm định**, nhập điểm và nhận xét.
2. `giang.nh`: bấm **Nhận xét**, chọn mức đề xuất.

## Bước 6. Họp thống nhất (Mẫu 20) rồi quyết định

Mẫu 20 là bắt buộc: chưa có biên bản thì nút **Ghi nhận quyết định** bị khóa và nêu lý do.

1. Thử trước: `hung.dt` mở hồ sơ, thấy nút **Ghi nhận quyết định** bị khóa kèm lý do. Ghi lại lý do hiện ra.
2. `loan.ttp`: vào **Duyệt kết quả**, thẻ **Họp thống nhất**, bấm **Lập biên bản**, nhập tên nhóm, chọn hồ sơ, rồi chốt biên bản.
3. Người chốt biên bản phải **khác** người lập. Thử chốt bằng chính người lập, hệ thống phải từ chối.
4. `hung.dt`: nút **Ghi nhận quyết định** nay mở được. Ghi nhận quyết định.
5. Mẫu 19 (phiếu kiểm tra) chỉ ghi nhận: không lập cũng không bị khóa. Thử lập một phiếu nếu muốn.

## Bước 7. Công bố

1. `hung.dt`: bấm **Công bố**.
2. `trung.nb`: vào **Kết quả**, xem kết quả của mình. Thử gửi một kiến nghị.

## Những chỗ cần để ý và ghi lại

- Nút nào bị khóa mà không rõ lý do.
- Thông báo lỗi khó hiểu.
- Bước nào không có người nhận nên hồ sơ đứng lại.
- Hạn hiển thị có đúng không.
- Biểu mẫu Word xuất ra có đúng tên, đúng số liệu không.
- Trên điện thoại có xem và làm được không.

Ghi theo mẫu: *Bước nào, tài khoản nào, bấm gì, thấy gì, mong đợi gì.* Chụp màn hình nếu được.
