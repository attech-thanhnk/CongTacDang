# Mục lục tài liệu

Tìm đúng tài liệu theo việc bạn cần làm. Mọi liên kết là đường dẫn tương đối trong `docs/`.

## Bạn cần làm gì

| Bạn là ai / muốn làm gì | Đọc file | Nội dung |
|---|---|---|
| Cho người thật dùng thử: nhóm thử, lịch, đăng nhập, góp ý | [dung-thu/ke-hoach-dung-thu.md](dung-thu/ke-hoach-dung-thu.md) | Kèm phiếu góp ý Excel. |
| Người trong nhóm dùng thử: tôi là ai, đọc hướng dẫn nào | [dung-thu/README.md](dung-thu/README.md) | Bảng người → hướng dẫn theo vai trò: người được đánh giá, lãnh đạo đơn vị, Chi bộ, thư ký, Phòng TCCB-LĐ, Giám đốc/Chủ tịch, Văn phòng Đảng ủy, Quản trị. |
| Lệnh chạy thử trên máy dev: đăng nhập, tạo dữ liệu tới một giai đoạn, làm sạch | [lenh-chay-thu.md](lenh-chay-thu.md) | Tài khoản thử, mật khẩu, các giai đoạn và ai làm tiếp, lệnh kiểm thử. |
| Cán bộ, người duyệt, cơ quan thẩm định dùng hệ thống hằng ngày | [dung-thu/README.md](dung-thu/README.md) | Hướng dẫn theo vai trò, kèm ảnh trong `dung-thu/anh/`. Trang Hướng dẫn trong ứng dụng gộp từ các tệp này. |
| Tra cứu nhanh vai trò, công việc, 55 tài khoản ATTECH và cách xử lý khi luân chuyển / giao phụ trách cán bộ | [so-tay-phan-quyen-va-luan-chuyen.md](so-tay-phan-quyen-va-luan-chuyen.md) | Phân biệt Chức vụ vs Vai trò vs Diện quyết định, bảng 55 tài khoản hiện hành và 6 kịch bản điều động/phụ trách thực tế. |
| Quản trị khai báo ban đầu (đơn vị, người dùng, vai trò, kỳ, bộ tiêu chí) | [huong-dan-ban-giao.md](huong-dan-ban-giao.md) | Ai làm gì ở bước nào, danh sách việc khi đưa vào sử dụng, xử lý hồ sơ kẹt. |
| Nhập danh mục và người dùng từ Excel | [nhap-du-lieu.md](nhap-du-lieu.md) | Thứ tự nhập, các loại tệp nhập, cách thêm một loại nhập mới. |
| Người vận hành dựng hệ thống trên máy mới | [trien-khai-may-moi.md](trien-khai-may-moi.md) | Các bước từ máy trống (Ubuntu) đến hệ thống chạy được. |
| Người vận hành nâng cấp lên phiên bản mới | [nang-cap-phien-ban.md](nang-cap-phien-ban.md) | Nâng cấp tại chỗ, kiểm tra phiên bản, quay lui khi lỗi. |
| Thay thế máy đang chạy bản cũ | [thay-the-may-cu.md](thay-the-may-cu.md) | Thay tại chỗ, có sao lưu và đường lui. |
| Chuyển dữ liệu gốc sang bản mới | [chuyen-du-lieu-goc.md](chuyen-du-lieu-goc.md) | Đưa dữ liệu từ bản trước sang bản mới. |
| Sao lưu và khôi phục | [deployment.md](deployment.md) | Mục "Backup và khôi phục": chạy sao lưu định kỳ, kiểm tra trước khi khôi phục. |
| Cấu hình, biến môi trường, xuất PDF | [deployment.md](deployment.md) | Cấu hình bắt buộc, secret, Compose, LibreOffice, log. |
| Đặt lại mật khẩu quản trị khi mất | [trien-khai-may-moi.md](trien-khai-may-moi.md), [huong-dan-ban-giao.md](huong-dan-ban-giao.md) | Dùng script `docker/dat-lai-mat-khau.sh` trên máy chủ. |
| Nhà phát triển thêm hoặc sửa biểu mẫu Word/PDF | [bieu-mau.md](bieu-mau.md) | Bảng Mẫu HD03, nơi nhập, nơi xuất, danh mục tag, cách thêm mẫu. |
| Nhà phát triển thêm migration CSDL | [database-migrations.md](database-migrations.md) | Quy tắc tạo, kiểm tra và áp dụng migration. |
| Nhà phát triển hiểu phân quyền | [thiet-ke/phan-quyen.md](thiet-ke/phan-quyen.md) | Vai trò, quyền, phạm vi, bản gán và cách tính quyền. |
| Nhà phát triển hiểu luồng đánh giá | [thiet-ke/luong-danh-gia.md](thiet-ke/luong-danh-gia.md) | Năm bước, trạng thái, hồ sơ luồng, kiểm tra kẹt luồng. |
| Nhà phát triển hiểu phiếu xác nhận số liệu (đối soát), kiểm tra (kiểm soát), họp thống nhất (hiệu chuẩn) | [thiet-ke/mau-03-19-20.md](thiet-ke/mau-03-19-20.md) | Thiết kế Mẫu 03, 19, 20 và các cổng trước quyết định. |
| Kiểm thử thủ công một kỳ đi hết luồng | [kiem-thu-thu-cong.md](kiem-thu-thu-cong.md) | Dựng bản sao thử và các kịch bản kèm kết quả mong đợi. |
| Tạo nhanh dữ liệu thử tới một giai đoạn của luồng (máy phát triển) | [kiem-thu-thu-cong.md](kiem-thu-thu-cong.md) | Mục 7: script `scripts/tao-du-lieu-thu.ps1`, các giai đoạn và cách làm sạch. |
| Chạy kiểm thử giao diện tự động | [kiem-thu-tu-dong.md](kiem-thu-tu-dong.md) | Dựng môi trường thử riêng và chạy Playwright. |
| Tra thuật ngữ, tên vai trò, tên mức xếp loại | [thiet-ke/thuat-ngu.md](thiet-ke/thuat-ngu.md) | Bảng tên gọi thống nhất cho giao diện, vai trò, quyền, biểu mẫu. |
| Xem các điểm nghiệp vụ cần chốt | [nghiep-vu/cau-hoi-xac-nhan.md](nghiep-vu/cau-hoi-xac-nhan.md) | Câu hỏi, cách hệ thống đang làm và phương án khác. |
| Đối chiếu nguyên văn Hướng dẫn 03 | [nghiep-vu/hd03-trich-xuat.md](nghiep-vu/hd03-trich-xuat.md) | Bản trích nguyên văn, không sửa. |
| Xem lỗi còn tồn và lịch sử khắc phục | [remediation/FINDINGS.md](remediation/FINDINGS.md) | Danh sách lỗi và trạng thái; quy tắc ở [remediation/RULES.md](remediation/RULES.md). |

## Quy ước tài liệu

- Viết bằng tiếng Việt, câu ngắn. Tên định danh trong code giữ tiếng Anh.
- Dùng đúng thuật ngữ trong [thiet-ke/thuat-ngu.md](thiet-ke/thuat-ngu.md); không dùng tên vai trò cũ.
- Không ghi mật khẩu, khóa hay bí mật. Mật khẩu thử chỉ nằm trong hai tài liệu kiểm thử.
- Không ghi quý đánh giá cụ thể làm ví dụ nếu không cần.
- Hướng dẫn người dùng và vận hành đặt trực tiếp trong `docs/`; thiết kế kỹ thuật trong `docs/thiet-ke/`; nghiệp vụ và trích dẫn gốc trong `docs/nghiep-vu/`; danh sách lỗi và quy tắc sửa mã trong `docs/remediation/`.
- Tài liệu mới phải được thêm vào bảng ở trên.
