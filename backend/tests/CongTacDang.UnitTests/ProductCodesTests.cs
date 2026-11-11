using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Mã sản phẩm tự đánh SPnn: theo thứ tự, giữ mã đã có, không dùng lại mã cũ, báo trùng.</summary>
public class ProductCodesTests
{
    private static List<EvaluationTask> Tasks(params string?[] codes) =>
        codes.Select((c, i) => new EvaluationTask { TaskOrder = i + 1, TaskName = $"Việc {i + 1}", ProductCode = c }).ToList();

    [Fact]
    public void AssignMissing_NumbersEmptyInOrder_KeepsTypedCodes()
    {
        var tasks = Tasks(null, "KT-02", " ", "sp05", null);

        ProductCodes.AssignMissing(tasks);

        Assert.Equal(new[] { "SP06", "KT-02", "SP07", "sp05", "SP08" }, tasks.Select(t => t.ProductCode));
    }

    [Fact]
    public void AssignMissing_ContinuesAfterCodesOfDeletedProducts()
    {
        var tasks = Tasks("SP01", null);

        ProductCodes.AssignMissing(tasks, new[] { "SP01", "SP02", "SP03" });

        Assert.Equal(new[] { "SP01", "SP04" }, tasks.Select(t => t.ProductCode));
    }

    [Fact]
    public void Format_PadsTwoDigits()
    {
        Assert.Equal("SP01", ProductCodes.Format(1));
        Assert.Equal("SP120", ProductCodes.Format(120));
    }

    [Fact]
    public void FindDuplicate_IgnoresCaseAndBlanks()
    {
        Assert.Equal("kt-1", ProductCodes.FindDuplicate(new[] { "KT-1", null, " ", "kt-1 " }));
        Assert.Null(ProductCodes.FindDuplicate(new[] { "SP01", null, null, "SP02" }));
    }
}
