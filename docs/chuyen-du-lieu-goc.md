# Chuyển dữ liệu gốc sang bản mới

Thay tại chỗ ngay trên máy đang chạy (đổi tên CSDL cũ, đường lui, làm sạch): làm theo [Thay thế tại chỗ máy đang chạy bản cũ](thay-the-may-cu.md), tài liệu này là phần tham chiếu cho công cụ chuyển.

Khi thay một máy đang chạy bản cũ bằng bản mới, không khôi phục nguyên CSDL cũ (`restore.sh` mang cả dữ liệu vận hành và nâng cấp schema tại chỗ). Cách làm ở đây: dựng **CSDL mới, sạch** đúng cấu trúc bản mới, rồi chỉ chép **dữ liệu gốc** từ bản sao lưu của máy cũ. Quy trình giống nhau cho máy thay thế và cho máy mới hoàn toàn.

| Công cụ | Việc làm |
|---|---|
| `docker/chuyen-du-lieu-goc.sh` | Kiểm tra điều kiện, chép dữ liệu gốc trong một transaction, đối chiếu số dòng, giải nén file mẫu Word |
| `docker/kiem-tra-sau-chuyen.sh` | Sau khi backend khởi động lại: kiểm tra bằng SQL và API, in ĐẠT / KHÔNG ĐẠT từng mục |
| `docker/du-lieu-goc.txt` | Phân loại từng bảng (GOC / VAN_HANH / HE_THONG), dùng chung với test `ChuyenDuLieuGocTests` |

## Quy trình

Việc của người vận hành đánh dấu **(tay)**.

1. **(tay) Máy cũ**: chạy `docker/backup.sh`, chép cả thư mục `congtacdang-YYYYMMDD-HHMMSS` (gồm `db.dump`, `uploads.tar.gz`, `SHA256SUMS`) sang máy mới, ví dụ `scp -r`.
2. **(tay) Máy mới**: lấy mã nguồn bản mới, tạo `docker/.env` mới (secret mới), dựng ảnh và khởi động bằng `docker compose build` rồi `docker compose up -d` (không dùng `deploy-ubuntu.sh`: nó dừng cả hệ thống và dựng lại không có bộ nhớ đệm). Backend khởi động trên CSDL trống, tự migrate đủ schema và tạo dữ liệu mặc định; dòng log mức Error về `__EFMigrationsHistory` ở lần đầu là bình thường. Không cần đặt `Seed__InitialAdmin__*` (tài khoản quản trị lấy từ máy cũ); không bật `Database__SeedSampleData`.
3. **(tay) Dừng backend và frontend** để không còn kết nối vào CSDL đích:
   `docker compose --env-file docker/.env -f docker/docker-compose.yml stop frontend backend`
4. **Chạy thử** (chạy đủ các bước rồi hoàn tác, không ghi gì):
   `docker/chuyen-du-lieu-goc.sh --kiem-tra /đường/dẫn/congtacdang-YYYYMMDD-HHMMSS`
5. **Chuyển thật**:
   `docker/chuyen-du-lieu-goc.sh /đường/dẫn/congtacdang-YYYYMMDD-HHMMSS`
   Bảng đối chiếu cuối cùng phải ghi **khớp** ở mọi dòng.
6. **(tay) Khởi động lại**: `docker compose --env-file docker/.env -f docker/docker-compose.yml up -d backend frontend`. Seeder bổ sung phần mới (xem mục "Sau khi khởi động lại").
7. **Kiểm tra**: `CTD_MAT_KHAU='<mật khẩu>' docker/kiem-tra-sau-chuyen.sh --tai-khoan <tên đăng nhập>`. Kết luận phải là ĐẠT.  Chạy qua cổng giao diện (mặc định `http://127.0.0.1:3001`) cũng được: mục kiểm tra backend khỏe hỏi thẳng container `attech-dangbo-api` (đổi bằng `API_CONTAINER`).
8. **(tay) Giao diện**: đăng nhập, mở các trang Người dùng, Vai trò, Đơn vị; kiểm tra thông tin đơn vị và bộ tiêu chí.

Tham số của `chuyen-du-lieu-goc.sh`: `--kiem-tra`, `--dich-csdl TEN` (mặc định CSDL của container), `--nguon-csdl TEN` (nguồn là CSDL có sẵn trong cùng container, đọc ở phiên chỉ đọc), `--uploads FILE`, `--thu-muc-upload DIR` hoặc `--volume-upload TEN`, `--repo-dir`, `--giu-csdl-tam`, `-h`. Script chạy được bằng Git Bash trên Windows vì mọi thao tác CSDL đi qua `docker exec` vào container `attech-dangbo-postgres` (đổi bằng `PG_CONTAINER`); mật khẩu CSDL không đi qua dòng lệnh.

### Điều kiện script kiểm tra trước khi ghi

- Container postgres chạy; bản dump đọc được bằng `pg_restore`; `SHA256SUMS` khớp (nếu có).
- Đích đã migrate đủ migration của mã nguồn và **không cũ hơn** nguồn. Nguồn cũ hơn thì được: các cột mới lấy giá trị mặc định.
- Mọi bảng của đích đã được phân loại trong `du-lieu-goc.txt`.
- Mọi bảng vận hành của đích **trống** (nhật ký audit, lịch sử đăng nhập và phiên đăng nhập được phép có vì do khởi tạo hoặc đăng nhập thử, sẽ bị dọn).
- Đích có không quá 1 tài khoản và 1 bản gán vai trò (tài khoản quản trị khởi tạo nếu có).
- Không còn kết nối khác vào đích (backend đã dừng).
- Đích **chưa từng nhận** dữ liệu gốc: sau khi chuyển, script ghi dấu vào chú thích của CSDL (`COMMENT ON DATABASE`, kèm số dòng từng bảng). Chạy lại lên cùng đích bị từ chối vì sẽ ghi đè thay đổi sau đó (đổi mật khẩu, gán vai trò...). Cần chuyển lại: xóa CSDL đích, dựng đích trống mới (bước 2) rồi chạy lại.

Không đạt điều kiện nào thì script dừng, nêu việc cần làm và không đổi gì.

### Cách chép

1. Bản dump khôi phục vào CSDL tạm `ctd_nguon_tam` (`--no-owner`; tự xóa khi xong). Với `--nguon-csdl`, nguồn là CSDL có sẵn, chỉ đọc.
2. Mỗi bảng GOC: so sánh cột bằng `information_schema` và chép **các cột chung** bằng `COPY`, giữ nguyên GUID để khóa ngoại khớp. Cột chỉ có ở nguồn bị bỏ (in ra). Cột chỉ có ở đích lấy mặc định của schema; nếu cột đó NOT NULL và không có mặc định, hoặc cột chung khác kiểu, script **dừng** và in tên cột (không tự bịa giá trị; có chỗ khai báo giá trị mặc định có lý do trong script, `DEFAULT_VALUE`). Nguồn thiếu cả bảng thì bỏ qua và giữ dữ liệu mặc định của đích.
3. Một transaction duy nhất: dọn nhật ký và phiên đăng nhập của đích, làm rỗng bảng gốc (theo thứ tự ngược khóa ngoại), chép theo thứ tự khóa ngoại trong `du-lieu-goc.txt`, kiểm số dòng từng bảng, áp dụng bản vá (bên dưới), đặt lại sequence (nếu có), `ANALYZE`, ghi dấu. Lỗi ở đâu cũng hoàn tác toàn bộ; khóa ngoại luôn được CSDL kiểm tra, không tắt ràng buộc.
4. Bảng đối chiếu nguồn / lọc bỏ / đích; lệch thì script báo lỗi.
5. Giải nén từ `uploads.tar.gz` **chỉ** `word-templates/` (file mẫu Word) và `login-background/` (ảnh nền đăng nhập, thuộc `organization_settings`) vào volume upload của đích (hoặc `--thu-muc-upload`), rồi kiểm các tệp mà bản ghi phiên bản file mẫu trỏ tới có trên đĩa. Tệp đính kèm hồ sơ **không** khôi phục.

### Bản vá dữ liệu theo migration mà nguồn chưa có

Migration của bản mới không chạy trên dữ liệu chép sang, nên script áp dụng lại phần chạm dữ liệu gốc, **chỉ khi nguồn chưa có migration đó** (đọc từ `__EFMigrationsHistory` của nguồn):

| Migration | Việc |
|---|---|
| `20261023095959_BoChamTheoTruc09B` | Không chép bộ tiêu chí chấm theo trục (`SelfScoreForm = '09B'`) và phiên bản file mẫu `MAU_09B`; `SourceSetId` trỏ vào bộ bị bỏ thì đặt rỗng. Kỳ và hồ sơ dùng bộ đó là dữ liệu vận hành nên vốn không chép. Tệp `word-templates/mau_09b/` nếu có vẫn được giải nén, không dùng đến |
| `20261023020159_AddPositionIsHead` | Đánh dấu `IsHead` cho các chức vụ cấp trưởng của danh mục mặc định |
| `20261023091047_PositionDefaultWorkflowProfile` | `DefaultWorkflowProfileCode = 'pho-giam-doc'` cho chức vụ Phó Giám đốc |
| `20261026081315_ChoVanPhongDangUyXacNhanPhieuDoiSoat` | Gán quyền xác nhận phiếu đối soát cho Văn phòng Đảng ủy nếu mã quyền đã có trong nguồn |

Thêm migration mới chạm dữ liệu gốc thì thêm bản vá tương ứng vào `PATCH_SQL` của script.

## Quyết định về `permissions`

Bảng `permissions` **được chép từ máy cũ** (không để seeder sinh sẵn): `role_permissions` tham chiếu `permission_id` nên phải giữ nguyên khóa. Mã quyền mới của bản mới không có trong nguồn, nên khi backend khởi động lại seeder thấy là "mã quyền mới" và theo cơ chế sẵn có thêm vào đúng các vai trò mặc định có mã đó. Nếu để đích giữ sẵn bản danh mục đầy đủ do seeder sinh, các mã mới đã tồn tại nên quyền mới sẽ **không** được gán cho vai trò mặc định nào. Các vai trò tự tạo của quản trị giữ nguyên, không bị thêm quyền.

## Sau khi khởi động lại

Seeder (không cần thao tác tay) bổ sung: mã quyền mới; vai trò mặc định chưa có (ví dụ Cơ quan chuyên môn); quyền mới của vai trò mặc định (ví dụ Văn phòng Đảng ủy: xác nhận phiếu đối soát, xác nhận phiếu kiểm soát, kết luận hiệu chuẩn). Vai trò, thông tin đơn vị, bộ tiêu chí, chức vụ, bố cục biểu mẫu lấy từ máy cũ không bị ghi đè. Bảng nào nguồn không có thì seeder tạo mặc định. Test `KhoiDongLaiSauChuyen_SeederBoSungQuyenMoiVaVaiTroMacDinhMoi` kiểm hành vi này.

Nếu nguồn không có tài khoản quản trị hoạt động, đặt `Seed__InitialAdmin__*` trong `.env` trước khi khởi động lại để tạo quản trị ban đầu.

Phiên đăng nhập cũ không chuyển sang: mọi người đăng nhập lại bằng mật khẩu hiện có (mật khẩu băm được chép nguyên, không bắt đổi).

## Phân loại bảng

Nguồn đúng là `docker/du-lieu-goc.txt`; bảng dưới là bản dễ đọc, theo thứ tự chép. Thêm bảng vào `CongTacDangDbContext` mà chưa thêm dòng ở đó thì test `ChuyenDuLieuGocTests` không đạt; test cũng kiểm bảng gốc không tham chiếu khóa ngoại tới bảng không phải gốc và thứ tự cha trước con.

| Bảng | Loại | Lý do |
|---|---|---|
| `org_unit_types` | GOC | Loại đơn vị (danh mục tổ chức) |
| `administrative_departments` | GOC | Cây đơn vị chính quyền |
| `party_cells` | GOC | Cây tổ chức Đảng |
| `form_layouts` | GOC | Bố cục biểu mẫu; chức vụ và tài khoản tham chiếu |
| `positions` | GOC | Danh mục chức vụ |
| `roles` | GOC | Vai trò do quản trị đặt |
| `permissions` | GOC | Danh mục quyền; chép từ máy cũ để role_permissions khớp khóa, mã quyền mới do khởi động lại bổ sung và gán cho vai trò mặc định |
| `role_permissions` | GOC | Quyền của từng vai trò |
| `party_member_profiles` | GOC | Tài khoản cán bộ kèm mật khẩu băm |
| `member_positions` | GOC | Chức vụ của từng cán bộ (kiêm nhiệm) |
| `user_role_assignments` | GOC | Bản gán vai trò: người, vai trò, phạm vi, thời hạn |
| `organization_settings` | GOC | Thông tin đơn vị |
| `criteria_sets` | GOC | Bộ tiêu chí và các phiên bản |
| `holidays` | GOC | Ngày nghỉ lễ, ngày làm việc |
| `word_template_versions` | GOC | Phiên bản file mẫu Word; tệp nằm ở thư mục word-templates trên đĩa |
| `evaluation_periods` | VAN_HANH | Kỳ đánh giá |
| `evaluation_records` | VAN_HANH | Hồ sơ đánh giá |
| `evaluation_tasks` | VAN_HANH | Nhiệm vụ của hồ sơ |
| `evaluation_record_histories` | VAN_HANH | Lịch sử hồ sơ |
| `evaluation_form_history` | VAN_HANH | Lịch sử biểu mẫu của hồ sơ |
| `evaluation_external_results` | VAN_HANH | Kết quả do cấp trên ghi nhận |
| `evaluation_appeals` | VAN_HANH | Kiến nghị về kết quả |
| `improvement_plans` | VAN_HANH | Kế hoạch 30-60-90 ngày |
| `task_change_requests` | VAN_HANH | Đề nghị điều chỉnh việc |
| `task_attachments` | VAN_HANH | Tệp đính kèm hồ sơ; không chuyển tệp trên đĩa |
| `collective_evaluation_records` | VAN_HANH | Hồ sơ tập thể |
| `collective_evaluation_items` | VAN_HANH | Dòng của hồ sơ tập thể |
| `evaluation_meetings` | VAN_HANH | Biên bản hội nghị |
| `evaluation_meeting_vote_summaries` | VAN_HANH | Kết quả kiểm phiếu hội nghị |
| `expert_crosschecks` | VAN_HANH | Phiếu đối soát chuyên ngành (Mẫu 03) |
| `expert_crosscheck_items` | VAN_HANH | Dòng của phiếu đối soát |
| `control_checks` | VAN_HANH | Phiếu kiểm soát (Mẫu 19) |
| `control_check_items` | VAN_HANH | Dòng của phiếu kiểm soát |
| `control_check_records` | VAN_HANH | Hồ sơ gắn với phiếu kiểm soát |
| `calibration_sessions` | VAN_HANH | Biên bản hiệu chuẩn (Mẫu 20) |
| `calibration_attendees` | VAN_HANH | Thành phần dự hiệu chuẩn |
| `calibration_cases` | VAN_HANH | Trường hợp hiệu chuẩn |
| `calibration_records` | VAN_HANH | Hồ sơ gắn với biên bản hiệu chuẩn |
| `calibration_summary_rows` | VAN_HANH | Dòng tổng hợp của biên bản hiệu chuẩn |
| `report_drafts` | VAN_HANH | Bản nháp báo cáo |
| `audit_logs` | VAN_HANH | Nhật ký audit; máy mới bắt đầu nhật ký riêng, nhật ký máy cũ giữ trong bản sao lưu |
| `login_events` | VAN_HANH | Lịch sử đăng nhập |
| `refresh_tokens` | VAN_HANH | Phiên đăng nhập; người dùng đăng nhập lại sau khi chuyển |
| `__EFMigrationsHistory` | HE_THONG | Lịch sử migration của EF; do migration của bản mới sinh ra |

Các bảng chọn phương án an toàn: `audit_logs` (nhật ký máy cũ giữ trong bản sao lưu, máy mới bắt đầu nhật ký riêng), `login_events` và `refresh_tokens` (không chép, người dùng đăng nhập lại), `task_attachments` (không chép bản ghi vì tệp trên đĩa không chuyển), `evaluation_form_history` và các bảng Mẫu 03, 19, 20 (gắn với hồ sơ nên là vận hành). `form_layouts`, `holidays` và `word_template_versions` là cấu hình do quản trị đặt nên thuộc dữ liệu gốc dù có thể rỗng.

## Giới hạn và rủi ro

- Nội dung `criteria_sets.Content` (jsonb) và các cột jsonb khác được chép nguyên văn; nếu bản mới đổi định dạng jsonb mà nguồn cũ chưa có thì cần bản vá riêng. Với hai nguồn đã diễn tập, bộ tiêu chí đọc được qua API.
- Kỳ đang mở và hồ sơ của máy cũ **không** sang máy mới. Kỳ dùng bộ tiêu chí chấm theo trục (09B) cũng không còn trong bản mới.
- Phiên bản file mẫu Word trỏ tới tệp thiếu trên đĩa: script cảnh báo; chép tệp vào kho tệp hoặc tải lại mẫu trên giao diện.
- Cột khác kiểu giữa nguồn và đích, hoặc cột đích NOT NULL không mặc định, làm script dừng: cần bổ sung `COL_EXPR`, `DEFAULT_VALUE` hoặc bản vá.
- Script không dừng hay khởi động backend; người vận hành làm tay như các bước trên.

## Kết quả diễn tập

Diễn tập trên Windows (Git Bash) bằng container `attech-dangbo-postgres`, backend chạy từ mã nguồn ở cổng 5100; đích là CSDL tạm `ctd_moi_thu`, `ctd_moi_thu2`, nguồn tạm `ctd_cu_thu`; đã dọn hết khi xong. Không đụng CSDL đang dùng.

### Lượt 1: bản mô phỏng máy cũ (`congtacdang_db`, thiếu 10 migration)

- Nguồn: `pg_dump` của `congtacdang_db` (chỉ đọc), khôi phục vào `ctd_cu_thu`; trên bản sao đặt mật khẩu thử bằng `crypt('…', gen_salt('bf',10))` cho 55 tài khoản, thêm 2 bản ghi `word_template_versions` (MAU_01, MAU_09B) và một `uploads.tar.gz` giả gồm `word-templates/` và một tệp đính kèm; dump lại thành thư mục kiểu `backup.sh` kèm `SHA256SUMS`.
- Đích `ctd_moi_thu` dựng bằng backend (`Database__AutoMigrate=true`, `Database__SeedSampleData=false`): 23 migration, nguồn 13.
- `--kiem-tra`: đạt, đích không đổi. Chuyển thật:

| Bảng | Nguồn | Lọc bỏ | Đích |
|---|---:|---:|---:|
| org_unit_types | 11 | 0 | 11 |
| administrative_departments | 31 | 0 | 31 |
| party_cells | 24 | 0 | 24 |
| form_layouts | 3 | 0 | 3 |
| positions | 58 | 0 | 58 |
| roles | 9 | 0 | 9 |
| permissions | 31 | 0 | 31 |
| role_permissions | 47 | 0 | 47 |
| party_member_profiles | 55 | 0 | 55 |
| member_positions | 138 | 0 | 138 |
| user_role_assignments | 139 | 0 | 139 |
| organization_settings | 1 | 0 | 1 |
| criteria_sets | 2 | 1 (09B) | 1 |
| holidays | 0 | 0 | 0 |
| word_template_versions | 2 | 1 (MAU_09B) | 1 |

- Bản vá: 16 chức vụ có `IsHead`, 1 chức vụ có `DefaultWorkflowProfileCode`. Giải nén `word-templates` (2 tệp), không giải nén tệp đính kèm; tải file mẫu MAU_01 qua API trả đúng nội dung.
- Khởi động lại backend: seeder tạo 7 mã quyền mới (`evaluation.crosscheck.*`, `evaluation.calibration.*`, `evaluation.control.*`), vai trò Cơ quan chuyên môn, và thêm quyền mới vào Văn phòng Đảng ủy (3 quyền) và Phòng TCCB-LĐ (3 quyền); báo "đã có tài khoản quản trị" nên không tạo thêm.
- `kiem-tra-sau-chuyen.sh`: ĐẠT cả 9 mục. Đăng nhập qua API bằng `loan.ctp`, `hung.dt`, `quantri` (mật khẩu thử) thành công, sai mật khẩu trả 401; danh sách người 55, danh sách vai trò 10, bộ tiêu chí đọc được, tạo kỳ mới và `readiness` trả `ready: true`.
- Chạy lại lên đích đã chuyển: bị từ chối ("Đích đã nhận dữ liệu gốc…"), đích không đổi.
- Đích có dữ liệu mẫu (13 vấn đề: hồ sơ, nhiệm vụ... khác rỗng): bị từ chối, không đổi gì.
- Bản dump cắt cụt: dừng ở bước đọc bằng `pg_restore`. Checksum sai: dừng ở `sha256sum`. Đường dẫn không tồn tại: thông báo rõ. Mọi trường hợp đều không còn CSDL tạm.
- Nguồn có một dòng `member_positions` mồ côi (thêm vào bản sao `ctd_cu_thu`): khóa ngoại báo lỗi khi chép, transaction hoàn tác; đích vẫn 1 tài khoản, 58 chức vụ, không có dấu đã chuyển.
- `--kiem-tra --nguon-csdl congtacdang_db` trực tiếp (phiên chỉ đọc): đạt.

### Lượt 2: bản sao lưu thật của máy cũ (12 migration, mới nhất `AddOrganizationAppraisalUnit`)

Nguồn: thư mục `backup.sh` thật (`db.dump`, `uploads.tar.gz`, `SHA256SUMS`), đích `ctd_moi_thu2`. Nguồn 12 migration, đích 23.

| Bảng | Nguồn | Lọc bỏ | Đích |
|---|---:|---:|---:|
| org_unit_types | 11 | 0 | 11 |
| administrative_departments | 31 | 0 | 31 |
| party_cells | 24 | 0 | 24 |
| form_layouts | 3 | 0 | 3 |
| positions | 58 | 0 | 58 |
| roles | 9 | 0 | 9 |
| permissions | 31 | 0 | 31 |
| role_permissions | 46 | 0 | 46 |
| party_member_profiles | 55 | 0 | 55 |
| member_positions | 138 | 0 | 138 |
| user_role_assignments | 139 | 0 | 139 |
| organization_settings | 1 | 0 | 1 |
| criteria_sets | 2 | 1 (09B) | 1 |
| holidays | 0 | 0 | 0 |
| word_template_versions | 0 | 0 | 0 |

- Chênh lệch cột: `form_layouts` bỏ `HeaderStyle`, `ShowCellConfirmation`; `criteria_sets` bỏ `SelfScoreForm`; `positions` lấy mặc định `IsHead`, `DefaultWorkflowProfileCode`; `organization_settings` lấy mặc định 5 cột ảnh nền đăng nhập. Không cột nào làm script dừng.
- Dữ liệu vận hành của máy cũ (1 kỳ, 11 hồ sơ...) không sang đích: kỳ = 0, hồ sơ = 0. `uploads.tar.gz` thật chỉ có tệp đính kèm, không có `word-templates`: không giải nén gì.
- Mật khẩu băm giữ nguyên: băm MD5 của (Id, Username, PasswordHash, SecurityStamp) của 55 tài khoản trùng giữa nguồn và đích; tương tự từng dòng của `user_role_assignments` và `member_positions`.
- Khởi động lại: seeder tạo cùng 7 mã quyền, vai trò Cơ quan chuyên môn, quyền mới cho vai trò mặc định. `kiem-tra-sau-chuyen.sh` (chưa có mật khẩu thật nên mục đăng nhập ghi CHƯA KIỂM): ĐẠT các mục còn lại; đăng nhập sai mật khẩu trả 401 (tài khoản có thật).
- Vì không biết mật khẩu thật, trên `ctd_moi_thu2` (CSDL thử bỏ đi) đặt mật khẩu thử cho 2 tài khoản rồi đăng nhập `loan.ctp`, `hung.dt` thành công, danh sách người 55, danh sách kỳ rỗng, tạo kỳ mới và `readiness` đạt.

### Lỗi gặp khi diễn tập và cách xử lý

- Đích dựng ở môi trường Development có dữ liệu mẫu bật sẵn nên không sạch: đặt `Database__SeedSampleData=false` khi dựng đích (Compose Production mặc định đã tắt).
- Tài khoản nguồn có `MustChangePassword = true` nên API trả `PASSWORD_CHANGE_REQUIRED` khi thử: đúng hành vi (cờ được chép nguyên); trên bản sao nguồn đặt lại cờ để thử.
- Đường dẫn nguồn không tồn tại báo lỗi `cd` khó hiểu: đã thêm kiểm tra riêng.

### Lượt 3: thay tại chỗ bằng bộ Compose thật

Diễn tập trọn quy trình [Thay thế tại chỗ](thay-the-may-cu.md) (đổi tên CSDL cũ, đích trống, chuyển từ bản sao lưu thật, quay lại bản cũ và tiến lại): `--kiem-tra` 44 giây, chuyển thật 47 giây, bảng đối chiếu khớp như lượt 2, băm mật khẩu 55 tài khoản trùng nguồn. `--kiem-tra` không để lại dấu (không có `ctd_nguon_tam`, không có dấu đã chuyển, số tài khoản vẫn 0). Bản dump cắt cụt bị chặn ở bước 1, đích giữ nguyên.

Lỗi gặp và cách xử lý: `kiem-tra-sau-chuyen.sh` mặc định gọi qua cổng giao diện 3001 nhưng cổng này không có `/healthz` (trả 307) và `/api/healthz` (trả 404) nên mục "Backend khỏe" luôn không đạt; script nay hỏi thẳng container backend khi hai đường đó không trả `Healthy`.
