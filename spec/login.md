# Login and register

Username and password accounts in Postgres.

Terms and assumptions are in `shared.md`.

## Usernames

- **L1** [test] Usernames match ignoring ASCII case.
- **L28** [test] Usernames match ignoring leading or trailing whitespace.
- **L2** [test] Registering a username that already exists, in any case, fails and leaves the first account unchanged.
- **L26** [test] Register rejects a username that is empty after trimming whitespace.
- **L27** [test] Register rejects a username with any character outside `a-z`, `0-9`, `.`, `_` and `-`, after lowercasing. (planned)
  - Why: ASCII-only names remove lookalike usernames and make L1 exact.

## Passwords

- **L3** [test] Register and login reject a username over 256 characters or a password over 1024 bytes without hashing it. (pending)
- **L4** [test] Every stored password hash is Argon2id with at least 19 MiB of memory and 2 iterations. (pending)
  - Why: the OWASP minimum. Today this is the library default, not a setting.
- **L29** [test] Login fails when auth runs with a different pepper than the one the password was registered with. (pending)
  - Why: NIST SP 800-63B-4 3.1.1.2 recommends a secret key stored apart from the hashes.
- **L11** [test] Registering the same password for two users stores two different hashes. (pending)
  - Why: NIST SP 800-63B-4 3.1.1.2 requires a salt.
- **L12** [test] Login fails for a password that differs from the registered one only in its last byte, at the maximum length. (pending)
  - Why: NIST requires verifying the whole password, with no truncation.
- **L15** [test] Register rejects a password shorter than 15 Unicode code points.
  - Why: NIST SP 800-63B-4 3.1.1.2, the minimum for password-only login.
- **L16** [test] Register accepts every password of 15 to 64 code points that L18 does not reject, including spaces and Unicode.
  - Why: NIST forbids composition rules.
- **L17** [test] A password logs in whether its Unicode characters arrive composed or decomposed. (planned)
  - Why: NIST recommends NFC normalization before hashing.
- **L18** [test] Register rejects a password that is on the blocklist or equal to the username. (planned)
  - Why: NIST SP 800-63B-4 3.1.1.2.
- **L21** [test] After the Argon2id cost is raised, a user's next successful login stores a hash at the new cost. (planned)

## Login

- **L8** [test] Login with the right password for an active user succeeds.
  - Assumes: Redis, Postgres and the key provider are reachable.
- **L9** [test] After a successful register, login with the same username and password succeeds.
  - Assumes: Redis, Postgres and the key provider are reachable.
- **L5** [test] Login for an unknown username runs one password verify, the same work as for a known username. (pending)
- **L6** [test] Login with the right password for an inactive user fails.
- **L7** [test] Login returns the same error to the client for an unknown user, a wrong password, an inactive user and over-long input. (pending)

## Throttling

- **L19** [tla, test] In any `login_window_secs` window, auth checks at most `max_login_attempts` wrong passwords for one username, even when attempts overlap. (planned)
  - Assumes: `max_login_attempts` is 100 or less. A2.
  - Why: NIST SP 800-63B-4 3.2.2. An attacker can spend the attempts to lock a user out for one window. That is the trade.
- **L20** [test] L19 counts attempts for a username that does not exist the same way. (planned)
  - Why: otherwise throttling reveals which usernames exist, which breaks L7.
- **L30** [test] A throttled login returns a "too many attempts" error that says how long to wait. (planned)
  - Why: a person who mistypes should know to wait. L20 keeps this from revealing which usernames exist.

## Password change

- **L22** [test] A password change fails unless the request carries the user's current password. (planned)
- **L23** [test] After a password change returns, login with the old password fails. (planned)
- **L24** [tla, test] After a password change returns, no refresh that starts later succeeds in any of that user's families created before it. (planned)
  - Why: RFC 9700 4.14. This includes the session that made the change. The client logs in again.

## Secrets

- **L13** [test] No log line or error response contains a password. (pending)
- **L25** [test] No response from auth contains a password hash. (pending)
