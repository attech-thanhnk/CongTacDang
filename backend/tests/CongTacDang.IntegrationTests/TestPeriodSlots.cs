namespace CongTacDang.IntegrationTests;

/// <summary>
/// Năm/quý không trùng cho kỳ tạo qua API trong test (API chặn hai kỳ cùng năm, quý).
/// Kỳ tạo qua API dùng quý 4, năm tăng dần 2001–2099; kỳ chèn thẳng vào CSDL trong test dùng quý 1–3 nên không đụng nhau.
/// </summary>
internal static class TestPeriodSlots
{
    private static int _next = -1;

    public const int Quarter = 4;

    public static int NextYear() => 2001 + Interlocked.Increment(ref _next) % 99;
}
