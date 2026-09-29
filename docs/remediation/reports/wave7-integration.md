# Báo cáo tích hợp đợt 7 (task 16 + task 17)

- **Branch:** `chore/wave7-integration` (fast-forward tới `main` @ `d65d4df` trước khi sửa)
- **Gộp:** `feat/criteria-sets` (task 16 — bộ tiêu chí theo phiên bản) → `feat/org-settings-templates` (task 17 — thông tin đơn vị, biểu mẫu Word), đều `--no-ff`
- **Ngày:** 2026-09-29

## Các commit

| Commit | Nội dung |
|---|---|
| `78829e7` | merge `feat/criteria-sets` (không xung đột) |
| `4db2b28` | merge `feat/org-settings-templates` — xung đột `DataSeeder` (giữ cả `SeedCriteriaSetsAsync` và `SeedOrganizationSettingsAsync`), `AuthzContractTests` (gộp đủ 3 mã quyền mới vào danh mục và tập "chỉ phạm vi Toàn công ty"); `PermissionCodes`, `DocumentTemplateTests`, `ReportService` tự gộp |
| `992dd27` | gộp lại một `InitialCreate` |
| `7cd102d` | tài khoản: chọn khung tỷ trọng mặc định (T-79 phần còn lại) |
| `09fba22` | thanh bên: mục "Bộ tiêu chí", tên hệ thống từ cài đặt; chân trang xem PDF từ cài đặt |
| `9d01b10` | Mẫu 10: content control `APPRAISAL_EXPLANATION`; danh mục tag Mẫu 01/02/10 |
| `3f04f1e` | `SampleDataSeedTests`: bộ 09B đã chụp, điểm theo mã, khung tỷ trọng, cài đặt đơn vị |
| `af8b1ba` | trang Biểu mẫu: mô tả Mẫu 09 bỏ "6 tiêu chí" |
| `3a74f96` | tài liệu thiết kế/triển khai |

## 1. Merge và file Word nhị phân

- Task 16 **không** sửa file `.docx` nào (`git diff main feat/criteria-sets -- '*.docx'` rỗng) nên không có xung đột nhị phân: 5 template giữ bản task 17 (đã bọc `ORG_*`). Không cần chạy lại script của task 17; xác nhận bằng
  `OrgSettingsTemplateTests.BundledTemplates_PassValidation_WithoutWarnings` và `DocumentTemplateTests` (44/44 pass).
- Tag Mẫu 01/02/10 của task 16 giữ nguyên tên (`T_AXIS` nay là mã trục của bộ; `T_SCORE` theo số chữ số của bộ) nên template không cần đổi ngoài phần giải trình (mục 2).

## 2. Phần phối hợp đã hoàn thiện

| Việc | Thay đổi | File |
|---|---|---|
| Khung tỷ trọng mặc định trên tài khoản (T-79) | Hộp thoại tạo/sửa tài khoản có ô chọn khung (danh sách `GET /api/criteria-sets/weight-frames`, ghi rõ bộ nguồn; khung hiện tại không còn trong bộ vẫn hiện để không mất giá trị); gửi `weightFrameCode` khi tạo (trống = không gửi) và khi sửa (chuỗi rỗng = bỏ chọn). Trang chi tiết hiện dòng "Khung tỷ trọng mặc định". Kiểu `AccountListItem`/`CreateAccountPayload`/`UpdateAccountPayload` thêm `weightFrameCode` | `components/admin/AccountFormModal.tsx`, `app/admin/users/[id]/page.tsx`, `services/userService.ts` |
| Thanh bên | Mục "Bộ tiêu chí" → `/criteria`, quyền `criteria.manage` (người chỉ có `period.manage` vẫn vào qua nút ở trang Kỳ đánh giá); dòng thương hiệu = `systemName` của cài đặt (chưa tải → "Đánh giá cán bộ"), bỏ "Đảng bộ ATTECH" | `components/layout/AppSidebar.tsx` |
| Tên đơn vị trên giao diện | `EvaluationPdfModal` chân trang = `superiorPartyName — partyCommitteeName` từ `GET /api/settings/organization` (tải khi mở). Chú thích CSS "ATTECH DESIGN SYSTEM" → "DESIGN SYSTEM". Không còn chuỗi tên đơn vị nào trong `frontend/{app,components,contexts,services}` | `components/evaluations/EvaluationPdfModal.tsx`, `app/globals.css` |
| Mẫu 10 — giải trình chênh lệch | Dòng "Nội dung giải trình của cá nhân (khi chênh lệch ≥ 5 điểm): ……" tách thành chữ in sẵn + control `APPRAISAL_EXPLANATION` (rỗng) + dấu chấm, cùng kiểu `SUPERVISOR_COMMENT`. `Mau10Data.AppraisalExplanation` = `record.AppraisalExplanation`. Script idempotent (chạy lại → "Đã có"), ghi ở dưới. Test tích hợp C1 kiểm Mẫu 10 có nội dung giải trình | `Templates/Word/Mau_10_PhieuThamDinh.docx`, `Documents/Forms/Mau10Data.cs`, `CriteriaSetIntegrationTests.cs` |
| Danh mục tag | `docs/bieu-mau.md` mục 6: Mẫu 01 (`T_AXIS` = mã trục), Mẫu 02 (tỷ lệ A–D, kết quả theo khung của hồ sơ, `T_SCORE` theo làm tròn của bộ, 09B không có dòng), Mẫu 10 (thêm `APPRAISAL_EXPLANATION`; nghĩa `GENERAL_SELF_SCORE`/`TASKS_SELF_SCORE` theo bộ). Trang Quản trị → Biểu mẫu Word → Tag tự sinh từ lớp dữ liệu nên đã có tag mới | `docs/bieu-mau.md` |
| Quyền mặc định | Đã có sau merge: Cơ quan thẩm định có `criteria.manage` (test `C3_Permissions_AndWeightFrameOptions`); Quản trị hệ thống nhận mọi `system.*` gồm `system.settings.manage`, `system.templates.manage` (test `T4_…`). Sửa mô tả vai trò Quản trị hệ thống cho khớp ("…, thông tin đơn vị, file mẫu biểu mẫu Word") | `DataSeeder.cs` |
| Chữ cũ "khung chức danh" | Đổi thành "khung tỷ trọng" ở chú thích/mô tả (`PeriodController`, `PeriodParticipantImportDefinition` — mô tả loại import, `EvaluationRecord`, `EvaluationTask`, `WorkflowSteps`, `Mau02Data`); trang Biểu mẫu mô tả Mẫu 09 theo bộ tiêu chí | nhiều |

Script thêm control Mẫu 10 (chạy `py -3 add_explanation_tag.py <đường dẫn Mau_10_PhieuThamDinh.docx>`, chạy lại an toàn):

```python
import zipfile, sys, shutil
p = sys.argv[1]; TAG = "APPRAISAL_EXPLANATION"
OLD = ('<w:r w:rsidRPr="004E4EB1"><w:rPr><w:sz w:val="24" /></w:rPr>'
       '<w:t>Nội dung giải trình của cá nhân (khi chênh lệch ≥ 5 điểm): ………………………………..</w:t></w:r>')
NEW = ('<w:r><w:rPr><w:sz w:val="24" /></w:rPr><w:t xml:space="preserve">Nội dung giải trình của cá nhân (khi chênh lệch ≥ 5 điểm): </w:t></w:r>'
       f'<w:sdt><w:sdtPr><w:alias w:val="{TAG}" /><w:tag w:val="{TAG}" /><w:id w:val="510070" /></w:sdtPr>'
       '<w:sdtContent><w:r><w:rPr><w:sz w:val="24" /></w:rPr><w:t xml:space="preserve" /></w:r></w:sdtContent></w:sdt>'
       '<w:r><w:rPr><w:sz w:val="24" /></w:rPr><w:t xml:space="preserve">………………………………..</w:t></w:r>')
src = zipfile.ZipFile(p); x = src.read("word/document.xml").decode("utf8")
if f'w:val="{TAG}"' in x: print("Đã có", TAG); sys.exit(0)
if x.count(OLD) != 1: raise SystemExit("Không tìm thấy dòng giải trình")
x = x.replace(OLD, NEW); tmp = p + ".tmp"
with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as out:
    for it in src.infolist():
        out.writestr(it, x.encode("utf8") if it.filename == "word/document.xml" else src.read(it.filename))
src.close(); shutil.move(tmp, p); print("OK", p)
```

## 3. Dữ liệu mẫu (`Database:SeedSampleData=true`)

Seeder sau merge đã đáp ứng (task 16 viết phần điểm, thứ tự seed: vai trò → danh mục → bộ tiêu chí → **thông tin đơn vị** → dữ liệu mẫu):
kỳ mẫu gắn bộ "Mẫu 09B — Quý III/2026" (`CriteriaSetId` + ảnh chụp); cán bộ mẫu có khung K1/K2/K4, hồ sơ chụp khung; hồ sơ đã tự chấm có điểm 17 tiêu chí con (2 "Không đảm bảo" có căn cứ) + 6 trục (26 + 62 = 88, HTT); hồ sơ đã thẩm định 88,5 (chênh 0,5 < ngưỡng → không cần giải trình); thông tin đơn vị mặc định; readiness 0 cảnh báo.
`SampleDataSeedTests.EmptyDatabase_SeedSampleData_LoginShowsWorkQueueByRoleAndScope` bổ sung kiểm: mã/tên bộ đã chụp qua API kỳ hiện hành; `GET /api/settings/organization/public` và `GET /api/settings/organization` trả giá trị mặc định; trong CSDL — `CriteriaSetId` trỏ bộ 09B, ảnh chụp 09B có 17 tiêu chí/6 trục, mọi cán bộ có khung thuộc bộ, hồ sơ chụp đúng khung của cán bộ, ≥ 4 hồ sơ đã tự chấm có đủ mã tiêu chí con/trục và tổng = chung + nhiệm vụ. Readiness `ready = true`, `issues` rỗng (đã có sẵn).

## 4. Migration

- Xóa `20260929031346_InitialCreate*` + snapshot, sinh lại `20260929052857_InitialCreate` (`ConnectionStrings__Default` giả `design_only`).
- Giữ SQL tay: `CREATE UNIQUE INDEX "IX_party_member_profiles_Username_lower" ON party_member_profiles (lower("Username"))` (sau index `IX_party_member_profiles_Username`, kèm chú thích).
- Schema mới có: `criteria_sets` (unique `Code`), `evaluation_periods.CriteriaSetId` (FK Restrict) + `CriteriaSnapshot` jsonb, `evaluation_records.GeneralScores`/`AxisScores` jsonb + `WeightFrameCode` + `AppraisalExplanation`, `evaluation_tasks.AxisCode`, `party_member_profiles.WeightFrameCode`, `organization_settings`, `word_template_versions` (unique `(TemplateCode, VersionNumber)`, unique lọc `"IsActive" = TRUE`). Không còn cột `GeneralScoreT*`/`AxisScoreT*`/`JobGroup`.
- `dotnet ef migrations has-pending-model-changes`: **sạch**.
- Áp dụng trên CSDL tạm `ctd_it_mig_w7` (máy 159) bằng `dotnet ef database update` → `Applying migration '20260929052857_InitialCreate'. Done.`; `migrations list` chỉ có `InitialCreate`; đã `dotnet ef database drop --force` (xóa thành công). Test tích hợp dựng mọi CSDL tạm bằng chính migration này.

## 5. Tài liệu

- `docs/thiet-ke/phan-quyen.md`: danh mục quyền thêm `system.settings.manage`, `system.templates.manage`, `criteria.manage`; `period.manage` ghi chọn bộ tiêu chí; vai trò Cơ quan thẩm định có `criteria.manage`; Quản trị hệ thống ghi rõ 2 quyền `system.*` mới.
- `docs/thiet-ke/luong-danh-gia.md`: cấu hình kỳ bỏ `selfScoreForm`/`parameters`; kiểu kỳ gợi ý bộ 09A/09B; ảnh chụp `WeightFrameCode` thay `JobGroup`; readiness khung tỷ trọng; mục mới **3.5 Bộ tiêu chí và thang điểm** (trạng thái, bất biến, chụp vào kỳ, điểm theo mã, giải trình chênh lệch B-09, trần Xuất sắc).
- `docs/deployment.md`: dữ liệu mẫu (bộ 09B, khung, điểm theo mã, cài đặt); go-live bước 0 (seed 2 bộ tiêu chí + thông tin đơn vị), bước **1b** mới (Thông tin đơn vị, Biểu mẫu Word, sao lưu `word-templates/`), 2d (cột "Mã khung tỷ trọng"), 4 (xuất thử kiểm tên đơn vị), 5 (rà/nhân bản/xuất bản bộ tiêu chí, chọn bộ cho kỳ, lỗi mở kỳ khi thiếu bộ, cảnh báo khung).
- `docs/database-migrations.md`: lần gộp gần nhất là sau đợt 7.
- `docs/bieu-mau.md`: danh mục tag (mục 2).
- Không sửa `CLAUDE.md`, `FINDINGS.md`, `RULES.md`, task file.

## 6. Grep

`GeneralScoreT|AxisScoreT|TaskResultAxis|JobGroup\b|GeneralCriterionMaxScore|PartyRole|AdministrativePosition|AppRoles|IsInRole|hasRole|minio`
trong `backend/src`, `backend/tests`, `frontend/{app,components,contexts,services}` (bỏ `bin/`, `obj/`, kể cả không phân biệt hoa thường): **rỗng** (migration mới cũng không còn).

`KỸ THUẬT QUẢN LÝ BAY|ATTECH` — các dòng còn lại:

| File:dòng | Loại |
|---|---|
| `Api/Extensions/HostingExtensions.cs:27` | chặn secret JWT mặc định đã lộ `CongTacDang_ATTECH_2026_SecretKey` |
| `Application/Imports/Definitions/CatalogImportDefinitions.cs:374`, `:431` | ví dụ file import (`ExampleParentCode` = mã đơn vị mẫu `ATTECH`, `DU-ATTECH`) |
| `Application/Imports/Definitions/PositionImportDefinitions.cs:303` | ví dụ file import (số quyết định `QĐ 123/QĐ-ATTECH`) |
| `Infrastructure/Data/DataSeeder.cs:391`, `:395`, `:397` | seed mặc định thông tin đơn vị (`PartyCommitteeName`, `ShortName`, `SystemName`) |
| `Infrastructure/Data/DataSeeder.cs:525–529`, `:535–537`, `:550`, `:558` | dữ liệu mẫu (mã đơn vị `ATTECH`, `DU-ATTECH`) |
| `tests/…/OrgSettingsTemplateIntegrationTests.cs:89`, `:103`; `tests/…/OrgSettingsTemplateTests.cs:25`, `:27`, `:33` | test khẳng định giá trị seed mặc định của cài đặt (giữ đúng chuỗi cũ) / tên mới thay chuỗi cũ |
| `tests/…/SampleDataSeedTests.cs:167` | test khẳng định mã đơn vị gốc của dữ liệu mẫu |

`frontend/{app,components,contexts,services}`: không còn dòng nào.

## 7. Cổng kiểm tra

| Bước | Kết quả |
|---|---|
| `dotnet build backend/CongTacDang.slnx --no-incremental` | 0 lỗi, 0 warning |
| `dotnet test` — unit | 274: 273 pass, 1 skip có sẵn (`PdfConversionTests.Mau01_ConvertsToPdf_AndCleansWorkDirectory` — máy không cài LibreOffice), 0 fail — cả 2 lần |
| `dotnet test` — tích hợp (159, `CONGTACDANG_TEST_PG`), lần 1 | **98 chạy, 98 pass, 0 skip**, 0 fail |
| `dotnet test` — tích hợp, lần 2 liên tiếp | **98 chạy, 98 pass, 0 skip**, 0 fail |
| `npx tsc --noEmit` | pass |
| `npm run build` | pass (3 cảnh báo ESLint có sẵn: `app/audit/page.tsx` `loadLogs`, `DocumentViewerModal` `blobUrl`, `<img>` — không thuộc đợt này) |
| `has-pending-model-changes` | sạch |
| CSDL tạm | chỉ `ctd_it_*` (fixture test + `ctd_it_mig_w7` đã xóa); không đụng `congtacdang_test`. Không chạy app (không cần: luồng giao diện mới kiểm bằng `tsc`/`build`, API bằng test tích hợp) |

## 8. Quyết định chính

1. Mục thanh bên "Bộ tiêu chí" theo đúng yêu cầu: quyền `criteria.manage` (báo cáo 16 đề xuất cả `period.manage`; người chỉ quản lý kỳ vẫn vào qua nút trên trang Kỳ đánh giá, API danh sách vẫn cho `period.manage`).
2. Tag Mẫu 10 đặt tên `APPRAISAL_EXPLANATION` (khớp tên trường `AppraisalExplanation`), không dùng `DISCREPANCY_EXPLANATION` như gợi ý báo cáo 16. Chữ in sẵn "(khi chênh lệch ≥ 5 điểm)" giữ nguyên văn biểu mẫu; ngưỡng thực tế theo bộ tiêu chí — sửa chữ này bằng cách tải phiên bản mẫu mới.
3. Chân trang xem PDF dùng `GET /api/settings/organization` (đã đăng nhập, có tên Đảng bộ/cấp trên), không dùng `/public` (phần công khai không có 2 trường này).
4. Ô khung tỷ trọng: tạo tài khoản với ô trống → không gửi (null); sửa → gửi chuỗi rỗng để bỏ chọn (đúng hợp đồng API của task 16).
5. Không đổi luật nghiệp vụ, công thức, schema ngoài phần hai task đã khai báo.

## 9. Còn lại / cần nghiệp vụ

- Câu hỏi nghiệp vụ của báo cáo 16 (8 câu: nội dung 2 bộ mặc định, làm tròn, K/AD, mẫu số trần, ngưỡng/ai giải trình, khung mặc định K2…) và "Phát hiện thêm" của báo cáo 16/17 (điểm thẩm định từng nhóm trên Mẫu 10, giải trình cho kết quả cấp trên ghi nhận, làm tròn bước 0,5, đổi khung sau khi tự chấm không tính lại, `reportService.downloadReport` bỏ qua `Content-Disposition`, trường "Văn bản căn cứ") — chưa làm, ngoài phạm vi tích hợp.
- Dòng chữ in sẵn "(khi chênh lệch ≥ 5 điểm)" trên Mẫu 10 không tự theo ngưỡng của bộ (xem quyết định 2).
