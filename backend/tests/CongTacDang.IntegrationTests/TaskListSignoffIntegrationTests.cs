using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Bước 1 bốn bước: chủ hồ sơ nộp → Trưởng đơn vị gửi → Phòng TCCB-LĐ rà soát, trình → Giám đốc / Chủ tịch duyệt;
/// Trưởng đơn vị tự đăng ký thì bỏ qua bước gửi; kỳ cũ chưa có hai bước mới vẫn đi luồng cũ.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaskListSignoffIntegrationTests
{
    private readonly ApiFactory _factory;

    public TaskListSignoffIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task FourSteps_DeputyHeadDeputyDirector_ReturnsAtEachStep_Mau01PrintsSigners()
    {
        var w = await World.CreateAsync(_factory);
        var periodId = await w.OpenPeriodAsync(stripNewSteps: false);
        var dao = await w.RecordOfAsync(periodId, w.Deputy.User);
        var duy = await w.RecordOfAsync(periodId, w.Head.User);
        var pgd = await w.RecordOfAsync(periodId, w.DeputyDirector.User);
        var trung = await w.RecordOfAsync(periodId, w.Deputy2.User);

        // Phó phòng: nộp → Trưởng phòng gửi (việc giao cho Trưởng phòng, không giao Giám đốc) → TCCB trình → Giám đốc duyệt.
        await w.StepAsync(w.Deputy.Client, dao, "tasks/submit", CriteriaTestData.Tasks(), "AwaitingUnitSubmit");
        Assert.Contains(dao, await QueueAsync(w.Head.Client, "B1_UNIT_SUBMIT"));
        Assert.DoesNotContain(dao, await QueueAsync(w.Director.Client, "B1_UNIT_SUBMIT"));
        Assert.False(await CanAsync(w.Reviewer.Client, dao, "ForwardTasks"));
        Assert.Equal(HttpStatusCode.Conflict, (await w.PostAsync(w.Director.Client, dao, "tasks/approve", new { })).StatusCode);
        await w.StepAsync(w.Head.Client, dao, "tasks/forward", new { comment = "Đồng ý gửi" }, "AwaitingTaskReview");
        Assert.Contains(dao, await QueueAsync(w.Reviewer.Client, "B1_HR_REVIEW"));
        Assert.False(await CanAsync(w.Head.Client, dao, "ReviewTasks"));
        await w.StepAsync(w.Reviewer.Client, dao, "tasks/review", new { comment = "Đủ điều kiện trình" }, "AwaitingTaskApproval");
        Assert.Contains(dao, await QueueAsync(w.Director.Client, "B1_APPROVE"));
        await w.StepAsync(w.Director.Client, dao, "tasks/approve", new { comment = "Đồng ý" }, "AwaitingSelfScore");

        // Mẫu 01: bốn ô ký theo thứ tự trên phiếu đều có họ tên người đã thực hiện.
        var mau01 = await DocxTextAsync(w.Reviewer.Client, $"/api/reports/docx/mau-01/{dao}");
        var signatures = mau01[mau01.IndexOf("CẤP CÓ THẨM QUYỀN PHÊ DUYỆT", StringComparison.Ordinal)..];
        var order = new[] { w.Director.Name, "CƠ QUAN TỔ CHỨC CÁN BỘ", w.Reviewer.Name, "NGƯỜI TRỰC TIẾP SỬ DỤNG", w.Head.Name, "CÁ NHÂN ĐĂNG KÝ", w.Deputy.Name }
            .Select(t => signatures.IndexOf(t, StringComparison.Ordinal)).ToList();
        Assert.True(order.All(i => i >= 0) && order.SequenceEqual(order.Order()), mau01);

        // Trưởng phòng tự đăng ký: bước gửi tự bỏ qua, ghi lịch sử; TCCB trình; Giám đốc duyệt.
        await w.StepAsync(w.Head.Client, duy, "tasks/submit", CriteriaTestData.Tasks(), "AwaitingTaskReview");
        var history = await Data(w.Head.Client, $"/api/evaluations/records/{duy}/history");
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("step").GetString() == "B1_UNIT_SUBMIT"
            && h.GetProperty("action").GetString() == "Complete"
            && h.GetProperty("actorName").GetString() == "Hệ thống"
            && h.GetProperty("comment").GetString()!.Contains("Bỏ qua bước gửi danh mục"));
        await w.StepAsync(w.Reviewer.Client, duy, "tasks/review", new { comment = "" }, "AwaitingTaskApproval");
        await w.StepAsync(w.Director.Client, duy, "tasks/approve", new { }, "AwaitingSelfScore");
        var headMau01 = await DocxTextAsync(w.Reviewer.Client, $"/api/reports/docx/mau-01/{duy}");
        var headSignatures = headMau01[headMau01.IndexOf("NGƯỜI TRỰC TIẾP SỬ DỤNG", StringComparison.Ordinal)..headMau01.IndexOf("CÁ NHÂN ĐĂNG KÝ", StringComparison.Ordinal)];
        Assert.Contains("................................", headSignatures);

        // Phó Giám đốc: không qua Trưởng đơn vị; TCCB trình; Giám đốc không duyệt, Chủ tịch duyệt.
        await w.StepAsync(w.DeputyDirector.Client, pgd, "tasks/submit", CriteriaTestData.Tasks(), "AwaitingTaskReview");
        await w.StepAsync(w.Reviewer.Client, pgd, "tasks/review", new { comment = "" }, "AwaitingTaskApproval");
        Assert.False(await CanAsync(w.Director.Client, pgd, "ApproveTasks"));
        Assert.Contains(pgd, await QueueAsync(w.Chairman.Client, "B1_APPROVE"));
        await w.StepAsync(w.Chairman.Client, pgd, "tasks/approve", new { }, "AwaitingSelfScore");

        // Trả lại ở từng bước: bắt buộc lý do, về chủ hồ sơ, xóa người đã gửi / rà soát.
        await w.StepAsync(w.Deputy2.Client, trung, "tasks/submit", CriteriaTestData.Tasks(), "AwaitingUnitSubmit");
        Assert.Equal(HttpStatusCode.BadRequest, (await w.PostAsync(w.Head.Client, trung, "tasks/forward/return", new { reason = " " })).StatusCode);
        var byUnit = await w.StepAsync(w.Head.Client, trung, "tasks/forward/return", new { reason = "Sản phẩm 2 chưa đúng nhiệm vụ của Phòng" }, "AwaitingRegistration");
        Assert.Equal("Sản phẩm 2 chưa đúng nhiệm vụ của Phòng", byUnit.GetProperty("returnReason").GetString());

        await w.StepAsync(w.Deputy2.Client, trung, "tasks/submit", CriteriaTestData.Tasks(), "AwaitingUnitSubmit");
        await w.StepAsync(w.Head.Client, trung, "tasks/forward", new { }, "AwaitingTaskReview");
        Assert.Equal(HttpStatusCode.BadRequest, (await w.PostAsync(w.Reviewer.Client, trung, "tasks/review/return", new { reason = "" })).StatusCode);
        await w.StepAsync(w.Reviewer.Client, trung, "tasks/review/return", new { reason = "Thiếu nguồn minh chứng" }, "AwaitingRegistration");
        await _factory.WithDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.SingleAsync(r => r.Id == trung);
            Assert.Null(record.TasksForwardedById);
            Assert.Null(record.TasksForwardedByName);
        });

        await w.StepAsync(w.Deputy2.Client, trung, "tasks/submit", CriteriaTestData.Tasks(), "AwaitingUnitSubmit");
        await w.StepAsync(w.Head.Client, trung, "tasks/forward", new { }, "AwaitingTaskReview");
        await w.StepAsync(w.Reviewer.Client, trung, "tasks/review", new { }, "AwaitingTaskApproval");
        await w.StepAsync(w.Director.Client, trung, "tasks/return", new { reason = "Tỷ trọng chưa hợp lý" }, "AwaitingRegistration");
        await _factory.WithDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.SingleAsync(r => r.Id == trung);
            Assert.Null(record.TasksForwardedById);
            Assert.Null(record.TasksReviewedById);
        });
    }

    [SkippableFact]
    public async Task OldPeriodSettings_WithoutNewSteps_GoStraightToApproval()
    {
        var w = await World.CreateAsync(_factory);
        var periodId = await w.OpenPeriodAsync(stripNewSteps: true);
        var dao = await w.RecordOfAsync(periodId, w.Deputy.User);
        var pgd = await w.RecordOfAsync(periodId, w.DeputyDirector.User);

        await w.StepAsync(w.Deputy.Client, dao, "tasks/submit", CriteriaTestData.Tasks(), "AwaitingTaskApproval");
        Assert.False(await CanAsync(w.Head.Client, dao, "ForwardTasks"));
        await w.StepAsync(w.Director.Client, dao, "tasks/approve", new { }, "AwaitingSelfScore");
        await w.StepAsync(w.DeputyDirector.Client, pgd, "tasks/submit", CriteriaTestData.Tasks(), "AwaitingTaskApproval");
        await w.StepAsync(w.Chairman.Client, pgd, "tasks/approve", new { }, "AwaitingSelfScore");

        // Tiến độ của hồ sơ: hai bước mới không áp dụng.
        var record = await Data(w.Deputy.Client, $"/api/evaluations/records/{dao}");
        Assert.All(record.GetProperty("progress").EnumerateArray().Where(p => p.GetProperty("step").GetString() is "B1_UNIT_SUBMIT" or "B1_HR_REVIEW"),
            p => Assert.False(p.GetProperty("enabled").GetBoolean()));
    }

    private static async Task<bool> CanAsync(HttpClient client, Guid recordId, string action) =>
        (await Data(client, $"/api/evaluations/records/{recordId}/actions")).GetProperty("actions").EnumerateArray()
            .Any(a => a.GetProperty("action").GetString() == action);

    private static async Task<List<Guid>> QueueAsync(HttpClient client, string step) =>
        (await Data(client, "/api/evaluations/work-queue")).GetProperty("groups").EnumerateArray()
            .Where(g => g.GetProperty("step").GetString() == step)
            .SelectMany(g => g.GetProperty("items").EnumerateArray()).Select(i => i.GetProperty("recordId").GetGuid()).ToList();

    private static async Task<string> DocxTextAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {url}");
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        return System.Text.RegularExpressions.Regex.Replace(await reader.ReadToEndAsync(), "<[^>]+>", string.Empty);
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private sealed record Person(TestUser User, HttpClient Client, string Name);

    /// <summary>Công ty → Phòng KH-KD, Ban Giám đốc; người như dữ liệu dùng thử (Giám đốc còn là Lãnh đạo đơn vị toàn Công ty).</summary>
    private sealed class World
    {
        private readonly ApiFactory _f;
        public Person Manager = null!, Director = null!, Chairman = null!, Head = null!, Deputy = null!, Deputy2 = null!, DeputyDirector = null!, Reviewer = null!;

        private World(ApiFactory f) => _f = f;

        public static async Task<World> CreateAsync(ApiFactory f)
        {
            Skip.If(f.SkipReason != null, f.SkipReason);
            var w = new World(f);
            var sfx = Guid.NewGuid().ToString("N")[..6];
            Guid company = Guid.Empty, sales = Guid.Empty, board = Guid.Empty, hr = Guid.Empty;
            await f.WithDbAsync(async db =>
            {
                var cty = new AdministrativeDepartment { Code = $"S-CTY-{sfx}", Name = $"Công ty {sfx}" };
                cty.Path = OrgTree.BuildPath(null, cty.Id);
                var kh = new AdministrativeDepartment { Code = $"S-KH-{sfx}", Name = $"Phòng KH-KD {sfx}", ParentId = cty.Id };
                kh.Path = OrgTree.BuildPath(cty.Path, kh.Id);
                var bgd = new AdministrativeDepartment { Code = $"S-BGD-{sfx}", Name = $"Ban Giám đốc {sfx}", ParentId = cty.Id };
                bgd.Path = OrgTree.BuildPath(cty.Path, bgd.Id);
                var tc = new AdministrativeDepartment { Code = $"S-TC-{sfx}", Name = $"Phòng TCCB-LĐ {sfx}", ParentId = cty.Id };
                tc.Path = OrgTree.BuildPath(cty.Path, tc.Id);
                db.AddRange(cty, kh, bgd, tc);
                await db.SaveChangesAsync();
                (company, sales, board, hr) = (cty.Id, kh.Id, bgd.Id, tc.Id);
            });

            async Task<Person> NewPerson(string name, Guid? dept, ApprovalAuthority authority = ApprovalAuthority.CoSo)
            {
                var fullName = $"{name} {sfx}";
                var user = await f.CreateUserAsync(dept, null, authority, fullName);
                return new Person(user, await f.LoginAsAsync(user.Username, user.Password, distinctClientIp: true), fullName);
            }

            async Task Assign(Person p, string role, Guid? dept) =>
                await f.AssignAsync(p.User.Id, await f.GetRoleIdAsync(role), dept.HasValue ? RoleScopeType.Department : RoleScopeType.Global, dept);

            w.Manager = await NewPerson("S Quản lý kỳ", hr);
            await Assign(w.Manager, "CO_QUAN_THAM_DINH", null);
            w.Reviewer = await NewPerson("S Chuyên viên TCCB", hr);
            await Assign(w.Reviewer, "CO_QUAN_THAM_DINH", null);
            w.Director = await NewPerson("S Giám đốc", board, ApprovalAuthority.CapTren);
            await Assign(w.Director, "LANH_DAO_PHONG", company);
            await Assign(w.Director, "CAP_TRUC_TIEP_SU_DUNG", sales);
            w.Chairman = await NewPerson("S Chủ tịch", board, ApprovalAuthority.CapTren);
            await Assign(w.Chairman, "CAP_TRUC_TIEP_SU_DUNG", company);
            w.Head = await NewPerson("S Trưởng phòng", sales);
            await Assign(w.Head, "LANH_DAO_PHONG", sales);
            w.Deputy = await NewPerson("S Phó phòng", sales);
            w.Deputy2 = await NewPerson("S Phó phòng hai", sales);
            w.DeputyDirector = await NewPerson("S Phó Giám đốc", board);
            foreach (var p in new[] { w.Head, w.Deputy, w.Deputy2, w.DeputyDirector })
                await Assign(p, "NGUOI_DUOC_DANH_GIA", null);
            return w;
        }

        /// <summary>Kỳ diện cơ sở cho Phòng KH-KD, diện Phó Giám đốc cho Phó Giám đốc; <paramref name="stripNewSteps"/>: cài đặt như kỳ cũ.</summary>
        public async Task<Guid> OpenPeriodAsync(bool stripNewSteps)
        {
            var created = await Data(await Manager.Client.PostAsJsonAsync("/api/evaluations/periods", new
            {
                year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter, criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(Manager.Client),
                name = $"Kỳ bốn bước {Guid.NewGuid().ToString("N")[..6]}", startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
            }));
            var periodId = created.GetProperty("id").GetGuid();
            await PeriodGates.DisableAsync(Manager.Client, periodId);
            await Data(await Manager.Client.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
                new { memberIds = new[] { Head.User.Id, Deputy.User.Id, Deputy2.User.Id }, workflowProfileCode = "co-so" }));
            await Data(await Manager.Client.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
                new { memberIds = new[] { DeputyDirector.User.Id }, workflowProfileCode = "pho-giam-doc" }));

            if (stripNewSteps)
            {
                await _f.WithDbAsync(async db =>
                {
                    var period = await db.EvaluationPeriods.SingleAsync(p => p.Id == periodId);
                    var settings = JsonNode.Parse(period.Settings!)!.AsObject();
                    foreach (var profile in settings["profiles"]!.AsArray())
                    {
                        var steps = profile!["steps"]!.AsObject();
                        steps.Remove("B1_UNIT_SUBMIT");
                        steps.Remove("B1_HR_REVIEW");
                    }
                    period.Settings = settings.ToJsonString();
                    await db.SaveChangesAsync();
                });
            }
            else
            {
                // Trưởng phòng là Trưởng đơn vị: kiểm tra kẹt luồng không báo bước gửi.
                var readiness = await Data(Manager.Client, $"/api/evaluations/periods/{periodId}/readiness");
                Assert.DoesNotContain(readiness.GetProperty("issues").EnumerateArray(), i => i.GetProperty("step").GetString() is "B1_UNIT_SUBMIT" or "B1_HR_REVIEW");
            }

            var draft = await Data(Manager.Client, $"/api/evaluations/periods/{periodId}");
            await Data(await Manager.Client.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
                new { version = draft.GetProperty("version").GetUInt32(), force = true, reason = "Kiểm thử bước 1" }));
            return periodId;
        }

        public async Task<Guid> RecordOfAsync(Guid periodId, TestUser user) =>
            (await Data(Manager.Client, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
                .First(p => p.GetProperty("memberId").GetGuid() == user.Id).GetProperty("recordId").GetGuid();

        public async Task<HttpResponseMessage> PostAsync(HttpClient client, Guid recordId, string action, object body)
        {
            var json = JsonSerializer.SerializeToNode(body)!.AsObject();
            json["version"] = (await Data(Manager.Client, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();
            return await client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
        }

        public async Task<JsonElement> StepAsync(HttpClient client, Guid recordId, string action, object body, string expectedStatus)
        {
            var data = await Data(await PostAsync(client, recordId, action, body));
            Assert.Equal(expectedStatus, data.GetProperty("status").GetString());
            return data;
        }
    }
}
