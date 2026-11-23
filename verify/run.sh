#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$(git rev-parse --show-toplevel)"
shopt -s nullglob

tlc_invariant_violated=12

die() { echo "FAIL $*" >&2; exit 1; }

"$here/check-rule-ids.sh"

if [ -d verify/lean ] && git grep --untracked -nw sorry -- 'verify/lean/*.lean'; then
  die "sorry in verify/lean"
fi

[ "${1:-}" = "--fast" ] && exit 0

for cfg in verify/tla/*.cfg; do
  base="$(basename "$cfg" .cfg)"
  model="${base%%.*}"
  [ -f "verify/tla/$model.tla" ] || die "$cfg has no $model.tla"
  : "${TLA2TOOLS:?set TLA2TOOLS to the path of tla2tools.jar}"
  want=0
  case "$base" in *witness) want="$tlc_invariant_violated" ;; esac
  if [ "$base" = "$model" ]; then
    witnesses=(verify/tla/"$model".*witness.cfg)
    [ "${#witnesses[@]}" -gt 0 ] || die "$model has no witness config ($model.witness.cfg)"
  fi
  meta="$(mktemp -d)"
  got=0
  (cd verify/tla && java -XX:+UseParallelGC -cp "$TLA2TOOLS" tlc2.TLC \
    -metadir "$meta" -config "$base.cfg" "$model.tla") >"$meta/out" 2>&1 || got=$?
  if [ "$got" -ne "$want" ]; then
    tail -n 40 "$meta/out"
    die "TLC on $base.cfg exited $got, expected $want"
  fi
  echo "ok   TLC $base.cfg"
  rm -rf "$meta"
done

if [ -f verify/lean/lakefile.toml ] || [ -f verify/lean/lakefile.lean ]; then
  (cd verify/lean && lake build)
  [ -f verify/lean/Axioms.lean ] || die "verify/lean/Axioms.lean is missing (one #print axioms per rule theorem)"
  out="$(cd verify/lean && lake env lean Axioms.lean | tr '\n' ' ')"
  while IFS= read -r name; do
    grep -qE "'([A-Za-z0-9_.]*\.)?$name'" <<<"$out" || die "$name is not in Axioms.lean"
  done < <(git grep --untracked -hoE 'theorem[[:space:]]+[A-Za-z0-9_.]*[A-Z]+[0-9]+_[A-Za-z0-9_]*' -- 'verify/lean/*.lean' |
    sed -E 's/.*[[:space:].]([A-Z]+[0-9]+_[A-Za-z0-9_]*)$/\1/' | sort -u)
  bad="$(grep -oE 'depends on axioms: \[[^]]*\]' <<<"$out" | sed -E 's/.*\[(.*)\]/\1/' | tr ',' '\n' |
    tr -d ' ' | grep -vxE 'propext|Classical\.choice|Quot\.sound|' | sort -u || true)"
  [ -z "$bad" ] || die "axioms outside the allowlist: $(tr '\n' ' ' <<<"$bad")"
  echo "ok   Lean build and axioms"
fi

if [ -f Cargo.toml ] && git grep --untracked -q 'kani::proof' -- '*.rs'; then
  cargo kani --workspace
fi
