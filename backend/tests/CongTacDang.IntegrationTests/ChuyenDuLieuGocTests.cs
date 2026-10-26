using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Infrastructure.Data;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Danh sách phân loại bảng docker/du-lieu-goc.txt (dùng chung với script chuyển dữ liệu) và hành vi seeder sau khi chuyển.</summary>
public sealed class ChuyenDuLieuGocTests
{
    private static readonly string? AdminConnection = Environment.GetEnvironmentVariable(ApiFactory.ConnectionEnvVar);

    private static string FindListFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "docker", "du-lieu-goc.txt");
            if (File.Exists(path)) return path;
        }

        throw new FileNotFoundException("Không tìm thấy docker/du-lieu-goc.txt từ thư mục chạy test.");
    }

    private static List<(string Kind, string Table)> ReadList() =>
        File.ReadAllLines(FindListFile())
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('|'))
            .Select(p => (p[0], p[1]))
            .ToList();

    [Fact]
    public void MoiBangCuaDbContext_PhaiDuocPhanLoaiTrongDuLieuGoc()
    {
        var options = new DbContextOptionsBuilder<CongTacDangDbContext>().UseNpgsql("Host=localhost;Database=khong_ket_noi").Options;
        using var db = new CongTacDangDbContext(options, new NoUser());
        var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Where(t => t != null).Select(t => t!).ToHashSet();
        tables.Add("__EFMigrationsHistory");

        var list = ReadList();
        Assert.All(list, x => Assert.Contains(x.Kind, new[] { "GOC", "VAN_HANH", "HE_THONG" }));
        Assert.Equal(list.Count, list.Select(x => x.Table).Distinct().Count());

        var unclassified = tables.Except(list.Select(x => x.Table)).OrderBy(t => t).ToList();
        Assert.True(unclassified.Count == 0,
            $"Bảng chưa phân loại trong docker/du-lieu-goc.txt: {string.Join(", ", unclassified)}. Thêm dòng GOC (chép sang) hoặc VAN_HANH (không chép).");
        var stale = list.Select(x => x.Table).Except(tables).OrderBy(t => t).ToList();
        Assert.True(stale.Count == 0, $"du-lieu-goc.txt có bảng không còn trong DbContext: {string.Join(", ", stale)}.");
    }

    [Fact]
    public void BangGoc_KhongThamChieuKhoaNgoaiToiBangVanHanh_VaThuTuTheoPhuThuoc()
    {
        var options = new DbContextOptionsBuilder<CongTacDangDbContext>().UseNpgsql("Host=localhost;Database=khong_ket_noi").Options;
        using var db = new CongTacDangDbContext(options, new NoUser());
        var list = ReadList();
        var goc = list.Where(x => x.Kind == "GOC").Select(x => x.Table).ToList();
        var order = goc.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null || !order.ContainsKey(table)) continue;
            foreach (var fk in entity.GetForeignKeys())
            {
                var parent = fk.PrincipalEntityType.GetTableName()!;
                Assert.True(order.ContainsKey(parent), $"Bảng gốc {table} tham chiếu bảng không phải gốc {parent}.");
                Assert.True(parent == table || order[parent] < order[table],
                    $"Trong du-lieu-goc.txt bảng cha {parent} phải đứng trước bảng con {table}.");
            }
        }
    }

    /// <summary>Mô phỏng CSDL đích sau khi chuyển từ máy cũ: thiếu mã quyền mới và vai trò mặc định mới; seeder phải bổ sung và gán.</summary>
    [SkippableFact]
    public async Task KhoiDongLaiSauChuyen_SeederBoSungQuyenMoiVaVaiTroMacDinhMoi()
    {
        Skip.If(string.IsNullOrWhiteSpace(AdminConnection), $"Bỏ qua: chưa đặt biến môi trường {ApiFactory.ConnectionEnvVar}.");
        var admin = TestDatabaseAdmin.AdminConnectionString(AdminConnection!);
        var name = TestDatabaseNames.NewName(DateTime.UtcNow);
        await TestDatabaseAdmin.CreateAsync(admin, name);
        try
        {
            var options = new DbContextOptionsBuilder<CongTacDangDbContext>()
                .UseNpgsql(TestDatabaseAdmin.TargetConnectionString(admin, name)).Options;
            await using (var db = new CongTacDangDbContext(options, new NoUser()))
            {
                await db.Database.MigrateAsync();
                await DataSeeder.SeedAsync(db);
                var newCodes = new[]
                {
                    PermissionCodes.EvaluationCrosscheckConfirm, PermissionCodes.EvaluationControlConfirm,
                    PermissionCodes.EvaluationCalibrationConclude
                };
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM role_permissions WHERE permission_id IN (SELECT \"Id\" FROM permissions WHERE \"Code\" = ANY({0}))", (object)newCodes);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM permissions WHERE \"Code\" = ANY({0})", (object)newCodes);
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM role_permissions WHERE role_id IN (SELECT \"Id\" FROM roles WHERE \"Code\" = 'CO_QUAN_CHUYEN_MON')");
                await db.Database.ExecuteSqlRawAsync("DELETE FROM roles WHERE \"Code\" = 'CO_QUAN_CHUYEN_MON'");
            }

            await using (var db = new CongTacDangDbContext(options, new NoUser()))
                await DataSeeder.SeedAsync(db);

            await using (var db = new CongTacDangDbContext(options, new NoUser()))
            {
                var roles = await db.Roles.Include(r => r.Permissions).ToListAsync();
                var expert = Assert.Single(roles, r => r.Code == "CO_QUAN_CHUYEN_MON");
                Assert.Contains(expert.Permissions, p => p.Code == PermissionCodes.EvaluationCrosscheckConfirm);
                var office = Assert.Single(roles, r => r.Code == "VAN_PHONG_DANG_UY");
                Assert.Contains(office.Permissions, p => p.Code == PermissionCodes.EvaluationCrosscheckConfirm);
                Assert.Contains(office.Permissions, p => p.Code == PermissionCodes.EvaluationControlConfirm);
                Assert.Contains(office.Permissions, p => p.Code == PermissionCodes.EvaluationCalibrationConclude);
            }
        }
        finally
        {
            await TestDatabaseAdmin.TryDropAsync(admin, name);
        }
    }

    private sealed class NoUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "chuyen-du-lieu-test";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }
}
