using KeyProvider.Signing;
using KeyProvider.SigningKeys;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace KeyProvider.UnitTests.Signing;

public sealed class SigningServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid KeyId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid NewKeyId = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly TimeSpan PublishDelay = TimeSpan.FromMinutes(3);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly IKeyPairStore _store = Substitute.For<IKeyPairStore>();
    private readonly ServiceProvider _services;
    private readonly SigningService _signing;

    public SigningServiceTests()
    {
        _services = new ServiceCollection().AddScoped(_ => _store).BuildServiceProvider();
        _signing = new SigningService(Options.Create(new SigningOptions()),
            _services.GetRequiredService<IServiceScopeFactory>(), _clock);
    }

    public void Dispose()
    {
        _signing.Dispose();
        _services.Dispose();
    }

    [Fact]
    public async Task GenerateTokenAsync_TwoCallsWhileTheKeyIsLoading_ReadsTheStoreOnce()
    {
        // Arrange
        TaskCompletionSource<SigningKeyPair?> loading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(loading.Task);
        CancellationToken ct = TestContext.Current.CancellationToken;
        Task<SignedToken> first = _signing.GenerateTokenAsync(UserId, ct);
        Task<SignedToken> second = _signing.GenerateTokenAsync(UserId, ct);

        // Act
        loading.SetResult(TestKeys.Create(KeyId, Now.AddDays(1)));
        SignedToken[] tokens = await Task.WhenAll(first, second);

        // Assert
        await _store.Received(1).GetLatestKeyPairAsync(Arg.Any<CancellationToken>());
        tokens[0].NewPublicKey.ShouldNotBeNull();
        tokens[1].NewPublicKey.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateTokenAsync_NoKeyInStore_ThrowsSigningKeyNotFound()
    {
        // Arrange
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns((SigningKeyPair?)null);

        // Act
        Exception error = await Should.ThrowAsync<Exception>(() =>
            _signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken));

        // Assert
        error.ShouldBeOfType<SigningKeyNotFoundException>();
        error.Message.ShouldBe("No signing key is available, a key rotation has not completed yet.");
    }

    [Fact]
    public async Task GenerateTokenAsync_KeyEndsWithinATokenLifetime_ThrowsSigningKeyExpired()
    {
        // Arrange
        DateTimeOffset expiresAt = Now.AddMinutes(1);
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(TestKeys.Create(KeyId, expiresAt));

        // Act
        Exception error = await Should.ThrowAsync<Exception>(() =>
            _signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken));

        // Assert
        error.ShouldBeOfType<SigningKeyExpiredException>();
        error.Message.ShouldBe($"Signing key '{KeyId}' expires at {expiresAt:O} and can no longer sign tokens.");
    }

    [Fact]
    public async Task GenerateTokenAsync_NewestKeyNotPublishedLongEnough_SignsWithTheOlderKey()
    {
        // Arrange
        _store.GetLatestKeyPairCreatedByAsync(Now - PublishDelay, Arg.Any<CancellationToken>())
            .Returns(TestKeys.Create(KeyId, Now.AddDays(1)));
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(TestKeys.Create(NewKeyId, Now.AddDays(1)));

        // Act
        SignedToken token = await _signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        TestKeys.Header(token.Token).GetProperty("kid").GetGuid().ShouldBe(KeyId);
    }

    [Fact]
    public async Task GenerateTokenAsync_NoKeyPublishedLongEnough_SignsWithTheNewestKey()
    {
        // Arrange
        _store.GetLatestKeyPairCreatedByAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((SigningKeyPair?)null);
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(TestKeys.Create(NewKeyId, Now.AddDays(1)));

        // Act
        SignedToken token = await _signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        TestKeys.Header(token.Token).GetProperty("kid").GetGuid().ShouldBe(NewKeyId);
    }

    [Fact]
    public async Task GenerateTokenAsync_PublishedKeyEndsWithinATokenLifetime_SignsWithTheNewestKey()
    {
        // Arrange
        _store.GetLatestKeyPairCreatedByAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(TestKeys.Create(KeyId, Now.AddMinutes(1)));
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(TestKeys.Create(NewKeyId, Now.AddDays(1)));

        // Act
        SignedToken token = await _signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        TestKeys.Header(token.Token).GetProperty("kid").GetGuid().ShouldBe(NewKeyId);
    }

    [Fact]
    public async Task Dispose_AfterSigning_WipesTheCachedPrivateKey()
    {
        // Arrange
        SigningKeyPair keys = TestKeys.Create(KeyId, Now.AddDays(1));
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(keys);
        await _signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken);

        // Act
        _signing.Dispose();

        // Assert
        keys.PrivateKey.PrivateKey.ShouldAllBe(b => b == 0);
    }

    [Fact]
    public async Task GenerateTokenAsync_AfterDispose_Throws()
    {
        // Arrange
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(TestKeys.Create(KeyId, Now.AddDays(1)));
        _signing.Dispose();

        // Act
        Task<SignedToken> sign = _signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<ObjectDisposedException>(sign);
    }

    [Fact]
    public async Task GenerateTokenAsync_ValidKey_TokenIdIsThirtyTwoHexDigits()
    {
        // Arrange
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(TestKeys.Create(KeyId, Now.AddDays(1)));

        // Act
        SignedToken token = await _signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        string? tokenId = TestKeys.Payload(token.Token).GetProperty("jti").GetString();
        tokenId.ShouldNotBeNull().ShouldMatch("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task T5_GenerateTokenAsync_IssuerConfigured_TokenIssIsTheIssuer()
    {
        // Arrange
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(TestKeys.Create(KeyId, Now.AddDays(1)));
        using SigningService signing = new(Options.Create(new SigningOptions { Issuer = "test-issuer" }),
            _services.GetRequiredService<IServiceScopeFactory>(), _clock);

        // Act
        SignedToken token = await signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        TestKeys.Payload(token.Token).GetProperty("iss").GetString().ShouldBe("test-issuer");
    }

    [Fact]
    public async Task K1_GenerateTokenAsync_ClockMovesBetweenReads_TokenExpiresBeforeItsKey()
    {
        // Arrange
        TimeSpan lifetime = new SigningOptions().TokenLifetime;
        DateTimeOffset keyExpiresAt = Now + lifetime + TimeSpan.FromSeconds(90);
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(TestKeys.Create(KeyId, keyExpiresAt));
        using SigningService signing = new(Options.Create(new SigningOptions()),
            _services.GetRequiredService<IServiceScopeFactory>(), new AdvancingClock(Now, TimeSpan.FromMinutes(2)));

        // Act
        SignedToken token = await signing.GenerateTokenAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        long expiresAt = TestKeys.Payload(token.Token).GetProperty("exp").GetInt64();
        expiresAt.ShouldBeLessThan(keyExpiresAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task K2_GenerateTokenAsync_OlderKeyBecomesPublished_SwitchesFromTheFallbackKey()
    {
        // Arrange
        DateTimeOffset olderPublishedAt = Now.AddMinutes(1);
        SigningKeyPair older = TestKeys.Create(KeyId, Now.AddDays(1));
        SigningKeyPair newest = TestKeys.Create(NewKeyId, Now.AddDays(1));
        _store.GetLatestKeyPairCreatedByAsync(Now - PublishDelay, Arg.Any<CancellationToken>())
            .Returns((SigningKeyPair?)null);
        _store.GetLatestKeyPairCreatedByAsync(olderPublishedAt - PublishDelay, Arg.Any<CancellationToken>())
            .Returns(older);
        _store.GetLatestKeyPairAsync(Arg.Any<CancellationToken>()).Returns(newest);
        _store.GetOldestCreationTimeAfterAsync(Now - PublishDelay, Arg.Any<CancellationToken>())
            .Returns(olderPublishedAt - PublishDelay);
        CancellationToken ct = TestContext.Current.CancellationToken;
        await _signing.GenerateTokenAsync(UserId, ct);
        _clock.SetUtcNow(olderPublishedAt);

        // Act
        SignedToken token = await _signing.GenerateTokenAsync(UserId, ct);

        // Assert
        TestKeys.Header(token.Token).GetProperty("kid").GetGuid().ShouldBe(KeyId);
    }

    private sealed class AdvancingClock(DateTimeOffset start, TimeSpan step) : TimeProvider
    {
        private DateTimeOffset _next = start;

        public override DateTimeOffset GetUtcNow()
        {
            DateTimeOffset now = _next;
            _next += step;
            return now;
        }
    }
}
