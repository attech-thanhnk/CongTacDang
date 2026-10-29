using System.Linq;
using System.Collections.Generic;
using CongTacDang.Application.Organization;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Diện đánh giá theo chức vụ: lãnh đạo, quản lý, Bí thư/Phó Bí thư; không gồm chuyên viên, đảng viên thường.</summary>
public class PositionEvaluableTests
{
    private static HeldPosition Pos(string name, string? statCode, bool head = false) => new(name, statCode, null, head);

    [Fact]
    public void NoPositions_NotEvaluable() => Assert.False(PositionRules.IsEvaluable(new List<HeldPosition>()));

    [Fact]
    public void Null_NotEvaluable() => Assert.False(PositionRules.IsEvaluable(null));

    [Fact]
    public void OrdinaryMemberOnly_NotEvaluable() =>
        Assert.False(PositionRules.IsEvaluable(new[] { Pos("Chuyên viên", null), Pos("Đảng viên", null) }));

    [Fact]
    public void StatCode_Evaluable() =>
        Assert.True(PositionRules.IsEvaluable(new[] { Pos("Đảng viên", null), Pos("Phó Trưởng phòng", "M26") }));

    [Fact]
    public void HeadWithoutStatCode_Evaluable() =>
        Assert.True(PositionRules.IsEvaluable(new[] { Pos("Đài trưởng", null, head: true) }));
}

/// <summary>Thứ tự từ cấp trên xuống: Chủ tịch/Giám đốc, Phó Giám đốc, Kiểm soát viên, Bí thư Chi bộ, Trưởng phòng, Phó Trưởng phòng.</summary>
public class PositionSeniorityTests
{
    private static HeldPosition Pos(string name, string? statCode, bool head = false) => new(name, statCode, null, head);

    [Fact]
    public void RanksFromTopDown()
    {
        var order = new[]
        {
            PositionRules.SeniorityRank(new[] { Pos("Giám đốc", "M14", true) }),
            PositionRules.SeniorityRank(new[] { Pos("Phó Giám đốc", "M14") }),
            PositionRules.SeniorityRank(new[] { Pos("Kiểm soát viên", "M15") }),
            PositionRules.SeniorityRank(new[] { Pos("Bí thư Chi bộ", "M22", true) }),
            PositionRules.SeniorityRank(new[] { Pos("Trưởng phòng", "M26", true) }),
            PositionRules.SeniorityRank(new[] { Pos("Đài trưởng", null, true) }),
            PositionRules.SeniorityRank(new[] { Pos("Phó Trưởng phòng", "M26") }),
            PositionRules.SeniorityRank(new[] { Pos("Chuyên viên", null) }),
        };
        Assert.Equal(order.OrderBy(x => x), order);
    }

    [Fact]
    public void MultiplePositions_TakeTheHighest() =>
        Assert.Equal(
            PositionRules.SeniorityRank(new[] { Pos("Bí thư Chi bộ", "M22", true) }),
            PositionRules.SeniorityRank(new[] { Pos("Phó Trưởng phòng", "M26"), Pos("Bí thư Chi bộ", "M22", true) }));

    [Fact]
    public void NoPositions_Last() => Assert.Equal(9999, PositionRules.SeniorityRank(null));
}
