# Spec

The rules this repo must keep. The spec says what I want, not what the code does today.

I approve every rule. Agents can propose rules in a PR and build the checks in `verify/`.

## Rule states

| Marker | Meaning | Gate |
|---|---|---|
| `(planned)` | I want it. The code does not do it yet. | Reported |
| `(pending)` | The code should already do it. No approved check yet. | Reported |
| none | Checked and approved. | The build fails if the check is missing or fails |

- Agents never change a marker. When a marked rule has a check for every tag, the `spec-ready` workflow opens a PR that removes the marker.
- I review that PR and add `protected-ok`. That is my sign-off that the check means what the rule says.
- Agents never add the `protected-ok` label.

## Format

One file per area, not per tool. Copy `_template.md`.

```md
- **R4** [tla, lean, test] Presenting a rotated-out refresh token to refresh, before its family expires, revokes the whole family. (planned)
  - Assumes: A2. A3.
  - Why: RFC 9700 4.14.
```

- The tags say which tools check the rule. A rule with two tags needs one check per tag.
- `Assumes:` lists every assumption. Each one becomes a hypothesis in a proof or the setup of a test. `A1`, `A2`... are the shared assumptions in `shared.md`.
- `Why:` is optional. It names the source or the reason.
- No other sub-bullets.

## Writing a rule

- One sentence that is either true or false.
- One claim per rule. If it has "and", split it.
- About behavior a client can see. Storage and secrecy rules are the exception ("Redis stores only the hash").
- Use the words in `shared.md` (family, live token, succeeds, starts later). Do not invent synonyms.
- Say what must work, not only what must never happen. A service that rejects everything must fail the spec.

## IDs

| Letter | File |
|---|---|
| R | `refresh.md` |
| L | `login.md` |
| K | `keys.md` |
| J | `jwks.md` |
| T | `tokens.md` |

- The ID is one letter then a number. It is unique across `spec/`.
- Never reuse an ID. A dropped rule leaves a gap.
- `A` is reserved for assumptions.

## Shared file

`shared.md` holds three things that apply to every rule:

- **Terms.** The words rules must use.
- **Assumptions.** `A1`, `A2`... Written without bold or tags, so the checker does not read them as rules.
- **Non-goals.** What the spec leaves out on purpose. A dropped rule moves here with one line saying why.

## Tags

| The rule talks about | Tag | Tool |
|---|---|---|
| Order of events, parallel requests, retries, timeouts, several services or stores | `tla` | TLA+ model in `verify/tla/` |
| Critical pure logic that must hold for any history length or any size | `lean` | Lean proof in `verify/lean/` |
| A pure Rust function never panics, overflows, or breaks a bound | `kani` | Kani harness next to the code, in `#[cfg(kani)] mod verification` |
| I/O, config, libraries, UI, or a language with no prover | `test` | A test in the repo's own test suite |

## Names

Every check starts with its rule ID, so the two can be matched:

- TLA+ invariant: `R3_OneLivePerFamily`
- Lean theorem: `R5_revoked_stays_revoked`
- Kani harness: `r21_expiry_never_panics`
- Test: `l4_argon2_uses_configured_cost` or `L4_Argon2UsesConfiguredCost`

`verify/check-rule-ids.sh` fails when an unmarked rule has no check with its ID. What counts as a check:

- `tla`: a definition named with the ID in a `.tla` file, and listed in a `.cfg` that is not a witness config.
- `lean`: a `theorem` named with the ID.
- `kani`: a `fn` named with the ID under `#[kani::proof]`.
- `test`: a function named with the ID, in any code file.

It only proves the check exists. Whether it means what the rule says is my review. `verify/check-rule-ids.sh --ready` lists marked rules whose checks already exist.

## Changing a rule

- Changing a rule's sentence or assumptions puts `(pending)` back. Its checks need a new review.
- Dropping a rule removes its checks in the same PR.

## For agents

- Take targets only from the tags here.
- Do not edit `spec/`. That includes markers. To propose a rule or a change, open a PR that touches only `spec/` and stop.
- A rule is done for you when `verify/check-rule-ids.sh --ready` lists it as ready for review.
- Do not weaken a statement, invariant or assumption to make a check pass.
- For a `(pending)` rule: build the check. If it fails against the code, stop and say so. Do not change the code or the rule.
- For a `(planned)` rule: build the check first and show it failing. Then change the code until it passes. Both land in one PR.
- One fix or one model per PR. The issue names the rules and tags it covers.
- If a rule looks wrong or impossible, stop and say so.
- Every model needs a test that drives the real code into the same case.
