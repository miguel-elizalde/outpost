# Refresh tokens

How auth keeps a user logged in: refresh tokens in Redis, token families in Postgres.

Terms and assumptions are in `shared.md`.

## Rotation

- **R1** [tla, test] A successful refresh returns a refresh token different from the one presented. (pending)
  - Assumes: A3. A7.
- **R2** [tla, test] Each refresh token succeeds at refresh at most once, even when calls overlap. (pending)
  - Assumes: A3. A7.
- **R3** [tla, lean] At any time, each family has at most one live token. (pending)
  - Assumes: A3. A7.
- **R14** [test] A refresh with a family's live token succeeds.
  - Assumes: the user is active. Redis, Postgres and the key provider are reachable.
- **R15** [test] An access token returned by a refresh has the same `sub` as the access token from the register or login that created the family. (pending)
- **R16** [test] A login does not stop the user's other families from refreshing. (pending)

## Reuse

- **R4** [tla, lean, test] Presenting a rotated-out refresh token to refresh, before its family expires, revokes the whole family. (planned)
  - Assumes: A2. A3. Postgres is reachable.
  - Why: RFC 9700 4.14. Fails today: the rotated-out marker is set with KEEPTTL (`refresh_token/redis.rs:87`), so it dies with the old token, not with the family.
- **R25** [tla, test] Presenting a rotated-out refresh token to logout, before its family expires, revokes the whole family. (planned)
  - Assumes: A2. A3.
  - Why: same marker as R4.
- **R5** [tla, lean] Once a family is revoked, no refresh that starts later succeeds in it. (pending)
  - Assumes: A8.
  - Why: a refresh that overlaps the revoke can still return tokens. The family check (`service/refresh_token.rs:98`) runs before the rotation (`:118`). See non-goals.
- **R23** [tla, test] A refresh that fails because Postgres or the key provider is unreachable leaves the presented token live. (pending)
  - Why: with strict reuse detection, a token consumed by a failed refresh makes the client's retry revoke the family.

## Expiry

- **R6** [test] The `refresh_token_expires_at` returned by register, login or refresh is never later than the family's start plus `max_age_secs`. (pending)
- **R7** [tla, test] No refresh succeeds after the presented token's `refresh_token_expires_at`. (pending)
  - Assumes: A1.
- **R21** [kani] Computing a token's expiry and a family's expiry never panics. (planned)
  - Assumes: the clock is between 2020 and 2200. The settings passed R22.
  - Why: chrono's `+` panics on overflow (`service/refresh_token.rs:75-76, 114`). Needs the math in a pure function first.
- **R22** [test] Auth refuses to start when `max_age_secs` or `idle_timeout_secs` is zero or more than 365 days.

## Logout

- **R8** [tla, test] After a logout with a family's live token returns, no refresh that starts later succeeds in that family. (pending)
- **R9** [tla, test] After logout-all returns, no refresh that starts later succeeds in any of that user's families created before the call. (pending)
- **R10** [test] Logout with a malformed or unknown refresh token returns success and changes nothing. (pending)
- **R24** [test] While Redis is unreachable, logout with a well-formed refresh token returns an error. (pending)
  - Why: R10 returns success for an unknown token. During an outage that would be a false "logged out".

## Inactive users

- **R11** [test] A refresh for a user marked inactive fails.
- **R26** [test] After a refresh fails because the user is inactive, no refresh in that family ever succeeds.

## Outages

- **R17** [test] While Redis is unreachable, no refresh succeeds.
- **R18** [test] While Postgres is unreachable, no refresh succeeds.

## Storage and secrets

- **R12** [test] Redis stores only the SHA-256 hash of a refresh token, never the token itself. (pending)
- **R19** [test] The secret part of every refresh token is 32 bytes long. (pending)
  - Assumes: A3.
- **R20** [test] No log line or error response contains a refresh token. (pending)
- **R13** [kani] `RefreshToken::parse` never panics, for any input up to 128 bytes. (pending)
