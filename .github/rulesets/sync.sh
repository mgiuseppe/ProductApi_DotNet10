#!/usr/bin/env bash
#
# Applies the rulesets in this folder to the GitHub repository: creates the missing ones, updates the
# existing ones. Modelled on https://github.com/oBusk/rulesets (sync.sh).
#
# Why a script: GitHub does not read these JSON files by itself. A ruleset lives in the repository settings
# (Settings -> Rules -> Rulesets); the files are the versioned copy, and this script sends them through the
# REST API. After changing a file, run the script again: changes made in the GitHub UI are not written back
# to the files, and the next run overwrites them.
#
# Usage (Git Bash on Windows, or any bash; needs gh, logged in as a repository admin):
#   ./.github/rulesets/sync.sh                           # every *.json in this folder
#   ./.github/rulesets/sync.sh main-require-pr.json      # only the given file(s)
#
# Several rulesets on the same branch add up: GitHub applies all their rules, and when two rules overlap
# the most restrictive one wins. That is what allows splitting the protection of main into separate files.

# Stop at the first failing command (-e), treat unset variables as errors (-u), and make a pipeline fail
# when any command in it fails (-o pipefail).
set -euo pipefail

# Git Bash rewrites arguments that look like Unix paths into Windows paths; turn it off (no effect elsewhere).
export MSYS_NO_PATHCONV=1

dir=$(cd "$(dirname "$0")" && pwd)

# The repository is the one of the current checkout (its "origin" remote), so nothing is hard-coded.
repo=$(gh repo view --json nameWithOwner --jq .nameWithOwner)

# The files to apply: the arguments, resolved against this folder, or every *.json in it.
if [[ $# -gt 0 ]]; then
  files=()
  for arg in "$@"; do files+=("$dir/$(basename "$arg")"); done
else
  files=("$dir"/*.json)
fi

# The REST API identifies a ruleset by a numeric id, not by its name. Read the existing ones once, as
# "id<TAB>name<TAB>url" lines, and match the files to them by name (the "name" field of each file).
existing=$(gh api "repos/$repo/rulesets" --paginate --jq '.[] | [.id, .name, ._links.html.href] | @tsv')

# The ruleset name written in a file: its top-level "name" field (two-space indentation, as in these files).
# Plain sed instead of jq, which Git Bash does not ship.
name_of() { sed -n 's/^  "name": *"\([^"]*\)".*/\1/p' "$1"; }

# The id of an existing ruleset with the given name, empty when there is none.
id_of() { awk -F'\t' -v name="$1" '$2 == name { print $1; exit }' <<<"$existing"; }

echo "Repository: $repo"

for file in "${files[@]}"; do
  name=$(name_of "$file")
  id=$(id_of "$name")
  if [[ -n "$id" ]]; then
    # PUT replaces the whole ruleset with the content of the file
    gh api --method PUT "repos/$repo/rulesets/$id" --input "$file" --silent
    echo "updated    $name"
  else
    gh api --method POST "repos/$repo/rulesets" --input "$file" --silent
    echo "created    $name"
  fi
done

# Rulesets in the repository that no file in this folder describes (created by hand, or renamed files).
# They keep applying next to these, so the script only lists them: review and delete them in the UI.
managed=$(for file in "$dir"/*.json; do name_of "$file"; done)
while IFS=$'\t' read -r _ name url; do
  [[ -n "$name" ]] || continue
  grep -qxF "$name" <<<"$managed" || echo "unmanaged  $name  $url"
done <<<"$existing"
