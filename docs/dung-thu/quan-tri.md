# Hướng dẫn cho Quản trị

Dành cho bộ phận CNTT hỗ trợ nhóm dùng thử: cấp và đặt lại mật khẩu, mở khóa, gán vai trò, xem ai làm việc gì ở từng đơn vị.

Gặp lỗi hoặc chỗ khó hiểu: ghi vào phiếu góp ý `phieu-gop-y.xlsx`.

## Cần biết trước

- Mỗi lần gán vai trò gồm: người + vai trò + phạm vi + thời hạn. Phạm vi là **Toàn công ty**, một **Đơn vị chính quyền** hoặc một **Tổ chức Đảng**, và bao trùm mọi đơn vị con.
- Việc trên hồ sơ được giao cho người ở cấp gần hồ sơ nhất. Người ở cấp trên (nhãn **Cấp trên** ở trang **Người thực hiện**) vẫn làm được khi người gần nhất vắng.
- Không ai tự gán vai trò cho chính mình.

## 1. Mở tài khoản của một cán bộ

1. Menu **Hệ thống** → **Tài khoản**.
2. Gõ tên hoặc tên đăng nhập vào ô **Tìm kiếm** → bấm vào dòng cán bộ.

![Danh sách tài khoản](anh/quan-tri-01-tai-khoan.png)

Trang tài khoản có các thẻ **Thông tin**, **Chức vụ**, **Vai trò & phạm vi**. Phần **Thông tin** cho biết **Đăng nhập cuối**, **Khóa tạm đến**, **Số lần nhập sai liên tiếp**.

## 2. Cấp hoặc đặt lại mật khẩu tạm

Dùng khi cấp tài khoản cho người thử lần đầu, hoặc khi người dùng quên mật khẩu.

1. Mở tài khoản (mục 1) → nút **⋯** ở đầu trang → **Đặt lại mật khẩu** → **Đặt lại**.
2. Hộp hiện **Tên đăng nhập** và **Mật khẩu tạm**. Bấm **Sao chép** và gửi cho người dùng. Mật khẩu tạm chỉ hiển thị một lần.

![Mật khẩu tạm](anh/quan-tri-02-mat-khau-tam.png)

**Xong thì thấy:** mật khẩu cũ hết hiệu lực, mọi phiên của người đó bị đăng xuất. Lần đăng nhập kế tiếp, người dùng phải đổi mật khẩu.

Tài khoản mới: trang **Tài khoản** → **Tạo tài khoản**; sau khi tạo, hệ thống cũng hiện mật khẩu tạm một lần.

## 3. Mở khóa đăng nhập

Người dùng nhập sai mật khẩu 5 lần liên tiếp bị khóa 15 phút.

1. Mở tài khoản → bấm **Mở khóa đăng nhập** ở đầu trang (chỉ hiện khi đang bị khóa hoặc đã nhập sai) → **Mở khóa**.

![Mở khóa đăng nhập](anh/quan-tri-03-mo-khoa.png)

Người dùng không nhớ mật khẩu thì đặt lại mật khẩu (mục 2).

## 4. Gán vai trò và phạm vi

1. Mở tài khoản → thẻ **Vai trò & phạm vi** → bấm **Phân công vai trò**.
2. Hộp **Phân công vai trò**:
   - chọn **Vai trò**;
   - **Loại phạm vi**: **Toàn công ty**, đơn vị chính quyền hoặc tổ chức Đảng;
   - **Đối tượng phạm vi**: chọn đơn vị hoặc tổ chức Đảng cụ thể;
   - **Hiệu lực từ ngày**, **Đến hết ngày** (để trống là không hết hạn); **Ghi chú**.
3. Bấm **Phân công**.

![Phân công vai trò](anh/quan-tri-04-phan-cong.png)

**Xong thì thấy:** dòng mới trong **Phân công đang và sắp hiệu lực**. Phần **Người này làm được gì** liệt kê quyền đang có hiệu lực.

- Sửa thời hạn: bấm **Sửa thời hạn, ghi chú** ở dòng phân công → **Lưu**.
- Thôi giao: **Kết thúc phân công** → **Kết thúc**. Gán nhầm: **Gỡ phân công** → **Gỡ**.

## 5. Xem người thực hiện các bước

Dùng để kiểm tra mỗi việc ở một đơn vị đã có người làm, và ai đang được giao.

1. Menu **Hệ thống** → **Người thực hiện**.
2. Chọn **Đơn vị chính quyền** hoặc **Tổ chức Đảng**, rồi chọn đơn vị cần xem.
3. Mỗi việc liệt kê người thực hiện:
   - người không có nhãn là người ở cấp gần nhất, việc hiện ở **Việc cần xử lý** của họ;
   - nhãn **Cấp trên**: người ở cấp trên, làm được khi người gần nhất vắng;
   - nhãn **Chưa có người**: việc chưa ai làm, cần thêm người;
   - nhãn **Kỳ hiện tại không dùng**: kỳ đang chọn không có việc này.

![Người thực hiện](anh/quan-tri-05-nguoi-thuc-hien.png)

**Thêm người cho một việc:** bấm **Thêm người** ở việc đó → hộp **Thêm người thực hiện**: chọn **Người thực hiện** (có nút **Chọn nhanh** theo người giữ chức vụ), **Vai trò**, **Đến hết ngày**, **Ghi chú** → bấm **Thêm**.

**Bỏ người khỏi một việc:** nút **⋯** cạnh tên người đó → **Gỡ khỏi việc này** → **Gỡ**. Chỉ gỡ được người được gán tại chính đơn vị đang xem.

Khi mọi việc đều có người, trang ghi **Mọi việc đều có người**.
