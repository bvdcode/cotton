"""Shared parsing, styling and plotting helpers for the benchmark chart scripts."""

import re
from pathlib import Path
from typing import Optional, Tuple

import matplotlib.gridspec as gridspec
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

try:
    import seaborn as sns  # optional, best effort
except Exception:  # pragma: no cover
    sns = None


ROOT = Path(__file__).parent.resolve()
MYLIB_INPUT_DEFAULT = ROOT / "input.txt"
OPENSSL_INPUT_DEFAULT = ROOT / "input-openssl.txt"

THREAD_HEX_COLORS = ["#1f77b4", "#ff7f0e", "#2ca02c", "#d62728", "#9467bd", "#8c564b"]
CHUNK_HEX_COLORS = ["#e41a1c", "#377eb8", "#4daf4a", "#984ea3", "#ff7f00", "#a65628", "#f781bf"]


def parse_mylib_results(filename: Path) -> Tuple[pd.DataFrame, pd.DataFrame]:
    """Parse CottonCrypto sweep results from input.txt.

    Expects two sections with headers:
      === ENCRYPTION THREAD/CHUNK SWEEP ===
      === DECRYPTION THREAD/CHUNK SWEEP ===

    And table lines: Threads | ChunkMB | Avg MB/s
    Returns two DataFrames with columns: Threads, ChunkMB (float), Throughput.
    """
    text = Path(filename).read_text(encoding="utf-8", errors="ignore")

    enc_sec = re.search(r"===\s*ENCRYPTION THREAD/CHUNK SWEEP\s*===(.*?)(?===|\Z)", text, re.DOTALL)
    dec_sec = re.search(r"===\s*DECRYPTION THREAD/CHUNK SWEEP\s*===(.*?)(?===|\Z)", text, re.DOTALL)

    def extract(section: Optional[re.Match]) -> pd.DataFrame:
        if not section:
            return pd.DataFrame(columns=["Threads", "ChunkMB", "Throughput"])
        body = section.group(1)
        # threads | chunk (can be decimal) | throughput (decimal)
        pat = re.compile(r"(\d+)\s*\|\s*([\d.]+)\s*\|\s*([\d.]+)")
        rows = []
        for th, ch, thr in pat.findall(body):
            rows.append({"Threads": int(th), "ChunkMB": float(ch), "Throughput": float(thr)})
        return pd.DataFrame(rows)

    return extract(enc_sec), extract(dec_sec)


def parse_test_results(filename: str | Path) -> Tuple[pd.DataFrame, pd.DataFrame]:
    """Parse encryption/decryption sweep results from a file.

    Thin alias kept for the standalone scripts; delegates to parse_mylib_results
    so all scripts share a single, decimal-aware parser implementation.
    """
    return parse_mylib_results(Path(filename))


def parse_openssl_results(filename: Path) -> pd.DataFrame:
    """Parse OpenSSL 'speed -evp aes-128-gcm' output.

    Returns DataFrame with columns: BlockBytes, ThroughputMBps, Label.
    ThroughputMBps is decimal MB/s (1 MB = 1,000,000 bytes).
    """
    text = Path(filename).read_text(encoding="utf-8", errors="ignore")
    header_match = re.search(r"^type\s+((?:\d+\s+bytes\s+)+)\s*$", text, re.MULTILINE)
    row_match = re.search(r"^AES-128-GCM\s+(.+?)\s*$", text, re.MULTILINE)

    if not header_match or not row_match:
        # Fallback: gather from 'Doing ... on X size blocks' lines.
        pat2 = re.compile(
            r"on\s+(\d+)\s+size blocks:\s*(\d+)\s+AES-128-GCM ops in\s+([\d.]+)s",
            re.IGNORECASE,
        )
        data = []
        for m in pat2.finditer(text):
            block = int(m.group(1))
            ops = int(m.group(2))
            secs = float(m.group(3))
            mbps = ops * block / secs / 1_000_000.0
            data.append({"BlockBytes": block, "ThroughputMBps": mbps, "Label": "OpenSSL AES-128-GCM"})
        return pd.DataFrame(sorted(data, key=lambda r: r["BlockBytes"]))

    sizes_str = header_match.group(1)
    size_vals = [int(x) for x in re.findall(r"(\d+)\s+bytes", sizes_str)]
    row_vals = [float(v) for v in re.findall(r"([\d.]+)k", row_match.group(1))]
    n = min(len(size_vals), len(row_vals))
    size_vals, row_vals = size_vals[:n], row_vals[:n]
    mbps = [v / 1000.0 for v in row_vals]
    df = pd.DataFrame(
        {"BlockBytes": size_vals, "ThroughputMBps": mbps, "Label": ["OpenSSL AES-128-GCM"] * n}
    )
    return df.sort_values("BlockBytes").reset_index(drop=True)


# --- Simple 4-panel figure (performance_charts.png) ---------------------------


def create_simple_plots(encrypt_data: pd.DataFrame, decrypt_data: pd.DataFrame):
    """Build the polished 4-panel throughput figure and return the Figure."""
    plt.rcParams["figure.facecolor"] = "white"
    plt.rcParams["axes.facecolor"] = "white"
    plt.rcParams["axes.grid"] = True
    plt.rcParams["grid.alpha"] = 0.3

    fig, ((ax1, ax2), (ax3, ax4)) = plt.subplots(2, 2, figsize=(16, 12))
    fig.suptitle(
        "Performance Analysis: Encryption/Decryption Throughput",
        fontsize=16, fontweight="bold", y=0.98,
    )

    chunk_colors = ["#e41a1c", "#377eb8", "#4daf4a", "#984ea3", "#ff7f00", "#ffff33", "#a65628"]

    unique_threads = sorted(encrypt_data["Threads"].unique())
    chunk_ticks = sorted(encrypt_data["ChunkMB"].unique())
    unique_chunks = chunk_ticks

    for i, threads in enumerate(unique_threads):
        d = encrypt_data[encrypt_data["Threads"] == threads].sort_values("ChunkMB")
        ax1.plot(d["ChunkMB"], d["Throughput"], marker="o", label=f"{threads} threads",
                 linewidth=2.5, markersize=8, color=THREAD_HEX_COLORS[i % len(THREAD_HEX_COLORS)])
    ax1.set_xlabel("Chunk Size (MB)", fontsize=12, fontweight="bold")
    ax1.set_ylabel("Throughput (MB/s)", fontsize=12, fontweight="bold")
    ax1.set_title("Encryption: Throughput vs Chunk Size", fontsize=14, fontweight="bold")
    ax1.legend(frameon=True, fancybox=True, shadow=True)
    ax1.grid(True, alpha=0.3, linestyle="-", linewidth=0.5)
    ax1.set_xticks(chunk_ticks)
    ax1.set_xticklabels([f"{int(x)}" for x in chunk_ticks])

    for i, threads in enumerate(unique_threads):
        d = decrypt_data[decrypt_data["Threads"] == threads].sort_values("ChunkMB")
        ax2.plot(d["ChunkMB"], d["Throughput"], marker="s", label=f"{threads} threads",
                 linewidth=2.5, markersize=8, color=THREAD_HEX_COLORS[i % len(THREAD_HEX_COLORS)])
    ax2.set_xlabel("Chunk Size (MB)", fontsize=12, fontweight="bold")
    ax2.set_ylabel("Throughput (MB/s)", fontsize=12, fontweight="bold")
    ax2.set_title("Decryption: Throughput vs Chunk Size", fontsize=14, fontweight="bold")
    ax2.legend(frameon=True, fancybox=True, shadow=True)
    ax2.grid(True, alpha=0.3, linestyle="-", linewidth=0.5)
    ax2.set_xticks(chunk_ticks)
    ax2.set_xticklabels([f"{int(x)}" for x in chunk_ticks])

    for i, chunk_size in enumerate(unique_chunks):
        d = encrypt_data[encrypt_data["ChunkMB"] == chunk_size].sort_values("Threads")
        ax3.plot(d["Threads"], d["Throughput"], marker="o", label=f"{int(chunk_size)}MB",
                 linewidth=2.5, markersize=8, color=chunk_colors[i % len(chunk_colors)])
    ax3.set_xlabel("Number of Threads", fontsize=12, fontweight="bold")
    ax3.set_ylabel("Throughput (MB/s)", fontsize=12, fontweight="bold")
    ax3.set_title("Encryption: Throughput vs Threads", fontsize=14, fontweight="bold")
    ax3.legend(frameon=True, fancybox=True, shadow=True, title="Chunk Size")
    ax3.grid(True, alpha=0.3, linestyle="-", linewidth=0.5)
    ax3.set_xticks(unique_threads)
    ax3.set_xticklabels([str(int(x)) for x in unique_threads])

    for i, chunk_size in enumerate(unique_chunks):
        d = decrypt_data[decrypt_data["ChunkMB"] == chunk_size].sort_values("Threads")
        ax4.plot(d["Threads"], d["Throughput"], marker="s", label=f"{int(chunk_size)}MB",
                 linewidth=2.5, markersize=8, color=chunk_colors[i % len(chunk_colors)])
    ax4.set_xlabel("Number of Threads", fontsize=12, fontweight="bold")
    ax4.set_ylabel("Throughput (MB/s)", fontsize=12, fontweight="bold")
    ax4.set_title("Decryption: Throughput vs Threads", fontsize=14, fontweight="bold")
    ax4.legend(frameon=True, fancybox=True, shadow=True, title="Chunk Size")
    ax4.grid(True, alpha=0.3, linestyle="-", linewidth=0.5)
    ax4.set_xticks(unique_threads)
    ax4.set_xticklabels([str(int(x)) for x in unique_threads])

    for ax in (ax1, ax2, ax3, ax4):
        ax.spines["top"].set_visible(False)
        ax.spines["right"].set_visible(False)
        ax.spines["left"].set_linewidth(0.5)
        ax.spines["bottom"].set_linewidth(0.5)

    plt.tight_layout()
    return fig


# --- Advanced 6-panel figure (advanced_performance_analysis.png) --------------


def create_advanced_plots(encrypt_data: pd.DataFrame, decrypt_data: pd.DataFrame):
    """Build the 6-panel advanced figure; return (fig, encrypt_optimal, decrypt_optimal)."""
    plt.style.use("seaborn-v0_8")

    fig = plt.figure(figsize=(20, 16))
    gs = fig.add_gridspec(3, 2, hspace=0.3, wspace=0.25)
    ax1, ax2 = fig.add_subplot(gs[0, 0]), fig.add_subplot(gs[0, 1])
    ax3, ax4 = fig.add_subplot(gs[1, 0]), fig.add_subplot(gs[1, 1])
    ax5, ax6 = fig.add_subplot(gs[2, 0]), fig.add_subplot(gs[2, 1])

    fig.suptitle("Complete Performance Analysis: Encryption/Decryption Throughput",
                 fontsize=18, fontweight="bold")

    unique_threads = sorted(encrypt_data["Threads"].unique())
    unique_chunks = sorted(encrypt_data["ChunkMB"].unique())
    colors = plt.cm.Set1(np.linspace(0, 1, len(unique_threads)))
    chunk_colors = plt.cm.tab10(np.linspace(0, 1, len(unique_chunks)))
    chunk_ticks = unique_chunks

    for i, threads in enumerate(unique_threads):
        d = encrypt_data[encrypt_data["Threads"] == threads].sort_values("ChunkMB")
        ax1.plot(d["ChunkMB"], d["Throughput"], marker="o", label=f"{threads} threads",
                 linewidth=2.5, markersize=8, color=colors[i])
    ax1.set_xlabel("Chunk Size (MB)", fontsize=12, fontweight="bold")
    ax1.set_ylabel("Throughput (MB/s)", fontsize=12, fontweight="bold")
    ax1.set_title("Encryption: Throughput vs Chunk Size", fontsize=14, fontweight="bold")
    ax1.legend(bbox_to_anchor=(1.05, 1), loc="upper left")
    ax1.grid(True, alpha=0.3)
    ax1.set_xticks(chunk_ticks)
    ax1.set_xticklabels([str(int(x)) for x in chunk_ticks])

    for i, threads in enumerate(unique_threads):
        d = decrypt_data[decrypt_data["Threads"] == threads].sort_values("ChunkMB")
        ax2.plot(d["ChunkMB"], d["Throughput"], marker="s", label=f"{threads} threads",
                 linewidth=2.5, markersize=8, color=colors[i])
    ax2.set_xlabel("Chunk Size (MB)", fontsize=12, fontweight="bold")
    ax2.set_ylabel("Throughput (MB/s)", fontsize=12, fontweight="bold")
    ax2.set_title("Decryption: Throughput vs Chunk Size", fontsize=14, fontweight="bold")
    ax2.legend(bbox_to_anchor=(1.05, 1), loc="upper left")
    ax2.grid(True, alpha=0.3)
    ax2.set_xticks(chunk_ticks)
    ax2.set_xticklabels([str(int(x)) for x in chunk_ticks])

    for i, chunk_size in enumerate(unique_chunks):
        d = encrypt_data[encrypt_data["ChunkMB"] == chunk_size].sort_values("Threads")
        ax3.plot(d["Threads"], d["Throughput"], marker="o", label=f"{chunk_size}MB",
                 linewidth=2.5, markersize=8, color=chunk_colors[i])
    ax3.set_xlabel("Number of Threads", fontsize=12, fontweight="bold")
    ax3.set_ylabel("Throughput (MB/s)", fontsize=12, fontweight="bold")
    ax3.set_title("Encryption: Throughput vs Threads", fontsize=14, fontweight="bold")
    ax3.legend(bbox_to_anchor=(1.05, 1), loc="upper left")
    ax3.grid(True, alpha=0.3)
    ax3.set_xticks(unique_threads)
    ax3.set_xticklabels([str(int(x)) for x in unique_threads])

    for i, chunk_size in enumerate(unique_chunks):
        d = decrypt_data[decrypt_data["ChunkMB"] == chunk_size].sort_values("Threads")
        ax4.plot(d["Threads"], d["Throughput"], marker="s", label=f"{chunk_size}MB",
                 linewidth=2.5, markersize=8, color=chunk_colors[i])
    ax4.set_xlabel("Number of Threads", fontsize=12, fontweight="bold")
    ax4.set_ylabel("Throughput (MB/s)", fontsize=12, fontweight="bold")
    ax4.set_title("Decryption: Throughput vs Threads", fontsize=14, fontweight="bold")
    ax4.legend(bbox_to_anchor=(1.05, 1), loc="upper left")
    ax4.grid(True, alpha=0.3)
    ax4.set_xticks(unique_threads)
    ax4.set_xticklabels([str(int(x)) for x in unique_threads])

    # Max per thread count (bar)
    threads_comparison = []
    for threads in unique_threads:
        enc_max = encrypt_data[encrypt_data["Threads"] == threads]["Throughput"].max()
        dec_max = decrypt_data[decrypt_data["Threads"] == threads]["Throughput"].max()
        threads_comparison.append([threads, enc_max, dec_max])
    threads_df = pd.DataFrame(threads_comparison, columns=["Threads", "Encrypt_Max", "Decrypt_Max"])
    x = np.arange(len(threads_df))
    width = 0.35
    ax5.bar(x - width / 2, threads_df["Encrypt_Max"], width, label="Encryption", alpha=0.8, color="skyblue")
    ax5.bar(x + width / 2, threads_df["Decrypt_Max"], width, label="Decryption", alpha=0.8, color="lightcoral")
    ax5.set_xlabel("Number of Threads", fontsize=12, fontweight="bold")
    ax5.set_ylabel("Max Throughput (MB/s)", fontsize=12, fontweight="bold")
    ax5.set_title("Maximum Throughput Comparison by Thread Count", fontsize=14, fontweight="bold")
    ax5.set_xticks(x)
    ax5.set_xticklabels(threads_df["Threads"])
    ax5.legend()
    ax5.grid(True, alpha=0.3)
    for i, (enc, dec) in enumerate(zip(threads_df["Encrypt_Max"], threads_df["Decrypt_Max"])):
        ax5.text(i - width / 2, enc + 50, f"{enc:.0f}", ha="center", va="bottom", fontsize=10)
        ax5.text(i + width / 2, dec + 50, f"{dec:.0f}", ha="center", va="bottom", fontsize=10)

    # Scaling efficiency at mid chunk
    baseline_enc = encrypt_data[encrypt_data["Threads"] == 1].groupby("ChunkMB")["Throughput"].mean()
    baseline_dec = decrypt_data[decrypt_data["Threads"] == 1].groupby("ChunkMB")["Throughput"].mean()
    mid_chunk = unique_chunks[len(unique_chunks) // 2]
    enc_scaling, dec_scaling = [], []
    for threads in unique_threads:
        enc_th = encrypt_data[(encrypt_data["Threads"] == threads) & (encrypt_data["ChunkMB"] == mid_chunk)]["Throughput"].mean()
        dec_th = decrypt_data[(decrypt_data["Threads"] == threads) & (decrypt_data["ChunkMB"] == mid_chunk)]["Throughput"].mean()
        enc_scaling.append(enc_th / baseline_enc[mid_chunk])
        dec_scaling.append(dec_th / baseline_dec[mid_chunk])
    ax6.plot(unique_threads, enc_scaling, marker="o", linewidth=3, markersize=10, label="Encryption Scaling", color="blue")
    ax6.plot(unique_threads, dec_scaling, marker="s", linewidth=3, markersize=10, label="Decryption Scaling", color="red")
    ax6.plot(unique_threads, unique_threads, "--", alpha=0.7, color="gray", label="Ideal Linear Scaling")
    ax6.set_xlabel("Number of Threads", fontsize=12, fontweight="bold")
    ax6.set_ylabel("Speedup Factor", fontsize=12, fontweight="bold")
    ax6.set_title(f"Scaling Efficiency (Chunk Size: {mid_chunk}MB)", fontsize=14, fontweight="bold")
    ax6.legend()
    ax6.grid(True, alpha=0.3)
    ax6.set_xticks(unique_threads)
    ax6.set_xticklabels([str(int(x)) for x in unique_threads])

    plt.tight_layout()
    encrypt_optimal = encrypt_data.loc[encrypt_data["Throughput"].idxmax()]
    decrypt_optimal = decrypt_data.loc[decrypt_data["Throughput"].idxmax()]
    return fig, encrypt_optimal, decrypt_optimal


# --- Mega 12-panel figure (mega_performance_analysis.png) ---------------------


def create_mega_analysis(encrypt_data: pd.DataFrame, decrypt_data: pd.DataFrame):
    """Build the 12-panel mega figure; return (fig, encrypt_best, decrypt_best)."""
    plt.style.use("default")
    if sns:
        sns.set_palette("husl")

    fig = plt.figure(figsize=(24, 18))
    gs = gridspec.GridSpec(4, 3, hspace=0.3, wspace=0.25, left=0.05, right=0.95, top=0.95, bottom=0.05)
    fig.suptitle("🚀 MEGA Performance Analysis: Complete Encryption/Decryption Study",
                 fontsize=20, fontweight="bold", y=0.97)

    ax1, ax2, ax3 = fig.add_subplot(gs[0, 0]), fig.add_subplot(gs[0, 1]), fig.add_subplot(gs[0, 2])
    ax4, ax5, ax6 = fig.add_subplot(gs[1, 0]), fig.add_subplot(gs[1, 1]), fig.add_subplot(gs[1, 2])
    ax7, ax8, ax9 = fig.add_subplot(gs[2, 0]), fig.add_subplot(gs[2, 1]), fig.add_subplot(gs[2, 2])
    ax10, ax11 = fig.add_subplot(gs[3, 0]), fig.add_subplot(gs[3, 1])

    unique_threads = sorted(encrypt_data["Threads"].unique())
    unique_chunks = sorted(encrypt_data["ChunkMB"].unique())

    colors = plt.cm.Set1(np.linspace(0, 1, len(unique_threads)))
    chunk_colors = plt.cm.tab10(np.linspace(0, 1, len(unique_chunks)))

    # 1-4: compact line plots
    for i, threads in enumerate(unique_threads):
        d = encrypt_data[encrypt_data["Threads"] == threads].sort_values("ChunkMB")
        ax1.plot(d["ChunkMB"], d["Throughput"], marker="o", label=f"{threads}T", linewidth=1.5, markersize=4, color=colors[i])
    ax1.set_title("Encrypt: Throughput vs Chunks", fontsize=12, fontweight="bold")
    ax1.set_xlabel("Chunk Size (MB)")
    ax1.set_ylabel("MB/s")
    ax1.legend(ncol=2, fontsize=8)
    ax1.grid(True, alpha=0.3)

    for i, threads in enumerate(unique_threads):
        d = decrypt_data[decrypt_data["Threads"] == threads].sort_values("ChunkMB")
        ax2.plot(d["ChunkMB"], d["Throughput"], marker="s", label=f"{threads}T", linewidth=1.5, markersize=4, color=colors[i])
    ax2.set_title("Decrypt: Throughput vs Chunks", fontsize=12, fontweight="bold")
    ax2.set_xlabel("Chunk Size (MB)")
    ax2.set_ylabel("MB/s")
    ax2.legend(ncol=2, fontsize=8)
    ax2.grid(True, alpha=0.3)

    for i, chunk_size in enumerate(unique_chunks):
        d = encrypt_data[encrypt_data["ChunkMB"] == chunk_size].sort_values("Threads")
        ax3.plot(d["Threads"], d["Throughput"], marker="o", label=f"{int(chunk_size)}MB", linewidth=1.5, markersize=4, color=chunk_colors[i])
    ax3.set_title("Encrypt: Throughput vs Threads", fontsize=12, fontweight="bold")
    ax3.set_xlabel("Threads")
    ax3.set_ylabel("MB/s")
    ax3.legend(ncol=2, fontsize=8)
    ax3.grid(True, alpha=0.3)

    for i, chunk_size in enumerate(unique_chunks):
        d = decrypt_data[decrypt_data["ChunkMB"] == chunk_size].sort_values("Threads")
        ax4.plot(d["Threads"], d["Throughput"], marker="s", label=f"{int(chunk_size)}MB", linewidth=1.5, markersize=4, color=chunk_colors[i])
    ax4.set_title("Decrypt: Throughput vs Threads", fontsize=12, fontweight="bold")
    ax4.set_xlabel("Threads")
    ax4.set_ylabel("MB/s")
    ax4.legend(ncol=2, fontsize=8)
    ax4.grid(True, alpha=0.3)

    # 5: Heat map
    encrypt_pivot = encrypt_data.pivot(index="Threads", columns="ChunkMB", values="Throughput")
    decrypt_pivot = decrypt_data.pivot(index="Threads", columns="ChunkMB", values="Throughput")
    combined_data = (encrypt_pivot + decrypt_pivot) / 2
    ax5.imshow(combined_data.values, cmap="viridis", aspect="auto")
    ax5.set_title("🔥 Performance Heat Map\n(Average Encrypt+Decrypt)", fontsize=12, fontweight="bold")
    ax5.set_xlabel("Chunk Size (MB)")
    ax5.set_ylabel("Threads")
    ax5.set_xticks(range(len(unique_chunks)))
    ax5.set_xticklabels([str(int(x)) for x in unique_chunks])
    ax5.set_yticks(range(len(unique_threads)))
    ax5.set_yticklabels([str(int(x)) for x in unique_threads])
    for i in range(len(unique_threads)):
        for j in range(len(unique_chunks)):
            ax5.text(j, i, f"{combined_data.iloc[i, j]:.0f}", ha="center", va="center", color="white", fontsize=8, fontweight="bold")

    # 6: Average performance by chunk size (bar)
    encrypt_by_chunk = encrypt_data.groupby("ChunkMB")["Throughput"].agg(["mean", "std", "max", "min"])
    decrypt_by_chunk = decrypt_data.groupby("ChunkMB")["Throughput"].agg(["mean", "std", "max", "min"])
    x = np.arange(len(unique_chunks))
    width = 0.35
    bars1 = ax6.bar(x - width / 2, encrypt_by_chunk["mean"], width, yerr=encrypt_by_chunk["std"], label="Encrypt", alpha=0.8, capsize=5, color="skyblue")
    bars2 = ax6.bar(x + width / 2, decrypt_by_chunk["mean"], width, yerr=decrypt_by_chunk["std"], label="Decrypt", alpha=0.8, capsize=5, color="lightcoral")
    ax6.set_title("📊 Average Performance by Chunk Size", fontsize=12, fontweight="bold")
    ax6.set_xlabel("Chunk Size (MB)")
    ax6.set_ylabel("Average Throughput (MB/s)")
    ax6.set_xticks(x)
    ax6.set_xticklabels([str(int(x_)) for x_ in unique_chunks])
    ax6.legend()
    ax6.grid(True, alpha=0.3)
    for i, (bar1, bar2) in enumerate(zip(bars1, bars2)):
        ax6.text(bar1.get_x() + bar1.get_width() / 2, bar1.get_height() + 100,
                 f'{encrypt_by_chunk.iloc[i]["mean"]:.0f}', ha="center", va="bottom", fontsize=8, rotation=45)
        ax6.text(bar2.get_x() + bar2.get_width() / 2, bar2.get_height() + 100,
                 f'{decrypt_by_chunk.iloc[i]["mean"]:.0f}', ha="center", va="bottom", fontsize=8, rotation=45)

    # 7: Violin distribution
    parts = ax7.violinplot([encrypt_data["Throughput"], decrypt_data["Throughput"]], positions=[1, 2], showmeans=True, showextrema=True)
    for pc, color in zip(parts["bodies"], ["skyblue", "lightcoral"]):
        pc.set_facecolor(color)
        pc.set_alpha(0.7)
    ax7.set_title("🎻 Performance Distribution", fontsize=12, fontweight="bold")
    ax7.set_ylabel("Throughput (MB/s)")
    ax7.set_xticks([1, 2])
    ax7.set_xticklabels(["Encrypt", "Decrypt"])
    ax7.grid(True, alpha=0.3)

    # 8: Scaling efficiency
    baseline_threads = 1
    scaling_data = []
    for chunk_size in unique_chunks:
        encrypt_baseline = encrypt_data[(encrypt_data["Threads"] == baseline_threads) & (encrypt_data["ChunkMB"] == chunk_size)]["Throughput"].iloc[0]
        decrypt_baseline = decrypt_data[(decrypt_data["Threads"] == baseline_threads) & (decrypt_data["ChunkMB"] == chunk_size)]["Throughput"].iloc[0]
        for threads in unique_threads:
            encrypt_current = encrypt_data[(encrypt_data["Threads"] == threads) & (encrypt_data["ChunkMB"] == chunk_size)]["Throughput"].iloc[0]
            decrypt_current = decrypt_data[(decrypt_data["Threads"] == threads) & (decrypt_data["ChunkMB"] == chunk_size)]["Throughput"].iloc[0]
            scaling_data.append({
                "Threads": threads,
                "ChunkMB": chunk_size,
                "Encrypt_Efficiency": (encrypt_current / encrypt_baseline) / threads * 100,
                "Decrypt_Efficiency": (decrypt_current / decrypt_baseline) / threads * 100,
            })
    scaling_df = pd.DataFrame(scaling_data)
    mid_chunk = unique_chunks[len(unique_chunks) // 2]
    mid_data = scaling_df[scaling_df["ChunkMB"] == mid_chunk]
    ax8.plot(mid_data["Threads"], mid_data["Encrypt_Efficiency"], marker="o", linewidth=3, markersize=8, label="Encrypt Efficiency", color="blue")
    ax8.plot(mid_data["Threads"], mid_data["Decrypt_Efficiency"], marker="s", linewidth=3, markersize=8, label="Decrypt Efficiency", color="red")
    ax8.axhline(y=100, color="gray", linestyle="--", alpha=0.7, label="Perfect Efficiency")
    ax8.set_title(f"⚡ Scaling Efficiency ({mid_chunk}MB chunks)", fontsize=12, fontweight="bold")
    ax8.set_xlabel("Number of Threads")
    ax8.set_ylabel("Efficiency (%)")
    ax8.legend()
    ax8.grid(True, alpha=0.3)

    # 9: Speed ratios scatter
    ratio_data = []
    for _, encrypt_row in encrypt_data.iterrows():
        decrypt_row = decrypt_data[(decrypt_data["Threads"] == encrypt_row["Threads"]) & (decrypt_data["ChunkMB"] == encrypt_row["ChunkMB"])]
        if not decrypt_row.empty:
            ratio = decrypt_row["Throughput"].iloc[0] / encrypt_row["Throughput"]
            ratio_data.append({"Threads": encrypt_row["Threads"], "ChunkMB": encrypt_row["ChunkMB"], "Decrypt_Encrypt_Ratio": ratio})
    ratio_df = pd.DataFrame(ratio_data)
    scatter = ax9.scatter(ratio_df["Threads"], ratio_df["ChunkMB"], c=ratio_df["Decrypt_Encrypt_Ratio"],
                          s=ratio_df["Decrypt_Encrypt_Ratio"] * 30, cmap="RdYlGn", alpha=0.7, edgecolors="black")
    ax9.set_title("🚀 Decrypt/Encrypt Speed Ratios", fontsize=12, fontweight="bold")
    ax9.set_xlabel("Threads")
    ax9.set_ylabel("Chunk Size (MB)")
    cbar = plt.colorbar(scatter, ax=ax9, shrink=0.8)
    cbar.set_label("Decrypt/Encrypt Ratio", rotation=270, labelpad=15)

    # 10: Performance zones
    encrypt_max = encrypt_data["Throughput"].max()
    decrypt_max = decrypt_data["Throughput"].max()
    high_perf_encrypt = encrypt_data[encrypt_data["Throughput"] > encrypt_max * 0.9]
    medium_perf_encrypt = encrypt_data[(encrypt_data["Throughput"] > encrypt_max * 0.7) & (encrypt_data["Throughput"] <= encrypt_max * 0.9)]
    high_perf_decrypt = decrypt_data[decrypt_data["Throughput"] > decrypt_max * 0.9]
    medium_perf_decrypt = decrypt_data[(decrypt_data["Throughput"] > decrypt_max * 0.7) & (decrypt_data["Throughput"] <= decrypt_max * 0.9)]
    ax10.scatter(high_perf_encrypt["Threads"], high_perf_encrypt["ChunkMB"], c="green", s=100, alpha=0.7, label="High Encrypt", marker="o")
    ax10.scatter(medium_perf_encrypt["Threads"], medium_perf_encrypt["ChunkMB"], c="orange", s=80, alpha=0.7, label="Medium Encrypt", marker="o")
    ax10.scatter(high_perf_decrypt["Threads"], high_perf_decrypt["ChunkMB"], c="darkgreen", s=100, alpha=0.7, label="High Decrypt", marker="s")
    ax10.scatter(medium_perf_decrypt["Threads"], medium_perf_decrypt["ChunkMB"], c="darkorange", s=80, alpha=0.7, label="Medium Decrypt", marker="s")
    ax10.set_title("🎯 Performance Zones", fontsize=12, fontweight="bold")
    ax10.set_xlabel("Threads")
    ax10.set_ylabel("Chunk Size (MB)")
    ax10.legend(bbox_to_anchor=(1.05, 1), loc="upper left")
    ax10.grid(True, alpha=0.3)

    # 11: Optimization suggestions table
    encrypt_best = encrypt_data.loc[encrypt_data["Throughput"].idxmax()]
    decrypt_best = decrypt_data.loc[decrypt_data["Throughput"].idxmax()]
    ax11.axis("off")
    recommendations = [
        ["🏆 BEST CONFIGURATIONS", "", ""],
        ["Operation", "Threads", "Chunk Size"],
        ["Encryption", f"{encrypt_best['Threads']:.0f}", f"{encrypt_best['ChunkMB']:.0f}MB"],
        ["Decryption", f"{decrypt_best['Threads']:.0f}", f"{decrypt_best['ChunkMB']:.0f}MB"],
        ["", "", ""],
        ["📈 PERFORMANCE INSIGHTS", "", ""],
        ["Avg Decrypt Speed", f"{decrypt_data['Throughput'].mean():.0f}", "MB/s"],
        ["Avg Encrypt Speed", f"{encrypt_data['Throughput'].mean():.0f}", "MB/s"],
        ["Decrypt Advantage", f"{((decrypt_data['Throughput'].mean() / encrypt_data['Throughput'].mean() - 1) * 100):.1f}%", ""],
        ["", "", ""],
        ["💡 RECOMMENDATIONS", "", ""],
        ["For Encryption", "Use 16-32MB", "chunks"],
        ["For Decryption", "Use 2-8", "threads"],
        ["General Rule", "Bigger chunks", "for encrypt"],
        ["Thread Scaling", "2-16 threads", "optimal"],
    ]
    table = ax11.table(
        cellText=recommendations,
        cellLoc="center",
        loc="center",
        cellColours=[["lightgray"] * 3 if i in [0, 5, 10] else ["white"] * 3 for i in range(len(recommendations))],
    )
    table.auto_set_font_size(False)
    table.set_fontsize(9)
    table.scale(1, 2)
    ax11.set_title("💡 Smart Optimization Guide", fontsize=12, fontweight="bold")

    # 12: Summary pie (placed in the last grid cell)
    ax12_sub1 = plt.subplot2grid((4, 3), (3, 2), fig=fig)
    avg_speeds = [encrypt_data["Throughput"].mean(), decrypt_data["Throughput"].mean()]
    ax12_sub1.pie(avg_speeds, labels=["Encrypt", "Decrypt"], colors=["skyblue", "lightcoral"], autopct="%1.0f%%", startangle=90)
    ax12_sub1.set_title("Performance Share", fontsize=10, fontweight="bold")

    return fig, encrypt_best, decrypt_best


# --- OpenSSL comparison (openssl_comparison.png) ------------------------------


def plot_openssl_comparison(enc: pd.DataFrame, dec: pd.DataFrame, ossl: pd.DataFrame, out_path: Path) -> None:
    """Compare CottonCrypto (best-per-chunk) against OpenSSL across buffer sizes."""
    if ossl is None or ossl.empty:
        print("[warn] OpenSSL data missing; skipping openssl_comparison.png")
        return

    enc_best = enc.groupby("ChunkMB")["Throughput"].max().reset_index()
    dec_best = dec.groupby("ChunkMB")["Throughput"].max().reset_index()
    enc_best["BlockBytes"] = (enc_best["ChunkMB"] * 1_000_000).astype(int)
    dec_best["BlockBytes"] = (dec_best["ChunkMB"] * 1_000_000).astype(int)

    fig, ax = plt.subplots(figsize=(10, 6))
    fig.suptitle("CottonCrypto vs OpenSSL: Throughput vs Buffer/Chunk Size", fontsize=16, fontweight="bold", y=0.96)
    ax.plot(ossl["BlockBytes"], ossl["ThroughputMBps"], marker="o", linewidth=2.5, markersize=7, label="OpenSSL AES-128-GCM")
    ax.plot(enc_best["BlockBytes"], enc_best["Throughput"], marker="s", linewidth=2.0, markersize=7, label="CottonCrypto Encrypt (best per chunk)")
    ax.plot(dec_best["BlockBytes"], dec_best["Throughput"], marker="^", linewidth=2.0, markersize=7, label="CottonCrypto Decrypt (best per chunk)")
    ax.set_xscale("log")
    ax.set_xlabel("Buffer / Chunk Size (bytes) [log scale]")
    ax.set_ylabel("Throughput (MB/s)")
    ax.grid(True, which="both", axis="both", alpha=0.3)
    ax.legend()
    note = (
        "Note: OpenSSL varies small buffer sizes; CottonCrypto varies file chunk sizes."
        " Scales differ; this is an approximate visual comparison."
    )
    ax.text(0.01, -0.18, note, transform=ax.transAxes, fontsize=9, va="top", ha="left", wrap=True)
    fig.tight_layout()
    fig.savefig(out_path, dpi=300, bbox_inches="tight")
    print(f"[ok] Saved {out_path.name}")
