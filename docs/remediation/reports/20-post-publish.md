# Báo cáo: 20 — Sau công bố: công khai kết quả, kiến nghị, kế hoạch 30-60-90 (Mẫu 17), nhắc việc

- **Branch:** `feat/post-publish` (từ `main` @ `46645e5`)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `038944b`)
- **Ngày:** 2026-09-29

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-86 | done | Quyền mới `evaluation.results.view`; API `GET /api/results` (lọc kỳ, đơn vị gồm đơn vị con, mức) chỉ trả họ tên, chức danh, đơn vị, mức chính thức, điểm khi bộ tiêu chí bật `publishScores`, ghi chú "đang xem xét kiến nghị"; phạm vi = phạm vi bản gán + kết quả của chính mình; trang `/results`, mục menu "Kết quả đánh giá" | `Application/Services/PublishedResultService.cs`, `Infrastructure/Repositories/PostPublishRepository.cs`, `Api/Controllers/PostPublishController.cs`, `Domain/Evaluation/CriteriaSetContent.cs` (tham số), `frontend/app/results/page.tsx` |
| T-87 | done | Entity `EvaluationAppeal` (`Submitted → UnderReview → Accepted/Rejected`, xmin); chủ hồ sơ gửi sau công bố (quyền mới `evaluation.appeal.submit`, chỉ hồ sơ của mình, không trùng khi đang xử lý), tệp gắn đối tượng `EvaluationAppeal`; xử lý bằng quyền mới `evaluation.appeal.resolve` (phạm vi), trả lời bắt buộc căn cứ; chặn xung đột lợi ích; nhãn "Đang xem xét kiến nghị"; chấp nhận → nút "Mở lại hồ sơ theo kiến nghị" gọi `Reopen` hiện có, lý do trong lịch sử dẫn chiếu kiến nghị; nhóm work-queue "Kiến nghị chờ xử lý"; khối `AppealPanel` | `Domain/Entities/EvaluationAppeal.cs`, `Application/Services/AppealService.cs`, `Data/Configurations/PostPublishConfigurations.cs`, `frontend/components/evaluations/AppealPanel.tsx` |
| T-88 | done | Entity `ImprovementPlan` (một kế hoạch/hồ sơ, nội dung jsonb theo mã mục Mẫu 17, `Draft → Approved → Acknowledged → Closed`); quyền mới `evaluation.improvement.manage` (lập, sửa khi đang lập, duyệt, ghi kết quả mốc; không trên hồ sơ của mình); chủ hồ sơ xác nhận cam kết; bắt buộc theo tham số `improvementPlanRequiredGrades` của bộ tiêu chí (mặc định Mức C, D); cảnh báo trên work-queue "Kế hoạch 30-60-90 ngày cần lập / duyệt" và "chờ bạn xác nhận"; xuất Word/PDF Mẫu 17 từ file mẫu dựng từ biểu mẫu gốc (tag `ORG_*`), `MAU_17` trong danh mục file mẫu Word | `Domain/Entities/ImprovementPlan.cs`, `Domain/Evaluation/ImprovementPlanContent.cs`, `Application/Services/ImprovementPlanService.cs`, `Infrastructure/Documents/Forms/Mau17Data.cs`, `Templates/Word/Mau_17_KeHoachHoTro.docx`, `Infrastructure/Services/ImprovementPlanDocumentService.cs`, `frontend/components/evaluations/ImprovementPlanPanel.tsx` |
| T-89 | done | `GET /api/notifications/summary` tính khi tải từ cùng nguồn với work-queue: tổng việc, bước quá hạn, sắp tới hạn (≤ `Notifications:DueSoonDays` ngày, mặc định 2, theo hạn bước trong hồ sơ luồng), kiến nghị chờ xử lý, kế hoạch cần lập/duyệt, kế hoạch chờ xác nhận, danh sách mục có liên kết; chuông ở header (tải lại khi chuyển trang) | `Application/Services/NotificationService.cs`, `Application/Services/PostPublishWorkQueue.cs`, `frontend/components/layout/NotificationBell.tsx` |

### Mẫu 17 — theo biểu mẫu gốc
Đọc XML `docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx`, phần "Mẫu số 17" (đến trước "Mẫu số 18"):
tiêu đề trái "CƠ QUAN QUẢN LÝ CẤP TRÊN / TÊN CƠ QUAN / ĐƠN VỊ", quốc hiệu, "……, ngày …… tháng …… năm 202…", tên mẫu, dòng
"(Bắt buộc áp dụng đối với cán bộ xếp loại Hoàn thành nhiệm vụ - Mức C hoặc Không hoàn thành nhiệm vụ - Mức D)", Họ và tên cán bộ,
Chức vụ, Đơn vị công tác, Mức xếp loại quý vừa qua, Người trực tiếp hỗ trợ, giám sát + Chức vụ; bảng 6 cột (Giai đoạn khắc phục,
Hạn chế cần khắc phục, Mục tiêu/sản phẩm, Biện pháp & đào tạo hỗ trợ, Phối hợp/giám sát, Kết quả sau mỗi mốc) × 3 mốc
(30 ngày — Khắc phục cấp bách; 60 ngày — Cải thiện hiệu suất; 90 ngày — Đánh giá chuyển biến), kết quả "☐ Đạt yêu cầu ☐ Chưa chuyển
biến" (mốc 90: "☐ Đạt (Đóng kế hoạch) ☐ Không đạt (Xem xét nhân sự)"), ba chữ ký (Thủ trưởng đơn vị, Người trực tiếp hỗ trợ theo dõi,
Cá nhân cam kết khắc phục), lưu ý chung.

- Nội dung lưu jsonb theo mã mục: `supporterName`, `supporterTitle`, `stages.M30|M60|M90.{limitation, target, measures, coordination,
  result (Achieved|NotAchieved), resultNote, resultRecordedByName, resultRecordedAt}`. Họ tên/chức vụ/đơn vị/mức lấy từ hồ sơ.
- File mẫu `Mau_17_KeHoachHoTro.docx` được **dựng một lần từ chính file biểu mẫu gốc** (công cụ OpenXML tạm ngoài repo): giữ nguyên
  phần Mẫu 17 (bố cục, định dạng, chữ in sẵn, section), bỏ các mẫu khác và phần phụ không dùng; chữ "Ví dụ: …" trong ô điền được thay
  bằng Content Control (chữ mặc định là dòng chấm); ô ☐ in sẵn được bọc tag → "☒" khi chọn. Thêm dòng họ tên dưới ba chữ ký.
- "Mức xếp loại quý vừa qua" in `<tên mức> - Mức A|B|C|D` (A = Hoàn thành xuất sắc, B = Hoàn thành tốt suy ra từ chữ in "Mức C", "Mức D").
- Test: tag template khớp lớp dữ liệu (không thiếu/thừa), qua bộ kiểm tra tải lên không lỗi/không cảnh báo, giữ đủ chữ in sẵn, không còn
  mẫu khác/"Ví dụ:", render hợp lệ OpenXML, đúng 2 ô ☒ (`PostPublishUnitTests`); `OrgSettingsTemplateTests` tự kiểm `MAU_17` qua
  `WordFormCatalog`.

### Lựa chọn mặc định — CHỜ NGHIỆP VỤ XÁC NHẬN (đều là cấu hình, không cứng trong code)

| Lựa chọn | Mặc định | Nơi cấu hình |
|---|---|---|
| Phạm vi công khai kết quả | Theo phạm vi bản gán `evaluation.results.view`; seed gán cho vai trò "Người được đánh giá", dữ liệu mẫu gán **Toàn công ty**. Chủ hồ sơ luôn thấy kết quả của mình | Bản gán vai trò (Quản trị → Vai trò / gán) |
| Công khai điểm | **Không** — chỉ công khai mức (`publishScores = false`) | Tham số bộ tiêu chí (trang Bộ tiêu chí → Tham số); kỳ dùng ảnh chụp khi mở kỳ |
| Mức bắt buộc lập kế hoạch 30-60-90 | `HoanThanh` (Mức C), `KhongHoanThanh` (Mức D) — theo chữ in trên Mẫu 17 | Tham số bộ tiêu chí `improvementPlanRequiredGrades` (có ô chọn trên trang Bộ tiêu chí). Mức khác vẫn lập được, không bắt buộc |
| Ai xử lý kiến nghị | `evaluation.appeal.resolve` seed cho "Văn phòng Đảng ủy" (Global) — HD03: "Cấp đã quyết định xếp loại xem xét kiến nghị" | Vai trò |
| Ai lập/duyệt kế hoạch | `evaluation.improvement.manage` seed cho "Lãnh đạo Phòng" (phạm vi Phòng) và "Cấp trực tiếp sử dụng" (Global) — "thủ trưởng đơn vị" | Vai trò |
| Gửi kiến nghị | Quyền mới `evaluation.appeal.submit` (seed "Người được đánh giá"), chỉ hồ sơ của mình, chỉ sau công bố, không giới hạn số lần nhưng không trùng khi đang xử lý; không có hạn nộp/trả lời (HD03 không quy định) | Vai trò |
| Xung đột lợi ích khi xử lý kiến nghị | Chặn: người gửi/chủ hồ sơ; người đã thực hiện bước mà chủ hồ sơ chọn là "liên quan tới" (duyệt danh mục, Chi bộ xác nhận, ghi nhận tập thể, thẩm định, cấp trực tiếp sử dụng, ghi nhận quyết định) — chụp khi gửi. Không chọn bước → chỉ chặn chủ hồ sơ | Luật trong `AppealService` (theo PL II III.2) |
| Xem kiến nghị / kế hoạch | Chủ hồ sơ, người có `evaluation.read` hoặc quyền xử lý/lập kế hoạch trong phạm vi (hồ sơ kiến nghị, kế hoạch là thành phần hồ sơ — PL II mục V; Mẫu 18 "Đơn vị, Cá nhân") | — |
| Sắp tới hạn | ≤ 2 ngày | `Notifications:DueSoonDays` |

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx --artifacts-path <ngoài repo>` | pass | 0 warning, 0 error (`--no-incremental`) |
| `dotnet test` — unit | pass | 259 passed, 1 skipped (`PdfConversionTests.Mau01_ConvertsToPdf…` — cần LibreOffice, skip sẵn có, không thuộc task) |
| `dotnet test` — integration (`CONGTACDANG_TEST_PG`, CSDL `ctd_it_*`) | pass | **92 chạy, 0 skip**, 0 fail (có migration tạm — xem "Thay đổi schema") |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | 3 warning ESLint có sẵn ở `app/audit/page.tsx`, `DocumentViewerModal.tsx` (không thuộc task) |

### Luồng kiểm thử (task file mục Test)

| Luồng | Trạng thái | Test |
|---|---|---|
| Công khai chỉ hiện mức, không lộ chi tiết (ý kiến, điểm tự chấm, minh chứng), theo phạm vi (Phòng / Toàn công ty), lọc mức/đơn vị, 403 khi không có quyền, bật `publishScores` → có điểm | pass | `PostPublishIntegrationTests.P1_Results_OnlyGradeWithinAssignmentScope_NoRecordDetails` |
| Kiến nghị: gửi (409 chưa công bố, 403 người khác, 400 thiếu nội dung, 409 trùng) → tệp gắn `EvaluationAppeal` → người có quyền khác chủ hồ sơ nhận xem xét → trả lời bắt buộc căn cứ (400) → nhãn "đang xem xét" bật/tắt (hồ sơ + danh sách kết quả); người có xung đột lợi ích (đã thẩm định) bị chặn 403 và không thấy trong hàng đợi; chấp nhận → mở lại qua `Reopen` hiện có, lịch sử dẫn chiếu kiến nghị | pass | `P2_Appeal_SubmitReviewResolveWithBasis_ConflictBlocked_UnderReviewLabel_ReopenLinked` |
| Kế hoạch: hồ sơ mức C bắt buộc (mức B không), cảnh báo trên work-queue theo phạm vi, 403 ngoài phạm vi / tự lập cho mình, lập → duyệt bị chặn khi thiếu mục → sửa → duyệt → chủ hồ sơ xác nhận → ghi kết quả mốc → mốc 90 đóng kế hoạch → xuất Mẫu 17 (Word có dữ liệu, ☒), 403 người không xem được hồ sơ | pass | `P3_ImprovementPlan_RequiredForGradeC_CreateApproveAcknowledgeResults_ExportMau17` |
| Chuông trả số đúng: 401 chưa đăng nhập, tổng = work-queue, mục có liên kết hồ sơ; người không có việc = 0; đếm kiến nghị/kế hoạch/chờ xác nhận (trong P2, P3); quá hạn/sắp tới hạn | pass | `P4_NotificationSummary_RequiresLogin_CountsWorkQueue`; unit `PostPublishUnitTests.NotificationSummary_CountsOverdueDueSoonAndPostPublishGroups` |
| Quyền mới và guard (gửi kiến nghị chỉ hồ sơ của mình; xử lý kiến nghị/lập kế hoạch là xung đột lợi ích trên hồ sơ của mình; phạm vi kết quả theo bản gán), tham số bộ tiêu chí (mặc định, JSON dạng mã, kiểm tra), nội dung Mẫu 17, file mẫu Mẫu 17 | pass | `PostPublishUnitTests` (13 test) |

## Thay đổi schema (cần migration)
- Bảng mới `evaluation_appeals`: `Id`, `xmin`, `RecordId` (FK `evaluation_records`, cascade), `SubmittedById` (tham chiếu mềm, index), `SubmittedByName` (200),
  `SubmittedAt`, `Content` (4000), `ConcernedSteps` `text[]`, `ConflictedUserIds` `uuid[]`, `Status` int, `ReviewStartedById/Name/At`,
  `ResolvedById/Name/At`, `Response` (4000), `ReopenedAt`, `ReopenedById`, audit (`CreatedAt/By`, `UpdatedAt/By`); index `(RecordId, Status)`, `Status`.
- Bảng mới `improvement_plans`: `Id`, `xmin`, `RecordId` (FK, cascade, **unique**), `Content` jsonb, `StartDate` date, `Status` int,
  `PreparedById/Name`, `ApprovedById/Name/At`, `AcknowledgedAt`, `AcknowledgementComment` (2000), `ClosedAt`, audit; index `Status`.
- Cột jsonb có sẵn `criteria_sets.Content` / `evaluation_periods.CriteriaSnapshot`: thêm khóa `parameters.publishScores`,
  `parameters.improvementPlanRequiredGrades` (không cần migration; JSON cũ đọc ra mặc định).
- **Không commit migration.** Cách test: chép `backend/` (trừ `bin/obj`) ra thư mục tạm ngoài repo, build, chạy
  `dotnet ef migrations add TmpPostPublishL20 -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api -o Data/Migrations`
  ở bản chép, chép tạm 2 file migration + snapshot vào worktree, chạy test tích hợp, rồi chuyển 2 file migration ra ngoài repo và
  `git checkout` snapshot. Migration sinh ra chỉ gồm 2 bảng trên (khớp model, không đụng bảng cũ).

## Key config mới
| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Notifications:DueSoonDays` | `2` | Bước còn từ 0 tới số ngày này tới hạn → "sắp tới hạn" trên chuông |

## Thay đổi hành vi API / breaking change
- Mới: `GET /api/results` (`evaluation.results.view`); `GET|POST /api/evaluations/records/{id}/appeals`; `POST /api/appeals/{id}/start-review|resolve`
  (`evaluation.appeal.resolve`), `POST /api/appeals/{id}/reopen` (`evaluation.reopen`); `GET|POST /api/evaluations/records/{id}/improvement-plan`,
  `PUT /api/improvement-plans/{id}`, `POST …/approve|milestone-result` (`evaluation.improvement.manage`), `POST …/acknowledge` (`evaluation.self`),
  `GET /api/improvement-plans/{id}/mau-17[?format=pdf]`; `GET /api/notifications/summary`.
- `GET /api/evaluations/work-queue`: thêm nhóm `APPEALS`, `IMPROVEMENT_PLANS`, `IMPROVEMENT_PLAN_ACK` (cả với kỳ đã đóng; `workflowProfileName` rỗng,
  `statusDisplayName` mô tả việc); `total` gồm các nhóm này.
- Tệp đính kèm: `ownerType` nhận thêm `EvaluationAppeal` (chỉ chủ hồ sơ tải lên; xem = xem hồ sơ).
- 4 mã quyền mới trong danh mục quyền (thêm vào `AuthzContractTests` — kỳ vọng cũ sửa theo yêu cầu task); `ConflictOfInterestCodes` thêm
  `evaluation.appeal.resolve`, `evaluation.improvement.manage` (sửa kỳ vọng `AuthorizationGuardTests` 10 → 12); quyền vai trò mặc định đổi
  (sửa kỳ vọng `GoLiveScenarioTests`).
- CSDL đã có vai trò: seeder **không** tự thêm quyền mới vào vai trò cũ — quản trị gán trên giao diện hoặc chạy một lần với
  `Database:ResetRolePermissions=true`.

## Cần phối hợp
- **Migration:** người điều phối sinh migration sau khi merge (2 bảng mới ở trên).
- **`WordFormCatalog.cs`, `docs/bieu-mau.md`:** task 18/19 có thể cũng thêm dòng biểu mẫu → xung đột merge dạng thêm dòng liền nhau, giữ cả hai.
- **`EvaluationWorkflowService.cs`** (chủ: luồng đánh giá): chỉ thêm tham số constructor tùy chọn `IPostPublishWorkQueue?` và tách phần
  thân `GetWorkQueueAsync` thành `GetStepQueueAsync` để nối nhóm sau công bố — không đổi logic hàng đợi cũ.
- **Tệp của kiến nghị** chạm `AttachmentOwnerTypes.cs` (thêm hằng + `All`) và `AttachmentRepository.GetOwnerRecordAsync` (thêm một nhánh) —
  cơ chế quyền tệp giữ nguyên (xem = `evaluation.read` hồ sơ, tải lên = `evaluation.self`).
- `AppHeader.tsx`, `AppSidebar.tsx`, `app/evaluations/[recordId]/page.tsx`, `app/work-queue/page.tsx`: chỉ chèn tối thiểu (chuông + tiêu đề trang
  `/results`; một mục menu; render 2 panel; một dòng hiển thị `statusDisplayName` cho nhóm sau công bố). `app/criteria/[id]/page.tsx`,
  `services/criteriaService.ts`: thêm 2 tham số (ô chọn) — không đổi phần khác.
- `docs/thiet-ke/luong-danh-gia.md` câu hỏi mở số 5 ("Kiến nghị sau công bố: chỉ cần Reopen hay luồng riêng?") nay đã có luồng riêng + Reopen —
  người điều phối cập nhật khi nghiệp vụ xác nhận.

## Phát hiện thêm
- Không chạy app/giao diện thật (chỉ API qua test tích hợp, `tsc`, `next build`); nên thử tay 3 màn hình (Kết quả, khối Kiến nghị/Kế hoạch,
  chuông) sau khi merge.
- Nhóm "Kế hoạch cần lập" quét mọi hồ sơ đã công bố mức C/D chưa có kế hoạch ở mọi kỳ: triển khai trên CSDL có kỳ cũ (trước khi có Mẫu 17)
  sẽ hiện cả hồ sơ cũ — cần nghiệp vụ quyết định có giới hạn theo kỳ không (đề xuất mã mới, 🟡).
- Chuông gọi tổng hợp hàng đợi mỗi lần chuyển trang (tính lại quyền trên từng hồ sơ) — đủ với quy mô hiện tại; nếu số hồ sơ lớn nên cache ngắn
  hạn phía máy chủ (⚪).
- Sidebar vẫn còn mục "Biểu mẫu" (`/forms`) trỏ trang đã bỏ ở `4ec847c` (ngoài phạm vi, ⚪).
