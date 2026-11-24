## Verify

`just verify`

## Areas

- `auth`: `apps/auth/**`
- `key-provider`: `apps/key-provider/**`, `libs/**`
- `proto`: `proto/**`, `codegen/**`, `buf.yaml`
- `examples`: `examples/**`
- `verify`: `verify/**`
- `deps`: `Cargo.toml`, `**/Cargo.toml`, `Cargo.lock`, `Directory.Packages.props`, `**/*.csproj`, `**/packages.lock.json`, `**/pyproject.toml`, `**/uv.lock`
- `migrations`: `**/[Mm]igrations/**`
- `root-config`: `*`, `.cargo/**`, `.config/**`, `.github/**`

A file that matches more than one area belongs to the most specific one only. Order, most specific first:

1. `migrations`
2. `deps`
3. `proto`
4. `auth`, `key-provider`, `examples`, `verify`
5. `root-config`

Globs follow git `:(glob)` pathspec rules: `*` stays inside one folder, `**` crosses folders. Check an area with `git ls-files ':(glob)<pattern>'`.

## Spec

Rules live in `spec/`. Read `spec/README.md` before touching `verify/`. Never edit `spec/`, markers included.

A rule is done for you when `verify/check-rule-ids.sh --ready` lists it as ready for review. The `spec-ready` workflow then opens the PR that removes its marker.
