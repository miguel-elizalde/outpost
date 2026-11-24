# Shared

Terms, assumptions and non-goals for every file in `spec/`. No rules live here.

## Terms

- Family: all refresh tokens that descend from one login.
- Live token: the one refresh token in a family that was returned to a client and has not been used, expired or revoked.
- Rotated-out token: a refresh token that already refreshed once.
- Revoked family: a family in which no token can ever refresh again.
- Succeeds: the call returns tokens. Anything else is a failure.
- Starts later: the call begins after the earlier call returned.
- Unreachable: every request to that dependency fails or times out.

## Assumptions

- A1. The clocks of auth, the key provider, Redis and Postgres agree within one second and never jump backwards.
- A2. Redis never drops a written entry before its expiry (`maxmemory-policy noeviction`).
- A3. Refresh token secrets are 32 bytes from the operating system's secure random generator, so they are unique and cannot be guessed.
- A4. Exactly one key provider instance runs.
- A5. The network between client and auth is encrypted by the deployment.
- A6. Only auth can reach the key provider's sign call. Delete this once K11 is checked.
- A7. Redis runs a `MULTI` transaction as one step.
- A8. Only auth and the key provider write to Redis and Postgres.

## Non-goals

- Access tokens stay valid after logout, logout-all, deactivation and password change until they expire.
- No second factor.
- Tokens are not bound to a device. A stolen refresh token works from anywhere until rotation catches it.
- Register reveals whether a username exists (L2).
- No account recovery or password reset.
- No way to retire a leaked signing key early. A key only leaves by expiring.
- A refresh response lost on the network logs that session out. This is the cost of strict reuse detection.
- Third-party apps must never collect Outpost passwords.
