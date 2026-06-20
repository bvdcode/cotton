"""All-in-one generator: CottonCrypto sweep charts plus the OpenSSL comparison."""

import sys
from pathlib import Path
from typing import Optional

import matplotlib.pyplot as plt
import pandas as pd

from chart_common import (
    CHUNK_HEX_COLORS,
    MYLIB_INPUT_DEFAULT,
    OPENSSL_INPUT_DEFAULT,
    ROOT,
    THREAD_HEX_COLORS,
    create_advanced_plots,
    create_mega_analysis,
    parse_mylib_results,
    parse_openssl_results,
    plot_openssl_comparison,
)


def plot_mylib_four_panels(enc: pd.DataFrame, dec: pd.DataFrame, out_path: Path) -> None:
    """Create the 4-panel figure: throughput vs chunk size (per threads) and vs threads (per chunk)."""
    if enc.empty or dec.empty:
        print("[warn] CottonCrypto data is empty; skipping library_performance.png")
        return

    plt.rcParams["figure.facecolor"] = "white"
    plt.rcParams["axes.facecolor"] = "white"
    plt.rcParams["axes.grid"] = True
    plt.rcParams["grid.alpha"] = 0.3

    fig, axes = plt.subplots(2, 2, figsize=(16, 12))
    (ax1, ax2), (ax3, ax4) = axes
    fig.suptitle("CottonCrypto Performance: Encryption/Decryption Throughput", fontsize=16, fontweight="bold", y=0.98)

    unique_threads = sorted(enc["Threads"].unique())
    unique_chunks = sorted(enc["ChunkMB"].unique())

    for i, t in enumerate(unique_threads):
        d = enc[enc["Threads"] == t].sort_values("ChunkMB")
        ax1.plot(d["ChunkMB"], d["Throughput"], marker="o", label=f"{t} threads",
                 linewidth=2.0, markersize=7, color=THREAD_HEX_COLORS[i % len(THREAD_HEX_COLORS)])
    ax1.set_title("Encryption: Throughput vs Chunk Size", fontsize=14, fontweight="bold")
    ax1.set_xlabel("Chunk Size (MB)")
    ax1.set_ylabel("Throughput (MB/s)")
    ax1.set_xticks(unique_chunks)
    ax1.legend(frameon=True, fancybox=True)

    for i, t in enumerate(unique_threads):
        d = dec[dec["Threads"] == t].sort_values("ChunkMB")
        ax2.plot(d["ChunkMB"], d["Throughput"], marker="s", label=f"{t} threads",
                 linewidth=2.0, markersize=7, color=THREAD_HEX_COLORS[i % len(THREAD_HEX_COLORS)])
    ax2.set_title("Decryption: Throughput vs Chunk Size", fontsize=14, fontweight="bold")
    ax2.set_xlabel("Chunk Size (MB)")
    ax2.set_ylabel("Throughput (MB/s)")
    ax2.set_xticks(unique_chunks)
    ax2.legend(frameon=True, fancybox=True)

    for i, ch in enumerate(unique_chunks):
        d = enc[enc["ChunkMB"] == ch].sort_values("Threads")
        ax3.plot(d["Threads"], d["Throughput"], marker="o", label=f"{int(ch)}MB",
                 linewidth=2.0, markersize=7, color=CHUNK_HEX_COLORS[i % len(CHUNK_HEX_COLORS)])
    ax3.set_title("Encryption: Throughput vs Threads", fontsize=14, fontweight="bold")
    ax3.set_xlabel("Number of Threads")
    ax3.set_ylabel("Throughput (MB/s)")
    ax3.set_xticks(unique_threads)
    ax3.legend(title="Chunk Size", frameon=True, fancybox=True)

    for i, ch in enumerate(unique_chunks):
        d = dec[dec["ChunkMB"] == ch].sort_values("Threads")
        ax4.plot(d["Threads"], d["Throughput"], marker="s", label=f"{int(ch)}MB",
                 linewidth=2.0, markersize=7, color=CHUNK_HEX_COLORS[i % len(CHUNK_HEX_COLORS)])
    ax4.set_title("Decryption: Throughput vs Threads", fontsize=14, fontweight="bold")
    ax4.set_xlabel("Number of Threads")
    ax4.set_ylabel("Throughput (MB/s)")
    ax4.set_xticks(unique_threads)
    ax4.legend(title="Chunk Size", frameon=True, fancybox=True)

    for ax in (ax1, ax2, ax3, ax4):
        ax.grid(True, alpha=0.3)
        ax.spines["top"].set_visible(False)
        ax.spines["right"].set_visible(False)

    fig.tight_layout()
    fig.savefig(out_path, dpi=300, bbox_inches="tight")
    print(f"[ok] Saved {out_path.name}")


def _save_advanced(enc: pd.DataFrame, dec: pd.DataFrame, out_path: Path) -> None:
    if enc.empty or dec.empty:
        print("[warn] CottonCrypto data empty; skipping advanced_performance_analysis.png")
        return
    fig, _, _ = create_advanced_plots(enc, dec)
    fig.savefig(out_path, dpi=300, bbox_inches="tight")
    print(f"[ok] Saved {out_path.name}")


def _save_mega(enc: pd.DataFrame, dec: pd.DataFrame, out_path: Path) -> None:
    if enc.empty or dec.empty:
        print("[warn] CottonCrypto data empty; skipping mega_performance_analysis.png")
        return
    fig, _, _ = create_mega_analysis(enc, dec)
    fig.savefig(out_path, dpi=300, bbox_inches="tight")
    print(f"[ok] Saved {out_path.name}")


def main(argv: Optional[list[str]] = None) -> int:
    """Generate all figures from the given (or default) input files."""
    args = sys.argv[1:] if argv is None else argv
    mylib_path = Path(args[0]).resolve() if len(args) >= 1 else MYLIB_INPUT_DEFAULT
    openssl_path = Path(args[1]).resolve() if len(args) >= 2 else OPENSSL_INPUT_DEFAULT

    if not mylib_path.exists():
        print(f"[error] CottonCrypto input not found: {mylib_path}")
        return 1

    enc, dec = parse_mylib_results(mylib_path)
    if enc.empty or dec.empty:
        print(f"[error] Failed to parse CottonCrypto data from {mylib_path}")
        return 2

    print(f"Loaded CottonCrypto data: enc={len(enc)} rows, dec={len(dec)} rows")
    plot_mylib_four_panels(enc, dec, ROOT / "library_performance.png")
    _save_advanced(enc, dec, ROOT / "advanced_performance_analysis.png")
    _save_mega(enc, dec, ROOT / "mega_performance_analysis.png")

    ossl_df = pd.DataFrame()
    if openssl_path.exists():
        ossl_df = parse_openssl_results(openssl_path)
        print(f"Loaded OpenSSL data: {len(ossl_df)} points")
        plot_openssl_comparison(enc, dec, ossl_df, ROOT / "openssl_comparison.png")
    else:
        print(f"[info] OpenSSL input not found, skipping comparison: {openssl_path}")

    enc_best = enc.loc[enc["Throughput"].idxmax()]
    dec_best = dec.loc[dec["Throughput"].idxmax()]
    print("\nSummary:")
    print(f"  CottonCrypto Encrypt best: {enc_best['Throughput']:.1f} MB/s at {enc_best['Threads']} threads, {enc_best['ChunkMB']}MB chunks")
    print(f"  CottonCrypto Decrypt best: {dec_best['Throughput']:.1f} MB/s at {dec_best['Threads']} threads, {dec_best['ChunkMB']}MB chunks")
    if not ossl_df.empty:
        print(f"  OpenSSL best: {ossl_df['ThroughputMBps'].max():.1f} MB/s at {int(ossl_df.loc[ossl_df['ThroughputMBps'].idxmax(), 'BlockBytes'])} bytes buffer")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
