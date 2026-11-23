# Spec

The rules this repo must keep. I write them. Agents build the checks in `verify/`.

## Format

One file per area (`refresh.md`, `keys.md`), not per tool. Copy `_template.md`.

```md
- **R2** [tla, lean] Presenting a rotated-out token revokes the whole family.
  - Assumes: new tokens are unique (CSPRNG).
```

- The ID is letters then a number (`R2`, `K10`). It must be unique across every file in `spec/`.
- The tags say which tools check the rule. A rule can have more than one.
- List every assumption. Each one becomes a hypothesis in a proof.
- Add `(pending)` to a rule that has no check yet. It is reported, not failed.

## Tags

| The rule talks about | Tag | Tool |
|---|---|---|
| Order of events, parallel requests, retries, timeouts, several services or stores | `tla` | TLA+ model in `verify/tla/` |
| Critical pure logic that must hold for any history length or any size | `lean` | Lean proof in `verify/lean/` |
| A pure Rust function never panics, overflows, or breaks a bound | `kani` | Kani harness next to the code, in `#[cfg(kani)] mod verification` |
| I/O, config, libraries, UI, or a language with no prover | `test` | A test in the repo's own test suite |

## Names

Every check starts with its rule ID, so the two can be matched:

- TLA+ invariant: `R9_OneLivePerFamily`
- Lean theorem: `R2_reuse_revokes`
- Kani harness: `r7_expiry_no_overflow`
- Test: `r10_argon2_uses_configured_cost` or `R10_Argon2UsesConfiguredCost`

`verify/check-rule-ids.sh` fails when a tagged rule has no check with its ID. It only proves the check exists. Whether it means what the rule says is my review.

## For agents

- Take targets only from the tags here.
- Do not edit `spec/`, or weaken a statement or invariant to make a check pass.
- If a rule looks wrong or impossible, stop and say so.
- Every model needs a test that drives the real code into the same case.
