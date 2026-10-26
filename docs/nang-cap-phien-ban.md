# Nâng cấp phiên bản về sau (tại chỗ)

Tài liệu này dành cho người vận hành, dùng **mỗi lần** nhóm phát triển giao một phiên bản mới cho máy đang chạy. Nâng cấp tại chỗ: giữ nguyên máy, giữ dữ liệu, thay mã nguồn và dựng lại image. Cấu trúc CSDL tự cập nhật khi backend khởi động (migration).

Chưa có máy chạy: xem [Triển khai trên máy mới](trien-khai-may-moi.md). Thay máy cũ bằng máy mới: [Chuyển dữ liệu gốc](chuyen-du-lieu-goc.md). Chi tiết sao lưu, khôi phục, migration: [deployment.md](deployment.md), [database-migrations.md](database-migrations.md).

**Mục lục**

1. [Nguyên tắc](#1-nguyên-tắc)
2. [Phiếu kiểm: trước, trong, sau nâng cấp](#2-phiếu-kiểm-trước-trong-sau-nâng-cấp)
3. [Các bước nâng cấp](#3-các-bước-nâng-cấp)
4. [Quay lại bản cũ khi có sự cố](#4-quay-lại-bản-cũ-khi-có-sự-cố)
5. [Sự cố thường gặp khi nâng cấp](#5-sự-cố-thường-gặp-khi-nâng-cấp)

Quy ước: mã nguồn ở `/opt/congtacdang`; mọi lệnh `docker compose` chạy trong `/opt/congtacdang/docker`; bản sao lưu ở `/var/backups/congtacdang`. Thay `<phiên-bản-mới>` bằng tag hoặc mã commit nhóm phát triển giao, `<phiên-bản-cũ>` bằng mã commit đang chạy (bước 3.1).

---

## 1. Nguyên tắc

- **Luôn sao lưu ngay trước khi nâng cấp**, dù đã có sao lưu hằng đêm.
- Chỉ nâng lên đúng tag hoặc mã commit được giao, không kéo "nhánh mới nhất".
- **Đường lui duy nhất đáng tin là khôi phục bản sao lưu.** Phần `Down` của migration chỉ thêm lại cột, không khôi phục dữ liệu đã bị xóa. Một số bản có migration xóa dữ liệu (xem bước 3.4).
- Làm ngoài giờ dùng; báo người dùng trước. Gián đoạn khoảng 1 đến 3 phút khi tạo lại container; người đang mở trang sẽ phải tải lại. Phiên đăng nhập vẫn giữ.
- Dựng image **trước** khi dừng dịch vụ: dựng lỗi thì hệ thống cũ vẫn chạy bình thường.

---

## 2. Phiếu kiểm: trước, trong, sau nâng cấp

In ra, đánh dấu từng dòng, ghi ngày và người làm.

| Giai đoạn | Việc | Lệnh / nơi xem | Đạt khi |
|---|---|---|---|
| **Trước** | Nhận từ nhóm phát triển: tag hoặc mã commit mới, ghi chú phiên bản | | Có đủ cả hai. |
| Trước | Báo người dùng giờ nâng cấp | | Đã báo. |
| Trước | Bản sao lưu gần nhất toàn vẹn và mới | `ls -1t /var/backups/congtacdang` rồi `sha256sum -c SHA256SUMS` (bước 3.2) | Các dòng `OK`. |
| Trước | Ổ đĩa còn trống | `df -h /` và `df -h /var/backups` | Còn ít nhất 10 GB. |
| Trước | Ghi phiên bản đang chạy | `git -C /opt/congtacdang log -1 --format='%h %s'` | Đã ghi vào sổ. |
| Trước | Hệ thống đang khỏe | `docker compose ps` | Ba container `(healthy)`. |
| Trước | Sao lưu mới ngay trước nâng cấp | `docker/backup.sh --backup-dir /var/backups/congtacdang/truoc-nang-cap --keep 5` | Có dòng `Hoàn tất`. |
| Trước | Đã đọc ghi chú migration (có xóa dữ liệu không?) và thay đổi `.env.example` | Bước 3.4, 3.5 | Đã đọc, đã chấp thuận. |
| **Trong** | Lấy mã nguồn mới | `git fetch --all --tags` rồi `git checkout --detach <phiên-bản-mới>` | `git status --short` không in gì. |
| Trong | Bổ sung biến mới vào `docker/.env` (nếu có) | Bước 3.5 | `docker compose config --quiet` không lỗi. |
| Trong | Dựng image | `docker compose build` | Không có `ERROR`. |
| Trong | Khởi động bản mới | `docker compose up -d` | Container được tạo lại. |
| Trong | Theo dõi migration | `docker compose logs -f backend` | Không có `Critical`, `[LỖI CẤU HÌNH]`. |
| **Sau** | Ba container khỏe | `docker compose ps` | Cả ba `(healthy)`. |
| Sau | Backend khỏe | `docker compose exec backend curl --fail --silent http://localhost:5000/healthz` | In `Healthy`. |
| Sau | Migration đã áp dụng | Bước 3.8 | Migration mới nhất khớp mã nguồn. |
| Sau | Đăng nhập, trang chủ, mở một hồ sơ | Trình duyệt | Hiện đúng. |
| Sau | Mở tệp đính kèm, xuất một biểu mẫu Word, xem PDF | Trình duyệt | Mở được. |
| Sau | Ghi phiên bản mới vào sổ | `git -C /opt/congtacdang log -1 --format='%h %s'` | Đã ghi. |
| Sau | Báo người dùng đã xong | | Đã báo. |
| Sau | Hôm sau: sao lưu đêm chạy với phiên bản mới | `cat <bản-mới-nhất>/manifest.txt` | `git_commit` là mã mới. |

---

## 3. Các bước nâng cấp

### 3.1 Ghi phiên bản đang chạy

```bash
cd /opt/congtacdang
git log -1 --format='%H  %ad  %s' --date=short
git status --short
```

Ghi lại dòng đầu (đây là `<phiên-bản-cũ>`, dùng để quay lại). Lệnh thứ hai phải không in gì; có in thì có người đã sửa file tại chỗ, hỏi nhóm phát triển trước khi tiếp tục.

### 3.2 Kiểm tra bản sao lưu gần nhất

```bash
ls -1d /var/backups/congtacdang/congtacdang-* | tail -n 3
cd "$(ls -1d /var/backups/congtacdang/congtacdang-* | tail -n 1)"
sha256sum -c SHA256SUMS
cat manifest.txt
```

Mong đợi: hai dòng `OK`; `created_at` trong vòng 24 giờ; `git_commit` đúng với phiên bản đang chạy. Không có bản nào hoặc quá cũ: chưa nâng cấp; xử lý sao lưu theo [mục 10 của hướng dẫn máy mới](trien-khai-may-moi.md#10-sao-lưu-định-kỳ).

### 3.3 Sao lưu ngay trước nâng cấp

```bash
/opt/congtacdang/docker/backup.sh --backup-dir /var/backups/congtacdang/truoc-nang-cap --keep 5
```

Mong đợi: dòng cuối `Hoàn tất: /var/backups/congtacdang/truoc-nang-cap/congtacdang-...`. **Ghi lại đường dẫn này**, vì đây là bản dùng để quay lại. Bản nằm trong thư mục con riêng nên không bị cron xoay vòng xóa. Chép thêm ra máy khác nếu có thể (xem [bước 10.4](trien-khai-may-moi.md#104-đưa-bản-sao-lưu-ra-khỏi-máy)).

Lỗi: dừng lại, không nâng cấp. Nguyên nhân thường là hết ổ đĩa hoặc người dùng không thuộc nhóm `docker`.

### 3.4 Lấy mã nguồn mới và đọc ghi chú thay đổi

```bash
cd /opt/congtacdang
git fetch --all --tags
git log --oneline HEAD..<phiên-bản-mới>
```

Mong đợi: danh sách các thay đổi sẽ vào. Trống = đã đúng phiên bản hoặc sai tên; kiểm tra lại với nhóm phát triển.

Xem có migration mới không:

```bash
git diff --name-status HEAD <phiên-bản-mới> -- backend/src/CongTacDang.Infrastructure/Data/Migrations | grep -v Designer
```

Mỗi dòng bắt đầu bằng `A` là một migration mới sẽ được áp dụng khi khởi động. Không có dòng nào: lần này không đổi cấu trúc CSDL.

**Bắt buộc đọc** mục "Migration có xóa dữ liệu" của phiên bản **mới** (file ở bản cũ có thể chưa có ghi chú mới):

```bash
git show <phiên-bản-mới>:docs/database-migrations.md | sed -n '/^## Migration có xóa dữ liệu/,/^## Khôi phục/p'
```

Nếu có migration mới mà ghi chú nói xóa dữ liệu: biết rõ dữ liệu nào sẽ mất, và chắc chắn bản sao lưu ở bước 3.3 đã có. Không rõ thì hỏi nhóm phát triển, chưa nâng cấp.

### 3.5 Chuyển sang phiên bản mới và cập nhật cấu hình

```bash
git checkout --detach <phiên-bản-mới>
git status --short
git diff <phiên-bản-cũ> HEAD -- docker/.env.example docker/docker-compose.yml
```

Mong đợi: `git status` không in gì. Lệnh cuối cho thấy biến cấu hình hoặc cổng mới. Có biến mới trong `.env.example` mà cần đặt (ghi chú trong file nói rõ): thêm vào `docker/.env` bằng `nano /opt/congtacdang/docker/.env`. Chưa đặt thì biến dùng giá trị mặc định ghi trong `docker-compose.yml`. Kiểm tra:

```bash
cd /opt/congtacdang/docker
docker compose config --quiet && echo CAU_HINH_HOP_LE
grep -c 'CHANGE_ME' .env
```

Mong đợi: `CAU_HINH_HOP_LE` và `0`.

`git checkout` báo `would be overwritten`: có file bị sửa tại chỗ, dừng lại và hỏi nhóm phát triển.

### 3.6 Dựng image mới (hệ thống cũ vẫn đang chạy)

```bash
cd /opt/congtacdang/docker
docker compose build
```

Mong đợi: 5 đến 15 phút, kết thúc không có `ERROR`. Lỗi: hệ thống cũ chưa bị ảnh hưởng; kiểm tra Internet và dung lượng ổ đĩa, chạy lại. Không giải quyết được: quay mã về bản cũ (`git checkout --detach <phiên-bản-cũ>`) và báo nhóm phát triển.

### 3.7 Khởi động bản mới

```bash
docker compose up -d
docker compose logs -f backend
```

Compose tạo lại container có image đổi. Backend khởi động sẽ áp dụng migration (khi `Database__AutoMigrate=true`, mặc định). Thoát xem log bằng `Ctrl+C`. Chờ khỏe:

```bash
timeout 300 sh -c 'until [ "$(docker inspect -f "{{.State.Health.Status}}" attech-dangbo-web)" = healthy ]; do sleep 5; done' && echo GIAO_DIEN_KHOE
```

Mong đợi: `GIAO_DIEN_KHOE` trong 1 đến 3 phút (lâu hơn nếu migration phải xử lý nhiều dữ liệu).

Nếu `Database__AutoMigrate=false` trên máy này: phải áp dụng migration bằng tay trước khi dùng, xem [database-migrations.md](database-migrations.md).

Lỗi (log có `Critical`, container `Restarting`): sang [mục 4](#4-quay-lại-bản-cũ-khi-có-sự-cố).

### 3.8 Kiểm tra sau nâng cấp

```bash
docker compose ps
docker compose exec backend curl --fail --silent http://localhost:5000/healthz; echo
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tA -c "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\""'
ls /opt/congtacdang/backend/src/CongTacDang.Infrastructure/Data/Migrations | grep -v Designer | grep -v Snapshot | tail -n 1
```

Mong đợi: ba container `(healthy)`; `Healthy`; hai dòng cuối cho cùng một tên migration (dòng thứ tư có thêm đuôi `.cs`).

Trên trình duyệt (làm đúng trong phiếu kiểm): đăng nhập; trang chủ và Việc cần xử lý hiện đúng; mở một hồ sơ cũ thấy đủ dữ liệu; mở một tệp đính kèm; xuất một biểu mẫu Word và xem PDF; trang **Hệ thống → Nhật ký** ghi nhận thao tác mới.

Mong đợi mọi bước đều đạt thì xong. Ghi phiên bản mới vào sổ.

Sau vài ngày chạy ổn, dọn image cũ cho nhẹ ổ đĩa (an toàn, không xóa dữ liệu):

```bash
docker image prune -f
docker builder prune -f
```

---

## 4. Quay lại bản cũ khi có sự cố

Quay lại khi: backend không khởi động (log `Critical`), migration lỗi, hoặc phát hiện hỏng dữ liệu hay lỗi nặng sau nâng cấp. Có hai trường hợp, tùy phiên bản mới có migration mới hay không (kết quả lệnh `git diff --name-status` ở bước 3.4).

### Trường hợp A: bản mới không có migration mới

CSDL không đổi cấu trúc, chỉ cần quay mã nguồn và dựng lại:

```bash
cd /opt/congtacdang
git checkout --detach <phiên-bản-cũ>
cd docker
docker compose build
docker compose up -d
```

Sau đó làm lại bước 3.8. Lưu ý dữ liệu nhập từ lúc nâng cấp vẫn còn, vì không khôi phục gì.

### Trường hợp B: bản mới có migration mới (hoặc không chắc)

Phải **khôi phục bản sao lưu** của bước 3.3. Phần `Down` của migration không khôi phục dữ liệu đã bị xóa, nên không dùng để quay lại. Dữ liệu nhập **sau** thời điểm sao lưu bước 3.3 sẽ mất: báo người dùng trước.

Chạy lần lượt:

1. Quay mã về bản cũ và dựng lại image (chưa dừng hệ thống):

```bash
cd /opt/congtacdang
git checkout --detach <phiên-bản-cũ>
cd docker
docker compose build
```

Phải dựng xong trước khi khôi phục, vì `restore.sh` khởi động lại bằng image đang có.

2. Kiểm tra bản sao lưu (không thay đổi gì):

```bash
/opt/congtacdang/docker/restore.sh --check /var/backups/congtacdang/truoc-nang-cap/congtacdang-YYYYMMDD-HHMMSS
```

Mong đợi: checksum `OK`; dòng `Migration của bản sao lưu khớp mã nguồn.` Hiện `Bản sao lưu MỚI hơn mã nguồn`: bạn chưa quay đúng mã nguồn, quay lại bước 1.

3. Giữ lại hiện trạng của bản mới (phòng cần xem lại sau), rồi dừng ứng dụng:

```bash
/opt/congtacdang/docker/backup.sh --backup-dir /var/backups/congtacdang/truoc-quay-lai --keep 5
docker compose stop frontend backend
```

4. Làm sạch cấu trúc CSDL. Bước này cần vì `restore.sh` chỉ xóa các bảng có trong bản sao lưu; bảng do migration mới tạo sẽ còn sót lại và làm lần nâng cấp sau lỗi `already exists`. Chỉ chạy sau khi bước 3 báo `Hoàn tất`:

```bash
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"'
```

Mong đợi: `DROP SCHEMA` và `CREATE SCHEMA`. CSDL lúc này trống, **không** chạy backend cho tới khi xong bước 5.

5. Khôi phục (gõ `KHOI PHUC` khi được hỏi). Bước sao lưu dự phòng đã làm ở bước 3 nên bỏ qua ở đây (CSDL đang trống):

```bash
/opt/congtacdang/docker/restore.sh --no-safety-backup /var/backups/congtacdang/truoc-nang-cap/congtacdang-YYYYMMDD-HHMMSS
```

Mong đợi: script khôi phục CSDL và tệp đính kèm, rồi khởi động lại backend và giao diện; dòng cuối `Khôi phục hoàn tất`.

6. Kiểm tra như bước 3.8: ba container `(healthy)`, migration mới nhất là của bản cũ, đăng nhập và mở hồ sơ thấy dữ liệu như trước nâng cấp.

7. Báo nhóm phát triển: gửi `docker compose logs --tail=200 backend` của lần nâng cấp lỗi và nội dung bản sao lưu ở `truoc-quay-lai/` nếu được yêu cầu. Không nâng cấp lại cho tới khi có hướng dẫn.

Khôi phục bị lỗi giữa chừng: CSDL chạy trong một giao dịch nên giữ nguyên trạng thái trước lệnh; bản sao lưu ở `truoc-quay-lai` vẫn còn để khôi phục lại trạng thái bản mới. Không xóa các thư mục sao lưu cho tới khi hệ thống ổn định.

---

## 5. Sự cố thường gặp khi nâng cấp

| Triệu chứng | Nguyên nhân | Cách xử lý |
|---|---|---|
| `docker compose build` báo lỗi tải gói | Mất Internet hoặc DNS. | Kiểm tra mạng, chạy lại. Hệ thống cũ vẫn đang chạy. |
| Sau `up -d` backend `Restarting`, log `Critical` về migration | Migration lỗi trên dữ liệu thực. | Làm theo [mục 4](#4-quay-lại-bản-cũ-khi-có-sự-cố), trường hợp B. Gửi log cho nhóm phát triển. |
| Log `[LỖI CẤU HÌNH]` sau nâng cấp | Bản mới đổi quy tắc giá trị cấu hình. | Đọc lý do trong log, sửa `docker/.env`, `docker compose up -d --force-recreate backend`. Xem [trien-khai-may-moi.md](trien-khai-may-moi.md#42-quy-tắc-backend-từ-chối-giá-trị-yếu-hoặc-mẫu). |
| Giao diện hiện bản cũ sau khi đã nâng cấp | Trình duyệt giữ bộ nhớ đệm. | Tải lại cứng bằng `Ctrl+F5`. |
| `git checkout` báo file bị ghi đè | Có sửa đổi tại chỗ trong thư mục mã nguồn. | Không dùng `--force`. Hỏi nhóm phát triển file nào được phép giữ. |
| Hết ổ đĩa khi dựng | Bộ nhớ đệm dựng và image cũ. | `docker builder prune -f`, `docker image prune -f`, chạy lại. Xem [mục 15 máy mới](trien-khai-may-moi.md#15-sự-cố-thường-gặp). |
