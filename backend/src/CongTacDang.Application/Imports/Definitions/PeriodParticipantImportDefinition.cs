using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>Một dòng import người được đánh giá.</summary>
public sealed class PeriodParticipantImportRow
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public string? PeriodName { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>Kỳ đã phân giải khi kiểm tra.</summary>
    public Guid PeriodId { get; set; }

    /// <summary>Cán bộ đã phân giải khi kiểm tra.</summary>
    public Guid MemberId { get; set; }
}

/// <summary>
/// Import danh sách người được đánh giá của kỳ (task 12, khung import của task 10). Mỗi dòng = một cán bộ vào một kỳ
/// (xác định bằng Năm + Quý, thêm Tên kỳ khi có nhiều kỳ cùng quý). Kỳ phải ở trạng thái Dự thảo hoặc Đang mở.
/// Ghi qua <see cref="IPeriodService.StageParticipantsAsync"/> (ảnh chụp Phòng/Chi bộ/khung/cấp quyết định, lịch sử).
/// </summary>
public sealed class PeriodParticipantImportDefinition : IImportDefinition<PeriodParticipantImportRow>
{
    private const string YearKey = "year";
    private const string QuarterKey = "quarter";
    private const string PeriodNameKey = "periodName";
    private const string UsernameKey = "username";
    private const string NoteKey = "note";

    private readonly IEvaluationWorkflowRepository _repo;
    private readonly IPeriodService _periods;

    public PeriodParticipantImportDefinition(IEvaluationWorkflowRepository repo, IPeriodService periods)
    {
        _repo = repo;
        _periods = periods;
    }

    /// <inheritdoc />
    public string Kind => "period-participants";

    /// <inheritdoc />
    public string DisplayName => "Người được đánh giá của kỳ";

    /// <inheritdoc />
    public string Description =>
        "Thêm cán bộ vào danh sách được đánh giá của một kỳ (đang dự thảo hoặc đang mở). Phòng, Chi bộ, khung chức danh, "
        + "cấp quyết định được chụp từ hồ sơ cán bộ tại thời điểm nhập.";

    /// <inheritdoc />
    public IReadOnlyList<ImportColumn> TemplateColumns { get; } = new[]
    {
        new ImportColumn(YearKey, "Năm", true, "Năm của kỳ đánh giá (4 chữ số).", null, "2026"),
        new ImportColumn(QuarterKey, "Quý", true, "Quý của kỳ đánh giá.", new[] { "1", "2", "3", "4" }, "3"),
        new ImportColumn(PeriodNameKey, "Tên kỳ", false, "Chỉ cần ghi khi có nhiều kỳ trong cùng một quý (ghi đúng tên kỳ).", null,
            "Đánh giá, xếp loại cán bộ Quý III/2026"),
        new ImportColumn(UsernameKey, "Tên đăng nhập", true, "Tên đăng nhập của cán bộ được đánh giá (tài khoản đang hoạt động).", null, "nguyenvana"),
        new ImportColumn(NoteKey, "Ghi chú", false, "Không bắt buộc, không được lưu.", null, null)
    };

    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.PeriodManage };

    /// <inheritdoc />
    public PeriodParticipantImportRow ParseRow(ImportSourceRow source, ICollection<string> errors)
    {
        var row = new PeriodParticipantImportRow
        {
            PeriodName = source.GetOrNull(PeriodNameKey),
            Username = source.Get(UsernameKey)
        };

        if (int.TryParse(source.Get(YearKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) && year is >= 2000 and <= 2100)
            row.Year = year;
        else
            errors.Add("Năm không hợp lệ: ghi năm 4 chữ số, ví dụ 2026.");

        if (int.TryParse(source.Get(QuarterKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quarter) && quarter is >= 1 and <= 4)
            row.Quarter = quarter;
        else
            errors.Add("Quý không hợp lệ: chọn 1, 2, 3 hoặc 4.");

        if (row.Username.Any(char.IsWhiteSpace) || row.Username.Length > 100)
            errors.Add("Tên đăng nhập không được có khoảng trắng và không dài quá 100 ký tự.");

        return row;
    }

    /// <inheritdoc />
    public async Task ValidateAsync(IReadOnlyList<ImportRow<PeriodParticipantImportRow>> rows, CancellationToken ct)
    {
        var periods = await _repo.ListPeriodsAsync(ct);
        var members = (await _repo.FindMembersByUsernamesAsync(
                rows.Select(r => r.Data.Username).Where(u => u.Length > 0).ToList(), ct))
            .GroupBy(m => m.Username.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());
        var participantsByPeriod = new Dictionary<Guid, HashSet<Guid>>();
        var seen = new HashSet<(Guid, Guid)>();

        foreach (var row in rows)
        {
            if (row.HasErrors)
                continue;
            var data = row.Data;

            var candidates = periods
                .Where(p => p.Year == data.Year && (int)p.Quarter == data.Quarter)
                .Where(p => data.PeriodName == null || string.Equals(p.Name.Trim(), data.PeriodName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count == 0)
            {
                row.AddError($"Không tìm thấy kỳ đánh giá Quý {data.Quarter}/{data.Year}"
                    + (data.PeriodName != null ? $" có tên \"{data.PeriodName}\"" : string.Empty) + ". Hãy tạo kỳ trước khi nhập.");
                continue;
            }
            if (candidates.Count > 1)
            {
                row.AddError($"Có {candidates.Count} kỳ trong Quý {data.Quarter}/{data.Year}. Hãy ghi cột \"Tên kỳ\" để chọn đúng kỳ.");
                continue;
            }

            var period = candidates[0];
            if (period.Status is not (PeriodStatus.Draft or PeriodStatus.Open))
            {
                row.AddError($"Kỳ \"{period.Name}\" không còn nhận thêm người được đánh giá (chỉ kỳ dự thảo hoặc đang mở).");
                continue;
            }

            if (!members.TryGetValue(data.Username.ToLowerInvariant(), out var member))
            {
                row.AddError($"Không có tài khoản \"{data.Username}\". Hãy kiểm tra tên đăng nhập hoặc nhập cán bộ trước.");
                continue;
            }
            if (!member.IsActive)
            {
                row.AddError($"Tài khoản \"{data.Username}\" đang bị khóa, không thêm vào kỳ được.");
                continue;
            }

            if (!participantsByPeriod.TryGetValue(period.Id, out var existing))
                participantsByPeriod[period.Id] = existing = await _repo.GetParticipantIdsAsync(period.Id, ct);
            if (existing.Contains(member.Id))
            {
                row.AddError($"{member.FullName} đã có trong danh sách được đánh giá của kỳ \"{period.Name}\".");
                continue;
            }
            if (!seen.Add((period.Id, member.Id)))
            {
                row.AddError($"Dòng trùng: {member.FullName} đã có ở dòng khác của tệp cho cùng kỳ.");
                continue;
            }

            data.PeriodId = period.Id;
            data.MemberId = member.Id;
            row.Action = ImportRowAction.Create;
        }
    }

    /// <inheritdoc />
    public async Task<ImportCommitResult> CommitAsync(IReadOnlyList<ImportRow<PeriodParticipantImportRow>> rows, CancellationToken ct)
    {
        var created = 0;
        foreach (var group in rows.GroupBy(r => r.Data.PeriodId))
        {
            var result = await _periods.StageParticipantsAsync(group.Key, group.Select(r => r.Data.MemberId).ToList(), "nhập từ Excel", ct);
            if (result.Skipped.Count > 0)
                throw new ValidationException("Không thêm được: " + string.Join(" ", result.Skipped));
            created += result.Added;
        }

        return new ImportCommitResult(created, 0);
    }
}
