# Public keys in auth

How auth caches the key provider's public keys and serves them at `/.well-known/jwks.json`.

Terms and assumptions are in `shared.md`.

## Serving

- **J1** [tla, test] Auth returns an access token only after its signing key is in the Redis key cache. (pending)
- **J2** [tla] Once the JWKS endpoint has returned a key list, every later list contains the key of every unexpired access token auth has issued. (planned)
  - Assumes: the key provider's `PublishDelay` is longer than auth's `jwks_refresh_secs`.
  - Assumes: A4. A9.
  - Why: fails today. The reload replaces the whole key set and can drop a brand-new key added since the fetch (`service/jwks.rs:30-31`).
- **J3** [test] The JWKS endpoint keeps serving the last loaded keys while the key provider is down.
- **J4** [test] The JWKS endpoint never lists an expired key. (pending)
  - Assumes: A1.
- **J5** [test] Before the first full key load, the JWKS endpoint answers 503 with `Retry-After`.
- **J11** [test] When every loaded key has expired, the JWKS endpoint answers 503. (planned)
  - Why: J3 serves stale keys and J4 hides expired ones. After a long outage the list is empty.

## Format

- **J7** [test] Every key in a JWKS response is an Ed25519 public key (`kty` OKP, `crv` Ed25519) with a `kid`.
  - Why: RFC 8725 3.1. Each key is used with one algorithm.
- **J8** [test] No JWKS response contains a private key parameter.

## Caching

- **J9** [test] Every JWKS response sets `Cache-Control` max-age to `jwks_refresh_secs` or less. (planned)
  - Assumes: J10.
  - Why: J2 protects auth's cache. A client that caches for an hour still misses new keys. Today max-age is the constant 60.
- **J10** [test] Auth refuses to issue tokens while `jwks_refresh_secs` is more than half the key provider's `PublishDelay`. (planned)
  - Why: a client can be one auth refresh plus one max-age behind. Both must fit inside `PublishDelay`. Today neither service checks the other's setting.

## Parsing

- **J6** [kani] Converting any key provider `VerificationKey` into a public key never panics. (pending)
