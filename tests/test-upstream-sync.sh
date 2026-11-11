#!/usr/bin/env bash
set -euo pipefail

# Find repo root
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SYNC_SCRIPT="$REPO_ROOT/scripts/upstream-sync.sh"

echo "=== Running tests for upstream-sync.sh ==="

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

TEST_REPO="$TMP_DIR/repo"
UPSTREAM_REPO="$TMP_DIR/upstream"
CACHE_DIR="$TMP_DIR/cache"
mkdir -p "$CACHE_DIR"

# Setup upstream repo
mkdir -p "$UPSTREAM_REPO"
cd "$UPSTREAM_REPO"
git init -b master
git config user.name "Upstream Author"
git config user.email "upstream@example.com"
echo "Initial upstream content" > file.txt
git add file.txt
git commit -m "upstream: initial commit"

# Setup fork repo
git clone "$UPSTREAM_REPO" "$TEST_REPO"
cd "$TEST_REPO"
git config user.name "Fork Author"
git config user.email "fork@example.com"
git remote add upstream "$UPSTREAM_REPO"

# Test 1: Clean rebase
echo "--> Test 1: Clean rebase"
# Upstream adds commit
cd "$UPSTREAM_REPO"
echo "Upstream feature" > upstream_feature.txt
git add upstream_feature.txt
git commit -m "upstream: add feature"

# Fork adds independent commit
cd "$TEST_REPO"
echo "Fork feature" > fork_feature.txt
git add fork_feature.txt
git commit -m "fork: add feature"

# Run sync
"$SYNC_SCRIPT" --sync-rebase upstream master
# Assert exit code 0 and both files present
if [ ! -f upstream_feature.txt ] || [ ! -f fork_feature.txt ]; then
    echo "FAIL: Test 1 failed, missing files after rebase"
    exit 1
fi
echo "PASS: Test 1 passed"

# Test 2: Conflict without rerere (should abort and exit 1)
echo "--> Test 2: Conflict without rerere"
cd "$UPSTREAM_REPO"
echo "Upstream change in file.txt" > file.txt
git add file.txt
git commit -m "upstream: modify file.txt"

cd "$TEST_REPO"
echo "Fork conflicting change in file.txt" > file.txt
git add file.txt
git commit -m "fork: conflict in file.txt"
PRE_REBASE_SHA=$(git rev-parse HEAD)

if "$SYNC_SCRIPT" --sync-rebase upstream master; then
    echo "FAIL: Expected sync to fail on conflict, but it succeeded"
    exit 1
fi

# Assert rebase was aborted
CURRENT_SHA=$(git rev-parse HEAD)
if [ "$CURRENT_SHA" != "$PRE_REBASE_SHA" ]; then
    echo "FAIL: Expected HEAD to remain at $PRE_REBASE_SHA after aborted rebase, got $CURRENT_SHA"
    exit 1
fi
if [ -d .git/rebase-merge ] || [ -d .git/rebase-apply ]; then
    echo "FAIL: Rebase directory still exists, abort did not clean up"
    exit 1
fi
echo "PASS: Test 2 passed"

# Test 3: Conflict with rerere resolution
echo "--> Test 3: Conflict with rerere"
# Enable rerere in repo
git config rerere.enabled true
git config rerere.autoupdate true

# Manually trigger conflict and resolve it
git fetch upstream master
git checkout -b test-rerere-resolve "$PRE_REBASE_SHA"
set +e
git rebase upstream/master
REBASE_STATUS=$?
set -e
if [ $REBASE_STATUS -eq 0 ]; then
    echo "FAIL: Expected conflict during manual rebase"
    exit 1
fi

# Resolve conflict
echo "Resolved content for file.txt" > file.txt
git add file.txt
git commit --no-edit
# Rebase finished
if [ -d .git/rebase-merge ]; then
    git rebase --continue
fi

# Save rerere cache
"$SYNC_SCRIPT" --save-rerere "$CACHE_DIR"
if [ ! -f "$CACHE_DIR/rr-cache.tar.gz" ]; then
    echo "FAIL: rr-cache.tar.gz was not created"
    exit 1
fi

# Switch back to master (which still has conflict) and reset rr-cache to test restore
git checkout master
rm -rf .git/rr-cache

# Restore rerere cache
"$SYNC_SCRIPT" --restore-rerere "$CACHE_DIR"
if [ ! -d .git/rr-cache ]; then
    echo "FAIL: .git/rr-cache was not restored"
    exit 1
fi

# Now run sync, should succeed automatically with rerere!
"$SYNC_SCRIPT" --sync-rebase upstream master
if [ "$(cat file.txt)" != "Resolved content for file.txt" ]; then
    echo "FAIL: Conflict was not resolved with recorded resolution"
    exit 1
fi
echo "PASS: Test 3 passed"

echo "=== All tests passed! ==="
