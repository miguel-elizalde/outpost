using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using KeyProvider.SigningKeys;

using Microsoft.Extensions.Options;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace KeyProvider.Signing;

internal sealed class SigningService(
    IOptions<SigningOptions> options,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IDisposable
{
    private readonly SigningOptions _options = options.Value;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private volatile CachedKey? _cachedKey;

    public void Dispose()
    {
        if (_cachedKey is { } cached)
        {
            CryptographicOperations.ZeroMemory(cached.Key.PrivateKey);
            _cachedKey = null;
        }

        _refreshLock.Dispose();
    }

    private byte[] GetHeader(PrivateSigningKey key)
    {
        TokenHeader header = new() { Algorithm = _options.Algorithm, Type = _options.Type, KeyId = key.Id };

        return JsonSerializer.SerializeToUtf8Bytes(header);
    }

    private byte[] GetPayload(Guid sub, DateTimeOffset now)
    {
        Dictionary<string, object> claims = new()
        {
            ["iss"] = _options.Issuer,
            ["sub"] = sub.ToString(),
            ["aud"] = _options.Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = (now + _options.TokenLifetime).ToUnixTimeSeconds(),
            ["jti"] = Guid.CreateVersion7(now).ToString("N")
        };

        return JsonSerializer.SerializeToUtf8Bytes(claims);
    }

    internal async Task<SignedToken> GenerateTokenAsync(Guid sub, CancellationToken ct = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        (PrivateSigningKey privateKey, PublicSigningKey? newPublicKey) = await GetActiveKeyAsync(now, ct);
        byte[] headerJson = GetHeader(privateKey);
        byte[] payloadJson = GetPayload(sub, now);

        string signingInput = $"{B64Url(headerJson)}.{B64Url(payloadJson)}";
        byte[] data = Encoding.ASCII.GetBytes(signingInput);

        Ed25519Signer signer = new();
        signer.Init(true, new Ed25519PrivateKeyParameters(privateKey.PrivateKey));
        signer.BlockUpdate(data, 0, data.Length);

        byte[] signature = signer.GenerateSignature();

        return new SignedToken($"{signingInput}.{B64Url(signature)}", privateKey.Id, newPublicKey);
    }

    private async Task<ActiveKey> GetActiveKeyAsync(DateTimeOffset now, CancellationToken ct)
    {
        CachedKey? cached = _cachedKey;
        if (cached is not null && CanReuse(cached, now))
        {
            return new ActiveKey(cached.Key, null);
        }

        await _refreshLock.WaitAsync(ct);
        try
        {
            cached = _cachedKey;
            if (cached is not null && CanReuse(cached, now))
            {
                return new ActiveKey(cached.Key, null);
            }

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IKeyPairStore keyPairStore = scope.ServiceProvider.GetRequiredService<IKeyPairStore>();

            SigningKeyPair? published = await keyPairStore.GetLatestKeyPairCreatedByAsync(
                now - _options.PublishDelay, ct);
            bool isPublished = published is not null && CanSign(published.PrivateKey, now);
            SigningKeyPair fresh = isPublished
                ? published!
                : await keyPairStore.GetLatestKeyPairAsync(ct) ?? throw new SigningKeyNotFoundException();

            if (!CanSign(fresh.PrivateKey, now))
            {
                throw new SigningKeyExpiredException(fresh.PrivateKey.Id, fresh.PrivateKey.ExpirationTime);
            }

            DateTimeOffset? nextPublishTime = isPublished
                ? null
                : await keyPairStore.GetOldestCreationTimeAfterAsync(now - _options.PublishDelay, ct)
                  + _options.PublishDelay;

            _cachedKey = new CachedKey(fresh.PrivateKey, nextPublishTime ?? DateTimeOffset.MaxValue);
            return new ActiveKey(fresh.PrivateKey, cached?.Key.Id == fresh.PrivateKey.Id ? null : fresh.PublicKey);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool CanReuse(CachedKey cached, DateTimeOffset now)
    {
        return now < cached.ReusableUntil && CanSign(cached.Key, now);
    }

    private bool CanSign(PrivateSigningKey key, DateTimeOffset now)
    {
        return key.ExpirationTime > now + _options.TokenLifetime;
    }

    private static string B64Url(ReadOnlySpan<byte> b)
    {
        return Base64Url.EncodeToString(b);
    }

    private sealed record CachedKey(PrivateSigningKey Key, DateTimeOffset ReusableUntil);
}
