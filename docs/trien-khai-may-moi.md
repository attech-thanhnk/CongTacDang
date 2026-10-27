# Triển khai trên máy mới tinh (Ubuntu)

Tài liệu này đưa một máy Ubuntu trắng thành hệ thống CongTacDang chạy được và dùng được. Làm lần lượt từng bước. Mỗi bước có: lệnh cần chạy, kết quả mong đợi, cách kiểm tra và việc làm nếu sai. Chưa xong bước trước thì chưa làm bước sau.

Tài liệu liên quan:

- [Nâng cấp phiên bản về sau](nang-cap-phien-ban.md): mỗi lần có bản mới.
- [Chuyển dữ liệu gốc từ máy cũ](chuyen-du-lieu-goc.md): khi thay máy đang chạy bản cũ.
- [Thay thế tại chỗ máy đang chạy bản cũ](thay-the-may-cu.md): quy trình từng bước, đường lui, làm sạch.
- [Triển khai (tham chiếu)](deployment.md): chi tiết biến cấu hình, LibreOffice, sao lưu, khôi phục, log.
- [Hướng dẫn bàn giao](huong-dan-ban-giao.md): đưa hệ thống vào sử dụng sau khi máy chạy.

**Mục lục**

1. [Chuẩn bị](#1-chuẩn-bị)
2. [Cài Docker và Compose](#2-cài-docker-và-compose)
3. [Lấy mã nguồn và cố định phiên bản](#3-lấy-mã-nguồn-và-cố-định-phiên-bản)
4. [Tạo file cấu hình docker/.env](#4-tạo-file-cấu-hình-dockerenv)
5. [Tài khoản quản trị ban đầu](#5-tài-khoản-quản-trị-ban-đầu)
6. [Dựng image và khởi động](#6-dựng-image-và-khởi-động)
7. [Kiểm tra sau cài](#7-kiểm-tra-sau-cài)
8. [Đổi mật khẩu quản trị và xóa mật khẩu khỏi .env](#8-đổi-mật-khẩu-quản-trị-và-xóa-mật-khẩu-khỏi-env)
9. [Tự khởi động lại sau khi bật máy](#9-tự-khởi-động-lại-sau-khi-bật-máy)
10. [Sao lưu định kỳ](#10-sao-lưu-định-kỳ)
11. [Tường lửa và cổng](#11-tường-lửa-và-cổng)
12. [Đưa vào sử dụng](#12-đưa-vào-sử-dụng)
13. [Nếu đã có dữ liệu gốc từ máy cũ](#13-nếu-đã-có-dữ-liệu-gốc-từ-máy-cũ)
14. [Đã xong khi nào (nghiệm thu máy mới)](#14-đã-xong-khi-nào-nghiệm-thu-máy-mới)
15. [Sự cố thường gặp](#15-sự-cố-thường-gặp)

Quy ước: lệnh có dạng `<giá-trị>` là chỗ bạn thay bằng giá trị thật. Thư mục cài đặt trong tài liệu là `/opt/congtacdang`. Mọi lệnh `docker compose` chạy trong thư mục `/opt/congtacdang/docker`.

---

## 1. Chuẩn bị

### 1.1 Cấu hình máy

Số liệu dưới đây suy ra từ máy đang chạy ổn (7,6 GB RAM, CSDL khoảng 12 MB). Đây là mức khuyến nghị, không phải giới hạn đo được.

| Thành phần | Tối thiểu | Khuyến nghị | Ghi chú |
|---|---|---|---|
| Hệ điều hành | Ubuntu Server 24.04 LTS (64 bit) | Ubuntu Server 24.04 LTS | Đã chạy thật trên bản này. |
| CPU | 2 nhân | 4 nhân | Dựng image tốn CPU nhất; lúc dùng bình thường nhẹ. |
| RAM | 4 GB | 8 GB | Dựng image Next.js và .NET cần nhiều RAM; chuyển PDF bằng LibreOffice cũng tốn RAM. |
| Ổ đĩa | 40 GB trống | 60 GB trở lên (SSD) | Image Docker và bộ nhớ đệm dựng khoảng vài GB; bản sao lưu và tệp đính kèm tăng dần theo thời gian. |
| Mạng | Có địa chỉ IP cố định trong LAN | Có địa chỉ IP cố định trong LAN | Cần Internet **trong lúc dựng image** (tải image nền, gói apt, gói npm, gói NuGet). Sau khi dựng xong, chạy trong LAN không cần Internet. |

Việc cần có sẵn:

- Một tài khoản Linux có quyền `sudo` (ví dụ `attech`) để cài đặt và vận hành.
- Địa chỉ IP của máy trong LAN (xem bằng `ip -4 addr show`).
- Quyền tải mã nguồn từ kho Git của dự án (xem bước 3).
- Giờ máy đúng (log, bản sao lưu và hạn bước đều theo giờ máy). Kiểm tra:

```bash
timedatectl
```

Mong đợi: dòng `System clock synchronized: yes` và múi giờ đúng (ví dụ `Asia/Ho_Chi_Minh`). Sai múi giờ: `sudo timedatectl set-timezone Asia/Ho_Chi_Minh`.

### 1.2 Cập nhật hệ điều hành

```bash
sudo apt-get update
sudo apt-get upgrade -y
sudo apt-get install -y git curl openssl ca-certificates rsync
```

Kiểm tra: `git --version` và `openssl version` đều in ra phiên bản. Lệnh lỗi không tải được gói: kiểm tra mạng và DNS của máy (`ping -c 2 archive.ubuntu.com`).

---

## 2. Cài Docker và Compose

Cài Docker Engine từ kho chính thức của Docker (máy đang chạy bản cũ dùng cách này). Chạy lần lượt:

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Cho tài khoản vận hành dùng `docker` không cần `sudo`, rồi đăng xuất và đăng nhập lại để có hiệu lực:

```bash
sudo usermod -aG docker "$USER"
exit
```

Đăng nhập lại, kiểm tra:

```bash
docker --version
docker compose version
docker run --rm hello-world
```

Mong đợi: hai lệnh đầu in phiên bản (Compose phải là v2, dạng `Docker Compose version v2.x`); lệnh thứ ba in `Hello from Docker!`.

Nếu sai:

| Triệu chứng | Cách xử lý |
|---|---|
| `permission denied while trying to connect to the Docker daemon` | Chưa đăng nhập lại sau `usermod`. Đăng xuất, đăng nhập lại; kiểm tra `groups` có `docker`. |
| `Cannot connect to the Docker daemon` | Dịch vụ chưa chạy: `sudo systemctl enable --now docker`. |
| Không tải được image `hello-world` | Máy chưa ra được Internet hoặc bị chặn. Kiểm tra mạng, proxy. |
| `docker compose` báo không có lệnh | Thiếu gói `docker-compose-plugin`: cài lại gói đó. |

---

## 3. Lấy mã nguồn và cố định phiên bản

### 3.1 Tải mã nguồn

```bash
sudo mkdir -p /opt/congtacdang
sudo chown "$USER": /opt/congtacdang
git clone https://github.com/attech-thanhnk/CongTacDang.git /opt/congtacdang
cd /opt/congtacdang
```

Kho Git cần đăng nhập thì Git sẽ hỏi tên tài khoản và mật khẩu (dùng token truy cập, không dùng mật khẩu tài khoản). Cách khác khi máy không vào được kho: trên máy khác chạy `git archive` hoặc chép thư mục mã nguồn sang bằng `scp`, nhưng khi đó **không còn** lệnh `git log` để biết phiên bản; chỉ làm khi bắt buộc.

### 3.2 Chọn đúng phiên bản (cố định bằng tag hoặc mã commit)

Không chạy "nhánh mới nhất" trên máy thật. Nhóm phát triển cho bạn **một** tag phát hành hoặc một mã commit; hiện kho chưa có tag phát hành nên dùng mã commit. Chuyển sang đúng phiên bản đó:

```bash
git fetch --all --tags
git checkout --detach <tag-hoặc-mã-commit>
```

Mong đợi: Git báo `HEAD is now at <mã> <nội dung commit>` (trạng thái "detached HEAD" là bình thường, vì máy chỉ chạy chứ không sửa mã).

Ghi lại phiên bản đang chạy (ghi vào sổ vận hành, mỗi lần nâng cấp ghi thêm):

```bash
git -C /opt/congtacdang log -1 --format='%h  %ad  %s' --date=short
git -C /opt/congtacdang status --short
```

Dòng đầu là mã commit, ngày và nội dung. Lệnh thứ hai phải **không in gì** (không có sửa đổi tại chỗ). Có in ra thì ai đó đã sửa file trong thư mục cài đặt: hỏi lại trước khi tiếp tục.

Phiên bản đang chạy xem được ngay trên ứng dụng: dòng "Phiên bản <mã commit>" ở chân trang đăng nhập và cuối menu tài khoản (bấm tên ở góc dưới thanh bên), hoặc bằng lệnh `curl -s http://127.0.0.1:3001/api/version` (in mã commit, thời điểm dựng, môi trường). Mã này chỉ có khi image được dựng bằng lệnh ở bước 6 (có `APP_VERSION=...` đứng trước `docker compose build`); dựng không kèm hai biến đó thì ứng dụng hiện "chưa rõ". Giao diện và backend ghi mã riêng; hai mã lệch nhau nghĩa là mới dựng lại một trong hai, dòng phiên bản sẽ ghi thêm "(giao diện <mã>)". Ngoài ra còn các cách:

- `git -C /opt/congtacdang log -1` (mã nguồn đã dựng);
- file `manifest.txt` trong mỗi bản sao lưu (dòng `git_commit` và `last_migration`);
- ngày dựng image: `docker image inspect -f '{{.Created}}' $(docker inspect -f '{{.Image}}' attech-dangbo-api)`.

Lưu ý: image được dựng từ thư mục mã nguồn tại thời điểm chạy `docker compose build`. Đổi commit mà chưa dựng lại thì ứng dụng vẫn là bản cũ.

---

## 4. Tạo file cấu hình docker/.env

```bash
cd /opt/congtacdang/docker
cp .env.example .env
chmod 600 .env
```

`docker/.env` chứa bí mật, đã nằm trong `.gitignore`. **Không** chép lên Git, không gửi qua chat hay email.

Sinh sẵn các giá trị ngẫu nhiên (chạy rồi chép kết quả vào `.env`; chạy mỗi lệnh một lần, mỗi giá trị dùng một lần):

```bash
openssl rand -base64 48     # dùng cho Jwt__Secret
openssl rand -hex 32        # dùng cho POSTGRES_PASSWORD (64 ký tự hex, không có ký tự đặc biệt)
openssl rand -hex 16        # dùng cho PGADMIN_DEFAULT_PASSWORD, và mật khẩu quản trị ban đầu nếu muốn (nhớ phải có chữ và số)
```

Mở file để sửa: `nano .env`. Mỗi dòng dạng `TÊN=giá-trị`, không có dấu cách quanh dấu `=`, không bao giá trị bằng dấu nháy. Bảng dưới giải thích từng biến trong `.env.example`.

### 4.1 Bảng biến

Cột "Bí mật": có = không để lộ.

| Biến | Ý nghĩa | Giá trị khuyến nghị / ví dụ | Bắt buộc | Bí mật |
|---|---|---|---|---|
| `POSTGRES_DB` | Tên CSDL. | Giữ `congtacdang_db`. | Không (có mặc định) | Không |
| `POSTGRES_USER` | Tên người dùng PostgreSQL của ứng dụng. | Giữ `dangbo_admin`. | Không (có mặc định) | Không |
| `POSTGRES_PASSWORD` | Mật khẩu người dùng PostgreSQL. **Chỉ có tác dụng khi CSDL được tạo lần đầu**; về sau đổi bằng `ALTER USER` (xem [deployment.md](deployment.md#đổi-khóa-và-mật-khẩu-trước-khi-chạy-thật)). | Kết quả `openssl rand -hex 32`. Tối thiểu 12 ký tự. | **Có** (thiếu thì Compose không chạy) | **Có** |
| `POSTGRES_PORT` | Cổng PostgreSQL mở trên máy chủ, chỉ ở `127.0.0.1`. | Giữ `5434`. Đổi nếu cổng bị chiếm. | Không | Không |
| `PGADMIN_DEFAULT_EMAIL` | Email đăng nhập pgAdmin (công cụ tùy chọn, không chạy mặc định). | Giữ nguyên. | Không | Không |
| `PGADMIN_DEFAULT_PASSWORD` | Mật khẩu pgAdmin. Compose đòi biến này **có giá trị** dù không bật pgAdmin, nên không xóa dòng này. | Kết quả `openssl rand -hex 16`. | **Có** (không được rỗng hay thiếu) | **Có** |
| `PGADMIN_PORT` | Cổng pgAdmin ở `127.0.0.1`. | Giữ `5050`. | Không | Không |
| `Jwt__Secret` | Khóa ký phiên đăng nhập. Tối thiểu 32 byte. Đổi khóa làm mọi người phải đăng nhập lại. | Kết quả `openssl rand -base64 48`. | **Có** | **Có** |
| `LOG_RETAINED_DAYS` | Số ngày giữ file log của backend. | Giữ `30`. | Không | Không |
| `Database__AutoMigrate` | Tự cập nhật cấu trúc CSDL (migration) khi backend khởi động. | Giữ `true`. | Không | Không |
| `Database__SeedSampleData` | Tạo dữ liệu mẫu. **Chỉ để thử nghiệm**. | Máy thật: để `false`. | Không | Không |
| `Seed__SamplePassword` | Mật khẩu tạm của tài khoản mẫu (chỉ dùng khi bật dữ liệu mẫu). | Để trống. | Không | Có |
| `Seed__InitialAdmin__Username` | Tên đăng nhập quản trị ban đầu (chữ thường, số, `.` `_` `-`, 3 đến 50 ký tự). | Ví dụ `quantri`. | **Có** ở máy mới (xem bước 5) | Không |
| `Seed__InitialAdmin__FullName` | Họ tên hiển thị của quản trị ban đầu. | Ví dụ `Quản trị hệ thống`. Để trống cũng được. | Không | Không |
| `Seed__InitialAdmin__Password` | Mật khẩu tạm của quản trị ban đầu. Tối thiểu 8 ký tự, có chữ và số. **Xóa sau khi đã đổi mật khẩu** (bước 8). | `<đặt-giá-trị-mạnh>` | **Có** ở máy mới | **Có** |
| `Security__Password__MinLength` | Độ dài tối thiểu mật khẩu người dùng (giá trị dưới 8 bị nâng lên 8). | Giữ `8` hoặc tăng lên `10`. | Không | Không |
| `Security__RateLimit__Auth__PermitLimit` | Số lần đăng nhập/làm mới phiên tối đa mỗi cửa sổ trên mỗi địa chỉ IP. | Giữ `10`. | Không | Không |
| `Security__RateLimit__Auth__WindowSeconds` | Độ dài cửa sổ trên (giây). | Giữ `60`. | Không | Không |
| `Organization__ApprovalAuthorityRefreshMinutes` | Chu kỳ (phút) tác vụ nền tính lại thẩm quyền phê duyệt theo chức vụ. `0` = tắt. | Giữ `60`. | Không | Không |
| `Notifications__DueSoonDays` | Chuông nhắc việc: báo "sắp tới hạn" khi còn từ 0 tới số ngày này. | Giữ `2`. | Không | Không |

Hai biến **không có** trong `.env.example` nhưng Compose đọc (thêm vào cuối `docker/.env` khi cần):

| Biến | Ý nghĩa | Mặc định | Khi nào đặt |
|---|---|---|---|
| `FRONTEND_BIND_IP` | Địa chỉ mạng mà cổng giao diện lắng nghe trên máy chủ. | `127.0.0.1` (chỉ truy cập được ngay trên máy chủ) | **Phải đặt** để người dùng trong LAN vào được: đặt IP của máy trong LAN (ví dụ `192.168.22.10`) hoặc `0.0.0.0`. |
| `FRONTEND_PORT` | Cổng giao diện trên máy chủ. | `3001` | Chỉ đổi khi cổng 3001 đã bị chiếm. |

Ví dụ thêm vào cuối file:

```bash
echo 'FRONTEND_BIND_IP=<IP-của-máy-trong-LAN>' >> .env
```

Các biến khác (tải tệp, PDF, đặt lại quyền vai trò...) không được Compose chuyển vào container; xem [deployment.md](deployment.md#biến-cấu-hình-tài-khoản-bảo-mật-và-dữ-liệu-khởi-tạo).

### 4.2 Quy tắc backend từ chối giá trị yếu hoặc mẫu

Ở môi trường chạy thật (Compose đặt `ASPNETCORE_ENVIRONMENT=Production`), khi khởi động backend kiểm tra và **dừng hẳn** nếu:

| Kiểm tra | Bị từ chối khi |
|---|---|
| `Jwt__Secret` | Trống; dưới 32 byte; còn chữ `CHANGE_ME`; trùng khóa mẫu cũ từng nằm trong mã nguồn; chỉ có dưới 10 ký tự khác nhau (quá đơn điệu); trùng mật khẩu CSDL. |
| `POSTGRES_PASSWORD` (trong chuỗi kết nối) | Trống; dưới 12 ký tự; còn chữ `CHANGE_ME`; trùng mật khẩu mẫu hoặc mặc định từng có trong mã nguồn (kể cả mật khẩu mặc định của PostgreSQL). |
| `Seed__InitialAdmin__Password` (nếu còn đặt) | Là giá trị mẫu hoặc còn chữ `CHANGE_ME`. |

Lý do bị từ chối nằm trong log, dòng có `[LỖI CẤU HÌNH]` (xem [mục 15](#15-sự-cố-thường-gặp)). Chỉ cần dùng các lệnh `openssl` ở trên là qua được.

### 4.3 Kiểm tra file .env

Trong `/opt/congtacdang/docker`:

```bash
grep -c 'CHANGE_ME' .env
docker compose config --quiet && echo CAU_HINH_HOP_LE
```

Mong đợi: lệnh đầu in `0` (không còn giá trị giữ chỗ); lệnh sau in `CAU_HINH_HOP_LE`. Nếu `docker compose config` báo `required variable ... is missing a value`: biến đó đang trống hoặc bị xóa, điền lại.

---

## 5. Tài khoản quản trị ban đầu

Nếu sẽ chuyển dữ liệu gốc từ máy cũ (mục 13) thì bỏ qua mục này.

CSDL mới chưa có ai đăng nhập được. Vì vậy trước lần khởi động đầu, khai báo quản trị ban đầu trong `docker/.env`:

```bash
Seed__InitialAdmin__Username=quantri
Seed__InitialAdmin__FullName=Quản trị hệ thống
Seed__InitialAdmin__Password=<đặt-giá-trị-mạnh>
```

Backend tạo tài khoản này khi khởi động, **chỉ khi** hệ thống chưa có tài khoản quản trị nào đang hoạt động. Đã có thì bỏ qua, không đổi mật khẩu. Mật khẩu không bao giờ được ghi log. Lần đăng nhập đầu hệ thống **bắt buộc đổi mật khẩu** (bước 8).

Kiểm tra: `grep '^Seed__InitialAdmin__Username=' .env` in ra tên đăng nhập, và dòng `Password` có giá trị (đừng in ra màn hình chung): `grep -c '^Seed__InitialAdmin__Password=.\+' .env` phải in `1`.

---

## 6. Dựng image và khởi động

Trong `/opt/congtacdang/docker`:

```bash
APP_VERSION=$(git rev-parse --short HEAD) APP_BUILD_TIME=$(date -u +%FT%TZ) docker compose build
```

Hai biến đầu lệnh ghi mã commit và thời điểm dựng vào image để ứng dụng hiển thị phiên bản (mục 3.2 bên dưới). Lần đầu mất khoảng 10 đến 20 phút (tải image nền, cài LibreOffice, `npm ci`, biên dịch). Mong đợi: kết thúc không có chữ `ERROR`, cuối cùng in `Built` hoặc tên hai image. Nếu lỗi tải gói giữa chừng: kiểm tra Internet rồi chạy lại đúng lệnh đó (phần đã xong được dùng lại).

Khởi động:

```bash
docker compose up -d
```

Mong đợi: tạo mạng, volume và ba container `attech-dangbo-postgres`, `attech-dangbo-api`, `attech-dangbo-web` (pgAdmin không chạy). Thứ tự tự động: PostgreSQL khỏe, rồi backend, rồi giao diện.

Theo dõi trạng thái và chờ khỏe:

```bash
docker compose ps
docker compose logs -f backend
```

Thoát xem log bằng `Ctrl+C` (không làm dừng container). Chờ tự động tối đa 5 phút:

```bash
timeout 300 sh -c 'until [ "$(docker inspect -f "{{.State.Health.Status}}" attech-dangbo-web)" = healthy ]; do sleep 5; done' && echo GIAO_DIEN_KHOE
```

Log lần đầu trên CSDL trống có một dòng mức Error về câu lệnh đọc `__EFMigrationsHistory` (bảng chưa có): bình thường, chỉ lo khi có `Critical` hoặc `[LỖI CẤU HÌNH]`.

Mong đợi: sau 1 đến 2 phút in `GIAO_DIEN_KHOE`; `docker compose ps` cột `STATUS` của cả ba container có `(healthy)`. Trong log backend lần đầu có các dòng áp dụng migration và tạo dữ liệu nền (danh mục quyền, vai trò mặc định, bộ tiêu chí mặc định, quản trị ban đầu).

Nếu sai: xem [mục 15](#15-sự-cố-thường-gặp). Lệnh xem log từng dịch vụ: `docker compose logs --tail=100 backend` (hoặc `postgres`, `frontend`).

Ghi chú: `docker/deploy-ubuntu.sh` là kịch bản cũ gộp `down`, `build --no-cache` và `up -d`; dựng lại hoàn toàn nên chậm và làm gián đoạn dịch vụ. Dùng các lệnh trong tài liệu này thay cho nó.

---

## 7. Kiểm tra sau cài

Trong `/opt/congtacdang/docker`, lần lượt:

**7.1 Backend khỏe (bao gồm kết nối CSDL):**

```bash
docker compose exec backend curl --fail --silent http://localhost:5000/healthz; echo
```

Mong đợi: `Healthy`. Lệnh báo lỗi `curl: (22)` hoặc không ra gì: backend chưa sẵn sàng hoặc mất kết nối CSDL, xem log backend.

Ghi chú: địa chỉ `/api/healthz` qua giao diện **không** dùng được để kiểm tra (đường `/api/*` chuyển tới backend, nhưng backend chỉ có `/healthz`).

**7.2 Giao diện trả trang đăng nhập (chạy ngay trên máy chủ):**

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:3001/login
```

Mong đợi: `200`. (Nếu đã đổi `FRONTEND_PORT` thì dùng cổng đó. Nếu `FRONTEND_BIND_IP` là IP LAN cụ thể thì dùng đúng IP đó thay `127.0.0.1`.)

**7.3 Từ một máy khác trong LAN** mở trình duyệt: `http://<IP-của-máy-chủ>:3001`. Mong đợi: thấy trang đăng nhập. Không vào được: xem [mục 11](#11-tường-lửa-và-cổng) và `FRONTEND_BIND_IP`.

**7.4 Đăng nhập lần đầu:** nhập tên và mật khẩu ở bước 5. Hệ thống chuyển sang trang **Đổi mật khẩu**. Làm tiếp [bước 8](#8-đổi-mật-khẩu-quản-trị-và-xóa-mật-khẩu-khỏi-env), sau đó đăng nhập lại và thấy trang chủ.

**7.5 Xuất thử Word:** vào menu **Báo cáo** và xuất Danh sách cán bộ hoặc Mẫu 14 (Excel/Word). Mong đợi: tải được tệp, mở được. CSDL mới chưa có hồ sơ nên Mẫu của hồ sơ chưa xuất thử được ở bước này; làm lại sau khi có hồ sơ (xem checklist ở [hướng dẫn bàn giao](huong-dan-ban-giao.md)).

**7.6 Kiểm tra LibreOffice (xuất PDF):**

```bash
docker compose exec backend soffice --version
docker compose exec backend fc-match "Times New Roman"
```

Mong đợi: dòng đầu in `LibreOffice 7.x` hoặc cao hơn. Dòng hai in `LiberationSerif-...` (font thay thế, dùng được). Muốn PDF đúng font Times New Roman, chép font gốc theo [deployment.md](deployment.md#libreoffice-xuất-pdf); không bắt buộc. Lệnh đầu lỗi `not found`: image dựng thiếu LibreOffice, dựng lại bằng `docker compose build backend`. Thử PDF thật: mở một hồ sơ, bấm "Xem PDF"; nếu lỗi 503 là LibreOffice chưa chạy được.

**7.7 Đúng phiên bản:**

```bash
curl -s http://127.0.0.1:3001/api/version
git -C /opt/congtacdang log -1 --format='%h %s'
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tA -c "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\""'
```

Mong đợi: `commit` ở lệnh đầu và mã commit lệnh `git log` đều là bản đã chọn ở bước 3; migration mới nhất khớp tên file mới nhất trong `backend/src/CongTacDang.Infrastructure/Data/Migrations/`.

---

## 8. Đổi mật khẩu quản trị và xóa mật khẩu khỏi .env

1. Đăng nhập bằng quản trị ban đầu, đổi sang mật khẩu mạnh trên trang **Đổi mật khẩu** (có chữ và số, từ 8 ký tự). Chỉ khi đã đổi xong mới sang bước 2.
2. Xóa mật khẩu tạm khỏi file cấu hình (giữ nguyên dòng, bỏ giá trị):

```bash
cd /opt/congtacdang/docker
sed -i 's/^Seed__InitialAdmin__Password=.*/Seed__InitialAdmin__Password=/' .env
grep -c '^Seed__InitialAdmin__Password=.\+' .env
```

Mong đợi: lệnh `grep` in `0`.

3. Tạo lại container backend để nhận cấu hình mới (`restart` không đọc lại `.env`, phải dùng lệnh dưới):

```bash
docker compose up -d --force-recreate backend
docker compose ps
```

Mong đợi: backend trở lại `(healthy)` sau khoảng 1 phút. Đăng nhập lại bằng mật khẩu **mới** vẫn được.

4. Khuyến nghị: tạo ngay quản trị thứ hai (trang **Hệ thống → Tài khoản**), xem [hướng dẫn bàn giao](huong-dan-ban-giao.md). Khi quên mật khẩu, quản trị còn lại đặt lại cho nhau được.

---

## 9. Tự khởi động lại sau khi bật máy

Ba container đã có chính sách `restart: unless-stopped`: tự chạy lại khi Docker khởi động, trừ khi chính bạn đã dừng chúng bằng `docker compose stop`. Cần dịch vụ Docker bật cùng máy:

```bash
sudo systemctl is-enabled docker
sudo systemctl enable docker containerd
```

Mong đợi: lệnh đầu in `enabled`.

Thử bằng cách khởi động lại máy (làm ngoài giờ dùng):

```bash
sudo reboot
```

Sau 2 đến 3 phút đăng nhập lại máy chủ và chạy:

```bash
cd /opt/congtacdang/docker && docker compose ps
```

Mong đợi: ba container `(healthy)` mà không chạy lệnh nào khác; mở giao diện từ máy khác thấy trang đăng nhập. Không tự lên: `systemctl status docker`, rồi `docker compose up -d`.

---

## 10. Sao lưu định kỳ

Sao lưu gồm CSDL và tệp đính kèm (kể cả file mẫu Word đã tải lên), do `docker/backup.sh` làm. Mỗi lần tạo một thư mục `congtacdang-YYYYMMDD-HHMMSS` chứa `db.dump`, `uploads.tar.gz`, `SHA256SUMS`, `manifest.txt`, rồi chỉ giữ số bản gần nhất theo `--keep`.

### 10.1 Thư mục lưu

```bash
sudo mkdir -p /var/backups/congtacdang
sudo chown "$USER": /var/backups/congtacdang
chmod 700 /var/backups/congtacdang
```

### 10.2 Chạy thử một lần bằng tay

```bash
/opt/congtacdang/docker/backup.sh --backup-dir /var/backups/congtacdang --keep 14
ls -l /var/backups/congtacdang
```

Mong đợi: dòng cuối `Hoàn tất: /var/backups/congtacdang/congtacdang-...`; trong thư mục có 4 file. Muốn xem trước các lệnh mà không chạy: thêm `--dry-run`.

Kiểm tra bản vừa tạo (thay tên thư mục cho đúng):

```bash
cd /var/backups/congtacdang/congtacdang-YYYYMMDD-HHMMSS
sha256sum -c SHA256SUMS
cat manifest.txt
```

Mong đợi: hai dòng `OK`; `manifest.txt` có `git_commit` và `last_migration` đúng phiên bản đang chạy. Script cũng tự kiểm tra đọc được `db.dump` và `uploads.tar.gz` ngay sau khi tạo.

### 10.3 Đặt lịch bằng cron

Mở bảng lịch của tài khoản vận hành (tài khoản trong nhóm `docker`):

```bash
crontab -e
```

Thêm hai dòng (chạy 02:00 hằng ngày, giữ 14 bản, ghi log vào chính thư mục sao lưu):

```cron
0 2 * * * /opt/congtacdang/docker/backup.sh --backup-dir /var/backups/congtacdang --keep 14 >> /var/backups/congtacdang/backup.log 2>&1
```

Kiểm tra cron chạy được trong môi trường tối giản (giống lúc cron chạy thật):

```bash
env -i HOME="$HOME" PATH=/usr/local/bin:/usr/bin:/bin /opt/congtacdang/docker/backup.sh --backup-dir /var/backups/congtacdang --keep 14 --dry-run
```

Mong đợi: in các bước `[1/4]` đến `[4/4]` mà không có chữ `[LỖI]`. Sáng hôm sau kiểm tra `tail -n 5 /var/backups/congtacdang/backup.log` có dòng `Hoàn tất` của 02:00, và có thư mục mới. Trong kỳ đánh giá cao điểm có thể đặt thêm một dòng giờ khác (ví dụ `0 14 * * *`).

Ước lượng dung lượng cần: (kích thước `db.dump` + `uploads.tar.gz`) nhân số bản giữ. Xem bằng `du -sh /var/backups/congtacdang`. Ổ đĩa đầy thì sao lưu thất bại: kiểm tra dung lượng hằng tháng bằng `df -h`.

### 10.4 Đưa bản sao lưu ra khỏi máy

Bản sao lưu nằm cùng máy không bảo vệ được khi hỏng ổ đĩa. Chép định kỳ sang máy hoặc ổ khác (NAS, máy chủ lưu trữ). Ví dụ chép qua SSH bằng khóa (cần tạo khóa SSH không mật khẩu cho tài khoản vận hành và chép sang máy đích trước), chạy lúc 03:00 sau khi sao lưu xong:

```cron
0 3 * * * rsync -a /var/backups/congtacdang/ <tài-khoản>@<máy-lưu-trữ>:/duong-dan/congtacdang/ >> /var/backups/congtacdang/rsync.log 2>&1
```

Không dùng `--delete` để máy đích còn giữ bản đã bị xoay vòng trên máy chủ. Hằng tháng kiểm tra bản ở máy đích bằng `sha256sum -c SHA256SUMS`.

### 10.5 Khôi phục thử (ít nhất mỗi tháng một lần)

Bản sao lưu chưa từng khôi phục thử thì chưa chắc dùng được. Khôi phục thử trên một máy thử khác, không làm trên máy đang chạy thật. Cách khôi phục: [deployment.md, mục Khôi phục sao lưu](deployment.md#khôi-phục-sao-lưu). Kiểm tra nhanh không thay đổi gì: `docker/restore.sh --check <thư-mục-bản-sao-lưu>`.

---

## 11. Tường lửa và cổng

Chỉ có **một** cổng cần mở cho người dùng trong LAN: cổng giao diện (mặc định 3001). Các cổng khác:

| Cổng | Dịch vụ | Cách bảo vệ |
|---|---|---|
| 3001 | Giao diện (Next.js) | Mở cho LAN qua `FRONTEND_BIND_IP`. |
| 5434 | PostgreSQL | Compose chỉ gán vào `127.0.0.1`; không ra mạng. Không đổi. |
| 5000 | Backend | Chỉ trong mạng Docker, không công bố ra máy chủ. Không mở. |
| 5050 | pgAdmin | Chỉ khi bật profile `tools`, chỉ `127.0.0.1`. |
| 22 | SSH quản trị | Giữ cho người vận hành. |

**Lưu ý quan trọng:** cổng do Docker công bố **không bị** `ufw` chặn (Docker tự thêm luật mạng của nó). Vì vậy việc giới hạn thật nằm ở `FRONTEND_BIND_IP` (và PostgreSQL bị buộc vào `127.0.0.1`). Nếu máy có nhiều card mạng, đặt `FRONTEND_BIND_IP` bằng IP của card trong LAN, không dùng `0.0.0.0`.

Kiểm tra cổng đang lắng nghe:

```bash
sudo ss -ltnp | grep -E ':(3001|5434|5000|5050)\b'
```

Mong đợi: `3001` ở đúng IP đã đặt (hoặc `0.0.0.0`); `5434` chỉ ở `127.0.0.1`; không có `5000`, `5050`.

Bật `ufw` cho các cổng của chính máy (SSH) nếu chính sách đơn vị yêu cầu; **cho phép SSH trước khi bật** để không tự khóa mình:

```bash
sudo ufw allow OpenSSH
sudo ufw allow 3001/tcp
sudo ufw enable
sudo ufw status
```

Truy cập từ ngoài mạng LAN (Internet): ngoài phạm vi tài liệu này, nếu cần thì cấu hình riêng.

---

## 12. Đưa vào sử dụng

Máy đã chạy. Việc tiếp theo là khai báo dữ liệu gốc: thông tin đơn vị, loại đơn vị, cây đơn vị chính quyền và tổ chức Đảng, chức vụ, tài khoản, vai trò, kỳ đánh giá. Làm theo danh sách 14 bước trong mục 3 của [Hướng dẫn bàn giao](huong-dan-ban-giao.md). Hướng dẫn thao tác từng màn hình: [Hướng dẫn sử dụng](huong-dan-su-dung.md); nhập hàng loạt từ Excel: [Nhập dữ liệu](nhap-du-lieu.md).

---

## 13. Nếu đã có dữ liệu gốc từ máy cũ

Khi máy mới thay cho máy đang chạy bản cũ, không khai báo tay lại từ đầu mà chuyển dữ liệu gốc (không chuyển hồ sơ đánh giá). Làm theo [Chuyển dữ liệu gốc từ máy cũ](chuyen-du-lieu-goc.md), **sau** bước 7 và **trước** bước 12. Khi có chuyển dữ liệu thì **bỏ bước 5 và bước 8**: tài khoản quản trị lấy từ máy cũ, nên không đặt `Seed__InitialAdmin__*` (backend vẫn khởi động trên CSDL trống, chỉ cảnh báo chưa có quản trị), và việc chuyển yêu cầu CSDL đích chưa có người dùng. Thay ngay trên máy đang chạy bản cũ (cùng thư mục, cùng tên CSDL): [Thay thế tại chỗ](thay-the-may-cu.md).

---

## 14. Đã xong khi nào (nghiệm thu máy mới)

Đánh dấu từng mục; còn mục chưa đạt thì chưa nghiệm thu.

- [ ] Máy đủ cấu hình (CPU, RAM, ổ đĩa), giờ máy đúng.
- [ ] `docker --version` và `docker compose version` chạy được bằng tài khoản vận hành, không cần `sudo`.
- [ ] Mã nguồn ở `/opt/congtacdang`, đúng tag hoặc mã commit đã chọn; `git status --short` không in gì; phiên bản đã ghi vào sổ vận hành.
- [ ] `docker/.env` quyền `600`, không còn `CHANGE_ME`, `docker compose config --quiet` không lỗi; `FRONTEND_BIND_IP` đã đặt.
- [ ] `docker compose ps`: ba container `(healthy)`.
- [ ] `/healthz` của backend trả `Healthy`.
- [ ] Từ máy khác trong LAN mở được trang đăng nhập.
- [ ] Quản trị ban đầu đã đổi mật khẩu; `Seed__InitialAdmin__Password` đã xóa khỏi `.env`; backend đã tạo lại; đăng nhập lại được.
- [ ] Có quản trị thứ hai.
- [ ] Xuất thử được tệp Excel/Word; `soffice --version` chạy được.
- [ ] Khởi động lại máy, ba container tự `(healthy)`.
- [ ] Cron sao lưu đã đặt; đã chạy tay một lần và `sha256sum -c SHA256SUMS` đều `OK`.
- [ ] Bản sao lưu được chép sang máy hoặc ổ khác.
- [ ] Cổng 5434 chỉ ở `127.0.0.1`; không có cổng 5000, 5050 ra mạng.
- [ ] Đã bàn giao cho người vận hành: vị trí `docker/.env`, nơi lưu bản sao lưu, tài liệu [nâng cấp](nang-cap-phien-ban.md).

---

## 15. Sự cố thường gặp

Mọi lệnh chạy trong `/opt/congtacdang/docker`.

| Triệu chứng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| Container không lên `(healthy)`; `docker compose ps` thấy `unhealthy` hoặc `Restarting` | Backend dừng vì lỗi cấu hình hoặc CSDL; giao diện chờ backend khỏe. | `docker compose logs --tail=100 backend`. Đọc dòng `[LỖI CẤU HÌNH]` hoặc `Critical`. Sửa theo các dòng dưới rồi `docker compose up -d`. |
| `docker compose up` báo `required variable ... is missing a value` | Thiếu `POSTGRES_PASSWORD`, `Jwt__Secret` hoặc `PGADMIN_DEFAULT_PASSWORD` trong `.env`. | Điền đủ giá trị (cả pgAdmin dù không dùng), chạy lại `docker compose config --quiet`. |
| Log backend có `[LỖI CẤU HÌNH]`, backend tự dừng | Secret yếu hoặc còn giá trị mẫu (xem [bước 4.2](#42-quy-tắc-backend-từ-chối-giá-trị-yếu-hoặc-mẫu)). | Sinh lại bằng `openssl rand -base64 48` (`Jwt__Secret`) hoặc `openssl rand -hex 32` (`POSTGRES_PASSWORD`). Sửa `.env`, rồi `docker compose up -d --force-recreate backend`. Nếu CSDL đã được tạo với mật khẩu cũ thì đổi `POSTGRES_PASSWORD` phải kèm `ALTER USER` (xem [deployment.md](deployment.md#đổi-khóa-và-mật-khẩu-trước-khi-chạy-thật)). Máy mới chưa có dữ liệu thì có thể xóa volume CSDL và tạo lại: `docker compose down` rồi `docker volume rm attech-dangbo-postgres-data` rồi `up -d` (**mất toàn bộ dữ liệu CSDL**, chỉ làm khi chắc chắn chưa có dữ liệu thật). |
| Log backend báo lỗi kết nối CSDL, `password authentication failed`, hoặc Critical khi migrate | Mật khẩu trong `.env` khác mật khẩu đã lưu trong CSDL (`POSTGRES_PASSWORD` chỉ có tác dụng ở lần tạo đầu); hoặc PostgreSQL chưa khỏe. | `docker compose ps` xem `postgres` có `(healthy)`. `docker compose logs --tail=50 postgres`. Sai mật khẩu: đặt lại mật khẩu bằng `ALTER USER` cho khớp `.env` (xem [deployment.md](deployment.md#đổi-khóa-và-mật-khẩu-trước-khi-chạy-thật)) hoặc trả `.env` về mật khẩu cũ. |
| Không đăng nhập được quản trị ban đầu | Quản trị chỉ được tạo khi chưa có quản trị nào hoạt động; hoặc mật khẩu bị từ chối vì yếu. | Xem log backend, tìm dòng nhắc `Seed:InitialAdmin`. Sửa mật khẩu trong `.env` đạt chính sách (từ 8 ký tự, có chữ và số), tạo lại backend. Nếu quản trị đã được tạo trước đó thì dùng mật khẩu đã đổi. |
| Quên mật khẩu quản trị | Mật khẩu đã đổi ở bước 8 không còn ai nhớ. | Còn quản trị khác: người đó vào trang **Hệ thống → Tài khoản** và dùng "Đặt lại mật khẩu". Chỉ có một quản trị duy nhất: dùng lệnh đặt lại mật khẩu ở [mục dưới đây](#đặt-lại-mật-khẩu-quản-trị-bằng-dòng-lệnh). |
| Hết ổ đĩa (`no space left on device`, container dừng, sao lưu lỗi) | Ổ đầy do image/bộ nhớ đệm dựng cũ, log, hoặc quá nhiều bản sao lưu. | `df -h` và `docker system df` để xem ai chiếm. Dọn bộ nhớ đệm dựng và image không dùng: `docker builder prune -f` và `docker image prune -f` (an toàn, không xóa dữ liệu). **Không** chạy `docker volume prune` hay `docker system prune --volumes` (xóa dữ liệu). Giảm `--keep` của sao lưu hoặc chép bản cũ ra máy khác rồi xóa. Sau khi có chỗ trống: `docker compose up -d`. |
| Cổng bị chiếm: `bind: address already in use` | Cổng 3001 (hoặc 5434) đã có chương trình khác dùng. | `sudo ss -ltnp \| grep ':3001'` xem chương trình nào. Dừng nó, hoặc đặt `FRONTEND_PORT` (hoặc `POSTGRES_PORT`) khác trong `.env` rồi `docker compose up -d`. Nhớ báo người dùng địa chỉ mới. |
| Mở `http://<IP>:3001` từ máy khác không được nhưng trên máy chủ mở được | `FRONTEND_BIND_IP` còn mặc định `127.0.0.1`; hoặc tường lửa của mạng chặn. | Đặt `FRONTEND_BIND_IP` (xem [bước 4.1](#41-bảng-biến)), `docker compose up -d`; kiểm tra bằng `sudo ss -ltnp \| grep 3001`. |
| Xuất PDF báo lỗi 503 | LibreOffice trong container không chạy được. | `docker compose exec backend soffice --version`. Xem [deployment.md](deployment.md#libreoffice-xuất-pdf). Xuất Word/Excel vẫn dùng bình thường. |
| Máy chạy chậm, container bị tắt đột ngột | Thiếu RAM. | `free -h`, `docker stats --no-stream`. Tăng RAM lên mức khuyến nghị. |

### Đặt lại mật khẩu quản trị bằng dòng lệnh

Dùng khi không còn quản trị nào đăng nhập được (quên mật khẩu, không có quản trị thứ hai). Chạy trên máy chủ, cần quyền dùng `docker`:

```bash
cd /opt/congtacdang/docker
./dat-lai-mat-khau.sh <tên-đăng-nhập> --kiem-tra   # chỉ kiểm tra, không đổi gì
./dat-lai-mat-khau.sh <tên-đăng-nhập>
```

Mong đợi: lệnh đầu in họ tên tài khoản và dòng `Chưa thay đổi gì`. Lệnh sau in `Đã đặt lại mật khẩu` và một dòng `Mật khẩu tạm (chỉ hiện lần này, ...)`: chép lại ngay. Đăng nhập bằng mật khẩu đó, hệ thống chuyển sang trang **Đổi mật khẩu**; đặt mật khẩu mới rồi đăng nhập lại.

- Chỉ đổi đúng tài khoản được nêu: mật khẩu, bắt đổi mật khẩu ở lần đăng nhập đầu, mở khóa đăng nhập, đăng xuất mọi phiên của tài khoản đó (hiệu lực trong vòng một phút), và ghi một dòng vào nhật ký (**Hệ thống → Nhật ký**, người thực hiện `dat-lai-mat-khau.sh`). Không gán thêm quyền hay vai trò nào.
- Tên đăng nhập không có: script báo lỗi và không đổi gì. Tài khoản đã xóa: từ chối. Tài khoản đang bị tắt: đặt lại nhưng không bật lại, cần quản trị khác bật.
- `--mat-khau-tam GIÁ_TRỊ` đặt mật khẩu tạm theo ý (từ 8 ký tự, có chữ và số); nên bỏ trống để script sinh ngẫu nhiên, vì giá trị gõ trên dòng lệnh còn lại trong lịch sử shell.
- Container khác tên mặc định: đặt `PG_CONTAINER=<tên>` trước lệnh.
