#!/usr/bin/env python3
"""Build a Windows portable zip archive from a published desktop app directory."""

from __future__ import annotations

import argparse
import logging
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


LOGGER = logging.getLogger("cotton-sync-windows-zip")


def parse_args() -> argparse.Namespace:
    """Parse command-line arguments for the packaging script."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("publish_dir", type=Path, help="Published win-x64 app directory.")
    parser.add_argument("output_zip", type=Path, help="Output zip archive path.")
    return parser.parse_args()


def build_zip(publish_dir: Path, output_zip: Path) -> None:
    """Create a zip archive containing every file under the publish directory."""
    resolved_publish_dir = publish_dir.resolve()
    if not resolved_publish_dir.is_dir():
        raise FileNotFoundError(f"Publish directory was not found: {resolved_publish_dir}")

    executable_path = resolved_publish_dir / "Cotton.Sync.Desktop.exe"
    if not executable_path.is_file():
        raise FileNotFoundError(f"Windows executable was not found: {executable_path}")

    checksums_path = resolved_publish_dir / "checksums.sha256"
    if not checksums_path.is_file():
        raise FileNotFoundError(f"Publish checksums were not found: {checksums_path}")

    output_zip.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(output_zip, "w", ZIP_DEFLATED) as archive:
        for path in sorted(resolved_publish_dir.rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(resolved_publish_dir).as_posix())

    LOGGER.info("Created %s from %s", output_zip, resolved_publish_dir)


def main() -> int:
    """Run the packaging script."""
    logging.basicConfig(level=logging.INFO, format="%(message)s")
    args = parse_args()
    build_zip(args.publish_dir, args.output_zip)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
