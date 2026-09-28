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
- `PGADMIN_DEFAULT_PASSWORD`: chỉ cần khi bật profile `tools`.

Sinh mật khẩu ngẫu nhiên trên Linux:

```bash
openssl rand -base64 32
```

Compose mặc định chỉ publish frontend ở `127.0.0.1:3001` và PostgreSQL ở `127.0.0.1:5434`. Backend chỉ có `expose: 5000` trong mạng Docker. Nếu cần debug backend cục bộ, đặt trong `.env`:

```dotenv
BACKEND_PORTS=["127.0.0.1:5000:5000"]
```

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

Dấu `__` trong tên biến môi trường tương ứng với `:` trong `appsettings.json`. `docker-compose.yml` truyền vào container các biến `Seed__InitialAdmin__*` và `Security__*` đặt trong `docker/.env`; các biến `Database__*` và `Seed__SamplePassword` không có trong Compose (cấu hình production) — đặt bằng biến môi trường / `user-secrets` khi chạy trực tiếp, hoặc thêm vào mục `environment` của môi trường thử nghiệm.

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

**Tài khoản quản trị ban đầu** chỉ được tạo khi hệ thống **chưa có** tài khoản đang hoạt động nào giữ cả hai quyền "Quản lý vai trò" và "Gán vai trò" (phạm vi Toàn công ty). Đã có quản trị → cấu hình bị bỏ qua (không tạo thêm, không đổi mật khẩu). Thiếu cấu hình khi chưa có quản trị → log mức Warning hướng dẫn đặt biến. Mật khẩu không bao giờ được ghi log. Sau khi quản trị đổi mật khẩu, xóa `Seed__InitialAdmin__Password` khỏi `.env`.

**Phân quyền** được tính lại ở mỗi request từ bản gán vai trò trong CSDL (cache trong bộ nhớ, TTL 5 phút, xóa ngay khi vai trò/bản gán/tài khoản thay đổi). JWT chỉ chứa danh tính. Cache, phiên import và tệp mật khẩu tạm nằm trong bộ nhớ của **một** tiến trình API: chạy nhiều instance cần cache phân tán và sticky session (chưa hỗ trợ).

### Dữ liệu mẫu (môi trường thử nghiệm)

Khi `Database__SeedSampleData=true` và CSDL **chưa có** tài khoản, Phòng, Chi bộ, kỳ đánh giá nào, lần khởi động đầu tạo:

- 4 Phòng (`BGD`, `PH-KT`, `PH-KH`, `PH-TCCB`), 2 Chi bộ (`CB-KT`, `CB-VP`).
- 9 tài khoản, tất cả **bắt buộc đổi mật khẩu** ở lần đăng nhập đầu, kèm bản gán vai trò có phạm vi:

  | Tài khoản | Vai trò (phạm vi) | Hồ sơ trong kỳ mẫu |
  |---|---|---|
  | `admin` | Quản trị hệ thống (Toàn công ty) | — |
  | `giamdoc` | Người được đánh giá, Cấp trực tiếp sử dụng (Toàn công ty) | Đã công bố (cấp trên quyết định) |
  | `vanphong` | Văn phòng Đảng ủy, Cấp ủy viên Đảng ủy (Toàn công ty) | — |
  | `thamdinh` | Người được đánh giá, Cơ quan thẩm định (Toàn công ty) | Chờ cấp trực tiếp sử dụng |
  | `truongphong.kt` | Người được đánh giá (Toàn công ty), Lãnh đạo Phòng (Phòng Kỹ thuật) | Chờ thẩm định |
  | `bithu.kt` | Người được đánh giá (Toàn công ty), Chi ủy / Bí thư Chi bộ (Chi bộ Khối Kỹ thuật) | Chờ ghi nhận đề xuất tập thể |
  | `thuky.kt` | Thư ký tập thể lãnh đạo (Phòng Kỹ thuật) | — |
  | `canbo.kt1` | Người được đánh giá (Toàn công ty) | Chờ tự chấm |
  | `canbo.kt2` | Người được đánh giá (Toàn công ty) | Chờ Chi bộ xác nhận |

- Kỳ "Đánh giá, xếp loại cán bộ Quý III/2026" theo mẫu "Quý III/2026 — chuyển tiếp" (không có bước đăng ký/duyệt danh mục, tự chấm Mẫu 09B), trạng thái **Đang mở**.

Mật khẩu tạm chung: lấy từ `Seed__SamplePassword` nếu có (phải đạt chính sách mật khẩu, sai → không tạo dữ liệu mẫu, log Error); nếu không, hệ thống sinh ngẫu nhiên 12 ký tự và ghi **một lần** vào log mức Warning ngay khi tạo ("Mật khẩu tạm chung …"). Khởi động lại không tạo lại dữ liệu mẫu và không ghi lại mật khẩu.

## Go-live (CSDL thật)

Thứ tự dưới đây được kiểm tra tự động bởi `GoLiveScenarioTests` (trừ bước 0).

**0. Chuẩn bị**
- [ ] `docker/.env`: `POSTGRES_PASSWORD`, `Seed__InitialAdmin__Username`, `Seed__InitialAdmin__Password` (mạnh, có chữ và số), các biến `Security__*` nếu khác mặc định. **Không** bật `Database__SeedSampleData`, `Database__ResetRolePermissions`.
- [ ] CSDL **mới, trống**. Áp dụng migration trước khi khởi động API: `ConnectionStrings__Default="Host=127.0.0.1;Port=5434;..." dotnet ef database update -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api` (Production không tự migrate — xem `docs/database-migrations.md`), rồi khởi động API. Seeder tạo danh mục quyền, 9 vai trò mặc định (có "Quản trị hệ thống" được bảo vệ) và tài khoản quản trị ban đầu; **không** tạo Phòng, Chi bộ, cán bộ nào.
- [ ] Chuẩn bị 4 tệp Excel từ file mẫu tải trong trang **Nhập dữ liệu**: Phòng, Chi bộ, Cán bộ và tài khoản, Gán vai trò. Tên vai trò chép đúng từ trang **Vai trò**. Tệp ≤ 5 MB, ≤ 2.000 dòng; tệp cán bộ nên ≤ ~150 dòng/lần (tạo mật khẩu chậm).

**1. Quản trị ban đầu**
- [ ] Đăng nhập bằng `Seed__InitialAdmin__Username`/`Password` → hệ thống chuyển tới **Đổi mật khẩu** (mọi chức năng khác bị chặn) → đổi mật khẩu mạnh.
- [ ] Xóa `Seed__InitialAdmin__Password` khỏi `docker/.env`.
- [ ] Khuyến nghị tạo thêm **một quản trị thứ hai** (nhập ở bước 2c, gán ở 2d): hệ thống không cho tự gán/thu hồi vai trò của chính mình và luôn giữ ít nhất một quản trị.

**2. Nhập dữ liệu (trang Nhập dữ liệu, đúng thứ tự)** — mỗi tệp: tải lên → xem trước từng dòng → sửa hết dòng lỗi (còn lỗi thì không xác nhận được, không dòng nào được ghi) → **Xác nhận**.
- [ ] a. **Phòng/đơn vị**.
- [ ] b. **Chi bộ**.
- [ ] c. **Cán bộ và tài khoản** (tham chiếu Mã Phòng / Mã Chi bộ) → xác nhận → **tải ngay tệp "tài khoản mới + mật khẩu tạm"** (tải được một lần, hết hạn sau 30 phút, mất nếu khởi động lại API). Lưu tệp ở nơi an toàn.
- [ ] d. **Gán vai trò** (tên đăng nhập, tên vai trò, loại phạm vi Toàn công ty/Phòng/Chi bộ, mã Phòng/Chi bộ, thời hạn). Vai trò có quyền quản trị/nhập dữ liệu/quản lý kỳ chỉ gán được phạm vi Toàn công ty. Kiểm tra trang **Tài khoản → Tra cứu quyền** của vài người tiêu biểu (Bí thư Chi bộ, Lãnh đạo Phòng, Cơ quan thẩm định, Văn phòng Đảng ủy).
- [ ] Mỗi lần xác nhận có một bản ghi "Import" trong **Nhật ký**; từng bản gán cũng được ghi nhật ký.

**3. Giao tài khoản**
- [ ] Giao riêng từng người tên đăng nhập + mật khẩu tạm (không gửi cả tệp); hủy tệp mật khẩu sau khi giao.
- [ ] Cán bộ đăng nhập lần đầu → bắt buộc đổi mật khẩu → thấy đúng chức năng theo vai trò được gán.
- [ ] Quên mật khẩu tạm: quản trị **Đặt lại mật khẩu** trong trang Tài khoản (không nhập lại tệp — tên đăng nhập đã có sẽ bị báo lỗi).

**4. Kiểm tra sau go-live**
- [ ] Kết thúc/xóa một bản gán thử → người đó bị chặn chức năng tương ứng ngay request kế tiếp (không cần đăng xuất).
- [ ] Tài khoản quản trị không xem được hồ sơ đánh giá (tách quản trị kỹ thuật và nghiệp vụ).

**5. Mở kỳ đánh giá** (người có vai trò "Cơ quan thẩm định", quyền "Quản lý kỳ đánh giá")
- [ ] Trang **Kỳ đánh giá** → tạo kỳ từ mẫu ("Đầy đủ theo HD03" hoặc "Quý III/2026 — chuyển tiếp") → kỳ ở trạng thái **Dự thảo**.
- [ ] Khi còn dự thảo: chỉnh bước bật/tắt, thời hạn, tham số; thêm người được đánh giá (chọn tay, theo Phòng/Chi bộ, hoặc nhập Excel loại "Người được đánh giá của kỳ"). Phòng, Chi bộ, khung chức danh, cấp quyết định được **chụp** vào hồ sơ tại thời điểm thêm.
- [ ] Bảo đảm mỗi bước bật đều có người được gán quyền tương ứng trong phạm vi (ví dụ Chi ủy cho "Chi bộ xác nhận", Văn phòng Đảng ủy cho "Quyết định"/"Công bố"); bước không có người phụ trách thì tắt khi kỳ còn dự thảo.
- [ ] **Mở kỳ**. Từ đây mỗi người thấy việc của mình ở trang **Việc cần xử lý**; hồ sơ đi qua các bước bật tới **Đã công bố**.

## LibreOffice (xuất PDF)

Biểu mẫu Word/Excel được chuyển sang PDF ngay trên máy chủ bằng LibreOffice headless (không gọi dịch vụ bên ngoài). Thiếu LibreOffice thì API xuất PDF (`?format=pdf`) trả **503** kèm thông báo; xuất Word/Excel vẫn hoạt động bình thường.

- **Docker:** image backend (`docker/Dockerfile.backend`) đã cài `libreoffice-writer-nogui`, `libreoffice-calc-nogui` và font `fonts-liberation` (Liberation Serif tương thích kích thước với Times New Roman, đủ dấu tiếng Việt), `fonts-dejavu-core`. Không cần cấu hình thêm.
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
| `docker/backup.sh` | `pg_dump --format=custom` CSDL + nén volume `attech-dangbo-backend-uploads`, ghi `SHA256SUMS` và `manifest.txt`, giữ `N` bản gần nhất |
| `docker/restore.sh` | Kiểm tra checksum, yêu cầu gõ `KHOI PHUC` để xác nhận, dừng frontend/backend, `pg_restore --clean --if-exists --single-transaction`, thay nội dung volume upload, khởi động lại |

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

### Khôi phục

```bash
docker/backup.sh                                                        # sao lưu hiện trạng trước
docker/restore.sh --dry-run /var/backups/congtacdang/congtacdang-20260928-020000
docker/restore.sh /var/backups/congtacdang/congtacdang-20260928-020000  # gõ KHOI PHUC để xác nhận
docker/restore.sh --skip-uploads <thư-mục>                              # chỉ khôi phục CSDL
```

`--yes` bỏ bước xác nhận (chỉ dùng trong kịch bản tự động đã kiểm soát). `pg_restore` chạy trong một transaction: lỗi giữa chừng thì CSDL giữ nguyên. Sau khi khôi phục, script khởi động lại backend/frontend; kiểm tra `/api/healthz`, đăng nhập và mở file minh chứng.

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

## Rotate secret

Secret cũ từng nằm trong lịch sử Git. Cần rotate cả JWT secret và mật khẩu database, không rewrite lịch sử Git:

1. Sinh JWT secret và mật khẩu database mới.
2. Cập nhật secret trong secret manager, `dotnet user-secrets`, hoặc `docker/.env` trên máy triển khai.
3. Khởi động lại backend và kiểm tra đăng nhập, healthcheck.
4. Thu hồi secret cũ và xác nhận các phiên/token cũ không còn được chấp nhận.
5. Giới hạn quyền đọc file `.env` ở `600`, không gửi file này qua chat hoặc commit vào repository.

Sau khi đổi mật khẩu PostgreSQL, cập nhật chuỗi kết nối backend cùng lúc và giữ lại backup trước khi rotate.
