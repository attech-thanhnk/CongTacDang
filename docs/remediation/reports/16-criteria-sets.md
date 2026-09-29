# Báo cáo: Task 16 — Bộ tiêu chí và thang điểm theo phiên bản

- **Branch:** `feat/criteria-sets` (fast-forward tới `d65d4df` trước khi sửa file đầu tiên)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `de3b6c5`)
- **Ngày:** 2026-09-29

> Mọi nội dung chấm điểm mặc định lấy theo **bản trích xuất HD 03-HD/TVĐU (`docs/nghiep-vu/hd03-trich-xuat.md`) — CHƯA XÁC NHẬN**.
> Không tự đặt luật ngoài văn bản; chỗ văn bản mâu thuẫn đã chọn một phương án (bảng "Lựa chọn khi văn bản mâu thuẫn"), đều sửa được
> trong bộ tiêu chí mà không sửa code.

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-77 | done | Entity `CriteriaSet` (`Code` duy nhất, `Name`, `Status` Draft/Published/Archived, `SelfScoreForm` 09A/09B, `Notes`, `Content` jsonb có `schemaVersion`, `SourceSetId`, thời điểm xuất bản/lưu trữ, audit, xmin). Nội dung `CriteriaSetContent`: nhóm tiêu chí chung + tiêu chí con (`Binary`/`Range`), K/AD có lý do + quy tắc `ExcludeAndRescale`/`GrantFull`, trục (điểm tối đa cho 09B), khung tỷ trọng A-B-C-D, thang quy đổi %, 4 mức xếp loại (ngưỡng + % vượt chuẩn tối thiểu + điều kiện hiển thị), tham số (số sản phẩm, tổng trọng số, sai số, điểm tối đa chung, khung mặc định, mức giảm phải nêu căn cứ, ngưỡng giải trình + giải trình khi đổi mức, trần xuất sắc: tỷ lệ/mẫu số/làm tròn, làm tròn từng loại điểm: số chữ số + `HalfUp`/`HalfEven`/`Truncate`, điểm tối đa hồ sơ tập thể). `Validate(form)` đầy đủ (mã, trùng mã, tổng tiêu chí con = điểm tối đa chung, tổng trục = 70 với 09B, khung tổng = 100%, thang có mức 0%, 4 mức giảm dần + KHT = 0, tham số). Bộ Published bất biến (sửa → 409, nhân bản thành nháp); xóa được nháp; không xóa bộ đã xuất bản. Seed 2 bộ mặc định (Published) khi CSDL chưa có bộ nào. Quyền `criteria.manage` (phân hệ `criteria`) gán vai trò mặc định Cơ quan thẩm định. API `api/criteria-sets`. | `Domain/Evaluation/CriteriaSetContent.cs`, `CriteriaSetDefaults.cs`, `Domain/Entities/CriteriaSet.cs`, `Domain/Enums/DomainEnums.cs`, `Application/Services/CriteriaSetService.cs`, `DTOs/CriteriaSetDtos.cs`, `Common/Interfaces/ICriteriaSetRepository.cs`, `Common/Security/PermissionCodes.cs`, `Infrastructure/Repositories/CriteriaSetRepository.cs`, `Data/Configurations/CriteriaSetConfiguration.cs`, `Data/DataSeeder.cs`, `Api/Controllers/CriteriaSetController.cs`, `Api/Extensions/EvaluationExtensions.cs` |
| T-78 | done | Kỳ chọn bộ **đã xuất bản** khi còn Dự thảo (`criteriaSetId` khi tạo/sửa kỳ; tạo kỳ không chỉ định → bộ đã xuất bản mới nhất có mẫu gợi ý của kiểu kỳ: `full` → 09A, `q3-2026-transition` → 09B); **chụp nguyên bộ** vào `EvaluationPeriod.CriteriaSnapshot` khi chọn và chụp lại khi mở kỳ; kỳ đã mở không đổi được bộ (409); mở kỳ cần bộ còn Published và hợp lệ. Gỡ `parameters` **và** `selfScoreForm` khỏi `PeriodSettings` (mẫu tự chấm thuộc bộ); kiểm tra "09A cần bước đăng ký sản phẩm" chuyển thành `Validate(..., selfScoreForm)`. `EvaluationRecord`: `GeneralScores` jsonb `{ mã: { score, notApplicable, reason } }`, `AxisScores` jsonb `{ mã trục: điểm }`, `WeightFrameCode` (ảnh chụp từ khung mặc định của cán bộ, cán bộ chưa có → khung mặc định của bộ), `AppraisalExplanation`. `EvaluationTask.AxisCode` (phải thuộc bộ của kỳ). `PartyMemberProfile.WeightFrameCode`. Gỡ `GeneralScoreT1..6`, `AxisScoreT1..6`, enum `JobGroup`, `TaskResultAxis`, `EvaluationParameters`. `EvaluationScoring` tính theo bộ của kỳ — **cấu trúc công thức A/B/C/D giữ nguyên** (có test so với code cũ). Readiness báo lỗi khi khung của hồ sơ không có trong bộ 09A; sửa ảnh chụp chỉ nhận khung có trong bộ. Giải trình chênh lệch (B-09): thẩm định có `|tự chấm − thẩm định| ≥ ngưỡng` (hoặc làm đổi mức theo ngưỡng điểm, nếu bộ bật) mà thiếu giải trình → 400. Trần xuất sắc (`branch-quotas`, Mẫu 15/15A/15B/16) theo tham số của bộ (mẫu số, làm tròn). Mẫu 01 ghi mã trục; Mẫu 02 ghi điểm theo số chữ số của bộ. Tài khoản cán bộ + import cán bộ có mã khung. | `Domain/Evaluation/EvaluationScoring.cs`, `PeriodSettings.cs`, `Domain/Entities/EvaluationPeriod.cs`, `EvaluationRecord.cs`, `EvaluationTask.cs`, `PartyMemberProfile.cs`, `Application/Services/EvaluationWorkflowService.cs`, `PeriodService.cs`, `EvaluationMapping.cs`, `EvaluationService.cs` (trần), `CollectiveEvaluationService.cs` (điểm tối đa tập thể), `UserAccountService.cs`, `Imports/Definitions/UserImportDefinition.cs`, `DTOs/EvaluationDtos.cs`, `Infrastructure/Services/ReportService.cs` (chỉ 2 dòng trần + hàm lấy quy tắc), `Documents/Forms/Mau01Data.cs`, `Mau02Data.cs`, `Data/Configurations/EvaluationConfigurations.cs` |
| T-79 | partial | `/criteria`: danh sách, tạo bản nháp (nội dung khởi tạo theo bản trích xuất), nhân bản, lưu trữ, xóa nháp. `/criteria/[id]`: xem (chỉ đọc) hoặc sửa bản nháp bằng bảng (nhóm/tiêu chí con, trục, khung %, khung mặc định, thang quy đổi, mức xếp loại + điều kiện, tham số, làm tròn từng loại điểm), kiểm tra tổng tức thì (chung = 30, tổng trục = 70 nếu 09B, khung = 100%) + lỗi kiểm tra đầy đủ của máy chủ sau khi lưu, xuất bản. Trang kỳ: chọn bộ khi Dự thảo, xem ảnh chụp; khung tỷ trọng của từng người (cột + sửa ảnh chụp theo danh sách khung của bộ); bỏ ô tham số/mẫu tự chấm cũ; trang danh sách kỳ có cột bộ tiêu chí và chọn bộ khi tạo kỳ. Form tự chấm dựng động (`SelfScoreForm`): "Đảm bảo / Không đảm bảo / K/AD (lý do)" hoặc ô điểm theo cách chấm của nhóm, lý do bắt buộc hiện khi cần, trục theo mã (09B), A-B-C-D (%) theo khung của hồ sơ với điểm từng sản phẩm, tổng tạm tính và mức theo ngưỡng. Đăng ký nhiệm vụ chọn trục; thẩm định hiện chênh lệch và bắt buộc giải trình. **Phần còn lại:** "Hồ sơ cán bộ: chọn khung tỷ trọng mặc định" — trang hồ sơ cán bộ nằm ở `app/admin/users/**` (vùng task 17, không được sửa); đã có API (`weightFrameCode` khi tạo/sửa tài khoản, `GET /api/criteria-sets/weight-frames` = danh sách khung của bộ đang dùng gần nhất) và cột import — xem "Cần phối hợp". | `frontend/app/criteria/**`, `app/periods/**`, `components/evaluations/{SelfScoreForm,CriteriaSummary,RecordActionPanel}.tsx`, `useCriteria.ts`, `services/criteriaService.ts`, `services/evaluationService.ts` |

## Mô hình JSON bộ tiêu chí (`criteria_sets.Content`, schemaVersion 1)

```jsonc
{
  "schemaVersion": 1,
  "generalGroups": [ { "code": "1", "name": "…", "scoringMode": "Binary|Range",
                       "items": [ { "code": "1.1", "text": "…", "maxScore": 2 } ] } ],
  "axes": [ { "code": "T1", "name": "…", "description": "…", "maxScore": 15 } ],          // maxScore dùng cho 09B
  "weightFrames": [ { "code": "K2", "name": "…", "a": 0.15, "b": 0.5, "c": 0.15, "d": 0.2 } ],
  "conversionScale": [ { "minPercent": 95, "label": "95 - 100%", "description": "…" } ],  // mức cao nhất có minPercent ≤ giá trị
  "grades": [ { "grade": "HoanThanhXuatSac", "minScore": 90, "minExceedStandardRatio": 0.3, "conditions": "…" } ],
  "parameters": {
    "minTasks": 3, "maxTasks": 7, "totalTaskWeight": 70, "taskWeightTolerance": 0.05, "generalMaxScore": 30,
    "defaultWeightFrameCode": "K2", "allowNotApplicable": true, "notApplicableRule": "ExcludeAndRescale|GrantFull",
    "deductionReasonMinPoints": 1, "explanationThreshold": 5, "explanationOnGradeChange": true,
    "excellentQuota": { "ratio": 0.2, "denominator": "GoodOnly|GoodOrBetter", "rounding": "HalfUp|HalfEven|Truncate" },
    "rounding": { "taskScore": { "decimals": 1, "mode": "HalfUp" }, "tasksTotal": {…}, "generalTotal": {…}, "total": {…} },
    "collectiveGeneralMaxScore": 30, "collectiveTaskMaxScore": 70
  }
}
```

Ảnh chụp trong kỳ (`evaluation_periods.CriteriaSnapshot`): `{ setId, code, name, selfScoreForm, takenAt, content }`.
Điểm trên hồ sơ: `GeneralScores = { "1.1": { "score": 0, "notApplicable": true, "reason": "…" }, … }`, `AxisScores = { "T1": 13, … }`.

Công thức (cố định trong code, `EvaluationScoring`):
- Tiêu chí chung = Σ điểm tiêu chí con; có K/AD: `ExcludeAndRescale` → điểm đạt / Σ tối đa tiêu chí áp dụng × điểm tối đa nhóm; `GrantFull` → tính tối đa. Làm tròn "tổng tiêu chí chung".
- Nhiệm vụ (09A): tỷ lệ A..D giới hạn 0..1; kết quả = A×tA + B×tB + C×tC + D×tD (tỷ trọng khung của hồ sơ); điểm = làm tròn "điểm sản phẩm"(trọng số × kết quả); tổng = làm tròn "tổng nhiệm vụ"(Σ điểm sản phẩm đã làm tròn) — **đúng cấu trúc cũ**.
- 09B: tổng nhiệm vụ = làm tròn "tổng nhiệm vụ"(Σ điểm trục). Tổng = làm tròn "tổng điểm"(chung + nhiệm vụ).
- Mức gợi ý (khi cá nhân không chọn): xét từ cao xuống, mức đầu tiên đạt ngưỡng điểm và (nếu có dữ liệu nhiệm vụ) % vượt chuẩn tối thiểu. 09B không có dữ liệu nhiệm vụ nên chỉ xét ngưỡng điểm.
- Làm tròn: `HalfUp`/`Truncate` tính trên `decimal` (9,55 → 9,6 như văn bản); `HalfEven` giữ đúng `Math.Round(double)` cũ để cấu hình "2 chữ số, nửa về chẵn" trùng từng bit với code trước task.

## Hai bộ mặc định (theo bản trích xuất HD03 — chờ nghiệp vụ xác nhận)

| Nội dung | "Mẫu 09B — Quý III/2026" (`HD03-09B-Q3-2026`) | "Mẫu 09A — từ 2027" (`HD03-09A-2027`) | Nguồn trích xuất |
|---|---|---|---|
| Tiêu chí chung | 3 nhóm 18/4/8 = 30; 17 tiêu chí con 1.1–1.9 (2 đ), 2.1–2.4 (1 đ), 3.1–3.4 (2 đ); chấm "Đảm bảo/Không đảm bảo" | như 09B | mục 3.2 |
| K/AD | cho phép, có lý do; `ExcludeAndRescale`; khoản giảm ≥ 1 điểm phải nêu căn cứ | như 09B | mục 2.2 (tr.9), 3.2 (tr.10) |
| Trục | T1–T6 (tên, nội dung tại VATM); điểm tối đa 15/10/10/15/10/10 = 70 | T1–T6, không có điểm tối đa riêng (chấm theo nhiệm vụ) | mục 3.3, 8.3 |
| Khung tỷ trọng | K1 25/35/20/20 · K2 15/50/15/20 · K3 20/30/35/15 · K4 15/30/20/35; mặc định K2 | như 09B | mục 3.6 |
| Thang quy đổi | 95–100 · 90–<95 · 80–<90 · 65–<80 · 50–<65 · 0–<50 (cách hiểu nguyên văn) | như 09B | mục 3.7 (bảng III.3.đ) |
| Mức xếp loại | XS ≥ 90 (+ ≥ 30% vượt chuẩn, điều kiện (i)–(v) hiển thị); Tốt ≥ 70; HT ≥ 50; KHT còn lại | như 09B | mục 4.1 |
| Sản phẩm | 3–7, tổng 70 ± 0,05 | như 09B | mục 3.5 |
| Giải trình | chênh ≥ 5 điểm hoặc đổi mức | như 09B | mục 9 TC-5 (PL II tr.31, Mẫu 10) |
| Trần xuất sắc | 20% số "Hoàn thành tốt", làm tròn 0,5 lên 1 | như 09B | mục 4.3 |
| Làm tròn | 1 chữ số, nửa lên, cho điểm sản phẩm / tổng nhiệm vụ / tổng chung / tổng | như 09B | mục 3.10 |
| Hồ sơ tập thể | 30 / 70 | như 09B | mục 5.1 |

### Lựa chọn khi văn bản mâu thuẫn (trích xuất mục 10)

| # | Mâu thuẫn | Chọn | Lý do |
|---|---|---|---|
| 1 | Làm tròn 1 chữ số vs 0,5 điểm | 1 chữ số, nửa lên, cộng các điểm sản phẩm đã làm tròn | Khớp ví dụ PL II (29,25→29,3; tổng 97,6). Bước 0,5 **chưa hỗ trợ** (chỉ số chữ số + kiểu) — xem Phát hiện thêm |
| 2 | Điều kiện HTT "Mức 2 từ 20% trở lên" | Ghi điều kiện "dưới 20%" kèm chú thích | Nguyên văn trái logic với mức HTNV; phần mềm không tự kiểm điều kiện Mức 1/2/3 (chỉ hiển thị) |
| 3 | Mẫu số trần XS | "Hoàn thành tốt" (câu chữ III.6) | Tham số `denominator` đổi được sang "tốt trở lên" (nhãn Mẫu 15A/15B/16) |
| 4 | Tiêu chí chung "có/không" vs giảm theo mức độ | "Đảm bảo/Không đảm bảo" (Mẫu 09A/09B) | Biểu mẫu áp dụng cho Q3/2026; `scoringMode = Range` chọn được theo nhóm |
| 5 | Thang A-B-C-D liên tục vs rời rạc | Liên tục (bảng III.3.đ) | PL II tr.36 "dải điểm liên tục"; thang chỉ để mô tả, không đổi công thức |
| 9 | Sàn B ±10 vs ≥ 35%, Gate KPI | Không cài | Chỉ có trong ví dụ, chưa rõ tính quy phạm |
| 33 | Trục K/AD ở 09B | Không cài (trục chỉ nhận điểm 0..tối đa) | Văn bản không có quy tắc phân bổ lại |
| — | K/AD tiêu chí chung | `ExcludeAndRescale` | HD03 tr.9: "xử lý trọng số…; không mặc nhiên chấm điểm tối đa hoặc bằng không" |
| — | Khung mặc định khi cán bộ chưa có | K2 | Văn bản không quy định; giữ mặc định của code trước task (Khung 2) |

## TC-1 / TC-2 với bộ mặc định (so với văn bản)

| Case | Kết quả phần mềm | Văn bản | Test |
|---|---|---|---|
| TC-1 (PL II mục VI, Khung 2) | Kết quả SP 97,5 / 94,0 / 100 / 95,5 / 91,5 %; điểm 29,3 / 14,1 / 10,0 / 9,6 / 4,6; tổng nhiệm vụ **67,6**; tổng **97,6**; vượt chuẩn 2/5 = 40% → **HTXS** | 67,6; 97,6; HTXS | `CriteriaSetUnitTests.TC1_DefaultSet_MatchesHd03Example` — **trùng văn bản**. (Trước task: 67,47 / 97,47 do làm tròn 2 chữ số — lệch do tham số làm tròn, nay là tham số của bộ) |
| TC-2 (A=B=C=D=x) | kết quả SP = x với cả 4 khung và khung 25% | = x | `TC2_EqualCriteria_GiveWeightTimesRatio_ForEveryFrame` |
| TC-3 (trần) | 7→1, 8→2, 13→3, 12→2; 25% với 12 → 3 | như văn bản | `ExcellentQuota_Tc3_DefaultRule` |
| TC-5 (giải trình) | 88/83 → bắt buộc; 91/89 (đổi mức) → bắt buộc; 88/85 → không | như văn bản | `RequiresExplanation_Tc5` |
| A/B/C/D = code cũ | Tỷ trọng bảng cũ + làm tròn {2, HalfEven}: tỷ lệ có trọng số trùng từng bit, điểm SP / tổng nhiệm vụ / tổng trùng hệt 20 000 bộ ngẫu nhiên; ngưỡng mức, trần (mẫu số "tốt trở lên", cắt) trùng | — | `AbcdFormula_WithOldWeightsAndRounding_IsIdenticalToOldCode_OnRandomInputs`, `Grades_And_Quota_WithOldParameters_EqualOldCode` |

## Luồng kiểm thử

Unit: `backend/tests/CongTacDang.UnitTests/CriteriaSetUnitTests.cs` (mới, **33** trường hợp): 2 bộ mặc định đúng trích xuất và hợp lệ; 23 kiểu nội dung sai bị từ chối (trùng mã, mã sai, tổng ≠ 30, trục ≠ 70, khung ≠ 100%, thiếu thang 0%, thiếu/đảo ngưỡng mức, KHT ≠ 0, khung mặc định không có, làm tròn, trần, schema, mẫu tự chấm…); JSON khứ hồi (enum dạng chuỗi) + ảnh chụp; tiêu chí chung Binary/Range, thiếu/lạ mã, căn cứ khi giảm; K/AD (thiếu lý do, `ExcludeAndRescale` 27,9, `GrantFull` 28, cấm K/AD, tất cả K/AD); làm tròn 15 trường hợp × 3 kiểu + sai số nhị phân; TC-1, TC-2, TC-3, TC-5; A/B/C/D = code cũ; gợi ý mức có điều kiện vượt chuẩn; trục 09B; đăng ký theo tham số bộ.

Tích hợp: `backend/tests/CongTacDang.IntegrationTests/CriteriaSetIntegrationTests.cs` (mới) + `CriteriaTestData.cs` (dữ liệu chấm dùng chung).

| # | Luồng (task file mục Test) | Trạng thái | Test |
|---|---|---|---|
| 1 | Tạo bộ nháp → sửa sai (lưu được, `validationErrors`) → xuất bản 400 → sửa đúng → xuất bản → sửa/xóa bộ đã xuất bản 409 → kỳ chọn bộ → mở kỳ (chụp) → nhân bản + sửa (ngưỡng 3, sửa nội dung 1.1) + xuất bản; lưu trữ bộ gốc → kỳ đã mở giữ nguyên ảnh chụp (mã, ngưỡng 5, nội dung cũ); bộ đã lưu trữ không chọn được cho kỳ mới | pass | `C1_DraftPublishSnapshot_SelfScoreWithNotApplicable_ExplanationRequired_ToPublished` |
| 2 | Tự chấm 17 tiêu chí + K/AD: thiếu lý do → 400; có lý do → 27,9 (quy đổi) + 62 trục = 89,9, gợi ý HTT; thẩm định 84 (chênh 5,9) thiếu giải trình → 400, có giải trình → lưu `appraisalExplanation`; đi tới `Published`; xuất Mẫu 10 | pass | như trên |
| 3 | Bộ 09A: khung "K9" không có trong bộ → readiness báo lỗi, mở kỳ 409; sửa ảnh chụp khung không có → 400, "k1" → K1 (tên khung); mã trục lạ → 400; đăng ký có trục; tự chấm A-B-C-D theo K2 làm tròn 1 chữ số (29,3/23,5/14,3 → 67,1; tổng 97,1; 1/3 vượt chuẩn → HTXS); xuất Mẫu 01 (có mã trục) và Mẫu 02 (29,3) | pass | `C2_Form09A_WeightFrameReadiness_TaskAxisCodes_AbcdByFrame_ExportsMau01Mau02` |
| 4 | Quyền: `period.manage` xem được danh sách, tạo → 403 nêu "Quản lý bộ tiêu chí"; người thường không xem danh sách, xem được danh mục khung; bản nháp không chọn được cho kỳ, xóa được; vai trò Cơ quan thẩm định có `criteria.manage`; 2 bộ mặc định được seed | pass | `C3_Permissions_AndWeightFrameOptions` |

Test cũ được sửa **kỳ vọng** do task yêu cầu đổi hành vi (không đổi ý nghĩa kiểm thử): dữ liệu tự chấm dạng mảng 6 số → dictionary theo mã (`CriteriaTestData`), thêm `explanation` cho thẩm định chênh lệch, `settings.selfScoreForm` → `criteria.selfScoreForm`, `jobGroup` → `weightFrameCode`, W5 "đổi mẫu tự chấm khi kỳ đã mở" → "đổi bộ tiêu chí khi kỳ đã mở" (409); kỳ dựng trực tiếp trong CSDL (`AuthorizationMatrixTests`) có ảnh chụp bộ 09A + khung K2; SQL chèn tài khoản (`DataSeederAuthorizationTests`) bỏ cột `JobGroup`; danh mục quyền (`AuthzContractTests`) có `criteria.manage`; test `PeriodSettings` bỏ `SelfScoreForm`; test tính điểm cũ (`EvaluationWorkflowUnitTests`) chuyển sang `CriteriaSetUnitTests` với bản sao công thức cũ.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx --no-incremental` | pass | 0 lỗi, **0 warning** |
| `dotnet test` — unit | pass | 256 test: 255 pass, 1 skip sẵn có (`PdfConversionTests.Mau01…` — máy không có LibreOffice), 0 fail |
| `dotnet test` — integration (máy chủ 159, `CONGTACDANG_TEST_PG`) | pass | **92 chạy, 92 pass, 0 skip**, 0 fail — 2 lần liên tiếp (3 test mới) |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | `next lint`: không cảnh báo ở file của task (2 cảnh báo sẵn có ở `app/audit/page.tsx`, `DocumentViewerModal.tsx`) |
| grep `GeneralScoreT\|AxisScoreT\|TaskResultAxis\|JobGroup\|GeneralCriterionMaxScore` | pass (trừ migration cũ) | Rỗng trong `backend/src` (mã nguồn) và `frontend/{app,components,services,contexts}`; chỉ còn trong `Data/Migrations/20260929031346_InitialCreate*` (người điều phối sinh lại). Không còn mảng 6 điểm/6 trục cứng (`AXIS_NAMES`, `GENERAL_NAMES`, `[5,5,5,5,5,5]`, `GeneralCriteriaCount`, `AxisCount`) |
| Migration khi chạy test | không commit | Test tích hợp dựng CSDL bằng `MigrateAsync` nên đã **tạm** sinh cục bộ `dotnet ef migrations add TmpTask16Local -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api -o Data/Migrations`, chạy test, rồi xóa 2 tệp `*_TmpTask16Local*.cs` và `git checkout` `CongTacDangDbContextModelSnapshot.cs`, build lại 0 warning. Không commit nào chứa migration/snapshot đã đổi |
| Chạy app / thử giao diện thủ công | không chạy | Không tạo `ctd_it_h16`; giao diện kiểm bằng `tsc` + `build` + lint, API bằng test tích hợp |
| CSDL tạm | chỉ `ctd_it_*` | Fixture tạo/xóa; không đụng `congtacdang_test` |

## Thay đổi schema (cần migration — người điều phối gộp vào `InitialCreate`)
- Bảng mới `criteria_sets`: `Id` uuid PK, `Code` varchar(50) **unique**, `Name` varchar(200), `Status` int (index), `SelfScoreForm` varchar(10), `Notes` varchar(4000) null, `Content` jsonb, `SourceSetId` uuid null, `PublishedAt`/`PublishedBy`/`ArchivedAt` null, audit (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`), `xmin`.
- `evaluation_periods`: thêm `CriteriaSetId` uuid null (FK → `criteria_sets`, **Restrict**, index), `CriteriaSnapshot` jsonb null.
- `evaluation_records`: **bỏ** `GeneralScoreT1..T6`, `AxisScoreT1..T6`, `JobGroup`; thêm `GeneralScores` jsonb not null (mặc định `'{}'` cho dữ liệu cũ nếu có), `AxisScores` jsonb null, `WeightFrameCode` varchar(20) not null, `AppraisalExplanation` varchar(4000) null.
- `evaluation_tasks`: thêm `AxisCode` varchar(20) null.
- `party_member_profiles`: **bỏ** `JobGroup` (int); thêm `WeightFrameCode` text null.
- `evaluation_periods.Settings` (jsonb): không còn `selfScoreForm`, `parameters` (khóa cũ bị bỏ qua khi đọc).
- Dữ liệu seed: 2 bộ tiêu chí Published (khi bảng trống); vai trò Cơ quan thẩm định mặc định thêm `criteria.manage` (CSDL đã có vai trò: chạy `Database:ResetRolePermissions` hoặc gán qua giao diện).

## Key config mới
Không có.

## Thay đổi hành vi API / breaking change
- **Mới** `api/criteria-sets`: `GET` (danh sách; `criteria.manage` hoặc `period.manage`), `GET {id}`, `GET defaults/{09A|09B}`, `GET weight-frames` (mọi người đã đăng nhập), `POST` (tạo nháp), `POST {id}/clone`, `PUT {id}` (chỉ nháp; bộ đã xuất bản → 409), `POST {id}/publish` (lỗi kiểm tra → 400), `POST {id}/archive`, `DELETE {id}?version=` (chỉ nháp). Ghi: `criteria.manage`.
- Kỳ: `EvaluationPeriodDto` thêm `criteriaSetId`, `criteria` (ảnh chụp); `settings` bỏ `selfScoreForm`, `parameters`. `POST periods` / `PUT periods/{id}` nhận `criteriaSetId` (chỉ bộ Published; kỳ đã mở → 409). Mở kỳ khi chưa có bộ → 400; bộ không còn Published → 409. `PeriodPresetDto.suggestedForm`. Readiness có thêm cảnh báo khung tỷ trọng (bước `B2_SELF_SCORE`, `permission` rỗng).
- Người được đánh giá: `jobGroup` → `weightFrameCode` (+ `weightFrameName`); sửa ảnh chụp nhận `weightFrameCode` (phải có trong bộ của kỳ).
- Hồ sơ: `generalScores` từ mảng → object theo mã tiêu chí con `{ score, notApplicable, reason }`; `axisScores` từ mảng → object theo mã trục; `jobGroup` → `weightFrameCode`; thêm `appraisalExplanation`; `selfScoreForm` lấy từ bộ của kỳ. Nhiệm vụ có `axisCode`.
- `self-score/submit`: `generalScores` / `axisScores` dạng object như trên (thiếu/lạ mã, sai "Đảm bảo/Không đảm bảo", thiếu lý do → 400). `tasks/submit`: `axisCode` tùy chọn (không thuộc bộ → 400). `appraisal`: `explanation` — bắt buộc khi chênh lệch ≥ ngưỡng hoặc đổi mức (400).
- Tài khoản: `weightFrameCode` khi tạo/sửa, trong danh sách; `GET /api/users/profile` (`UserProfileDto`): `jobGroup` → `weightFrameCode`. Import cán bộ: cột mới "Mã khung tỷ trọng" (`weightFrameCode`).

## Cần phối hợp
- **Task 17 / người điều phối — `components/layout/AppSidebar.tsx`:** thêm mục "Bộ tiêu chí" → `/criteria` (hiện khi có `criteria.manage` hoặc `period.manage`). Tạm thời vào qua nút "Bộ tiêu chí" ở trang Kỳ đánh giá.
- **Task 17 — `app/admin/users/**`:** ô chọn "Khung tỷ trọng mặc định" trong hồ sơ cán bộ: danh sách từ `GET /api/criteria-sets/weight-frames`, lưu bằng `weightFrameCode` của `POST/PUT /api/users`.
- **Task 17 — template `Mau_10_PhieuThamDinh.docx`:** mẫu có dòng "Nội dung giải trình của cá nhân (khi chênh lệch ≥ 5 điểm)" nhưng chưa có content control. Thêm tag (ví dụ `DISCREPANCY_EXPLANATION`) thì bổ sung một dòng trong `Mau10Data` (`record.AppraisalExplanation`); hiện không thêm vì `DocumentTemplateTests` yêu cầu tag dữ liệu ⊆ tag template.
- **Merge `PermissionCodes` + `AuthzContractTests.PermissionCodes_MatchDesignCatalog`:** task 17 cũng thêm 2 mã — gộp cả 3 mã vào danh sách kỳ vọng và tập "chỉ phạm vi Toàn công ty".
- **Người điều phối:** sinh lại `InitialCreate` (mục "Thay đổi schema"); cập nhật `docs/thiet-ke/phan-quyen.md` (quyền `criteria.manage`, vai trò Cơ quan thẩm định) và `docs/thiet-ke/luong-danh-gia.md` (cấu hình kỳ không còn `parameters`/`selfScoreForm`; bộ tiêu chí + ảnh chụp; giải trình chênh lệch khi thẩm định); `docs/deployment.md` (go-live: kiểm tra/xuất bản bộ tiêu chí trước khi tạo kỳ).
- **Ngoài phạm vi, đã sửa tối thiểu vì gỡ enum/đổi DTO:** `ReportService` (2 dòng trần + hàm lấy quy tắc), `app/evaluations/[recordId]/page.tsx` (1 dòng hiển thị điểm theo trục), `components/admin/EffectivePermissionsPanel.tsx` + `RoleService` (tên phân hệ "Bộ tiêu chí"), `EvaluationService`/`CollectiveEvaluationService` (trần, điểm tối đa tập thể), tài khoản (`UserAccountService`, `IUserAccountService`, `AccountDtos`, `UserController`, `UserService`, `DtoModels`), `IEvaluationWorkflowRepository`/`EvaluationWorkflowRepository` (ảnh chụp khung).

## Câu hỏi nghiệp vụ (cần Phòng TCCB-LĐ / Ban Tổ chức Đảng ủy xác nhận)
1. Hai bộ mặc định (17 tiêu chí con, 6 trục + điểm 09B, 4 khung, thang quy đổi, ngưỡng 90/70/50, 3–7 sản phẩm, trần 20%) có đúng văn bản áp dụng tại ATTECH không?
2. Làm tròn: 1 chữ số thập phân (ví dụ PL II) hay bước 0,5 điểm (Mục III.1)? Làm tròn điểm từng sản phẩm rồi cộng, hay cộng rồi mới làm tròn?
3. Tiêu chí chung chấm "Đảm bảo/Không đảm bảo" (Mẫu 09) hay giảm điểm theo mức độ (thân văn bản tr.10)? Khoản giảm từ 1 điểm có bắt buộc nêu căn cứ cả khi chấm "Không đảm bảo"?
4. Tiêu chí "K/AD": bỏ khỏi mẫu số rồi quy đổi về 30 điểm (đang dùng) hay cách khác? Trục 09B có được "K/AD" không, nếu có phân bổ lại điểm thế nào?
5. Trần xuất sắc: mẫu số là số "Hoàn thành tốt" (III.6) hay "Hoàn thành tốt trở lên" (Mẫu 15A/15B/16)? Làm tròn 0,5 lên 1 áp cho số người?
6. Chênh lệch tự chấm – thẩm định: ngưỡng 5 điểm và "đổi mức cũng phải giải trình" (PL II tr.31) — đúng không? Ai nhập giải trình: người thẩm định (căn cứ) hay cá nhân (Mẫu 10 "giải trình của cá nhân")? Kết quả do cấp trên thẩm định có cần giải trình không?
7. Cán bộ chưa khai báo khung tỷ trọng: gán Khung 2 mặc định (như code cũ) hay bắt buộc chọn?
8. Điều kiện kèm theo của các mức (100% nhiệm vụ, Mức 1/2/3 nhiệm vụ trọng tâm, khắc phục hạn chế…) có cần phần mềm kiểm tự động không (cần thêm dữ liệu "nhiệm vụ trọng tâm", "Mức 1/2/3")?

## Phát hiện thêm
- **Thẩm định chi tiết:** hồ sơ chỉ lưu tổng điểm thẩm định; Mẫu 10 các ô "điểm thẩm định từng nhóm/chênh lệch từng nhóm" vẫn giữ chữ mặc định. Nếu cần, thêm `AppraisalGeneralScores/AppraisalAxisScores` (cùng dạng jsonb) — đề xuất mã mới, mức 🟡.
- **Kết quả thẩm định do cấp trên (External B3b):** chưa bắt giải trình chênh lệch (dựa văn bản của cấp trên) — cần nghiệp vụ quyết định (câu hỏi 6).
- **Làm tròn bước 0,5** (trích xuất mục 10 #1) chưa có trong tham số; thêm `step` vào `RoundingRule` nếu nghiệp vụ chọn.
- **Gợi ý mức khi 09B:** điều kiện "≥ 30% vượt chuẩn" không kiểm được (không có dữ liệu nhiệm vụ) → chỉ theo ngưỡng điểm.
- `frontend/app/forms/page.tsx` còn mô tả cũ "Chấm điểm 6 tiêu chí … (30 điểm)" cho Mẫu 09 — nên sửa theo bộ tiêu chí (vùng trang biểu mẫu, không thuộc task).
- Đổi khung tỷ trọng của hồ sơ sau khi đã tự chấm (09A) không tự tính lại điểm nhiệm vụ (như trước với khung chức danh) — cân nhắc chặn hoặc tính lại.
