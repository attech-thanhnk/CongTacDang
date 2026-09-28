# Báo cáo: Nền tảng file & sinh biểu mẫu

- **Branch:** `feat/files-docs` (tạo từ `main` `0479b7c`)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `00402da`)
- **Ngày:** 2026-09-28

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-36 | done | Tệp gắn **đối tượng tổng quát** (`OwnerType` + `OwnerId`: `General`, `EvaluationRecord`, `EvaluationTask`), nhiều tệp trên một đối tượng. **Phiên bản:** thay tệp = bản ghi mới (`FileGroupId`, `VersionNumber`), bản cũ giữ lại và đánh dấu `IsSuperseded` + `SupersededAt/SupersededById`; người/thời điểm của từng phiên bản là `UploadedById/UploadedAt`. **Xóa** = xóa mềm mọi phiên bản (file vật lý giữ lại). Mọi thao tác kiểm tra quyền qua `IAccessPolicy.CanAccessAttachment` (giữ nguyên luật của task 04); tải lên gắn hồ sơ/nhiệm vụ cần quyền `Update` trên hồ sơ (`CanAccessRecord`). Liên kết qua `EvaluationTask.AttachmentId` được tính theo **nhóm phiên bản** (task trỏ vào bản cũ vẫn thấy bản hiện hành). API mới: danh sách theo đối tượng, tải bản hiện hành, lịch sử, tải phiên bản cụ thể, tải phiên bản mới. Giữ `IFileStorageService` + `LocalFileStorageService`. | `TaskAttachment.cs`, `AttachmentOwnerTypes.cs`, `AttachmentService.cs`, `AttachmentController.cs`, `SpecificRepositories.cs` (interface `IAttachmentVersionRepository` + repo), `CongTacDangDbContext.cs` |
| T-07 (phần còn lại) | **partial** | Lệnh xóa `MinioFileStorageService.cs` (`git rm`) **bị môi trường từ chối** (auto mode classifier). Không tìm cách lách; tệp và khối `Storage:Minio` trong `appsettings.json` được giữ nguyên. Adapter không được đăng ký DI nên không ảnh hưởng chạy. | — |
| T-47 | done | `RegisterTasksAsync`/`SubmitSelfScoreAsync`: mọi `AttachmentId` **mới** client gửi (khác giá trị đang lưu) phải tồn tại (400) và người gửi phải có quyền `Update` trên tệp theo `IAccessPolicy` (người tải lên, chủ hồ sơ đang gắn tệp, quản trị) — 403. Id đã gắn sẵn không kiểm tra lại để dữ liệu cũ không chặn việc lưu. | `EvaluationService.cs`, `AttachmentService.EnsureCanLinkAttachmentsAsync` |
| T-37 | done | `DocxTemplateEngine` viết lại: điền theo **Content Control (SDT) định danh bằng Tag** qua OpenXML SDK sẵn có (không thêm thư viện; `DocumentFormat.OpenXml` 3.5.1, MIT). Hỗ trợ trường đơn (`TAG`), bảng lặp dòng (`repeat:TÊN`), khối điều kiện (`if:TÊN`/`ifnot:TÊN`); giá trị `null` giữ chữ mặc định trong template; control được gỡ sau khi điền. Bỏ `CreateMasterMau*Template`, `EnsureMasterTemplatesExist`, `EnsureOrientation` — template là tệp `.docx` thật, thiếu tệp thì báo lỗi rõ ràng. 5 template (01, 02, 10, 11, 13) chuyển sang Content Control, giữ nguyên bố cục/nội dung. Mỗi mẫu = một lớp dữ liệu (`Documents/Forms/MauXXData.cs`, gắn `[TemplateField]`/`[TemplateCollection]`/`[TemplateCondition]`) dựng từ dữ liệu đã lưu + một template. Chữ in cứng (tên cơ quan, dòng chấm, “Lãnh đạo đơn vị”, “CHI BỘ CƠ SỞ TRỰC THUỘC”…) nằm trong template, không còn trong code. Tài liệu `docs/bieu-mau.md`. | `Services/DocxTemplateEngine.cs`, `Documents/*`, `Templates/Word/*.docx`, `ReportService.cs`, `Api/Extensions/DocumentExtensions.cs` |
| T-39 (phần `DocxTemplateEngine`) | done | Build 0 warning (trước: 5× CS8602 trong `DocxTemplateEngine`). | `DocxTemplateEngine.cs` |
| T-38 | done (PDF thật: chưa chạy) | **Một nguồn số liệu:** Mẫu 02 dùng `SelfScore` đã lưu (bỏ công thức trọng số Khung 2 cố định); Mẫu 10/11/13 đọc giá trị đã lưu (xem “Khác biệt”). **PDF phía máy chủ:** `LibreOfficePdfConverter` gọi `soffice --headless --convert-to pdf`, thư mục tạm + hồ sơ LibreOffice riêng mỗi lần, timeout, giới hạn đồng thời, dọn thư mục; đường dẫn `soffice` từ cấu hình hoặc tự tìm; thiếu LibreOffice → **503** kèm thông báo, không ảnh hưởng xuất Word/Excel. API xuất nhận `?format=docx|xlsx|pdf` (Word 01/02/10/11/13 và Excel 14/15/15A/15B/16). LibreOffice + font Liberation/DejaVu trong `Dockerfile.backend`; mục cài đặt trong `docs/deployment.md`. **Frontend:** `EvaluationPdfModal` giữ các nút nhưng hiển thị/tải PDF từ máy chủ; bản in HTML (`EvaluationPrintTemplate`, `window.print`) bị bỏ. | `LibreOfficePdfConverter.cs`, `ReportService.cs`, `ExportReportController.cs`, `IReportService.cs`, `docker/Dockerfile.backend`, `docs/deployment.md`, `EvaluationPdfModal.tsx`, `EvaluationPrintTemplate.tsx`, `reportService.ts` |
| T-30 (phần Excel) | done | Excel 14/15/15A/15B/16 nhận `periodId` (mặc định kỳ đang hoạt động, 404 nếu không có) và `branchId`; phạm vi xác định qua `IReportAccessService.ResolveBranchExportScopeAsync` (dựa trên `IAccessPolicy.CanAccessBranch(Export)`): cấp cao xuất toàn Đảng bộ hoặc Chi bộ bất kỳ, **Bí thư Chi bộ xuất Chi bộ mình** (không truyền `branchId` thì mặc định Chi bộ mình), người khác 403. `ReportService` lọc hồ sơ/Chi bộ/cán bộ theo phạm vi; tiêu đề và tên tệp theo kỳ + phạm vi (bỏ chữ cố định `Q3_2026`, `QUÝ III/2026`). | `ReportService.cs`, `IReportService.cs`, `ExportReportController.cs` |
| T-42 (phần 05) | done | 38 test mới (37 chạy, 1 bỏ qua có lý do): bộ điền template (trường đơn, trường bị Word tách run, null giữ mặc định, xuống dòng, bảng lặp dòng, danh sách rỗng, điều kiện inline/khối, control không Tag), đối chiếu Tag của 5 template với lớp dữ liệu (không thiếu/không thừa), tài liệu xuất hợp lệ theo `OpenXmlValidator`, Mẫu 02/10 dùng giá trị đã lưu; phiên bản tệp (tải lên gắn đối tượng, thay, lịch sử, tải phiên bản, xóa mềm) và quyền; T-47; chuyển PDF (thiếu LibreOffice → 503; chuyển thật **skip** khi không có `soffice`). | `tests/.../DocumentTemplateTests.cs`, `AttachmentVersioningTests.cs`, `PdfConversionTests.cs` |

### T-07 — phần còn lại (partial)
- Việc cần làm (người điều phối/người dùng chạy tay): `git rm backend/src/CongTacDang.Infrastructure/Services/MinioFileStorageService.cs` và bỏ khối `"Storage": { "Minio": {...} }` trong `backend/src/CongTacDang.Api/appsettings.json`. Không có code nào tham chiếu tới class/khối cấu hình này (đã kiểm tra bằng grep), build sẽ vẫn sạch.
- Dịch vụ `minio`/`minio-init` trong `docker/docker-compose.yml`, biến `MINIO_*` trong `docker/.env.example` và câu về MinIO trong `docs/deployment.md` (mục “Công cụ tùy chọn”) thuộc task 06 — nên bỏ cùng lúc.
- **Đề xuất nếu sau này cần object storage:** thêm một triển khai `IFileStorageService` dùng SDK chính thức (`Minio` .NET SDK hoặc `AWSSDK.S3`, ký SigV4), bucket riêng tư, backend là cổng duy nhất tải/đọc tệp (giữ `GetDownloadUrlAsync` trả URL API, không cấp presigned URL ra ngoài LAN); cấu hình chọn provider (`Storage:Provider=Local|S3`), chuyển dữ liệu bằng công cụ sao chép theo `ObjectKey` (khóa không đổi giữa hai kho).

### T-36 — tương thích dữ liệu cũ và cách chuyển đổi
- **Không xóa cột cũ.** `RecordId`, `RelatedId`/`TaskId`, `EvaluationTask.AttachmentId` giữ nguyên và vẫn được ghi cho tệp mới (tải lên gắn nhiệm vụ ghi cả `RecordId` của hồ sơ chứa nhiệm vụ) để các truy vấn/luật quyền hiện có tiếp tục đúng.
- **Không bắt buộc backfill.** Cột mới đều nullable hoặc có mặc định phù hợp: `FileGroupId = null` ⇒ tệp tự là nhóm của chính nó (`GroupId = Id`); `VersionNumber` mặc định 1; `IsSuperseded` mặc định `false` ⇒ bản hiện hành; `OwnerType = null` ⇒ đối tượng suy ra từ `RecordId` (hồ sơ) → `RelatedId` (nhiệm vụ) → `General`. Danh sách theo đối tượng gồm cả tệp cũ gắn qua `EvaluationTask.AttachmentId`.
- Backfill tùy chọn (chạy sau migration nếu muốn dữ liệu tường minh):
  ```sql
  UPDATE task_attachments SET "OwnerType" = 'EvaluationRecord', "OwnerId" = "RecordId" WHERE "OwnerType" IS NULL AND "RecordId" IS NOT NULL;
  UPDATE task_attachments SET "OwnerType" = 'EvaluationTask', "OwnerId" = "RelatedId" WHERE "OwnerType" IS NULL AND "RelatedId" IS NOT NULL;
  UPDATE task_attachments SET "OwnerType" = 'General' WHERE "OwnerType" IS NULL;
  UPDATE task_attachments SET "FileGroupId" = "Id" WHERE "FileGroupId" IS NULL;
  ```
- Về sau có thể chuyển `EvaluationTask.AttachmentId` sang tệp gắn `OwnerType=EvaluationTask` (nhiều tệp/nhiệm vụ) rồi mới bỏ cột — cần frontend Bước 1/2 dùng API mới, ngoài phạm vi task này.
- **Quyền trên phiên bản:** mọi phiên bản của một tệp dùng chung quyền của phiên bản hiện hành (xem/tải = `Read`, thay = `Update`, xóa = `Delete`). Thay tệp chỉ dành cho người có `Update` trên bản hiện hành (người tải lên bản đó, chủ hồ sơ liên quan, quản trị).

### T-37 — quy ước và chuyển đổi template
- Tag giữ tên placeholder cũ (`FULL_NAME`, `T_NAME`, `R_STT`…) để dễ đối chiếu; khối lặp: `repeat:TASKS` (Mẫu 01/02), `repeat:RECORDS` (Mẫu 11/13); điều kiện: `if:T_IS_EXCEED`/`ifnot:T_IS_EXCEED` (cột “Ghi nhận vượt chuẩn” Mẫu 02).
- 5 template được chuyển bằng một công cụ chạy một lần (OpenXML SDK, không commit): tách run tại `{{TAG}}`, thay bằng Content Control cùng định dạng run, bọc dòng mẫu của bảng bằng control `repeat:`. Các phần khác của tệp (styles, header/footer, numbering, theme…) không bị sửa; trong `document.xml` chỉ các vị trí placeholder thay đổi. Template gốc và bản chuyển đều hợp lệ theo `OpenXmlValidator` (0 lỗi); tài liệu xuất cũng 0 lỗi.
- Các chỗ template cũ **gắn nhầm trường** đã được gắn đúng khi chuyển (bố cục/chữ in sẵn không đổi): “Đơn vị công tác: {{FULL_NAME}}” (Mẫu 02, 10) → `DEPARTMENT`; 9 ô điểm Mẫu 10 cùng dùng `{{SCORE}}` → 9 trường riêng; “Ý kiến của Người trực tiếp…/Kết luận của Cơ quan thẩm định: {{FULL_NAME}}” (Mẫu 10) → `SUPERVISOR_COMMENT`/`APPRAISAL_COMMENT`; cột (4a)/(4b) Mẫu 11 → `R_GENERAL_SCORE`/`R_TASKS_SCORE` (thêm control vào ô (4b) đang trống); “Số phiếu không hợp lệ: {{TOTAL_VOTERS}}” (Mẫu 13) → `INVALID_BALLOTS`.

### Xuất thử 5 mẫu — khác biệt so với bản xuất cũ
Xuất cùng một bộ dữ liệu mẫu bằng bộ máy cũ (trước khi sửa) và mới, so sánh văn bản từng đoạn/ô (bố cục, bảng, chữ in sẵn giống hệt; chỉ khác các giá trị dưới đây). **PDF chưa xuất thử được vì máy không có LibreOffice.**

| Mẫu | Vị trí | Bản cũ | Bản mới | Lý do |
|---|---|---|---|---|
| 01, 02, 10, 11, 13 | Quý | `Quý Quy3`, `QUÝ Quy3 NĂM 2026` | `Quý 3`, `QUÝ 3 NĂM 2026` | Bản cũ in tên enum. |
| 01, 02, 10, 11, 13 | Số thập phân | `30.0`, `27.30` | `30,0`, `27,30` | Định dạng cố định dấu phẩy như chữ in sẵn `70,0` trong template (bản cũ phụ thuộc culture của máy chủ). |
| 01 | Cột Mã SP / Trục KQ / Vai trò | `SP-01`, `T1`, `Chủ trì`/`Phối hợp` | để trống | Giá trị bịa theo số thứ tự, không có dữ liệu lưu. Nếu nghiệp vụ cần, bổ sung trường lưu rồi điền. Các cột có chữ cố định (Tiêu chí vượt chuẩn, Minh chứng, Thẩm quyền xác nhận, Chuẩn đạt khi trống) vẫn in như cũ nhưng chữ nay nằm trong template. |
| 02 | Kết quả SP (%) / Điểm đạt | tính lại theo trọng số Khung 2 cố định (ví dụ `91%`, `27.30`) | `SelfScore/Trọng số` và `SelfScore` đã lưu (ví dụ `91,0%`, `27,30`) | T-38. Khác số khi hồ sơ không thuộc Khung 2. |
| 02, 10 | Đơn vị công tác | họ tên cán bộ | tên đơn vị | Template cũ gắn nhầm trường. |
| 10 | 9 ô điểm | cả 9 ô = điểm thẩm định | tự chấm TC chung / chuyên môn / tổng từ giá trị đã lưu; điểm thẩm định tổng + chênh lệch tổng; ô thẩm định từng nhóm để `…` | Template cũ gắn nhầm; chưa lưu điểm thẩm định theo nhóm. |
| 10 | Ý kiến người trực tiếp sử dụng / Kết luận cơ quan thẩm định | họ tên cán bộ | để dòng chấm / ý kiến thẩm định đã lưu | Template cũ gắn nhầm trường. |
| 11 | Cột (4a)/(4b) | (4a) = tổng điểm tự chấm `93.9đ`, (4b) trống | (4a) điểm TC chung, (4b) điểm chuyên môn | Đúng tiêu đề cột, giá trị đã lưu. |
| 13 | Số phiếu không hợp lệ | = tổng số người bỏ phiếu | `…..` | Chưa lưu dữ liệu. |
| 13 | Số người bỏ phiếu khi không có dữ liệu | mặc định bịa `12`/`10` | lấy `TotalVoters` đã lưu, không có thì sĩ số Chi bộ, không có nữa thì `…..` | Bỏ số bịa. |

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx` | pass | 0 error, **0 warning** (baseline trên `main`: 5 warning CS8602 trong `DocxTemplateEngine`). |
| `dotnet test backend/CongTacDang.slnx` | pass | 75 test: 74 pass, 1 skip (`PdfConversionTests.Mau01_ConvertsToPdf_AndCleansWorkDirectory` — máy không cài LibreOffice). 38 test mới của task 05. |
| `npx tsc --noEmit` (frontend) | pass | Chạy `npm ci` trước; tạo `frontend/next-env.d.ts` chuẩn (bị `.gitignore`, không commit). |
| Xuất thử 5 mẫu DOCX, so với bản cũ | đã chạy | Bằng chương trình tạm ngoài repo, dữ liệu mẫu trong bộ nhớ, không dùng CSDL. Kết quả ở mục “Khác biệt”. |
| Xuất PDF thật | **không chạy** | Máy không có LibreOffice. Đường thiếu LibreOffice (503) đã test. Cần thử trên image Docker/máy chủ có LibreOffice. |
| Build image Docker có LibreOffice | không chạy | Không build image (tải gói lớn); `Dockerfile.backend` chỉ thêm gói apt chuẩn Debian. |
| Chạy app / gọi API thật | không chạy | Theo RULES 7.3 không chạy app vào CSDL dùng chung; hành vi kiểm bằng unit test. Model EF được kiểm bằng `GenerateCreateScript()` (không kết nối CSDL) — cột/index mới đúng như mục dưới. |
| Truy vấn EF mới của repository (`IAttachmentVersionRepository`, liên kết theo nhóm phiên bản) | chưa test tự động | Test project không có provider EF in-memory/SQLite; logic dịch vụ test bằng repository giả. Cần kiểm tay trên CSDL thử nghiệm (xem “Cần phối hợp”). |

## Thay đổi schema (cần migration)
Bảng `task_attachments` (chỉ thêm, không xóa/đổi cột cũ):
- `OwnerType` `character varying(50)` NULL
- `OwnerId` `uuid` NULL
- `FileGroupId` `uuid` NULL
- `VersionNumber` `integer` NOT NULL **DEFAULT 1**
- `IsSuperseded` `boolean` NOT NULL DEFAULT `false`
- `SupersededAt` `timestamp with time zone` NULL
- `SupersededById` `uuid` NULL (không FK)
- Index `IX_task_attachments_OwnerType_OwnerId` (`OwnerType`, `OwnerId`), `IX_task_attachments_FileGroupId`.

Lưu ý khi sinh migration: bảo đảm migration giữ `defaultValue: 1` cho `VersionNumber` (đã cấu hình `HasDefaultValue(1)`) để dữ liệu cũ là phiên bản 1. Backfill tùy chọn: xem mục T-36.

## Key config mới
| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Documents:TemplatePath` | `<thư mục ứng dụng>/Templates/Word` | Thư mục template Word. |
| `Documents:Pdf:SofficePath` | tự tìm (`PATH`, `C:\Program Files\LibreOffice\program`, `/usr/bin/soffice`, `/usr/lib/libreoffice/program`…) | Đường dẫn `soffice`. |
| `Documents:Pdf:TimeoutSeconds` | `60` | Thời gian tối đa một lần chuyển PDF. |
| `Documents:Pdf:MaxConcurrency` | `2` | Số lần chuyển PDF chạy đồng thời. |
| `Documents:Pdf:WorkDirectory` | thư mục tạm hệ thống (`/tmp/congtacdang-pdf/...`) | Thư mục làm việc tạm; tài khoản chạy backend phải ghi được. |

## Thay đổi hành vi API / breaking change
- **Tệp đính kèm:**
  - Mới: `GET /api/attachments?ownerType=&ownerId=` (danh sách phiên bản hiện hành của đối tượng, lọc theo quyền), `POST /api/attachments/{id}/versions` (form `file`, tải phiên bản mới, cần `attachments.upload` + quyền cập nhật tệp), `GET /api/attachments/{id}/versions` (lịch sử, mới nhất trước), `GET /api/attachments/{id}/versions/{n}/download`.
  - `POST /api/attachments/upload` nhận thêm `ownerType`, `ownerId` (tùy chọn; gắn hồ sơ/nhiệm vụ cần là chủ hồ sơ — 403, đối tượng không tồn tại — 404, loại sai — 400).
  - `GET /api/attachments/{id}/download|view`: trả **phiên bản hiện hành** của tệp chứa `{id}` (trước đây: đúng bản ghi `{id}`; khác biệt chỉ xảy ra sau khi có phiên bản mới).
  - `GET /api/attachments/list`: chỉ phiên bản hiện hành.
  - `DELETE /api/attachments/{id}`: xóa mềm **mọi phiên bản**.
  - `AttachmentDto` thêm `uploadedById`, `ownerType`, `ownerId`, `fileGroupId`, `versionNumber`, `isCurrent`, `supersededAt`, `supersededById`.
  - `IAttachmentService`: thêm phương thức; `UploadAttachmentAsync` thêm tham số tùy chọn `ownerType`, `ownerId`; constructor đầy đủ thêm `IAttachmentVersionRepository`. Constructor 2 tham số (test của task 01) giữ nguyên.
- **Đăng ký/tự chấm nhiệm vụ** (`POST` Bước 1/Bước 2): `AttachmentId` mới không tồn tại → **400**, tệp của người khác/không có quyền → **403**. `EvaluationService` nhận thêm `IAttachmentService`.
- **Xuất biểu mẫu:** mọi endpoint `/api/reports/docx/mau-*` và `/api/reports/form-*` nhận `format=docx|xlsx|pdf` (sai giá trị → 400); `pdf` trả `application/pdf`, tên tệp `.pdf`; thiếu LibreOffice/timeout/lỗi chuyển → **503**. Tên tệp Word Mẫu 11/13 giữ dạng cũ; Excel đổi tên theo kỳ + phạm vi (ví dụ `Mau_14_TongHopXepLoaiCanBo_ToanDangBo_Q3_2026.xlsx`, `Mau_15B_KiemSoatTran20_TheoChiBo_Chi_bộ_Kỹ_thuật_Q3_2026.xlsx`).
- **Excel 14/15/15A/15B/16:** thêm `periodId`, `branchId`. **Bí thư Chi bộ** (có `branch_vote` + `reports.export`) nay xuất được Excel **của Chi bộ mình** (trước: 403); cấp cao không truyền `branchId` vẫn nhận toàn Đảng bộ; `branchId` Chi bộ khác với người không phải cấp cao → 403; Cán bộ vẫn 403. `periodId` không tồn tại → 404.
- **Frontend:** modal “PDF” ở trang Đánh giá hiển thị PDF từ máy chủ; loại “Tự chấm tiêu chí chung (Mẫu 09)” và “Hồ sơ tổng hợp cá nhân” chưa có template máy chủ nên hiện thông báo (bản HTML cũ đã bỏ). `reportService.exportForm14/15/15A/15B/16(periodId?, branchId?)`, thêm `fetchReportBlob`, `saveBlob`.

## Cần phối hợp
- **Người dùng/người điều phối:** chạy tay việc xóa MinIO bị môi trường từ chối (mục T-07). Task 06: bỏ dịch vụ MinIO trong compose/`.env.example`/`deployment.md` nếu thống nhất bỏ hẳn.
- **Người điều phối:** sinh migration cho `task_attachments` (mục Thay đổi schema). Nếu CSDL cũ nào còn dựa vào khối DDL `ExecuteSqlRaw` trong `DataSeeder` (task 06) thay vì migration, cần thêm các cột này tương tự (`ADD COLUMN IF NOT EXISTS`, `VersionNumber` DEFAULT 1, `IsSuperseded` DEFAULT false) — nếu thiếu cột, mọi truy vấn tệp đính kèm sẽ lỗi.
- **Task 03/06 (appsettings, `.env.example`):** bổ sung các key `Documents:*` ở mục Key config mới (tất cả có mặc định an toàn trong code, không bắt buộc).
- **Kiểm thử tay trên máy có LibreOffice / image Docker mới:** (1) `GET /api/reports/docx/mau-01/{recordId}?format=pdf` và Mẫu 02/10/11/13, Excel 14/15 với `format=pdf` — so bố cục PDF với bản Word (font Liberation Serif thay Times New Roman); (2) gỡ LibreOffice/đặt `SofficePath` sai → nhận 503, bản Word vẫn tải được; (3) modal PDF ở trang Đánh giá.
- **Kiểm thử tay trên CSDL thử nghiệm sau migration:** tải minh chứng ở Bước 1/2 → thay phiên bản qua `POST /api/attachments/{id}/versions` → Bí thư cùng Chi bộ xem lịch sử/tải phiên bản 1, Bí thư khác Chi bộ bị 403 → xóa → tệp biến khỏi danh sách; gửi `AttachmentId` của người khác khi tự chấm → 403.
- **Frontend tệp đính kèm (`components/attachments/*`):** chưa có giao diện xem lịch sử/tải phiên bản mới — API đã sẵn sàng; cần làm khi chốt nghiệp vụ.
- **Nghiệp vụ:** quyết định có cần lưu các giá trị đang để trống (mã SP, trục KQ, vai trò — Mẫu 01; điểm thẩm định từng nhóm — Mẫu 10; số phiếu không hợp lệ — Mẫu 13; người quản lý trực tiếp) và template máy chủ cho Mẫu 09/hồ sơ tổng hợp.

## Phát hiện thêm
- **(đề xuất T-52, ⚪)** `TaskAttachment.TaskId` và `FilePath` là thuộc tính bí danh (getter/setter trỏ `RelatedId`/`ObjectKey`) nhưng EF vẫn map thành **cột riêng** `TaskId`, `FilePath` (và index `IX_task_attachments_TaskId`) — dữ liệu trùng lặp. Nên `Ignore` hai thuộc tính này trong mapping (cần migration xóa cột, sau khi xác nhận không có dữ liệu chỉ nằm ở cột bí danh). Không sửa trong task này.
- **(⚪)** `EvaluationService.MapToRecordDto` lấy `AttachmentFileName` từ `EvaluationTask.Attachment` (bản ghi mà task trỏ tới, có thể là phiên bản cũ). Bản xuất Word đã dùng tên phiên bản hiện hành; màn hình đánh giá sẽ hiện tên phiên bản đầu cho tới khi đổi truy vấn.
- **(⚪)** Mẫu 02: ô tổng “… / 70,0” và “… SP” trong template chưa được điền (bản cũ cũng không điền). Có thể gắn `TasksScore` đã lưu nếu nghiệp vụ muốn.
- **(⚪)** `IReportAccessService.EnsureCanExportOrganizationReportAsync` không còn được gọi (Excel dùng `ResolveBranchExportScopeAsync`); giữ lại để không đổi interface của task 04.
- **(⚪)** Trang `app/forms` và `app/reports` vẫn có nút `reportService.printDocument()` (`window.print` của cả trang danh sách, không phải biểu mẫu) — ngoài phạm vi T-38.
- **(⚪)** Excel 14/15/15A/16 vẫn in cứng tên cơ quan (“ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY”…) và câu dẫn Hướng dẫn trong code. Khi chốt biểu mẫu Excel nên chuyển sang template `.xlsx` giống cơ chế Word.
