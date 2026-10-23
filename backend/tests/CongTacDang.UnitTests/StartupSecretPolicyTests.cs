using CongTacDang.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Kiểm tra khóa JWT / mật khẩu khi khởi động.</summary>
public class StartupSecretPolicyTests
{
    // Giá trị ngẫu nhiên chỉ dùng trong test.
    private const string StrongJwt = "q8Zr3vT1mXc9LpA2sWk7Hn4Ye6Ub0JdFgRtQwEiOxN5=";
    private const string StrongDbPassword = "Vn7#pQ2zLk9wRt4sXm";

    private static IConfiguration Config(string? jwt, string? connectionString, string? adminPassword = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = jwt,
            ["ConnectionStrings:Default"] = connectionString,
            ["Seed:InitialAdmin:Password"] = adminPassword,
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static string Conn(string password) =>
        $"Host=postgres;Port=5432;Database=congtacdang_db;Username=dangbo_admin;Password={password}";

    [Fact]
    public void Production_StrongValues_Pass()
    {
        Assert.Empty(StartupSecretPolicy.Validate(Config(StrongJwt, Conn(StrongDbPassword)), "Production"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short-secret")]
    public void AnyEnvironment_MissingOrShortJwt_Fails(string? jwt)
    {
        Assert.NotEmpty(StartupSecretPolicy.Validate(Config(jwt, Conn(StrongDbPassword)), "Production"));
        Assert.NotEmpty(StartupSecretPolicy.Validate(Config(jwt, Conn(StrongDbPassword)), "Development"));
    }

    [Fact]
    public void AnyEnvironment_KnownSampleJwt_Fails()
    {
        // Không ghi lại khóa/mật khẩu mẫu cũ trong test: dùng giá trị mặc định phổ biến có trong danh sách cấm.
        Assert.True(StartupSecretPolicy.IsKnownSampleValue(" postgres "));
        Assert.False(StartupSecretPolicy.IsKnownSampleValue(StrongJwt));
        Assert.Contains(StartupSecretPolicy.Validate(Config("postgres", Conn(StrongDbPassword)), "Development"),
            e => e.Contains("chuỗi mẫu cũ"));
        Assert.Contains(StartupSecretPolicy.Validate(Config("postgres", Conn(StrongDbPassword)), "Production"),
            e => e.Contains("chuỗi mẫu cũ"));
    }

    [Fact]
    public void Production_PlaceholderJwt_Fails_DevelopmentKeepsOldBehavior()
    {
        var placeholder = "CHANGE_ME_JWT_SECRET_CHANGE_ME_JWT_SECRET_000";
        Assert.Contains(StartupSecretPolicy.Validate(Config(placeholder, Conn(StrongDbPassword)), "Production"),
            e => e.Contains("CHANGE_ME"));
        Assert.Empty(StartupSecretPolicy.Validate(Config(placeholder, Conn(StrongDbPassword)), "Development"));
    }

    [Fact]
    public void Production_LowEntropyJwt_Fails()
    {
        var repeated = new string('a', 64);
        Assert.Contains(StartupSecretPolicy.Validate(Config(repeated, Conn(StrongDbPassword)), "Production"),
            e => e.Contains("đơn điệu"));
        Assert.Empty(StartupSecretPolicy.Validate(Config(repeated, Conn(StrongDbPassword)), "Development"));
    }

    [Fact]
    public void AnyEnvironment_MissingConnectionString_Fails()
    {
        Assert.NotEmpty(StartupSecretPolicy.Validate(Config(StrongJwt, null), "Development"));
        Assert.NotEmpty(StartupSecretPolicy.Validate(Config(StrongJwt, null), "Production"));
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("postgres")]
    [InlineData("CHANGE_ME_POSTGRES_PASSWORD")]
    [InlineData("ngan12")]
    public void Production_SampleOrWeakDbPassword_Fails(string password)
    {
        Assert.NotEmpty(StartupSecretPolicy.Validate(Config(StrongJwt, Conn(password)), "Production"));
        Assert.Empty(StartupSecretPolicy.Validate(Config(StrongJwt, Conn(password)), "Development"));
        Assert.Empty(StartupSecretPolicy.Validate(Config(StrongJwt, Conn(password)), "Testing"));
    }

    [Fact]
    public void Production_DefaultDbPassword_ReportsSampleValue()
    {
        Assert.Contains(StartupSecretPolicy.Validate(Config(StrongJwt, Conn("postgres")), "Production"),
            e => e.Contains("mẫu/mặc định"));
    }

    [Fact]
    public void Production_NoDbPassword_Fails()
    {
        Assert.NotEmpty(StartupSecretPolicy.Validate(
            Config(StrongJwt, "Host=postgres;Database=congtacdang_db;Username=dangbo_admin"), "Production"));
    }

    [Fact]
    public void Production_JwtEqualsDbPassword_Fails()
    {
        Assert.Contains(StartupSecretPolicy.Validate(Config(StrongJwt, Conn(StrongJwt)), "Production"),
            e => e.Contains("không được trùng"));
    }

    [Fact]
    public void Production_SampleInitialAdminPassword_Fails()
    {
        Assert.NotEmpty(StartupSecretPolicy.Validate(Config(StrongJwt, Conn(StrongDbPassword), "123456"), "Production"));
        Assert.Empty(StartupSecretPolicy.Validate(Config(StrongJwt, Conn(StrongDbPassword), "QuanTri2026xyz"), "Production"));
    }

    [Fact]
    public void Staging_IsTreatedAsProduction()
    {
        Assert.NotEmpty(StartupSecretPolicy.Validate(Config(StrongJwt, Conn("123456")), "Staging"));
    }

    [Fact]
    public void Messages_DoNotContainSecretValues()
    {
        var errors = StartupSecretPolicy.Validate(Config(StrongJwt, Conn(StrongJwt)), "Production");
        Assert.All(errors, e => Assert.DoesNotContain(StrongJwt, e));
    }

    [Fact]
    public void ValidateRequiredConfiguration_Throws_WithVietnameseMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Config(StrongJwt, Conn("123456")).ValidateRequiredConfiguration("Production"));
        Assert.Contains("Không khởi động", ex.Message);
        Config(StrongJwt, Conn(StrongDbPassword)).ValidateRequiredConfiguration("Production");
    }
}
