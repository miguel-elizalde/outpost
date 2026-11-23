#!/usr/bin/env bash
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

has() { git grep --untracked -qE "$@" 2>/dev/null; }

fail=0
seen=" "
for file in spec/*.md; do
  case "$(basename "$file")" in README.md | _template.md) continue ;; esac
  while IFS= read -r line; do
    id="$(sed -E 's/^[[:space:]]*- \*\*([A-Z]+[0-9]+)\*\*.*/\1/' <<<"$line")"
    lower="$(tr '[:upper:]' '[:lower:]' <<<"$id")"
    case "$seen" in *" $id "*) echo "FAIL $id: used twice in spec/"; fail=1 ;; esac
    seen="$seen$id "
    if ! grep -qE '\*\* \[[^]]+\]' <<<"$line"; then
      echo "FAIL $id: no tag ($file)"; fail=1; continue
    fi
    if grep -q '(pending)' <<<"$line"; then
      echo "pending $id"; continue
    fi
    tags="$(sed -E 's/^[^[]*\[([^]]+)\].*/\1/' <<<"$line" | tr ',' ' ')"
    for tag in $tags; do
      case "$tag" in
        tla) has "(^|[^A-Za-z0-9_])${id}_" -- 'verify/tla/*.tla' ;;
        lean) has "theorem[[:space:]]+([A-Za-z0-9_.]*\.)?${id}_" -- 'verify/lean/*.lean' ;;
        kani) has "fn[[:space:]]+${lower}_" -- '*.rs' ;;
        test) has "(^|[^A-Za-z0-9])(${id}|${lower})_" -- . ':!spec' ':!verify/tla' ':!verify/lean' ;;
        *) echo "FAIL $id: unknown tag [$tag]"; fail=1; continue ;;
      esac || { echo "FAIL $id: no [$tag] check named ${id}_..."; fail=1; }
    done
  done < <(grep -E '^[[:space:]]*- \*\*[A-Z]+[0-9]+\*\*' "$file" || true)
done
exit "$fail"
