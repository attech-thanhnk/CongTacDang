# Hướng dẫn cho Quản trị

Áp dụng cho: cán bộ bộ phận Công nghệ thông tin được giao quản trị tài khoản, vai trò, người thực hiện, cán bộ và chức vụ, danh mục tổ chức, nhập dữ liệu, thông tin đơn vị, file mẫu Word.

Đăng nhập, đổi mật khẩu, màn hình chính: xem [Hướng dẫn chung](huong-dan-chung.md).

Mỗi lần gán vai trò gồm: người, vai trò, phạm vi, thời hạn. Phạm vi là **Toàn công ty**, một **Đơn vị chính quyền** hoặc một **Tổ chức Đảng**, bao trùm mọi đơn vị con. Việc trên hồ sơ giao cho người ở cấp gần hồ sơ nhất; người ở cấp trên (nhãn **Cấp trên**) thực hiện được khi người gần nhất vắng. Không tự gán vai trò cho chính mình.

## I. Việc cần làm theo thời điểm

| Thời điểm | Việc | Mục |
|---|---|---|
| Trước kỳ | Nhập thông tin đơn vị | II |
| | Cập nhật file mẫu Word | III |
| | Cập nhật danh mục tổ chức, chức vụ, ngày nghỉ | IV |
| | Nhập dữ liệu từ Excel (cán bộ, tài khoản) | V |
| | Tạo tài khoản, cấp mật khẩu tạm | VI, XIV |
| | Cập nhật thông tin cán bộ, chức vụ | VII, VIII |
| | Phân công vai trò, cán bộ giúp việc | IX, X |
| | Phân quyền cho vai trò (khi cần) | XI |
| | Kiểm tra người thực hiện các bước | XII |
| Trong kỳ | Theo dõi **Quản trị cần xử lý** ở **Tổng quan** | XIII |
| | Đặt lại mật khẩu, mở khóa đăng nhập | XIV, XV |
| | Bổ sung người thực hiện khi có cảnh báo, kiến nghị chưa có người xử lý | XII, XIII |
| | Tra cứu nhật ký hệ thống | XVI |
| Khi có thay đổi nhân sự | Tạo tài khoản cho người mới | VI |
| | Cập nhật đơn vị, nơi sinh hoạt Đảng | VII |
| | Kết thúc, thêm chức vụ | VIII |
| | Kết thúc, phân công lại vai trò | IX, X |
| | Kiểm tra lại người thực hiện các bước | XII |

## II. Thông tin đơn vị

**Mục đích:** khai báo tên đơn vị in trên mọi biểu mẫu, báo cáo.
**Thời điểm:** khi bắt đầu sử dụng; khi tên đơn vị, văn bản hướng dẫn thay đổi.
**Thực hiện:**
1. Menu **Hệ thống** → **Thông tin đơn vị**.
2. Nhập **Tên tổ chức Đảng cấp trên**, **Tên Đảng bộ**, **Tên đầy đủ của công ty**, **Tên đơn vị chủ quản cấp trên**, **Tên viết tắt**, **Địa danh**.
3. Nhập **Cơ quan thẩm định**, **Số văn bản hướng dẫn**, **Ngày văn bản hướng dẫn** → **Lưu**.

**Kết quả:** biểu mẫu, báo cáo xuất sau đó in thông tin mới.

## III. File mẫu Word

**Mục đích:** thay file mẫu Word dùng để xuất biểu mẫu.
**Thời điểm:** khi có bản mẫu mới.
**Thực hiện:**
1. Menu **Hệ thống** → **Biểu mẫu Word** → **Tải lên** ở dòng biểu mẫu.
2. Chọn file `.docx` → **Kiểm tra**.
3. Giữ tích **Kích hoạt ngay** nếu dùng ngay → **Tải lên**.

**Kết quả:** biểu mẫu xuất theo file mới (khi đã kích hoạt).
**Trường hợp khác:**
- Thiếu tag thì bị chặn; cảnh báo không chặn.
- Nút **⋯** ở dòng biểu mẫu: **Lịch sử phiên bản** (**Kích hoạt** để dùng lại bản cũ), **Danh mục tag**, **Tải file gốc**, **Dùng file gốc**.

## IV. Danh mục tổ chức

**Mục đích:** quản lý cây đơn vị chính quyền, tổ chức Đảng, loại đơn vị, chức vụ, ngày nghỉ.
**Thời điểm:** trước kỳ; khi có thay đổi tổ chức.
**Thực hiện:**
1. Menu **Danh mục tổ chức** (nhóm **Thiết lập**).
2. Chọn thẻ cần cập nhật (bảng dưới).
3. Thêm hoặc sửa → **Lưu**.

| Thẻ | Nội dung |
|---|---|
| **Đơn vị chính quyền**, **Tổ chức Đảng** | Cây nhiều cấp: **Thêm … gốc**, hoặc **⋯** → **Thêm … cấp dưới**; **Sửa** để đổi tên, đổi cấp trên |
| **Loại đơn vị** | Loại dùng cho hai cây trên |
| **Chức vụ** | **Thêm chức vụ**, **Sửa** (các ô chính nêu dưới) |
| **Kiểu trình bày biểu mẫu** | Cách in Mẫu 09C, 9D |
| **Ngày nghỉ** | Ngày lễ, Tết để tính hạn các bước |

**Kết quả:** danh mục mới dùng cho tài khoản, chức vụ, phân công và kỳ đánh giá.
**Trường hợp khác:** các ô chính của chức vụ:
- **Chức danh khi in**: dùng `{don_vi}` cho tên đơn vị (ví dụ `Trưởng {don_vi}`); để trống thì in tên chức vụ.
- **Mã chức danh** (M1–M26): dùng cho Mẫu 15A, 15B và trần Xuất sắc.
- **Thẩm quyền quyết định xếp loại mặc định**, **Diện quyết định khi thêm vào kỳ**.
- **Kiểu trình bày biểu mẫu tự đánh giá** (Mẫu 09C, 9D).

## V. Nhập dữ liệu từ Excel

**Mục đích:** nhập, cập nhật hàng loạt dữ liệu.
**Thời điểm:** khi bắt đầu sử dụng; khi có nhiều thay đổi cùng lúc.
**Thực hiện:**
1. Menu **Hệ thống** → **Nhập dữ liệu** → chọn loại dữ liệu → **Tải file mẫu**, điền theo mẫu.
2. Chọn file → **Kiểm tra file**.
3. Xem từng dòng: **Tạo mới**, **Cập nhật** hoặc **Lỗi**.
4. **Nhập dữ liệu** → **Nhập dữ liệu**.
5. Có tài khoản mới: bấm **Tải file mật khẩu tạm** ngay.

**Kết quả:** dữ liệu được tạo mới, cập nhật theo file.
**Trường hợp khác:**
- Còn dòng **Lỗi**: sửa file, kiểm tra lại.
- File mật khẩu tạm chỉ tải được một lần.

## VI. Tạo tài khoản

**Mục đích:** cấp tài khoản cho cán bộ mới.
**Thời điểm:** trước kỳ; khi có người mới.
**Thực hiện:**
1. Menu **Hệ thống** → **Tài khoản** → **Tạo tài khoản**.
2. Nhập **Tên đăng nhập**, **Họ và tên** và các thông tin cán bộ (mục VII) → **Tạo tài khoản**.
3. Bấm **Sao chép** ở hộp **Tên đăng nhập**, **Mật khẩu tạm**; gửi cho người dùng.


**Kết quả:** tài khoản mới; mật khẩu tạm chỉ hiển thị một lần; lần đăng nhập đầu người dùng phải đổi mật khẩu.
**Trường hợp khác:**
- Mở tài khoản có sẵn: nhập tên hoặc tên đăng nhập vào ô **Tìm kiếm** → **Lọc** → bấm tên cán bộ (hoặc **Chi tiết**). Trang gồm các thẻ **Thông tin** (có **Đăng nhập cuối**, **Khóa tạm đến**, **Số lần nhập sai liên tiếp**), **Chức vụ**, **Vai trò & phạm vi**, **Người này làm được gì**.
- Người thôi việc: mở tài khoản → **⋯** → **Vô hiệu hóa** → **Vô hiệu hóa**; mọi phiên đang dùng bị đăng xuất ngay.

## VII. Thông tin cán bộ

**Mục đích:** khai báo thông tin in trên biểu mẫu và nơi công tác, sinh hoạt Đảng.
**Thời điểm:** khi tạo tài khoản; khi cán bộ chuyển đơn vị, chuyển sinh hoạt Đảng.
**Thực hiện:**
1. Mở tài khoản → **Sửa**.
2. Nhập **Mã số cán bộ** (in trên biểu mẫu), **Đơn vị công tác chính**, **Nơi sinh hoạt Đảng**, **Khung tỷ trọng mặc định** → **Lưu**.

**Kết quả:** thông tin dùng cho kỳ đánh giá thêm người sau đó.
**Trường hợp khác:**
- **Thẩm quyền** (Đảng ủy cơ sở hay cấp trên quyết định) tự xác định theo chức vụ. Đặt khác: **Thay đổi** cạnh ô **Thẩm quyền** → chọn, ghi **Lý do** → **Lưu**; trả về: chọn **Theo chức vụ**.
- Thay đổi không tự đổi hồ sơ của kỳ đang chạy; Phòng TCCB-LĐ sửa trên trang kỳ nếu cần.

## VIII. Chức vụ của cán bộ

**Mục đích:** ghi chức vụ chính, kiêm nhiệm của cán bộ.
**Thời điểm:** trước kỳ; khi bổ nhiệm, thôi giữ chức vụ.
**Thực hiện:**
1. Mở tài khoản → thẻ **Chức vụ** → **Thêm chức vụ**.
2. Chọn chức vụ, **Nơi giữ chức vụ**, **Từ ngày**, **Đến hết ngày** (trống là không thời hạn).
3. Tích **Chức vụ chính (bỏ đánh dấu = kiêm nhiệm)** nếu là chức vụ chính → **Lưu**.

**Kết quả:** một người giữ được nhiều chức vụ; chức vụ chính in trước.
**Trường hợp khác:**
- Thôi giữ: **⋯** → **Kết thúc**. Nhập nhầm: **⋯** → **Xóa (nhập nhầm)**.
- Thay đổi chức vụ không tự đổi hồ sơ của kỳ đang chạy; Phòng TCCB-LĐ sửa trên trang kỳ nếu cần.

## IX. Phân công vai trò

**Mục đích:** giao quyền thực hiện cho cán bộ trong một phạm vi, thời hạn.
**Thời điểm:** trước kỳ; khi có thay đổi nhân sự.
**Thực hiện:**
1. Mở tài khoản → thẻ **Vai trò & phạm vi** → **Phân công vai trò**.
2. Chọn **Vai trò**, **Loại phạm vi** (**Toàn công ty**, đơn vị chính quyền, tổ chức Đảng), **Đối tượng phạm vi**.
3. Nhập **Hiệu lực từ ngày**, **Đến hết ngày** (trống là không hết hạn), **Ghi chú** → **Phân công**.


**Kết quả:** dòng mới trong **Phân công đang và sắp hiệu lực**; thẻ **Người này làm được gì** liệt kê quyền đang hiệu lực. Quyền bắt đầu từ **Hiệu lực từ ngày** và tự hết sau **Đến hết ngày**.
**Trường hợp khác:**
- Sửa thời hạn: **Sửa thời hạn, ghi chú** → **Lưu**.
- Thôi giao: **Kết thúc phân công** → **Kết thúc**. Gán nhầm: **Gỡ phân công** → **Gỡ**.

## X. Phân công cán bộ giúp việc

**Mục đích:** giao vai trò giúp việc đánh giá cho cán bộ Phòng TCCB-LĐ, Văn phòng Công ty.
**Thời điểm:** trước kỳ; khi chuyển việc.
**Thực hiện:**
1. Mở tài khoản → kiểm tra thẻ **Chức vụ** đã có đúng chức vụ.
2. **Phân công vai trò** → chọn vai trò theo bảng dưới; **Loại phạm vi**: **Toàn công ty**.
3. **Ghi chú**: văn bản hoặc người phân công → **Phân công**.

| Đối tượng (chức vụ) | Vai trò | Thực hiện được |
|---|---|---|
| Nhân viên Phòng TCCB-LĐ được Trưởng phòng phân công (**Nhân viên Nhân sự**) | **Cán bộ giúp việc đánh giá** | Rà soát danh mục, thẩm định, thư ký hội nghị tập thể lãnh đạo cấp Công ty, biên bản họp thống nhất, phiếu kiểm tra, xem hồ sơ, kết quả, xuất báo cáo |
| Cán bộ công tác Đảng ở Văn phòng (**Nhân viên công tác Đảng, đoàn thể và thi đua**) | **Cán bộ giúp việc Đảng ủy** | Như trên, trừ rà soát danh mục; thêm lập hồ sơ tập thể Mẫu 06, 07, 08 |

**Kết quả:** người được giao thấy việc tương ứng ở **Việc cần xử lý**. Cả hai vai trò không quản lý kỳ, bộ tiêu chí.
**Trường hợp khác:**
- Chuyển việc, thôi giao: **Kết thúc phân công** ở dòng vai trò.
- Quyền **Cập nhật danh mục sản phẩm theo kết quả cấp trên duyệt** có sẵn ở vai trò **Cơ quan thẩm định**, **Cán bộ giúp việc đánh giá**, **Cán bộ giúp việc Đảng ủy**, **Văn phòng Đảng ủy**: sửa danh mục của hồ sơ do cấp trên duyệt (Chủ tịch, Giám đốc, Kiểm soát viên), ghi điều chỉnh đã được cấp trên phê duyệt; không áp dụng cho hồ sơ của chính mình.

## XI. Phân quyền cho vai trò

**Mục đích:** thay đổi tập quyền của một vai trò.
**Thời điểm:** khi cần tạo vai trò mới hoặc điều chỉnh quyền.
**Thực hiện:**
1. Menu **Hệ thống** → **Vai trò**. Vai trò mới: **Tạo vai trò**.
2. **Phân quyền** ở dòng vai trò → tích, bỏ quyền → **Lưu quyền**.

**Kết quả:** có hiệu lực ngay với mọi người đang được gán vai trò.

## XII. Người thực hiện các bước

**Mục đích:** bảo đảm mỗi việc ở mỗi đơn vị đều có người thực hiện.
**Thời điểm:** trước khi Phòng TCCB-LĐ mở kỳ; khi có thay đổi nhân sự; khi có cảnh báo thiếu người.
**Thực hiện:**
1. Menu **Hệ thống** → **Người thực hiện**.
2. Ô **Xem theo**: chọn **Đơn vị chính quyền** hoặc **Tổ chức Đảng**, chọn đơn vị ở ô bên cạnh.
3. Xem việc theo hồ sơ của kỳ đang chọn (gồm cả **Gửi danh mục sản phẩm**, **Rà soát, trình duyệt danh mục**) và người thực hiện.
4. Việc **Chưa có người**: **Thêm người** → chọn **Người thực hiện** (có **Chọn nhanh** theo người giữ chức vụ), **Vai trò**, **Đến hết ngày**, **Ghi chú** → **Thêm**.


**Kết quả:** đủ người thì trang ghi **Mọi việc đều có người**.

| Nhãn | Ý nghĩa |
|---|---|
| Không nhãn | Người ở cấp gần nhất; việc hiện ở **Việc cần xử lý** của người đó |
| **Cấp trên** | Thực hiện được khi người gần nhất vắng |
| **Chưa có người** | Cần thêm người |

**Trường hợp khác:**
- Bỏ người: **⋯** cạnh tên → **Gỡ khỏi việc này** → **Gỡ**; chỉ gỡ được người gán tại chính đơn vị đang xem.
- Mỗi đơn vị tổ chức hội nghị tập thể lãnh đạo cần ít nhất một người lập được biên bản (thư ký phòng, trung tâm; cấp Công ty là **Cán bộ giúp việc đánh giá**). Thiếu thì Phòng TCCB-LĐ nhận cảnh báo khi chuẩn bị mở kỳ; gán vai trò **Thư ký hội nghị** cho người được cử.
- Chế độ **Toàn công ty** chỉ liệt kê người gán ở phạm vi này; nhãn **Chưa gán Toàn công ty** không có nghĩa kỳ thiếu người. Kiểm tra đủ người tại **Kiểm tra người thực hiện của kỳ**.

## XIII. Quản trị cần xử lý ở Tổng quan

**Mục đích:** phát hiện tài khoản, phân công, dữ liệu cán bộ cần xử lý.
**Thời điểm:** hằng ngày trong kỳ.
**Thực hiện:**
1. Mở **Tổng quan**, xem khối **Quản trị cần xử lý** (chỉ hiện mục có số khác 0).
2. Bấm một mục để mở trang **Tài khoản** đã lọc (ô **Cần xử lý**) hoặc thẻ **Tiến độ** của kỳ với danh sách bước thiếu người.
3. Xử lý theo mục tương ứng (VII, VIII, IX, XII, XV).

**Kết quả:** các mục được xử lý không còn trong khối.
**Trường hợp khác:** khối gồm các mục:
- tài khoản đang bị khóa, chưa đăng nhập lần nào;
- vai trò hết hạn trong 30 ngày tới;
- đảng viên chưa có tổ chức Đảng, người sinh hoạt tại tổ chức Đảng có tổ chức con, người tham gia đánh giá chưa có chức vụ;
- bước chưa có người thực hiện của kỳ đang mở (nếu có thêm quyền quản lý kỳ toàn công ty).

Ngoài ra:
- Người quản lý kỳ toàn công ty không có quyền **Quản lý tài khoản** chỉ thấy khối **Kỳ đánh giá cần xử lý** khi kỳ đang mở còn bước thiếu người.
- Dòng trên cùng ghi số tài khoản đăng nhập trong 7 ngày; người có quyền xem nhật ký thấy thêm **Hoạt động gần đây**.
- Nhóm **Kiến nghị chưa có người xử lý** (ở **Việc cần xử lý**): ngoài người liên quan không còn ai có quyền xử lý. Bấm **Gán vai trò** để thêm người ở trang **Người thực hiện**.

## XIV. Đặt lại mật khẩu tạm

**Mục đích:** cấp mật khẩu tạm khi người dùng quên mật khẩu.
**Thời điểm:** khi người dùng đề nghị.
**Thực hiện:**
1. Mở tài khoản → **⋯** ở đầu trang → **Đặt lại mật khẩu** → **Đặt lại**.
2. Bấm **Sao chép** ở hộp **Tên đăng nhập**, **Mật khẩu tạm**; gửi cho người dùng.


**Kết quả:** mật khẩu cũ hết hiệu lực, mọi phiên bị đăng xuất; lần đăng nhập kế tiếp người dùng phải đổi mật khẩu.
**Trường hợp khác:**
- Mật khẩu tạm chỉ hiển thị một lần; đóng hộp trước khi sao chép thì đặt lại lần nữa.

## XV. Mở khóa đăng nhập

**Mục đích:** cho người dùng đăng nhập lại ngay khi bị khóa.
**Thời điểm:** người dùng nhập sai mật khẩu 5 lần liên tiếp, bị khóa 15 phút.
**Thực hiện:**
1. Mở tài khoản → **Mở khóa đăng nhập** (chỉ hiện khi đang bị khóa hoặc đã nhập sai) → **Mở khóa**.


**Kết quả:** người dùng đăng nhập lại được.
**Trường hợp khác:**
- Người dùng không nhớ mật khẩu: đặt lại mật khẩu (mục XIV).

## XVI. Nhật ký và sao lưu

**Mục đích:** tra cứu thay đổi dữ liệu; bảo đảm có bản sao lưu.
**Thời điểm:** khi cần đối chiếu; trước mỗi lần nâng cấp.
**Thực hiện:**
1. Menu **Hệ thống** → **Nhật ký hệ thống**.
2. Lọc theo **Đối tượng**, **Thao tác**; xem người sửa, nội dung, thời gian.

**Kết quả:** biết ai đã thay đổi gì, khi nào.
**Trường hợp khác:**
- Sao lưu, khôi phục thực hiện trên máy chủ, không có trên giao diện, theo tài liệu vận hành; trước mỗi lần nâng cấp phải có bản sao lưu mới.

## XVII. Câu hỏi thường gặp

- **Không thấy nút Mở khóa đăng nhập?** Nút chỉ hiện khi tài khoản đang bị khóa hoặc đã nhập sai mật khẩu.
- **Không gán được vai trò cho chính mình?** Không ai tự gán vai trò cho mình; đề nghị người quản trị khác thực hiện.
- **Không gỡ được người ở trang Người thực hiện?** Người đó được gán ở đơn vị cấp trên; chuyển ô **Xem theo** về đơn vị đã gán để gỡ.
- **Đổi chức vụ nhưng hồ sơ trong kỳ không đổi?** Thay đổi không tự áp vào kỳ đang chạy; Phòng TCCB-LĐ sửa trên trang kỳ.
- **Mất mật khẩu tạm vừa cấp?** Mật khẩu tạm chỉ hiển thị một lần; đặt lại mật khẩu lần nữa (mục XIV).
