#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 <version>"; exit 2
fi
VER="$1"

git add -A
git commit -m "chore(release): $VER" || echo "No changes to commit"
git tag -a "$VER" -m "Release $VER"

echo "Created tag $VER locally. To push and create GitHub release run:" 
echo "  git push origin $VER"
echo "CI will create a release when the tag is pushed."
