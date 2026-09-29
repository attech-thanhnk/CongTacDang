namespace CongTacDang.Application.Imports;

/// <summary>Giới hạn chung của chức năng nhập dữ liệu.</summary>
public static class ImportLimits
{
    /// <summary>Dung lượng tệp tối đa (5 MB).</summary>
    public const long MaxFileBytes = 5L * 1024 * 1024;

    /// <summary>Số dòng dữ liệu tối đa trong một tệp.</summary>
    public const int MaxRows = 2000;

    /// <summary>Thời gian sống của phiên xem trước và của tệp kết quả.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Tên sheet dữ liệu trong file mẫu.</summary>
    public const string DataSheetName = "Dữ liệu";

    /// <summary>Tên sheet hướng dẫn trong file mẫu.</summary>
    public const string GuideSheetName = "Hướng dẫn";
}

/// <summary>Mô tả một cột của file mẫu.</summary>
/// <param name="Key">Khóa kỹ thuật (camelCase) — dùng làm khóa trong <c>data</c> của bảng xem trước.</param>
/// <param name="Header">Tiêu đề cột trong file Excel (khớp không phân biệt hoa thường, bỏ dấu <c>*</c> cuối).</param>
/// <param name="Required">Bắt buộc có giá trị.</param>
/// <param name="Description">Hướng dẫn nhập, hiển thị ở sheet "Hướng dẫn".</param>
/// <param name="AllowedValues">Danh sách giá trị hợp lệ (null = tự do) — file mẫu gắn data validation.</param>
/// <param name="Example">Giá trị ví dụ hiển thị ở sheet "Hướng dẫn".</param>
public sealed record ImportColumn(
    string Key,
    string Header,
    bool Required,
    string Description,
    IReadOnlyList<string>? AllowedValues = null,
    string? Example = null);

/// <summary>Một dòng dữ liệu thô đọc từ file (giá trị đã cắt khoảng trắng, ô trống = chuỗi rỗng).</summary>
public sealed class ImportSourceRow
{
    /// <summary>Khởi tạo dòng thô.</summary>
    public ImportSourceRow(int rowNumber, IReadOnlyDictionary<string, string> values)
    {
        RowNumber = rowNumber;
        Values = values;
    }

    /// <summary>Số dòng trong Excel (dòng tiêu đề là 1).</summary>
    public int RowNumber { get; }

    /// <summary>Giá trị theo <see cref="ImportColumn.Key"/>.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>Giá trị của cột (chuỗi rỗng nếu thiếu).</summary>
    public string Get(string key) => Values.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>Giá trị của cột hoặc null nếu ô trống.</summary>
    public string? GetOrNull(string key)
    {
        var value = Get(key);
        return value.Length == 0 ? null : value;
    }
}

/// <summary>Kết quả phân loại một dòng.</summary>
public enum ImportRowAction
{
    /// <summary>Dòng lỗi — chặn xác nhận cả file.</summary>
    Error = 0,

    /// <summary>Tạo mới.</summary>
    Create = 1,

    /// <summary>Cập nhật bản ghi đã có.</summary>
    Update = 2
}

/// <summary>Một dòng đã phân tích, mang dữ liệu có kiểu <typeparamref name="TRow"/>.</summary>
public sealed class ImportRow<TRow> where TRow : class
{
    private readonly List<string> _errors = new();

    /// <summary>Khởi tạo dòng.</summary>
    public ImportRow(ImportSourceRow source, TRow data)
    {
        Source = source;
        Data = data;
    }

    /// <summary>Dòng thô.</summary>
    public ImportSourceRow Source { get; }

    /// <summary>Số dòng trong Excel.</summary>
    public int RowNumber => Source.RowNumber;

    /// <summary>Dữ liệu có kiểu (định nghĩa import có thể ghi thêm tham chiếu đã phân giải khi kiểm tra).</summary>
    public TRow Data { get; }

    /// <summary>Hành động dự kiến — định nghĩa import đặt trong <c>ValidateAsync</c>; có lỗi thì luôn là <see cref="ImportRowAction.Error"/>.</summary>
    public ImportRowAction Action { get; set; } = ImportRowAction.Create;

    /// <summary>Danh sách lỗi (tiếng Việt, nêu lý do và cách sửa).</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>Dòng có lỗi.</summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>Thêm lỗi.</summary>
    public void AddError(string message) => _errors.Add(message);
}

/// <summary>Bảng dữ liệu của tệp kết quả (ví dụ danh sách tài khoản + mật khẩu tạm).</summary>
/// <param name="FileName">Tên tệp gợi ý khi tải về (.xlsx).</param>
/// <param name="SheetName">Tên sheet.</param>
/// <param name="Title">Dòng tiêu đề / ghi chú trên cùng (có thể null).</param>
/// <param name="Headers">Tiêu đề cột.</param>
/// <param name="Rows">Dữ liệu từng dòng (cùng số cột với <paramref name="Headers"/>).</param>
public sealed record ImportResultTable(
    string FileName,
    string SheetName,
    string? Title,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string?>> Rows);

/// <summary>Kết quả ghi dữ liệu của một định nghĩa import.</summary>
/// <param name="Created">Số bản ghi tạo mới.</param>
/// <param name="Updated">Số bản ghi cập nhật.</param>
/// <param name="ResultFile">Tệp kết quả cần giao cho người dùng (tải một lần), null nếu không có.</param>
public sealed record ImportCommitResult(int Created, int Updated, ImportResultTable? ResultFile = null);

/// <summary>
/// Thông tin mô tả của một loại import (không phụ thuộc kiểu dòng).
/// Triển khai thực tế dùng <see cref="IImportDefinition{TRow}"/>.
/// </summary>
public interface IImportDefinition
{
    /// <summary>Mã loại (kebab-case, dùng trong URL), ví dụ <c>departments</c>.</summary>
    string Kind { get; }

    /// <summary>Tên hiển thị.</summary>
    string DisplayName { get; }

    /// <summary>Mô tả ngắn cho người dùng.</summary>
    string Description { get; }

    /// <summary>Các cột của file mẫu theo thứ tự.</summary>
    IReadOnlyList<ImportColumn> TemplateColumns { get; }

    /// <summary>
    /// Quyền dữ liệu cần có ngoài <c>system.import</c> (người dùng phải có <b>tất cả</b>), ví dụ <c>catalog.manage</c>.
    /// </summary>
    IReadOnlyList<string> RequiredPermissions { get; }
}

/// <summary>
/// Định nghĩa một loại import. Khung import lo việc đọc/ghi Excel, phiên xem trước, transaction, audit tóm tắt,
/// kiểm tra quyền và tệp kết quả; định nghĩa chỉ lo nghiệp vụ của loại dữ liệu.
/// Đăng ký: <c>services.AddImportDefinition&lt;TDefinition&gt;()</c> (scoped).
/// </summary>
/// <typeparam name="TRow">Kiểu dữ liệu một dòng.</typeparam>
public interface IImportDefinition<TRow> : IImportDefinition where TRow : class
{
    /// <summary>
    /// Chuyển dòng thô thành dữ liệu có kiểu và kiểm tra riêng dòng đó (định dạng, độ dài, giá trị hợp lệ).
    /// Luôn trả về đối tượng (có thể thiếu trường); lỗi ghi vào <paramref name="errors"/>.
    /// Cột bắt buộc để trống đã được khung kiểm tra trước.
    /// </summary>
    TRow ParseRow(ImportSourceRow source, ICollection<string> errors);

    /// <summary>
    /// Kiểm tra chéo toàn bộ dòng: trùng trong file, trùng/khớp CSDL, mã tham chiếu tồn tại.
    /// Đặt <see cref="ImportRow{TRow}.Action"/> (Create/Update) cho dòng hợp lệ, <see cref="ImportRow{TRow}.AddError"/> cho dòng lỗi.
    /// Được gọi cả lúc xem trước và ngay trước khi ghi (dữ liệu có thể đã đổi).
    /// </summary>
    Task ValidateAsync(IReadOnlyList<ImportRow<TRow>> rows, CancellationToken ct);

    /// <summary>
    /// Ghi các dòng (đều hợp lệ). Khung đã mở transaction qua <c>IUnitOfWork</c> và sẽ lưu/commit sau khi hàm trả về;
    /// ném ngoại lệ → rollback toàn bộ.
    /// </summary>
    Task<ImportCommitResult> CommitAsync(IReadOnlyList<ImportRow<TRow>> rows, CancellationToken ct);
}

/// <summary>Đọc/ghi Excel cho chức năng import (triển khai bằng ClosedXML ở Infrastructure).</summary>
public interface IImportWorkbook
{
    /// <summary>Tạo file mẫu: sheet dữ liệu (tiêu đề cố định, data validation) + sheet "Hướng dẫn".</summary>
    byte[] CreateTemplate(IImportDefinition definition);

    /// <summary>
    /// Đọc sheet dữ liệu: khớp tiêu đề cột, cắt khoảng trắng, bỏ dòng trống.
    /// Ném <c>ValidationException</c> khi tệp không đọc được, thiếu cột, quá số dòng cho phép.
    /// </summary>
    IReadOnlyList<ImportSourceRow> ReadRows(Stream content, IImportDefinition definition);

    /// <summary>Ghi tệp kết quả; giá trị bắt đầu bằng <c>= + - @</c> (và tab/CR) được thêm tiền tố <c>'</c> chống formula injection.</summary>
    byte[] CreateResultFile(ImportResultTable table);
}

/// <summary>Tra cứu phục vụ kiểm tra chéo khi import (chỉ đọc).</summary>
public interface IImportLookup
{
    /// <summary>Toàn bộ Phòng/đơn vị, kể cả đã xóa mềm.</summary>
    Task<IReadOnlyList<CatalogLookupEntry>> GetDepartmentsAsync(CancellationToken ct);

    /// <summary>Toàn bộ Chi bộ, kể cả đã xóa mềm.</summary>
    Task<IReadOnlyList<CatalogLookupEntry>> GetPartyCellsAsync(CancellationToken ct);

    /// <summary>Tên đăng nhập (chữ thường) đã tồn tại trong số <paramref name="usernames"/>, kể cả tài khoản đã xóa mềm.</summary>
    Task<IReadOnlySet<string>> FindExistingUsernamesAsync(IReadOnlyCollection<string> usernames, CancellationToken ct);
}

/// <summary>Một mục danh mục phục vụ tra cứu.</summary>
/// <param name="Id">Id đơn vị.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="IsActive">Đang hoạt động.</param>
/// <param name="IsDeleted">Đã xóa mềm.</param>
/// <param name="ParentId">Đơn vị cha (cây đơn vị, task 14).</param>
public sealed record CatalogLookupEntry(Guid Id, string Code, string Name, bool IsActive, bool IsDeleted, Guid? ParentId = null);

/// <summary>Ghi bản ghi audit tóm tắt cho mỗi lần xác nhận import (cùng transaction).</summary>
public interface IImportAuditLog
{
    /// <summary>Đưa bản ghi audit vào DbContext (được lưu cùng lần <c>SaveChanges</c> của khung).</summary>
    void Add(string kind, string displayName, string? fileName, int rowCount, int created, int updated);
}
