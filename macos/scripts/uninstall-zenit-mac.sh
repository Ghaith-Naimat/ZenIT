#!/bin/zsh
# Removes ZenIT.app. Pass --purge to also delete per-user ZenIT data
# (config, policy, logs, reports).

set -euo pipefail

PURGE="${1:-}"

for target in "/Applications/ZenIT.app" "$HOME/Applications/ZenIT.app"; do
    if [[ -d "$target" ]]; then
        rm -rf "$target"
        echo "Removed: $target"
    fi
done

if [[ "$PURGE" == "--purge" ]]; then
    rm -rf "$HOME/Library/Application Support/ZenIT"
    rm -rf "$HOME/Library/Logs/ZenIT"
    echo "Removed per-user ZenIT data."
else
    echo "Per-user data kept at ~/Library/Application Support/ZenIT (use --purge to remove)."
fi

echo "Done."
