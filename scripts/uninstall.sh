#!/usr/bin/env bash
set -euo pipefail

# ------------------------------------------------
# Hokai Uninstaller — Linux & macOS
# Safe to run multiple times. No residual scans.
# ------------------------------------------------

INSTALL_DIR="${HOKAI_INSTALL_DIR:-/usr/local/bin}"
BINARY_NAME="hokai"
PURGE=false

# --- Help ---
if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    echo "Usage: uninstall.sh [--purge] [--help]"
    echo ""
    echo "Options:"
    echo "  --purge  Remove configuration, data, and logs as well"
    echo ""
    echo "Environment variables:"
    echo "  HOKAI_INSTALL_DIR  Installation directory (default: /usr/local/bin)"
    exit 0
fi

# --- Parse arguments ---
while [[ $# -gt 0 ]]; do
    case "$1" in
        --purge)
            PURGE=true
            shift
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1
            ;;
    esac
done

BINARY_PATH="$INSTALL_DIR/$BINARY_NAME"

# --- Stop and unregister service ---
if [ -x "$BINARY_PATH" ]; then
    echo "Stopping Hokai service..."
    "$BINARY_PATH" service stop 2>/dev/null || true

    if [ "$PURGE" = true ]; then
        echo "Removing service registration, config, and data..."
        "$BINARY_PATH" service uninstall --purge 2>/dev/null || true
    else
        echo "Removing service registration..."
        "$BINARY_PATH" service uninstall 2>/dev/null || true
    fi
else
    echo "Hokai binary not found at ${BINARY_PATH}. Cleaning up native service artifacts..."
    # Best-effort native cleanup without the binary
    if [ -f /etc/systemd/system/hokai.service ]; then
        systemctl stop hokai 2>/dev/null || true
        systemctl disable hokai 2>/dev/null || true
        rm -f /etc/systemd/system/hokai.service
        systemctl daemon-reload 2>/dev/null || true
        echo "Removed systemd service definition."
    fi
fi

# --- Remove binary ---
if [ -f "$BINARY_PATH" ]; then
    rm -f "$BINARY_PATH"
    echo "Binary removed from ${BINARY_PATH}"
fi

# --- Purge ---
if [ "$PURGE" = true ]; then
    echo "Removing configuration and data..."

    if [ "$(uname -s)" = "Linux" ]; then
        rm -rf /etc/hokai 2>/dev/null || true
        rm -rf /var/lib/hokai 2>/dev/null || true
    elif [ "$(uname -s)" = "Darwin" ]; then
        local home_dir="${HOME}"
        rm -rf "${home_dir}/Library/Application Support/Hokai" 2>/dev/null || true
        rm -rf "${home_dir}/Library/Logs/Hokai" 2>/dev/null || true
    fi
else
    echo "Config and data preserved. Use --purge to remove them."
fi

echo "Hokai uninstalled."
