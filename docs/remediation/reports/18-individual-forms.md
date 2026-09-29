# Báo cáo: Task 18 — Biểu mẫu cá nhân phải nộp (09B, 09C, 9D)

- **Branch:** `feat/individual-forms`
- **Commit cuối:** commit chứa báo cáo này (ngay sau `e6925ca`)
- **Ngày:** 2026-09-29

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-82 | done | Hồ sơ lưu nội dung Mẫu 09C (jsonb theo mã mục), Mẫu 9D (jsonb, dòng theo mã trục, đúng cột (2)–(7) mẫu gốc) và phần tự luận theo trục của Mẫu 09B (jsonb). Nhập trong bước `B2_SELF_SCORE` cùng phiếu tự chấm; kiểm tra mã mục/mã trục theo bộ tiêu chí của kỳ, độ dài (09C mặc định 6.000 ký tự ≈ 02 trang A4). Bộ tiêu chí có `requiredForms` và `selfAssessmentSections`; trục có `formTitle`/`formGuidance` (tiêu đề + nội dung gợi ý in trên 09B, chép nguyên văn mẫu gốc). Lịch sử hồ sơ ghi "cập nhật Mẫu 09C, Mẫu 9D…". Giao diện: form 09C, bảng 9D theo trục, 3 ô tự luận mỗi trục (09B) trong phiếu tự chấm; hiển thị chỉ đọc trên trang hồ sơ. | `Domain/Evaluation/CriteriaSetForms.cs`, `CriteriaSetDefaultsForms.cs`, `RecordFormContent.cs`, `CriteriaSetContent.cs`, `Entities/EvaluationRecord.cs`, `EvaluationWorkflowService.cs` (phần tự chấm), `frontend/components/evaluations/SelfScoreForm.tsx`, `IndividualFormInputs.tsx` |
| T-83 | done | Template Word 09A, 09B, 09C, 9D **dựng từ file biểu mẫu gốc** (cắt đúng phần của mẫu, giữ nguyên bố cục/định dạng, chỉ thêm Content Control; tên đơn vị qua `ORG_PARTY_NAME`, địa danh `ORG_LOCATION`). Lớp dữ liệu `Mau09AData`, `Mau09BData`, `Mau09CData`, `Mau9DData`. Controller mới `RecordFormsController` (`GET api/reports/docx/record/{recordId}` danh sách mẫu áp dụng, `GET …/{recordId}/{formCode}?format=pdf`), kiểm tra quyền xem hồ sơ bằng `IAuthorizationGuard` trên hồ sơ. Đăng ký 4 mẫu vào danh mục quản lý biểu mẫu (task 17) → thay được qua giao diện. Trang hồ sơ: khối "Biểu mẫu của hồ sơ" (Word/PDF) theo `requiredForms`; nút in nhanh 01/02/10 chỉ hiện khi kỳ áp dụng. | `Infrastructure/Documents/Forms/Mau09*.cs`, `Mau9DData.cs`, `Infrastructure/Templates/Word/Mau_09A_…`, `Mau_09B_…`, `Mau_09C_…`, `Mau_9D_…`, `Infrastructure/Services/RecordFormService.cs`, `Api/Controllers/RecordFormsController.cs`, `WordFormCatalog.cs`, `DocumentExtensions.cs`, `frontend/components/evaluations/RecordFormsPanel.tsx`, `app/evaluations/[recordId]/page.tsx`, `docs/bieu-mau.md` |

## Cấu trúc biểu mẫu gốc (trích từ `docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx`)

Đọc bằng script trên `word/document.xml` (phần tử thân văn bản 175–245). Nguyên văn các mục:

**Mẫu số 09C** — "BẢN TỰ ĐÁNH GIÁ, XẾP LOẠI CỦA CÁ NHÂN / Quý …., Năm …. / (Các trường hợp thuộc đối tượng đánh giá, xếp loại chất lượng cán bộ hằng quý)"
```
Họ và tên: …  Ngày sinh: …
Chức vụ Đảng: …
Chức vụ chính quyền: …
Chức vụ đoàn thể: …
Đơn vị công tác: …
I. Tự đánh giá kết quả thực hiện chức trách, nhiệm vụ được giao
   Trên cơ sở nhiệm vụ được giao, cá nhân tự đánh giá về kết quả thực hiện nhiệm vụ theo quý như sau:
   .……………………
   (Lưu ý: Viết tóm tắt theo kết quả 6 trục. Đối với nhiệm vụ trọng tâm, then chốt, … phải kết luận rõ … theo 01 trong 03 mức: Mức 1-…; Mức 2-…; Mức 3-…)
II. Tự đề xuất xếp loại mức chất lượng
   1. Kết quả tự chấm điểm: - Nhóm tiêu chí chung: …/30; - Nhóm tiêu chí về kết quả thực hiện chức trách, nhiệm vụ: …/70; Tổng điểm: …/100
   2. Tự đề xuất xếp loại mức chất lượng: ……
CÁ NHÂN TỰ ĐÁNH GIÁ (Ký, ghi rõ họ tên) | XÁC NHẬN CỦA CHI BỘ (Xác lập thời điểm, ký, ghi rõ họ tên và đóng dấu)
Lưu ý: Đề nghị tập trung đánh giá khái quát kết quả thực hiện chỉ tiêu, nhiệm vụ. Nội dung trình bày trong phạm vi không quá 02 trang A4.
```
→ Mẫu gốc **chỉ có một mục tự luận** (I); không có các mục "ưu điểm / hạn chế / nguyên nhân / phương hướng". Phần II lấy từ điểm và mức tự đề xuất đã lưu. Bộ mặc định khai báo đúng 1 mục `I` (tiêu đề, câu dẫn, lưu ý nguyên văn); thêm mục khác được qua bộ tiêu chí mà không đổi schema.

**Mẫu 9D** — "PHỤ LỤC / KẾT QUẢ THỰC HIỆN NHIỆM VỤ, CÔNG VIỆC ĐƯỢC GIAO TRONG QUÝ…. NĂM….."
```
Họ và tên: … / Chức vụ (Đảng, Chính quyền, Đoàn thể): … / Đơn vị công tác: … / Chi bộ, Đảng bộ đang sinh hoạt: …
KẾT QUẢ THỰC HIỆN
| TT (1) | Nội dung nhiệm vụ, công việc (2) | Thời hạn hoàn thành (3) | Tình hình thực hiện (4) | Sản phẩm hoàn thành (5) | Đánh giá tiến độ (6) | Ghi chú (7) |
| Trục 1 | … |
| …      |
| Trục 6 | … |
XÁC NHẬN CỦA CHI BỘ (Xác lập thời điểm, ký, ghi rõ họ tên) | CÁ NHÂN TỰ ĐÁNH GIÁ (Ký, ghi rõ họ tên)
```

**Mẫu số 09B** — Phần II "NHÓM TIÊU CHÍ VỀ KẾT QUẢ THỰC HIỆN CHỨC TRÁCH, NHIỆM VỤ (70 điểm)", cột: TT | Nội dung tiêu chí (tiêu đề "TRỤC (n) – …" + gạch đầu dòng gợi ý) | Mục tiêu, nhiệm vụ đề ra | Điểm tối đa | Điểm tự chấm | Tóm tắt kết quả sản phẩm thực tế; Tài liệu minh chứng | Ghi chú. Phần I bảng 17 tiêu chí con (Đảm bảo / Không đảm bảo / Điểm tối đa / Điểm đạt / Tóm tắt, minh chứng); phần III tổng hợp.

## Thiết kế dữ liệu (T-82)

- `CriteriaSetContent.RequiredForms` (mã `01, 02, 09A, 09B, 09C, 9D, 10`; "09D"/"Mẫu 09C" được chuẩn hóa). **Mẫu tự chấm luôn theo `SelfScoreForm` của bộ**; mã 09A/09B trong danh sách bị bỏ qua (`CriteriaSnapshot.ApplicableForms()`), nên đổi mẫu tự chấm của bản nháp không làm danh sách sai. Bộ mặc định: 09B → `09B, 09C, 9D, 10`; 09A → `01, 02, 09A, 09C, 9D, 10` (theo trích xuất HD03 mục 7–8: Q3/2026 chưa áp dụng 01–06, 09A, 11; Mẫu 10 không bị loại trừ).
- `CriteriaSetContent.SelfAssessmentSections` (mã, tiêu đề, câu dẫn, lưu ý, `MaxLength` 100–20.000, `Required`). Mặc định `Required = false`: văn bản không yêu cầu hệ thống chặn nộp khi để trống — bật được trong bộ tiêu chí.
- `ResultAxis.FormTitle` / `FormGuidance`: tiêu đề và nội dung gợi ý in trên Mẫu 09B (chép nguyên văn mẫu gốc cho 6 trục mặc định); trống → dựng từ tên / nội dung áp dụng của trục.
- Hồ sơ: `SelfAssessment` (`{ "I": "…" }`), `TaskResults` (`[{ axisCode, content, deadline, status, product, progress, note }]`, tối đa 100 dòng, ô dài ≤ 2.000, ô ngắn ≤ 500 ký tự), `AxisNotes` (`{ "T1": { target, result, note } }`, ≤ 4.000 ký tự/ô). Yêu cầu tự chấm có 3 trường mới; **null = giữ nội dung đã lưu** (luồng cũ không gửi vẫn chạy); trường của mẫu không áp dụng cho kỳ bị bỏ qua.
- Sửa tới khi nộp / trả lại → sửa tiếp / kỳ khóa hoặc đã công bố → chỉ đọc: theo đúng máy trạng thái và luật kỳ hiện có của hành động `SubmitSelfScore` (không có đường ghi riêng). Không đổi công thức điểm.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx` | pass | 0 warning, 0 error (`--artifacts-path` ngoài repo) |
| `dotnet test` — unit | pass | 269 passed, 1 skipped (`PdfConversionTests.Mau01_ConvertsToPdf…` — máy không có LibreOffice, có từ trước) |
| `dotnet test` — integration | pass | **90 chạy, 0 skip, 0 fail** (`CONGTACDANG_TEST_PG`, CSDL `ctd_it_*`), với migration tạm (xem dưới) |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | 3 warning ESLint có sẵn ở `app/audit/page.tsx`, `DocumentViewerModal.tsx` (ngoài phạm vi) |
| Template khớp tag / hợp lệ OpenXML | pass | `IndividualFormTests.Template_*` + `OrgSettingsTemplateTests.BundledTemplates_PassValidation_WithoutWarnings` (tự chạy cho `MAU_09A/09B/09C/9D` qua danh mục) |

### Luồng kiểm thử

| Luồng | Trạng thái | Test |
|---|---|---|
| Unit: cấu hình `requiredForms`, mục 09C, tiêu đề trục (mặc định + lỗi) | pass | `IndividualFormTests.Defaults_…`, `Validate_RejectsInvalidFormConfiguration`, `ApplicableForms_…`, `Content_RoundTripsJson_…` |
| Unit: validate 09C / 9D / ghi chú trục + chuẩn hóa JSON | pass | `SelfAssessment_Validation_AndJson`, `TaskResults_Validation_AndJson_OrderedByAxis`, `AxisNotes_Validation_AndJson` |
| Unit: dữ liệu tag, xuất ra đúng nội dung | pass | `Template_TagsMatchFormDataClass`, `Template_RendersValidDocument_WithoutLeftoverTags`, `Mau09B_…`, `Mau09C_…`, `Mau9D_…`, `Mau09A_…`, `NotYetSelfScored_…` |
| Tích hợp: nhập 09C + 9D (+ 09B theo trục) → nộp → xuất 09B/09C/9D .docx hợp lệ, không còn tag, có nội dung đã nhập, tên đơn vị từ cài đặt; lịch sử ghi thay đổi; 400 khi quá dài / trục lạ | pass | `IndividualFormsIntegrationTests.F1_SelfScoreWith09C9D_Submit_Export09B09C9D` |
| Tích hợp: người ngoài phạm vi → 403 (danh sách + xuất); không quyền → 403; mẫu không áp dụng → 409; mã lạ / format lạ → 400; hồ sơ không có → 404 | pass | F1 |
| Tích hợp: Chi bộ trả lại → sửa được; đã công bố → 409 và nội dung giữ nguyên; kỳ khóa dữ liệu → 409 | pass | `F2_ReturnedRecord_IsEditable_LockedOrPublished_IsReadOnly` |
| Xuất PDF (`?format=pdf`) | không chạy | Máy không có LibreOffice; dùng chung `IPdfConverter` như các mẫu hiện có |
| Giao diện trên trình duyệt | không chạy | Chỉ kiểm bằng `tsc` + `npm run build` |

### Migration tạm (không commit)

```bash
# sinh tạm để test tích hợp (MigrateAsync) chạy được với 3 cột mới
ConnectionStrings__Default="Host=localhost;Database=ctd_it_j18;Username=x;Password=x" \
  dotnet ef migrations add Task18Temp -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api -o Data/Migrations
# … chạy dotnet test (có CONGTACDANG_TEST_PG) …
ConnectionStrings__Default="…" dotnet ef migrations remove --force -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api
git restore backend/src/CongTacDang.Infrastructure/Data/Migrations/CongTacDangDbContextModelSnapshot.cs   # EF đổi 1 dòng định dạng khi revert
```
Branch hiện **không có** migration: test tích hợp chỉ chạy được sau khi người điều phối gộp `InitialCreate`.

## Thay đổi schema (cần migration)

Bảng `evaluation_records`, thêm 3 cột:
- `SelfAssessment` jsonb NOT NULL (mặc định entity `{}`) — Mẫu 09C.
- `TaskResults` jsonb NOT NULL (mặc định entity `[]`) — Mẫu 9D.
- `AxisNotes` jsonb NULL — phần tự luận theo trục Mẫu 09B.

Nội dung bộ tiêu chí (`criteria_sets.Content`, `evaluation_periods.CriteriaSnapshot`, jsonb) có thêm `requiredForms`, `selfAssessmentSections`, `axes[].formTitle`, `axes[].formGuidance` — không đổi cột.

## Key config mới
Không có.

## Thay đổi hành vi API / breaking change
- Mới: `GET /api/reports/docx/record/{recordId}` → `ApiResponse<RecordFormDto[]>` (`code`, `name`); `GET /api/reports/docx/record/{recordId}/{formCode}?format=docx|pdf` → tệp (409 mẫu không áp dụng, 400 mã/format lạ, 403 ngoài phạm vi, 404 không có hồ sơ). 01/02/10 gọi lại bộ xuất hiện có.
- `POST /api/evaluations/records/{id}/self-score/submit`: thêm `selfAssessment`, `taskResults`, `axisNotes` (tùy chọn; null = giữ nguyên).
- `EvaluationRecordDto`: thêm `selfAssessment`, `taskResults`, `axisNotes`.
- Bộ tiêu chí: `Validate` báo lỗi mã biểu mẫu lạ/trùng, thiếu mục 09C khi áp dụng 09C, mục 09C sai.
- Bộ tiêu chí **đã có trong CSDL trước task này** không có `requiredForms` → hồ sơ chỉ còn mẫu tự chấm trong danh sách xuất, không có phần nhập 09C/9D (không giữ tương thích cũ; CSDL mới seed bộ mặc định có đủ).

## Cần phối hợp
- **Người điều phối:** gộp migration `InitialCreate` (3 cột trên).
- **Trang bộ tiêu chí (`app/criteria/[id]`, `services/criteriaService.ts` — ngoài phạm vi):** chưa có ô sửa `requiredForms`, `selfAssessmentSections`, `formTitle`/`formGuidance` của trục. Trang hiện giữ nguyên các trường này khi lưu (cập nhật theo kiểu spread), nên bộ nhân bản từ bộ mặc định vẫn đúng; cần thêm giao diện chỉnh nếu nghiệp vụ muốn đổi.
- `WordFormCatalog.cs` (task 17) và `EvaluationDtos.cs`, `EvaluationMapping.cs` (module đánh giá): chỉ thêm dòng — lưu ý khi merge với task 19/20 nếu cũng thêm vào đây.
- Nghiệp vụ xác nhận: (1) Mẫu 10 có thuộc danh sách áp dụng Q3/2026 không (văn bản không nói rõ — đang để có); (2) có bắt buộc nhập mục 09C khi nộp không (đang để không bắt buộc, bật bằng `required`); (3) giới hạn 6.000 ký tự ≈ 02 trang A4.

## Phát hiện thêm
- Hồ sơ cán bộ không có **ngày sinh** → dòng "Ngày sinh" của 09A/09B/09C giữ dòng chấm. Đề xuất bổ sung trường nếu cần in.
- Mẫu 9D dòng "Chi bộ, Đảng bộ đang sinh hoạt" chỉ in tên Chi bộ (ảnh chụp trên hồ sơ).
- Mẫu gốc dùng khoảng trắng không ngắt (U+00A0) sau dấu hai chấm ở 09C ("Chức vụ Đảng: …") — giữ nguyên trong template.
- Tiêu đề trái Mẫu 9D gốc canh bằng tab: tên Đảng bộ dài (cài đặt đơn vị) có thể đẩy "ĐẢNG CỘNG SẢN VIỆT NAM" xuống dòng; sửa được trong file mẫu qua giao diện.
- Công cụ dựng template (cắt phần mẫu từ file gốc, gắn Content Control) chạy một lần trong thư mục tạm, không commit; template sửa tiếp bằng Word theo `docs/bieu-mau.md`.
