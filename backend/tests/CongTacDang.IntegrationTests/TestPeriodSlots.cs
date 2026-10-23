namespace CongTacDang.IntegrationTests;

/// <summary>Năm/quý không trùng cho kỳ tạo qua API: quý 4, năm 2001–2099; kỳ chèn thẳng CSDL dùng quý 1–3.</summary>
internal static class TestPeriodSlots
{
    private static int _next = -1;

    public const int Quarter = 4;

    public static int NextYear() => 2001 + Interlocked.Increment(ref _next) % 99;
}
