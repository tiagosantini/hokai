#!/usr/bin/env bash
set -euo pipefail

# ------------------------------------------------
# Hokai Installer — Linux & macOS
# Idempotent. Safe to run multiple times.
# ------------------------------------------------

REPO="tiagosantini/hokai"
INSTALL_VERSION="${HOKAI_VERSION:-latest}"
INSTALL_DIR="${HOKAI_INSTALL_DIR:-/usr/local/bin}"
BINARY_NAME="hokai"
SKIP_SERVICE=false
TEMP_DIR=$(mktemp -d)

# --- Portable SHA-256 ---
sha256hash() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | awk '{print $1}'
    elif command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | awk '{print $1}'
    elif command -v openssl >/dev/null 2>&1; then
        openssl dgst -sha256 "$1" | awk '{print $NF}'
    else
        echo "No SHA-256 tool found (sha256sum, shasum, or openssl). Skipping." >&2
    fi
}

cleanup() { rm -rf "$TEMP_DIR"; }
trap cleanup EXIT

# --- Help ---
if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    echo "Usage: install.sh [--version vX.Y.Z] [--skip-service] [--help]"
    echo ""
    echo "Environment variables:"
    echo "  HOKAI_VERSION      Version to install (default: latest)"
    echo "  HOKAI_INSTALL_DIR  Installation directory (default: /usr/local/bin)"
    exit 0
fi

# --- Parse arguments ---
while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)
            INSTALL_VERSION="$2"
            shift 2
            ;;
        --skip-service)
            SKIP_SERVICE=true
            shift
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1
            ;;
    esac
done

# --- Platform detection ---
detect_platform() {
    local os arch
    case "$(uname -s)" in
        Linux)  os="linux" ;;
        Darwin) os="osx"   ;;
        *) echo "Unsupported OS: $(uname -s)" >&2; exit 1 ;;
    esac
    case "$(uname -m)" in
        x86_64)  arch="x64"   ;;
        aarch64) arch="arm64" ;;
        arm64)   arch="arm64" ;;
        *) echo "Unsupported arch: $(uname -m)" >&2; exit 1 ;;
    esac
    echo "${os}-${arch}"
}

# --- Download binary ---
download_binary() {
    local platform url checksum_url
    platform=$(detect_platform)

    if [ "$INSTALL_VERSION" = "latest" ]; then
        echo "Resolving latest release..."
        local release_json
        release_json=$(curl -fsSL -H "Accept: application/vnd.github+json" \
            "https://api.github.com/repos/${REPO}/releases/latest") || {
            echo "Could not resolve latest release. Specify --version explicitly." >&2
            exit 1
        }
        INSTALL_VERSION=$(echo "$release_json" | grep -o '"tag_name": *"[^"]*"' | head -1 | sed 's/.*"tag_name": *"\([^"]*\)".*/\1/')
        if [ -z "$INSTALL_VERSION" ]; then
            echo "Could not parse latest release tag. Specify --version explicitly." >&2
            exit 1
        fi
        echo "Latest release: $INSTALL_VERSION"
    fi
    url="https://github.com/${REPO}/releases/download/${INSTALL_VERSION}/hokai-${platform}.tar.gz"
    checksum_url="https://github.com/${REPO}/releases/download/${INSTALL_VERSION}/SHA256SUMS"

    echo "Downloading hokai ${INSTALL_VERSION} for ${platform}..."
    curl -fsSLo "$TEMP_DIR/hokai.tar.gz" "$url" || {
        echo "Download failed. Trying with wget..." >&2
        wget -qO "$TEMP_DIR/hokai.tar.gz" "$url"
    }

    echo "Downloading checksums..."
    curl -fsSLo "$TEMP_DIR/SHA256SUMS" "$checksum_url" 2>/dev/null || \
        wget -qO "$TEMP_DIR/SHA256SUMS" "$checksum_url" 2>/dev/null || true

    if [ -f "$TEMP_DIR/SHA256SUMS" ]; then
        echo "Verifying checksum..."
        local expected
        expected=$(grep "hokai-${platform}.tar.gz" "$TEMP_DIR/SHA256SUMS" | awk '{print $1}')
        if [ -n "$expected" ]; then
            local actual
            actual=$(sha256hash "$TEMP_DIR/hokai.tar.gz")
            if [ "$expected" != "$actual" ]; then
                echo "Checksum mismatch! Aborting." >&2
                exit 1
            fi
            echo "Checksum verified."
        fi
    fi

    tar -xzf "$TEMP_DIR/hokai.tar.gz" -C "$TEMP_DIR"
}

# --- Installation ---
install_binary() {
    local dest="$INSTALL_DIR/$BINARY_NAME"

    if [ -f "$dest" ]; then
        echo "Existing installation found at ${dest}. Replacing..."
    fi

    mkdir -p "$INSTALL_DIR"
    cp "$TEMP_DIR/$BINARY_NAME" "$dest"
    chmod +x "$dest"
    echo "Binary installed to ${dest}"
}

# --- Service setup ---
install_service() {
    if [ "$SKIP_SERVICE" = true ]; then
        echo "Skipping service installation."
        return
    fi

    if [ "$(uname -s)" = "Linux" ]; then
        echo "Installing systemd service..."
        "$INSTALL_DIR/$BINARY_NAME" service install
    elif [ "$(uname -s)" = "Darwin" ]; then
        echo "Installing launchd service..."
        "$INSTALL_DIR/$BINARY_NAME" service install
    fi
}

# --- Execution ---
echo "Hokai Installer ${INSTALL_VERSION}"
download_binary
install_binary

if [ "$SKIP_SERVICE" = false ]; then
    install_service
fi

echo ""
echo "Hokai installed successfully."
echo "  Binary:  ${INSTALL_DIR}/${BINARY_NAME}"
echo ""
echo "To add an endpoint:  hokai endpoint add <url>"
echo "To check status:      hokai status"
echo "To start daemon:      hokai run"
echo ""
