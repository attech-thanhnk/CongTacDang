# Triển khai CongTacDang

Tài liệu này là tài liệu triển khai duy nhất của hệ thống. Hệ thống chạy trong mạng LAN nội bộ bằng Docker Compose, gồm frontend Next.js, backend ASP.NET Core và PostgreSQL. File đính kèm lưu trên đĩa (volume `backend-uploads`). pgAdmin là công cụ tùy chọn, không chạy mặc định.

## Yêu cầu

- Ubuntu có Docker Engine và Docker Compose v2.
- DNS hoặc địa chỉ IP nội bộ trỏ tới máy chạy frontend.
- Không mở trực tiếp cổng backend ra mạng LAN nếu không có nhu cầu vận hành cụ thể.

## Cấu hình bắt buộc

Không ghi secret vào Git. Tạo file môi trường từ mẫu:

```bash
cd docker
cp .env.example .env
chmod 600 .env
```

Thay các giá trị `CHANGE_ME_*` trong `docker/.env`, tối thiểu:

- `POSTGRES_PASSWORD`: mật khẩu PostgreSQL.
- `Jwt__Secret`: khóa ký JWT, ít nhất 32 byte (`openssl rand -base64 48`). Thiếu hoặc quá ngắn thì backend dừng khi khởi động.
- `PGADMIN_DEFAULT_PASSWORD`: chỉ cần khi bật profile `tools`.

Khi chạy thật (mọi môi trường trừ `Development` và `Testing`; Compose đặt `ASPNETCORE_ENVIRONMENT=Production`) backend **từ chối khởi động** và ghi lý do vào log (`docker compose logs backend`, dòng `[LỖI CẤU HÌNH]`) nếu:

- `Jwt__Secret` trống, dưới 32 byte, còn giá trị giữ chỗ `CHANGE_ME…`, trùng khóa mẫu cũ từng có trong mã nguồn, quá đơn điệu (dưới 10 ký tự khác nhau) hoặc trùng mật khẩu CSDL;
- mật khẩu CSDL trong chuỗi kết nối trống, dưới 12 ký tự, còn `CHANGE_ME…` hoặc trùng mật khẩu mẫu/mặc định từng có trong mã nguồn;
- `Seed__InitialAdmin__Password` (nếu còn đặt) là giá trị mẫu/mặc định.

Ở `Development` chỉ kiểm tra như trước (khóa JWT bắt buộc, ≥ 32 byte, không phải khóa mẫu cũ; có chuỗi kết nối). Cách đổi: mục [Đổi khóa và mật khẩu trước khi chạy thật](#đổi-khóa-và-mật-khẩu-trước-khi-chạy-thật).

Sinh mật khẩu ngẫu nhiên trên Linux:

```bash
openssl rand -base64 32
```

Compose mặc định chỉ publish frontend ở `127.0.0.1:3001` và PostgreSQL ở `127.0.0.1:5434`. Backend chỉ có `expose: 5000` trong mạng Docker. Nếu cần debug backend cục bộ, tạo `docker/docker-compose.debug.yml` (không commit):

```yaml
services:
  backend:
    ports:
      - "127.0.0.1:5000:5000"
```

rồi chạy `docker compose -f docker/docker-compose.yml -f docker/docker-compose.debug.yml up -d`.

Không đặt backend bind vào `0.0.0.0` nếu không có firewall và reverse proxy phù hợp.

## Secret backend khi chạy trực tiếp

Khi chạy API ngoài Docker, bắt buộc cung cấp `ConnectionStrings:Default` và `Jwt:Secret`. Secret JWT phải có ít nhất 32 byte và không được dùng chuỗi mẫu cũ.

Khởi tạo user-secrets một lần:

```bash
dotnet user-secrets init --project backend/src/CongTacDang.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=congtacdang_db;Username=dangbo_admin;Password=<mat-khau>" --project backend/src/CongTacDang.Api
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)" --project backend/src/CongTacDang.Api
```

Trên PowerShell, thay lệnh sinh secret bằng:

```powershell
dotnet user-secrets set "Jwt:Secret" ([Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))) --project backend/src/CongTacDang.Api
```

Swagger chỉ bật ở Development hoặc khi đặt `Swagger:Enabled=true`. Không bật tùy chọn này trên môi trường production nếu không cần.

## Khởi động Compose

Kiểm tra cấu hình trước khi chạy:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml config
```

Khởi động dịch vụ chính:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml up -d
docker compose --env-file docker/.env -f docker/docker-compose.yml ps
```

Truy cập frontend tại `http://127.0.0.1:3001` hoặc địa chỉ reverse proxy nội bộ. Kiểm tra healthcheck:

```bash
curl --fail http://127.0.0.1:3001/api/healthz
```

Backend `/healthz` được container kiểm tra nội bộ; health check gồm cả kết nối CSDL (DbContext).

## Biến cấu hình tài khoản, bảo mật và dữ liệu khởi tạo

Dấu `__` trong tên biến môi trường tương ứng với `:` trong `appsettings.json`. `docker-compose.yml` truyền vào container các biến `Jwt__Secret`, `Database__AutoMigrate` (mặc định `true` trong Compose), `Database__SeedSampleData`, `Seed__*`, `Security__*`, `Organization__*` và `Notifications__*` đặt trong `docker/.env`. Khi chạy trực tiếp ngoài Docker, đặt bằng biến môi trường hoặc `user-secrets`.

| Biến môi trường | Mặc định | Ý nghĩa |
|---|---|---|
| `Seed__InitialAdmin__Username` | rỗng | Tên đăng nhập quản trị ban đầu (`[a-z0-9._-]`, 3–50 ký tự). |
| `Seed__InitialAdmin__FullName` | rỗng → "Quản trị hệ thống" | Họ tên hiển thị của quản trị ban đầu. |
| `Seed__InitialAdmin__Password` | rỗng | Mật khẩu ban đầu (phải đạt chính sách mật khẩu, bắt buộc đổi ở lần đăng nhập đầu). |
| `Security__Password__MinLength` | `8` | Độ dài tối thiểu mật khẩu (giá trị < 8 bị nâng lên 8; luôn yêu cầu có chữ và số). |
| `Security__RateLimit__Auth__PermitLimit` | `10` | Số lần gọi `/api/auth/login`, `/api/auth/refresh-token` mỗi cửa sổ trên mỗi địa chỉ IP. |
| `Security__RateLimit__Auth__WindowSeconds` | `60` | Độ dài cửa sổ giới hạn (giây). |
| `Database__AutoMigrate` | `true` ở Development, `false` ở Production | Tự áp dụng migration khi khởi động (xem `docs/database-migrations.md`). |
| `Database__SeedSampleData` | `false` | Tạo dữ liệu mẫu — **chỉ môi trường thử nghiệm**, không bật trên CSDL thật. |
| `Seed__SamplePassword` | rỗng | Mật khẩu tạm chung của tài khoản mẫu; rỗng → sinh ngẫu nhiên. Chỉ dùng khi bật dữ liệu mẫu. |
| `Database__ResetRolePermissions` | `false` | Đặt lại quyền của các vai trò mặc định về cấu hình mặc định ở lần khởi động này; tắt lại ngay sau khi dùng. |
| `Notifications__DueSoonDays` | `2` | Chuông nhắc việc: bước của hồ sơ còn từ 0 tới số ngày này tới hạn (theo thời hạn bước trong hồ sơ luồng) được báo "sắp tới hạn". |
| `Organization__ApprovalAuthorityRefreshMinutes` | `60` | Chu kỳ (phút) tác vụ nền tính lại thẩm quyền phê duyệt suy ra từ chức vụ cho cán bộ không đặt tay (chức vụ có thời hạn tự bắt đầu/hết hạn mà không có thao tác sửa). `0` = tắt (thẩm quyền vẫn được tính lại ngay khi thêm/sửa/kết thúc chức vụ). |

**Tài khoản quản trị ban đầu** chỉ được tạo khi hệ thống **chưa có** tài khoản đang hoạt động nào giữ cả hai quyền "Quản lý vai trò" và "Gán vai trò" (phạm vi Toàn công ty). Đã có quản trị → cấu hình bị bỏ qua (không tạo thêm, không đổi mật khẩu). Thiếu cấu hình khi chưa có quản trị → log mức Warning hướng dẫn đặt biến. Mật khẩu không bao giờ được ghi log. Sau khi quản trị đổi mật khẩu, xóa `Seed__InitialAdmin__Password` khỏi `.env`.

**Phân quyền** được tính lại ở mỗi request từ bản gán vai trò trong CSDL (cache trong bộ nhớ, TTL 5 phút, xóa ngay khi vai trò/bản gán/tài khoản thay đổi). JWT chỉ chứa danh tính. Cache quyền nằm trong bộ nhớ của **một** tiến trình API: chạy nhiều instance cần cache phân tán và sticky session (chưa hỗ trợ).

### Dữ liệu mẫu (môi trường thử nghiệm)

Khi `Database__SeedSampleData=true` và CSDL **chưa có** tài khoản, đơn vị, kỳ đánh giá nào, lần khởi động đầu tạo:

- Cây đơn vị chính quyền: `ATTECH` (loại Công ty) → `BGD` (Ban Giám đốc, loại Đơn vị), `PH-KT`, `PH-KH`, `PH-TCCB` (loại Phòng). Cây tổ chức Đảng: `DU-ATTECH` (loại Đảng ủy) → `CB-KT`, `CB-VP` (loại Chi bộ). Loại đơn vị và chức vụ lấy từ danh mục mặc định (tạo khi danh mục còn trống).
- 10 tài khoản, tất cả **bắt buộc đổi mật khẩu** ở lần đăng nhập đầu, kèm chức vụ (có kiêm nhiệm) và bản gán vai trò có phạm vi (phạm vi đơn vị gồm cả đơn vị cấp dưới). Thẩm quyền phê duyệt **suy ra từ chức vụ**: `giamdoc` (Giám đốc, kiêm Bí thư Đảng ủy) → cấp trên quyết định; các tài khoản khác → Đảng ủy cơ sở.

  | Tài khoản | Chức vụ | Vai trò (phạm vi) | Hồ sơ trong kỳ mẫu |
  |---|---|---|---|
  | `admin` | Chuyên viên (PH-KH) | Quản trị hệ thống (Toàn công ty) | — |
  | `giamdoc` | Giám đốc (ATTECH), kiêm Bí thư Đảng ủy (DU-ATTECH) | Người được đánh giá, Cấp trực tiếp sử dụng (Toàn công ty) | Đã công bố — hồ sơ luồng "Diện BTV Đảng ủy Tổng công ty", B3b/B3c/B4 là kết quả của cấp trên |
  | `vanphong` | Chuyên viên (PH-KH) | Văn phòng Đảng ủy, Cấp ủy viên Đảng ủy (Toàn công ty) | — |
  | `thamdinh` | Trưởng phòng (PH-TCCB), kiêm Đảng ủy viên | Người được đánh giá, Cơ quan thẩm định (Toàn công ty) | Chờ cấp trực tiếp sử dụng |
  | `truongphong.kt` | Trưởng phòng (PH-KT), kiêm Chi ủy viên (CB-KT) | Người được đánh giá (Toàn công ty), Lãnh đạo Phòng (Phòng Kỹ thuật) | Chờ thẩm định |
  | `bithu.kt` | Phó Trưởng phòng (PH-KT), kiêm Bí thư Chi bộ (CB-KT) | Người được đánh giá (Toàn công ty), Chi ủy / Bí thư Chi bộ (Chi bộ Khối Kỹ thuật) | Chờ ghi nhận đề xuất tập thể |
  | `thuky.kt` | Chuyên viên (PH-KT) | Thư ký tập thể lãnh đạo (Phòng Kỹ thuật) | — |
  | `canbo.kt1` | Phó Trưởng phòng (PH-KT) | Người được đánh giá (Toàn công ty) | Chờ tự chấm |
  | `canbo.kt2` | Phó Trưởng phòng (PH-KT) | Người được đánh giá (Toàn công ty) | Chờ Chi bộ xác nhận |
  | `canbo.kh` | Phó Trưởng phòng (PH-KH) | Người được đánh giá (Toàn công ty) | Đã công bố **Mức C** (Hoàn thành nhiệm vụ) — có kế hoạch 30-60-90 ngày (Mẫu 17) đang lập và một kiến nghị chờ xử lý |

- Kỳ "Đánh giá, xếp loại cán bộ Quý III/2026" gắn bộ tiêu chí **"Chấm theo 6 trục (Quý III, IV/2026)"** đã chụp vào kỳ (bộ 09B nên không có bước đăng ký/duyệt danh mục), với đủ 3 hồ sơ luồng dựng sẵn, trạng thái **Đang mở**; hồ sơ luồng của từng hồ sơ chọn theo cấp quyết định; kiểm tra kẹt luồng của kỳ **không có cảnh báo**.
- Mỗi cán bộ mẫu có khung tỷ trọng mặc định (K1/K2/K4), hồ sơ chụp lại khung khi thêm vào kỳ; hồ sơ đã qua bước tự chấm có điểm theo đủ 17 tiêu chí con (hai tiêu chí "Không đảm bảo" có căn cứ) và 6 trục của bộ, nội dung Mẫu 09C, 9D và phần tự luận theo trục của Mẫu 09B (xuất được ở "Biểu mẫu của hồ sơ").
- Trang **Đánh giá tập thể & Hội nghị**: hồ sơ tập thể Mẫu 07 (đủ mục I.1–I.4, II–VI) và Mẫu 08 (một số nhóm nội dung) của Chi bộ Khối Kỹ thuật; biên bản Mẫu 12 hội nghị tập thể lãnh đạo Phòng Kỹ thuật (B3a, có mục 3.2, chức vụ chủ trì/thư ký, kết quả kiểm phiếu của hồ sơ Trưởng phòng Kỹ thuật); biên bản kiểm phiếu hội nghị Đảng ủy (B4) có Tổ kiểm phiếu, số phiếu, cán bộ mục I (Giám đốc — cấp trên quyết định) và mục II (`canbo.kh`) — xuất Mẫu 13.
- Trang **Báo cáo**: bản nháp Mẫu 16 toàn Đảng bộ (số văn bản, nơi gửi, quy chế, đề xuất III.1, người ký).
- Thông tin đơn vị và 2 bộ tiêu chí mặc định được tạo như trên CSDL thật (xem Go-live bước 0).

Mật khẩu tạm chung: lấy từ `Seed__SamplePassword` nếu có (phải đạt chính sách mật khẩu, sai → không tạo dữ liệu mẫu, log Error); nếu không, hệ thống sinh ngẫu nhiên 12 ký tự và ghi **một lần** vào log mức Warning ngay khi tạo ("Mật khẩu tạm chung …"). Khởi động lại không tạo lại dữ liệu mẫu và không ghi lại mật khẩu.

## Go-live (CSDL thật)

Các bước 1–5 được kiểm tra tự động bởi `GoLiveScenarioTests` (khai báo đơn vị, tổ chức Đảng, tài khoản, gán vai trò qua API của các trang quản trị, mở kỳ); cây đơn vị nhiều cấp, danh mục chức vụ và chức vụ của cán bộ được kiểm bởi `DynamicOrgIntegrationTests`, thêm người được đánh giá kèm hồ sơ luồng chỉ định bởi `WorkflowProfileIntegrationTests`.

Mọi dữ liệu nền được khai báo trực tiếp trên giao diện (không có chức năng nhập từ tệp).

**0. Chuẩn bị**
- [ ] `docker/.env`: `POSTGRES_PASSWORD`, `Jwt__Secret`, `Seed__InitialAdmin__Username`, `Seed__InitialAdmin__Password` (mạnh, có chữ và số), các biến `Security__*` nếu khác mặc định. **Không** bật `Database__SeedSampleData`, `Database__ResetRolePermissions`. `POSTGRES_PASSWORD`, `Jwt__Secret` là giá trị ngẫu nhiên mới, không dùng lại giá trị mẫu/cũ (mục [Đổi khóa và mật khẩu trước khi chạy thật](#đổi-khóa-và-mật-khẩu-trước-khi-chạy-thật)).
- [ ] CSDL **mới, trống**. Khởi động stack (`Database__AutoMigrate=true` mặc định trong `docker/.env`) — backend tự áp dụng migration khi khởi động; lỗi migrate làm backend dừng và ghi log Critical. Chạy ngoài Docker: dùng `dotnet ef database update` (xem `docs/database-migrations.md`). Seeder tạo danh mục quyền, 9 vai trò mặc định (có "Quản trị hệ thống" được bảo vệ), danh mục loại đơn vị và danh mục chức vụ mặc định (M1–M26 theo HD03 — chờ nghiệp vụ xác nhận), **2 bộ tiêu chí đã xuất bản** theo bản trích xuất HD03 ("Chấm theo 6 trục (Quý III, IV/2026)", "Từ Quý I/2027" — chờ nghiệp vụ xác nhận), **thông tin đơn vị mặc định** và tài khoản quản trị ban đầu; **không** tạo đơn vị, tổ chức Đảng, cán bộ nào.
- [ ] Chuẩn bị danh sách (giấy hoặc bảng tính nội bộ) để khai báo: cây đơn vị chính quyền và cây tổ chức Đảng (mã, tên, đơn vị cấp trên, loại đơn vị), cán bộ (tên đăng nhập, họ tên, đơn vị, tổ chức Đảng), chức vụ của từng cán bộ (kể cả kiêm nhiệm), vai trò và phạm vi của từng người, danh sách người được đánh giá của kỳ.

**1. Quản trị ban đầu**
- [ ] Đăng nhập bằng `Seed__InitialAdmin__Username`/`Password` → hệ thống chuyển tới **Đổi mật khẩu** (mọi chức năng khác bị chặn) → đổi mật khẩu mạnh.
- [ ] Xóa `Seed__InitialAdmin__Password` khỏi `docker/.env`.
- [ ] Khuyến nghị tạo thêm **một quản trị thứ hai** (tạo ở bước 2d, gán ở 2f): hệ thống không cho tự gán/thu hồi vai trò của chính mình và luôn giữ ít nhất một quản trị.

**1b. Thông tin đơn vị và biểu mẫu Word** (quản trị hệ thống — quyền "Quản lý thông tin đơn vị", "Quản lý file mẫu biểu mẫu")
- [ ] Trang **Quản trị → Thông tin đơn vị**: kiểm tra/sửa tên Đảng bộ, tổ chức Đảng cấp trên, tên công ty, đơn vị chủ quản, tên viết tắt (dùng trong tên tệp, chỉ `A-Z a-z 0-9 _ -`), địa danh, tên hệ thống. Giá trị này in lên biểu mẫu Word/Excel, tên tệp tải xuống, thanh bên, đầu trang, trang đăng nhập.
- [ ] Trang **Quản trị → Biểu mẫu Word**: mỗi mẫu (01, 02, 07, 08, 09A, 09B, 09C, 9D, 10, 11, 12, 13, 16, 17) đang dùng file gốc hoặc phiên bản đã tải lên; tải *File đang dùng* về mở thử. Cần sửa bố cục → sửa trong Word theo `docs/bieu-mau.md` mục 7 → *Kiểm tra* (tag lạ = lỗi, thiếu tag = cảnh báo) → *Tải lên*. Tệp mẫu tải lên nằm trong kho tệp (`Storage:Local:Path`, thư mục `word-templates/`) — cùng phạm vi sao lưu với tệp đính kèm.

**2. Khai báo tổ chức, tài khoản, vai trò (đúng thứ tự)** — trên giao diện: **Danh mục** → **Tài khoản** → **Vai trò** (gán ở trang chi tiết tài khoản) → **Kỳ đánh giá** (bước 5). Mọi thao tác được ghi **Nhật ký**.
- [ ] a. **Loại đơn vị** (trang **Quản trị → Danh mục**, tab **Loại đơn vị**): rà danh mục mặc định (Đảng ủy, Đảng bộ bộ phận, Chi bộ; Công ty, Đơn vị, Phòng, Trung tâm, Xưởng, Đội), thêm/sửa cho đúng cơ cấu thực tế.
- [ ] b. **Đơn vị có cấp trên** (trang **Danh mục**, tab **Đơn vị chính quyền** rồi tab **Tổ chức Đảng**): thêm đơn vị gốc trước, sau đó các đơn vị cấp dưới (chọn đơn vị cấp trên và loại đơn vị). Hệ thống chặn tạo vòng và loại đơn vị sai bên. Kiểm tra cây hiển thị ở từng tab.
- [ ] c. **Danh mục chức vụ** (nếu cần; trang **Danh mục**, tab **Chức vụ**): tên, bên, mã thống kê M1–M26, thẩm quyền mặc định CoSo/CapTren, lãnh đạo/quản lý. Thẩm quyền mặc định của chức vụ quyết định thẩm quyền phê duyệt suy ra của cán bộ.
- [ ] d. **Cán bộ và tài khoản** (trang **Quản trị → Tài khoản** → **Tạo tài khoản**): tên đăng nhập, họ tên, đơn vị chính quyền, tổ chức Đảng; thẩm quyền phê duyệt để trống = suy ra từ chức vụ; khung tỷ trọng (ví dụ `K2`, theo bộ tiêu chí) để trống = dùng khung mặc định của bộ tiêu chí của kỳ. Sau khi tạo, hệ thống hiển thị **mật khẩu tạm một lần** — ghi lại ngay để giao cho người đó.
- [ ] e. **Chức vụ của cán bộ** (trang chi tiết tài khoản, tab **Chức vụ** → **Thêm chức vụ**): chức vụ, đơn vị giữ chức vụ, chính/kiêm nhiệm, từ ngày, đến ngày. Nhiều chức vụ = kiêm nhiệm. Kiểm tra thẩm quyền suy ra (Giám đốc/Bí thư Đảng ủy… → cấp trên quyết định) và mã thống kê ở cùng tab.
- [ ] f. **Gán vai trò** (trang chi tiết tài khoản, tab **Vai trò & phạm vi** → **Gán vai trò**): vai trò (danh sách ở trang **Quản trị → Vai trò**), loại phạm vi Toàn công ty / Đơn vị chính quyền / Tổ chức Đảng, đơn vị, thời hạn, ghi chú. Phạm vi đơn vị **gồm cả mọi đơn vị cấp dưới** của đơn vị được gán. Vai trò có quyền quản trị/quản lý kỳ chỉ gán được phạm vi Toàn công ty; không tự gán cho chính mình. Kiểm tra tab **Người này làm được gì** của vài người tiêu biểu (Bí thư Chi bộ, Lãnh đạo Phòng, Cơ quan thẩm định, Văn phòng Đảng ủy).
- [ ] g. **Người được đánh giá của kỳ** — làm ở bước 5 sau khi đã tạo kỳ (trang **Kỳ đánh giá** → chi tiết kỳ).

**3. Giao tài khoản**
- [ ] Giao riêng từng người tên đăng nhập + mật khẩu tạm; không lưu mật khẩu tạm sau khi giao.
- [ ] Cán bộ đăng nhập lần đầu → bắt buộc đổi mật khẩu → thấy đúng chức năng theo vai trò được gán.
- [ ] Quên mật khẩu tạm: quản trị **Đặt lại mật khẩu** trong trang Tài khoản.

**4. Kiểm tra sau go-live**
- [ ] Kết thúc/xóa một bản gán thử → người đó bị chặn chức năng tương ứng ngay request kế tiếp (không cần đăng xuất).
- [ ] Tài khoản quản trị không xem được hồ sơ đánh giá (tách quản trị kỹ thuật và nghiệp vụ).
- [ ] Xuất thử Mẫu 14 và danh sách cán bộ (Excel, trang **Báo cáo**) và một biểu mẫu của hồ sơ (Mẫu 09B/09C/9D, trang hồ sơ đánh giá → "Biểu mẫu của hồ sơ"): tên Đảng bộ, tổ chức Đảng cấp trên, tên công ty, địa danh, tên tệp đúng Thông tin đơn vị.
- [ ] Vai trò đã có từ trước khi nâng cấp **không** tự nhận 4 quyền sau công bố (`evaluation.results.view`, `evaluation.appeal.submit`, `evaluation.appeal.resolve`, `evaluation.improvement.manage`): gán trên trang **Vai trò** (hoặc khởi động một lần với `Database__ResetRolePermissions=true` rồi tắt). Phạm vi bản gán `evaluation.results.view` = phạm vi công khai kết quả (mặc định vai trò "Người được đánh giá" — gán Toàn công ty nếu công khai toàn công ty).

**5. Mở kỳ đánh giá** (người có vai trò "Cơ quan thẩm định", quyền "Quản lý kỳ đánh giá" và "Quản lý bộ tiêu chí")
- [ ] Trang **Bộ tiêu chí**: rà bộ sẽ dùng cho kỳ (tiêu chí chung và tiêu chí con, trục, khung tỷ trọng, thang quy đổi, ngưỡng mức, tham số: số sản phẩm, ngưỡng giải trình chênh lệch, trần Xuất sắc, làm tròn). Rà thêm: **biểu mẫu cá nhân áp dụng** (Q3/2026: 09B, 09C, 9D, 10 — Mẫu 10 chờ nghiệp vụ xác nhận), **mục Mẫu 09C** (tiêu đề, câu dẫn, lưu ý, số ký tự tối đa, bắt buộc khi nộp), **tiêu đề/nội dung gợi ý trục** in trên Mẫu 09B, và **tham số sau công bố**: công khai kèm điểm (mặc định chỉ công khai mức), mức bắt buộc lập kế hoạch 30-60-90 ngày (mặc định Mức C, D), số ngày sau khi đóng kỳ còn cảnh báo kế hoạch cần lập (mặc định 90). Bộ đã xuất bản không sửa được: cần điều chỉnh → **Nhân bản** → sửa bản nháp (kiểm tra tổng điểm tức thì) → **Xuất bản**; lưu trữ bộ không dùng nữa. Nghiệp vụ xác nhận nội dung bộ trước khi mở kỳ.
- [ ] Trang **Kỳ đánh giá** → tạo kỳ, chọn bộ tiêu chí đã xuất bản (bộ 09A có bước đăng ký, duyệt sản phẩm; bộ 09B không có). Kỳ mới có sẵn 3 hồ sơ luồng (Diện Đảng ủy cơ sở, Diện BTV Đảng ủy Tổng công ty, Bí thư/Phó bí thư Chi bộ là nhân viên — cấu hình mặc định chờ nghiệp vụ xác nhận) → kỳ ở trạng thái **Dự thảo**.
- [ ] Khi còn dự thảo: **chọn/xác nhận bộ tiêu chí** của kỳ (chỉ bộ đã xuất bản; đổi bộ thì bước đăng ký, duyệt sản phẩm đặt lại theo bộ mới; bộ được chụp vào kỳ và chụp lại khi mở kỳ — sau đó sửa bộ gốc không ảnh hưởng kỳ); sửa hồ sơ luồng (mỗi bước: Nội bộ / Cấp trên thực hiện / Không áp dụng, quyền thực hiện, thời hạn); thêm người được đánh giá ở trang chi tiết kỳ (chọn tay hoặc theo đơn vị, có thể chỉ định hồ sơ luồng; bỏ trống = theo cấp quyết định). Đơn vị, tổ chức Đảng, khung tỷ trọng, cấp quyết định được **chụp** vào hồ sơ tại thời điểm thêm; hồ sơ luồng chọn theo cấp quyết định (đổi được từng người/hàng loạt, có lý do).
- [ ] Xem bảng **Kiểm tra kẹt luồng**: mỗi bước Nội bộ/Cấp trên còn phía trước của mỗi hồ sơ phải có ít nhất một tài khoản đang hoạt động (không phải chủ hồ sơ) có quyền thực hiện bước trong phạm vi bao trùm hồ sơ (ví dụ Chi ủy cho "Chi bộ xác nhận", Văn phòng Đảng ủy cho "Quyết định", "Ghi nhận kết quả của cấp trên", "Công bố"). Sửa bằng cách gán vai trò hoặc sửa hồ sơ luồng. Bộ mẫu 09A: khung tỷ trọng của hồ sơ không có trong bộ cũng là cảnh báo (sửa ảnh chụp khung của người đó).
- [ ] **Mở kỳ** (kỳ chưa có bộ tiêu chí → 400; bộ không còn ở trạng thái đã xuất bản → 409). Còn cảnh báo kẹt luồng → hệ thống từ chối (409) và liệt kê cảnh báo; chỉ "mở bắt buộc" khi có lý do (ghi vào lịch sử kỳ). Từ đây mỗi người thấy việc của mình ở trang **Việc cần xử lý**; hồ sơ đi qua các bước áp dụng tới **Đã công bố**; bước do cấp trên thực hiện được Văn phòng Đảng ủy ghi nhận kèm văn bản của cấp trên (người xem được hồ sơ xem được văn bản).

## LibreOffice (xuất PDF)

Biểu mẫu Word/Excel được chuyển sang PDF ngay trên máy chủ bằng LibreOffice headless (không gọi dịch vụ bên ngoài). Thiếu LibreOffice thì API xuất PDF (`?format=pdf`) trả **503** kèm thông báo; xuất Word/Excel vẫn hoạt động bình thường.

- **Docker:** image backend (`docker/Dockerfile.backend`) đã cài `libreoffice-writer-nogui`, `libreoffice-calc-nogui` và font `fonts-liberation` (Liberation Serif tương thích kích thước với Times New Roman, đủ dấu tiếng Việt), `fonts-dejavu-core`.
- **Font gốc cho PDF (làm một lần trên máy dựng image):** biểu mẫu dùng Times New Roman, bảng tính dùng Calibri — font bản quyền Microsoft nên không có trong git. Chép 8 tệp từ `C:WindowsFonts` của một máy Windows bất kỳ vào `docker/fonts/` trên máy chủ (Linux hay Windows đều được, font nằm trong container) rồi dựng lại image backend:

  ```bash
  # tệp cần chép: times.ttf timesbd.ttf timesi.ttf timesbi.ttf calibri.ttf calibrib.ttf calibrii.ttf calibriz.ttf
  scp times*.ttf calibri.ttf calibrib.ttf calibrii.ttf calibriz.ttf user@server:/opt/congtacdang/docker/fonts/
  docker compose -f docker/docker-compose.yml up -d --build backend
  docker exec attech-dangbo-api fc-match "Times New Roman"   # phải ra times.ttf
  ```

  Không chép thì PDF dùng Liberation Serif/Sans thay thế (cùng kích thước chữ, nét hơi khác). Các lần cập nhật sau không phải chép lại, trừ khi xóa thư mục `docker/fonts/`.
- **Chạy trực tiếp trên Ubuntu/Debian:**

  ```bash
  sudo apt-get install -y --no-install-recommends libreoffice-writer-nogui libreoffice-calc-nogui fonts-liberation fonts-dejavu-core
  soffice --version
  ```

  Nếu có bản quyền font Times New Roman, cài thêm để PDF giống Word nhất (ví dụ `ttf-mscorefonts-installer` hoặc chép tệp `.ttf` vào `/usr/local/share/fonts` rồi `fc-cache -f`).
- **Chạy trực tiếp trên Windows:** cài LibreOffice bản ổn định (mặc định `C:\Program Files\LibreOffice`). Backend tự tìm `soffice` trong `PATH` và thư mục cài đặt mặc định.

Cấu hình (tùy chọn, đặt trong `appsettings` hoặc biến môi trường `Documents__Pdf__...`):

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Documents:Pdf:SofficePath` | tự tìm | Đường dẫn tệp chạy `soffice` khi cài ở vị trí khác. |
| `Documents:Pdf:TimeoutSeconds` | `60` | Thời gian tối đa một lần chuyển; quá hạn thì dừng tiến trình và trả 503. |
| `Documents:Pdf:MaxConcurrency` | `2` | Số lần chuyển chạy đồng thời. |
| `Documents:Pdf:WorkDirectory` | thư mục tạm hệ thống | Thư mục làm việc tạm; mỗi lần chuyển dùng thư mục con riêng và được xóa sau khi xong. Tài khoản chạy backend phải có quyền ghi. |
| `Documents:TemplatePath` | `Templates/Word` cạnh ứng dụng | Thư mục template Word (xem `docs/bieu-mau.md`). |

Kiểm tra nhanh sau khi triển khai: đăng nhập, mở một hồ sơ đánh giá → “Xem PDF” (hoặc gọi `GET /api/reports/docx/mau-01/{recordId}?format=pdf`).

## Công cụ tùy chọn

pgAdmin nằm trong profile `tools`, không được khởi động cùng stack mặc định:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml --profile tools up -d pgadmin
```

## Backup và khôi phục

Dùng hai script trong `docker/` (chạy trên máy chủ Docker, bằng user có quyền dùng `docker`):

| Script | Việc làm |
|---|---|
| `docker/backup.sh` | `pg_dump --format=custom` CSDL + nén volume `attech-dangbo-backend-uploads` (file đính kèm và file mẫu Word tải lên), ghi `SHA256SUMS` và `manifest.txt` (commit mã nguồn, migration mới nhất), giữ `N` bản gần nhất |
| `docker/restore.sh` | Kiểm tra checksum, so sánh migration của bản sao lưu với mã nguồn đang triển khai, xác nhận, dừng frontend/backend, sao lưu dự phòng hiện trạng, `pg_restore --clean --if-exists --single-transaction`, thay nội dung volume upload, khởi động lại — xem [Khôi phục sao lưu](#khôi-phục-sao-lưu) |

Mật khẩu CSDL không đi qua dòng lệnh: `pg_dump`/`pg_restore` chạy bên trong container `postgres` bằng biến môi trường của container.

### Sao lưu

```bash
# Mặc định lưu vào /var/backups/congtacdang, giữ 14 bản
sudo mkdir -p /var/backups/congtacdang && sudo chown "$USER" /var/backups/congtacdang
docker/backup.sh
docker/backup.sh --backup-dir /mnt/nas/congtacdang --keep 30
docker/backup.sh --dry-run          # chỉ in các lệnh sẽ chạy
```

Mỗi bản là một thư mục `congtacdang-YYYYMMDD-HHMMSS/` gồm `db.dump`, `uploads.tar.gz`, `SHA256SUMS`, `manifest.txt`. Bản đang chạy dở có hậu tố `.partial` và bị xóa nếu lỗi; chỉ thư mục đúng mẫu tên mới được tính khi xoay vòng.

Biến môi trường tùy chọn: `BACKUP_DIR`, `BACKUP_KEEP`, `ENV_FILE` (mặc định `docker/.env`), `COMPOSE_FILE`, `UPLOAD_VOLUME`, `HELPER_IMAGE` (mặc định `alpine:3.20`).

### Lịch sao lưu khuyến nghị

- Hằng ngày lúc 02:00, giữ 14 bản; trước mỗi lần nâng cấp/triển khai chạy thêm một bản thủ công.
- Sao chép thư mục sao lưu sang máy/ổ khác (NAS, ổ ngoài) — bản sao lưu nằm cùng máy chủ không bảo vệ được khi hỏng đĩa.
- Trong kỳ đánh giá cao điểm (cuối quý) có thể tăng lên 2 lần/ngày.

Ví dụ crontab (`crontab -e` của user vận hành):

```cron
0 2 * * * /opt/congtacdang/docker/backup.sh >> /var/log/congtacdang-backup.log 2>&1
```

### Kiểm tra bản sao lưu

`backup.sh` đã tự kiểm tra `pg_restore --list` và `tar tzf` ngay sau khi tạo. Định kỳ (ít nhất mỗi tháng) kiểm tra thêm:

```bash
cd /var/backups/congtacdang/congtacdang-YYYYMMDD-HHMMSS
sha256sum -c SHA256SUMS                    # toàn vẹn file
cat manifest.txt                           # thời điểm, kích thước
tar tzf uploads.tar.gz | head              # danh sách file upload
```

Và **khôi phục thử** vào một môi trường riêng (máy thử nghiệm hoặc compose project khác, không phải CSDL đang dùng): chạy `restore.sh`, đăng nhập, mở một hồ sơ có minh chứng, xuất một biểu mẫu.

## Khôi phục sao lưu

Backend chạy trong Compose bật `Database__AutoMigrate=true`: khi khởi động, nó **tự nâng cấp** schema lên migration của mã nguồn đang chạy. Vì vậy khôi phục phải đi cùng đúng phiên bản mã nguồn:

| Bản sao lưu so với mã nguồn đang triển khai | `restore.sh` làm gì |
|---|---|
| Cùng danh sách migration | Khôi phục bình thường (gõ `KHOI PHUC`). |
| **Cũ hơn** (mã nguồn có thêm migration) | Liệt kê các migration sẽ được áp dụng, yêu cầu gõ thêm `NANG CAP`. Khởi động lại backend → schema được nâng cấp, **không quay lại** schema cũ được trừ khi khôi phục lại. Muốn giữ đúng phiên bản cũ: checkout commit ghi trong `manifest.txt` (`git_commit`), dựng lại image rồi mới khôi phục. |
| **Mới hơn** (bản sao lưu có migration mã nguồn không biết) | **Dừng, không khôi phục**: mã nguồn cũ không chạy được trên dữ liệu mới. Triển khai phiên bản mã nguồn của bản sao lưu (`git_commit` trong `manifest.txt`) rồi chạy lại. |
| Không có bảng `__EFMigrationsHistory` | Dừng, không khôi phục (không xác định được phiên bản schema). |

Danh sách migration của bản sao lưu được đọc từ bảng `__EFMigrationsHistory` trong `db.dump` (bằng `pg_restore`, không đụng CSDL); danh sách của mã nguồn đọc từ `backend/src/CongTacDang.Infrastructure/Data/Migrations/` trong thư mục repo chứa script (`--repo-dir` để chỉ thư mục khác). Thư mục repo phải **cùng phiên bản** với image backend đang dựng — sau `git pull` luôn dựng lại image trước khi khôi phục.

Quy trình của `restore.sh`:

1. Kiểm tra `db.dump`, `uploads.tar.gz`, `SHA256SUMS`; in `manifest.txt`.
2. So sánh migration như bảng trên; in migration mới nhất của mã nguồn, bản sao lưu và CSDL hiện tại.
3. Xác nhận (`NANG CAP` nếu bản sao lưu cũ hơn, rồi `KHOI PHUC`).
4. **Dừng frontend và backend** (không ai ghi dữ liệu, backend không tự migrate giữa chừng); giữ `postgres` chạy.
5. **Sao lưu dự phòng hiện trạng** (CSDL + upload) bằng `backup.sh` vào `<thư mục cha của bản sao lưu>/truoc-khoi-phuc/` (giữ 10 bản; đổi bằng `--safety-dir`). Sao lưu dự phòng lỗi → dừng, dữ liệu giữ nguyên.
6. `pg_restore --clean --if-exists --single-transaction`: lỗi giữa chừng thì CSDL giữ nguyên.
7. Thay toàn bộ nội dung volume upload (bỏ qua với `--skip-uploads`).
8. Khởi động lại backend/frontend (giữ dừng với `--no-start`). Bản sao lưu cũ hơn → backend nâng cấp schema khi khởi động; theo dõi `docker compose ... logs -f backend`.

```bash
# 1. Kiểm tra trước, không thay đổi gì: checksum + so sánh migration
docker/restore.sh --check /var/backups/congtacdang/congtacdang-20260928-020000
# 2. Xem các lệnh sẽ chạy
docker/restore.sh --dry-run /var/backups/congtacdang/congtacdang-20260928-020000
# 3. Khôi phục (gõ NANG CAP nếu được hỏi, rồi KHOI PHUC)
docker/restore.sh /var/backups/congtacdang/congtacdang-20260928-020000
# Chỉ khôi phục CSDL, giữ nguyên file đính kèm hiện tại
docker/restore.sh --skip-uploads <thư-mục>
# Khôi phục nhưng chưa khởi động lại (kiểm tra thủ công trước)
docker/restore.sh --no-start <thư-mục>
```

Kịch bản tự động: `--yes` bỏ các bước gõ xác nhận; nếu bản sao lưu cũ hơn mã nguồn phải kèm `--allow-upgrade`, không thì script dừng. `--no-safety-backup` bỏ bước sao lưu dự phòng — chỉ dùng khi vừa chạy `backup.sh` thủ công.

Nếu chạy với `Database__AutoMigrate=false`, sau khi khôi phục bản cũ hơn phải áp dụng migration thủ công (`docs/database-migrations.md`) trước khi dùng.

Sau khi khôi phục: kiểm tra `/api/healthz`, đăng nhập, mở một hồ sơ có minh chứng, xuất một biểu mẫu. Khôi phục nhầm → khôi phục lại từ bản trong `truoc-khoi-phuc/` bằng chính `restore.sh`.

## Log

Backend ghi log có cấu trúc (JSON) ra console và ra file xoay vòng theo ngày `congtacdang-YYYYMMDD.log`. Trong Docker, thư mục log là `/var/log/congtacdang`, mount volume `attech-dangbo-backend-logs`; số ngày giữ đặt bằng `LOG_RETAINED_DAYS` trong `docker/.env` (mặc định 30).

Mỗi request có correlation id: backend nhận header `X-Request-Id` nếu hợp lệ (tối đa 64 ký tự chữ/số/`-_.:`), không thì sinh mới; id được trả lại trong response header `X-Request-Id` và gắn vào trường `CorrelationId` của mọi dòng log của request đó.

```bash
# Tìm toàn bộ log của một request
docker run --rm -v attech-dangbo-backend-logs:/logs:ro alpine grep -h '"CorrelationId":"<id>"' /logs/congtacdang-*.log
docker compose --env-file docker/.env -f docker/docker-compose.yml logs backend | grep '<id>'
```

Cấu hình (biến môi trường dạng `Logging__File__Path`):

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Logging:File:Enabled` | `true` | Bật/tắt log ra file |
| `Logging:File:Path` | thư mục `logs` cạnh thư mục publish (Docker: `/var/log/congtacdang`) | Thư mục log |
| `Logging:File:RetainedDays` | `30` | Số ngày giữ file log |
| `Logging:Console:Json` | `true` ngoài Development | Log console dạng JSON |

Log không ghi body request, header, cookie hay token.

## Đổi khóa và mật khẩu trước khi chạy thật

Khóa ký JWT và mật khẩu CSDL mẫu/cũ từng nằm trong lịch sử Git (không viết lại lịch sử Git). Mọi máy chạy thật phải dùng giá trị **mới, ngẫu nhiên**; backend ở Production từ chối khởi động nếu còn giá trị mẫu (xem [Cấu hình bắt buộc](#cấu-hình-bắt-buộc)). Làm khi triển khai lần đầu, và làm lại khi nghi lộ `docker/.env` hoặc có người vận hành nghỉ việc.

Các lệnh dưới đây chạy trên máy chủ, tại thư mục gốc của repo, bằng user vận hành.

1. **Sao lưu** trước khi đổi:

   ```bash
   docker/backup.sh
   ```

2. **Sinh giá trị mới** (không gửi qua chat/email, không ghi vào Git):

   ```bash
   openssl rand -base64 48   # khóa ký JWT mới (Jwt__Secret)
   openssl rand -hex 32      # mật khẩu PostgreSQL mới (POSTGRES_PASSWORD) — chỉ chữ số hex, không cần trích dẫn
   ```

3. **Đổi mật khẩu PostgreSQL** của user ứng dụng (mặc định `dangbo_admin`). Biến `POSTGRES_PASSWORD` chỉ có tác dụng khi khởi tạo CSDL lần đầu — CSDL đã có thì **phải** đổi bằng `ALTER USER`, nếu chỉ sửa `.env` thì backend không kết nối được:

   ```bash
   docker compose --env-file docker/.env -f docker/docker-compose.yml exec postgres \
     sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
   ```

   Trong `psql`:

   ```sql
   ALTER USER dangbo_admin WITH PASSWORD '<mật-khẩu-mới>';
   \q
   ```

   (Hoặc gõ `\password dangbo_admin` để nhập mật khẩu không hiện trên màn hình và không lưu vào lịch sử lệnh.)

4. **Cập nhật `docker/.env`**: `POSTGRES_PASSWORD=<mật-khẩu-mới>`, `Jwt__Secret=<khóa-mới>`; nếu dùng pgAdmin đổi cả `PGADMIN_DEFAULT_PASSWORD`. Xóa `Seed__InitialAdmin__Password` nếu quản trị ban đầu đã đổi mật khẩu. Giữ quyền file:

   ```bash
   chmod 600 docker/.env
   ```

5. **Khởi động lại** để container nhận biến mới (`restart` không đọc lại `.env`, phải tạo lại container):

   ```bash
   docker compose --env-file docker/.env -f docker/docker-compose.yml up -d --force-recreate backend
   docker compose --env-file docker/.env -f docker/docker-compose.yml logs --tail=50 backend   # không có [LỖI CẤU HÌNH]
   curl --fail http://127.0.0.1:3001/api/healthz
   ```

6. **Buộc mọi người đăng nhập lại.** Khóa JWT mới làm mọi access token cũ mất hiệu lực ngay, nhưng refresh token (lưu trong CSDL) vẫn có thể cấp access token mới. Thu hồi toàn bộ refresh token để mọi phiên cũ (kể cả phiên có thể đã bị lộ) phải đăng nhập lại:

   ```bash
   docker compose --env-file docker/.env -f docker/docker-compose.yml exec postgres \
     sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "UPDATE refresh_tokens SET \"IsRevoked\" = true, \"RevokedAt\" = now() WHERE NOT \"IsRevoked\";"'
   ```

7. Đăng nhập thử, mở một hồ sơ, xuất một biểu mẫu.

**Hậu quả:** mọi người dùng bị đăng xuất và phải đăng nhập lại (mật khẩu tài khoản không đổi); thao tác đang mở dở chưa lưu bị mất — nên làm ngoài giờ làm việc và báo trước. Đổi mật khẩu CSDL làm backend mất kết nối từ lúc chạy `ALTER USER` tới khi khởi động lại ở bước 5, nên làm liền các bước 3–5. Bản sao lưu cũ vẫn khôi phục được (mật khẩu CSDL không nằm trong bản dump).

Khi chạy API ngoài Docker, cập nhật `Jwt:Secret` và chuỗi kết nối bằng `dotnet user-secrets` (mục [Secret backend khi chạy trực tiếp](#secret-backend-khi-chạy-trực-tiếp)) rồi khởi động lại.
