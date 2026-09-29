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
| Endpoint | `backend/src/CongTacDang.Api/Controllers/ExportReportController.cs` |

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
| `ORG_PARTY_NAME` | Tên Đảng bộ (ghi đúng như in) | — (Mẫu 11, 13 dùng tên Đảng bộ làm dòng tổ chức lập phiếu khi xuất toàn Đảng bộ, qua `PARTY_CELL`) |
| `ORG_SUPERIOR_PARTY_NAME` | Tên tổ chức Đảng cấp trên (ghi đúng như in) | Mẫu 11, 13 — dòng trên cùng tiêu đề trái |
| `ORG_COMPANY_NAME` / `ORG_COMPANY_NAME_UPPER` | Tên công ty / chữ in hoa | Mẫu 01, 02 — dòng cơ quan quản lý (`_UPPER`) |
| `ORG_PARENT_COMPANY_NAME` / `ORG_PARENT_COMPANY_NAME_UPPER` | Tên đơn vị chủ quản / chữ in hoa | Mẫu 10 — dòng cơ quan cấp trên (`_UPPER`) |
| `ORG_SHORT_NAME` | Tên viết tắt | — |
| `ORG_LOCATION` | Địa danh | Mẫu 01, 02, 10, 11 — phần `……` của dòng "……, ngày … tháng … năm …" |

Excel (Mẫu 14, 15, danh sách cán bộ) dùng cùng cài đặt: dòng tiêu đề trái (tổ chức Đảng cấp trên, tên Đảng bộ), dòng
"Địa danh, ngày … tháng … năm …", tiêu đề và tên tệp danh sách cán bộ (`DanhSach_CanBo_<tên viết tắt>.xlsx`).

### Danh mục tag từng mẫu

Nguồn chính xác là lớp dữ liệu (`Documents/Forms/MauXXData.cs`); trang **Quản trị → Biểu mẫu Word → Tag** hiển thị danh mục
sinh từ code nên luôn khớp phiên bản đang chạy. Tag trong khối lặp chỉ đặt bên trong control `repeat:` tương ứng. Mọi tag
dưới đây là **bắt buộc** (thiếu → cảnh báo khi tải lên); khối điều kiện chỉ cần một trong hai nhánh `if:`/`ifnot:`.

| Mẫu | Tag cấp tài liệu | Khối lặp và tag trong khối |
|---|---|---|
| `MAU_01` Phiếu giao / đăng ký sản phẩm | `DEPARTMENT`, `QUARTER`, `YEAR`, `FULL_NAME`, `POSITION`, `SUPERVISOR_NAME` | `repeat:TASKS`: `T_STT`, `T_NAME`, `T_CODE`, `T_AXIS`, `T_ROLE`, `T_WEIGHT`, `T_DEADLINE`, `T_STANDARD`, `T_EXCEED`, `T_EVIDENCE`, `T_SIGNER` |
| `MAU_02` Phiếu tự đánh giá | `DEPARTMENT`, `QUARTER`, `YEAR`, `FULL_NAME`, `POSITION` | `repeat:TASKS`: `T_STT`, `T_NAME`, `T_WEIGHT`, `T_A`, `T_B`, `T_C`, `T_D`, `T_RESULT_PCT`, `T_SCORE`, `T_EVIDENCE`, `if:T_IS_EXCEED` / `ifnot:T_IS_EXCEED` |
| `MAU_10` Phiếu thẩm định | `QUARTER`, `YEAR`, `FULL_NAME`, `POSITION`, `DEPARTMENT`, `GENERAL_SELF_SCORE`, `GENERAL_APPRAISAL_SCORE`, `GENERAL_DIFF`, `TASKS_SELF_SCORE`, `TASKS_APPRAISAL_SCORE`, `TASKS_DIFF`, `TOTAL_SELF_SCORE`, `TOTAL_APPRAISAL_SCORE`, `TOTAL_DIFF`, `SUPERVISOR_COMMENT`, `APPRAISAL_COMMENT`, `PROPOSED_GRADE` | — |
| `MAU_11` Phiếu đánh giá, xếp loại (bỏ phiếu) | `PARTY_CELL`, `PERIOD_QUARTER_YEAR` | `repeat:RECORDS`: `R_STT`, `R_NAME`, `R_POSITION_DEPT`, `R_GENERAL_SCORE`, `R_TASKS_SCORE`, `R_SELF_GRADE` |
| `MAU_13` Biên bản kiểm phiếu | `PARTY_CELL`, `PERIOD_QUARTER_YEAR`, `TOTAL_VOTERS`, `INVALID_BALLOTS` | `repeat:RECORDS`: `V_STT`, `V_NAME`, `V_POSITION_DEPT`, `V_EXC`, `V_GOOD`, `V_SAT`, `V_UNSAT`, `V_PCT` |

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
