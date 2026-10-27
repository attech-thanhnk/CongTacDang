using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Infrastructure.Data;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Migration đổi tên vai trò mặc định: chỉ đổi khi còn đúng tên cũ, giữ tên quản trị đã sửa, tránh trùng tên, chạy lại không đổi thêm.</summary>
public sealed class DoiTenVaiTroMacDinhTests
{
    private const string BeforeMigration = "20261026081315_ChoVanPhongDangUyXacNhanPhieuDoiSoat";
    private const string ThisMigration = "20261027025334_DoiTenVaiTroMacDinhTheoThuatNgu";
    private static readonly string? AdminConnection = Environment.GetEnvironmentVariable(ApiFactory.ConnectionEnvVar);

    /// <summary>Bản vá trong script chuyển dữ liệu phải mang đúng các chuỗi của migration (hai nơi cùng danh sách).</summary>
    [Fact]
    public void BanVaChuyenDuLieuGoc_CungDanhSachVoiMigration()
    {
        var migration = File.ReadAllText(FindFromRepoRoot("backend", "src", "CongTacDang.Infrastructure", "Data", "Migrations", ThisMigration + ".cs"));
        var script = File.ReadAllText(FindFromRepoRoot("docker", "chuyen-du-lieu-goc.sh"));
        Assert.Contains("PATCH_SQL[" + ThisMigration + "]", script);
        Assert.Contains(ThisMigration, script[script.IndexOf("PATCH_ORDER=", StringComparison.Ordinal)..]);
        var literals = System.Text.RegularExpressions.Regex.Matches(migration, "'([^']+)'").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(literals.Count >= 28, "Không đọc được danh sách chuỗi của migration.");
        Assert.All(literals, s => Assert.Contains(s, script));
    }

    private static string FindFromRepoRoot(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return path;
        }

        throw new FileNotFoundException(string.Join("/", parts));
    }

    [SkippableFact]
    public async Task Migration_DoiTenDungQuyTac()
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
                await db.GetService<IMigrator>().MigrateAsync(BeforeMigration);
                // Bộ nạp dữ liệu theo model hiện tại ghi cả cột thêm sau migration này; thêm tạm rồi bỏ để migration sau chạy đúng.
                await db.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE organization_settings ADD COLUMN IF NOT EXISTS "GuideDocumentNumber" varchar(50), ADD COLUMN IF NOT EXISTS "GuideDocumentDate" date;
                    ALTER TABLE party_cells ADD COLUMN IF NOT EXISTS "ShortName" varchar(30);
                    ALTER TABLE administrative_departments ADD COLUMN IF NOT EXISTS "ShortName" varchar(30);
                    """);
                await DataSeeder.SeedAsync(db);
                await db.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE organization_settings DROP COLUMN "GuideDocumentNumber", DROP COLUMN "GuideDocumentDate";
                    ALTER TABLE party_cells DROP COLUMN "ShortName";
                    ALTER TABLE administrative_departments DROP COLUMN "ShortName";
                    """);
                // Mô phỏng CSDL đã vận hành với tên cũ.
                await db.Database.ExecuteSqlRawAsync("""
                    UPDATE roles SET "Name" = 'Lãnh đạo Phòng',
                        "Description" = 'Duyệt danh mục sản phẩm, đề xuất mức, lập kế hoạch 30-60-90 ngày (Mẫu 17) cho cán bộ của Phòng.'
                      WHERE "Code" = 'LANH_DAO_PHONG';
                    UPDATE roles SET "Name" = 'Chi ủy / Bí thư Chi bộ', "Description" = 'Mô tả do quản trị tự viết.'
                      WHERE "Code" = 'CHI_UY_CHI_BO';
                    UPDATE roles SET "Name" = 'Phòng Tổ chức do quản trị đặt' WHERE "Code" = 'CO_QUAN_THAM_DINH';
                    UPDATE roles SET "Name" = 'Cấp ủy viên Đảng ủy' WHERE "Code" = 'CAP_UY_VIEN';
                    UPDATE roles SET "Name" = 'Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch)' WHERE "Code" = 'CAP_TRUC_TIEP_SU_DUNG';
                    """);
                // Vai trò do quản trị tạo đã dùng mất tên mới "Cấp ủy viên" → vai trò mặc định giữ tên cũ.
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO roles ("Id", "Code", "Name", "Description", "IsSystem", "IsProtected", "CreatedAt", "IsDeleted")
                    VALUES (gen_random_uuid(), 'TU_TAO', 'Cấp ủy viên', '', false, false, now(), false)
                    """);
            }

            await using (var db = new CongTacDangDbContext(options, new NoUser()))
            {
                await db.GetService<IMigrator>().MigrateAsync(ThisMigration);
                await db.GetService<IMigrator>().MigrateAsync();
                await DataSeeder.SeedAsync(db);
            }

            await using (var db = new CongTacDangDbContext(options, new NoUser()))
            {
                var roles = await db.Roles.ToDictionaryAsync(r => r.Code);
                Assert.Equal("Lãnh đạo đơn vị", roles["LANH_DAO_PHONG"].Name);
                Assert.EndsWith("cán bộ của đơn vị.", roles["LANH_DAO_PHONG"].Description);
                Assert.Equal("Chi bộ", roles["CHI_UY_CHI_BO"].Name);
                Assert.Equal("Mô tả do quản trị tự viết.", roles["CHI_UY_CHI_BO"].Description);
                Assert.Equal("Phòng Tổ chức do quản trị đặt", roles["CO_QUAN_THAM_DINH"].Name);
                Assert.Equal("Cấp ủy viên Đảng ủy", roles["CAP_UY_VIEN"].Name);
                Assert.Equal("Cấp trực tiếp sử dụng", roles["CAP_TRUC_TIEP_SU_DUNG"].Name);
                Assert.StartsWith("Giám đốc, Chủ tịch:", roles["CAP_TRUC_TIEP_SU_DUNG"].Description);
                Assert.Equal("Thư ký hội nghị", roles["THU_KY_TAP_THE"].Name);
                Assert.Equal("Văn phòng Đảng ủy", roles["VAN_PHONG_DANG_UY"].Name);
                Assert.Equal("Người được đánh giá", roles["NGUOI_DUOC_DANH_GIA"].Name);
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
        public string UserName => "doi-ten-vai-tro-test";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }
}
