using System.Text.Json;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Khung tiêu chí tập thể (Mẫu 06, 07) trong bộ tiêu chí: mặc định, chuẩn hóa và kiểm tra.</summary>
public sealed class CollectiveCriteriaSetUnitTests
{
    private static double Total(CriteriaSetContent c, CollectiveScoreKind kind) =>
        c.CollectiveGroups.Where(g => g.Kind == kind).SelectMany(g => g.Items).Sum(i => i.MaxScore);

    [Fact]
    public void Default_HasCollectiveFramework_30_70_AndIsValid()
    {
        var content = CriteriaSetDefaults.Build();
        Assert.Empty(content.Validate());
        Assert.Equal(new[] { "I", "II" }, content.CollectiveGroups.Select(g => g.Code));
        Assert.Equal(new[] { CollectiveScoreKind.General, CollectiveScoreKind.Task }, content.CollectiveGroups.Select(g => g.Kind));
        Assert.Equal(new[] { "I.1", "I.2", "I.3", "I.4", "I.5", "I.6" }, content.CollectiveGroups[0].Items.Select(i => i.Code));
        Assert.All(content.CollectiveGroups[0].Items, i => Assert.Equal(5, i.MaxScore));
        Assert.Equal(new[] { 50.0, 20 }, content.CollectiveGroups[1].Items.Select(i => i.MaxScore));
        Assert.Equal(30, Total(content, CollectiveScoreKind.General), 6);
        Assert.Equal(70, Total(content, CollectiveScoreKind.Task), 6);
    }

    [Fact]
    public void Defaults_ReturnFreshCopy_AndKindSerializesAsString()
    {
        var first = CriteriaSetDefaults.CollectiveGroups();
        first[0].Items.Clear();
        Assert.Equal(6, CriteriaSetDefaults.CollectiveGroups()[0].Items.Count);

        var json = CriteriaSetDefaults.Build().ToJson();
        Assert.Contains("\"collectiveGroups\"", json);
        Assert.Contains("\"kind\":\"Task\"", json);
        var parsed = CriteriaSetContent.Parse(json);
        Assert.Equal(CollectiveScoreKind.Task, parsed.CollectiveGroups[1].Kind);
        Assert.Equal(JsonSerializer.Serialize(CriteriaSetDefaults.CollectiveGroups(), CriteriaSetContent.JsonOptions),
            JsonSerializer.Serialize(parsed.CollectiveGroups, CriteriaSetContent.JsonOptions));
    }

    [Fact]
    public void EmptyFramework_IsValid_AndNullIsNormalized()
    {
        var c = CriteriaSetDefaults.Build();
        c.CollectiveGroups = null!;
        c.Normalize();
        Assert.Empty(c.CollectiveGroups);
        Assert.Empty(c.Validate());

        // Bộ cũ lưu trước khi có khung: JSON không có trường collectiveGroups.
        var old = CriteriaSetContent.Parse("{\"schemaVersion\":1}");
        Assert.Empty(old.CollectiveGroups);
    }

    [Fact]
    public void Normalize_TrimsCodesAndTexts_DropsNulls()
    {
        var c = CriteriaSetDefaults.Build();
        c.CollectiveGroups[0].Code = "  I ";
        c.CollectiveGroups[0].Name = " Nhóm tiêu chí chung  ";
        c.CollectiveGroups[0].Items[0].Code = " I.1 ";
        c.CollectiveGroups[0].Items[0].Text = "  nội dung ";
        c.CollectiveGroups[0].Items[0].FormText = "   ";
        c.CollectiveGroups[0].Items[1].FormText = " in phiếu ";
        c.CollectiveGroups[0].Items.Add(null!);
        c.CollectiveGroups.Add(null!);
        c.Normalize();
        Assert.Equal("I", c.CollectiveGroups[0].Code);
        Assert.Equal("Nhóm tiêu chí chung", c.CollectiveGroups[0].Name);
        Assert.Equal("I.1", c.CollectiveGroups[0].Items[0].Code);
        Assert.Equal("nội dung", c.CollectiveGroups[0].Items[0].Text);
        Assert.Null(c.CollectiveGroups[0].Items[0].FormText);
        Assert.Equal("in phiếu", c.CollectiveGroups[0].Items[1].FormText);
        Assert.Equal(2, c.CollectiveGroups.Count);
        Assert.Equal(6, c.CollectiveGroups[0].Items.Count);
        Assert.Empty(c.Validate());
    }

    [Fact]
    public void Validate_RejectsInvalidFramework()
    {
        List<string> Errors(Action<CriteriaSetContent> mutate)
        {
            var c = CriteriaSetDefaults.Build();
            mutate(c);
            return c.Normalize().Validate();
        }

        // Tổng lệch so với điểm tối đa hai nhóm điểm.
        var e = Errors(c => c.CollectiveGroups[0].Items[0].MaxScore = 4);
        Assert.Contains(e, x => x.Contains("nhóm tiêu chí chung (29)") && x.Contains("(30)"));
        e = Errors(c => c.CollectiveGroups[1].Items[1].MaxScore = 25);
        Assert.Contains(e, x => x.Contains("nhóm kết quả thực hiện nhiệm vụ (75)") && x.Contains("(70)"));
        e = Errors(c => c.Parameters.CollectiveTaskMaxScore = 60);
        Assert.Contains(e, x => x.Contains("(70)") && x.Contains("(60)"));
        e = Errors(c => c.CollectiveGroups[1].Kind = CollectiveScoreKind.General);
        Assert.Contains(e, x => x.Contains("nhóm tiêu chí chung (100)"));

        // Mã trùng trong khung (giữa nhóm và tiêu chí, không phân biệt hoa thường), mã sai định dạng.
        e = Errors(c => c.CollectiveGroups[1].Items[0].Code = "i.1");
        Assert.Contains(e, x => x.Contains("bị trùng"));
        e = Errors(c => c.CollectiveGroups[1].Code = "I");
        Assert.Contains(e, x => x.Contains("\"I\" bị trùng"));
        e = Errors(c => c.CollectiveGroups[0].Items[0].Code = "I 1");
        Assert.Contains(e, x => x.Contains("\"I 1\" không hợp lệ"));

        // Không gian mã riêng: trùng mã nhóm tiêu chí chung cá nhân vẫn hợp lệ.
        Assert.Empty(Errors(c => c.CollectiveGroups[0].Code = c.GeneralGroups[0].Code));

        // Nhóm rỗng, thiếu tên, nhóm điểm sai.
        e = Errors(c => c.CollectiveGroups.Add(new CollectiveCriteriaGroup { Code = "III", Name = "Khác", Kind = CollectiveScoreKind.Task }));
        Assert.Contains(e, x => x.Contains("\"Khác\" phải có ít nhất một tiêu chí con"));
        e = Errors(c => c.CollectiveGroups[0].Name = " ");
        Assert.Contains(e, x => x.Contains("\"I\" chưa có tên"));
        e = Errors(c => c.CollectiveGroups[0].Kind = (CollectiveScoreKind)9);
        Assert.Contains(e, x => x.Contains("Nhóm điểm") && x.Contains("không hợp lệ"));

        // Tiêu chí con: nội dung, độ dài, điểm.
        e = Errors(c => c.CollectiveGroups[0].Items[2].Text = "");
        Assert.Contains(e, x => x.Contains("\"I.3\" chưa có nội dung"));
        e = Errors(c => c.CollectiveGroups[0].Items[2].Text = new string('x', 2001));
        Assert.Contains(e, x => x.Contains("\"I.3\" không được dài quá 2000"));
        e = Errors(c => c.CollectiveGroups[0].Items[2].FormText = new string('x', 4001));
        Assert.Contains(e, x => x.Contains("in trên phiếu") && x.Contains("\"I.3\""));
        e = Errors(c => c.CollectiveGroups[0].Items[2].MaxScore = 0);
        Assert.Contains(e, x => x.Contains("\"I.3\" phải lớn hơn 0"));
        e = Errors(c => c.CollectiveGroups[1].Items[0].MaxScore = 101);
        Assert.Contains(e, x => x.Contains("\"II.1\" phải lớn hơn 0 và không quá 100"));
    }
}
