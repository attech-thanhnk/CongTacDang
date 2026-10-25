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
| Endpoint | `backend/src/CongTacDang.Api/Controllers/ExportReportController.cs`; biểu mẫu cá nhân của hồ sơ (09A/09C/9D, mục 8): `RecordFormsController.cs` + `Infrastructure/Services/RecordFormService.cs` |

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
5. Kiểm thử: thêm mẫu vào `DocumentTemplateTests.AllForms` (mẫu có đủ hai nhánh `if:`/`ifnot:` của mọi điều kiện) hoặc
   `CollectiveFormsTemplateTests.Forms` (điều kiện chỉ cần một nhánh — Mẫu 07, 08, 12, 13, 16) — test kiểm tra mọi Tag trong
   template đều có trong lớp dữ liệu hoặc là tag thông tin đơn vị `ORG_*` (không thiếu, không thừa) và tài liệu xuất ra hợp lệ theo
   OpenXML; `OrgSettingsTemplateTests` kiểm tra file gốc qua được bộ kiểm tra khi tải lên (không lỗi, không cảnh báo).

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
| `ORG_PARTY_NAME` | Tên Đảng bộ (ghi đúng như in) | Mẫu 09A, 09C, 9D — dòng "ĐẢNG BỘ …" tiêu đề trái (Mẫu 11 dùng tên Đảng bộ làm dòng tổ chức lập phiếu khi xuất toàn Đảng bộ, qua `PARTY_CELL`) |
| `ORG_SUPERIOR_PARTY_NAME` | Tên tổ chức Đảng cấp trên (ghi đúng như in) | Mẫu 11 — dòng trên cùng tiêu đề trái (Mẫu 07, 08, 12, 13, 16 dùng `PARTY_PARENT`/`PARTY_ORG` dựng từ cây tổ chức Đảng và cài đặt đơn vị) |
| `ORG_COMPANY_NAME` / `ORG_COMPANY_NAME_UPPER` | Tên công ty / chữ in hoa | Mẫu 01, 02 — dòng cơ quan quản lý; Mẫu 10 — dòng trên tiêu đề trái (`_UPPER`) |
| `ORG_PARENT_COMPANY_NAME` / `ORG_PARENT_COMPANY_NAME_UPPER` | Tên đơn vị chủ quản / chữ in hoa | Mẫu 17 — dòng cơ quan cấp trên (`_UPPER`) |
| `ORG_APPRAISAL_UNIT_UPPER` | Cơ quan thẩm định, chữ in hoa (để trống → giữ chữ "CƠ QUAN THẨM ĐỊNH") | Mẫu 10 — dòng dưới tiêu đề trái: phiếu do hệ thống in là phiếu của cơ quan thẩm định ở công ty (diện Đảng ủy cơ sở); diện cấp trên do Tổng công ty tự thẩm định |
| `ORG_SHORT_NAME` | Tên viết tắt | — |
| `ORG_LOCATION` | Địa danh | Mẫu 01, 02, 07, 08, 09A, 09C, 10, 11, 16, 17 — phần `……` của dòng "……, ngày … tháng … năm …" (phần ngày là tag `DATE_LINE` của từng mẫu) |

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
| `MAU_01` Phiếu giao / đăng ký sản phẩm | `DATE_LINE` (ngày duyệt nhiệm vụ), `DEPARTMENT`, `QUARTER` (số La Mã), `YEAR`, `FULL_NAME`, `POSITION`, `SUPERVISOR_NAME` | `repeat:TASKS`: `T_STT`, `T_NAME`, `T_CODE` (mã SP, không bắt buộc), `T_AXIS` (**mã trục** T1… theo bộ tiêu chí của kỳ), `T_ROLE` ("Chủ trì" / "Phối hợp"), `T_WEIGHT`, `T_DEADLINE`, `T_STANDARD` (chuẩn đạt), `T_EXCEED` (tiêu chí vượt chuẩn), `T_EVIDENCE` (nguồn dữ liệu / minh chứng), `T_SIGNER` (thẩm quyền xác nhận) — nhập ở bước đăng ký (mọi cột bắt buộc trừ mã SP); dữ liệu đăng ký cũ thiếu cột → giữ chữ mặc định |
| `MAU_02` Phiếu tự đánh giá | `DATE_LINE` (ngày nộp tự chấm), `DEPARTMENT`, `QUARTER` (số La Mã), `YEAR`, `FULL_NAME`, `POSITION`, `TOTAL_SCORE` (dòng CỘNG: tổng điểm đạt "… / 70,0"), `EXCEED_COUNT` (dòng CỘNG: số sản phẩm vượt chuẩn "… SP") — chưa có sản phẩm thì giữ dòng chấm | `repeat:TASKS`: `T_STT`, `T_NAME`, `T_WEIGHT`, `T_A`, `T_B`, `T_C`, `T_D`, `T_RESULT_PCT`, `T_SCORE`, `T_EVIDENCE`, `if:T_IS_EXCEED` / `ifnot:T_IS_EXCEED` — `T_A`…`T_D` là tỷ lệ đạt từng tiêu chí (%), `T_RESULT_PCT` là kết quả theo **khung tỷ trọng hiệu lực của hồ sơ** (gồm tỷ lệ A-B-C-D đã được điều chỉnh khi duyệt danh mục, nếu có); `T_SCORE` in theo số chữ số làm tròn "điểm sản phẩm" của bộ tiêu chí. Dòng lấy từ nhiệm vụ đã đăng ký (Mẫu 01) |
| `MAU_10` Phiếu thẩm định | `DATE_LINE` (ngày thẩm định), `QUARTER` (số La Mã), `YEAR`, `FULL_NAME`, `POSITION`, `DEPARTMENT` ("Kỳ đánh giá" chỉ in một lần ở dòng đầu phiếu), `GENERAL_SELF_SCORE`, `GENERAL_APPRAISAL_SCORE`, `GENERAL_DIFF`, `TASKS_SELF_SCORE`, `TASKS_APPRAISAL_SCORE`, `TASKS_DIFF`, `TOTAL_SELF_SCORE`, `TOTAL_APPRAISAL_SCORE`, `TOTAL_DIFF`, `SUPERVISOR_COMMENT`, `APPRAISAL_COMMENT`, `APPRAISAL_EXPLANATION` (ba ô ý kiến: dòng chấm là chữ mặc định của control, có nội dung thì không còn dòng chấm), `CHECK_EXCELLENT`, `CHECK_GOOD`, `CHECK_DONE`, `CHECK_FAILED` (ô ☒/☐ của mức Tổ thẩm định đề xuất) — điểm in gọn ("26", "26,5"); `GENERAL_SELF_SCORE` là tổng tiêu chí chung theo bộ (tính cả K/AD theo quy tắc của bộ), `TASKS_SELF_SCORE` là tổng điểm nhiệm vụ (Mẫu 02); `APPRAISAL_EXPLANATION` là nội dung giải trình/căn cứ bắt buộc khi chênh lệch tự chấm – thẩm định đạt ngưỡng của bộ; `GENERAL_/TASKS_APPRAISAL_SCORE` là điểm thẩm định theo nhóm (`AppraisalGeneralScore`/`AppraisalTasksScore`), `GENERAL_/TASKS_DIFF` = điểm thẩm định nhóm − điểm tự chấm nhóm (một chữ số thập phân); `TOTAL_APPRAISAL_SCORE` = chung + nhiệm vụ; hồ sơ chỉ có điểm tổng (kết quả cấp trên ghi nhận, dữ liệu cũ) → hai ô nhóm giữ chữ mặc định. Mẫu 10 không có cột mức từng nhiệm vụ (mức lưu trên hồ sơ, hiện ở danh mục sản phẩm) | — |
| `MAU_11` Phiếu đánh giá, xếp loại (bỏ phiếu) | `PARTY_CELL` (tổ chức Đảng lập phiếu: tổ chức được chọn, xuất toàn Đảng bộ → tên Đảng bộ trong cài đặt), `PERIOD_QUARTER_YEAR` ("QUÝ IV NĂM 2026" — quý số La Mã), `BASE_AUTHORITY` (cấp ủy ở tiêu đề mục II, dựng từ `PARTY_CELL` như Mẫu 13: "Đảng bộ X" → "ĐẢNG ỦY X"), `if:SHOW_SUPERIOR` / `if:SHOW_BASE` (mục I/II chỉ in khi có cán bộ của mục — như bảng tính Công ty dùng; cả hai rỗng → in phiếu trắng hai mục), `ifnot:HAS_SUPERIOR_ROWS` / `ifnot:HAS_BASE_ROWS` (dòng trống của phiếu trắng) | `repeat:SUPERIOR_ROWS` (mục I — thẩm quyền BTV Đảng ủy cấp trên) và `repeat:BASE_ROWS` (mục II — thẩm quyền đảng ủy/chi ủy cơ sở), cùng tag: `R_STT` (đánh lại từ 1 mỗi mục), `R_NAME`, `R_POSITION_DEPT` (chức vụ Đảng, chính quyền, đoàn thể theo danh mục, ngăn cách dấu phẩy, "— đơn vị công tác"; không giữ chức vụ nào → chức danh trên hồ sơ cán bộ), `R_GENERAL_SCORE`, `R_TASKS_SCORE`, `R_SELF_GRADE` (chưa tự chấm → trống), `R_COLLECTIVE_GRADE` (5), `R_APPRAISAL_GRADE` (6), `R_DIRECTOR_GRADE` (7) — mức đề xuất đã ghi nhận ở bước tập thể lãnh đạo, thẩm định, cấp có thẩm quyền trực tiếp (bước cấp trên thực hiện: kết quả ghi nhận); bước chưa ghi nhận hoặc không áp dụng → trống, nên phiếu in cho hội nghị tập thể lãnh đạo chỉ có cột (1)–(4c), phiếu cho hội nghị quyết định có đủ (5)–(7). Mục theo thẩm quyền ảnh chụp trên hồ sơ, thứ tự theo mã chức danh rồi họ tên |
| `MAU_13` Biên bản kiểm phiếu (theo một biên bản hội nghị) | `PARTY_PARENT`, `PARTY_ORG`, `MEETING_NAME`, `QUARTER_YEAR` (dòng tiêu đề "quý ……../….."), `PERIOD_TEXT`, `WORKING_RULES`, `ORGANIZER`, `PURPOSE`, `VOTE_PURPOSE`, `START_TIME`, `START_DATE`, `END_TIME`, `LOCATION`, `INVITED`, `PRESENT`, `ABSENT`, `CHAIR_NAME`, `CHAIR_TITLE`, `SECRETARY_NAME`, `SECRETARY_TITLE`, `REPORTING_UNIT`, `BALLOTS_ISSUED`, `BALLOTS_COLLECTED`, `BALLOTS_VALID`, `BALLOTS_INVALID`, `ARCHIVE_UNIT`, `COUNTER_HEAD_SIGN`, `CHAIR_SIGN`, `CHAIR_SIGN_TITLE`, `CHAIR_SIGN_NOTE`, `BASE_AUTHORITY` (cấp ủy cơ sở ở tiêu đề mục II bảng: cấp ủy của tổ chức Đảng lưu biên bản, "Đảng bộ X" → "ĐẢNG ỦY X"), `ifnot:HAS_ATTENDEES`, `ifnot:HAS_COUNTERS` (giữ 3 dòng Tổ kiểm phiếu của biểu mẫu), `if:SHOW_SUPERIOR` / `if:SHOW_BASE` (mục I/II chỉ in khi có cán bộ của mục, như bảng tính Công ty dùng; biên bản chưa có dòng kiểm phiếu nào → in cả hai mục), `ifnot:HAS_SUPERIOR_ROWS` / `ifnot:HAS_BASE_ROWS` (dòng trống khi in cả hai mục mà chưa có cán bộ) — phần chung với Mẫu 12 cùng quy tắc; số phiếu không hợp lệ chưa ghi trên biên bản → số chung của các dòng kiểm phiếu | `repeat:ATTENDEES` (mục I.3.2): `A_STT`, `A_NAME`, `A_TITLE`; `repeat:COUNTERS` (Tổ kiểm phiếu, người đầu là Tổ trưởng): `C_STT`, `C_NAME`, `C_TITLE`, `C_ROLE`; `repeat:SUPERIOR_ROWS` (mục I — thẩm quyền BTVĐUTCT) và `repeat:BASE_ROWS` (mục II — thẩm quyền Đảng ủy/Chi ủy cơ sở), cùng tag: `R_STT`, `R_NAME`, `R_POSITION`, `R_EXC`, `R_GOOD`, `R_SAT`, `R_UNSAT`, `R_NONE` (cột 8 "Chưa đánh giá, xếp loại"), `R_NOTE` — mục theo thẩm quyền ảnh chụp trên hồ sơ, thứ tự theo mã chức danh rồi họ tên; `R_POSITION` cùng quy tắc với `R_POSITION_DEPT` của Mẫu 11 |
| `MAU_09A` Phiếu tự chấm (theo sản phẩm Mẫu 01/02) | `PARTY_CELL`, `DATE_LINE`, `QUARTER` (số La Mã), `YEAR`, `FULL_NAME`, `PARTY_POSITION`, `ADMIN_POSITION`, `MASS_POSITION`, `DEPARTMENT`, `GENERAL_SCORE`, `TASKS_SCORE`, `TOTAL_SCORE`, `EXCEED_COUNT`, `TASK_COUNT`, `EXCEED_PERCENT`, `CHECK_EXCELLENT`, `CHECK_GOOD`, `CHECK_DONE`, `CHECK_FAILED` — `CHECK_*` là ô ☒/☐ của mức tự đề xuất; `EXCEED_PERCENT` là số nguyên (ký hiệu % in sẵn) | `repeat:GROUPS` (dòng nhóm): `G_NO`, `G_NAME`, `G_MAX`, `G_NOTE` (nhận xét chung của nhóm, ô cuối dòng nhóm), lồng `repeat:ITEMS` (dòng tiêu chí con): `I_CODE`, `I_TEXT`, `I_MET`, `I_NOT_MET`, `I_MAX`, `I_SCORE`, `I_NOTE` |
| `MAU_09C` Bản tự đánh giá, xếp loại của cá nhân | `PARTY_CELL`, `DATE_LINE`, `QUARTER` (số La Mã: "Quý III Năm 2026"), `YEAR`, `FULL_NAME` (in hoa), `DATE_OF_BIRTH` (dd/MM/yyyy; chưa nhập → dòng chấm), `PARTY_POSITION`, `ADMIN_POSITION`, `MASS_POSITION`, `DEPARTMENT` (tên phòng; dòng "Đơn vị công tác: `DEPARTMENT`, `ORG_COMPANY_NAME`."; cụm "`DEPARTMENT`, " nằm trong `if:HAS_DEPARTMENT` — hồ sơ thuộc đơn vị gốc của cây, tức lãnh đạo Công ty, chỉ in tên công ty), `GENERAL_SCORE`, `TASKS_SCORE`, `TOTAL_SCORE` (số nguyên in gọn "30/30", số lẻ theo làm tròn "29,5/30"), `SELF_GRADE`, `SIGNER_NAME` (họ tên chữ thường dưới "(Ký, ghi rõ họ tên)") | `repeat:SECTIONS` (khối đoạn văn, mỗi mục khai báo trong bộ tiêu chí): `S_TITLE`, `if:S_HAS_INTRO` bọc đoạn `S_INTRO` (nhận xét chung đã nhập **thay** câu dẫn; chưa nhập thì câu dẫn của mục), `if:S_SHOW_DOTS` (dòng chấm viết tay — chỉ khi mục chưa nhập nhận xét và không có nhiệm vụ theo trục), `repeat:S_AXES` (chỉ mục đầu tiên, mỗi trục có nhiệm vụ): `AX_TITLE` (đoạn đậm "n. Tên trục (trục n)", không thụt đầu dòng), lồng `repeat:AX_LINES`: mỗi nhiệm vụ / ý con "+" một đoạn gồm `L_TEXT` ("- Nhiệm vụ k: … .") và `L_LEVEL` (câu nối mức, in nghiêng; rỗng khi không chọn mức) — lưu ý hướng dẫn viết (chữ đỏ của biểu mẫu gốc) không in, chỉ hiện trên màn hình nhập |
| `MAU_9D` Phụ lục kết quả thực hiện nhiệm vụ trong quý (ORG: `ORG_PARTY_NAME`, `ORG_COMPANY_NAME`, `ORG_LOCATION`; dòng địa danh – ngày `DATE_LINE` chỉ in khi kiểu trình bày bật — xem "Kiểu trình bày Mẫu 09C, 9D") | `PARTY_CELL`, `QUARTER` (số La Mã: "QUÝ III"), `YEAR`, `FULL_NAME`, `PARTY_POSITION`, `ADMIN_POSITION`, `MASS_POSITION` (3 dòng "Chức vụ Đảng / chính quyền / đoàn thể"; không giữ chức vụ nào trong danh mục → dòng chính quyền là chức danh trên hồ sơ), `DEPARTMENT` (in "`DEPARTMENT`, `ORG_COMPANY_NAME`"; `if:HAS_DEPARTMENT` như Mẫu 09C), `CELL_NAME` (tên Chi bộ bỏ chữ "Chi bộ" đứng đầu: "Chi bộ: Kỹ thuật" như phiếu đã nộp) | `repeat:AXES` (đúng một dòng "Trục n" mỗi trục của bộ tiêu chí, như biểu mẫu gốc): `A_LABEL`, `A_NAME`, `A_DEADLINE`, `A_STATUS`, `A_PRODUCT`, `A_PROGRESS`, `A_NOTE` |
| `MAU_07` Báo cáo tự đánh giá, xếp loại chất lượng của tập thể Đảng ủy (Chi ủy, Chi bộ) | `PARTY_PARENT`, `PARTY_ORG`, `DATE_LINE` (lần cập nhật hồ sơ tập thể, chưa sửa → ngày tạo), `SUBJECT_NAME_UPPER`, `QUARTER`, `YEAR`, `UNIT_NAME`, `SUBJECT_NAME`, `STRENGTH_1`…`STRENGTH_4` (mục A.I.1–4, mã I.1–I.4), `LIMITATIONS`, `CAUSES` (A.II), `PREVIOUS_REMEDIATION` (A.III), `EXPLANATION` (A.IV), `RESPONSIBILITIES` (A.V), `REMEDIATION_PLAN` (A.VI), `GENERAL_SCORE`, `TASK_SCORE`, `TOTAL_SCORE` (B: bộ tiêu chí của kỳ có khung tiêu chí tập thể thì là tổng điểm các tiêu chí của khung theo nhóm chung/nhiệm vụ, máy chủ tự cộng từ điểm từng tiêu chí; bộ không có khung thì nhập thẳng hai điểm nhóm) — nội dung mỗi mục nằm ở đoạn ngay dưới tiêu đề mục in sẵn; để trống giữ dòng chấm | — |
| `MAU_08` Báo cáo tổng hợp kết quả thực hiện các nhiệm vụ của cơ quan, đơn vị | `PARTY_PARENT`, `PARTY_ORG`, `DATE_LINE` (như Mẫu 07), `QUARTER`, `YEAR` | `repeat:ROWS` (luôn 13 dòng theo nhóm nội dung 1–13 của biểu mẫu, tên nhóm nguyên văn trong `Hd03FormCatalog`): `R_STT`, `R_TITLE`, `R_TASKS` (các dòng "- nhiệm vụ" dưới tên nhóm; nhóm 1–7 chưa nhập giữ "- Nhiệm vụ 1: …"), `R_PLAN`, `R_RESULT`, `R_ISSUES`, `R_NOTES` |
| `MAU_12` Biên bản hội nghị | `PARTY_PARENT`, `PARTY_ORG`, `MEETING_NAME`, `PERIOD_TEXT`, `WORKING_RULES`, `ORGANIZER`, `PURPOSE`, `VOTE_PURPOSE`, `START_TIME`, `START_DATE`, `END_TIME`, `LOCATION`, `INVITED`, `PRESENT`, `ABSENT`, `CHAIR_NAME`, `CHAIR_TITLE`, `SECRETARY_NAME`, `SECRETARY_TITLE`, `REPORTING_UNIT`, `ARCHIVE_UNIT`, `SECRETARY_SIGN`, `CHAIR_SIGN`, `CHAIR_SIGN_TITLE` (chức danh dưới "CHỦ TRÌ HỘI NGHỊ" = chức vụ chủ trì đã lưu, in hoa; B3a chưa ghi chức vụ → để trống, B4 → giữ "BÍ THƯ"), `CHAIR_SIGN_NOTE` (B3a: "(ký, ghi rõ họ tên)", B4 giữ "… đóng dấu"), `ifnot:HAS_ATTENDEES` (dòng "…" của mục 3.2), `if:HAS_CONTENT` + `CONTENT` (diễn biến, kết quả hội nghị) — `MEETING_NAME`/`ORGANIZER`/`PURPOSE` theo bước: B3a "Hội nghị tập thể lãnh đạo, quản lý …" (đề xuất), B4 "Hội nghị &lt;Đảng ủy/Chi ủy&gt;" (quyết định, phê duyệt); giờ theo giờ Việt Nam | `repeat:ATTENDEES` (đoạn văn mục 3.2): `A_STT`, `A_NAME`, `A_TITLE` |
| `MAU_16` Báo cáo về kết quả đánh giá, xếp loại chất lượng cán bộ quý | `PARTY_PARENT`, `PARTY_ORG`, `DATE_LINE` (lần lưu bản nháp), `DOC_NUMBER`, `QUARTER`, `YEAR`, `RECIPIENT`, `WORKING_RULES`, `MEETING_DATE`, `ORGANIZER`, `PROPOSER`, `PROPOSAL_1` (khối 3 đoạn mẫu của mục III.1), `PROPOSAL_2`, `PROPOSAL_3`, `SIGNER_NAME` — phần nhập tay lấy từ bản nháp (kỳ + tổ chức Đảng); trường trống giữ chữ mẫu | `repeat:BASE_ROWS` (mục I, thẩm quyền đảng ủy/chi ủy cơ sở): `B_STT`, `B_SUBJECT`, `B_TOTAL`, `B_EXC`, `B_GOOD`, `B_SAT`, `B_UNSAT`, `B_NONE`, `B_PCT`, `B_NOTE`; `repeat:SUPERIOR_ROWS` (mục II, thẩm quyền BTV Đảng ủy Tổng công ty): `S_…` cùng cột — mỗi dòng một nhóm chức danh (mã M1–M26), dòng cuối "Tổng cộng" |
| `MAU_QD` Quyết định xếp loại cán bộ quý của Đảng ủy (Chi ủy) kèm Phụ lục (trang 1 Quyết định khổ đứng, trang 2 Phụ lục khổ ngang; ORG: `ORG_LOCATION`, `ORG_PARENT_COMPANY_NAME` — "Thường vụ Đảng ủy &lt;đơn vị chủ quản&gt;" ở căn cứ Hướng dẫn) | `PARTY_PARENT`, `PARTY_ORG` (như Mẫu 16, dùng ở cả hai trang), `DOC_NUMBER` ("Số …-QĐ/…": số văn bản quyết định khi mọi hồ sơ ghi cùng một số), `DATE_LINE` (ngày văn bản quyết định chung), `QUARTER` (La Mã), `YEAR`, `COMMITTEE` ("Đảng ủy &lt;…&gt;" — tổ chức "Đảng bộ X" → "Đảng ủy X", Chi bộ giữ tên), `COMMITTEE_UPPER` ("… QUYẾT ĐỊNH", tiêu đề Phụ lục), `EXECUTIVE` ("Ban Chấp hành Đảng bộ X"; Chi bộ giữ tên — căn cứ quy chế, diện đánh giá, cột 8 Phụ lục), `TERM` (nhiệm kỳ 5 năm tính từ 2025; năm đại hội 2030, 2035… giữ dòng chấm), `MEETING_DATE` (ngày hội nghị quyết định B4), `SUPERIOR_COMMITTEE` (Nơi nhận "- BTV &lt;cấp ủy cấp trên&gt; (để b/c)"), `SIGN_ON_BEHALF` ("T/M ĐẢNG ỦY" / "T/M CHI BỘ"), `SIGNER_NAME` (người chủ trì hội nghị quyết định; không có → trống), `DECISION_REF` ("(kèm theo Quyết định số … ngày …)") | `repeat:ROWS` (Phụ lục, mỗi cán bộ một dòng): `R_STT`, `R_NAME`, `R_POSITION` (chức vụ đang giữ — đơn vị, như Mẫu 14), `R_SELF`, `R_COLLECTIVE`, `R_APPRAISAL`, `R_DIRECTOR` (bước chưa có mức → trống), `R_FINAL` (mức quyết định), `R_NOTE` |
| `MAU_17` Kế hoạch hỗ trợ, khắc phục 30-60-90 ngày (task 20; file mẫu dựng từ đúng phần Mẫu 17 của biểu mẫu gốc; ORG: `ORG_PARENT_COMPANY_NAME_UPPER`, `ORG_COMPANY_NAME_UPPER`, `ORG_LOCATION`) | `DATE_LINE` (ngày duyệt kế hoạch), `FULL_NAME`, `POSITION`, `DEPARTMENT`, `GRADE_LEVEL`, `SUPPORTER_NAME`, `SUPPORTER_TITLE`, `M30_/M60_/M90_` + `LIMITATION`, `TARGET`, `MEASURES`, `COORDINATION`, `ACHIEVED_BOX`, `NOT_ACHIEVED_BOX` (ô ☐ in sẵn → ☒ khi chọn), `APPROVER_NAME`, `SUPPORTER_SIGN_NAME`, `MEMBER_SIGN_NAME` (chỉ khi cá nhân đã xác nhận) | — |

Trình bày bản in (khác biểu mẫu gốc, áp dụng mọi mẫu): chữ tô đỏ của biểu mẫu gốc in màu đen; tên Đảng bộ dài ở cột tiêu đề trái (`PARTY_PARENT`, `PARTY_ORG`) ngắt thành hai dòng cân nhau; ô bảng hẹp không căn đều hai bên; dòng có ngắt dòng trong đoạn căn đều không giãn chữ; khối chữ ký không tách trang và đi liền đoạn nội dung ngay trước; Mẫu 01 đặt dòng "Lưu ý" trước phần chữ ký; Mẫu 12, 13 có cột tiêu đề đúng khổ giấy và họ tên người ký thẳng hàng.

Mẫu 09A/09C/9D: tiêu đề trái "ĐẢNG BỘ …" dùng `ORG_PARTY_NAME`, "CHI BỘ …" là `PARTY_CELL` (tên Chi bộ ảnh chụp trên hồ sơ, in
hoa); điểm để trống (giữ dòng chấm) khi hồ sơ chưa nộp phiếu tự chấm. Template dựng từ file biểu mẫu gốc
`docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx` (cắt đúng phần của mẫu, giữ nguyên bố cục, chỉ thêm Content Control).

Template Mẫu 07, 08, 12, 13, 16 được dựng **từ đúng biểu mẫu gốc** `docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx`:
cắt nguyên phần thân của mẫu (bảng tiêu đề, đoạn văn, bảng, chữ ký, khổ giấy của section), gắn Content Control bao đúng đoạn chữ
mặc định ("…", "……") trong đoạn gốc (giữ định dạng), bỏ màu đỏ đánh dấu soạn thảo. Mẫu 13 (đợt 8) dựng bằng script
`tools/templates/build_mau13.py` (`py -3 tools/templates/build_mau13.py <file biểu mẫu gốc> <template đích>`, cần `lxml`): so khớp
nguyên văn, sai là dừng; chạy lại cho ra cùng template. `tools/templates/fix_mau12_period.py` tách dấu chấm câu khỏi chỗ điền quý
của Mẫu 12 (chạy lại an toàn). `tools/templates/fix_form_tags.py` (`py -3 tools/templates/fix_form_tags.py <thư mục Templates/Word>`,
chạy sau `build_mau13.py`, chạy lại an toàn) bổ sung control `DATE_LINE` (phần "ngày … tháng … năm …" sau `ORG_LOCATION`),
`CHAIR_SIGN_TITLE`/`CHAIR_SIGN_NOTE` (Mẫu 12, 13), `BASE_AUTHORITY` (Mẫu 13), tách ngắt dòng khỏi `ORG_COMPANY_NAME_UPPER` (Mẫu 17)
và sửa "Mẫu số10", dòng chấm sau `DEPARTMENT` (Mẫu 10). `tools/templates/build_axis_reports.py` (`py -3 tools/templates/build_axis_reports.py <thư mục Templates/Word>`,
chạy lại an toàn) dựng phần theo trục của Mẫu 9D (một dòng mỗi trục) và Mẫu 09C (khối `S_AXES`/`AX_LINES`, bỏ đoạn lưu ý `S_NOTE`);
`tools/templates/fix_mau10_layout.py` (cùng cách chạy) trình bày gọn Mẫu 10 (bỏ "Kỳ đánh giá" lặp, dòng chấm vào control, ô ☒ mức đề xuất); ô `G_NOTE` ở dòng nhóm Mẫu 09A đã có sẵn trong template; sau đó `tools/templates/fix_mau09c_layout.py` (cùng cách chạy) trình bày Mẫu 09C theo bản tự đánh giá đã nộp trong `docs/tai-lieu-chuan/`
(tiêu đề màu đen, `DATE_OF_BIRTH`, `ORG_COMPANY_NAME` ở dòng đơn vị công tác, `if:S_HAS_INTRO`/`if:S_SHOW_DOTS`, `L_LEVEL` nghiêng, `SIGNER_NAME`). `tools/templates/fix_mau9d_layout.py` (cùng cách chạy, sau `build_axis_reports.py`) sửa bố cục Mẫu 9D theo bản chuẩn: "Mẫu số 09D", bảng tiêu đề 2 cột, bỏ dòng địa danh – ngày và dấu "*", chức vụ 3 dòng, đơn vị kèm tên công ty, lề trang và số trang đầu trang. Sau cùng `tools/templates/fix_department_company.py` (cùng cách chạy) bọc cụm "`DEPARTMENT`, " của Mẫu 09C, 9D trong `if:HAS_DEPARTMENT`; rồi `tools/templates/fix_form_layout.py` (cùng cách chạy) thêm các khối theo kiểu trình bày: chức vụ một dòng (`if:POSITIONS_ONE_LINE` + `POSITIONS`, ba dòng cũ trong `ifnot:POSITIONS_ONE_LINE`) trên 9D; dòng `ORG_LOCATION`, `DATE_LINE` dưới quốc hiệu Mẫu 9D trong `if:HAS_DATE_LINE`; giữ phần ký Mẫu 9D trên cùng một trang (dòng tiêu đề ký `keepNext`). `tools/templates/fix_mau11_mau13_sections.cs` (`dotnet run tools/templates/fix_mau11_mau13_sections.cs -- <thư mục Templates/Word>`, sau `build_mau13.py` và `fix_form_tags.py`, chạy lại an toàn) chia bảng Mẫu 11 thành mục I/II (`repeat:SUPERIOR_ROWS`/`repeat:BASE_ROWS`, thêm control cột (5)–(7) và `BASE_AUTHORITY`) và bọc mỗi mục của Mẫu 11, 13 trong `if:SHOW_SUPERIOR`/`if:SHOW_BASE`. Tên tổ chức chèn **giữa câu** (`ORGANIZER`, `ARCHIVE_UNIT`, `UNIT_NAME`,
`PROPOSER`…) dùng dạng thường: tên Đảng bộ trong cài đặt viết HOA được viết lại theo tên công ty
(`CollectiveFormText.NameInSentence`: "ĐẢNG BỘ CÔNG TY TNHH A" → "Đảng bộ Công ty TNHH A"); chỉ dòng tiêu đề in hoa. Số, ngày
"Hướng dẫn số ……-HD/TVĐU ngày …" (Mẫu 07, 12, 13, 16) chưa có trong cài đặt đơn vị nên giữ dòng chấm. Mẫu 08 (bản Excel), 14, 15A, 15B là bảng tính (HD03 V.1: "lập trên file excel (trừ các Mẫu: 07, 09C, 12, 13, 16)") dựng
bằng ClosedXML theo đúng cột/tiêu đề biểu mẫu (PDF tr.48–49, 72–76).

Template Quyết định xếp loại (`Mau_QD_QuyetDinhXepLoai.docx`) không có trong biểu mẫu gốc HD03: dựng theo hai trang tính "Quyết định
xếp loại" và "Phụ lục kèm theo Quyết định" của bảng tính Công ty đã dùng (`docs/tai-lieu-chuan/`) — gói tài liệu (styles, bảng
tiêu đề hai cột có gạch dưới) lấy từ Mẫu 16, căn cứ giữ nguyên văn, chỗ số/ngày Hướng dẫn của cấp trên giữ dòng chấm; Nơi nhận
dùng chữ viết tắt chung ("Các đ/c UVBCH", "UBKT ĐU", "Lưu ĐU") — sửa được bằng cách tải lên phiên bản file mẫu mới. Phụ lục
sắp xếp như Mẫu 14 (theo họ tên), chức vụ in như Mẫu 14 (chức vụ đang giữ — đơn vị).

### Toàn bộ biểu mẫu HD03: nơi nhập → nơi xuất → định dạng

Cột "Áp dụng" theo bộ tiêu chí mặc định (áp dụng đầy đủ mọi biểu mẫu) và `docs/nghiep-vu/hd03-trich-xuat.md` mục 7. Mẫu cá nhân áp dụng cho hồ sơ do **bộ tiêu chí của kỳ** quyết định
(`requiredForms`; tập thể `requiredCollectiveForms`; tổng hợp `requiredSummaryForms` — sửa ở trang Bộ tiêu chí). Mẫu khai báo mà chưa có chức năng xuất file chỉ chưa hiện nút xuất, không phải lỗi cấu hình. PDF: thêm `?format=pdf` vào endpoint (máy chủ chuyển bằng LibreOffice).

| Mẫu | Áp dụng | Nơi nhập dữ liệu | Nơi xuất (trang → endpoint) | Định dạng | Quyền / phạm vi |
|---|---|---|---|---|---|
| 01 — Phiếu giao / đăng ký sản phẩm | Áp dụng | Hồ sơ cá nhân — bước B1 (đăng ký nhiệm vụ) | Hồ sơ → "Biểu mẫu của hồ sơ": `GET /api/reports/docx/record/{recordId}/01` | Word/PDF | xem hồ sơ (`evaluation.read` trong phạm vi, chủ hồ sơ) |
| 02 — Phiếu tự đánh giá sản phẩm | Áp dụng | Hồ sơ cá nhân — bước B2 (tự chấm nhiệm vụ) | Hồ sơ: `…/record/{recordId}/02` | Word/PDF | như Mẫu 01 |
| 03 — Phiếu ý kiến chuyên môn độc lập / đối soát dữ liệu chuyên ngành | Áp dụng (khi cơ quan thẩm định yêu cầu) | Khai báo trong bộ tiêu chí; phần nhập và xuất file đang bổ sung | — | — | — |
| 06 — Báo cáo kết quả tập thể / lĩnh vực | Áp dụng | Khai báo trong bộ tiêu chí; phần nhập và xuất file đang bổ sung | — | — | — |
| 04 — Đề nghị điều chỉnh, chuyển tiếp, loại trừ / ngoại lệ sản phẩm | Áp dụng (khi có đề nghị) | Hồ sơ cá nhân — khối "Điều chỉnh việc trong quý" (sau duyệt danh sách việc, trước tự chấm): chủ hồ sơ đề nghị, người duyệt danh sách việc duyệt (`/api/evaluations/records/{recordId}/task-changes`) | — (chưa có file mẫu Word; nội dung xem trên trang hồ sơ và lịch sử) | — | đề nghị: `evaluation.self`; duyệt: quyền bước duyệt danh sách việc theo cách xét duyệt |
| 05 — Ghi nhận nhiệm vụ bổ sung, khẩn cấp, đột xuất | Áp dụng (khi có việc đột xuất) | Hồ sơ cá nhân — "Ghi nhận việc đột xuất" (chủ hồ sơ hoặc người duyệt danh sách việc); duyệt như Mẫu 04 → thêm việc loại đột xuất | — (chưa có file mẫu Word) | — | như Mẫu 04 |
| 07 — Báo cáo tự đánh giá của tập thể | Áp dụng | Tập thể & Hội nghị → hồ sơ tập thể M07 (mục I.1–I.4, II–VI, điểm B) | Tập thể & Hội nghị (từng hồ sơ): `GET /api/reports/docx/mau-07/{collectiveRecordId}` | Word/PDF | `evaluation.read` hoặc `collective.manage` trên tổ chức của hồ sơ |
| 08 — Tổng hợp kết quả nhiệm vụ của cơ quan, đơn vị | Áp dụng | Tập thể & Hội nghị → hồ sơ tập thể M08 (dòng nhiệm vụ theo nhóm nội dung 1–13) | Tập thể & Hội nghị: **Excel** `GET /api/reports/form-08/{collectiveRecordId}` (HD03 V.1); Word `GET /api/reports/docx/mau-08/{collectiveRecordId}` | Excel/Word/PDF | như Mẫu 07 |
| 09A — Phiếu tự chấm | Áp dụng (luôn) | Hồ sơ — bước B2 (phiếu tự chấm: tiêu chí chung + điểm nhiệm vụ theo Mẫu 02) | Hồ sơ: `…/record/{recordId}/09A` | Word/PDF | như Mẫu 01 |
| 09C — Bản tự đánh giá của cá nhân | Áp dụng | Hồ sơ — bước B2 (nhận xét chung; nhiệm vụ theo trục kèm mức, ý con) | Hồ sơ: `…/record/{recordId}/09C` | Word/PDF | như Mẫu 01 |
| 9D — Phụ lục kết quả nhiệm vụ | Áp dụng | Hồ sơ — bước B2 (dòng tóm tắt mỗi trục) | Hồ sơ: `…/record/{recordId}/9D` | Word/PDF | như Mẫu 01 |
| 10 — Phiếu thẩm định | Áp dụng | Hồ sơ — bước B3b (thẩm định, giải trình chênh lệch) | Hồ sơ: `…/record/{recordId}/10` | Word/PDF | như Mẫu 01 |
| 11 — Phiếu đánh giá, xếp loại (phiếu kín) | Áp dụng | — (phiếu trắng theo danh sách hồ sơ) | Báo cáo: `GET /api/reports/docx/mau-11?periodId&branchId` | Word/PDF | `meeting.read` hoặc `report.export` (phạm vi tổ chức Đảng) |
| 12 — Biên bản hội nghị | Áp dụng | Tập thể & Hội nghị → biên bản M12 (thời gian, thành phần, mục 3.2, chủ trì/thư ký, nội dung) | Tập thể & Hội nghị (từng biên bản M12): `GET /api/reports/docx/mau-12/{meetingId}` | Word/PDF | `meeting.read`/`meeting.manage` trên đơn vị của biên bản |
| 13 — Biên bản kiểm phiếu | Áp dụng | Tập thể & Hội nghị → biên bản (Tổ kiểm phiếu, số phiếu phát ra/thu về/hợp lệ/không hợp lệ) + kết quả kiểm phiếu từng hồ sơ (lập biên bản M13 hoặc ghi khi ghi nhận B3a/B4, gồm cột "Chưa đánh giá, xếp loại") | Tập thể & Hội nghị (biên bản đã có kết quả kiểm phiếu): `GET /api/reports/docx/mau-13/{meetingId}` — chưa có kết quả → 400 | Word/PDF | như Mẫu 12 |
| 14 — Danh sách đánh giá và đề xuất xếp loại | Áp dụng | Dữ liệu các bước của hồ sơ; cột 13 "Đề xuất nội dung liên quan về công tác cán bộ" nhập ở bước B4 (quyết định nội bộ hoặc ghi nhận cấp trên) | Báo cáo: `GET /api/reports/form-14?periodId&branchId` (mỗi cấp quyết định một trang tính) | Excel/PDF | `report.export` (phạm vi tổ chức Đảng) |
| 15A, 15B — Tổng hợp theo mã chức danh | Áp dụng | Tự động từ mức của hồ sơ và mã chức danh | Báo cáo: `GET /api/reports/form-15a`, `form-15b` `?periodId&branchId` | Excel/PDF | `report.export` |
| 16 — Báo cáo kết quả đánh giá, xếp loại | Áp dụng | Số liệu tự động; phần nhập tay lưu nháp theo kỳ + tổ chức Đảng (`GET/PUT /api/reports/mau-16/draft`) | Báo cáo ("Soạn báo cáo"): `GET /api/reports/docx/mau-16?periodId&branchId` | Word/PDF | `report.export` |
| Quyết định xếp loại kèm Phụ lục (văn bản của Đảng ủy/Chi ủy cơ sở, không mang số mẫu HD03) | Áp dụng | Tự động: hồ sơ của kỳ trong phạm vi thuộc diện tổ chức Đảng cơ sở quyết định (thẩm quyền ảnh chụp "cơ sở" và bước quyết định của cách xét duyệt thực hiện trong hệ thống), mức từng bước, mức quyết định; số/ngày văn bản và hội nghị quyết định ghi ở bước B4 | Báo cáo (dòng "Quyết định", cạnh Mẫu 16): `GET /api/reports/docx/decision?periodId&branchId` — không có hồ sơ diện cơ sở hoặc còn hồ sơ chưa ghi quyết định → 400 (nêu tên) | Word/PDF | `report.export` (phạm vi tổ chức Đảng như Mẫu 16) |
| 17 — Kế hoạch hỗ trợ, khắc phục 30-60-90 ngày | Áp dụng (bắt buộc theo mức trong bộ tiêu chí, mặc định C, D) | Hồ sơ đã công bố → khối "Kế hoạch 30-60-90 ngày" (lập, duyệt, xác nhận, kết quả mốc) | Hồ sơ: `GET /api/improvement-plans/{id}/mau-17` | Word/PDF | `evaluation.improvement.manage` hoặc xem hồ sơ |
| 18 — Bảng kiểm hồ sơ, quyền truy cập và quản lý phiên bản | Lập ngoài hệ thống | — | — | — | — |
| 19 — Phiếu kiểm soát tính hợp lệ, công bằng và nhất quán | Áp dụng (trước khi trình phê duyệt) | Khai báo trong bộ tiêu chí; phần nhập và xuất file đang bổ sung | — | — | — |
| 20 — Biên bản hiệu chuẩn kết quả đánh giá | Áp dụng (nhóm chức danh tương đồng, trước khi phê duyệt) | Khai báo trong bộ tiêu chí; phần nhập và xuất file đang bổ sung | — | — | — |
| Hồ sơ kiến nghị (V.1 "hồ sơ giải quyết khiếu nại, kiến nghị") | — | Hồ sơ đã công bố → khối "Kiến nghị" (gửi, xem xét, trả lời có căn cứ, tệp gắn) | Trên trang hồ sơ (không có biểu mẫu HD03 riêng) | — | chủ hồ sơ / `evaluation.appeal.resolve` |
| Báo cáo nội bộ — Danh sách cán bộ | — | Danh mục cán bộ | Báo cáo (nhóm nội bộ): `GET /api/reports/cadres` | Excel | `report.export` (lọc theo phạm vi) |
| Báo cáo nội bộ — Kiểm soát tỷ lệ Hoàn thành xuất sắc | — | Tự động | Báo cáo (nhóm nội bộ): `GET /api/reports/internal/excellent-quota?periodId&branchId` | Excel/PDF | `report.export` |
| Báo cáo nội bộ — Bảng tổng hợp xếp loại cán bộ (2 khối: diện BTV Đảng ủy TCT / diện BCH Đảng bộ Công ty theo thẩm quyền phê duyệt của hồ sơ; 13 cột: STT, mã hiệu M1–M26 (kiêm nhiệm lấy mã đứng trước), họ tên, chức vụ, điểm tiêu chí chung, điểm kết quả nhiệm vụ, tổng điểm tự chấm, mức tự đề xuất, mức tập thể, mức thẩm định, mức cấp trực tiếp sử dụng, mức quyết định, số tệp đính kèm) | — | Dữ liệu các bước của hồ sơ (bước cấp trên thực hiện: kết quả ghi nhận) | Báo cáo (nhóm nội bộ) và nút trên trang chi tiết kỳ: `GET /api/reports/internal/rating-summary?periodId&branchId` | Excel/PDF | `report.export` (lọc theo phạm vi Toàn công ty / đơn vị / tổ chức Đảng; `branchId` phải trong phạm vi) |

Đã bỏ: `GET /api/reports/form-15` (thống kê/kiểm soát trần — không có trong HD03, nay là báo cáo nội bộ ở trên),
`GET /api/reports/form-16` (Excel — Mẫu 16 HD03 là báo cáo Word) và `GET /api/reports/docx/mau-13?periodId&branchId` (Mẫu 13 nay
xuất theo từng biên bản) → 404. Báo cáo theo kỳ không truyền `branchId`: phạm vi Toàn công ty → toàn Đảng bộ; phạm vi đúng một tổ
chức Đảng → tổ chức đó; nhiều tổ chức → 400 yêu cầu chọn.

Số liệu Mẫu 15A/15B/16: mỗi cán bộ thống kê một lần theo mã chức danh nhỏ nhất đang giữ (HD03 tr.74); mức dùng để đếm là mức
quyết định, chưa có thì mức đề xuất gần nhất (cấp trực tiếp sử dụng → thẩm định → tập thể lãnh đạo), không tính mức tự đề
xuất; chưa có mức nào → "Chưa xếp loại". Mẫu 16 chia mục I/II theo thẩm quyền (ảnh chụp trên hồ sơ), Mẫu 15A/15B chia theo
khoảng mã chức danh (bố cục biểu mẫu); cán bộ chưa có mã: Mẫu 15A/15B liệt kê ở trang tính "Kiểm tra dữ liệu", Mẫu 16 ghi
dòng "Cán bộ chưa có mã chức danh thống kê".



### Kiểu trình bày Mẫu 09C, 9D

Các bản tự đánh giá đã nộp trình bày khác nhau theo người (bộ của Trưởng phòng, Giám đốc, Chủ tịch trong `docs/tai-lieu-chuan/`).
Cách in nằm trong danh mục **Kiểu trình bày biểu mẫu** (bảng `form_layouts`, *Danh mục → Kiểu trình bày biểu mẫu*), không ghi cứng:

| Thiết lập | Tác dụng | Tag |
|---|---|---|
| Chức vụ một dòng (9D) | "Chức vụ (Đảng, Đoàn thể, Chính quyền): …" thay 3 dòng | `if:POSITIONS_ONE_LINE`, `POSITIONS` |
| Dòng địa danh – ngày (9D) | Dưới "ĐẢNG CỘNG SẢN VIỆT NAM" | `if:HAS_DATE_LINE`, `DATE_LINE` |
| Chức vụ trống ghi là | Chữ in ở dòng chức vụ không có (ví dụ "không"); trống → dòng chấm | `PARTY_POSITION`, `ADMIN_POSITION`, `MASS_POSITION` |

Kiểu áp dụng cho cán bộ: kiểu đặt riêng (hồ sơ người dùng → Chức vụ) → kiểu của các chức vụ đang giữ (cùng thời điểm với chức vụ
in trên phiếu; nhiều kiểu thì kiểu có thứ tự nhỏ hơn) → kiểu mặc định → không có kiểu nào thì in như template. Giá trị khởi tạo
(tạo khi danh mục trống): "Bí thư Đảng ủy" (gắn chức vụ Bí thư Đảng ủy), "Lãnh đạo Công ty" (Chủ tịch Công ty, Giám đốc, Phó Giám
đốc), "Cán bộ phòng ban" (mặc định, như biểu mẫu gốc). Kiểu đặt riêng cũng nhập được bằng cột **Kiểu trình bày biểu mẫu** của
file nhập cán bộ (tên kiểu; "Theo chức vụ" = bỏ kiểu đặt riêng; trống = giữ nguyên).

Phiên bản file mẫu tải lên **trước** khi file gốc có các tag trên vẫn xuất được nhưng không đổi theo kiểu trình bày; trang
*Biểu mẫu Word* báo "Thiếu n tag so với file gốc" cho phiên bản đang dùng đó — tải file gốc, sửa lại rồi tải lên, hoặc **Dùng file gốc**.

## 7. Thay file mẫu trên giao diện (quyền `system.templates.manage`)

Trang **Quản trị → Biểu mẫu Word** liệt kê từng biểu mẫu (mã, tên, phiên bản đang dùng, người/lúc cập nhật):

- **File đang dùng** / **File gốc**: tải về để sửa. Nên sửa từ *file đang dùng*.
- **Tải lên**: chọn `.docx` → *Kiểm tra* (không lưu) → *Tải lên* (mặc định kích hoạt ngay; bỏ chọn để lưu mà chưa dùng).
- **Lịch sử**: mọi phiên bản (tệp, người tải, ghi chú, cảnh báo lúc tải), tải từng phiên bản, **Kích hoạt** lại phiên bản cũ.
- **Dùng file gốc**: bỏ kích hoạt mọi phiên bản, xuất theo file gốc đi kèm ứng dụng.
- Phiên bản đang dùng thiếu tag bắt buộc so với danh mục tag hiện tại (ví dụ file gốc được bổ sung tag sau khi tải bản đó lên) →
  cột phiên bản hiện "Thiếu n tag so với file gốc" (`activeMissingTags` trong `GET /api/templates`).

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

## 8. Biểu mẫu cá nhân theo bộ tiêu chí của kỳ (Mẫu 09A, 09C, 9D — task 18)

- **Mẫu áp dụng** khai báo trong nội dung bộ tiêu chí: `requiredForms` (mã `01`–`05`, `09A`, `09C`, `9D`, `10`, `11`, `17`), `requiredCollectiveForms` (`06`, `07`, `08`, `12`, `13`) và `requiredSummaryForms` (`14`, `15A`, `15B`, `16`, `19`, `20`) — sửa trên
  trang **Bộ tiêu chí** (bản nháp) ở khối "Biểu mẫu cá nhân áp dụng". Mẫu 09A (phiếu tự chấm) luôn áp dụng. Bộ mặc định:
  đủ cả ba danh sách trên. Nút xuất trên hồ sơ chỉ hiện với mẫu đã có file xuất (01, 02, 09A, 09C, 9D, 10).
- **Trục kết quả** của bộ (`axes[]`: mã, tên, nội dung áp dụng) dùng để phân loại nhiệm vụ: cột trục của Mẫu 01, nhóm nhiệm vụ
  theo trục ở Mẫu 09C, một dòng mỗi trục ở Mẫu 9D.
- **Mục Mẫu 09C** khai báo trong `selfAssessmentSections` (mã, tiêu đề, câu dẫn, lưu ý, số ký tự tối đa, bắt buộc); nội dung hồ sơ
  lưu jsonb theo mã mục (`evaluation_records.SelfAssessment`) — mục đầu tiên là phần nhận xét chung. **Nhiệm vụ và kết quả theo
  trục** nhập một lần, lưu jsonb theo mã trục (`evaluation_records.AxisReports`): `tasks` (nhiệm vụ: nội dung, `level`, `items` —
  ý con "+" có mức riêng; in ở mục I Mẫu 09C, lưu khi kỳ áp dụng 09C) và các cột tóm tắt `deadline`, `status`, `product`,
  `progress`, `note` (một dòng "Trục n" của Mẫu 9D, lưu khi kỳ áp dụng 9D). Nhận xét theo nhóm tiêu chí chung (ô "Tóm tắt kết quả/Tài liệu minh chứng" của dòng nhóm Mẫu 09A) lưu jsonb theo mã nhóm (`evaluation_records.GeneralGroupNotes`), nhập ở dòng nhóm của phiếu tự chấm; nội dung in của từng tiêu chí con khai báo ở bộ tiêu chí (`generalGroups[].items[].formText`, bộ mặc định = nguyên văn biểu mẫu gốc). Tất
  cả nhập cùng phiếu tự chấm (bước `B2_SELF_SCORE`); trường không gửi = giữ nội dung đã lưu. Giới hạn ký tự mục I (mặc định 6.000,
  khoảng 02 trang A4) tính cả nhận xét chung lẫn phần nhiệm vụ theo trục.
- **Mức tự đánh giá từng nhiệm vụ:** khai báo trong bộ tiêu chí — `taskLevels` (mã số nguyên + nhãn; bộ mặc định theo lưu ý cuối Mẫu 09C: mức 1, 2, 3), `taskLineLabel` (tiền tố, `{n}` = số thứ tự, mặc định "- Nhiệm vụ {n}:") và `taskLevelPhrase` (câu nối, `{level}` = mã mức, mặc định "Tự đánh giá mức {level}."); sửa ở trang Bộ tiêu chí (bản nháp). Mỗi nhiệm vụ và ý con có `level` (mã mức của bộ hoặc trống; máy chủ chỉ nhận mã có trong bộ). Khi xuất Mẫu 09C, mục đầu tiên in phần nhận xét chung (thay cho câu dẫn; chưa nhập thì in câu dẫn), rồi mỗi trục có nhiệm vụ một đoạn đậm "n. Tên trục (trục n)" và mỗi nhiệm vụ một đoạn "<tiền tố> nội dung. <câu nối mức>", ý con "+ nội dung. <câu nối mức>" (câu nối mức in nghiêng; bỏ nếu chưa chọn) — đúng cách trình bày của bản tự đánh giá mẫu trong `docs/tai-lieu-chuan/`. Màn hình tự chấm có xem trước mục I. Bộ không khai báo mức thì màn hình tự chấm không có ô chọn mức; bộ đã xuất bản trước đây muốn dùng phải nhân bản thành bản nháp mới.
- **API** (quyền xem hồ sơ — `evaluation.read` trong phạm vi, chủ hồ sơ luôn xem được; kiểm tra bằng guard trên hồ sơ):
  - `GET /api/reports/docx/record/{recordId}` — danh sách mẫu áp dụng (mã, tên).
  - `GET /api/reports/docx/record/{recordId}/{formCode}?format=docx|pdf` — xuất một mẫu; 01/02/10 dùng lại bộ xuất hiện có. Mẫu
    không áp dụng cho kỳ → 409; mã không có → 400. Tên tệp không dấu `Mau_<mã>_<Họ_tên>_Q<quý>-<năm>.docx`.
- Thay file mẫu qua **Quản trị → Biểu mẫu Word** như các mẫu khác (mã `MAU_09A`, `MAU_09C`, `MAU_9D`).
