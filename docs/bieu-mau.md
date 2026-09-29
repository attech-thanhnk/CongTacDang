# Biểu mẫu Word/PDF — cách thêm và sửa một mẫu

Tài liệu dành cho người phát triển. Bộ sinh biểu mẫu nằm ở:

| Thành phần | Vị trí |
|---|---|
| Template `.docx` (soạn bằng Word) | `backend/src/CongTacDang.Infrastructure/Templates/Word/` |
| Lớp dữ liệu mẫu (mỗi mẫu một lớp) | `backend/src/CongTacDang.Infrastructure/Documents/Forms/MauXXData.cs` |
| Bộ điền template | `backend/src/CongTacDang.Infrastructure/Services/DocxTemplateEngine.cs` |
| Định dạng số, ngày, mức xếp loại | `backend/src/CongTacDang.Infrastructure/Documents/FormText.cs` |
| Đọc template (phiên bản đang kích hoạt, không có thì file gốc) | `backend/src/CongTacDang.Infrastructure/Documents/WordTemplateStore.cs` |
| Danh mục biểu mẫu Word (mã, tên, lớp dữ liệu) | `backend/src/CongTacDang.Infrastructure/Documents/WordFormCatalog.cs` |
| Kiểm tra file mẫu khi tải lên | `backend/src/CongTacDang.Infrastructure/Documents/WordTemplateValidator.cs` |
| Tag thông tin đơn vị dùng chung (`ORG_*`) | `OrganizationTemplateFields` trong `Documents/TemplateData.cs` |
| Chuyển DOCX/XLSX → PDF | `backend/src/CongTacDang.Infrastructure/Documents/LibreOfficePdfConverter.cs` |
| Gọi xuất (dựng dữ liệu, điền, chuyển PDF) | `backend/src/CongTacDang.Infrastructure/Services/ReportService.cs` |
| Endpoint | `backend/src/CongTacDang.Api/Controllers/ExportReportController.cs`; biểu mẫu cá nhân của hồ sơ (09A/09B/09C/9D, mục 8): `RecordFormsController.cs` + `Infrastructure/Services/RecordFormService.cs` |

Nguyên tắc:

- **Template là tệp Word thật.** Không sinh phôi bằng code. Bố cục, chữ in sẵn (tiêu đề, dòng chấm, chữ ký…) nằm trong template, không nằm trong code.
- **Không ghi cứng tên đơn vị.** Tên Đảng bộ, tên công ty, địa danh… lấy từ *Thông tin đơn vị* (Quản trị → Thông tin đơn vị, bảng `organization_settings`) qua tag `ORG_*` (mục 6).
- **Một nguồn số liệu.** Lớp dữ liệu mẫu chỉ lấy giá trị đã lưu trên hồ sơ (ví dụ điểm nhiệm vụ = `EvaluationTask.SelfScore`), không tự tính lại điểm. Định dạng (số chữ số thập phân, dấu phẩy) đặt ở `FormText`.
- **Không tìm/thay chuỗi.** Vị trí điền dữ liệu là *Content Control* (thẻ nội dung) của Word, định danh bằng thuộc tính **Tag**. Word có tách chữ thành nhiều đoạn (run) cũng không ảnh hưởng.

## 1. Quy ước Tag

| Loại | Tag | Tác dụng |
|---|---|---|
| Trường đơn | `TÊN_TRƯỜNG` (ví dụ `FULL_NAME`) | Thay toàn bộ nội dung control bằng giá trị, giữ định dạng (font, cỡ, đậm…) của chữ đầu tiên trong control. Giá trị có xuống dòng → ngắt dòng. |
| Khối lặp | `repeat:TÊN_DANH_SÁCH` (ví dụ `repeat:TASKS`) | Nhân bản nội dung control (thường là **một dòng bảng**, cũng có thể là nhiều dòng hoặc đoạn văn) cho mỗi phần tử. Tag bên trong khối tra theo phần tử trước, sau đó tới dữ liệu chung của mẫu. Danh sách rỗng → khối biến mất. |
| Khối điều kiện | `if:TÊN` / `ifnot:TÊN` | Giữ nội dung khi điều kiện đúng / sai, ngược lại bỏ đi. |

- **Chữ mặc định:** nội dung đang gõ trong control chính là chữ hiện ra khi dữ liệu để `null` (ví dụ dòng chấm `................`, “CHI BỘ CƠ SỞ TRỰC THUỘC”). Trường luôn có dữ liệu được ghi dạng `«TÊN_TRƯỜNG»` để dễ nhận ra khi soạn.
- Sau khi điền, control có Tag được gỡ bỏ, tài liệu xuất ra là văn bản thường. Control không có Tag (ví dụ số trang trong header) giữ nguyên.
- Tag không phân biệt hoa thường; nên dùng chữ HOA, số và `_`.

## 2. Tạo hoặc sửa template trong Word

1. Bật tab **Developer** (File → Options → Customize Ribbon → đánh dấu *Developer*).
2. Soạn văn bản như bình thường (bố cục, bảng, chữ in sẵn).
3. Trường đơn: bôi đen chữ mặc định (ví dụ `................`) → Developer → **Plain Text Content Control** (hoặc Rich Text) → **Properties** → điền **Tag** (và Title cùng giá trị cho dễ nhìn).
4. Bảng lặp dòng: đặt các trường của một dòng mẫu như bước 3, sau đó **chọn cả dòng** (bấm lề trái của dòng) → Developer → **Rich Text Content Control** (hoặc *Repeating Section Content Control*) → Tag `repeat:TÊN_DANH_SÁCH`. Chỉ giữ **một** dòng mẫu; các dòng tiêu đề/tổng cộng để ngoài control.
5. Khối điều kiện: chọn phần chữ/đoạn cần ẩn hiện → Rich Text Content Control → Tag `if:TÊN` (hoặc `ifnot:TÊN`). Muốn hiển thị “A” khi đúng, “B” khi sai: đặt hai control liền nhau `if:TÊN` chứa “A” và `ifnot:TÊN` chứa “B” (ví dụ cột “Ghi nhận vượt chuẩn” của Mẫu 02).
6. Thay file mẫu khi đang vận hành: **Quản trị → Biểu mẫu Word → Tải lên** (mục 7) — không cần chép tệp lên máy chủ.
   File mẫu gốc (đi kèm ứng dụng, dùng khi chưa có phiên bản nào được kích hoạt) nằm trong `Templates/Word/`, được copy ra thư mục
   chạy ứng dụng khi build; `Documents:TemplatePath` trỏ thư mục file gốc khác nếu cần.

Có thể xem Tag của template bằng `DocxTemplateEngine.GetTags(bytes)`.

## 3. Viết lớp dữ liệu mẫu

Mỗi mẫu một lớp trong `Documents/Forms/`, gắn thuộc tính lên các property:

```csharp
public sealed class Mau99Data
{
    public const string TemplateFileName = "Mau_99_TenMau.docx";

    [TemplateField("FULL_NAME")] public string? FullName { get; init; }        // trường đơn (null = giữ chữ mặc định)
    [TemplateCondition("HAS_COMMENT")] public bool HasComment { get; init; }   // if:HAS_COMMENT / ifnot:HAS_COMMENT
    [TemplateCollection("TASKS")] public List<Mau99Row> Tasks { get; init; } = new(); // repeat:TASKS

    public static Mau99Data From(EvaluationRecord record) => new()
    {
        FullName = FormText.OrNull(record.Member?.FullName),
        HasComment = !string.IsNullOrWhiteSpace(record.AppraisalComment),
        Tasks = record.Tasks.OrderBy(t => t.TaskOrder)
            .Select((t, i) => new Mau99Row { Order = (i + 1).ToString(), Score = FormText.Number(t.SelfScore, 2) })
            .ToList()
    };
}

public sealed class Mau99Row
{
    [TemplateField("T_STT")] public string? Order { get; init; }
    [TemplateField("T_SCORE")] public string? Score { get; init; }
}
```

- Property để `string?`; định dạng số/ngày bằng `FormText` (dấu phẩy thập phân như chữ in sẵn trong template).
- Chỉ đọc giá trị đã lưu. Nếu biểu mẫu cần một con số chưa được lưu, bổ sung vào luồng nghiệp vụ (service) trước, không tính trong lớp dữ liệu mẫu.
- Trường chưa có dữ liệu vẫn khai báo (để `null`) và ghi chú, để kiểm thử đối chiếu Tag không báo thiếu.

## 4. Nối vào luồng xuất

1. `ReportService`: thêm phương thức nạp dữ liệu (kèm `Include` cần thiết) rồi gọi
   `RenderWord(Mau99Data.TemplateFileName, Mau99Data.From(record))` và `ToResultAsync(...)` (tự chuyển PDF khi `format=pdf`).
2. `IReportService`: khai báo phương thức.
3. `ExportReportController`: thêm route, **kiểm tra quyền qua `IReportAccessService`/`IAccessPolicy`** trước khi xuất, nhận `format` (`docx` | `pdf`).
4. `WordFormCatalog`: thêm một dòng (mã `MAU_XX`, tên, `TemplateFileName`, `typeof(MauXXData)`) để biểu mẫu xuất hiện trên trang
   quản lý file mẫu và được kiểm tra tag khi tải lên.
5. Kiểm thử: thêm mẫu vào `DocumentTemplateTests.AllForms` — test kiểm tra mọi Tag trong template đều có trong lớp dữ liệu hoặc là
   tag thông tin đơn vị `ORG_*` (không thiếu, không thừa) và tài liệu xuất ra hợp lệ theo OpenXML; `OrgSettingsTemplateTests`
   kiểm tra file gốc qua được bộ kiểm tra khi tải lên (không lỗi, không cảnh báo).

## 5. Xuất PDF

- API: thêm `?format=pdf` vào endpoint xuất (Word và Excel). Mặc định `docx`/`xlsx`.
- Máy chủ chuyển bằng LibreOffice headless (`soffice --headless --convert-to pdf`), chạy hoàn toàn trong mạng nội bộ, có timeout và thư mục tạm riêng cho mỗi lần chuyển.
- Thiếu LibreOffice → API trả **503** kèm thông báo, xuất Word/Excel vẫn hoạt động. Cài đặt: xem `docs/deployment.md` mục “LibreOffice (xuất PDF)”.
- Cấu hình: `Documents:Pdf:SofficePath`, `Documents:Pdf:TimeoutSeconds`, `Documents:Pdf:MaxConcurrency`, `Documents:Pdf:WorkDirectory`.

## 6. Tag thông tin đơn vị (dùng chung mọi mẫu)

Giá trị lấy từ *Thông tin đơn vị* (API `GET/PUT /api/settings/organization`, quyền sửa `system.settings.manage`). Tag **không bắt
buộc**: mẫu nào cần thì đặt Content Control tương ứng. Cài đặt để trống → giữ chữ mặc định trong control.

| Tag | Giá trị | Đang dùng ở file gốc |
|---|---|---|
| `ORG_PARTY_NAME` | Tên Đảng bộ (ghi đúng như in) | Mẫu 09A, 09B, 09C, 9D — dòng "ĐẢNG BỘ …" tiêu đề trái (Mẫu 11, 13 dùng tên Đảng bộ làm dòng tổ chức lập phiếu khi xuất toàn Đảng bộ, qua `PARTY_CELL`) |
| `ORG_SUPERIOR_PARTY_NAME` | Tên tổ chức Đảng cấp trên (ghi đúng như in) | Mẫu 11, 13 — dòng trên cùng tiêu đề trái |
| `ORG_COMPANY_NAME` / `ORG_COMPANY_NAME_UPPER` | Tên công ty / chữ in hoa | Mẫu 01, 02 — dòng cơ quan quản lý (`_UPPER`) |
| `ORG_PARENT_COMPANY_NAME` / `ORG_PARENT_COMPANY_NAME_UPPER` | Tên đơn vị chủ quản / chữ in hoa | Mẫu 10 — dòng cơ quan cấp trên (`_UPPER`) |
| `ORG_SHORT_NAME` | Tên viết tắt | — |
| `ORG_LOCATION` | Địa danh | Mẫu 01, 02, 09A, 09B, 09C, 9D, 10, 11 — phần `……` của dòng "……, ngày … tháng … năm …" |

Excel (Mẫu 14, 15A, 15B, báo cáo nội bộ) dùng cùng cài đặt: dòng tiêu đề trái "ĐẢNG BỘ … / ĐẢNG ỦY (CHI BỘ) …" (toàn
Đảng bộ: tổ chức Đảng cấp trên + tên Đảng bộ; theo tổ chức Đảng: tổ chức cha + tổ chức được chọn), dòng "Địa danh, ngày … tháng
… năm …", tên tệp theo quy cách HD03 V.1 (`Mau 15A_<tên viết tắt>_Quy III-2026.xlsx`, không dấu) và tên tệp danh sách cán bộ
(`DanhSach_CanBo_<tên viết tắt>.xlsx`).

### Danh mục tag từng mẫu

Nguồn chính xác là lớp dữ liệu (`Documents/Forms/MauXXData.cs`); trang **Quản trị → Biểu mẫu Word → Tag** hiển thị danh mục
sinh từ code nên luôn khớp phiên bản đang chạy. Tag trong khối lặp chỉ đặt bên trong control `repeat:` tương ứng. Mọi tag
dưới đây là **bắt buộc** (thiếu → cảnh báo khi tải lên); khối điều kiện chỉ cần một trong hai nhánh `if:`/`ifnot:`.

| Mẫu | Tag cấp tài liệu | Khối lặp và tag trong khối |
|---|---|---|
| `MAU_01` Phiếu giao / đăng ký sản phẩm | `DEPARTMENT`, `QUARTER`, `YEAR`, `FULL_NAME`, `POSITION`, `SUPERVISOR_NAME` | `repeat:TASKS`: `T_STT`, `T_NAME`, `T_CODE`, `T_AXIS`, `T_ROLE`, `T_WEIGHT`, `T_DEADLINE`, `T_STANDARD`, `T_EXCEED`, `T_EVIDENCE`, `T_SIGNER` — `T_AXIS` là **mã trục** (T1…) theo bộ tiêu chí của kỳ (nhiệm vụ chưa chọn trục → giữ chữ mặc định) |
| `MAU_02` Phiếu tự đánh giá | `DEPARTMENT`, `QUARTER`, `YEAR`, `FULL_NAME`, `POSITION` | `repeat:TASKS`: `T_STT`, `T_NAME`, `T_WEIGHT`, `T_A`, `T_B`, `T_C`, `T_D`, `T_RESULT_PCT`, `T_SCORE`, `T_EVIDENCE`, `if:T_IS_EXCEED` / `ifnot:T_IS_EXCEED` — `T_A`…`T_D` là tỷ lệ đạt từng tiêu chí (%), `T_RESULT_PCT` là kết quả theo **khung tỷ trọng của hồ sơ**; `T_SCORE` in theo số chữ số làm tròn "điểm sản phẩm" của bộ tiêu chí. Dòng lấy từ nhiệm vụ đã đăng ký (mẫu 09A); kỳ dùng mẫu 09B không đăng ký nhiệm vụ nên bảng trống |
| `MAU_10` Phiếu thẩm định | `QUARTER`, `YEAR`, `FULL_NAME`, `POSITION`, `DEPARTMENT`, `GENERAL_SELF_SCORE`, `GENERAL_APPRAISAL_SCORE`, `GENERAL_DIFF`, `TASKS_SELF_SCORE`, `TASKS_APPRAISAL_SCORE`, `TASKS_DIFF`, `TOTAL_SELF_SCORE`, `TOTAL_APPRAISAL_SCORE`, `TOTAL_DIFF`, `SUPERVISOR_COMMENT`, `APPRAISAL_COMMENT`, `APPRAISAL_EXPLANATION`, `PROPOSED_GRADE` — `GENERAL_SELF_SCORE` là tổng tiêu chí chung theo bộ (tính cả K/AD theo quy tắc của bộ), `TASKS_SELF_SCORE` là tổng nhiệm vụ (09A) hoặc tổng điểm trục (09B); `APPRAISAL_EXPLANATION` là nội dung giải trình/căn cứ bắt buộc khi chênh lệch tự chấm – thẩm định đạt ngưỡng của bộ; `GENERAL_/TASKS_APPRAISAL_SCORE`, `GENERAL_/TASKS_DIFF` chưa có dữ liệu (hồ sơ chỉ lưu tổng điểm thẩm định) → giữ chữ mặc định | — |
| `MAU_11` Phiếu đánh giá, xếp loại (bỏ phiếu) | `PARTY_CELL`, `PERIOD_QUARTER_YEAR` | `repeat:RECORDS`: `R_STT`, `R_NAME`, `R_POSITION_DEPT`, `R_GENERAL_SCORE`, `R_TASKS_SCORE`, `R_SELF_GRADE` |
| `MAU_13` Biên bản kiểm phiếu | `PARTY_CELL`, `PERIOD_QUARTER_YEAR`, `TOTAL_VOTERS`, `INVALID_BALLOTS` | `repeat:RECORDS`: `V_STT`, `V_NAME`, `V_POSITION_DEPT`, `V_EXC`, `V_GOOD`, `V_SAT`, `V_UNSAT`, `V_PCT` |
| `MAU_09A` Phiếu tự chấm (theo sản phẩm Mẫu 01/02) | `PARTY_CELL`, `QUARTER`, `YEAR`, `FULL_NAME`, `PARTY_POSITION`, `ADMIN_POSITION`, `MASS_POSITION`, `DEPARTMENT`, `GENERAL_SCORE`, `TASKS_SCORE`, `TOTAL_SCORE`, `EXCEED_COUNT`, `TASK_COUNT`, `EXCEED_PERCENT`, `CHECK_EXCELLENT`, `CHECK_GOOD`, `CHECK_DONE`, `CHECK_FAILED` — `CHECK_*` là ô ☒/☐ của mức tự đề xuất; `EXCEED_PERCENT` là số nguyên (ký hiệu % in sẵn) | `repeat:GROUPS` (dòng nhóm): `G_NO`, `G_NAME`, `G_MAX`, lồng `repeat:ITEMS` (dòng tiêu chí con): `I_CODE`, `I_TEXT`, `I_MET`, `I_NOT_MET`, `I_MAX`, `I_SCORE`, `I_NOTE` |
| `MAU_09B` Phiếu tự chấm (theo trục, Quý III/2026) | `PARTY_CELL`, `QUARTER`, `YEAR`, `FULL_NAME`, `POSITIONS`, `DEPARTMENT`, `CELL_NAME`, `GENERAL_SCORE`, `TASKS_SCORE`, `TOTAL_SCORE`, `SELF_GRADE` | `repeat:GROUPS` / `repeat:ITEMS` như Mẫu 09A; `repeat:AXES`: `A_NO`, `A_TITLE`, `A_GUIDANCE`, `A_TARGET`, `A_MAX`, `A_SCORE`, `A_RESULT`, `A_NOTE` — `A_TITLE`/`A_GUIDANCE` lấy từ `formTitle`/`formGuidance` của trục trong bộ tiêu chí; `A_TARGET`/`A_RESULT`/`A_NOTE` là phần tự luận theo trục chủ hồ sơ nhập khi tự chấm |
| `MAU_09C` Bản tự đánh giá, xếp loại của cá nhân | `PARTY_CELL`, `QUARTER`, `YEAR`, `FULL_NAME`, `PARTY_POSITION`, `ADMIN_POSITION`, `MASS_POSITION`, `DEPARTMENT`, `GENERAL_SCORE`, `TASKS_SCORE`, `TOTAL_SCORE`, `SELF_GRADE` | `repeat:SECTIONS` (khối đoạn văn, mỗi mục khai báo trong bộ tiêu chí): `S_TITLE`, `S_INTRO`, `S_CONTENT`, `S_NOTE` — mục chưa nhập nội dung giữ dòng chấm của template |
| `MAU_9D` Phụ lục kết quả thực hiện nhiệm vụ trong quý | `PARTY_CELL`, `QUARTER`, `YEAR`, `FULL_NAME`, `POSITIONS`, `DEPARTMENT`, `CELL_NAME` | `repeat:AXES` (dòng "Trục n", mỗi trục của bộ tiêu chí): `A_LABEL`, `A_NAME`, lồng `repeat:ROWS` (dòng nhiệm vụ của trục): `R_STT`, `R_CONTENT`, `R_DEADLINE`, `R_STATUS`, `R_PRODUCT`, `R_PROGRESS`, `R_NOTE` |
| `MAU_07` Báo cáo tự đánh giá, xếp loại chất lượng của tập thể Đảng ủy (Chi ủy, Chi bộ) | `PARTY_PARENT`, `PARTY_ORG`, `SUBJECT_NAME_UPPER`, `QUARTER`, `YEAR`, `UNIT_NAME`, `SUBJECT_NAME`, `STRENGTH_1`…`STRENGTH_4` (mục A.I.1–4, mã I.1–I.4), `LIMITATIONS`, `CAUSES` (A.II), `PREVIOUS_REMEDIATION` (A.III), `EXPLANATION` (A.IV), `RESPONSIBILITIES` (A.V), `REMEDIATION_PLAN` (A.VI), `GENERAL_SCORE`, `TASK_SCORE`, `TOTAL_SCORE` (B) — nội dung mỗi mục nằm ở đoạn ngay dưới tiêu đề mục in sẵn; để trống giữ dòng chấm | — |
| `MAU_08` Báo cáo tổng hợp kết quả thực hiện các nhiệm vụ của cơ quan, đơn vị | `PARTY_PARENT`, `PARTY_ORG`, `QUARTER`, `YEAR` | `repeat:ROWS` (luôn 13 dòng theo nhóm nội dung 1–13 của biểu mẫu, tên nhóm nguyên văn trong `Hd03FormCatalog`): `R_STT`, `R_TITLE`, `R_TASKS` (các dòng "- nhiệm vụ" dưới tên nhóm; nhóm 1–7 chưa nhập giữ "- Nhiệm vụ 1: …"), `R_PLAN`, `R_RESULT`, `R_ISSUES`, `R_NOTES` |
| `MAU_12` Biên bản hội nghị | `PARTY_PARENT`, `PARTY_ORG`, `MEETING_NAME`, `PERIOD_TEXT`, `WORKING_RULES`, `ORGANIZER`, `PURPOSE`, `VOTE_PURPOSE`, `START_TIME`, `START_DATE`, `END_TIME`, `LOCATION`, `INVITED`, `PRESENT`, `ABSENT`, `CHAIR_NAME`, `CHAIR_TITLE`, `SECRETARY_NAME`, `SECRETARY_TITLE`, `REPORTING_UNIT`, `ARCHIVE_UNIT`, `SECRETARY_SIGN`, `CHAIR_SIGN`, `ifnot:HAS_ATTENDEES` (dòng "…" của mục 3.2), `if:HAS_CONTENT` + `CONTENT` (diễn biến, kết quả hội nghị) — `MEETING_NAME`/`ORGANIZER`/`PURPOSE` theo bước: B3a "Hội nghị tập thể lãnh đạo, quản lý …" (đề xuất), B4 "Hội nghị &lt;Đảng ủy/Chi ủy&gt;" (quyết định, phê duyệt); giờ theo giờ Việt Nam | `repeat:ATTENDEES` (đoạn văn mục 3.2): `A_STT`, `A_NAME`, `A_TITLE` |
| `MAU_16` Báo cáo về kết quả đánh giá, xếp loại chất lượng cán bộ quý | `PARTY_PARENT`, `PARTY_ORG`, `DOC_NUMBER`, `QUARTER`, `YEAR`, `RECIPIENT`, `WORKING_RULES`, `MEETING_DATE`, `ORGANIZER`, `PROPOSER`, `PROPOSAL_1` (khối 3 đoạn mẫu của mục III.1), `PROPOSAL_2`, `PROPOSAL_3`, `SIGNER_NAME` — phần nhập tay lấy từ bản nháp (kỳ + tổ chức Đảng); trường trống giữ chữ mẫu | `repeat:BASE_ROWS` (mục I, thẩm quyền đảng ủy/chi ủy cơ sở): `B_STT`, `B_SUBJECT`, `B_TOTAL`, `B_EXC`, `B_GOOD`, `B_SAT`, `B_UNSAT`, `B_NONE`, `B_PCT`, `B_NOTE`; `repeat:SUPERIOR_ROWS` (mục II, thẩm quyền BTV Đảng ủy Tổng công ty): `S_…` cùng cột — mỗi dòng một nhóm chức danh (mã M1–M26), dòng cuối "Tổng cộng" |
| `MAU_17` Kế hoạch hỗ trợ, khắc phục 30-60-90 ngày (task 20; file mẫu dựng từ đúng phần Mẫu 17 của biểu mẫu gốc; ORG: `ORG_PARENT_COMPANY_NAME_UPPER`, `ORG_COMPANY_NAME_UPPER`, `ORG_LOCATION`) | `FULL_NAME`, `POSITION`, `DEPARTMENT`, `GRADE_LEVEL`, `SUPPORTER_NAME`, `SUPPORTER_TITLE`, `M30_/M60_/M90_` + `LIMITATION`, `TARGET`, `MEASURES`, `COORDINATION`, `ACHIEVED_BOX`, `NOT_ACHIEVED_BOX` (ô ☐ in sẵn → ☒ khi chọn), `APPROVER_NAME`, `SUPPORTER_SIGN_NAME`, `MEMBER_SIGN_NAME` (chỉ khi cá nhân đã xác nhận) | — |

Mẫu 09A/09B/09C/9D: tiêu đề trái "ĐẢNG BỘ …" dùng `ORG_PARTY_NAME`, "CHI BỘ …" là `PARTY_CELL` (tên Chi bộ ảnh chụp trên hồ sơ, in
hoa); điểm để trống (giữ dòng chấm) khi hồ sơ chưa nộp phiếu tự chấm. Template dựng từ file biểu mẫu gốc
`docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx` (cắt đúng phần của mẫu, giữ nguyên bố cục, chỉ thêm Content Control).

Template Mẫu 07, 08, 12, 16 được dựng **từ đúng biểu mẫu gốc** `docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx`:
cắt nguyên phần thân của mẫu (bảng tiêu đề, đoạn văn, bảng, chữ ký, khổ giấy của section), gắn Content Control bao đúng đoạn chữ
mặc định ("…", "……") trong đoạn gốc (giữ định dạng), bỏ màu đỏ đánh dấu soạn thảo. Mẫu 14, 15A, 15B là bảng tính (HD03 V.1:
"lập trên file excel (trừ các Mẫu: 07, 09C, 12, 13, 16)") dựng bằng ClosedXML theo đúng cột/tiêu đề biểu mẫu (PDF tr.72–76).

### Mẫu nào xuất ở đâu (task 19)

| Mẫu HD03 | Định dạng | Endpoint | Trang | Quyền / phạm vi |
|---|---|---|---|---|
| 07 — Báo cáo tự đánh giá của tập thể | Word/PDF | `GET /api/reports/docx/mau-07/{collectiveRecordId}` | Tập thể & Hội nghị (từng hồ sơ M07) | `evaluation.read` hoặc `collective.manage` trên tổ chức của hồ sơ |
| 08 — Tổng hợp kết quả nhiệm vụ của cơ quan, đơn vị | Word/PDF | `GET /api/reports/docx/mau-08/{collectiveRecordId}` | Tập thể & Hội nghị (từng hồ sơ M08) | như Mẫu 07 |
| 11 — Phiếu đánh giá, xếp loại (chưa áp dụng Q3/2026) | Word/PDF | `GET /api/reports/docx/mau-11?periodId&branchId` | Báo cáo | `meeting.read` hoặc `report.export` (phạm vi tổ chức Đảng) |
| 12 — Biên bản hội nghị (B3a, B4) | Word/PDF | `GET /api/reports/docx/mau-12/{meetingId}` | Tập thể & Hội nghị (từng biên bản M12) | `meeting.read`/`meeting.manage` trên đơn vị của biên bản |
| 13 — Biên bản kiểm phiếu | Word/PDF | `GET /api/reports/docx/mau-13?periodId&branchId` | Báo cáo | như Mẫu 11 |
| 14 — Danh sách đánh giá và đề xuất xếp loại | Excel/PDF | `GET /api/reports/form-14?periodId&branchId` | Báo cáo | `report.export` (phạm vi tổ chức Đảng) |
| 15A — Tổng hợp (đối tượng BTVĐUTCT quyết định, M1–M16) | Excel/PDF | `GET /api/reports/form-15a?periodId&branchId` | Báo cáo | `report.export` |
| 15B — Tổng hợp (đối tượng Đảng ủy/Chi ủy cơ sở quyết định, M17–M26) | Excel/PDF | `GET /api/reports/form-15b?periodId&branchId` | Báo cáo | `report.export` |
| 16 — Báo cáo kết quả đánh giá, xếp loại | Word/PDF | `GET /api/reports/docx/mau-16?periodId&branchId`; bản nháp `GET/PUT /api/reports/mau-16/draft?periodId&branchId` | Báo cáo ("Soạn báo cáo") | `report.export` |
| Báo cáo nội bộ — Danh sách cán bộ | Excel | `GET /api/reports/cadres` | Báo cáo (nhóm nội bộ) | `report.export` (lọc theo phạm vi) |
| Báo cáo nội bộ — Kiểm soát tỷ lệ Hoàn thành xuất sắc | Excel/PDF | `GET /api/reports/internal/excellent-quota?periodId&branchId` | Báo cáo (nhóm nội bộ) | `report.export` |

Đã bỏ: `GET /api/reports/form-15` (thống kê/kiểm soát trần — không có trong HD03, nay là báo cáo nội bộ ở trên) và
`GET /api/reports/form-16` (Excel — Mẫu 16 HD03 là báo cáo Word) → 404. Không truyền `branchId`: phạm vi Toàn công ty → toàn
Đảng bộ; phạm vi đúng một tổ chức Đảng → tổ chức đó; nhiều tổ chức → 400 yêu cầu chọn.

Số liệu Mẫu 15A/15B/16: mỗi cán bộ thống kê một lần theo mã chức danh nhỏ nhất đang giữ (HD03 tr.74); mức dùng để đếm là mức
quyết định, chưa có thì mức đề xuất gần nhất (cấp trực tiếp sử dụng → thẩm định → tập thể lãnh đạo), không tính mức tự đề
xuất; chưa có mức nào → "Chưa xếp loại". Mẫu 16 chia mục I/II theo thẩm quyền (ảnh chụp trên hồ sơ), Mẫu 15A/15B chia theo
khoảng mã chức danh (bố cục biểu mẫu); cán bộ chưa có mã: Mẫu 15A/15B liệt kê ở trang tính "Kiểm tra dữ liệu", Mẫu 16 ghi
dòng "Cán bộ chưa có mã chức danh thống kê".


## 7. Thay file mẫu trên giao diện (quyền `system.templates.manage`)

Trang **Quản trị → Biểu mẫu Word** liệt kê từng biểu mẫu (mã, tên, phiên bản đang dùng, người/lúc cập nhật):

- **File đang dùng** / **File gốc**: tải về để sửa. Nên sửa từ *file đang dùng*.
- **Tải lên**: chọn `.docx` → *Kiểm tra* (không lưu) → *Tải lên* (mặc định kích hoạt ngay; bỏ chọn để lưu mà chưa dùng).
- **Lịch sử**: mọi phiên bản (tệp, người tải, ghi chú, cảnh báo lúc tải), tải từng phiên bản, **Kích hoạt** lại phiên bản cũ.
- **Dùng file gốc**: bỏ kích hoạt mọi phiên bản, xuất theo file gốc đi kèm ứng dụng.

API (`/api/templates`, mọi thao tác cần `system.templates.manage`): `GET /` (danh mục + tag), `GET /{mã}/versions`,
`POST /{mã}/check` (multipart `file`), `POST /{mã}/versions` (multipart `file`, `note`, `activate`), `POST /{mã}/versions/{id}/activate`,
`POST /{mã}/use-original`, `GET /{mã}/current`, `GET /{mã}/original`, `GET /{mã}/versions/{id}/file`.

Kiểm tra khi tải lên (`WordTemplateValidator`):

| Kiểm tra | Kết quả |
|---|---|
| Đuôi `.docx`, chữ ký tệp ZIP (`PK\x03\x04`), tối đa 10 MB | Sai → từ chối (400) |
| Mở được bằng OpenXML, là tài liệu Word thường (không phải `.dotx`/`.docm`) | Sai → **lỗi**, không lưu |
| Tag không thuộc danh mục của mẫu và không phải `ORG_*` | **Lỗi**, không lưu |
| Tag bắt buộc bị thiếu | **Cảnh báo** — vẫn lưu, dữ liệu tương ứng sẽ không hiện |
| Sinh thử với dữ liệu giả (mọi trường có giá trị, điều kiện đúng, danh sách 2 dòng) | Lỗi khi điền → **lỗi**; tài liệu sinh ra sai cấu trúc OpenXML → **cảnh báo** |

Tệp lưu qua kho tệp (`IFileStorageService`, khóa `word-templates/<mã>/<yyyyMM>/<id>_v<n>.docx`), metadata trong bảng
`word_template_versions` (mỗi mẫu tối đa một phiên bản kích hoạt). Tệp của phiên bản đang kích hoạt bị mất khỏi kho → máy chủ
ghi log lỗi và xuất theo file gốc.

### Sửa file mẫu trong Word mà không làm vỡ tag

1. Bật tab **Developer**, bấm **Design Mode** để thấy khung các Content Control và Tag.
2. Chỉ sửa chữ **ngoài** control, hoặc chữ mặc định **bên trong** control (không xóa khung). Muốn chuyển vị trí một trường: cắt
   (Ctrl+X) **cả khung** control rồi dán chỗ mới.
3. Không gõ tag bằng tay dạng `«…»`/`{{…}}` — đó chỉ là chữ, không phải control. Thêm trường mới: chọn chữ → *Plain Text
   Content Control* → *Properties* → điền **Tag** đúng như danh mục (không phân biệt hoa thường).
4. Bảng lặp: control `repeat:…` phải bao **trọn một dòng bảng** (chọn cả dòng trước khi chèn); không đặt dòng tiêu đề/tổng cộng
   vào trong. Tag của dòng lặp chỉ đặt bên trong control lặp.
5. Xóa control thừa bằng chuột phải → *Remove Content Control* (giữ chữ) hoặc xóa cả khung; không dán control từ mẫu khác vào.
6. Lưu dạng **Word Document (*.docx)** (không lưu `.doc`, `.dotx`, `.docm`), rồi *Kiểm tra* trên trang trước khi tải lên.

## 8. Biểu mẫu cá nhân theo bộ tiêu chí của kỳ (Mẫu 09A, 09B, 09C, 9D — task 18)

- **Mẫu áp dụng** khai báo trong nội dung bộ tiêu chí: `requiredForms` (mã `01`, `02`, `09A`, `09B`, `09C`, `9D`, `10`). Mẫu tự chấm
  (09A/09B) luôn theo `selfScoreForm` của bộ; mã 09A/09B ghi trong `requiredForms` được bỏ qua. Bộ mặc định: 09B (Quý III/2026) →
  `09B, 09C, 9D, 10`; 09A (từ 2027) → `01, 02, 09A, 09C, 9D, 10`.
- **Mục Mẫu 09C** khai báo trong `selfAssessmentSections` (mã, tiêu đề, câu dẫn, lưu ý, số ký tự tối đa, bắt buộc); nội dung hồ sơ
  lưu jsonb theo mã mục (`evaluation_records.SelfAssessment`). **Mẫu 9D** lưu các dòng theo mã trục (`TaskResults`); phần tự luận
  theo trục của 09B lưu ở `AxisNotes`. Cả ba nhập cùng phiếu tự chấm (bước `B2_SELF_SCORE`); trường không gửi = giữ nội dung đã lưu.
- **API** (quyền xem hồ sơ — `evaluation.read` trong phạm vi, chủ hồ sơ luôn xem được; kiểm tra bằng guard trên hồ sơ):
  - `GET /api/reports/docx/record/{recordId}` — danh sách mẫu áp dụng (mã, tên).
  - `GET /api/reports/docx/record/{recordId}/{formCode}?format=docx|pdf` — xuất một mẫu; 01/02/10 dùng lại bộ xuất hiện có. Mẫu
    không áp dụng cho kỳ → 409; mã không có → 400. Tên tệp không dấu `Mau_<mã>_<Họ_tên>_Q<quý>-<năm>.docx`.
- Thay file mẫu qua **Quản trị → Biểu mẫu Word** như các mẫu khác (mã `MAU_09A`, `MAU_09B`, `MAU_09C`, `MAU_9D`).
