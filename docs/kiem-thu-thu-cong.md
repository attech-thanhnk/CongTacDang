# Kiểm thử thủ công

Hướng dẫn tự mở hệ thống lên và dùng thử như người dùng thật. Làm trên **bản sao** của dữ liệu thật để không làm bẩn dữ liệu đang dùng.

Phần lặp lại được (đăng nhập, menu, kỳ, luồng hồ sơ, họp thống nhất, kiểm tra, xác nhận số liệu, tải Word, điện thoại) đã có bộ kiểm thử giao diện tự động: xem [kiem-thu-tu-dong.md](kiem-thu-tu-dong.md). Kiểm thử thủ công dành cho phần cần mắt người.

## 1. Chuẩn bị bản sao dữ liệu để thử

Bật Docker Desktop, rồi chạy trong PowerShell (thư mục gốc dự án). Thay `<USER>` và `<MẬT KHẨU>` bằng `POSTGRES_USER` và `POSTGRES_PASSWORD` trong `docker/.env`.

```powershell
docker compose -f docker/docker-compose.yml up -d postgres

# Nhân bản dữ liệu thật thành cơ sở dữ liệu thử (cần tắt backend đang chạy trên congtacdang_dev)
docker exec attech-dangbo-postgres psql -U <USER> -d postgres -c "DROP DATABASE IF EXISTS ctd_thu;"
docker exec attech-dangbo-postgres psql -U <USER> -d postgres -c "CREATE DATABASE ctd_thu TEMPLATE congtacdang_dev;"

# Đặt mật khẩu thử Thu12345a cho mọi tài khoản trong bản sao (không bị buộc đổi mật khẩu)
@'
CREATE EXTENSION IF NOT EXISTS pgcrypto;
UPDATE party_member_profiles
   SET "PasswordHash" = crypt('Thu12345a', gen_salt('bf', 10)),
       "MustChangePassword" = false, "FailedLoginCount" = 0, "LockoutEnd" = NULL;
'@ | docker exec -i attech-dangbo-postgres psql -U <USER> -d ctd_thu
```

Lệnh cuối phải in ra `CREATE EXTENSION` và `UPDATE <số tài khoản>`. Không thấy thì dừng lại, đừng làm tiếp.

## 2. Chạy hệ thống trên bản sao

Mở hai cửa sổ PowerShell.

Cửa sổ 1, backend:
```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__Default = 'Host=localhost;Port=5434;Database=ctd_thu;Username=<USER>;Password=<MẬT KHẨU>'
dotnet run --project backend/src/CongTacDang.Api --no-launch-profile --urls http://localhost:5000
```

Cửa sổ 2, giao diện:
```powershell
cd frontend
npm run dev
```

Mở http://localhost:3001. Tất cả tài khoản dùng mật khẩu `Thu12345a`. Mỗi kịch bản bên dưới dùng nhiều tài khoản: đăng xuất rồi đăng nhập lại, hoặc dùng cửa sổ ẩn danh riêng cho từng người.

## 3. Kịch bản chính: một kỳ đi hết luồng

Kỳ thử trong bản sao đang ở trạng thái Dự thảo với ba hồ sơ của Phòng Kế hoạch - Kinh doanh: Lê Mai Duy (`duy.lm`), Nguyễn Bình Trung (`trung.nb`), Nguyễn Thị Anh Đào (`dao.nta`).

| Bước | Tài khoản | Làm gì | Kết quả mong đợi |
|---|---|---|---|
| 1 | `loan.ctp` | Menu "Kỳ đánh giá" → mở kỳ → xem "Diện quyết định" và ba cổng → bấm kiểm tra sẵn sàng → mở kỳ | Kiểm tra báo sẵn sàng (có thể có cảnh báo "chỉ một người làm được"). Mở được kỳ |
| 2 | `duy.lm`, `trung.nb`, `dao.nta` | Mở "Hồ sơ của tôi", đăng ký sản phẩm và nộp danh mục | Hồ sơ chuyển sang chờ duyệt danh mục |
| 3 | `giang.nh` duyệt hồ sơ của Duy; `duy.lm` duyệt hồ sơ của Trung và Đào | Menu "Việc cần xử lý" → duyệt danh mục | Hồ sơ chuyển sang tự chấm |
| 4 | Từng cán bộ | Tự chấm điểm và nộp | Chuyển sang chờ Chi bộ xác nhận |
| 5 | `dao.nta` xác nhận Duy và Trung; `duy.lm` xác nhận Đào | "Việc cần xử lý" → xác nhận | Chuyển sang chờ đề xuất tập thể |
| 6 | `dao.nta` ghi đề xuất cho Duy và Trung; `loan.ttp` cho Đào | Nhập đề xuất mức của tập thể | Chuyển sang chờ thẩm định |
| 7 | `loan.ctp` thẩm định Trung và Đào; `loan.ttp` thẩm định Duy | Nhập điểm và mức thẩm định | Chuyển sang nhận xét |
| 8 | `giang.nh` | Nhận xét cấp trực tiếp sử dụng | Chuyển sang chờ quyết định |
| 9 | `hung.dt` | Mở hồ sơ, thử bấm nút quyết định | **Nút bị khóa**, hiện lý do "chưa thuộc biên bản họp thống nhất nào còn hiệu lực" |
| 10 | `loan.ctp` | Menu "Duyệt kết quả" → tab Họp thống nhất → lập biên bản cho ba hồ sơ, nhập tên nhóm, chủ trì, thư ký, thành phần, một trường hợp điều chỉnh mức kèm căn cứ → lưu | Biên bản ở trạng thái nháp |
| 11 | `long.nc` | Mở biên bản → chốt | Chốt thành công. Hồ sơ bị điều chỉnh hiện kết quả sau họp thống nhất |
| 12 | `hung.dt` | Mở hồ sơ | Nút quyết định vẫn khóa, lý do chuyển sang "phiếu kiểm tra" |
| 13 | `loan.ttp` | Tab Kiểm tra → lập phiếu cho ba hồ sơ → điền 10 mục (có thể để một mục "K/AD" kèm lý do) → kết luận "Đủ điều kiện" → gửi xác nhận | Phiếu chờ xác nhận |
| 14 | `loan.ttp` | Thử tự xác nhận phiếu của mình | **Bị từ chối** (người xác nhận phải khác người lập) |
| 15 | `hung.dt` | Xác nhận phiếu | Nút quyết định mở khóa |
| 16 | `hung.dt`, `thao.nt`, `long.nc` | Ghi nhận quyết định, rồi công bố | Hồ sơ chuyển sang đã công bố. Quyết định lần lượt từng hồ sơ đều thành công |

## 4. Các tình huống nên thử thêm

Mỗi tình huống ghi kết quả mong đợi.

1. **Phiếu xác nhận số liệu (Mẫu 03).** `loan.ctp` mở hồ sơ ở bước thẩm định → khối "Xác nhận số liệu" → yêu cầu xác nhận, chọn đơn vị chuyên môn (ví dụ Phòng Kỹ thuật - An toàn - Chất lượng). Trong lúc phiếu còn mở, nút thẩm định bị khóa và hiện lý do. Hủy phiếu (kèm lý do) hoặc hoàn tất phiếu thì nút mở lại.
2. **Cơ quan ngoài hệ thống.** Như trên nhưng chọn "cơ quan ngoài hệ thống": `loan.ctp` nhập hộ kết quả và gửi, rồi `hung.dt` (Văn phòng Đảng ủy) xác nhận.
3. **Chủ hồ sơ không tự xử lý phiếu của mình.** Đăng nhập `dao.nta`, thử mở hoặc xác nhận phiếu có hồ sơ của Đào. Phải bị từ chối hoặc không thấy.
4. **Hết hiệu lực.** Sau bước 15, trả lại thẩm định một hồ sơ, thẩm định lại. Cổng của hồ sơ đó khóa lại, phiếu hiện nhãn "Hết hiệu lực" cạnh hồ sơ đó (các hồ sơ khác không bị ảnh hưởng).
5. **Kỳ khóa dữ liệu.** `loan.ctp` khóa kỳ trước khi lập xong biên bản họp thống nhất. Kiểm tra thông báo khi lập biên bản mới, rồi "Mở lại kỳ".
6. **Tải biểu mẫu.** Ở hồ sơ, phiếu và biên bản: "Tải Word" cho Mẫu 01, 02, 03, 06, 10, 11, 19, 20. Mở file bằng Word: kiểm tên người, tên đơn vị, số liệu, không còn ô trống kiểu `«…»` hay `{{…}}`. Tải PDF báo "chưa cài LibreOffice" là bình thường trên máy phát triển.
7. **Phân quyền menu.** Đăng nhập một cán bộ thường (ví dụ `canbo` bất kỳ): chỉ thấy "Hồ sơ của tôi" và các mục dành cho mình. Đăng nhập `quantri`: thấy quản trị nhưng không xem được nội dung đánh giá.
8. **Điện thoại.** Thu nhỏ cửa sổ trình duyệt hoặc dùng chế độ giả lập điện thoại: các danh sách chuyển sang dạng thẻ, không bị cuộn ngang.

## 5. Ghi nhận lỗi

Khi thấy sai, ghi lại: tài khoản nào, trang nào, bấm gì, thấy gì so với mong đợi, kèm ảnh chụp màn hình. Nếu có thông báo lỗi màu đỏ, chép nguyên văn.

## 6. Dọn sau khi thử

Tắt hai cửa sổ chạy backend và giao diện (Ctrl+C), rồi xóa cơ sở dữ liệu thử:
```powershell
docker exec attech-dangbo-postgres psql -U <USER> -d postgres -c "DROP DATABASE IF EXISTS ctd_thu;"
```
Dữ liệu thật `congtacdang_dev` không bị thay đổi trong suốt quá trình này.

## 7. Tạo nhanh dữ liệu thử bằng script

Muốn bắt đầu thử từ giữa luồng mà không bấm lại các bước đầu: chạy `scripts/tao-du-lieu-thu.ps1` khi backend đang chạy (mặc định `http://localhost:5000`). Script chỉ gọi API nên đi qua đúng kiểm tra quyền và luật luồng; nên chạy trên bản sao dữ liệu ở mục 1.

```powershell
powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan tham-dinh
powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan cong-bo -TiepTuc   # đẩy tiếp kỳ thử đã có
powershell -File scripts/tao-du-lieu-thu.ps1 -LamSach
```

Script tạo kỳ Quý IV/2026 với bộ tiêu chí đã xuất bản đầu tiên, thêm `duy.lm`, `dao.nta`, `trung.nb` (Phòng Kế hoạch - Kinh doanh) và `thanh.nn` (Phó Giám đốc), dời hạn các bước để không bước nào quá hạn, mở kỳ rồi đẩy mọi hồ sơ tới giai đoạn chọn:

| `-GiaiDoan` | Hồ sơ dừng ở |
|---|---|
| `dang-ky` | Chờ đăng ký sản phẩm (vừa mở kỳ) |
| `tu-cham` | Chờ tự chấm (đã đăng ký, đã duyệt danh mục) |
| `tap-the` | Chờ đề xuất tập thể (đã tự chấm, Chi bộ đã xác nhận) |
| `tham-dinh` | Chờ thẩm định; diện không có bước này dừng ở bước kế |
| `nhan-xet` | Chờ cấp trực tiếp sử dụng nhận xét |
| `quyet-dinh` | Chờ quyết định; đã chốt biên bản họp thống nhất (Mẫu 20) và phiếu kiểm tra (Mẫu 19) nếu cổng Bắt buộc |
| `cong-bo` | Chờ công bố (đã quyết định hoặc ghi nhận kết quả cấp trên) |

Mỗi bước dùng tài khoản đầu tiên trong danh sách `-TaiKhoan` có nút trên hồ sơ, như trên giao diện. Cuối cùng script in bảng hồ sơ, trạng thái và tài khoản cần làm tiếp. Mật khẩu mặc định `Thu12345a` (đổi bằng `-MatKhau`).

- Đã có kỳ quý đó với hồ sơ: script dừng. Dùng `-TiepTuc` để đẩy tiếp, hoặc `-LamSach`.
- `-LamSach` làm rỗng toàn bộ dữ liệu vận hành của CSDL dev (kỳ, hồ sơ, biên bản, phiếu, nhật ký) qua docker; tài khoản, danh mục, vai trò, bộ tiêu chí giữ nguyên. Chỉ chạy trên `congtacdang_dev` hoặc `ctd_*`.
- Đăng nhập bị giới hạn 10 lần mỗi phút: script tự chờ khi gặp giới hạn.
