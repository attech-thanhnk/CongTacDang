# Báo cáo: Task 19 — Biểu mẫu tập thể, biên bản, báo cáo tổng hợp đúng HD03

- **Branch:** `feat/collective-forms-reports` (từ `46645e5`)
- **Commit cuối:** commit chứa báo cáo này (commit cuối của branch)
- **Ngày:** 2026-09-29

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-84 | done | Xuất Word/PDF Mẫu 07, 08 (từ hồ sơ tập thể) và Mẫu 12 (từ biên bản — hội nghị B3a và B4); template dựng từ biểu mẫu gốc. Bổ sung mục thiếu: Mẫu 07 mục A.I.1–4 (`Sections` jsonb, mã I.1–I.4), Mẫu 08 nhóm nội dung 1–13 (mã trên dòng nhiệm vụ), Mẫu 12 quy chế làm việc / đơn vị báo cáo / chức vụ chủ trì, thư ký / mục 3.2 người dự (`Details` jsonb). Sửa hồ sơ tập thể và biên bản (PUT, kiểm tra phiên bản). Trang Tập thể & Hội nghị nhập đủ mục, nút Word/PDF trên từng hồ sơ/biên bản. | `Documents/Forms/Mau07Data.cs`, `Mau08Data.cs`, `Mau12Data.cs`, `CollectiveFormText.cs`, `Templates/Word/Mau_07_*.docx`, `Mau_08_*.docx`, `Mau_12_*.docx`, `CollectiveEvaluationService.cs`, `CollectiveEvaluationController.cs`, `Application/Reports/Hd03FormCatalog.cs`, `app/collective-evaluations/page.tsx`, `components/collective/*` |
| T-85 | done | Mẫu 14, 15A, 15B: tiêu đề, cột, ghi chú, chữ ký đúng nguyên văn biểu mẫu (Mẫu 14 một trang tính cho mỗi cấp quyết định; 15A/15B đủ mã M1–M16 / M17–M26, cột "Tỷ lệ % xếp loại xuất sắc trong số xếp loại tốt trở lên", dòng "Tổng cộng", ghi chú ưu tiên nhóm chức danh đứng trước). Mẫu 16 là báo cáo Word theo biểu mẫu gốc: số liệu mục I/II tự động theo nhóm chức danh và thẩm quyền, phần nhập tay (số văn bản, nơi gửi, quy chế, ngày hội nghị, đề xuất III.1–3, người ký) lưu nháp theo kỳ + tổ chức Đảng. Bỏ `form-15` (thống kê/kiểm soát trần) → chuyển thành "Báo cáo nội bộ — Kiểm soát tỷ lệ Hoàn thành xuất sắc" (`internal/excellent-quota`); bỏ `form-16` Excel. Danh sách cán bộ đặt tên "Danh sách cán bộ (nội bộ)". Trang Báo cáo: nhóm "Hồ sơ nộp theo HD03" / "Báo cáo nội bộ", chọn kỳ + phạm vi tổ chức Đảng, soạn Mẫu 16. Tên tệp theo quy cách HD03 V.1 (`Mau 16_<tên viết tắt>_Quy III-2026`). | `ReportService.cs`, `IReportService.cs`, `ReportAccessService.cs`, `ExportReportController.cs`, `Documents/Forms/Mau16Data.cs`, `Templates/Word/Mau_16_*.docx`, `Domain/Entities/ReportDraft.cs`, `Configurations/ReportDraftConfiguration.cs`, `app/reports/page.tsx`, `components/reports/Form16DraftPanel.tsx`, `services/reportService.ts` |

### Bảng mẫu HD03 → endpoint → trang

| Mẫu | Định dạng | Endpoint | Trang |
|---|---|---|---|
| 07 | Word/PDF | `GET /api/reports/docx/mau-07/{collectiveRecordId}` | Tập thể & Hội nghị — từng hồ sơ M07 |
| 08 | Word/PDF | `GET /api/reports/docx/mau-08/{collectiveRecordId}` | Tập thể & Hội nghị — từng hồ sơ M08 |
| 11 | Word/PDF | `GET /api/reports/docx/mau-11?periodId&branchId` (không đổi) | Báo cáo (ghi chú: chưa áp dụng Q3/2026) |
| 12 | Word/PDF | `GET /api/reports/docx/mau-12/{meetingId}` | Tập thể & Hội nghị — từng biên bản M12 |
| 13 | Word/PDF | `GET /api/reports/docx/mau-13?periodId&branchId` (không đổi) | Báo cáo |
| 14 | Excel/PDF | `GET /api/reports/form-14?periodId&branchId` | Báo cáo |
| 15A | Excel/PDF | `GET /api/reports/form-15a?periodId&branchId` | Báo cáo |
| 15B | Excel/PDF | `GET /api/reports/form-15b?periodId&branchId` | Báo cáo |
| 16 | Word/PDF | `GET /api/reports/docx/mau-16?periodId&branchId`; nháp `GET/PUT /api/reports/mau-16/draft?periodId&branchId` | Báo cáo — "Soạn báo cáo" |
| Nội bộ | Excel | `GET /api/reports/cadres` | Báo cáo — nhóm nội bộ |
| Nội bộ | Excel/PDF | `GET /api/reports/internal/excellent-quota?periodId&branchId` | Báo cáo — nhóm nội bộ |

Phân quyền: 07/08 — `evaluation.read` hoặc `collective.manage` trên tổ chức của hồ sơ (`IReportAccessService.EnsureCanExportCollectiveAsync`); 12 — `meeting.read`/`meeting.manage` trên đơn vị của biên bản; 14/15A/15B/16/nội bộ — `report.export`, phạm vi tổ chức Đảng qua `ResolveBranchExportScopeAsync` (Bí thư Chi bộ chỉ xuất/lưu nháp của Chi bộ mình). Sửa hồ sơ tập thể: `collective.manage` (guard trên đối tượng); sửa biên bản: `meeting.manage`.

### Các chỗ đã sửa cho khớp nguyên văn biểu mẫu

- **Mẫu 14** trước: "BẢNG TỔNG HỢP KẾT QUẢ ĐÁNH GIÁ, XẾP LOẠI CHẤT LƯỢNG CÁN BỘ", 12 cột tự đặt (Chi bộ, Điểm thẩm định…), dẫn chiếu sai. Nay: "DANH SÁCH ĐÁNH GIÁ VÀ ĐỀ XUẤT XẾP LOẠI QUÝ … NĂM …" / "ĐỐI VỚI CÁN BỘ THUỘC DIỆN … QUYẾT ĐỊNH, PHÊ DUYỆT MỨC XẾP LOẠI", 13 cột đúng tên biểu mẫu (nhóm cột 5–7 "Cá nhân tự chấm điểm, đề xuất mức xếp loại"), dòng số cột 1–13, 3 dòng ghi chú, chữ ký "NGƯỜI LẬP" / "T/M CẤP ỦY (HOẶC TẬP THỂ LÃNH ĐẠO, QUẢN LÝ CƠ QUAN, ĐƠN VỊ)". Cột 4 mã chức danh (task 14), cột 8–11 mức đề xuất/quyết định của từng cấp, cột 12 căn cứ khi xuất sắc/không hoàn thành (giải trình, ý kiến thẩm định, nhận xét).
- **Mẫu 15A/15B** trước: tiêu đề "… - MẪU 15A", cột "Mã | Chức danh | Tổng số…" (không có TT, không có cột tỷ lệ %), tên chức danh tự viết, thêm dòng "Kiểm soát trần" (không có trong biểu mẫu). Nay: đúng 11 cột (TT, Đối tượng, Mã chức danh, Tổng số, 5 mức, Tỷ lệ %, Ghi chú), nhóm cột "Mức xếp loại đề nghị BTVĐUTCT quyết định" (15A) / "Mức xếp loại" (15B), tên đối tượng nguyên văn PDF tr.73–76, ghi chú "Để tránh trùng lặp số liệu…", chữ ký. Cán bộ chưa có mã chuyển sang trang tính "Kiểm tra dữ liệu".
- **"Mẫu 15"** (thống kê/kiểm soát trần theo Chi bộ, ghi nhầm "Điều 12") không có trong HD03 → báo cáo nội bộ, sửa dẫn chiếu thành "HD03 mục III.6".
- **"Mẫu 16"** Excel "BẢNG TỔNG HỢP KẾT QUẢ XẾP LOẠI CÁN BỘ - MẪU 16" → bỏ; Mẫu 16 là văn bản Word đúng biểu mẫu gốc.
- Template 07/08/12/16: bỏ màu chữ đỏ (đánh dấu soạn thảo của file gốc), giữ nguyên chữ in sẵn, bảng, khổ giấy.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx --artifacts-path <tạm>` (`--no-incremental`) | pass | 0 warning, 0 error |
| `dotnet test` — unit | pass | 263 pass, 1 skip có sẵn (`PdfConversionTests.Mau01_ConvertsToPdf…` — máy không có LibreOffice) |
| `dotnet test` — integration (`CONGTACDANG_TEST_PG`, CSDL `ctd_it_*`) | pass | 93 pass, **0 skip** (chạy với migration tạm, xem mục Thay đổi schema) |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | không có cảnh báo lint ở file của task |

Test theo luồng:

| Luồng | Trạng thái | Test |
|---|---|---|
| Template 07/08/12/16 khớp tag lớp dữ liệu, sinh .docx hợp lệ OpenXML, không còn control | pass | `CollectiveFormsTemplateTests.Template_TagsMatchFormDataClass`, `Template_RendersValidDocument_WithoutControls` (+ `OrgSettingsTemplateTests.BundledTemplates_PassValidation_WithoutWarnings` tự áp dụng cho 4 mẫu mới) |
| Nội dung Mẫu 07 (mục I.1–4, điểm), 08 (đủ 13 nhóm), 12 (theo bước, giờ VN, mục 3.2), 16 (số, %, đề xuất) | pass | `CollectiveFormsTemplateTests.Mau07_…`, `Mau08_…`, `Mau12_…` (2 trường hợp), `Mau16_…` |
| Kỳ có hồ sơ đã công bố ở các mức → số đếm/tỷ lệ Mẫu 15A/15B/14; phạm vi Chi bộ | pass | `CollectiveFormsReportsIntegrationTests.R1_…` |
| Mẫu 16: số liệu tự động, lưu nháp + 409 khi phiên bản cũ, xuất Word có tên đơn vị từ cài đặt; Bí thư Chi bộ chỉ Chi bộ mình (403 Chi bộ khác) | pass | `R2_…` |
| Mẫu 07/08: lưu mục, sửa (phiên bản, 409), xuất .docx; mục/nhóm sai → 400; Chi bộ khác → 403; sai loại → 400 | pass | `R3_…` |
| Mẫu 12: lưu/sửa mục, xuất theo bước B4; Chi bộ khác → 403; biên bản M13 → 400 | pass | `R4_…` |
| Báo cáo cũ `form-15`, `form-16` → 404; báo cáo nội bộ còn | pass | `R5_…` |

Sửa kỳ vọng test cũ do task đổi hành vi (RULES 8.2): `AuthorizationMatrixTests.BranchReports_ResolveScope` (`form-15` → `form-15a`), `DynamicOrgIntegrationTests.ReadFormCountsAsync` (cột mã 3, tổng số 4 theo bố cục 15A/15B), `OrgSettingsTemplateIntegrationTests.S1_…` (Mẫu 14: tiêu đề trái ở A2/A3, dòng địa danh ở I3).

## Thay đổi schema (cần migration)

- `collective_evaluation_records.Sections` — `jsonb NOT NULL DEFAULT '{}'::jsonb` (mục con theo mã, Mẫu 07: I.1–I.4).
- `evaluation_meetings.Details` — `jsonb NOT NULL DEFAULT '{}'::jsonb` (mục Mẫu 12: workingRules, reportingUnit, chairTitle, secretaryTitle, attendees[]).
- Bảng mới `report_drafts` (entity `ReportDraft`, cấu hình `ReportDraftConfiguration`): `Id`, `xmin`, `PeriodId` (FK kỳ, cascade), `PartyCellId` (FK tổ chức Đảng, null = toàn Đảng bộ, cascade), `FormCode` varchar(10), `Content` jsonb, audit; unique index (`PeriodId`, `PartyCellId`, `FormCode`) `NULLS NOT DISTINCT`, index `PartyCellId`.
- Không commit migration. Cách chạy test tích hợp cục bộ: đặt `ArtifactsPath=<thư mục tạm ngoài repo>`, `UseArtifactsOutput=true` (biến môi trường), `dotnet build backend/src/CongTacDang.Api --artifacts-path <tạm>`, rồi `dotnet ef migrations add TmpK19Local -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api -o Data/Migrations` → chạy `dotnet test` → `dotnet ef migrations remove -f` và `git checkout` snapshot (ef tool ghi lại `ToTable("role_permissions", (string)null)` — thay đổi định dạng, không phải schema).

## Key config mới

Không có.

## Thay đổi hành vi API / breaking change

- Bỏ `GET /api/reports/form-15`, `GET /api/reports/form-16` → 404. Thêm `GET /api/reports/internal/excellent-quota`, `GET /api/reports/docx/mau-07/{id}`, `mau-08/{id}`, `mau-12/{meetingId}`, `mau-16`, `GET/PUT /api/reports/mau-16/draft`, `PUT /api/evaluations/collective-records/{id}`, `PUT /api/evaluations/meetings/{id}`, `GET /api/evaluations/collective-forms/catalog`.
- `form-14/15a/15b`: bố cục bảng tính mới (dòng 1 "Mẫu …", tiêu đề trái dòng 2–3, bảng từ dòng 8), Mẫu 14 có 2 trang tính; tên tệp theo quy cách HD03 (`Mau 14_<viết tắt>_Quy III-2026.xlsx`).
- DTO hồ sơ tập thể thêm `sections`; biên bản thêm `details`. Mẫu 07 chỉ nhận mã mục I.1–I.4, Mẫu 08 bắt buộc nhóm 1–13 → 400 kèm thông báo.
- `reportService.ts`: bỏ `REPORT_LIST`, `exportForm14/15/15A/15B/16`, `exportMau11Docx/13Docx`, `printDocument` (chỉ trang Báo cáo dùng); thay bằng `REPORTS` và các hàm mới. `fetchReportBlob`/`saveBlob`/`exportMau01/02/10Docx` giữ nguyên.

## Cần phối hợp

- **Người điều phối:** sinh migration cho 3 thay đổi schema ở trên khi gộp `InitialCreate`.
- **`WordFormCatalog.cs`**: task 18 (09B/09C/9D) và task 20 (Mẫu 17) cũng thêm dòng vào cuối danh mục → xung đột merge dạng "cùng thêm dòng", giữ cả hai.
- **`Application/DTOs/EvaluationDtos.cs`**: chỉ sửa vùng "Hồ sơ tập thể, biên bản" (thêm `Sections`, `Details`); task 18 có thể sửa vùng khác cùng file.
- **`components/evaluations/EvaluationPdfModal.tsx`** (không thuộc task này): nhánh `mau15` còn gọi `/reports/form-15` (đã bỏ) — nhánh này không được trang nào mở, nhưng nên đổi sang `form-15b` hoặc bỏ nhánh `mau14`/`mau15` (task sở hữu trang hồ sơ).
- **File cần người điều phối xóa:** không có.

## Phát hiện thêm

- **Mẫu 13** (thuộc phạm vi nhưng task không yêu cầu làm lại): template hiện tại khác biểu mẫu gốc — chưa tách mục I (BTVĐUTCT) / II (Đảng ủy cơ sở), cột 8 "Chưa đánh giá, xếp loại" để trống, cột "Ghi chú" in tỷ lệ % kèm mức tập thể đề xuất, dòng tổ kiểm phiếu, số phiếu phát ra/thu về dùng chung một số. Đề xuất mã mới (🟡) để dựng lại từ biểu mẫu gốc như Mẫu 12.
- HD03 V.1 yêu cầu **Mẫu 08 lập trên Excel** ("trừ các Mẫu: 07, 09C, 12, 13, 16"); task yêu cầu Word nên đã làm Word (+PDF). Nếu cần nộp đúng định dạng, bổ sung bản Excel cho Mẫu 08 (cùng dữ liệu).
- Mẫu 14 cột 13 "Đề xuất nội dung liên quan về công tác cán bộ" chưa có trường dữ liệu trên hồ sơ → để trống.
- Số liệu 15A/15B/16 khi kỳ chưa kết thúc dùng mức đề xuất gần nhất (không tính tự đề xuất); cần nghiệp vụ xác nhận có muốn chỉ đếm hồ sơ đã quyết định.
- "Căn cứ Hướng dẫn số …-HD/TVĐU ngày …" trên Mẫu 07/12/16 giữ chỗ trống như biểu mẫu gốc; có thể bổ sung vào *Thông tin đơn vị* (số, ngày văn bản hướng dẫn) để điền tự động.
- Công cụ dựng template: script Python (lxml) cắt phần thân từ file .docx gốc theo chỉ số khối, gắn Content Control bao đúng đoạn chữ mặc định (so khớp toàn văn đoạn, sai là dừng). Script chạy ngoài repo; nếu cần tái lập, nên đưa vào `tools/` (chưa làm để giữ phạm vi file).
