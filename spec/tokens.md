# Access tokens

The JWTs auth returns from login and refresh. The key provider signs them.

Terms and assumptions are in `shared.md`.

## Signature

- **T1** [test] Every access token auth returns verifies with the public key the JWKS lists under the token's `kid`. (pending)
- **T2** [test] Every access token's header says `alg` EdDSA.
  - Assumes: T9.
  - Why: RFC 8725 3.1 and 3.2.
- **T9** [test] The key provider refuses to start when `Signing:Algorithm` is not `EdDSA`. (planned)
  - Why: the setting is free text. Any other value makes the header lie while the signature stays Ed25519.
- **T6** [test] Every access token's header has `typ` set to `at+jwt`. (planned)
  - Why: RFC 8725 3.11. It stops another kind of JWT signed by the same keys from passing as an access token.

## Claims

- **T3** [test] An access token returned by register or login has `sub` equal to the ID of the user whose username and password were presented.
- **T5** [test] Every access token's `iss` equals the key provider's `Signing:Issuer`. (pending)
  - Why: RFC 8725 3.8.
- **T7** [test] Every access token's `aud` equals the audience named in the login request. (planned)
  - Why: RFC 8725 3.9, RFC 9700 2.3. Without it, a token from one side project works on another.
- **T8** [test] Login rejects an audience that is not in the configured list. (planned)
