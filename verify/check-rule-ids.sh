#!/usr/bin/env bash
# Every unmarked rule in spec/ must have one check per tag, named with its ID.
# Usage: check-rule-ids.sh [--ready]   --ready also says, for each marked rule, which checks exist and which are missing.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
ready="${1:-}"

# git grep exits 1 on no match. Never let that kill a pipeline.
found() { git grep --untracked -hE "$@" 2>/dev/null || true; }
has() { [ -n "$(found "$@")" ]; }

# Prints why the rule has no check for this tag. Prints nothing when it has one.
missing() {
  local id="$1" lower="$2" tag="$3" start='(^|[^A-Za-z0-9_])'
  case "$tag" in
    tla)
      has "${start}${id}_" -- 'verify/tla/*.tla' || { echo "no TLA+ definition named ${id}_..."; return; }
      has "${start}${id}_" -- 'verify/tla/*.cfg' ':!verify/tla/*witness.cfg' ||
        echo "${id}_... is in no .cfg, so TLC never checks it"
      ;;
    lean)
      has "theorem[[:space:]]+([A-Za-z0-9_.]*\.)?${id}_" -- 'verify/lean/*.lean' ||
        echo "no Lean theorem named ${id}_..."
      ;;
    kani)
      grep -q 'kani::proof' <<<"$(found -B3 "fn[[:space:]]+${lower}_" -- '*.rs')" ||
        echo "no #[kani::proof] fn named ${lower}_..."
      ;;
    test)
      has "(^|[^A-Za-z0-9])(${id}|${lower})_[A-Za-z0-9_]*[[:space:]]*\(" \
        -- . ':!spec' ':!verify/tla' ':!verify/lean' ':!*.md' ||
        echo "no test function named ${id}_... or ${lower}_..."
      ;;
  esac
}

fail=0
seen=" "
checked=0 pending=0 planned=0
for file in spec/*.md; do
  case "$(basename "$file")" in README.md | _template.md) continue ;; esac

  # A bold ID on a line that is not a rule line would be skipped without a word.
  while IFS= read -r bad; do
    echo "FAIL $file: looks like a rule but is not written as one: $bad"; fail=1
  done < <(grep -E '\*\*[A-Za-z]+[0-9]+\*\*' "$file" | grep -vE '^[[:space:]]*- \*\*[A-Z]+[0-9]+\*\*' || true)

  while IFS= read -r line; do
    id="$(sed -E 's/^[[:space:]]*- \*\*([A-Z]+[0-9]+)\*\*.*/\1/' <<<"$line")"
    lower="$(tr '[:upper:]' '[:lower:]' <<<"$id")"
    case "$seen" in *" $id "*) echo "FAIL $id: used twice in spec/"; fail=1 ;; esac
    seen="$seen$id "
    if ! grep -qE '\*\* \[[^]]+\]' <<<"$line"; then
      echo "FAIL $id: no tag ($file)"; fail=1; continue
    fi
    tags="$(sed -E 's/^[^[]*\[([^]]+)\].*/\1/' <<<"$line" | tr ',' ' ')"
    bad_tag=0
    for tag in $tags; do
      case "$tag" in tla | lean | kani | test) ;; *) echo "FAIL $id: unknown tag [$tag]"; fail=1; bad_tag=1 ;; esac
    done
    [ "$bad_tag" -eq 0 ] || continue

    marker="$(grep -oE '\((pending|planned)\)[[:space:]]*$' <<<"$line" | tr -d '() ' || true)"
    if [ -n "$marker" ] && [ "$ready" != "--ready" ]; then
      echo "$marker $id"
      if [ "$marker" = pending ]; then pending=$((pending + 1)); else planned=$((planned + 1)); fi
      continue
    fi

    gaps=""
    lack=""
    for tag in $tags; do
      why="$(missing "$id" "$lower" "$tag")"
      [ -z "$why" ] || { gaps="${gaps}FAIL $id [$tag]: $why"$'\n'; lack="$lack $tag"; }
    done
    if [ -n "$marker" ]; then
      if [ -z "$gaps" ]; then echo "$marker $id (checks found, ready for review)"; else echo "$marker $id (missing:$lack)"; fi
      if [ "$marker" = pending ]; then pending=$((pending + 1)); else planned=$((planned + 1)); fi
    elif [ -n "$gaps" ]; then
      printf '%s' "$gaps"; fail=1
    else
      checked=$((checked + 1))
    fi
  done < <(grep -E '^[[:space:]]*- \*\*[A-Z]+[0-9]+\*\*' "$file" || true)
done
echo "rules: $checked checked, $pending pending, $planned planned"
exit "$fail"
