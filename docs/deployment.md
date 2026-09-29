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

Dấu `__` trong tên biến môi trường tương ứng với `:` trong `appsettings.json`. `docker-compose.yml` truyền vào container các biến `Database__AutoMigrate` (mặc định `true` trong Compose), `Database__SeedSampleData`, `Seed__*`, `Security__*` và `Organization__*` đặt trong `docker/.env`. Khi chạy trực tiếp ngoài Docker, đặt bằng biến môi trường hoặc `user-secrets`.

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
| `Organization__ApprovalAuthorityRefreshMinutes` | `60` | Chu kỳ (phút) tác vụ nền tính lại thẩm quyền phê duyệt suy ra từ chức vụ cho cán bộ không đặt tay (chức vụ có thời hạn tự bắt đầu/hết hạn mà không có thao tác sửa). `0` = tắt (thẩm quyền vẫn được tính lại ngay khi thêm/sửa/kết thúc chức vụ). |

**Tài khoản quản trị ban đầu** chỉ được tạo khi hệ thống **chưa có** tài khoản đang hoạt động nào giữ cả hai quyền "Quản lý vai trò" và "Gán vai trò" (phạm vi Toàn công ty). Đã có quản trị → cấu hình bị bỏ qua (không tạo thêm, không đổi mật khẩu). Thiếu cấu hình khi chưa có quản trị → log mức Warning hướng dẫn đặt biến. Mật khẩu không bao giờ được ghi log. Sau khi quản trị đổi mật khẩu, xóa `Seed__InitialAdmin__Password` khỏi `.env`.

**Phân quyền** được tính lại ở mỗi request từ bản gán vai trò trong CSDL (cache trong bộ nhớ, TTL 5 phút, xóa ngay khi vai trò/bản gán/tài khoản thay đổi). JWT chỉ chứa danh tính. Cache, phiên import và tệp mật khẩu tạm nằm trong bộ nhớ của **một** tiến trình API: chạy nhiều instance cần cache phân tán và sticky session (chưa hỗ trợ).

### Dữ liệu mẫu (môi trường thử nghiệm)

Khi `Database__SeedSampleData=true` và CSDL **chưa có** tài khoản, đơn vị, kỳ đánh giá nào, lần khởi động đầu tạo:

- Cây đơn vị chính quyền: `ATTECH` (loại Công ty) → `BGD` (Ban Giám đốc, loại Đơn vị), `PH-KT`, `PH-KH`, `PH-TCCB` (loại Phòng). Cây tổ chức Đảng: `DU-ATTECH` (loại Đảng ủy) → `CB-KT`, `CB-VP` (loại Chi bộ). Loại đơn vị và chức vụ lấy từ danh mục mặc định (tạo khi danh mục còn trống).
- 9 tài khoản, tất cả **bắt buộc đổi mật khẩu** ở lần đăng nhập đầu, kèm chức vụ (có kiêm nhiệm) và bản gán vai trò có phạm vi (phạm vi đơn vị gồm cả đơn vị cấp dưới). Thẩm quyền phê duyệt **suy ra từ chức vụ**: `giamdoc` (Giám đốc, kiêm Bí thư Đảng ủy) → cấp trên quyết định; các tài khoản khác → Đảng ủy cơ sở.

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

- Kỳ "Đánh giá, xếp loại cán bộ Quý III/2026" theo kiểu kỳ "Quý III/2026 — chuyển tiếp" (không có bước đăng ký/duyệt danh mục) gắn bộ tiêu chí **"Mẫu 09B — Quý III/2026"** đã chụp vào kỳ, với đủ 3 hồ sơ luồng dựng sẵn, trạng thái **Đang mở**; hồ sơ luồng của từng hồ sơ chọn theo cấp quyết định; kiểm tra kẹt luồng của kỳ **không có cảnh báo**.
- Mỗi cán bộ mẫu có khung tỷ trọng mặc định (K1/K2/K4), hồ sơ chụp lại khung khi thêm vào kỳ; hồ sơ đã qua bước tự chấm có điểm theo đủ 17 tiêu chí con (hai tiêu chí "Không đảm bảo" có căn cứ) và 6 trục của bộ.
- Thông tin đơn vị và 2 bộ tiêu chí mặc định được tạo như trên CSDL thật (xem Go-live bước 0).

Mật khẩu tạm chung: lấy từ `Seed__SamplePassword` nếu có (phải đạt chính sách mật khẩu, sai → không tạo dữ liệu mẫu, log Error); nếu không, hệ thống sinh ngẫu nhiên 12 ký tự và ghi **một lần** vào log mức Warning ngay khi tạo ("Mật khẩu tạm chung …"). Khởi động lại không tạo lại dữ liệu mẫu và không ghi lại mật khẩu.

## Go-live (CSDL thật)

Các bước 1–5 được kiểm tra tự động bởi `GoLiveScenarioTests` (nhập đơn vị, tổ chức Đảng, cán bộ, gán vai trò, mở kỳ); nhập cây đơn vị nhiều cấp, danh mục chức vụ và chức vụ của cán bộ được kiểm bởi `DynamicOrgIntegrationTests`, cột "Hồ sơ luồng" của tệp người được đánh giá bởi `WorkflowProfileIntegrationTests`.

**0. Chuẩn bị**
- [ ] `docker/.env`: `POSTGRES_PASSWORD`, `Seed__InitialAdmin__Username`, `Seed__InitialAdmin__Password` (mạnh, có chữ và số), các biến `Security__*` nếu khác mặc định. **Không** bật `Database__SeedSampleData`, `Database__ResetRolePermissions`.
- [ ] CSDL **mới, trống**. Khởi động stack (`Database__AutoMigrate=true` mặc định trong `docker/.env`) — backend tự áp dụng migration khi khởi động; lỗi migrate làm backend dừng và ghi log Critical. Chạy ngoài Docker: dùng `dotnet ef database update` (xem `docs/database-migrations.md`). Seeder tạo danh mục quyền, 9 vai trò mặc định (có "Quản trị hệ thống" được bảo vệ), danh mục loại đơn vị và danh mục chức vụ mặc định (M1–M26 theo HD03 — chờ nghiệp vụ xác nhận), **2 bộ tiêu chí đã xuất bản** theo bản trích xuất HD03 ("Mẫu 09B — Quý III/2026", "Mẫu 09A — từ 2027" — chờ nghiệp vụ xác nhận), **thông tin đơn vị mặc định** và tài khoản quản trị ban đầu; **không** tạo đơn vị, tổ chức Đảng, cán bộ nào.
- [ ] Chuẩn bị các tệp Excel từ file mẫu tải trong trang **Nhập dữ liệu**: Danh mục đơn vị chính quyền, Danh mục tổ chức Đảng, Danh mục chức vụ (nếu cần sửa danh mục mặc định), Cán bộ và tài khoản, Chức vụ của cán bộ, Gán vai trò, Người được đánh giá của kỳ. Tên vai trò chép đúng từ trang **Vai trò**, tên chức vụ đúng từ **Danh mục → Chức vụ**, tên loại đơn vị đúng từ **Danh mục → Loại đơn vị**. Tệp ≤ 5 MB, ≤ 2.000 dòng; tệp cán bộ nên ≤ ~150 dòng/lần (tạo mật khẩu chậm).

**1. Quản trị ban đầu**
- [ ] Đăng nhập bằng `Seed__InitialAdmin__Username`/`Password` → hệ thống chuyển tới **Đổi mật khẩu** (mọi chức năng khác bị chặn) → đổi mật khẩu mạnh.
- [ ] Xóa `Seed__InitialAdmin__Password` khỏi `docker/.env`.
- [ ] Khuyến nghị tạo thêm **một quản trị thứ hai** (nhập ở bước 2d, gán ở 2f): hệ thống không cho tự gán/thu hồi vai trò của chính mình và luôn giữ ít nhất một quản trị.

**1b. Thông tin đơn vị và biểu mẫu Word** (quản trị hệ thống — quyền "Quản lý thông tin đơn vị", "Quản lý file mẫu biểu mẫu")
- [ ] Trang **Quản trị → Thông tin đơn vị**: kiểm tra/sửa tên Đảng bộ, tổ chức Đảng cấp trên, tên công ty, đơn vị chủ quản, tên viết tắt (dùng trong tên tệp, chỉ `A-Z a-z 0-9 _ -`), địa danh, tên hệ thống. Giá trị này in lên biểu mẫu Word/Excel, tên tệp tải xuống, thanh bên, đầu trang, trang đăng nhập.
- [ ] Trang **Quản trị → Biểu mẫu Word**: mỗi mẫu (01, 02, 10, 11, 13) đang dùng file gốc hoặc phiên bản đã tải lên; tải *File đang dùng* về mở thử. Cần sửa bố cục → sửa trong Word theo `docs/bieu-mau.md` mục 7 → *Kiểm tra* (tag lạ = lỗi, thiếu tag = cảnh báo) → *Tải lên*. Tệp mẫu tải lên nằm trong kho tệp (`Storage:Local:Path`, thư mục `word-templates/`) — cùng phạm vi sao lưu với tệp đính kèm.

**2. Khai báo tổ chức và nhập dữ liệu (đúng thứ tự)** — mỗi tệp ở trang **Nhập dữ liệu**: tải lên → xem trước từng dòng → sửa hết dòng lỗi (còn lỗi thì không xác nhận được, không dòng nào được ghi) → **Xác nhận**.
- [ ] a. **Loại đơn vị** (trang **Danh mục → Loại đơn vị**, không có tệp nhập): rà danh mục mặc định (Đảng ủy, Đảng bộ bộ phận, Chi bộ; Công ty, Đơn vị, Phòng, Trung tâm, Xưởng, Đội), thêm/sửa cho đúng cơ cấu thực tế.
- [ ] b. **Đơn vị có cấp trên**: tệp **Danh mục đơn vị chính quyền** rồi **Danh mục tổ chức Đảng** — cột "Mã đơn vị cha" (trống = đơn vị gốc) và "Loại đơn vị"; thứ tự dòng tùy ý (cha trong cùng tệp được tạo trước), thiếu cha/tạo vòng/sai loại → lỗi dòng. Kiểm tra cây ở trang **Danh mục**.
- [ ] c. **Danh mục chức vụ** (nếu cần): tệp **Danh mục chức vụ** (tên, bên, mã thống kê M1–M26, thẩm quyền mặc định CoSo/CapTren…) hoặc sửa ở **Danh mục → Chức vụ**. Thẩm quyền mặc định của chức vụ quyết định thẩm quyền phê duyệt suy ra của cán bộ.
- [ ] d. **Cán bộ và tài khoản** (tham chiếu mã đơn vị chính quyền / mã tổ chức Đảng; cột "Thẩm quyền phê duyệt" để trống = suy ra từ chức vụ; cột "Mã khung tỷ trọng" (ví dụ `K2`, theo bộ tiêu chí) để trống = dùng khung mặc định của bộ tiêu chí của kỳ; sửa từng người ở **Tài khoản → Sửa**) → xác nhận → **tải ngay tệp "tài khoản mới + mật khẩu tạm"** (tải được một lần, hết hạn sau 30 phút, mất nếu khởi động lại API). Lưu tệp ở nơi an toàn.
- [ ] e. **Chức vụ của cán bộ** (tên đăng nhập, chức vụ, mã đơn vị giữ chức vụ, chính/kiêm nhiệm, từ ngày, đến ngày). Một người nhiều dòng = kiêm nhiệm. Kiểm tra ở trang chi tiết tài khoản: thẩm quyền suy ra (Giám đốc/Bí thư Đảng ủy… → cấp trên quyết định) và mã thống kê.
- [ ] f. **Gán vai trò** (tên đăng nhập, tên vai trò, loại phạm vi Toàn công ty / Đơn vị chính quyền / Tổ chức Đảng, cột "Mã đơn vị", thời hạn). Phạm vi đơn vị **gồm cả mọi đơn vị cấp dưới** của đơn vị được gán. Vai trò có quyền quản trị/nhập dữ liệu/quản lý kỳ chỉ gán được phạm vi Toàn công ty. Kiểm tra trang **Tài khoản → Tra cứu quyền** của vài người tiêu biểu (Bí thư Chi bộ, Lãnh đạo Phòng, Cơ quan thẩm định, Văn phòng Đảng ủy).
- [ ] g. **Người được đánh giá của kỳ** — làm ở bước 5 sau khi đã tạo kỳ (cột "Hồ sơ luồng" tùy chọn; trống = theo cấp quyết định).
- [ ] Mỗi lần xác nhận có một bản ghi "Import" trong **Nhật ký**; từng bản gán cũng được ghi nhật ký.

**3. Giao tài khoản**
- [ ] Giao riêng từng người tên đăng nhập + mật khẩu tạm (không gửi cả tệp); hủy tệp mật khẩu sau khi giao.
- [ ] Cán bộ đăng nhập lần đầu → bắt buộc đổi mật khẩu → thấy đúng chức năng theo vai trò được gán.
- [ ] Quên mật khẩu tạm: quản trị **Đặt lại mật khẩu** trong trang Tài khoản (không nhập lại tệp — tên đăng nhập đã có sẽ bị báo lỗi).

**4. Kiểm tra sau go-live**
- [ ] Kết thúc/xóa một bản gán thử → người đó bị chặn chức năng tương ứng ngay request kế tiếp (không cần đăng xuất).
- [ ] Tài khoản quản trị không xem được hồ sơ đánh giá (tách quản trị kỹ thuật và nghiệp vụ).
- [ ] Xuất thử Mẫu 14 và danh sách cán bộ (Excel, trang **Báo cáo**) và một phiếu Word của hồ sơ (Mẫu 02/10, trang hồ sơ đánh giá): tên Đảng bộ, tổ chức Đảng cấp trên, tên công ty, địa danh, tên tệp đúng Thông tin đơn vị.

**5. Mở kỳ đánh giá** (người có vai trò "Cơ quan thẩm định", quyền "Quản lý kỳ đánh giá" và "Quản lý bộ tiêu chí")
- [ ] Trang **Bộ tiêu chí**: rà bộ sẽ dùng cho kỳ (tiêu chí chung và tiêu chí con, trục, khung tỷ trọng, thang quy đổi, ngưỡng mức, tham số: số sản phẩm, ngưỡng giải trình chênh lệch, trần Xuất sắc, làm tròn). Bộ đã xuất bản không sửa được: cần điều chỉnh → **Nhân bản** → sửa bản nháp (kiểm tra tổng điểm tức thì) → **Xuất bản**; lưu trữ bộ không dùng nữa. Nghiệp vụ xác nhận nội dung bộ trước khi mở kỳ.
- [ ] Trang **Kỳ đánh giá** → tạo kỳ từ kiểu kỳ dựng sẵn ("Đầy đủ" hoặc "Quý III/2026 — chuyển tiếp"; mỗi kiểu sinh sẵn 3 hồ sơ luồng: Diện Đảng ủy cơ sở, Diện BTV Đảng ủy Tổng công ty, Bí thư/Phó bí thư Chi bộ là nhân viên — cấu hình mặc định chờ nghiệp vụ xác nhận) → kỳ ở trạng thái **Dự thảo**.
- [ ] Khi còn dự thảo: **chọn/xác nhận bộ tiêu chí** của kỳ (chỉ bộ đã xuất bản; tạo kỳ không chọn → bộ mới nhất có mẫu gợi ý của kiểu kỳ; bộ được chụp vào kỳ và chụp lại khi mở kỳ — sau đó sửa bộ gốc không ảnh hưởng kỳ); sửa hồ sơ luồng (mỗi bước: Nội bộ / Cấp trên thực hiện / Không áp dụng, quyền thực hiện, thời hạn); thêm người được đánh giá (chọn tay, theo đơn vị, hoặc nhập Excel loại "Người được đánh giá của kỳ"). Đơn vị, tổ chức Đảng, khung tỷ trọng, cấp quyết định được **chụp** vào hồ sơ tại thời điểm thêm; hồ sơ luồng chọn theo cấp quyết định (đổi được từng người/hàng loạt, có lý do).
- [ ] Xem bảng **Kiểm tra kẹt luồng**: mỗi bước Nội bộ/Cấp trên còn phía trước của mỗi hồ sơ phải có ít nhất một tài khoản đang hoạt động (không phải chủ hồ sơ) có quyền thực hiện bước trong phạm vi bao trùm hồ sơ (ví dụ Chi ủy cho "Chi bộ xác nhận", Văn phòng Đảng ủy cho "Quyết định", "Ghi nhận kết quả của cấp trên", "Công bố"). Sửa bằng cách gán vai trò hoặc sửa hồ sơ luồng. Bộ mẫu 09A: khung tỷ trọng của hồ sơ không có trong bộ cũng là cảnh báo (sửa ảnh chụp khung của người đó).
- [ ] **Mở kỳ** (kỳ chưa có bộ tiêu chí → 400; bộ không còn ở trạng thái đã xuất bản → 409). Còn cảnh báo kẹt luồng → hệ thống từ chối (409) và liệt kê cảnh báo; chỉ "mở bắt buộc" khi có lý do (ghi vào lịch sử kỳ). Từ đây mỗi người thấy việc của mình ở trang **Việc cần xử lý**; hồ sơ đi qua các bước áp dụng tới **Đã công bố**; bước do cấp trên thực hiện được Văn phòng Đảng ủy ghi nhận kèm văn bản của cấp trên (người xem được hồ sơ xem được văn bản).

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
