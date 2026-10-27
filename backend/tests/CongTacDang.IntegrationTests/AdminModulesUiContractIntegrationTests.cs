using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Quản trị, danh mục tổ chức, bộ tiêu chí, nhập dữ liệu, biểu mẫu Word, thông tin đơn vị: gọi API đúng thân yêu cầu
/// và đường dẫn mà giao diện gửi (lấy từ mã frontend), gồm trường để trống và trường không có ô nhập.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AdminModulesUiContractIntegrationTests
{
    private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, HttpClient> Admins = new();

    private readonly ApiFactory _factory;

    public AdminModulesUiContractIntegrationTests(ApiFactory factory) => _factory = factory;

    #region A1 — tài khoản: tạo, sửa, khóa, đặt lại mật khẩu, chức vụ, vai trò

    [SkippableFact]
    public async Task A1_Accounts_CreateEditLockResetPositionsAssignments_AsSentByUi()
    {
        var admin = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var dept = await Data(await PostRawAsync(admin, "/api/organizations/departments", new
        {
            code = $"UA-{suffix}".ToUpperInvariant(), name = $"Phòng UA {suffix}", description = "", sortOrder = 0, isActive = true,
            parentId = await RootIdAsync(admin, "departments"), unitTypeId = await FirstUnitTypeAsync(admin, "Administrative")
        }));
        var deptId = dept.GetProperty("id").GetString()!;

        // Tạo (AccountFormModal): ô để trống không gửi; ngày sinh yyyy-MM-dd; thẩm quyền "Theo chức vụ" không gửi.
        var created = await Data(await PostRawAsync(admin, "/api/users", new JsonObject
        {
            ["username"] = $"ui_{suffix}",
            ["fullName"] = $"Nguyễn Văn UI {suffix}",
            ["dateOfBirth"] = "1985-03-07",
            ["email"] = $"ui_{suffix}@example.vn",
            ["departmentId"] = deptId
        }));
        var userId = created.GetProperty("userId").GetString()!;
        Assert.False(string.IsNullOrEmpty(created.GetProperty("temporaryPassword").GetString()));

        // Đặt tay thẩm quyền gửi dạng số (select trả 1 hoặc 2) kèm lý do.
        var withAuthority = await PostRawAsync(admin, "/api/users", new JsonObject
        {
            ["username"] = $"ui2_{suffix}", ["fullName"] = $"Trần Thị UI {suffix}", ["approvalAuthority"] = 2,
            ["approvalAuthorityReason"] = "Căn cứ văn bản phân cấp"
        });
        var second = await Data(withAuthority);
        var secondAccount = await Data(admin, $"/api/users/{second.GetProperty("userId").GetString()}");
        Assert.Equal(2, secondAccount.GetProperty("approvalAuthority").GetInt32() == 2 ? 2 : -1);

        // Danh sách như trang Tài khoản: page, pageSize, q, isActive.
        var page = await Data(admin, $"/api/users?page=1&pageSize=20&q={suffix}&isActive=true");
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1985, DateTime.Parse(
            (await Data(admin, $"/api/users/{userId}")).GetProperty("dateOfBirth").GetString()!).Year);

        // Sửa (nút Lưu): gửi mọi ô, ô trống là chuỗi rỗng; bỏ gán đơn vị bằng Guid rỗng.
        var edited = await Data(await PutRawAsync(admin, $"/api/users/{userId}", new JsonObject
        {
            ["fullName"] = $"Nguyễn Văn UI {suffix} (sửa)", ["dateOfBirth"] = "", ["email"] = "", ["phoneNumber"] = "0901234567",
            ["partyCardNumber"] = "", ["positionTitle"] = "Chuyên viên", ["departmentId"] = EmptyGuid, ["partyCellId"] = EmptyGuid,
            ["weightFrameCode"] = ""
        }));
        Assert.Equal(JsonValueKind.Null, edited.GetProperty("departmentId").ValueKind);
        Assert.Equal(JsonValueKind.Null, edited.GetProperty("dateOfBirth").ValueKind);
        Assert.Equal("", edited.GetProperty("email").GetString());
        Assert.False(edited.GetProperty("isPartyMember").GetBoolean());

        // Gán lại đơn vị và ngày sinh ở lần sửa kế tiếp.
        var edited2 = await Data(await PutRawAsync(admin, $"/api/users/{userId}", new JsonObject
        {
            ["fullName"] = "Nguyễn Văn UI", ["dateOfBirth"] = "1990-12-31", ["email"] = "a@example.vn", ["phoneNumber"] = "",
            ["partyCardNumber"] = "", ["positionTitle"] = "Chuyên viên", ["departmentId"] = deptId, ["partyCellId"] = EmptyGuid,
            ["weightFrameCode"] = ""
        }));
        Assert.Equal(deptId, edited2.GetProperty("departmentId").GetString());

        // Khóa / mở / mở khóa / đặt lại mật khẩu: POST không thân yêu cầu như request() của giao diện.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{userId}/deactivate", null)).StatusCode);
        Assert.False((await Data(admin, $"/api/users/{userId}")).GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{userId}/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{userId}/unlock", null)).StatusCode);
        var reset = await Data(await admin.PostAsync($"/api/users/{userId}/reset-password", null));
        Assert.False(string.IsNullOrEmpty(reset.GetProperty("temporaryPassword").GetString()));
        Assert.True(reset.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal($"ui_{suffix}", reset.GetProperty("userName").GetString());

        // Chức vụ (MemberPositionsPanel): thêm kèm đơn vị, thời hạn là mốc UTC 00:00, ghi chú rỗng.
        var positions = await Data(admin, "/api/positions");
        var administrative = positions.EnumerateArray().First(p => p.GetProperty("side").GetString() == "Administrative" && p.GetProperty("isActive").GetBoolean());
        var addPosition = await Data(await PostRawAsync(admin, $"/api/users/{userId}/positions", new JsonObject
        {
            ["positionId"] = administrative.GetProperty("id").GetString(), ["departmentId"] = deptId, ["isPrimary"] = true,
            ["isActing"] = false, ["validFrom"] = DateTime.UtcNow.Date.AddDays(-3).ToString("O"),
            ["validTo"] = DateTime.UtcNow.Date.AddDays(60).ToString("O"), ["note"] = ""
        }));
        var positionId = addPosition.GetProperty("id").GetString()!;
        Assert.True(addPosition.GetProperty("isEffective").GetBoolean());

        // Sửa chức vụ: không gửi positionId, bỏ đơn vị bằng Guid rỗng, bỏ thời hạn bằng clearValidTo.
        var updatedPosition = await Data(await PutRawAsync(admin, $"/api/users/{userId}/positions/{positionId}", new JsonObject
        {
            ["departmentId"] = deptId, ["partyCellId"] = EmptyGuid, ["isPrimary"] = true, ["isActing"] = true,
            ["validFrom"] = DateTime.UtcNow.Date.AddDays(-3).ToString("O"), ["clearValidTo"] = true, ["note"] = "QĐ 12"
        }));
        Assert.Equal(JsonValueKind.Null, updatedPosition.GetProperty("validTo").ValueKind);
        Assert.True(updatedPosition.GetProperty("isActing").GetBoolean());
        var authority = await Data(admin, $"/api/users/{userId}/approval-authority");
        Assert.False(string.IsNullOrEmpty(authority.GetProperty("effective").GetString()));
        var ended = await Data(await admin.PostAsync($"/api/users/{userId}/positions/{positionId}/end", null));
        Assert.False(ended.GetProperty("isEffective").GetBoolean());

        // Bản gán vai trò từ tab "Vai trò & phạm vi": hạn là ISO, ghi chú không gửi khi rỗng.
        var roles = await Data(admin, "/api/admin/roles");
        var roleId = roles.EnumerateArray().First(r => !r.GetProperty("isProtected").GetBoolean() && r.GetProperty("permissionCodes").GetArrayLength() > 0)
            .GetProperty("id").GetString()!;
        var assignment = await Data(await PostRawAsync(admin, "/api/admin/assignments", new JsonObject
        {
            ["userId"] = userId, ["roleId"] = roleId, ["scopeType"] = "Department", ["scopeId"] = deptId,
            ["validFrom"] = DateTime.UtcNow.Date.ToString("O"), ["validTo"] = DateTime.UtcNow.Date.AddDays(31).ToString("O")
        }));
        var assignmentId = assignment.GetProperty("id").GetString()!;
        var listed = await Data(admin, $"/api/admin/assignments?userId={userId}");
        Assert.Contains(listed.EnumerateArray(), a => a.GetProperty("id").GetString() == assignmentId);
        var changed = await Data(await PutRawAsync(admin, $"/api/admin/assignments/{assignmentId}", new JsonObject
        {
            ["validTo"] = DateTime.UtcNow.Date.AddDays(45).ToString("O"), ["note"] = "Gia hạn"
        }));
        Assert.Equal("Gia hạn", changed.GetProperty("note").GetString());
        var effective = await Data(admin, $"/api/admin/users/{userId}/effective-permissions");
        Assert.True(effective.GetProperty("permissions").GetArrayLength() > 0);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/assignments/{assignmentId}/end", null)).StatusCode);
        var review = await Data(admin, $"/api/admin/performers/review?userId={userId}");
        Assert.Equal(JsonValueKind.Array, review.ValueKind);

        // Xóa tài khoản như nút "Xóa tài khoản".
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/users/{userId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/users/{userId}")).StatusCode);
    }

    [SkippableFact]
    public async Task A2_AccountErrors_MessagesUsableByUi()
    {
        var admin = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var missingName = await PostRawAsync(admin, "/api/users", new JsonObject { ["username"] = $"ui_{suffix}", ["fullName"] = "" });
        Assert.Equal(HttpStatusCode.BadRequest, missingName.StatusCode);
        Assert.Contains("Họ và tên", await MessageAsync(missingName));

        await Data(await PostRawAsync(admin, "/api/users", new JsonObject { ["username"] = $"ui_{suffix}", ["fullName"] = "Một" }));
        var duplicate = await PostRawAsync(admin, "/api/users", new JsonObject { ["username"] = $"UI_{suffix}", ["fullName"] = "Hai" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var badDate = await PostRawAsync(admin, "/api/users", new JsonObject { ["username"] = $"ui9_{suffix}", ["fullName"] = "Ba", ["dateOfBirth"] = "2999-01-01" });
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
        var badEmail = await PostRawAsync(admin, "/api/users", new JsonObject { ["username"] = $"ui8_{suffix}", ["fullName"] = "Bốn", ["email"] = "khong-hop-le" });
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        var needsReason = await PostRawAsync(admin, "/api/users", new JsonObject { ["username"] = $"ui7_{suffix}", ["fullName"] = "Năm", ["approvalAuthority"] = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, needsReason.StatusCode);
        foreach (var response in new[] { missingName, duplicate, badDate, badEmail, needsReason })
            AssertWording(await MessageAsync(response));
    }

    #endregion

    #region A3 — vai trò và ma trận quyền

    [SkippableFact]
    public async Task A3_Roles_CreateEditPermissionsDelete_AsSentByUi()
    {
        var admin = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];

        // RoleFormModal chỉ gửi name và description.
        var role = await Data(await PostRawAsync(admin, "/api/admin/roles", new { name = $"Vai trò UI {suffix}", description = "" }));
        var roleId = role.GetProperty("id").GetString()!;
        Assert.Equal(0, role.GetProperty("permissionCodes").GetArrayLength());

        var modules = await Data(admin, "/api/admin/permissions");
        var codes = modules.EnumerateArray().SelectMany(m => m.GetProperty("permissions").EnumerateArray())
            .Select(p => p.GetProperty("code").GetString()!).Where(c => c is PermissionCodes.EvaluationRead or PermissionCodes.ReportExport).ToArray();
        var saved = await Data(await PutRawAsync(admin, $"/api/admin/roles/{roleId}/permissions", new { permissionCodes = codes }));
        Assert.Equal(codes.Length, saved.GetProperty("permissionCodes").GetArrayLength());

        var renamed = await Data(await PutRawAsync(admin, $"/api/admin/roles/{roleId}", new { name = $"Vai trò UI {suffix} (sửa)", description = "Mô tả" }));
        Assert.Equal("Mô tả", renamed.GetProperty("description").GetString());

        var listedRole = await Data(admin, $"/api/admin/roles/{roleId}");
        Assert.Equal(0, listedRole.GetProperty("assignmentCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/admin/roles/{roleId}")).StatusCode);

        // Vai trò được bảo vệ: xóa bị chặn bằng 409 kèm lý do.
        var all = await Data(admin, "/api/admin/roles");
        var protectedRole = all.EnumerateArray().First(r => r.GetProperty("isProtected").GetBoolean());
        var refused = await admin.DeleteAsync($"/api/admin/roles/{protectedRole.GetProperty("id").GetString()}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    }

    #endregion

    #region A4 — danh mục tổ chức

    [SkippableFact]
    public async Task A4_Catalog_UnitTypesUnitsPositionsHolidays_AsSentByUi()
    {
        var admin = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];

        // Loại đơn vị: UnitTypesTab gửi name, side, sortOrder, isActive.
        var type = await Data(await PostRawAsync(admin, "/api/organizations/unit-types",
            new { name = $"Loại UI {suffix}", side = "Administrative", sortOrder = 5, isActive = true }));
        var typeId = type.GetProperty("id").GetString()!;
        await Data(await PutRawAsync(admin, $"/api/organizations/unit-types/{typeId}",
            new { name = $"Loại UI {suffix} (sửa)", side = "Administrative", sortOrder = 6, isActive = true }));

        // Đơn vị gốc rồi đơn vị con (OrgTreeTab): mã chữ hoa, mô tả rỗng; sửa chuyển về gốc bằng Guid rỗng.
        var root = await Data(await PostRawAsync(admin, "/api/organizations/departments", new
        {
            code = $"UR-{suffix}".ToUpperInvariant(), name = $"Gốc {suffix}", description = "", sortOrder = 0, isActive = true,
            parentId = await RootIdAsync(admin, "departments"), unitTypeId = typeId
        }));
        var child = await Data(await PostRawAsync(admin, "/api/organizations/departments", new
        {
            code = $"UC-{suffix}".ToUpperInvariant(), name = $"Con {suffix}", description = "", sortOrder = 1, isActive = true,
            parentId = root.GetProperty("id").GetString(), unitTypeId = typeId
        }));
        Assert.Equal(root.GetProperty("id").GetString(), child.GetProperty("parentId").GetString());
        var childId = child.GetProperty("id").GetString()!;
        var moved = await Data(await PutRawAsync(admin, $"/api/organizations/departments/{childId}", new
        {
            code = child.GetProperty("code").GetString(), name = $"Con {suffix}", description = "Mô tả", sortOrder = 1, isActive = true,
            parentId = root.GetProperty("id").GetString(), unitTypeId = typeId
        }));
        Assert.Equal("Mô tả", moved.GetProperty("description").GetString());
        // Ngừng hoạt động như nút trong menu thao tác (chỉ gửi isActive).
        var inactive = await Data(await PutRawAsync(admin, $"/api/organizations/departments/{childId}", new { isActive = false }));
        Assert.False(inactive.GetProperty("isActive").GetBoolean());
        // Tổ chức Đảng: để trống mã → hệ thống tự sinh.
        var partyType = await FirstUnitTypeAsync(admin, "Party");
        var cell = await Data(await PostRawAsync(admin, "/api/organizations/branches", new
        {
            code = "", name = $"Chi bộ UI {suffix}", description = "", sortOrder = 0, isActive = true,
            parentId = await RootIdAsync(admin, "branches"), unitTypeId = partyType
        }));
        Assert.False(string.IsNullOrWhiteSpace(cell.GetProperty("code").GetString()));

        // Chức vụ (PositionsTab).
        var position = await Data(await PostRawAsync(admin, "/api/positions", new JsonObject
        {
            ["name"] = $"Chức vụ UI {suffix}", ["side"] = "Administrative", ["statCode"] = "", ["formLayoutId"] = "",
            ["defaultApprovalAuthority"] = "", ["defaultWorkflowProfileCode"] = "", ["isLeadership"] = false, ["isHead"] = false,
            ["sortOrder"] = 50, ["isActive"] = true
        }));
        var positionId = position.GetProperty("id").GetString()!;
        var editedPosition = await Data(await PutRawAsync(admin, $"/api/positions/{positionId}", new JsonObject
        {
            ["name"] = $"Chức vụ UI {suffix}", ["side"] = "Administrative", ["statCode"] = "M7", ["formLayoutId"] = "",
            ["defaultApprovalAuthority"] = "CoSo", ["defaultWorkflowProfileCode"] = "", ["isLeadership"] = true, ["isHead"] = false,
            ["sortOrder"] = 50, ["isActive"] = true
        }));
        Assert.Equal("M7", editedPosition.GetProperty("statCode").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/positions/{positionId}")).StatusCode);
        var workflowProfiles = await Data(admin, "/api/positions/workflow-profiles");
        Assert.Equal(JsonValueKind.Array, workflowProfiles.ValueKind);

        // Ngày nghỉ: thêm khoảng ngày (endDate), sửa (endDate null), xóa.
        var year = DateTime.UtcNow.Year + 3;
        var holidays = await Data(await PostRawAsync(admin, "/api/holidays", new JsonObject
        {
            ["date"] = $"{year}-09-01", ["endDate"] = $"{year}-09-03", ["name"] = $"Quốc khánh {suffix}"
        }));
        Assert.Equal(3, holidays.GetArrayLength());
        var first = holidays.EnumerateArray().First();
        var updated = await Data(await PutRawAsync(admin, $"/api/holidays/{first.GetProperty("id").GetString()}", new JsonObject
        {
            ["date"] = first.GetProperty("date").GetString(), ["endDate"] = null, ["name"] = "Nghỉ lễ"
        }));
        Assert.Equal("Nghỉ lễ", updated.GetProperty("name").GetString());
        Assert.Equal(3, (await Data(admin, $"/api/holidays?year={year}")).GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/holidays/{first.GetProperty("id").GetString()}")).StatusCode);

        // Xóa cha còn con → 409 kèm lý do hiểu được.
        var blocked = await admin.DeleteAsync($"/api/organizations/departments/{root.GetProperty("id").GetString()}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        AssertWording(await MessageAsync(blocked));
    }

    #endregion

    #region A5 — bộ tiêu chí

    [SkippableFact]
    public async Task A5_CriteriaSets_CreateEditWholeContentPublishCloneArchiveDelete_AsSentByUi()
    {
        var admin = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        // Trang danh sách gửi {code, name, notes}; mã nhập chữ thường được chuẩn hóa.
        var created = await Data(await PostRawAsync(admin, "/api/criteria-sets", new { code = $"ui-{suffix}", name = $"Bộ UI {suffix}", notes = "" }));
        var id = created.GetProperty("id").GetString()!;
        Assert.Equal($"UI-{suffix}", created.GetProperty("code").GetString());

        // Trình soạn: tải về, sao chép sâu rồi lưu nguyên cấu trúc cùng thông tin chung.
        var loaded = await Data(admin, $"/api/criteria-sets/{id}");
        var content = JsonNode.Parse(loaded.GetProperty("content").GetRawText())!;
        var version = loaded.GetProperty("version").GetUInt32();
        var saved = await Data(await PutRawAsync(admin, $"/api/criteria-sets/{id}", new JsonObject
        {
            ["version"] = version, ["code"] = $"ui-{suffix}", ["name"] = $"Bộ UI {suffix}", ["notes"] = "", ["content"] = content.DeepClone()
        }));
        Assert.Equal(0, saved.GetProperty("validationErrors").GetArrayLength());

        // Sửa một số ô như giao diện: tên trục, khung tỷ trọng (giá trị / 100), ngưỡng, tham số số nguyên, xóa ô vượt chuẩn.
        var edit = JsonNode.Parse(saved.GetProperty("content").GetRawText())!;
        edit["axes"]![0]!["description"] = "";
        edit["weightFrames"]![0]!["a"] = Math.Round(edit["weightFrames"]![0]!["a"]!.GetValue<double>() * 100) / 100;
        edit["grades"]![0]!["minExceedStandardRatio"] = null;
        edit["parameters"]!["deductionReasonMinPoints"] = null;
        edit["parameters"]!["frameAdjustLimit"] = 10;
        edit["parameters"]!["improvementPlanAlertDays"] = 120;
        edit["parameters"]!["improvementPlanRequiredGrades"] = new JsonArray("HoanThanh");
        edit["parameters"]!["excellentQuota"] = new JsonObject { ["ratio"] = 0.25 };
        edit["taskLineLabel"] = null;
        edit["selfAssessmentSections"]![0]!["guidance"] = null;
        var saved2 = await Data(await PutRawAsync(admin, $"/api/criteria-sets/{id}", new JsonObject
        {
            ["version"] = saved.GetProperty("version").GetUInt32(), ["code"] = $"ui-{suffix}", ["name"] = $"Bộ UI {suffix} v2", ["notes"] = "Ghi chú", ["content"] = edit
        }));
        Assert.Equal(120, saved2.GetProperty("content").GetProperty("parameters").GetProperty("improvementPlanAlertDays").GetInt32());
        Assert.Equal("HoanThanh", saved2.GetProperty("content").GetProperty("parameters").GetProperty("improvementPlanRequiredGrades")[0].GetString());

        // Phiên bản cũ → 409; thiếu phiên bản → 400.
        var stale = await PutRawAsync(admin, $"/api/criteria-sets/{id}", new JsonObject { ["version"] = version, ["name"] = "Cũ" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        // Xuất bản gửi {version}; bản đã xuất bản không sửa được.
        var published = await Data(await PostRawAsync(admin, $"/api/criteria-sets/{id}/publish", new { version = saved2.GetProperty("version").GetUInt32() }));
        Assert.Equal("Published", published.GetProperty("status").GetString());
        var editPublished = await PutRawAsync(admin, $"/api/criteria-sets/{id}", new JsonObject { ["version"] = published.GetProperty("version").GetUInt32(), ["name"] = "Sửa" });
        Assert.Equal(HttpStatusCode.Conflict, editPublished.StatusCode);

        // Nhân bản không gửi gì (thân rỗng) → bản nháp mới có mã sinh tự động; xóa bản nháp bằng ?version=.
        var clone = await Data(await PostRawAsync(admin, $"/api/criteria-sets/{id}/clone", new { }));
        Assert.Equal("Draft", clone.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/criteria-sets/{clone.GetProperty("id").GetString()}?version={clone.GetProperty("version").GetUInt32()}")).StatusCode);

        var archived = await Data(await PostRawAsync(admin, $"/api/criteria-sets/{id}/archive", new { version = published.GetProperty("version").GetUInt32() }));
        Assert.Equal("Archived", archived.GetProperty("status").GetString());

        var list = await Data(admin, "/api/criteria-sets");
        Assert.Contains(list.EnumerateArray(), s => s.GetProperty("id").GetString() == id);
        Assert.True((await Data(admin, "/api/criteria-sets/weight-frames")).GetProperty("frames").GetArrayLength() >= 0);
        Assert.True((await Data(admin, "/api/criteria-sets/defaults/collective-groups")).GetArrayLength() > 0);
    }

    [SkippableFact]
    public async Task A6_CriteriaSets_InvalidDraft_ErrorsPerField_PublishRefusedWithReason()
    {
        var admin = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var created = await Data(await PostRawAsync(admin, "/api/criteria-sets", new { code = $"BAD-{suffix}", name = $"Bộ lỗi {suffix}", notes = "" }));
        var id = created.GetProperty("id").GetString()!;
        var edit = JsonNode.Parse(created.GetProperty("content").GetRawText())!;
        edit["parameters"]!["generalMaxScore"] = 999;
        edit["parameters"]!["excellentQuota"] = new JsonObject { ["ratio"] = 0.5 };
        edit["weightFrames"]![0]!["a"] = 0.9;
        edit["parameters"]!["improvementPlanAlertDays"] = 0;
        var saved = await Data(await PutRawAsync(admin, $"/api/criteria-sets/{id}", new JsonObject
        {
            ["version"] = created.GetProperty("version").GetUInt32(), ["code"] = $"BAD-{suffix}", ["name"] = $"Bộ lỗi {suffix}", ["notes"] = "", ["content"] = edit
        }));
        var errors = saved.GetProperty("validationErrors").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.True(errors.Count >= 3, string.Join(" | ", errors));
        var refused = await PostRawAsync(admin, $"/api/criteria-sets/{id}/publish", new { version = saved.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("chưa xuất bản được", await MessageAsync(refused));
    }

    #endregion

    #region A7 — thông tin đơn vị, biểu mẫu Word, nhật ký

    [SkippableFact]
    public async Task A7_OrganizationSettings_Templates_Audit_AsSentByUi()
    {
        var admin = await AdminAsync();
        var current = await Data(admin, "/api/settings/organization");
        // Trang cài đặt gửi đủ tám ô chữ và vị trí khung đăng nhập.
        var payload = new JsonObject
        {
            ["partyCommitteeName"] = current.GetProperty("partyCommitteeName").GetString(),
            ["superiorPartyName"] = current.GetProperty("superiorPartyName").GetString(),
            ["companyName"] = current.GetProperty("companyName").GetString(),
            ["parentCompanyName"] = current.GetProperty("parentCompanyName").GetString(),
            ["shortName"] = current.GetProperty("shortName").GetString(),
            ["location"] = current.GetProperty("location").GetString(),
            ["appraisalUnitName"] = "",
            ["systemName"] = current.GetProperty("systemName").GetString(),
            ["loginPanelPosition"] = "left"
        };
        var saved = await Data(await PutRawAsync(admin, "/api/settings/organization", payload));
        Assert.Equal("left", saved.GetProperty("loginPanelPosition").GetString());
        Assert.Equal("", saved.GetProperty("appraisalUnitName").GetString());
        var blank = (JsonObject)JsonNode.Parse(payload.ToJsonString())!;
        blank["companyName"] = "";
        var refused = await PutRawAsync(admin, "/api/settings/organization", blank);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        AssertWording(await MessageAsync(refused));

        // Ảnh nền đăng nhập: multipart với trường "file".
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "nen.png");
        var uploaded = await Data(await admin.PostAsync("/api/settings/organization/login-background", form));
        Assert.False(string.IsNullOrEmpty(uploaded.GetProperty("loginBackgroundUrl").GetString()));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/settings/organization/login-background")).StatusCode);
        var removed = await Data(await admin.DeleteAsync("/api/settings/organization/login-background"));
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("loginBackgroundUrl").ValueKind);

        // Biểu mẫu Word: danh mục, tải bản gốc rồi kiểm tra và tải lên làm phiên bản mới (activate là chuỗi "true").
        var templates = await Data(admin, "/api/templates");
        var template = templates.EnumerateArray().First();
        var code = template.GetProperty("code").GetString()!;
        var original = await admin.GetAsync($"/api/templates/{code}/original");
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var bytes = await original.Content.ReadAsByteArrayAsync();
        using var checkForm = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "mau.docx" } };
        var check = await Data(await admin.PostAsync($"/api/templates/{code}/check", checkForm));
        Assert.True(check.GetProperty("isValid").GetBoolean(), check.GetRawText());
        using var uploadForm = new MultipartFormDataContent
        {
            { new ByteArrayContent(bytes), "file", "mau.docx" }, { new StringContent(""), "note" }, { new StringContent("true"), "activate" }
        };
        var upload = await Data(await admin.PostAsync($"/api/templates/{code}/versions", uploadForm));
        Assert.True(upload.GetProperty("isValid").GetBoolean());
        var versions = await Data(admin, $"/api/templates/{code}/versions");
        Assert.True(versions.GetArrayLength() >= 1);
        var versionId = versions.EnumerateArray().First().GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/templates/{code}/versions/{versionId}/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/templates/{code}/current")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/templates/{code}/use-original", null)).StatusCode);
        // Tệp không phải Word: kết quả kiểm tra có danh sách lỗi để giao diện hiện.
        using var badForm = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes("không phải docx")), "file", "sai.docx" } };
        var bad = await admin.PostAsync($"/api/templates/{code}/versions", badForm);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        AssertWording(await MessageAsync(bad));

        // Nhật ký: bộ lọc như trang Nhật ký hệ thống và nhật ký đăng nhập.
        var audit = await Data(admin, "/api/admin/audit?entityType=CriteriaSet&page=1&pageSize=20");
        Assert.True(audit.TryGetProperty("actionCounts", out _) && audit.TryGetProperty("entityTypes", out _) && audit.TryGetProperty("actorCount", out _));
        // Đang lọc một loại thao tác: ô số liệu và danh sách thao tác vẫn đủ các loại khác, tổng chỉ tính loại đang lọc.
        var onlyCreate = await Data(admin, "/api/admin/audit?action=Create&page=1&pageSize=20");
        var counts = onlyCreate.GetProperty("actionCounts").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
        Assert.True(counts.Count > 1 && counts.ContainsKey("Update"), string.Join(",", counts.Keys));
        Assert.Equal(counts["Create"], onlyCreate.GetProperty("totalCount").GetInt32());
        var logins = await Data(admin, $"/api/audit/logins?result=Success&from={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("O"))}&page=1&pageSize=50");
        Assert.True(logins.GetProperty("totalCount").GetInt32() >= 1);
    }

    #endregion

    #region Hỗ trợ

    private async Task<HttpClient> AdminAsync()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        await Gate.WaitAsync();
        try
        {
            if (Admins.TryGetValue(_factory.DatabaseName, out var existing))
                return existing;
            var user = await _factory.CreateUserAsync(fullName: "Quản trị UI");
            await _factory.AssignAsync(user.Id, await _factory.GetAdministratorRoleIdAsync(), RoleScopeType.Global, null);
            await _factory.AssignAsync(user.Id, (await _factory.CreateRoleAsync(PermissionCodes.All)).Id, RoleScopeType.Global, null);
            var client = await _factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
            Admins[_factory.DatabaseName] = client;
            return client;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Mỗi cây chỉ một gốc: đơn vị mới của test treo dưới gốc hiện có (như giao diện bắt chọn cấp trên).</summary>
    private static async Task<string?> RootIdAsync(HttpClient admin, string kind)
    {
        var items = await Data(admin, $"/api/organizations/{kind}");
        var root = items.EnumerateArray().Where(i => i.GetProperty("parentId").ValueKind == JsonValueKind.Null).ToList();
        return root.Count == 0 ? null : root[0].GetProperty("id").GetString();
    }

    private static async Task<string?> FirstUnitTypeAsync(HttpClient admin, string side)
    {
        var types = await Data(admin, "/api/organizations/unit-types");
        return types.EnumerateArray().FirstOrDefault(t => t.GetProperty("side").GetString() == side && t.GetProperty("isActive").GetBoolean())
            .ValueKind == JsonValueKind.Undefined
            ? null
            : types.EnumerateArray().First(t => t.GetProperty("side").GetString() == side && t.GetProperty("isActive").GetBoolean()).GetProperty("id").GetString();
    }

    private static Task<HttpResponseMessage> PostRawAsync(HttpClient client, string url, object body) =>
        client.PostAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> PutRawAsync(HttpClient client, string url, object body) =>
        client.PutAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    /// <summary>Thông báo lỗi viết "… không thành công", không viết "Không … được".</summary>
    private static void AssertWording(string message) =>
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(message, @"Không[^.]*được"), message);

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    #endregion
}
