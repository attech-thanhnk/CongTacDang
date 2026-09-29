# Báo cáo: 17 — Cài đặt đơn vị và quản lý biểu mẫu trên giao diện

- **Branch:** `feat/org-settings-templates` (từ `main` @ `d65d4df`)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `7b72979`)
- **Ngày:** 2026-09-29

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-80 | done | Bảng `organization_settings` (một bản ghi, có kiểu); seed mặc định = đúng chuỗi trước đây ghi cứng; API đọc/sửa + phần công khai; cache xóa khi sửa; Excel 14/15/danh sách cán bộ, Word (tag `ORG_*`), tên tệp, Swagger, giao diện dùng cài đặt | `Domain/Entities/OrganizationSettings.cs`, `Infrastructure/Services/OrganizationSettingsService.cs`, `ReportService.cs`, `Documents/TemplateData.cs`, `DataSeeder.cs`, `Api/Controllers/SettingsController.cs`, `frontend/app/admin/settings/page.tsx` |
| T-81 | done | Bảng `word_template_versions` + kho tệp; `WordTemplateStore` đọc phiên bản kích hoạt, không có thì file gốc; kiểm tra tải lên (đuôi, magic bytes, 10 MB, mở OpenXML, tag lạ = lỗi, tag bắt buộc thiếu = cảnh báo, sinh thử); API + trang quản trị (tải file đang dùng/gốc, tải lên, lịch sử, kích hoạt lại, dùng file gốc, danh mục tag); `docs/bieu-mau.md` | `Documents/WordTemplateStore.cs`, `WordFormCatalog.cs`, `WordTemplateValidator.cs`, `Infrastructure/Services/WordTemplateService.cs`, `SettingsController.cs` (`WordTemplateController`), `frontend/app/admin/templates/page.tsx` |

### Thiết kế cài đặt: một bản ghi có kiểu (không key–value)
Danh sách thông tin do code quyết định (mỗi trường gắn một vị trí trên báo cáo / một tag biểu mẫu), cần kiểm tra hợp lệ riêng
(tên viết tắt dùng trong tên tệp chỉ cho `[A-Za-z0-9_-]`). Bảng key–value không thêm được gì ngoài mất kiểu. Id cố định
`OrganizationSettings.SingletonId`. Cache singleton (`OrganizationSettingsCache`) xóa ngay khi sửa, tự hết hạn sau 5 phút.

### Chuỗi đã đưa vào cài đặt

| Trường (API) | Mặc định seed | Trước đây ghi cứng ở | Nay dùng ở |
|---|---|---|---|
| `partyCommitteeName` Tên Đảng bộ | `ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY` | `ReportService` (A1 danh sách cán bộ, A2 Mẫu 14, 15) | Như trước + Word Mẫu 11/13 khi xuất toàn Đảng bộ (dòng `PARTY_CELL`, trước là chữ chờ "CHI BỘ CƠ SỞ TRỰC THUỘC"); tag `ORG_PARTY_NAME` |
| `superiorPartyName` Tổ chức Đảng cấp trên | `ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM` | `ReportService` (A1 Mẫu 14, 15); template Mẫu 11/13 in "ĐẢNG BỘ TCT QUẢN LÝ BAY VIỆT NAM" | Excel như trước; Word Mẫu 11/13 (tag `ORG_SUPERIOR_PARTY_NAME`) |
| `companyName` Tên công ty | `Công ty TNHH Kỹ thuật Quản lý bay` | Trang đăng nhập; mẫu dữ liệu | Word Mẫu 01/02 dòng "CƠ QUAN QUẢN LÝ CẤP TRÊN" (tag `ORG_COMPANY_NAME_UPPER`); trang đăng nhập |
| `parentCompanyName` Đơn vị chủ quản | `Tổng công ty Quản lý bay Việt Nam` | Template Mẫu 10 "TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM"; trang đăng nhập | Word Mẫu 10 (tag `ORG_PARENT_COMPANY_NAME_UPPER`, in hoa từ mặc định = đúng chữ cũ); trang đăng nhập |
| `shortName` Tên viết tắt | `ATTECH` | `ReportService` (A3 "DANH SÁCH CÁN BỘ LÃNH ĐẠO, QUẢN LÝ ATTECH", tên tệp `DanhSach_CanBo_ATTECH.xlsx`); `frontend/services/reportService.ts` | Tiêu đề và tên tệp danh sách cán bộ (máy chủ và giao diện); tag `ORG_SHORT_NAME` |
| `location` Địa danh | `Hà Nội` | `ReportService` ("Hà Nội, ngày …" Mẫu 14, 15) | Excel như trước; Word Mẫu 01/02/10/11 (tag `ORG_LOCATION`, trước là chữ chờ "……") |
| `systemName` Tên hiển thị hệ thống | `Đảng bộ ATTECH` | `AppHeader`, `AppSidebar`, `AuthGuard` (chân trang), trang đăng nhập, `layout.tsx` | `AppHeader`, `AuthGuard`, trang đăng nhập (xem "Cần phối hợp" cho `AppSidebar`) |

Bỏ tên riêng (không đưa vào cài đặt): Swagger title/endpoint (`Program.cs`), tên người tải mặc định "Cán bộ ATTECH" →
"Cán bộ" (`AttachmentController`), `layout.tsx` metadata → "Đánh giá cán bộ", chân `DocumentViewerModal`, mô tả báo cáo
trong `reportService.ts`, chú thích code (`OrganizationController`, `Organizations.cs`, `ApprovalAuthority.cs`).
Không có trường "người/đơn vị lập báo cáo mặc định": không biểu mẫu hiện có nào in trường này.

**Thay đổi hiển thị so với trước (khi dùng giá trị mặc định):**
- Word Mẫu 11/13: dòng tổ chức Đảng cấp trên in đầy đủ "ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM" (trước viết tắt "TCT") —
  thống nhất với Excel; sửa được trong cài đặt.
- Word Mẫu 01/02: chữ chờ "CƠ QUAN QUẢN LÝ CẤP TRÊN" nay in tên công ty; Mẫu 01/02/10/11: "……, ngày …" nay in địa danh
  (ngày để trống như cũ). Mẫu 11/13 xuất toàn Đảng bộ: dòng tổ chức lập phiếu in tên Đảng bộ thay chữ chờ.
- Excel và mọi chuỗi khác: không đổi.

### Kết quả grep tiêu chí hoàn thành
`grep -rnE "KỸ THUẬT QUẢN LÝ BAY|ATTECH" backend/src --include=*.cs` (trừ Migrations) còn:
- `DataSeeder.DefaultOrganizationSettings()` — giá trị seed mặc định của cài đặt.
- `DataSeeder` dữ liệu mẫu (mã đơn vị mẫu `ATTECH`, `DU-ATTECH`).
- Ví dụ trong file mẫu nhập liệu (`CatalogImportDefinitions` `ExampleParentCode` = mã đơn vị mẫu, `PositionImportDefinitions`
  ví dụ số quyết định) — dữ liệu ví dụ khớp dữ liệu mẫu, không phải logic xuất.
- `HostingExtensions`: kiểm tra chặn **secret JWT mặc định đã lộ** `CongTacDang_ATTECH_2026_SecretKey` — là chốt an ninh, phải giữ.

Không còn trong logic xuất biểu mẫu / tên tệp. Template Word gốc không còn chữ tên đơn vị nằm ngoài Content Control (các chuỗi đã bọc
vào tag `ORG_*`); câu trích dẫn tên Hướng dẫn trong thân Mẫu 13 ("…trong Đảng bộ Tổng công ty Quản lý bay Việt Nam") là trích văn
bản, giữ nguyên, sửa được bằng cách tải lên phiên bản mới.

### Danh mục tag từng mẫu
Tag riêng từng mẫu (bắt buộc) và tag đơn vị dùng chung (tùy chọn) ghi đầy đủ ở `docs/bieu-mau.md` mục 6; trang
**Quản trị → Biểu mẫu Word → Tag** sinh từ code (`TemplateTagCatalog.Describe(MauXXData)`) nên luôn khớp bản đang chạy.

| Mẫu | Tag cấp tài liệu | Khối lặp |
|---|---|---|
| `MAU_01` | `DEPARTMENT`, `QUARTER`, `YEAR`, `FULL_NAME`, `POSITION`, `SUPERVISOR_NAME` | `repeat:TASKS`: `T_STT`, `T_NAME`, `T_CODE`, `T_AXIS`, `T_ROLE`, `T_WEIGHT`, `T_DEADLINE`, `T_STANDARD`, `T_EXCEED`, `T_EVIDENCE`, `T_SIGNER` |
| `MAU_02` | `DEPARTMENT`, `QUARTER`, `YEAR`, `FULL_NAME`, `POSITION` | `repeat:TASKS`: `T_STT`, `T_NAME`, `T_WEIGHT`, `T_A`–`T_D`, `T_RESULT_PCT`, `T_SCORE`, `T_EVIDENCE`, `if:`/`ifnot:T_IS_EXCEED` |
| `MAU_10` | `QUARTER`, `YEAR`, `FULL_NAME`, `POSITION`, `DEPARTMENT`, `GENERAL_*`, `TASKS_*`, `TOTAL_*` (SELF_SCORE/APPRAISAL_SCORE/DIFF), `SUPERVISOR_COMMENT`, `APPRAISAL_COMMENT`, `PROPOSED_GRADE` | — |
| `MAU_11` | `PARTY_CELL`, `PERIOD_QUARTER_YEAR` | `repeat:RECORDS`: `R_STT`, `R_NAME`, `R_POSITION_DEPT`, `R_GENERAL_SCORE`, `R_TASKS_SCORE`, `R_SELF_GRADE` |
| `MAU_13` | `PARTY_CELL`, `PERIOD_QUARTER_YEAR`, `TOTAL_VOTERS`, `INVALID_BALLOTS` | `repeat:RECORDS`: `V_STT`, `V_NAME`, `V_POSITION_DEPT`, `V_EXC`, `V_GOOD`, `V_SAT`, `V_UNSAT`, `V_PCT` |
| Chung | `ORG_PARTY_NAME`, `ORG_SUPERIOR_PARTY_NAME`, `ORG_COMPANY_NAME(_UPPER)`, `ORG_PARENT_COMPANY_NAME(_UPPER)`, `ORG_SHORT_NAME`, `ORG_LOCATION` | — |

### API mới

| Method | Route | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/settings/organization` | đã đăng nhập | Thông tin đơn vị (kèm người/lúc cập nhật) |
| GET | `/api/settings/organization/public` | **không cần đăng nhập** | `systemName`, `companyName`, `parentCompanyName`, `shortName` — cho trang đăng nhập/đầu trang |
| PUT | `/api/settings/organization` | `system.settings.manage` | Sửa (mọi trường bắt buộc; tên viết tắt `[A-Za-z0-9_-]`) |
| GET | `/api/templates` | `system.templates.manage` | Danh mục biểu mẫu + phiên bản đang dùng + danh mục tag |
| GET | `/api/templates/{code}/versions` | ″ | Lịch sử phiên bản |
| POST | `/api/templates/{code}/check` | ″ | Kiểm tra tệp (multipart `file`), không lưu |
| POST | `/api/templates/{code}/versions` | ″ | Tải lên (multipart `file`, `note`, `activate`=true); lỗi → 400 kèm kết quả kiểm tra |
| POST | `/api/templates/{code}/versions/{id}/activate` | ″ | Kích hoạt (kể cả bản cũ) |
| POST | `/api/templates/{code}/use-original` | ″ | Về file gốc |
| GET | `/api/templates/{code}/current` · `/original` · `/versions/{id}/file` | ″ | Tải file đang dùng / gốc / một phiên bản |

Quyền mới: `system.settings.manage` ("Quản lý thông tin đơn vị"), `system.templates.manage` ("Quản lý file mẫu biểu mẫu") —
cả hai chỉ phạm vi Global. Vai trò Quản trị hệ thống nhận tự động (seeder gán mọi mã `system.*` khi tạo vai trò mặc định; test T4).

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx --no-incremental` | pass | 0 error, 0 warning |
| `dotnet test` — unit | pass | 246 pass, 1 skip (`PdfConversionTests` — máy không cài LibreOffice, có sẵn từ trước) |
| `dotnet test` — tích hợp | pass | 95 chạy, 95 pass, **0 skip** (có `CONGTACDANG_TEST_PG`, CSDL tạm `ctd_it_*` trên 159) |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | 3 cảnh báo ESLint có sẵn ở file khác (audit, DocumentViewerModal `blobUrl`, `<img>`), không phải do task này |
| `has-pending-model-changes` (với migration tạm) | sạch | |
| Chạy app thủ công (5118/3118) | không chạy | Luồng API đã phủ bằng test tích hợp chạy host thật; trang FE chỉ kiểm bằng tsc/build |

**Cách chạy test tích hợp khi chưa có migration** (không commit):
`dotnet ef migrations add TmpTask17 -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api -o Data/Migrations`
→ chạy test → xóa 2 file `*_TmpTask17*.cs` và `git checkout` `CongTacDangDbContextModelSnapshot.cs`. Migration tạm chỉ gồm 2 bảng
của task này.

### Test theo luồng

| Luồng | Trạng thái | Test |
|---|---|---|
| Unit: validate tag thừa (lỗi) | pass | `OrgSettingsTemplateTests.UnknownTag_IsError` |
| Unit: validate tag thiếu (cảnh báo), tag đơn vị tùy chọn, khối điều kiện một nhánh | pass | `MissingRequiredTag_IsWarningOnly`, `OptionalOrganizationTags_MayBeAbsent`, `ConditionBlock_OneBranchIsEnough` |
| Unit: file gốc qua được bộ kiểm tra; tệp không phải Word | pass | `BundledTemplates_PassValidation_WithoutWarnings` (5 mẫu), `NotAWordDocument_IsError` |
| Unit: chọn phiên bản đang kích hoạt / fallback file gốc / tệp kích hoạt bị mất | pass | `Store_UsesActiveVersion_FromStorage`, `Store_NoActiveVersion_FallsBackToBundled`, `Store_ActiveFileMissing_FallsBackToBundled` |
| Unit: mặc định = chuỗi cũ; tag đơn vị trên Mẫu 02 | pass | `DefaultSettings_KeepPreviouslyHardcodedStrings`, `Mau02_RendersOrganizationTags_FromSettings` |
| Tích hợp: đổi tên Đảng bộ → Excel Mẫu 14 + Word có tên mới; tên tệp theo tên viết tắt; về mặc định giữ chuỗi cũ | pass | `OrgSettingsTemplateIntegrationTests.S1_…` (Mẫu 14 A1/A2/F2, danh sách cán bộ, Mẫu 02 tên công ty + địa danh, Mẫu 11 tên Đảng bộ + cấp trên) |
| Tích hợp: đọc cài đặt (mọi người đăng nhập / công khai), sửa cần quyền, kiểm tra hợp lệ | pass | `S2_…` |
| Tích hợp: tải bản mới → xuất dùng bản mới; kích hoạt lại bản cũ → dùng bản cũ; về file gốc | pass | `T1_…` |
| Tích hợp: thiếu tag bắt buộc → cảnh báo (vẫn lưu); tag lạ → 400 không lưu; sai chữ ký tệp → 400 | pass | `T2_…` |
| Tích hợp: không có quyền → 403 | pass | `T3_…` |
| Tích hợp: seeder gán 2 quyền cho Quản trị hệ thống, tạo cài đặt mặc định | pass | `T4_…` |

Ghi chú về test "Word Mẫu 02 có tên Đảng bộ mới": Mẫu 02 là phiếu kiểu chính quyền (tiêu đề trái "CƠ QUAN QUẢN LÝ CẤP TRÊN / đơn vị",
không có dòng Đảng bộ). Test kiểm Mẫu 02 hiển thị **tên công ty + địa danh** mới, và kiểm **tên Đảng bộ + tổ chức Đảng cấp trên**
mới trên Word Mẫu 11 (biểu mẫu Đảng).

Sửa kỳ vọng test cũ (do task yêu cầu đổi hành vi): `AuthzContractTests.PermissionCodes_MatchDesignCatalog` (thêm 2 mã quyền);
`DocumentTemplateTests.Template_TagsMatchFormDataClass` / `Template_RendersValidDocument_WithoutPlaceholders` (template có thêm tag
`ORG_*` dùng chung — được chấp nhận ngoài lớp dữ liệu; khi render ghép dữ liệu đơn vị).

## Thay đổi schema (cần migration)
- Bảng mới `organization_settings`: `Id` (uuid, PK, không tự sinh), `PartyCommitteeName`, `SuperiorPartyName`, `CompanyName`,
  `ParentCompanyName`, `SystemName` (varchar 300, not null), `ShortName` (varchar 30), `Location` (varchar 100), cột audit
  `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy`.
- Bảng mới `word_template_versions`: `Id` uuid PK, `TemplateCode` varchar 20, `VersionNumber` int, `OriginalFileName` varchar 255,
  `ObjectKey` varchar 500, `FileSize` bigint, `Checksum` varchar 64, `IsActive` bool, `Note` varchar 500 null, `TagsJson` text,
  `WarningsJson` text, `UploadedById` uuid null, `UploadedByName` varchar 200, `UploadedAt`, `ActivatedAt`, `ActivatedById`,
  `ActivatedByName` varchar 200, cột audit. Index duy nhất `(TemplateCode, VersionNumber)`; index duy nhất `TemplateCode`
  lọc `"IsActive" = TRUE` (mỗi mẫu tối đa một bản kích hoạt).
- Cấu hình: `Infrastructure/Data/Configurations/OrganizationSettingsConfigurations.cs` (không sửa `OnModelCreating`, không thêm DbSet).
- Không tạo migration. Người điều phối gộp vào `InitialCreate`.

## Key config mới
Không có. (`Documents:TemplatePath` giữ nguyên ý nghĩa: thư mục **file gốc**.) File mẫu tải lên nằm trong kho tệp `Storage:Local:Path`
dưới thư mục `word-templates/` — cần nằm trong phạm vi sao lưu như tệp đính kèm.

## Thay đổi hành vi API / breaking change
- `IWordTemplateStore` đổi thành bất đồng bộ (`LoadAsync`), đăng ký scoped; file gốc qua `IBundledWordTemplates`
  (`FileWordTemplateStore` giữ `Load`). `ReportService` nhận thêm `IOrganizationSettingsService`.
- Tên tệp danh sách cán bộ: `DanhSach_CanBo_<tên viết tắt>.xlsx` (mặc định không đổi).
- Endpoint công khai mới `GET /api/settings/organization/public` (không cần đăng nhập, chỉ tên hiển thị — không có dữ liệu nhạy cảm).
- Template Word gốc (5 tệp) thêm Content Control `ORG_*` — xem mục "Thay đổi hiển thị".

## Cần phối hợp
- **Template Word nhị phân (`Templates/Word/*.docx`) — xung đột với task 16:** task này sửa cả 5 file gốc (thêm control `ORG_*`,
  xem bảng dưới). Nếu task 16 cũng sửa `Mau_01/02/10*.docx` (đổi tag điểm/trục), git không gộp được tệp nhị phân: lấy bản của
  task 16 rồi áp lại phần của task 17 bằng script idempotent dưới đây (chạy lại an toàn; báo lỗi nếu không tìm thấy chuỗi), sau đó
  chạy `OrgSettingsTemplateTests.BundledTemplates_PassValidation_WithoutWarnings` và `DocumentTemplateTests`.

  | Tệp | Chuỗi bọc vào control |
  |---|---|
  | Mau_01, Mau_02 | "CƠ QUAN QUẢN LÝ CẤP TRÊN" → `ORG_COMPANY_NAME_UPPER`; "……" đầu dòng "……, ngày …… tháng …… năm 20…" → `ORG_LOCATION` |
  | Mau_10 | "TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM" → `ORG_PARENT_COMPANY_NAME_UPPER`; dòng ngày như trên → `ORG_LOCATION` |
  | Mau_11 | "ĐẢNG BỘ TCT QUẢN LÝ BAY VIỆT NAM" → `ORG_SUPERIOR_PARTY_NAME`; "…………" đầu dòng "…………, ngày…… tháng… … năm……" → `ORG_LOCATION` |
  | Mau_13 | "ĐẢNG BỘ TCT QUẢN LÝ BAY VIỆT NAM" → `ORG_SUPERIOR_PARTY_NAME` |

  ```python
  # py -3 add_org_tags.py <thư mục Templates/Word>
  import zipfile, re, sys, os, shutil
  D = sys.argv[1]
  A = "……, ngày …… tháng …… năm 20…"; B = "…………, ngày…… tháng… … năm……"
  PLAN = {
      "Mau_01_PhieuGiaoNhiemVu.docx": [("wrap", "CƠ QUAN QUẢN LÝ CẤP TRÊN", "ORG_COMPANY_NAME_UPPER"), ("loc", A)],
      "Mau_02_TuDanhGia.docx": [("wrap", "CƠ QUAN QUẢN LÝ CẤP TRÊN", "ORG_COMPANY_NAME_UPPER"), ("loc", A)],
      "Mau_10_PhieuThamDinh.docx": [("wrap", "TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM", "ORG_PARENT_COMPANY_NAME_UPPER"), ("loc", A)],
      "Mau_11_PhieuBoPhieuChiBo.docx": [("wrap", "ĐẢNG BỘ TCT QUẢN LÝ BAY VIỆT NAM", "ORG_SUPERIOR_PARTY_NAME"), ("loc", B)],
      "Mau_13_BienBanKiemPhieu.docx": [("wrap", "ĐẢNG BỘ TCT QUẢN LÝ BAY VIỆT NAM", "ORG_SUPERIOR_PARTY_NAME")],
  }
  nid = [531000]
  def sdt(tag, inner):
      nid[0] += 1
      return (f'<w:sdt><w:sdtPr><w:alias w:val="{tag}" /><w:tag w:val="{tag}" /><w:id w:val="{nid[0]}" /></w:sdtPr>'
              f'<w:sdtContent>{inner}</w:sdtContent></w:sdt>')
  def run(text):
      return re.compile(r'<w:r(?: [^>]*)?>(<w:rPr>(?:(?!</w:rPr>).)*</w:rPr>)?<w:t(?: [^>]*)?>' + re.escape(text) + r'</w:t></w:r>', re.S)
  def transform(x, steps):
      for st in steps:
          tag = st[2] if st[0] == "wrap" else "ORG_LOCATION"
          if f'w:val="{tag}"' in x: continue
          m = run(st[1]).search(x)
          if not m: raise SystemExit(f"Không tìm thấy '{st[1]}'")
          if st[0] == "wrap":
              new = sdt(tag, m.group(0))
          else:
              rpr = m.group(1) or ""; i = st[1].index(",")
              new = sdt(tag, f'<w:r>{rpr}<w:t>{st[1][:i]}</w:t></w:r>') + f'<w:r>{rpr}<w:t xml:space="preserve">{st[1][i:]}</w:t></w:r>'
          x = x[:m.start()] + new + x[m.end():]
      return x
  for name, steps in PLAN.items():
      p = os.path.join(D, name); src = zipfile.ZipFile(p); tmp = p + ".tmp"
      with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as out:
          for it in src.infolist():
              data = src.read(it.filename)
              if it.filename == "word/document.xml":
                  data = transform(data.decode("utf8"), steps).encode("utf8")
              out.writestr(it, data)
      src.close(); shutil.move(tmp, p); print("OK", name)
  ```
- **`PermissionCodes.cs`, `DataSeeder.cs`, `AuthzContractTests.cs`, `DocumentTemplateTests.cs`** cũng do task 16 sửa: xung đột văn bản
  nhỏ (task 17 chèn 2 mã sau `system.import`; một lời gọi + một region "Thông tin đơn vị mặc định (task 17)" trước region task 14;
  2 chỗ trong `DocumentTemplateTests`). Nếu task 16 đổi chữ ký `Mau01/02/10Data.From(...)`, `DocumentTemplateTests.AllForms` theo
  bản task 16; phần task 17 chỉ là ghép `OrganizationTemplateFields`. `WordFormCatalog`/bộ kiểm tra dựa trên thuộc tính của lớp
  dữ liệu nên tự theo tag mới của task 16.
- **`docs/bieu-mau.md` mục 6** liệt kê tag Mẫu 01/02/10 theo lớp dữ liệu hiện tại — sau khi gộp task 16 cần cập nhật bảng
  (trang Quản trị → Biểu mẫu Word → Tag luôn đúng vì sinh từ code).
- **`frontend/components/layout/AppSidebar.tsx`**: task chỉ cho thêm mục nên dòng thương hiệu `Đảng bộ ATTECH` (dòng ~112) chưa đổi.
  Đề xuất: `const org = useOrganizationInfo();` và hiển thị `{org?.systemName ?? "Đánh giá cán bộ"}`.
- **`frontend/components/evaluations/EvaluationPdfModal.tsx`** (trang in, thuộc task 16): dòng
  "Đảng ủy Tổng công ty Quản lý bay Việt Nam — Đảng bộ Công ty TNHH Kỹ thuật Quản lý bay" cần đổi sang cài đặt: gọi
  `organizationSettingsService.get()` (đã đăng nhập) và hiển thị `superiorPartyName` — `partyCommitteeName`.
- Người điều phối: gộp 2 bảng mới vào `InitialCreate`.

## Phát hiện thêm
- (⚪, frontend) `reportService.downloadReport` đặt tên tệp theo chuỗi cố định ở giao diện, bỏ qua `Content-Disposition` của máy chủ
  (interceptor `apiClient` trả về `data` nên mất header). Tên tệp máy chủ tính theo kỳ/Chi bộ (`…_ChiBo_X_Q3_2026.xlsx`) không tới
  người dùng. Task này chỉ thay `{SHORT_NAME}` cho danh sách cán bộ. Đề xuất: đọc `Content-Disposition` trong một hàm tải tệp dùng chung
  (`apiClient.ts`, owner 01).
- (⚪) Mẫu 13 thân văn bản trích tên Hướng dẫn có "Đảng bộ Tổng công ty Quản lý bay Việt Nam"; Excel Mẫu 14 dòng A5 ghi cứng
  "Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 …". Là trích dẫn văn bản căn cứ, không phải tên đơn vị; nếu cần khai báo được, đề xuất
  thêm trường "Văn bản căn cứ" vào cài đặt ở đợt sau.
