# Báo cáo tích hợp đợt 8 (task 18 + 19 + 20)

- **Branch:** `chore/wave8-integration` (fast-forward tới `main` @ `46645e5` trước khi sửa)
- **Gộp:** `feat/individual-forms` (task 18) → `feat/collective-forms-reports` (task 19) → `feat/post-publish` (task 20), đều `--no-ff`
- **Ngày:** 2026-09-29

## Các commit

| Commit | Nội dung |
|---|---|
| `752c9c2` | merge task 18 (không xung đột) |
| `c419675` | merge task 19 — xung đột `docs/bieu-mau.md` (bảng tag: giữ dòng 09A/09B/09C/9D và 07/08/12/16, ghép hai đoạn ghi chú) |
| `e2f6d99` | merge task 20 — xung đột `WordFormCatalog.cs` (giữ 07/08/12/16 và 17) và `docs/bieu-mau.md` (thêm dòng `MAU_17`); `PermissionCodes`, `DataSeeder`, `EvaluationDtos.cs`, test kỳ vọng tự gộp |
| `ded0a1e` | Mẫu 14 cột 13 "Đề xuất nội dung liên quan về công tác cán bộ" |
| `d3ee6d6` | cửa sổ cảnh báo "kế hoạch Mẫu 17 cần lập" (`improvementPlanAlertDays`) |
| `a4506d7` | Mẫu 13 dựng lại theo biểu mẫu gốc (theo biên bản); Mẫu 08 bản Excel |
| `a62f7eb` | trang sửa bộ tiêu chí: `requiredForms`, mục 09C, tiêu đề/gợi ý trục 09B, tham số sau công bố |
| `47f2b4f` | thanh bên bỏ `/forms`; `EvaluationPdfModal` qua API biểu mẫu của hồ sơ |
| `b8a80ef` | gộp lại một `InitialCreate` |
| `934c465` | dữ liệu mẫu đợt 8 + `SampleDataSeedTests` |
| `6733459` | test cửa sổ cảnh báo Mẫu 17, cột 13 Mẫu 14 qua B4 nội bộ/cấp trên |
| `b799d8b`, `ac7e167` | tài liệu, key cấu hình `Notifications:DueSoonDays` |
| `56a3e91` | bỏ tham chiếu `form-15`/`form-16` đã gỡ |
| `645afde` | Mẫu 12 mất dấu chấm câu sau chỗ điền quý; danh mục biểu mẫu Word theo số mẫu |
| `e337889` | `DataSeederAuthorizationTests` theo dữ liệu mẫu mới |
| `cad23c0` | `tools/templates/` — script dựng template Mẫu 13, sửa Mẫu 12 |

## 1. Merge

Build sau 3 merge: 0 lỗi, 0 warning; unit 301 pass. File Word nhị phân không xung đột (mỗi task thêm template riêng).

## 2. Phần còn thiếu đã hoàn thiện

| Việc | Thay đổi | File chính |
|---|---|---|
| Trang sửa bộ tiêu chí | Khối "Biểu mẫu cá nhân áp dụng" (ô chọn 01/02/09C/9D/10; 09A/09B tự theo mẫu tự chấm, khóa); bảng mục Mẫu 09C (mã, tiêu đề, câu dẫn, lưu ý, số ký tự 100–20.000, bắt buộc khi nộp; thêm/xóa); mỗi trục thêm dòng "Mẫu 09B" (tiêu đề, nội dung gợi ý); ô số ngày cảnh báo kế hoạch Mẫu 17 cạnh `publishScores`/mức bắt buộc. `quickChecks` kiểm tra: áp dụng 09C phải có ≥ 1 mục, mã mục hợp lệ/không trùng, tiêu đề, giới hạn ký tự, số ngày 1–3650 (máy chủ `Validate` kiểm lại đủ). Xem chỉ đọc (`CriteriaSummary`) hiện mẫu áp dụng, mục 09C, tiêu đề trục, tham số sau công bố | `app/criteria/[id]/page.tsx`, `services/criteriaService.ts`, `components/evaluations/CriteriaSummary.tsx`, `Domain/Evaluation/CriteriaSetContent.cs` |
| Thanh bên / liên kết chết | Bỏ mục "Biểu mẫu" (`/forms`), bỏ tiêu đề `/forms` ở header (thêm tiêu đề cho `/criteria`, `/admin/settings`, `/admin/templates`, `/change-password`). Rà mọi `href`/`push` trong `frontend/{app,components,contexts,services}`: đều trỏ trang có thật | `components/layout/AppSidebar.tsx`, `AppHeader.tsx` |
| `EvaluationPdfModal` | Viết lại: xem/tải PDF qua `GET /reports/docx/record/{recordId}/{mã}?format=pdf`, chọn giữa các mẫu kỳ áp dụng (từ API biểu mẫu của hồ sơ). Bỏ nhánh `mau13`/`mau14`/`mau15` (gọi `/reports/form-15`, Mẫu 13 theo kỳ) và nhánh chết `mau09`/`individual`; xóa `EvaluationPrintTemplate.tsx` (chỉ còn khai báo kiểu). Trang hồ sơ: một nút "Xem bản in (PDF)" thay 3 nút cứng 01/02/10 | `components/evaluations/EvaluationPdfModal.tsx`, `app/evaluations/[recordId]/page.tsx` |
| **Mẫu 13** đúng biểu mẫu gốc | Template dựng lại từ phần "Mẫu số 13" của file gốc (`tools/templates/build_mau13.py`, xem mục 4): tiêu đề trái, "BIÊN BẢN KIỂM PHIẾU", căn cứ, mục I (thời gian, địa điểm, thành phần, 3.2, chủ trì, thư ký), mục II (Tổ kiểm phiếu — lặp, người đầu "Tổ trưởng"; số phiếu phát ra/thu về/hợp lệ/không hợp lệ), bảng 9 cột đúng gốc với **mục I** (BTVĐUTCT) / **mục II** (Đảng ủy/Chi ủy cơ sở) theo `ApprovalAuthority` ảnh chụp trên hồ sơ, cột 8 **"Chưa đánh giá, xếp loại"**, cột Ghi chú = ghi chú dòng kiểm phiếu; giờ kết thúc, nơi lưu; chữ ký Tổ trưởng / Chủ trì có họ tên. Không còn nhét "%(mức)" vào Ghi chú, không dùng chung một số cho phát ra/thu về. Xuất **theo từng biên bản** có kết quả kiểm phiếu: `GET /api/reports/docx/mau-13/{meetingId}` (quyền như Mẫu 12; chưa có kết quả → 400); bỏ `GET /api/reports/docx/mau-13?periodId&branchId`. Dữ liệu bổ sung: `EvaluationMeetingVoteSummary.VotesNotRated` (cột), biên bản `Details` jsonb thêm `countingCommittee`, `ballotsIssued/Collected/Valid/Invalid` (kiểm tra: không âm, phát ra ≤ có mặt, thu về ≤ phát ra, hợp lệ + không hợp lệ = thu về). Giao diện: MeetingEditor nhập Tổ kiểm phiếu, số phiếu, cột "Chưa đánh giá" (chỉ gửi hồ sơ đã nhập phiếu); ô phiếu ở B3a/B4 thêm "Chưa đánh giá"; nút Mẫu 13 Word/PDF trên biên bản có kết quả kiểm phiếu; trang Báo cáo chuyển Mẫu 13 sang nhóm "xuất từ biên bản" | `Documents/Forms/Mau13Data.cs`, `Templates/Word/Mau_13_BienBanKiemPhieu.docx`, `ReportService.cs`, `ExportReportController.cs`, `CollectiveEvaluationService.cs`, `EvaluationWorkflowService.cs`, `EvaluationMeeting.cs`, `CollectiveReportDtos.cs`, `components/collective/MeetingEditor.tsx`, `app/collective-evaluations/page.tsx`, `services/reportService.ts` |
| **Mẫu 08 Excel** (HD03 V.1) | `GET /api/reports/form-08/{collectiveRecordId}[?format=pdf]`: tiêu đề "Mẫu số 08", tiêu đề trái/phải, "BÁO CÁO TỔNG HỢP … QUÝ III/NĂM 2026", 6 cột nguyên văn + dòng số cột, 13 nhóm nội dung (nhóm 1–7 chưa nhập giữ "- Nhiệm vụ 1: …"), ghi chú, chữ ký "NGƯỜI LẬP" / "T/M ĐẢNG ỦY (CHI BỘ)"; cùng dữ liệu bản Word (dùng `Mau08Data`); tên tệp `Mau 08_<viết tắt>_Quy III-2026.xlsx`. Word/PDF giữ nguyên. Nút Excel trên hồ sơ M08 | `ReportService.ExportForm08ExcelAsync`, `ExportReportController`, `app/collective-evaluations/page.tsx` |
| **Mẫu 14 cột 13** | `EvaluationRecord.CadreWorkProposal` (≤ 2.000 ký tự, tùy chọn) nhập ở B4: quyết định nội bộ (`decision`) và ghi nhận kết quả cấp trên (`external/B4_DECISION`; bước khác gửi trường này → 400); in cột 13 Mẫu 14; hiện trên trang hồ sơ | `EvaluationWorkflowService.cs`, `EvaluationDtos.cs`, `WorkflowProfileDtos.cs`, `ReportService.cs`, `components/evaluations/RecordActionPanel.tsx` |
| Cảnh báo kế hoạch Mẫu 17 | Tham số bộ tiêu chí `improvementPlanAlertDays` (1–3650, mặc định 90; JSON cũ → 90). Nhóm "Kế hoạch 30-60-90 ngày cần lập / duyệt" chỉ xét kỳ `Open`/`Locked` và kỳ `Closed` có `StatusChangedAt` trong N ngày (N theo ảnh chụp bộ tiêu chí của từng kỳ). Repository nạp danh sách kỳ trước, chỉ truy vấn hồ sơ của kỳ trong cửa sổ | `CriteriaSetContent.cs`, `ImprovementPlanService.InAlertWindow`, `PostPublishWorkQueue.cs`, `PostPublishRepository.cs` |
| `requiredForms` mặc định | Đã khớp cột "Q3/2026" (hd03-trich-xuat mục 7–8): bộ 09B → `09B, 09C, 9D, 10`; bộ 09A (từ 2027, áp dụng đầy đủ) → `01, 02, 09A, 09C, 9D, 10`. Mẫu 10 ("Không nêu") giữ theo quyết định task 18 — **chờ xác nhận**. Mẫu 07/08/12–16 là cấp tổ chức, không thuộc `requiredForms`. Test `Wave8FormsTests.DefaultCriteriaSets_RequiredForms_MatchHd03Q3Column` | `CriteriaSetDefaultsForms.cs` (không đổi) |
| Sửa thêm | Mẫu 12: control `PERIOD_TEXT` bao cả dấu chấm câu ("quý...." → bản xuất "quý III/2026 Cụ thể…") → tách dấu chấm (`tools/templates/fix_mau12_period.py`, chạy lại an toàn); `WordFormCatalog` xếp theo số mẫu; chú thích "Mẫu 15" của `branch-quotas` | `Mau_12_BienBanHoiNghi.docx`, `WordFormCatalog.cs`, `EvaluationController.cs` |

## 3. Dữ liệu mẫu (`Database:SeedSampleData=true`)

Thêm tài khoản `canbo.kh` (Phó Trưởng phòng PH-KH, Chi bộ Khối Văn phòng) — hồ sơ **đã công bố Mức C**. Tài khoản mẫu: 10; hồ sơ: 7 (6 trạng thái).

- Mọi hồ sơ đã tự chấm có Mẫu 09C (mục I), 9D (một dòng mỗi trục), tự luận theo trục 09B.
- Hồ sơ tập thể Chi bộ Khối Kỹ thuật: Mẫu 07 (đủ I.1–I.4, II–VI, điểm 27/63), Mẫu 08 (nhóm 1, 2, 8, 13).
- Biên bản Mẫu 12 hội nghị tập thể lãnh đạo Phòng Kỹ thuật (B3a; mục 3.2, chức vụ chủ trì/thư ký, Tổ kiểm phiếu, số phiếu, kết quả kiểm phiếu của hồ sơ Trưởng phòng Kỹ thuật, gắn `CollectiveMeetingId`).
- Biên bản kiểm phiếu (M13) hội nghị Đảng ủy Công ty (B4): Tổ kiểm phiếu 2 người, số phiếu 7/7/7/0, dòng Giám đốc (mục I) và `canbo.kh` (mục II, có phiếu "Chưa đánh giá").
- Hồ sơ `canbo.kh`: kế hoạch Mẫu 17 **Draft** (người hỗ trợ, mốc 30 ngày), kiến nghị **Submitted** (liên quan bước thẩm định).
- Bản nháp Mẫu 16 toàn Đảng bộ.
- Readiness kỳ mẫu: `ready = true`, 0 cảnh báo, 5 hồ sơ kiểm tra (2 hồ sơ đã công bố không tính).

`SampleDataSeedTests` thêm `AssertWave8SampleAsync`: API trả 09C/9D/ghi chú trục và xuất 09B/09C/9D; Mẫu 07/08 (Word, Excel); Mẫu 12, Mẫu 13 (2 biên bản); kế hoạch bắt buộc + Draft; kiến nghị đang xem xét; nhóm `APPEALS` (Văn phòng Đảng ủy), `IMPROVEMENT_PLANS` (Giám đốc); nháp Mẫu 16 có phiên bản. `DataSeederAuthorizationTests`: 10 tài khoản, 7 hồ sơ.

## 4. Template

- `Mau_13_BienBanKiemPhieu.docx`: `py -3 tools/templates/build_mau13.py "docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx" <đích>` — lấy gói của file gốc (styles, numbering, theme), thân = các khối từ đoạn "Mẫu số 13" tới section của nó; tách run giữ `rPr` của từng ký tự, bọc Content Control đúng đoạn chữ mặc định (sai là dừng); dòng trống mục I/II → `repeat:` + bản sao trống trong `ifnot:`; chuyển `sectPr` lên thân. Chạy lại cho ra `document.xml` giống hệt (đã kiểm).
- Kiểm: `CollectiveFormsTemplateTests` (tag khớp lớp dữ liệu, render hợp lệ OpenXML), `Wave8FormsTests` (chữ in sẵn, tách mục I/II, 9 cột, số phiếu, giữ dòng biểu mẫu khi thiếu Tổ kiểm phiếu/mục trống), `OrgSettingsTemplateTests.BundledTemplates_PassValidation_WithoutWarnings` (qua danh mục).

## 5. Migration

- Xóa `20260929052857_InitialCreate*` + snapshot, sinh lại `20260929095331_InitialCreate` (`ConnectionStrings__Default` giả `design_only`, `ArtifactsPath`/`UseArtifactsOutput` ngoài repo).
- SQL tay giữ: `IX_party_member_profiles_Username_lower` trên `lower("Username")`. `report_drafts` unique `(PeriodId, PartyCellId, FormCode)` — EF tự sinh `NULLS NOT DISTINCT` (`AreNullsDistinct(false)`), không cần SQL tay. jsonb mặc định `'{}'::jsonb` của `Sections`, `Details` do EF sinh.
- Schema đợt 8: `evaluation_records.SelfAssessment`/`TaskResults`/`AxisNotes`/`CadreWorkProposal`, `collective_evaluation_records.Sections`, `evaluation_meetings.Details`, `evaluation_meeting_vote_summaries.VotesNotRated`, bảng `report_drafts`, `evaluation_appeals`, `improvement_plans`.
- `has-pending-model-changes`: **sạch** (sau mọi thay đổi).
- Áp dụng trên CSDL tạm `ctd_it_mig_w8` (159): `Applying migration '20260929095331_InitialCreate'. Done.`; `migrations list` chỉ có `InitialCreate`; `database drop --force` thành công.

## 6. Tài liệu, cấu hình

- `docs/bieu-mau.md`: bảng **toàn bộ mẫu HD03 01–20 → nơi nhập → nơi xuất (trang, endpoint) → định dạng → quyền**; tag Mẫu 13 mới; Mẫu 08 Excel; cách chạy script template; test cho mẫu có điều kiện một nhánh; `requiredForms` sửa trên trang Bộ tiêu chí.
- `docs/thiet-ke/luong-danh-gia.md`: mẫu áp dụng theo bộ tiêu chí; tham số sau công bố (3.5); bỏ phiếu kín thêm "chưa đánh giá", Tổ kiểm phiếu, số phiếu, Mẫu 13 theo biên bản (4); mục **4a Sau công bố** (công khai, kiến nghị, Mẫu 17, nhắc việc, cửa sổ cảnh báo); `cadreWorkProposal` ở API B4; câu hỏi 5 đã trả lời.
- `docs/thiet-ke/phan-quyen.md`: 4 quyền sau công bố và vai trò mặc định đã có từ task 20; bổ sung quyền xuất Mẫu 07/08/12/13 theo đối tượng, `report.export` cho 14/15A/15B/16.
- `docs/deployment.md`: `Notifications__DueSoonDays`; dữ liệu mẫu (10 tài khoản, tập thể, biên bản, Mẫu 17, kiến nghị, nháp 16); go-live: danh sách mẫu Word, gán 4 quyền mới cho vai trò cũ / `ResetRolePermissions`, phạm vi công khai; rà mẫu áp dụng, mục 09C, trục 09B, tham số sau công bố trước khi mở kỳ.
- `docs/database-migrations.md`: lần gộp gần nhất sau đợt 8.
- `appsettings.json` (`Notifications:DueSoonDays: 2`), `docker/.env.example`, `docker/docker-compose.yml` (`Notifications__DueSoonDays`).
- Không sửa `CLAUDE.md`, `FINDINGS.md`, `RULES.md`, task file.

## 7. Grep

`/forms"|href="/forms|form-15"|form-16"|/reports/form-15|/reports/form-16` trong `backend/src`, `backend/tests`, `frontend/{app,components,contexts,services}`:
mẫu này (không có ranh giới từ) khớp các endpoint hợp lệ `/reports/form-15a`, `/reports/form-15b` (4 test, `reportService.ts`). Với ranh giới
`/reports/form-15([^ab]|$)` (và mọi loại tệp, bỏ `bin/obj`): **rỗng**. Đã bỏ chú thích `"form-15"`/`"form-16"` ở `ExportReportController` và 2 kiểm tra 404 của báo cáo đã gỡ trong `CollectiveFormsReportsIntegrationTests.R5`.

## 8. Cổng kiểm tra

| Bước | Kết quả |
|---|---|
| `dotnet build backend/CongTacDang.slnx --no-incremental` (artifacts ngoài repo) | 0 lỗi, 0 warning |
| `dotnet test` — unit | 307: 306 pass, 1 skip có sẵn (`PdfConversionTests.Mau01_ConvertsToPdf_AndCleansWorkDirectory` — máy không có LibreOffice), 0 fail — cả 2 lần |
| `dotnet test` — tích hợp (159, `CONGTACDANG_TEST_PG`), lần 1 | **100 chạy, 100 pass, 0 skip**, 0 fail |
| `dotnet test` — tích hợp, lần 2 liên tiếp | **100 chạy, 100 pass, 0 skip**, 0 fail |
| `npx tsc --noEmit` | pass |
| `npm run build` | pass (3 cảnh báo ESLint có sẵn: `app/audit/page.tsx`, `DocumentViewerModal.tsx` ×2) |
| `has-pending-model-changes` | sạch |
| `docker compose -f docker/docker-compose.yml config` | **không chạy** — máy không có Docker (chỉ thêm một biến môi trường theo mẫu dòng kề) |
| CSDL | chỉ `ctd_it_*` (fixture test + `ctd_it_mig_w8` đã xóa); không đụng `congtacdang_test`; không chạy app (luồng giao diện kiểm bằng `tsc`/`build`, API bằng test tích hợp) |

Test mới/sửa: `Wave8FormsTests` (unit, 5), `Wave8IntegrationTests.W1` (cửa sổ cảnh báo: mở, khóa, đóng 10 ngày → có; đóng 200 ngày → không; đóng 200 ngày với tham số 365 → có), `EvaluationWorkflowTests.W6` (cột 13 qua B4 cấp trên và nội bộ), `CollectiveFormsReportsIntegrationTests` R1 (cột 13 Mẫu 14), R3 (Mẫu 08 Excel: bố cục, dữ liệu, tên tệp, 403, 400), R4 (Mẫu 13: tiêu đề, Tổ kiểm phiếu, số phiếu, mục I/II, ghi chú, chữ ký, tên tệp, 400 biên bản chưa có kết quả, 403 Chi bộ khác, 404 endpoint cũ; 400 số phiếu không khớp; dấu chấm câu Mẫu 12/13), `AuthorizationMatrixTests.BranchReports_ResolveScope` (Mẫu 11 thay Mẫu 13 theo kỳ), `SampleDataSeedTests`, `DataSeederAuthorizationTests`.

## 9. Quyết định chính

1. **Mẫu 13 theo từng biên bản** (không theo kỳ + tổ chức Đảng): biên bản kiểm phiếu là của một hội nghị. Xuất được từ mọi biên bản có kết quả kiểm phiếu (M13, hoặc M12 được gắn phiếu khi ghi nhận B3a/B4). Mẫu 12 vẫn chỉ xuất từ biên bản M12 (giữ quyết định task 19).
2. Mục I/II của Mẫu 13 theo `ApprovalAuthority` **ảnh chụp trên hồ sơ** (như Mẫu 14/16); thứ tự trong mục theo mã chức danh nhỏ nhất rồi họ tên. Dòng "…ĐẢNG ỦY/CHI ỦY CƠ SỞ…" giữ nguyên chữ gốc.
3. "Chưa đánh giá, xếp loại" là một cột số phiếu mới (`VotesNotRated`); tổng phiếu kiểm tra ≤ số có mặt tính cả cột này. Số phiếu phát ra/thu về/hợp lệ/không hợp lệ ghi ở cấp biên bản (một phiếu kín gồm mọi cán bộ); chưa ghi số không hợp lệ → dùng số chung của các dòng (như trước).
4. Tổ kiểm phiếu: danh sách người, người đầu là Tổ trưởng (theo thứ tự in trên biểu mẫu), không thêm trường vai trò.
5. Mẫu 08: thêm Excel (theo V.1), giữ Word/PDF; trên trang Báo cáo Mẫu 08 ghi định dạng gốc là Excel.
6. Cột 13 Mẫu 14 lưu trên hồ sơ (một giá trị), ghi lại khi quyết định lại sau mở hồ sơ; ghi nhận cấp trên chỉ nhận ở B4 (bước khác → 400).
7. Cửa sổ cảnh báo tính từ `StatusChangedAt` của kỳ đã đóng (thời điểm đóng), N lấy từ ảnh chụp bộ tiêu chí của **từng kỳ**. Chỉ ảnh hưởng nhóm việc/chuông; trang hồ sơ vẫn hiện "bắt buộc lập kế hoạch" theo mức.
8. `EvaluationPdfModal` chỉ phục vụ biểu mẫu cá nhân theo `requiredForms`; báo cáo kỳ xuất ở trang Báo cáo (không xem trong modal).
9. Script dựng template đưa vào `tools/templates/` để tái lập (báo cáo 19 đề xuất).

## 10. Còn lại / câu hỏi nghiệp vụ

1. **HD03 V.1 đọc nguyên văn**: "Tất cả các hồ sơ, biểu mẫu báo cáo nêu trên được lập trên file excel (trừ 07, 09C, 12, 13, 16)" — danh sách "nêu trên" gồm cả 09A, 09B, 09D (và 14, 15). Hiện 09A/09B/9D/10 xuất **Word/PDF** (biểu mẫu gốc cung cấp dạng bảng Word). Cần nghiệp vụ xác nhận có phải nộp 09B/9D bằng Excel không; nếu có, bổ sung bản Excel như Mẫu 08.
2. Mẫu 10 có thuộc danh sách áp dụng Q3/2026 không (đang có, theo task 18); mục 09C có bắt buộc nhập khi nộp không (đang không, bật được trên trang Bộ tiêu chí); 6.000 ký tự ≈ 02 trang A4.
3. Số liệu 15A/15B/16 khi kỳ chưa kết thúc dùng mức đề xuất gần nhất — có muốn chỉ đếm hồ sơ đã quyết định (báo cáo 19).
4. Hạn gửi/trả lời kiến nghị (HD03 không quy định); ai xử lý kiến nghị, ai lập/duyệt kế hoạch (mặc định Văn phòng Đảng ủy / Lãnh đạo Phòng + Cấp trực tiếp sử dụng); công khai điểm (mặc định không); mức bắt buộc Mẫu 17 (mặc định C, D); cửa sổ cảnh báo 90 ngày.
5. Chưa làm (ngoài phạm vi tích hợp): trường **ngày sinh** cán bộ cho 09A/09B/09C; "Căn cứ Hướng dẫn số …-HD/TVĐU ngày …" trên 07/12/13/16 để điền tự động (đề xuất thêm vào Thông tin đơn vị); cache ngắn hạn cho chuông nhắc việc khi số hồ sơ lớn; Mẫu 03–06, 18–20 chưa số hóa.
6. Chưa thử tay giao diện trên trình duyệt (trang Bộ tiêu chí mới, MeetingEditor, nút Mẫu 13/08 Excel, modal PDF); chưa chạy `docker compose config` (máy không có Docker); PDF chưa kiểm được (máy không có LibreOffice — endpoint trả 503 như thiết kế).
