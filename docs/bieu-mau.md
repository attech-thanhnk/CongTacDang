# Biểu mẫu Word/PDF — cách thêm và sửa một mẫu

Tài liệu dành cho người phát triển. Bộ sinh biểu mẫu nằm ở:

| Thành phần | Vị trí |
|---|---|
| Template `.docx` (soạn bằng Word) | `backend/src/CongTacDang.Infrastructure/Templates/Word/` |
| Lớp dữ liệu mẫu (mỗi mẫu một lớp) | `backend/src/CongTacDang.Infrastructure/Documents/Forms/MauXXData.cs` |
| Bộ điền template | `backend/src/CongTacDang.Infrastructure/Services/DocxTemplateEngine.cs` |
| Định dạng số, ngày, mức xếp loại | `backend/src/CongTacDang.Infrastructure/Documents/FormText.cs` |
| Đọc template | `backend/src/CongTacDang.Infrastructure/Documents/WordTemplateStore.cs` |
| Chuyển DOCX/XLSX → PDF | `backend/src/CongTacDang.Infrastructure/Documents/LibreOfficePdfConverter.cs` |
| Gọi xuất (dựng dữ liệu, điền, chuyển PDF) | `backend/src/CongTacDang.Infrastructure/Services/ReportService.cs` |
| Endpoint | `backend/src/CongTacDang.Api/Controllers/ExportReportController.cs` |

Nguyên tắc:

- **Template là tệp Word thật.** Không sinh phôi bằng code. Bố cục, chữ in sẵn (tên cơ quan, tiêu đề, dòng chấm, chữ ký…) nằm trong template, không nằm trong code.
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
6. Lưu dạng `.docx` vào `Templates/Word/`. Tệp được copy ra thư mục chạy ứng dụng khi build; có thể trỏ cấu hình `Documents:TemplatePath` sang thư mục khác để cập nhật template mà không build lại.

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
4. Kiểm thử: thêm mẫu vào `DocumentTemplateTests.AllForms` — test sẽ kiểm tra mọi Tag trong template đều có trong lớp dữ liệu (không thiếu, không thừa) và tài liệu xuất ra hợp lệ theo OpenXML.

## 5. Xuất PDF

- API: thêm `?format=pdf` vào endpoint xuất (Word và Excel). Mặc định `docx`/`xlsx`.
- Máy chủ chuyển bằng LibreOffice headless (`soffice --headless --convert-to pdf`), chạy hoàn toàn trong mạng nội bộ, có timeout và thư mục tạm riêng cho mỗi lần chuyển.
- Thiếu LibreOffice → API trả **503** kèm thông báo, xuất Word/Excel vẫn hoạt động. Cài đặt: xem `docs/deployment.md` mục “LibreOffice (xuất PDF)”.
- Cấu hình: `Documents:Pdf:SofficePath`, `Documents:Pdf:TimeoutSeconds`, `Documents:Pdf:MaxConcurrency`, `Documents:Pdf:WorkDirectory`.
