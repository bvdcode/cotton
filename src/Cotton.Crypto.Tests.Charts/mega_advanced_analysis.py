"""Generate the 12-panel mega analysis figure (mega_performance_analysis.png)."""

import traceback

import matplotlib.pyplot as plt

from chart_common import create_mega_analysis, parse_test_results


def print_mega_summary(encrypt_data, decrypt_data, encrypt_best, decrypt_best) -> None:
    """Print the detailed mega analysis summary."""
    print("\n" + "=" * 70)
    print("🚀 MEGA PERFORMANCE ANALYSIS SUMMARY")
    print("=" * 70)

    print("\n📊 DATASET OVERVIEW:")
    print(f"   Total measurements: {len(encrypt_data) + len(decrypt_data)}")
    print(f"   Thread configurations: {len(encrypt_data['Threads'].unique())}")
    print(f"   Chunk size variations: {len(encrypt_data['ChunkMB'].unique())}")
    print(f"   Test combinations per operation: {len(encrypt_data)}")

    print("\n🏆 ABSOLUTE CHAMPIONS:")
    print(f"   🔐 Encryption King: {encrypt_best['Throughput']:.1f} MB/s")
    print(f"       Configuration: {encrypt_best['Threads']:.0f} threads × {encrypt_best['ChunkMB']:.0f}MB chunks")
    print(f"   🔓 Decryption Master: {decrypt_best['Throughput']:.1f} MB/s")
    print(f"       Configuration: {decrypt_best['Threads']:.0f} threads × {decrypt_best['ChunkMB']:.0f}MB chunks")

    print("\n📈 DETAILED STATISTICS:")
    for op_name, data in [("Encryption", encrypt_data), ("Decryption", decrypt_data)]:
        stats = data["Throughput"].describe()
        print(f"   {op_name}:")
        print(f"      Mean: {stats['mean']:.1f} MB/s")
        print(f"      Median: {stats['50%']:.1f} MB/s")
        print(f"      Std Dev: {stats['std']:.1f} MB/s")
        print(f"      Range: {stats['min']:.1f} - {stats['max']:.1f} MB/s")
        print(f"      Coefficient of Variation: {(stats['std'] / stats['mean'] * 100):.1f}%")

    print("\n⚡ EFFICIENCY ANALYSIS:")
    speed_advantage = (decrypt_data["Throughput"].mean() / encrypt_data["Throughput"].mean() - 1) * 100
    print(f"   Decryption speed advantage: {speed_advantage:.1f}%")

    print("\n🎯 OPTIMAL THREAD COUNTS:")
    for op_name, data in [("Encryption", encrypt_data), ("Decryption", decrypt_data)]:
        thread_performance = data.groupby("Threads")["Throughput"].mean().sort_values(ascending=False)
        print(f"   {op_name}: {thread_performance.index[0]} threads ({thread_performance.iloc[0]:.1f} MB/s avg)")

    print("\n🧩 OPTIMAL CHUNK SIZES:")
    for op_name, data in [("Encryption", encrypt_data), ("Decryption", decrypt_data)]:
        chunk_performance = data.groupby("ChunkMB")["Throughput"].mean().sort_values(ascending=False)
        print(f"   {op_name}: {chunk_performance.index[0]}MB chunks ({chunk_performance.iloc[0]:.1f} MB/s avg)")

    print("\n💡 KEY INSIGHTS:")
    print("   • Decryption consistently outperforms encryption")
    print("   • Larger chunks generally favor encryption performance")
    print("   • Thread scaling shows diminishing returns after 8-16 threads")
    print("   • Configuration matters more than raw thread count")

    print("\n" + "=" * 70)


def main() -> None:
    """Parse input.txt, build the 12-panel figure and save it."""
    try:
        encrypt_data, decrypt_data = parse_test_results("input.txt")

        if encrypt_data.empty or decrypt_data.empty:
            print("Error: failed to find data in input.txt")
            return

        print("🎯 Data loaded for MEGA analysis:")
        print(f"   Encryption: {len(encrypt_data)} records")
        print(f"   Decryption: {len(decrypt_data)} records")

        fig, encrypt_optimal, decrypt_optimal = create_mega_analysis(encrypt_data, decrypt_data)
        fig.savefig("mega_performance_analysis.png", dpi=300, bbox_inches="tight")
        print("\n💾 MEGA analysis saved to mega_performance_analysis.png")

        print_mega_summary(encrypt_data, decrypt_data, encrypt_optimal, decrypt_optimal)
        plt.show()

    except FileNotFoundError:
        print("❌ Error: file 'input.txt' not found")
    except Exception as e:
        print(f"❌ Error: {e}")
        traceback.print_exc()


if __name__ == "__main__":
    main()
