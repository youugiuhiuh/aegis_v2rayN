#!/usr/bin/env bash
# Upstream synchronization and Git Rerere cache management script

set -euo pipefail

ensure_rerere_config() {
    git config rerere.enabled true
    git config rerere.autoupdate true
}

restore_rerere() {
    local cache_dir="$1"
    local tar_path="$cache_dir/rr-cache.tar.gz"
    ensure_rerere_config

    if [ -f "$tar_path" ]; then
        local git_dir
        git_dir="$(git rev-parse --git-dir)"
        mkdir -p "$git_dir/rr-cache"
        tar -xzf "$tar_path" -C "$git_dir/rr-cache"
        echo "Successfully restored rerere cache from $tar_path"
    else
        echo "No existing rerere cache tarball found at $tar_path (first run or empty)"
    fi
}

save_rerere() {
    local cache_dir="$1"
    local git_dir
    git_dir="$(git rev-parse --git-dir)"

    if [ -d "$git_dir/rr-cache" ] && [ "$(ls -A "$git_dir/rr-cache" 2>/dev/null)" ]; then
        mkdir -p "$cache_dir"
        tar -czf "$cache_dir/rr-cache.tar.gz" -C "$git_dir/rr-cache" .
        echo "Successfully saved rerere cache to $cache_dir/rr-cache.tar.gz"
    else
        echo "No rerere cache entries in $git_dir/rr-cache to save."
    fi
}

sync_rebase() {
    local upstream_remote="$1"
    local upstream_branch="$2"
    local target_ref="$upstream_remote/$upstream_branch"

    ensure_rerere_config

    echo "Fetching $target_ref..."
    git fetch "$upstream_remote" "$upstream_branch"

    if git merge-base --is-ancestor "$target_ref" HEAD; then
        echo "Current HEAD already contains $target_ref. Nothing to rebase."
        return 0
    fi

    echo "Attempting rebase onto $target_ref..."
    set +e
    git rebase "$target_ref"
    set -e

    local git_dir
    git_dir="$(git rev-parse --git-dir)"

    while [ -d "$git_dir/rebase-merge" ] || [ -d "$git_dir/rebase-apply" ]; do
        local unmerged
        unmerged="$(git diff --name-only --diff-filter=U || true)"
        if [ -n "$unmerged" ]; then
            echo "::error::Unresolved merge conflicts detected in files:" >&2
            echo "$unmerged" >&2
            git rebase --abort
            return 1
        fi

        echo "Conflicts auto-resolved by rerere. Continuing rebase..."
        set +e
        GIT_EDITOR=true git rebase --continue
        set -e
    done

    echo "Rebase onto $target_ref completed successfully."
    return 0
}

usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --restore-rerere <dir>                Restore .git/rr-cache from <dir>/rr-cache.tar.gz"
    echo "  --save-rerere <dir>                   Save .git/rr-cache to <dir>/rr-cache.tar.gz"
    echo "  --sync-rebase <remote> <branch>       Rebase current HEAD onto <remote>/<branch> with rerere"
    exit 1
}

main() {
    if [ $# -lt 2 ]; then
        usage
    fi

    case "$1" in
        --restore-rerere)
            restore_rerere "$2"
            ;;
        --save-rerere)
            save_rerere "$2"
            ;;
        --sync-rebase)
            if [ $# -lt 3 ]; then
                usage
            fi
            sync_rebase "$2" "$3"
            ;;
        *)
            usage
            ;;
    esac
}

main "$@"
