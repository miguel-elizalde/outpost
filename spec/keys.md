# Signing keys

The key provider creates Ed25519 key pairs on a schedule and signs access tokens for auth.

Terms and assumptions are in `shared.md`.

## Signing

- **K1** [tla, test] Every access token expires before the key that signed it.
  - Assumes: A1.
  - Why: fails in a narrow race. The clock is read twice (`Signing/SigningService.cs:116-119`, then `:44-51`). A key that expires between the reads signs a token that outlives it.
- **K2** [tla, test] The key provider signs with a key created at least `PublishDelay` ago whenever such a key can still sign.
  - Why: fails in an edge case. A fallback key stays cached and keeps signing after an older key becomes published (`Signing/SigningService.cs:78-82`).
- **K3** [tla] After its first key is saved, the key provider always has a key that can sign.
  - Assumes: each due rotation succeeds within its first retry round.
  - Assumes: A1. A4.
  - Assumes: the configuration passed K4 and K17 at startup.
  - Assumes: one retry round never takes longer than the worst-case retry duration in K4.
  - Why: fails today. The validator counts the worst-case retry once. A config it accepts leaves no key that can sign when one rotation round fails and its retry runs to the limit.
- **K16** [tla] Once its first key is older than `PublishDelay`, the key provider never signs with a key younger than `PublishDelay`.
  - Assumes: the same as K3.
  - Why: K2 allows a brand-new key as a fallback. This rule says the fallback is never needed after startup, which is what keeps every signing key published before it is used (J2).
- **K12** [test] No token the key provider signs expires more than `TokenLifetime` after it was signed.
  - Why: K1 only holds if auth cannot ask for a longer lifetime.
- **K11** [test] The key provider rejects every sign request that does not carry valid caller credentials. (planned)
  - Why: without this or A6, anything on the network can mint a token for any user.

## Configuration

- **K4** [test] The key provider refuses to start unless `Lifespan` is longer than the largest schedule gap plus `FailureRetryDelay`, twice the worst-case retry duration, `AttemptTimeout`, `TokenLifetime` and `PublishDelay`.
  - Why: the TLA+ model shows K3 needs the retry counted twice (a failed round, then its retry) and one `AttemptTimeout` for the first rotation after startup. `PublishDelay` stays for K16.
- **K17** [test] The key provider refuses to start unless the smallest schedule gap is longer than `FailureRetryDelay` plus twice the worst-case retry duration.
  - Why: the model proves K3 only when a failed rotation and its retry finish before the next slot. Whether K3 survives without this is unknown.

## Storage

- **K5** [test] Key pair and rotation job rows can never be updated, deleted or truncated.
- **K6** [test] A key pair can only be saved with a successful rotation job.
- **K7** [test] Private keys are stored only after Data Protection encrypts them.
- **K8** [test] No gRPC response from the key provider contains a private key.
- **K10** [test] No two key pairs ever have the same key ID.

## Listing

- **K9** [test] `GetVerificationKeys` returns every unexpired key.
- **K14** [test] `GetVerificationKeys` returns no expired key.
- **K15** [test] `GetVerificationKeys` returns keys oldest first.
