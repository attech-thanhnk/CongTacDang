using System;
using System.Collections.Generic;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Tự đề xuất Xuất sắc mà chưa cam kết không bị khống chế: chỉ cảnh báo trong danh sách cảnh báo mức.</summary>
public sealed class SelfNoCapWarningTests
{
    private static readonly CriteriaSnapshot Criteria = new() { SetId = Guid.NewGuid(), Code = "X", Name = "Bộ X", Content = new CriteriaSetContent() };

    private static EvaluationRecord Record(EvaluationGrade proposed, bool? declares) => new()
    {
        SelfProposedGrade = proposed,
        SelfDeclaresNoCap = declares,
        SelfScoredAt = DateTime.UtcNow,
        Tasks = new List<EvaluationTask>()
    };

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void Excellent_WithoutDeclaration_Warns(bool? declares) =>
        Assert.Contains(EvaluationMapping.NoCapDeclarationWarning,
            EvaluationMapping.GradeWarnings(Record(EvaluationGrade.HoanThanhXuatSac, declares), EvaluationGrade.HoanThanhXuatSac, Criteria));

    [Fact]
    public void Excellent_WithDeclaration_NoWarning() =>
        Assert.DoesNotContain(EvaluationMapping.NoCapDeclarationWarning,
            EvaluationMapping.GradeWarnings(Record(EvaluationGrade.HoanThanhXuatSac, true), EvaluationGrade.HoanThanhXuatSac, Criteria));

    [Fact]
    public void NotExcellent_OrLoweredLater_NoWarning()
    {
        Assert.DoesNotContain(EvaluationMapping.NoCapDeclarationWarning,
            EvaluationMapping.GradeWarnings(Record(EvaluationGrade.HoanThanhTot, null), EvaluationGrade.HoanThanhTot, Criteria));
        // Cấp trên đã hạ mức thì cam kết không còn ý nghĩa với mức hiện hành.
        Assert.DoesNotContain(EvaluationMapping.NoCapDeclarationWarning,
            EvaluationMapping.GradeWarnings(Record(EvaluationGrade.HoanThanhXuatSac, null), EvaluationGrade.HoanThanhTot, Criteria));
    }
}
