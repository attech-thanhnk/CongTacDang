using System.Linq.Expressions;
using System.Text;
using CongTacDang.Api.Middlewares;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CongTacDang.UnitTests;

public class SecurityTests
{
    [Fact]
    public async Task LocalStorage_RejectsPathTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "congtacdang-tests", Guid.NewGuid().ToString("N"));
        var storage = new LocalFileStorageService(root);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.SaveFileAsync(new MemoryStream([1]), "../outside.bin"));
    }

    [Theory]
    [InlineData("evidence.pdf", "<html>")]
    [InlineData("evidence.png", "not-a-png")]
    public async Task Attachment_RejectsMismatchedMagicBytes(string fileName, string contents)
    {
        var service = new AttachmentService(new FakeAttachmentRepository(), new FakeStorage());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAttachmentAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(contents)), fileName, contents.Length, "MAU01", "", "tester"));
    }

    [Fact]
    public async Task Attachment_RejectsPathTraversalFormCode()
    {
        var service = new AttachmentService(new FakeAttachmentRepository(), new FakeStorage());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAttachmentAsync(
            new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7")), "evidence.pdf", 8, "../../x", "", "tester"));
    }

    [Fact]
    public async Task ExceptionMiddleware_MapsForbiddenTo403()
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new ForbiddenException("Không có quyền."),
            NullLogger<GlobalExceptionMiddleware>.Instance,
            new TestHostEnvironment());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task RefreshRotation_SecondUseWithinGraceDoesNotRevokeAll()
    {
        var user = new PartyMemberProfile { Username = "tester", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1") };
        var repository = new FakeRefreshTokenRepository(user);
        var users = new FakeUserRepository(user);
        var service = new AuthService(users, repository, new FakeJwtService(), new PermissionResolver(users, new PermissionCache()));
        var first = await service.RefreshTokenAsync(repository.Current.Token);

        await Assert.ThrowsAsync<RefreshTokenGracePeriodException>(() => service.RefreshTokenAsync(repository.Original.Token));

        Assert.NotEmpty(first.RefreshToken);
        Assert.Equal(0, repository.RevokeAllCalls);
    }

    [Fact]
    public async Task Login_LocksAccountAfterFiveFailures()
    {
        var user = new PartyMemberProfile { Username = "tester", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1") };
        var repository = new FakeUserRepository(user);
        var service = new AuthService(repository, new FakeRefreshTokenRepository(user), new FakeJwtService(), new PermissionResolver(repository, new PermissionCache()));

        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoginAsync(new LoginRequestDto { Username = "tester", Password = "wrong" }));

        Assert.Equal(5, user.FailedLoginCount);
        Assert.True(user.LockoutEnd > DateTime.UtcNow);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class FakeStorage : IFileStorageService
    {
        public Task DeleteFileAsync(string objectKey) => Task.CompletedTask;
        public bool FileExists(string objectKey) => false;
        public Task<Stream?> GetFileStreamAsync(string objectKey) => Task.FromResult<Stream?>(null);
        public Task<string?> GetDownloadUrlAsync(Guid attachmentId, TimeSpan? expiry = null) => Task.FromResult<string?>(null);
        public async Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream")
        {
            await fileStream.CopyToAsync(Stream.Null);
            return objectKey;
        }
    }

    private sealed class FakeAttachmentRepository : IAttachmentRepository
    {
        public Task AddAsync(TaskAttachment entity) => Task.CompletedTask;
        public Task DeleteAsync(TaskAttachment entity) => Task.CompletedTask;
        public Task<List<TaskAttachment>> FindAsync(Expression<Func<TaskAttachment, bool>> predicate) => Task.FromResult(new List<TaskAttachment>());
        public Task<TaskAttachment?> GetByIdAsync(Guid id) => Task.FromResult<TaskAttachment?>(null);
        public Task<List<TaskAttachment>> ListAsync() => Task.FromResult(new List<TaskAttachment>());
        public Task UpdateAsync(TaskAttachment entity) => Task.CompletedTask;
        public Task<List<TaskAttachment>> GetAllAttachmentsAsync() => Task.FromResult(new List<TaskAttachment>());
    }

    private sealed class FakeJwtService : IJwtService
    {
        private int _counter;
        public (string Token, DateTime ExpiresAt) GenerateToken(PartyMemberProfile member, IEnumerable<string> roles, IEnumerable<string> permissions) => ("access", DateTime.UtcNow.AddMinutes(15));
        public RefreshToken GenerateRefreshToken(Guid userId, string? ipAddress = null)
        {
            var raw = "refresh-" + Interlocked.Increment(ref _counter);
            return new RefreshToken { UserId = userId, Token = raw, TokenHash = raw, ExpiresAt = DateTime.UtcNow.AddDays(7) };
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly PartyMemberProfile _user;
        public FakeUserRepository(PartyMemberProfile user) => _user = user;
        public Task AddAsync(PartyMemberProfile entity) => Task.CompletedTask;
        public Task DeleteAsync(PartyMemberProfile entity) => Task.CompletedTask;
        public Task<List<PartyMemberProfile>> FindAsync(Expression<Func<PartyMemberProfile, bool>> predicate) => Task.FromResult(new List<PartyMemberProfile>());
        public Task<PartyMemberProfile?> GetByIdAsync(Guid id) => Task.FromResult<PartyMemberProfile?>(_user.Id == id ? _user : null);
        public Task<List<PartyMemberProfile>> ListAsync() => Task.FromResult(new List<PartyMemberProfile> { _user });
        public Task UpdateAsync(PartyMemberProfile entity) => Task.CompletedTask;
        public Task<PartyMemberProfile?> GetByUsernameAsync(string username) => Task.FromResult<PartyMemberProfile?>(_user.Username == username ? _user : null);
        public Task<PartyMemberProfile?> GetFirstMemberAsync() => Task.FromResult<PartyMemberProfile?>(_user);
        public Task<List<PartyMemberProfile>> GetAllWithDetailsAsync() => Task.FromResult(new List<PartyMemberProfile> { _user });
        public Task<PartyMemberProfile?> GetWithRolesAndPermissionsAsync(string username) => GetByUsernameAsync(username);
        public Task<PartyMemberProfile?> GetWithRolesAndPermissionsByIdAsync(Guid id) => GetByIdAsync(id);
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public RefreshToken Original { get; }
        public RefreshToken Current { get; private set; }
        public int RevokeAllCalls { get; private set; }
        private readonly PartyMemberProfile _user;

        public FakeRefreshTokenRepository(PartyMemberProfile user)
        {
            _user = user;
            Original = new RefreshToken { UserId = user.Id, Token = "original", TokenHash = "original", ExpiresAt = DateTime.UtcNow.AddDays(1), User = user };
            Current = Original;
        }

        public Task AddAsync(RefreshToken entity) => Task.CompletedTask;
        public Task DeleteAsync(RefreshToken entity) => Task.CompletedTask;
        public Task<List<RefreshToken>> FindAsync(Expression<Func<RefreshToken, bool>> predicate) => Task.FromResult(new List<RefreshToken>());
        public Task<RefreshToken?> GetByIdAsync(Guid id) => Task.FromResult<RefreshToken?>(null);
        public Task<List<RefreshToken>> ListAsync() => Task.FromResult(new List<RefreshToken>());
        public Task UpdateAsync(RefreshToken entity) => Task.CompletedTask;
        public Task<RefreshToken?> GetByTokenWithUserAsync(string token) => Task.FromResult<RefreshToken?>(token == Original.Token || token == Current.Token ? (token == Original.Token ? Original : Current) : null);
        public Task<bool> RotateAsync(string oldToken, RefreshToken newToken)
        {
            if (Current.IsRevoked) return Task.FromResult(false);
            Current.IsRevoked = true;
            Current.RevokedAt = DateTime.UtcNow;
            Current.ReplacedByTokenHash = newToken.TokenHash;
            newToken.User = _user;
            Current = newToken;
            return Task.FromResult(true);
        }
        public Task RevokeAllUserTokensAsync(Guid userId) { RevokeAllCalls++; return Task.CompletedTask; }
        public Task RevokeOtherUserTokensAsync(Guid userId, string? currentToken) => Task.CompletedTask;
    }
}
