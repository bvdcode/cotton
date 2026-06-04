#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "Usage: package-deb.sh <publish-dir> <output-deb> <version>" >&2
  exit 2
fi

publish_dir="$(realpath "$1")"
output_deb="$(realpath "$2")"
version="$3"

if [ ! -x "$publish_dir/Cotton.Sync.Desktop" ]; then
  echo "Linux publish directory must contain executable Cotton.Sync.Desktop." >&2
  exit 1
fi

if [ ! -f "$publish_dir/Assets/icon-192.png" ]; then
  echo "Linux publish directory must contain Assets/icon-192.png." >&2
  exit 1
fi

if [ ! -f "$publish_dir/checksums.sha256" ]; then
  echo "Linux publish directory must contain checksums.sha256." >&2
  exit 1
fi

package_root="$(mktemp -d)"
trap 'rm -rf "$package_root"' EXIT

install -d "$package_root/DEBIAN"
install -d "$package_root/opt/cotton-sync"
install -d "$package_root/usr/bin"
install -d "$package_root/usr/share/applications"
install -d "$package_root/usr/share/icons/hicolor/192x192/apps"

cp -a "$publish_dir/." "$package_root/opt/cotton-sync/"
chmod 755 "$package_root/opt/cotton-sync/Cotton.Sync.Desktop"
ln -s /opt/cotton-sync/Cotton.Sync.Desktop "$package_root/usr/bin/cotton-sync"
install -m 644 "$publish_dir/Assets/icon-192.png" \
  "$package_root/usr/share/icons/hicolor/192x192/apps/cotton-sync.png"

cat > "$package_root/usr/share/applications/cotton-sync.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Version=1.0
Name=Cotton Sync
GenericName=Cloud folder synchronization
Comment=Synchronize Cotton Cloud folders
Exec=/opt/cotton-sync/Cotton.Sync.Desktop
TryExec=/opt/cotton-sync/Cotton.Sync.Desktop
Icon=cotton-sync
Terminal=false
Categories=Network;FileTransfer;
StartupNotify=true
StartupWMClass=Cotton.Sync.Desktop
EOF

installed_size="$(du -sk "$package_root" | cut -f1)"

cat > "$package_root/DEBIAN/control" <<EOF
Package: cotton-sync-desktop
Version: $version
Section: net
Priority: optional
Architecture: amd64
Maintainer: Vadim Belov <vadim@belov.us>
Depends: libsecret-tools
Installed-Size: $installed_size
Homepage: https://cottoncloud.dev
Description: Cotton Cloud desktop synchronization client
 Cotton Sync keeps local folders synchronized with Cotton Cloud.
EOF

dpkg-deb --root-owner-group --build "$package_root" "$output_deb"
