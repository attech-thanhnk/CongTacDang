# Task 16 — Bộ tiêu chí và thang điểm theo phiên bản (Đợt 7)

- **Agent:** H · **Branch:** `feat/criteria-sets`
- **Mã:** T-77, T-78, T-79
- **Báo cáo:** `docs/remediation/reports/16-criteria-sets.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` 7.3 + 8, `docs/thiet-ke/luong-danh-gia.md`, `docs/nghiep-vu/hd03-trich-xuat.md` mục 3 (thang điểm, 30 điểm tiêu chí chung 3 nhóm/17 tiêu chí con, 6 trục, khung tỷ trọng, thang quy đổi A/B/C/D, công thức, làm tròn), mục 4 (mức xếp loại, trần 20%), mục 8 (Quý III/2026), mục 9 (ví dụ số — dùng làm test), mục 10; `Domain/Evaluation/EvaluationParameters.cs`, `EvaluationScoring.cs`, `docs/remediation/reports/12-evaluation-workflow.md` (tham số).
- Chạy song song với task 17 (cài đặt đơn vị, biểu mẫu) — tôn trọng phạm vi file.

## Vấn đề
- Nội dung chấm điểm cứng trong code: tiêu chí chung là 6 ô `GeneralScoreT1..T6` × 5 điểm — **lệch HD03** (3 nhóm 18/4/8, 17 tiêu chí con, chấm "đảm bảo/không đảm bảo"); trục kết quả là enum `TaskResultAxis`; khung tỷ trọng là enum `JobGroup`; tham số trong kỳ chỉ xem, không sửa được.
- Văn bản thay đổi theo thời kỳ (09B cho Q3/2026, 09A từ 2027) → phải khai báo được, không sửa code.

## 1. Mô hình bộ tiêu chí (T-77)
- Entity `CriteriaSet` (bộ tiêu chí): `Code`, `Name`, `Status` (`Draft | Published | Archived`), `SelfScoreForm` (`09A` chấm nhiệm vụ theo Mẫu 01/02 + A-B-C-D, `09B` chấm trực tiếp theo trục), `Notes`, audit. Nội dung lưu `jsonb` có `schemaVersion` và validate đầy đủ:
  - **Nhóm tiêu chí chung**: `code`, `name`, danh sách tiêu chí con {`code`, `text`, `maxScore`}, `scoringMode` (`Binary` — đảm bảo = điểm tối đa, không = 0; `Range` — nhập 0..max). Cho phép đánh dấu "K/AD — Không áp dụng" có lý do (HD03 tr.9) và quy tắc xử lý điểm khi K/AD (`ExcludeAndRescale` hoặc `GrantFull` — tham số, mặc định theo HD03 bản trích xuất; ghi rõ là chờ xác nhận).
  - **Trục kết quả**: `code` (T1…), `name`, `description`, `maxScore` (dùng cho 09B).
  - **Khung tỷ trọng**: `code`, `name`, tỷ trọng A/B/C/D (tổng = 1).
  - **Mức xếp loại**: 4 mức cố định theo `EvaluationGrade` (quy định Đảng, dùng trong các biểu mẫu cố định) — cấu hình **ngưỡng điểm** và **điều kiện** kèm theo (ví dụ tỷ lệ nhiệm vụ vượt chuẩn tối thiểu cho Xuất sắc).
  - **Tham số**: số sản phẩm min/max, tổng trọng số nhiệm vụ, sai số, trần tỷ lệ Xuất sắc + cách tính mẫu số + làm tròn, **ngưỡng chênh lệch cần giải trình**, **số chữ số thập phân + kiểu làm tròn** (`HalfUp | HalfEven | Truncate`) cho từng loại điểm.
- Bộ `Published` không sửa được (chỉ nhân bản thành bản nháp mới). Xóa được bản nháp; không xóa bộ đã dùng.
- Seed 2 bộ mặc định **theo bản trích xuất HD03**: "Mẫu 09B — Quý III/2026" và "Mẫu 09A — từ 2027" (17 tiêu chí con nguyên văn rút gọn trong trích xuất mục 3.2, 6 trục + điểm tối đa 09B mục 3.3, khung tỷ trọng mục 3.6, thang quy đổi mục 3.7, mức xếp loại mục 4.1) — **ghi rõ chờ nghiệp vụ xác nhận**; chỗ nào văn bản mâu thuẫn (mục 10) chọn một, ghi lý do.

## 2. Gắn vào kỳ và hồ sơ (T-78)
- Kỳ chọn một bộ `Published` khi còn `Draft`; khi mở kỳ, **chụp nguyên bộ** vào kỳ (bất biến cho kỳ đó). Bỏ `parameters` cũ trong `PeriodSettings` (chuyển vào bộ tiêu chí). Không giữ tương thích cũ.
- `EvaluationRecord`: thay `GeneralScoreT1..T6`, `AxisScoreT1..T6` bằng điểm theo mã tiêu chí/trục (`jsonb`: `{ itemCode: { score, notApplicable, reason } }`, `{ axisCode: score }`), tương tự cho điểm thẩm định nếu có. Gỡ enum `TaskResultAxis` (nhiệm vụ gắn `axisCode` string thuộc bộ của kỳ) và enum `JobGroup` (cán bộ và hồ sơ dùng `weightFrameCode` string; cán bộ có mã khung mặc định, hồ sơ chụp lại khi thêm vào kỳ; khung phải tồn tại trong bộ của kỳ — readiness báo lỗi nếu không).
- `EvaluationScoring` tính theo bộ tiêu chí của kỳ: tiêu chí chung = tổng tiêu chí con (có K/AD), nhiệm vụ = công thức A/B/C/D hiện có với tỷ trọng khung + thang quy đổi của bộ, làm tròn theo tham số, gợi ý mức theo ngưỡng/điều kiện. **Cấu trúc công thức A/B/C/D giữ nguyên**.
- Giải trình chênh lệch (B-09): ở bước thẩm định, nếu |tự chấm − thẩm định| ≥ ngưỡng của bộ → bắt buộc nhập "nội dung giải trình/căn cứ" (lưu vào hồ sơ, hiện trên Mẫu 10 nếu template có chỗ).
- Trần tỷ lệ Xuất sắc: kiểm tra theo tham số của bộ (mẫu số, làm tròn) tại nơi đang kiểm (`branch-quotas` / bước quyết định).

## 3. Giao diện (T-79)
- Quyền mới `criteria.manage` ("Quản lý bộ tiêu chí"), mặc định gán cho vai trò Cơ quan thẩm định (Phòng TCCB-LĐ).
- Trang `app/criteria/**`: danh sách bộ, xem, nhân bản, sửa bản nháp (các nhóm/tiêu chí con/trục/khung/mức/tham số — bảng sửa trực tiếp, kiểm tra tổng điểm tức thì: nhóm chung = 30, tổng trục = 70 nếu 09B…), xuất bản, lưu trữ.
- Trang kỳ (`app/periods/[periodId]`): chọn bộ tiêu chí (khi Draft), xem bộ đã chụp.
- Form tự chấm / thẩm định (`components/evaluations/**`): dựng động từ bộ của kỳ — tiêu chí con dạng "Đảm bảo / Không đảm bảo / K/AD (lý do)" hoặc ô số, trục theo mã, tổng tạm tính; không còn số 6 hay 5 cứng.
- Hồ sơ cán bộ: chọn khung tỷ trọng mặc định (danh sách lấy từ bộ đang dùng gần nhất).

## Phạm vi file
Chủ sở hữu: `Domain/Evaluation/**` (trừ phần luồng: `RecordStateMachine`, `WorkflowSteps`, cấu trúc profile trong `PeriodSettings` — chỉ gỡ `parameters`), `CriteriaSet` + cấu hình, `EvaluationRecord`/`EvaluationTask` (trường điểm), enum `JobGroup`, `TaskResultAxis` (gỡ), `EvaluationScoring`, phần tính điểm/validate điểm trong `EvaluationWorkflowService` và `PeriodService` (chọn bộ, readiness khung), service/controller bộ tiêu chí mới, `PermissionCodes` (thêm `criteria.manage`), `DataSeeder` (bộ mặc định + gán quyền + dữ liệu mẫu điểm), `Documents/Forms/Mau02Data.cs`, `Mau10Data.cs`, `Mau01Data.cs` (nội dung tiêu chí/trục/điểm), import cán bộ (cột khung), frontend `app/criteria/**`, `app/periods/**`, `components/evaluations/**`, `services/evaluationService.ts`, service FE mới cho bộ tiêu chí.
**Không** sửa: `WordTemplateStore`, `TemplateData` (phần thông tin đơn vị), `ReportService` phần tiêu đề/tên đơn vị/tên file, `app/admin/**` (task 17). Cần đổi `ReportService` do gỡ enum → chỉ sửa đúng dòng tham chiếu.

## Test
- Unit: validate bộ tiêu chí (tổng điểm, khung tổng = 1, mã trùng…); tính điểm tiêu chí chung có K/AD; làm tròn theo từng kiểu; TC-1, TC-2 trong trích xuất mục 9 — **chạy với bộ mặc định và ghi kết quả so với văn bản** (lệch → nêu rõ tham số nào tạo lệch, không sửa văn bản); công thức A/B/C/D cho kết quả **bằng đúng** bản trước khi đổi với cùng tỷ trọng/thang quy đổi.
- Tích hợp: tạo bộ nháp → xuất bản → kỳ chọn bộ → mở kỳ (chụp) → sửa bộ gốc (nhân bản) không ảnh hưởng kỳ đã mở; tự chấm theo 17 tiêu chí + K/AD → thẩm định chênh ≥ ngưỡng bắt buộc giải trình; hồ sơ đi tới `Published`, Mẫu 02/10 xuất được.

## Tiêu chí hoàn thành
- Không còn `GeneralScoreT`, `AxisScoreT`, `TaskResultAxis`, `JobGroup` (enum), `GeneralCriterionMaxScore`, mảng 6 phần tử cứng trong `backend/src`, `frontend`.
- Build 0 warning, test (integration 0 skip), `tsc`, `npm run build`. Không tạo migration (người điều phối gộp `InitialCreate`).
- Báo cáo: mô hình JSON bộ tiêu chí, 2 bộ mặc định (bảng), kết quả TC-1/TC-2, câu hỏi nghiệp vụ.
