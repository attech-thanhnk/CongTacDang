namespace CongTacDang.Application.Common.Exceptions;

/// <summary>Không tìm thấy dữ liệu nghiệp vụ.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404)
    {
    }
}
