using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Phiếu đối soát chuyên ngành (Mẫu 03): phạm vi theo đơn vị đối soát, người lập khác người xác nhận, chặn hoàn thành thẩm định.</summary>
[Collection(ApiCollection.Name)]
public sealed class ExpertCrosscheckIntegrationTests
{
    private readonly ApiFactory _factory;

    public ExpertCrosscheckIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Crosscheck_Scope_Coi_Flow_AndAppraisalGate()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await WorldAsync();
        var recordId = await AppraisalRecordAsync(w);
        var url = $"/api/evaluations/records/{recordId}/crosschecks";

        // Chỉ người có quyền yêu cầu mới tạo được; chủ hồ sơ và cơ quan chuyên môn đều bị chặn.
        var body = Request(w);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.PostAsJsonAsync(url, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ResponderC.PostAsJsonAsync(url, body)).StatusCode);
        var created = await Data(await w.ManagerC.PostAsJsonAsync(url, body));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Requested", created.GetProperty("status").GetString());
        Assert.True(created.GetProperty("canCancel").GetBoolean());

        // Trùng đơn vị + lĩnh vực khi còn mở → 409.
        Assert.Equal(HttpStatusCode.Conflict, (await w.ManagerC.PostAsJsonAsync(url, body)).StatusCode);

        // Chủ hồ sơ không xem phiếu về mình; cơ quan chuyên môn không có evaluation.read vẫn xem phiếu gửi đến đơn vị mình,
        // còn cơ quan đơn vị khác thì không.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.GetAsync($"/api/crosschecks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ResponderC.GetAsync($"/api/evaluations/records/{recordId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await w.ResponderC.GetAsync($"/api/crosschecks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OtherExpertC.GetAsync($"/api/crosschecks/{id}")).StatusCode);
        Assert.Contains((await Data(w.ResponderC, "/api/crosschecks")).EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.DoesNotContain((await Data(w.OtherExpertC, "/api/crosschecks")).EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);

        // Việc cần xử lý của cơ quan đối soát.
        var queue = await Data(w.ResponderC, "/api/evaluations/work-queue");
        Assert.Contains(queue.GetProperty("groups").EnumerateArray(), g => g.GetProperty("step").GetString() == "CROSSCHECKS"
            && g.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("formId").GetGuid() == id));

        // Thẩm định bị chặn khi còn yêu cầu mở.
        var blocked = await w.ManagerC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/appraisal", await AppraiseBodyAsync(w, recordId));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("phiếu đối soát", await MessageAsync(blocked));
        // Danh sách hành động báo khóa thẩm định đúng như lúc ghi nhận bị từ chối.
        var actions = await Data(w.ManagerC, $"/api/evaluations/records/{recordId}/actions");
        var appraise = actions.GetProperty("actions").EnumerateArray().Single(a => a.GetProperty("action").GetString() == "Appraise");
        Assert.Equal("CROSSCHECK_OPEN", appraise.GetProperty("blockedBy").GetString());
        Assert.Contains("phiếu đối soát", appraise.GetProperty("blockedReason").GetString());

        // Chủ hồ sơ có đủ quyền đối soát vẫn không thao tác được phiếu về mình.
        var ownerRole = await _factory.CreateRoleAsync(PermissionCodes.EvaluationCrosscheckRequest, PermissionCodes.EvaluationCrosscheckRespond, PermissionCodes.EvaluationCrosscheckConfirm);
        await _factory.AssignAsync(w.Owner.Id, ownerRole.Id, RoleScopeType.Global, null);
        var ownerVersion = created.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.PostAsJsonAsync(url, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.PutAsJsonAsync($"/api/crosschecks/{id}", new { version = ownerVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.PostAsJsonAsync($"/api/crosschecks/{id}/cancel", new { version = ownerVersion, reason = "x" })).StatusCode);
        foreach (var action in new[] { "submit", "confirm", "return" })
            Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.PostAsJsonAsync($"/api/crosschecks/{id}/{action}", new { version = ownerVersion, reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.PutAsJsonAsync($"/api/crosschecks/{id}/response", new { version = ownerVersion, items = Array.Empty<object>() })).StatusCode);

        // Lập → gửi; người lập không tự xác nhận; người xác nhận khác đơn vị bị chặn.
        var detail = await Data(w.ResponderC, $"/api/crosschecks/{id}");
        var items = detail.GetProperty("items").EnumerateArray().Select(i => new
        {
            id = i.GetProperty("id").GetGuid(), sourceData = "Khớp hệ thống", result = "Defect", adjustmentType = "Decrease",
            adjustmentPoints = 1.5, basisText = "Báo cáo số 12/BC-ATCL", basisDocNumber = "12/BC-ATCL"
        }).ToArray();
        var saved = await Data(await w.ResponderC.PutAsJsonAsync($"/api/crosschecks/{id}/response",
            new { version = detail.GetProperty("version").GetUInt32(), items }));
        Assert.Equal("InProgress", saved.GetProperty("status").GetString());
        Assert.Equal("−1,5đ", saved.GetProperty("items")[0].GetProperty("adjustmentText").GetString());
        var submitted = await Data(await w.ResponderC.PostAsJsonAsync($"/api/crosschecks/{id}/submit",
            new { version = saved.GetProperty("version").GetUInt32() }));
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        var version = submitted.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ResponderC.PostAsJsonAsync($"/api/crosschecks/{id}/confirm", new { version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OtherExpertC.PostAsJsonAsync($"/api/crosschecks/{id}/confirm", new { version })).StatusCode);

        // Trả lại cần lý do, rồi lập lại, gửi lại và xác nhận.
        Assert.Equal(HttpStatusCode.BadRequest, (await w.ConfirmerC.PostAsJsonAsync($"/api/crosschecks/{id}/return", new { version, reason = " " })).StatusCode);
        var returned = await Data(await w.ConfirmerC.PostAsJsonAsync($"/api/crosschecks/{id}/return", new { version, reason = "Thiếu số liệu" }));
        Assert.Equal("InProgress", returned.GetProperty("status").GetString());
        var resubmitted = await Data(await w.ResponderC.PostAsJsonAsync($"/api/crosschecks/{id}/submit",
            new { version = returned.GetProperty("version").GetUInt32() }));
        var confirmed = await Data(await w.ConfirmerC.PostAsJsonAsync($"/api/crosschecks/{id}/confirm",
            new { version = resubmitted.GetProperty("version").GetUInt32() }));
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());
        Assert.Equal(new[] { "Create", "Submit", "Return", "Submit", "Confirm" },
            confirmed.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("action").GetString()).ToArray());

        // Đã xác nhận: khóa sửa và hết chặn thẩm định (nếu còn lỗi thì là lỗi dữ liệu, không phải phiếu đối soát).
        var confirmedVersion = confirmed.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await w.ResponderC.PutAsJsonAsync($"/api/crosschecks/{id}/response", new { version = confirmedVersion, items })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.ManagerC.PostAsJsonAsync($"/api/crosschecks/{id}/cancel", new { version = confirmedVersion, reason = "x" })).StatusCode);
        var afterConfirm = await w.ManagerC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/appraisal", await AppraiseBodyAsync(w, recordId));
        Assert.DoesNotContain("phiếu đối soát", await MessageAsync(afterConfirm));
    }

    [SkippableFact]
    public async Task Crosscheck_Cancel_NeedsReason_AndReleasesGate()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await WorldAsync();
        var recordId = await AppraisalRecordAsync(w);
        var created = await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/crosschecks", Request(w)));
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        Assert.Equal(HttpStatusCode.BadRequest, (await w.ManagerC.PostAsJsonAsync($"/api/crosschecks/{id}/cancel", new { version, reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ResponderC.PostAsJsonAsync($"/api/crosschecks/{id}/cancel", new { version, reason = "x" })).StatusCode);
        var cancelled = await Data(await w.ManagerC.PostAsJsonAsync($"/api/crosschecks/{id}/cancel", new { version, reason = "Đã có văn bản giấy" }));
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());

        var response = await w.ManagerC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/appraisal", await AppraiseBodyAsync(w, recordId));
        Assert.DoesNotContain("phiếu đối soát", await MessageAsync(response));
    }

    [SkippableFact]
    public async Task Crosscheck_BodiesLikeUi_CreateEditRespondSubmitConfirm()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await WorldAsync();
        var recordId = await AppraisalRecordAsync(w);
        Guid taskId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var task = new EvaluationTask { RecordId = recordId, TaskOrder = 1, TaskName = "Sản phẩm đối soát", Weight = 10, SelfScore = 10, Deadline = DateTime.UtcNow };
            db.EvaluationTasks.Add(task);
            await db.SaveChangesAsync();
            taskId = task.Id;
        });

        // Khối hồ sơ: người yêu cầu thấy nút yêu cầu, chưa có phiếu.
        var panel = await Data(w.ManagerC, $"/api/evaluations/records/{recordId}/crosschecks");
        Assert.True(panel.GetProperty("applicable").GetBoolean());
        Assert.True(panel.GetProperty("canRequest").GetBoolean());

        // Nội dung như CrosscheckRequestForm gửi: không có version, cơ quan ngoài hệ thống null, dòng sản phẩm kèm taskId, ô trống thành null.
        var create = await UiAsync(w.ManagerC, HttpMethod.Post, $"/api/evaluations/records/{recordId}/crosschecks", new
        {
            expertDepartmentId = w.ExpertDept, expertAgencyName = (string?)null, fields = new[] { "SAFETY_QUALITY", "FINANCE" },
            addresseeName = "", requestReason = "Đối soát số liệu quý",
            items = new object[]
            {
                new { taskId, itemName = "Sản phẩm đối soát", selfData = (string?)null },
                new { taskId = (Guid?)null, itemName = "Dòng tự do", selfData = "12 sự cố" }
            }
        });
        var created = await Data(create);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(2, created.GetProperty("items").GetArrayLength());
        Assert.Equal(taskId, created.GetProperty("items")[0].GetProperty("taskId").GetGuid());
        Assert.True(created.GetProperty("canEditRequest").GetBoolean());

        // Sửa yêu cầu: gửi lại version và các dòng như form sửa (dòng sản phẩm giữ taskId).
        var edited = await Data(await UiAsync(w.ManagerC, HttpMethod.Put, $"/api/crosschecks/{id}", new
        {
            version = created.GetProperty("version").GetUInt32(), expertDepartmentId = w.ExpertDept, expertAgencyName = (string?)null,
            fields = new[] { "SAFETY_QUALITY" }, addresseeName = "Phòng chuyên môn", requestReason = "Đối soát số liệu quý (sửa)",
            items = new object[]
            {
                new { taskId, itemName = "Sản phẩm đối soát", selfData = "ghi chú" },
                new { taskId = (Guid?)null, itemName = "Dòng tự do", selfData = "12 sự cố" }
            }
        }));
        Assert.Equal("Phòng chuyên môn", edited.GetProperty("addresseeName").GetString());

        // Lưu nháp như trang phiếu: mọi trường có mặt, ô trống là chuỗi rỗng hoặc null, dòng không áp dụng.
        var rows = edited.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        object Row(Guid rowId, string src, string? result, string adj, double? points, string basis, string? docDate, bool na) => new
        {
            id = rowId, sourceData = src, result, adjustmentType = adj, adjustmentPoints = points, basisText = basis,
            basisDocNumber = "", basisDocDate = docDate, basisAgency = "", notApplicable = na
        };
        var draft = await Data(await UiAsync(w.ResponderC, HttpMethod.Put, $"/api/crosschecks/{id}/response", new
        {
            version = edited.GetProperty("version").GetUInt32(), issuedDate = (string?)null,
            items = new[] { Row(rows[0], "", null, "None", null, "", null, false), Row(rows[1], "", null, "None", null, "", null, true) }
        }));
        Assert.Equal("InProgress", draft.GetProperty("status").GetString());
        Assert.True(draft.GetProperty("canSubmit").GetBoolean());

        // Gửi khi dòng đầu còn thiếu kết luận: thông báo nêu rõ cần nhập gì.
        var early = await UiAsync(w.ResponderC, HttpMethod.Post, $"/api/crosschecks/{id}/submit", new { version = draft.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Contains("kết luận", await MessageAsync(early));

        // Nhập đủ: vượt chuẩn ghi nhận, ngày văn bản dạng yyyy-MM-dd, ngày lập phiếu.
        var full = await Data(await UiAsync(w.ResponderC, HttpMethod.Put, $"/api/crosschecks/{id}/response", new
        {
            version = draft.GetProperty("version").GetUInt32(), issuedDate = "2026-10-26",
            items = new[] { Row(rows[0], "Khớp", "Exceed", "RecognizeExceed", null, "Báo cáo", "2026-10-05", false), Row(rows[1], "", null, "None", null, "", null, true) }
        }));
        Assert.Equal("2026-10-26", full.GetProperty("issuedDate").GetString());
        Assert.Equal("K/AD", full.GetProperty("items")[1].GetProperty("adjustmentText").GetString());
        var submitted = await Data(await UiAsync(w.ResponderC, HttpMethod.Post, $"/api/crosschecks/{id}/submit", new { version = full.GetProperty("version").GetUInt32() }));
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());

        // Người xác nhận thấy cờ đúng; trả lại thì người lập lập lại được.
        var asConfirmer = await Data(w.ConfirmerC, $"/api/crosschecks/{id}");
        Assert.True(asConfirmer.GetProperty("canConfirm").GetBoolean());
        Assert.True(asConfirmer.GetProperty("canReturn").GetBoolean());
        Assert.False(asConfirmer.GetProperty("canCancel").GetBoolean());
        var returned = await Data(await UiAsync(w.ConfirmerC, HttpMethod.Post, $"/api/crosschecks/{id}/return", new { version = asConfirmer.GetProperty("version").GetUInt32(), reason = "Bổ sung" }));
        Assert.True((await Data(w.ResponderC, $"/api/crosschecks/{id}")).GetProperty("canSubmit").GetBoolean());
        Assert.False(returned.GetProperty("canConfirm").GetBoolean());
    }

    [SkippableFact]
    public async Task Crosscheck_ExternalAgency_RequesterEntersResultOnBehalf()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await WorldAsync();
        var recordId = await AppraisalRecordAsync(w);

        var created = await Data(await UiAsync(w.ManagerC, HttpMethod.Post, $"/api/evaluations/records/{recordId}/crosschecks", new
        {
            expertDepartmentId = (Guid?)null, expertAgencyName = "Cục Hàng không", fields = new[] { "ATM" },
            addresseeName = "", requestReason = "Xác nhận số liệu",
            items = new[] { new { taskId = (Guid?)null, itemName = "Chỉ tiêu ngoài", selfData = (string?)null } }
        }));
        var id = created.GetProperty("id").GetGuid();
        Assert.True(created.GetProperty("enteredOnBehalf").GetBoolean());
        Assert.True(created.GetProperty("canRespond").GetBoolean());
        Assert.Equal("Cục Hàng không", created.GetProperty("expertUnitName").GetString());

        // Người yêu cầu nhập thay và gửi; phiếu chờ người khác có quyền xác nhận ở đơn vị của hồ sơ.
        var rowId = created.GetProperty("items")[0].GetProperty("id").GetGuid();
        var saved = await Data(await UiAsync(w.ManagerC, HttpMethod.Put, $"/api/crosschecks/{id}/response", new
        {
            version = created.GetProperty("version").GetUInt32(), issuedDate = (string?)null,
            items = new[] { new { id = rowId, sourceData = "Khớp văn bản", result = (string?)"Pass", adjustmentType = "None", adjustmentPoints = (double?)null,
                basisText = "", basisDocNumber = "", basisDocDate = (string?)null, basisAgency = "", notApplicable = false } }
        }));
        var submitted = await Data(await UiAsync(w.ManagerC, HttpMethod.Post, $"/api/crosschecks/{id}/submit", new { version = saved.GetProperty("version").GetUInt32() }));
        Assert.False(submitted.GetProperty("canConfirm").GetBoolean());

        var confirmRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationCrosscheckConfirm)).Id;
        var leader = await _factory.CreateUserAsync(w.Dept, null);
        await _factory.AssignAsync(leader.Id, confirmRole, RoleScopeType.Department, w.Dept);
        var leaderC = await _factory.LoginAsAsync(leader.Username, leader.Password, distinctClientIp: true);
        var seen = await Data(leaderC, $"/api/crosschecks/{id}");
        Assert.True(seen.GetProperty("canConfirm").GetBoolean());
        var confirmed = await Data(await UiAsync(leaderC, HttpMethod.Post, $"/api/crosschecks/{id}/confirm", new { version = seen.GetProperty("version").GetUInt32() }));
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task Crosscheck_ExternalAgency_PartyOfficeConfirms_ButNotOwnRecord()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await WorldAsync();
        var office = await _factory.CreateUserAsync(w.Dept, w.Cell, ApprovalAuthority.CoSo, "DS Văn phòng Đảng ủy");
        await _factory.AssignAsync(office.Id, await _factory.GetRoleIdAsync("VAN_PHONG_DANG_UY"), RoleScopeType.Global, null);
        var officeC = await _factory.LoginAsAsync(office.Username, office.Password, distinctClientIp: true);

        // Phiếu của cán bộ khác: người yêu cầu nhập thay và gửi, Văn phòng Đảng ủy xác nhận.
        var id = await SubmittedExternalAsync(w, await AppraisalRecordAsync(w));
        var seen = await Data(officeC, $"/api/crosschecks/{id}");
        Assert.True(seen.GetProperty("canConfirm").GetBoolean());
        var confirmed = await Data(await UiAsync(officeC, HttpMethod.Post, $"/api/crosschecks/{id}/confirm", new { version = seen.GetProperty("version").GetUInt32() }));
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());

        // Phiếu về hồ sơ của chính người này: không thấy, không xác nhận được.
        var ownId = await SubmittedExternalAsync(w, await AppraisalRecordAsync(w, office.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await officeC.GetAsync($"/api/crosschecks/{ownId}")).StatusCode);
        var denied = await UiAsync(officeC, HttpMethod.Post, $"/api/crosschecks/{ownId}/confirm", new { version = 1u });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private static async Task<Guid> SubmittedExternalAsync(World w, Guid recordId)
    {
        var created = await Data(await UiAsync(w.ManagerC, HttpMethod.Post, $"/api/evaluations/records/{recordId}/crosschecks", new
        {
            expertDepartmentId = (Guid?)null, expertAgencyName = "Cục Hàng không", fields = new[] { "ATM" },
            addresseeName = "", requestReason = "Xác nhận số liệu",
            items = new[] { new { taskId = (Guid?)null, itemName = "Chỉ tiêu ngoài", selfData = (string?)null } }
        }));
        var id = created.GetProperty("id").GetGuid();
        var rowId = created.GetProperty("items")[0].GetProperty("id").GetGuid();
        var saved = await Data(await UiAsync(w.ManagerC, HttpMethod.Put, $"/api/crosschecks/{id}/response", new
        {
            version = created.GetProperty("version").GetUInt32(), issuedDate = (string?)null,
            items = new[] { new { id = rowId, sourceData = "Khớp văn bản", result = (string?)"Pass", adjustmentType = "None", adjustmentPoints = (double?)null,
                basisText = "", basisDocNumber = "", basisDocDate = (string?)null, basisAgency = "", notApplicable = false } }
        }));
        await Data(await UiAsync(w.ManagerC, HttpMethod.Post, $"/api/crosschecks/{id}/submit", new { version = saved.GetProperty("version").GetUInt32() }));
        return id;
    }

    #region Hỗ trợ

    /// <summary>Gửi đúng chuỗi JSON như axios của giao diện (giá trị null giữ nguyên, trường undefined bị bỏ).</summary>
    private static Task<HttpResponseMessage> UiAsync(HttpClient client, HttpMethod method, string url, object body) =>
        client.SendAsync(new HttpRequestMessage(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        });

    private sealed class World
    {
        public Guid Dept, ExpertDept, OtherExpertDept, Cell;
        public TestUser Owner = null!;
        public HttpClient OwnerC = null!, ManagerC = null!, ResponderC = null!, ConfirmerC = null!, OtherExpertC = null!;
    }

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();

    private async Task<World> WorldAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (Worlds.TryGetValue(_factory.DatabaseName, out var existing))
                return existing;

            var w = new World();
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            Guid cell = Guid.Empty;
            await _factory.WithDbAsync(async db =>
            {
                var d = new AdministrativeDepartment { Code = $"DS-P-{suffix}", Name = $"Phòng DS {suffix}" };
                var e1 = new AdministrativeDepartment { Code = $"DS-E1-{suffix}", Name = $"Phòng chuyên môn {suffix}" };
                var e2 = new AdministrativeDepartment { Code = $"DS-E2-{suffix}", Name = $"Phòng khác {suffix}" };
                var c = new PartyCell { Code = $"DS-C-{suffix}", Name = $"Chi bộ DS {suffix}" };
                db.AddRange(d, e1, e2, c);
                await db.SaveChangesAsync();
                (w.Dept, w.ExpertDept, w.OtherExpertDept, cell) = (d.Id, e1.Id, e2.Id, c.Id);
                w.Cell = c.Id;
            });

            w.Owner = await _factory.CreateUserAsync(w.Dept, cell, ApprovalAuthority.CoSo, $"DS Chủ hồ sơ {suffix}");
            var manager = await _factory.CreateUserAsync(null, null);
            var responder = await _factory.CreateUserAsync(w.ExpertDept, null);
            var confirmer = await _factory.CreateUserAsync(w.ExpertDept, null);
            var other = await _factory.CreateUserAsync(w.OtherExpertDept, null);
            await _factory.AssignAsync(w.Owner.Id, await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA"), RoleScopeType.Global, null);
            await _factory.AssignAsync(manager.Id, await _factory.GetRoleIdAsync("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
            var respondRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationCrosscheckRespond)).Id;
            var confirmRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationCrosscheckConfirm)).Id;
            await _factory.AssignAsync(responder.Id, respondRole, RoleScopeType.Department, w.ExpertDept);
            await _factory.AssignAsync(confirmer.Id, confirmRole, RoleScopeType.Department, w.ExpertDept);
            await _factory.AssignAsync(other.Id, respondRole, RoleScopeType.Department, w.OtherExpertDept);
            await _factory.AssignAsync(other.Id, confirmRole, RoleScopeType.Department, w.OtherExpertDept);

            w.OwnerC = await _factory.LoginAsAsync(w.Owner.Username, w.Owner.Password, distinctClientIp: true);
            w.ManagerC = await _factory.LoginAsAsync(manager.Username, manager.Password, distinctClientIp: true);
            w.ResponderC = await _factory.LoginAsAsync(responder.Username, responder.Password, distinctClientIp: true);
            w.ConfirmerC = await _factory.LoginAsAsync(confirmer.Username, confirmer.Password, distinctClientIp: true);
            w.OtherExpertC = await _factory.LoginAsAsync(other.Username, other.Password, distinctClientIp: true);
            Worlds[_factory.DatabaseName] = w;
            return w;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Kỳ mới mở, hồ sơ của chủ hồ sơ được đặt thẳng ở bước thẩm định để thử phiếu.</summary>
    private async Task<Guid> AppraisalRecordAsync(World w, Guid? ownerId = null)
    {
        var body = new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter,
            criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(w.ManagerC),
            name = $"Kỳ DS {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        };
        var periodId = (await Data(await w.ManagerC.PostAsJsonAsync("/api/evaluations/periods", body))).GetProperty("id").GetGuid();
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { ownerId ?? w.Owner.Id } }));
        var period = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}");
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), reason = "Kiểm thử đối soát", force = true }));
        var recordId = (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("memberId").GetGuid() == (ownerId ?? w.Owner.Id)).GetProperty("recordId").GetGuid();
        await _factory.WithDbAsync(async db =>
        {
            var record = db.Set<EvaluationRecord>().Single(r => r.Id == recordId);
            record.Status = RecordStatus.AwaitingAppraisal;
            await db.SaveChangesAsync();
        });
        return recordId;
    }

    private static object Request(World w) => new
    {
        expertDepartmentId = w.ExpertDept,
        fields = new[] { "SAFETY_QUALITY" },
        addresseeName = "Phòng chuyên môn",
        requestReason = "Xác nhận số liệu sự cố trong quý",
        items = new object[] { new { itemName = "Số sự cố nghiêm trọng", selfData = "0" } }
    };

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? string.Empty : string.Empty;
    }

    private static async Task<System.Text.Json.Nodes.JsonObject> AppraiseBodyAsync(World w, Guid recordId)
    {
        var record = await Data(w.ManagerC, $"/api/evaluations/records/{recordId}");
        return await AppraisalTestData.BodyAsync(w.ManagerC, recordId, new
        {
            version = record.GetProperty("version").GetUInt32(), explanation = "Căn cứ thẩm định", appraisalScore = 90.0,
            comment = "Đạt", proposedGrade = "HoanThanhTot"
        });
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    #endregion
}
