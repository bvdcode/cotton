import re
import sys
from pathlib import Path
from typing import Tuple, Optional

import matplotlib.pyplot as plt
import numpy as np
import pandas as pd


ROOT = Path(__file__).parent.resolve()
MYLIB_INPUT_DEFAULT = ROOT / "input.txt"
OPENSSL_INPUT_DEFAULT = ROOT / "input-openssl.txt"


def parse_mylib_results(filename: Path) -> Tuple[pd.DataFrame, pd.DataFrame]:
    """Parse my library performance test results from input.txt.

    Expects two sections with headers:
      === ENCRYPTION THREAD/CHUNK SWEEP ===
      === DECRYPTION THREAD/CHUNK SWEEP ===

    And table lines: Threads | ChunkMB | Avg MB/s
    Returns two DataFrames with columns: Threads, ChunkMB, Throughput
    """

    text = filename.read_text(encoding="utf-8", errors="ignore")

    enc_sec = re.search(r"===\s*ENCRYPTION THREAD/CHUNK SWEEP\s*===(.*?)(?===|\Z)", text, re.DOTALL)
    dec_sec = re.search(r"===\s*DECRYPTION THREAD/CHUNK SWEEP\s*===(.*?)(?===|\Z)", text, re.DOTALL)

    def extract(section: Optional[re.Match]) -> pd.DataFrame:
        if not section:
            return pd.DataFrame(columns=["Threads", "ChunkMB", "Throughput"])
        body = section.group(1)
        # pattern matches: threads | chunk | number (may be spaces)
        pat = re.compile(r"(\d+)\s*\|\s*(\d+)\s*\|\s*([\d.]+)")
        rows = []
        for th, ch, thr in pat.findall(body):
            rows.append({
                "Threads": int(th),
                "ChunkMB": int(ch),
                "Throughput": float(thr),
            })
        return pd.DataFrame(rows)

    enc_df = extract(enc_sec)
    dec_df = extract(dec_sec)
    return enc_df, dec_df


def parse_openssl_results(filename: Path) -> pd.DataFrame:
    """Parse OpenSSL 'speed -evp aes-128-gcm' output.

    We parse the summary table:
      The 'numbers' are in 1000s of bytes per second processed.
      header: type  16 bytes 64 bytes ...
      row:    AES-128-GCM  103872.58k ...

    Returns DataFrame with columns: BlockBytes, ThroughputMBps, Label
    ThroughputMBps is decimal MB/s (1 MB = 1,000,000 bytes).
    """

    text = filename.read_text(encoding="utf-8", errors="ignore")
    # Find header line with sizes and the AES-128-GCM row
    header_match = re.search(r"^type\s+((?:\d+\s+bytes\s+)+)\s*$", text, re.MULTILINE)
    row_match = re.search(r"^AES-128-GCM\s+(.+?)\s*$", text, re.MULTILINE)

    if not header_match or not row_match:
        # Fallback: try to gather from 'Doing ... on X size blocks' lines
        # Doing AES-128-GCM ops for 3s on 16 size blocks: 19070356 AES-128-GCM ops in 2.94s
        pat = re.compile(r"on\s+(\d+)\s+size blocks:.*? in\s+([\d.]+)s", re.IGNORECASE)
        sizes = []
        times = []
        for m in pat.finditer(text):
            sizes.append(int(m.group(1)))
            times.append(float(m.group(2)))
        # Also find the count of ops
        pat2 = re.compile(r"on\s+(\d+)\s+size blocks:\s*(\d+)\s+AES-128-GCM ops in\s+([\d.]+)s", re.IGNORECASE)
        data = []
        for m in pat2.finditer(text):
            block = int(m.group(1))
            ops = int(m.group(2))
            secs = float(m.group(3))
            bytes_per_sec = ops * block / secs
            mbps = bytes_per_sec / 1_000_000.0
            data.append({"BlockBytes": block, "ThroughputMBps": mbps, "Label": "OpenSSL AES-128-GCM"})
        return pd.DataFrame(sorted(data, key=lambda r: r["BlockBytes"]))

    # Parse sizes from header
    sizes_str = header_match.group(1)
    size_vals = [int(x) for x in re.findall(r"(\d+)\s+bytes", sizes_str)]
    # Parse k-values from row (thousands of bytes per second). Split by whitespace ending with 'k'
    row_vals = [float(v) for v in re.findall(r"([\d.]+)k", row_match.group(1))]
    # Defensive: align by min length
    n = min(len(size_vals), len(row_vals))
    size_vals = size_vals[:n]
    row_vals = row_vals[:n]

    # Convert k (thousands of bytes/s) to MB/s (decimal)
    mbps = [v / 1000.0 for v in row_vals]
    df = pd.DataFrame({
        "BlockBytes": size_vals,
        "ThroughputMBps": mbps,
        "Label": ["OpenSSL AES-128-GCM"] * n,
    })
    return df.sort_values("BlockBytes").reset_index(drop=True)


def plot_mylib_four_panels(enc: pd.DataFrame, dec: pd.DataFrame, out_path: Path) -> None:
    """Create 4-panel figure for my library: throughput vs chunk size (per threads) and vs threads (per chunk)."""
    if enc.empty or dec.empty:
        print("[warn] MyLib data is empty; skipping library_performance.png")
        return

    plt.rcParams["figure.facecolor"] = "white"
    plt.rcParams["axes.facecolor"] = "white"
    plt.rcParams["axes.grid"] = True
    plt.rcParams["grid.alpha"] = 0.3

    fig, axes = plt.subplots(2, 2, figsize=(16, 12))
    (ax1, ax2), (ax3, ax4) = axes
    fig.suptitle("MyLib Performance: Encryption/Decryption Throughput", fontsize=16, fontweight="bold", y=0.98)

    thread_colors = ["#1f77b4", "#ff7f0e", "#2ca02c", "#d62728", "#9467bd", "#8c564b"]
    chunk_colors = ["#e41a1c", "#377eb8", "#4daf4a", "#984ea3", "#ff7f00", "#a65628", "#f781bf"]

    unique_threads = sorted(enc["Threads"].unique())
    unique_chunks = sorted(enc["ChunkMB"].unique())

    # 1) Encrypt: throughput vs chunk size per thread
    for i, t in enumerate(unique_threads):
        d = enc[enc["Threads"] == t].sort_values("ChunkMB")
        ax1.plot(d["ChunkMB"], d["Throughput"], marker="o", label=f"{t} threads",
                 linewidth=2.0, markersize=7, color=thread_colors[i % len(thread_colors)])
    ax1.set_title("Encryption: Throughput vs Chunk Size", fontsize=14, fontweight="bold")
    ax1.set_xlabel("Chunk Size (MB)")
    ax1.set_ylabel("Throughput (MB/s)")
    ax1.set_xticks(unique_chunks)
    ax1.legend(frameon=True, fancybox=True)

    # 2) Decrypt: throughput vs chunk size per thread
    for i, t in enumerate(unique_threads):
        d = dec[dec["Threads"] == t].sort_values("ChunkMB")
        ax2.plot(d["ChunkMB"], d["Throughput"], marker="s", label=f"{t} threads",
                 linewidth=2.0, markersize=7, color=thread_colors[i % len(thread_colors)])
    ax2.set_title("Decryption: Throughput vs Chunk Size", fontsize=14, fontweight="bold")
    ax2.set_xlabel("Chunk Size (MB)")
    ax2.set_ylabel("Throughput (MB/s)")
    ax2.set_xticks(unique_chunks)
    ax2.legend(frameon=True, fancybox=True)

    # 3) Encrypt: throughput vs threads per chunk
    for i, ch in enumerate(unique_chunks):
        d = enc[enc["ChunkMB"] == ch].sort_values("Threads")
        ax3.plot(d["Threads"], d["Throughput"], marker="o", label=f"{int(ch)}MB",
                 linewidth=2.0, markersize=7, color=chunk_colors[i % len(chunk_colors)])
    ax3.set_title("Encryption: Throughput vs Threads", fontsize=14, fontweight="bold")
    ax3.set_xlabel("Number of Threads")
    ax3.set_ylabel("Throughput (MB/s)")
    ax3.set_xticks(unique_threads)
    ax3.legend(title="Chunk Size", frameon=True, fancybox=True)

    # 4) Decrypt: throughput vs threads per chunk
    for i, ch in enumerate(unique_chunks):
        d = dec[dec["ChunkMB"] == ch].sort_values("Threads")
        ax4.plot(d["Threads"], d["Throughput"], marker="s", label=f"{int(ch)}MB",
                 linewidth=2.0, markersize=7, color=chunk_colors[i % len(chunk_colors)])
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


def plot_openssl_comparison(enc: pd.DataFrame, dec: pd.DataFrame, ossl: pd.DataFrame, out_path: Path) -> None:
    """Create a simple comparison: MyLib (best-per-chunk) vs OpenSSL across buffer sizes.

    - X-axis: buffer/chunk size in bytes, log scale
    - Lines: OpenSSL AES-128-GCM, MyLib Encrypt best-per-chunk, MyLib Decrypt best-per-chunk
    """
    if ossl is None or ossl.empty:
        print("[warn] OpenSSL data missing; skipping openssl_comparison.png")
        return

    # MyLib best per chunk (across threads)
    enc_best_per_chunk = enc.groupby("ChunkMB")["Throughput"].max().reset_index()
    dec_best_per_chunk = dec.groupby("ChunkMB")["Throughput"].max().reset_index()

    # Convert ChunkMB (decimal) to bytes for x-axis
    enc_best_per_chunk["BlockBytes"] = (enc_best_per_chunk["ChunkMB"] * 1_000_000).astype(int)
    dec_best_per_chunk["BlockBytes"] = (dec_best_per_chunk["ChunkMB"] * 1_000_000).astype(int)

    fig, ax = plt.subplots(figsize=(10, 6))
    fig.suptitle("MyLib vs OpenSSL: Throughput vs Buffer/Chunk Size", fontsize=16, fontweight="bold", y=0.96)

    # OpenSSL line
    ax.plot(ossl["BlockBytes"], ossl["ThroughputMBps"], marker="o", linewidth=2.5, markersize=7,
            label="OpenSSL AES-128-GCM")

    # MyLib lines (best per chunk)
    ax.plot(enc_best_per_chunk["BlockBytes"], enc_best_per_chunk["Throughput"], marker="s", linewidth=2.0,
            markersize=7, label="MyLib Encrypt (best per chunk)")
    ax.plot(dec_best_per_chunk["BlockBytes"], dec_best_per_chunk["Throughput"], marker="^", linewidth=2.0,
            markersize=7, label="MyLib Decrypt (best per chunk)")

    ax.set_xscale("log")
    ax.set_xlabel("Buffer / Chunk Size (bytes) [log scale]")
    ax.set_ylabel("Throughput (MB/s)")
    ax.grid(True, which="both", axis="both", alpha=0.3)
    ax.legend()

    # Lightweight note to clarify semantics difference
    note = (
        "Note: OpenSSL varies small buffer sizes; MyLib varies file chunk sizes."
        " Scales differ; this is an approximate visual comparison."
    )
    ax.text(0.01, -0.18, note, transform=ax.transAxes, fontsize=9, va="top", ha="left", wrap=True)

    fig.tight_layout()
    fig.savefig(out_path, dpi=300, bbox_inches="tight")
    print(f"[ok] Saved {out_path.name}")


def main(argv: Optional[list[str]] = None) -> int:
    # Allow optional custom paths: charts.py [mylib_input] [openssl_input]
    args = sys.argv[1:] if argv is None else argv
    mylib_path = Path(args[0]).resolve() if len(args) >= 1 else MYLIB_INPUT_DEFAULT
    openssl_path = Path(args[1]).resolve() if len(args) >= 2 else OPENSSL_INPUT_DEFAULT

    if not mylib_path.exists():
        print(f"[error] MyLib input not found: {mylib_path}")
        return 1

    enc, dec = parse_mylib_results(mylib_path)
    if enc.empty or dec.empty:
        print(f"[error] Failed to parse MyLib data from {mylib_path}")
        return 2

    print(f"Loaded MyLib data: enc={len(enc)} rows, dec={len(dec)} rows")
    plot_mylib_four_panels(enc, dec, ROOT / "library_performance.png")

    # OpenSSL part is optional
    ossl_df = pd.DataFrame()
    if openssl_path.exists():
        ossl_df = parse_openssl_results(openssl_path)
        print(f"Loaded OpenSSL data: {len(ossl_df)} points")
        plot_openssl_comparison(enc, dec, ossl_df, ROOT / "openssl_comparison.png")
    else:
        print(f"[info] OpenSSL input not found, skipping comparison: {openssl_path}")

    # Print a tiny summary to console
    enc_best = enc.loc[enc["Throughput"].idxmax()]
    dec_best = dec.loc[dec["Throughput"].idxmax()]
    print("\nSummary:")
    print(f"  MyLib Encrypt best: {enc_best['Throughput']:.1f} MB/s at {enc_best['Threads']} threads, {enc_best['ChunkMB']}MB chunks")
    print(f"  MyLib Decrypt best: {dec_best['Throughput']:.1f} MB/s at {dec_best['Threads']} threads, {dec_best['ChunkMB']}MB chunks")
    if not ossl_df.empty:
        print(f"  OpenSSL best: {ossl_df['ThroughputMBps'].max():.1f} MB/s at {int(ossl_df.loc[ossl_df['ThroughputMBps'].idxmax(), 'BlockBytes'])} bytes buffer")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
