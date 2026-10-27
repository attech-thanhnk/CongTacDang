# Kiểm thử giao diện tự động

Bộ kịch bản Playwright chạy bằng trình duyệt Chromium thật, đi qua giao diện như người dùng (đăng nhập bằng tài khoản thật, bấm nút, điền biểu mẫu, tải tệp). Mục đích là bắt loại lỗi chỉ lộ khi đi qua giao diện: giao diện không gửi trường mà máy chủ bắt buộc, nút bị khóa sai, trang gọi API không có quyền, trang tràn ngang trên điện thoại.

Kiểm thử thủ công vẫn dùng cho phần cần mắt người (bố cục, nội dung biểu mẫu in): xem `docs/kiem-thu-thu-cong.md`.

## 1. Yêu cầu

| Thứ cần có | Ghi chú |
|---|---|
| Docker Desktop đang chạy container `attech-dangbo-postgres` | Chỉ để đọc bản sao dữ liệu và tạo CSDL thử. Không dừng, không khởi động lại container. |
| .NET SDK 10, Node.js 20 trở lên | Chạy backend và giao diện từ chính thư mục làm việc hiện tại. |
| PowerShell 5.1 trở lên (Windows) | Script `frontend/e2e/chay-e2e.ps1`. Cách chạy trên Linux ở mục 7. |
| `docker/.env` | Script đọc `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_PORT`; không in ra màn hình. Trong git worktree, script tự tìm `docker/.env` ở bản checkout chính. |
| Trình duyệt Chromium của Playwright | Cài một lần (xem dưới). |

Cài một lần, trong `frontend/`:

```powershell
cd frontend
npm ci
npx playwright install chromium
```

## 2. Chạy

Từ thư mục gốc dự án:

```powershell
powershell -File frontend/e2e/chay-e2e.ps1                    # dựng môi trường, chạy tất cả, dọn
powershell -File frontend/e2e/chay-e2e.ps1 -Grep "Mẫu 20"     # chỉ chạy kịch bản có tên khớp
powershell -File frontend/e2e/chay-e2e.ps1 -Headed            # nhìn thấy trình duyệt
powershell -File frontend/e2e/chay-e2e.ps1 -Up                # chỉ dựng môi trường rồi để chạy
powershell -File frontend/e2e/chay-e2e.ps1 -Down              # dừng môi trường dựng bằng -Up, xóa CSDL thử
```

Lần chạy đầy đủ mất khoảng 5 phút (dựng môi trường khoảng 30 giây, 79 kịch bản khoảng 4 phút). Mã thoát của script là mã thoát của Playwright (0 là đạt hết).

Script làm lần lượt:

1. Tạo CSDL `ctd_e2e` bằng `pg_dump | pg_restore` từ `congtacdang_db` ngay trong container Postgres. CSDL nguồn **chỉ được đọc**, không bị sửa. Đổi nguồn bằng `-SourceDb`.
2. Đặt mật khẩu thử cho mọi tài khoản trong `ctd_e2e` (pgcrypto) và bỏ ảnh nền đăng nhập của bản sao (tệp ảnh nằm ở máy chủ thật).
3. Chạy backend từ thư mục làm việc ở cổng **5400** (`ASPNETCORE_ENVIRONMENT=Development`, chuỗi kết nối trỏ `ctd_e2e`, khóa JWT sinh ngẫu nhiên mỗi lần, giới hạn đăng nhập nới rộng).
4. Chạy giao diện `next dev` ở cổng **3400**, `BACKEND_INTERNAL_URL=http://localhost:5400`.
5. Chạy Playwright với `E2E_BASE_URL=http://localhost:3400` và `E2E_PASSWORD` (mật khẩu thử).
6. Dừng tiến trình đã khởi động và xóa `ctd_e2e` (trừ khi dùng `-Up`).

Script chỉ dùng cổng 5400, 3400 và CSDL `ctd_e2e`; không đụng container `attech-dangbo-*` đang chạy bản thật (cổng 3001), các CSDL `congtacdang_*`, `ctd_thu`. Cổng đang bị chiếm thì script dừng, không tự giết tiến trình lạ (trừ `-Down`, chỉ dừng người nghe cổng 5400 và 3400).

Mật khẩu thử `Thu12345a` chỉ có trong CSDL thử `ctd_e2e`, không dùng cho môi trường nào khác.

### Chạy lại nhanh khi đang viết kịch bản

```powershell
powershell -File frontend/e2e/chay-e2e.ps1 -Up       # dựng môi trường, giữ chạy
cd frontend
$env:E2E_BASE_URL = 'http://localhost:3400'; $env:E2E_PASSWORD = 'Thu12345a'
npx playwright test e2e/04                           # chạy riêng một tệp
```

Các kịch bản nối tiếp nhau trên cùng CSDL (kỳ, hồ sơ, phiếu). Chạy riêng một tệp từ 03 trở đi cần các tệp trước đã chạy trên CSDL đó; trạng thái chia sẻ (mã hồ sơ, biên bản, phiếu) lưu ở `frontend/e2e-ket-qua/trang-thai.json` và bị xóa đầu mỗi lần chạy. Chạy lại một tệp đã chạy rồi thì dựng lại môi trường (`-Down`, `-Up`) hoặc đặt `E2E_GIU_TRANG_THAI=1` để giữ tệp trạng thái khi chạy tiếp các tệp sau.

## 3. Đọc kết quả

Mọi thứ nằm trong `frontend/e2e-ket-qua/` (đã loại khỏi git):

| Thư mục / tệp | Nội dung |
|---|---|
| `bao-cao/` | Báo cáo HTML: `cd frontend; npx playwright show-report e2e-ket-qua/bao-cao` |
| `vet/<kịch bản>/` | Khi lỗi: ảnh chụp màn hình, video, `trace.zip` (mở bằng `npx playwright show-trace <đường dẫn>`), `error-context.md` (cây trang lúc lỗi) |
| `backend.log`, `backend.err.log`, `frontend.log`, `frontend.err.log` | Nhật ký hai tiến trình do script khởi động |

Đọc lỗi từ trên xuống: dòng "Error" đầu tiên là nguyên nhân; kịch bản sau trong cùng tệp thường không chạy (`-`) vì phụ thuộc kịch bản lỗi.

## 4. Các kịch bản

| Tệp | Nội dung |
|---|---|
| `01-dang-nhap` | Trang đăng nhập; chuyển về đăng nhập khi chưa có phiên; mật khẩu sai báo lỗi; đăng nhập rồi đăng xuất; quản trị chỉ thấy menu quản trị; menu theo quyền của bốn vai (cơ quan thẩm định, Văn phòng Đảng ủy, cấp trực tiếp sử dụng, cán bộ); cán bộ mở thẳng trang quản trị bị chặn; ảnh nền đăng nhập (tải lên, hiển thị, bỏ) |
| `02-ky-danh-gia` | Tạo kỳ; thêm người theo đơn vị; đặt thời hạn; mở kỳ khi còn bước chưa có người phụ trách bị từ chối kèm đề nghị mở bắt buộc; chỉnh chế độ ba cổng khi dự thảo, lưu, tải lại; mở kỳ |
| `03-luong-ho-so` | Hai hồ sơ đi qua luồng bằng giao diện: đăng ký danh mục, duyệt, tự chấm, Chi bộ xác nhận, đề xuất của tập thể, thẩm định, nhận xét; một hồ sơ tới "chờ quyết định", một hồ sơ dừng ở thẩm định |
| `04-hieu-chuan` | Nút quyết định khóa trước hiệu chuẩn; lập biên bản (Mẫu 20); điền chủ trì, thư ký, thành phần, một ca điều chỉnh; lưu và tải lại; người lập không tự chốt, người khác chốt |
| `05-kiem-soat` | Phiếu kiểm soát (Mẫu 19): lập, 10 mục, kết luận, gửi; người lập không tự xác nhận; người khác xác nhận thì nút quyết định mở; ghi nhận quyết định và công bố |
| `06-doi-soat` | Phiếu xác nhận số liệu (Mẫu 03) từ hồ sơ ở bước thẩm định: yêu cầu, thẩm định bị chặn khi còn phiếu mở, lập và gửi, xác nhận từ "Việc cần xử lý", yêu cầu thứ hai rồi hủy |
| `07-xuat-word` | Tải Word biên bản hiệu chuẩn (tab Họp thống nhất), phiếu kiểm soát (tab Kiểm tra), phiếu xác nhận số liệu, Mẫu 01 và Mẫu 02: tệp tải về là `.docx` hợp lệ (đủ lớn, đầu tệp `PK`, có nội dung đúng, không còn thẻ chưa điền) |
| `08-dien-thoai` | Khung 390 px: các trang chính và trang chi tiết mở được, trang không cuộn ngang, danh sách chuyển sang thẻ, menu mở bằng nút |
| `09-ca-trang` | Mở lần lượt các trang của từng vai ở màn hình máy tính, cả trang chi tiết (kỳ, hồ sơ, biên bản, phiếu, người dùng, vai trò) |

### Kiểm tra chung cho mọi kịch bản

Hàm kiểm chung nằm trong `e2e/fixtures.ts` và gắn vào mọi trang mở bằng `page` hoặc `mo(...)`. Cuối mỗi kịch bản, nếu trên **bất kỳ** trang nào đã mở xuất hiện một trong các điều sau thì kịch bản lỗi:

- lỗi JavaScript (`console.error`, `pageerror`);
- phản hồi 5xx từ `/api`;
- phản hồi 403 hoặc 404 cho yêu cầu GET tới `/api` (giao diện gọi API mà người dùng không có quyền, hoặc gọi đối tượng không tồn tại). Kịch bản cố ý kiểm tra bị chặn thì thêm mẫu đường dẫn được phép vào mảng `cho4xx`, ví dụ `cho4xx.push(/\/api\/roles/)`.

## 5. Thêm kịch bản mới

1. Tạo `frontend/e2e/NN-ten.spec.ts` (số `NN` quyết định thứ tự chạy). Import `test`, `expect` từ `./fixtures`, không import từ `@playwright/test`, để có kiểm tra chung.
2. Mở trang đã đăng nhập sẵn: `const page = await mo('loan.ctp')`. Mỗi lần gọi `mo` là một người dùng, một phiên riêng; đăng nhập qua API cho nhanh. Kịch bản kiểm tra chính màn hình đăng nhập thì dùng `page` và đăng nhập bằng giao diện như `01-dang-nhap`.
3. Ưu tiên bộ chọn theo vai trò và nhãn: `getByRole('button', { name: 'Lưu' })`, `getByLabel('Chủ trì')`, `getByRole('dialog', { name: '…' })`. Chỉ dùng `#id` khi nhãn không gắn với ô nhập. Thông báo kiểm bằng `thongBao(page, 'Đã lưu …')`.
4. Dữ liệu chia sẻ giữa các tệp: `ghiTrangThai({ ... })` và `docTrangThai()` trong `e2e/du-lieu.ts`; các thao tác dùng chung về hồ sơ ở `e2e/luong.ts`; tải tệp ở `e2e/xuat-tep.ts`.
5. Chỉ dùng tài khoản và tên đơn vị có thật trong bản sao dữ liệu; kịch bản phụ thuộc dữ liệu thì nêu rõ ở đầu tệp.
6. Dùng đúng tên menu, nút, trạng thái theo `docs/thiet-ke/thuat-ngu.md`. Đổi tên trên giao diện thì sửa kịch bản cùng lúc.
7. Chạy riêng bằng `-Grep`, sau đó chạy đầy đủ một lần trước khi commit.

Một kịch bản đạt mà không có khẳng định nào là vô nghĩa: nút bị khóa phải kiểm bằng lý do hiện ra hoặc khi đã điền đủ biểu mẫu, không chỉ `toBeDisabled()` (nút gửi của biểu mẫu trống vốn bị khóa).

## 6. Giới hạn hiện tại

- Chạy trên `next dev` nên lần mở trang đầu tiên chậm; script đã mở trước các trang chính.
- Chưa có kịch bản cho: phiếu xác nhận số liệu do cơ quan chuyên môn trong hệ thống lập (bản sao dữ liệu chưa gán vai trò đó cho ai; kịch bản dùng đường "cơ quan ngoài hệ thống, người yêu cầu nhập hộ"), xuất PDF, đính kèm tệp, kiến nghị và kế hoạch sau công bố, nhập dữ liệu, chế độ kỳ khóa dữ liệu.
- Kịch bản phụ thuộc dữ liệu của `congtacdang_db` (tài khoản, phòng, vai trò). Dữ liệu thật đổi nhiều thì cập nhật kịch bản.
- Chỉ chạy Chromium, máy tính 1366 px và điện thoại 390 px.

## 7. Windows và Linux

Script PowerShell dùng `docker exec`, `taskkill` và `Get-NetTCPConnection` nên chạy trên Windows. Trên Linux hoặc macOS, làm tay các bước tương đương rồi chạy Playwright:

```bash
# 1. CSDL thử (thay <USER>, <MẬT KHẨU> bằng giá trị trong docker/.env)
docker exec -e PGPASSWORD=<MẬT KHẨU> attech-dangbo-postgres psql -U <USER> -d postgres -c "CREATE DATABASE ctd_e2e;"
docker exec -e PGPASSWORD=<MẬT KHẨU> attech-dangbo-postgres sh -c "pg_dump -U <USER> -Fc congtacdang_db | pg_restore -U <USER> -d ctd_e2e --no-owner --no-privileges"
# 2. đặt mật khẩu thử và bỏ ảnh nền: chạy hai câu UPDATE như trong chay-e2e.ps1
# 3. backend (cổng 5400) và giao diện (cổng 3400)
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__Default='Host=localhost;Port=5434;Database=ctd_e2e;Username=<USER>;Password=<MẬT KHẨU>' \
  Jwt__Secret="$(openssl rand -base64 48)" Security__RateLimit__Auth__PermitLimit=1000 \
  dotnet run --project backend/src/CongTacDang.Api --no-launch-profile --urls http://localhost:5400 &
cd frontend && BACKEND_INTERNAL_URL=http://localhost:5400 npx next dev -p 3400 &
# 4. kịch bản
E2E_BASE_URL=http://localhost:3400 E2E_PASSWORD=Thu12345a npx playwright test
```

Trên Linux, nếu thiếu thư viện hệ thống cho Chromium: `npx playwright install --with-deps chromium`.
