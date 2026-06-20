"""Generate the 6-panel advanced analysis figure (advanced_performance_analysis.png)."""

import traceback

import matplotlib.pyplot as plt

from chart_common import create_advanced_plots, parse_test_results


def print_analysis_summary(encrypt_data, decrypt_data, encrypt_optimal, decrypt_optimal) -> None:
    """Print the optimal configurations and overall statistics."""
    print("\n" + "=" * 60)
    print("PERFORMANCE ANALYSIS")
    print("=" * 60)

    print("\n📊 OPTIMAL CONFIGURATIONS:")
    print("   Encryption:")
    print(f"      Best result: {encrypt_optimal['Throughput']:.1f} MB/s")
    print(f"      Threads: {encrypt_optimal['Threads']}, Chunk size: {encrypt_optimal['ChunkMB']}MB")
    print("   Decryption:")
    print(f"      Best result: {decrypt_optimal['Throughput']:.1f} MB/s")
    print(f"      Threads: {decrypt_optimal['Threads']}, Chunk size: {decrypt_optimal['ChunkMB']}MB")

    print("\n📈 OVERALL STATISTICS:")
    for name, data in [("Encryption", encrypt_data), ("Decryption", decrypt_data)]:
        print(f"   {name}:")
        print(f"      Mean: {data['Throughput'].mean():.1f} MB/s")
        print(f"      Median: {data['Throughput'].median():.1f} MB/s")
        print(f"      Min/Max: {data['Throughput'].min():.1f} / {data['Throughput'].max():.1f} MB/s")

    print("\n🧩 EFFECT OF CHUNK SIZE:")
    for operation, data in [("Encryption", encrypt_data), ("Decryption", decrypt_data)]:
        chunk_performance = data.groupby("ChunkMB")["Throughput"].mean().sort_values(ascending=False)
        best_chunk = chunk_performance.index[0]
        worst_chunk = chunk_performance.index[-1]
        print(f"   {operation}:")
        print(f"      Best chunk size: {best_chunk}MB ({chunk_performance[best_chunk]:.1f} MB/s)")
        print(f"      Worst chunk size: {worst_chunk}MB ({chunk_performance[worst_chunk]:.1f} MB/s)")

    print("\n" + "=" * 60)


def main() -> None:
    """Parse input.txt, build the 6-panel figure and save it."""
    try:
        encrypt_data, decrypt_data = parse_test_results("input.txt")

        if encrypt_data.empty or decrypt_data.empty:
            print("Error: failed to find data in input.txt")
            return

        print("Data loaded:")
        print(f"  Encryption: {len(encrypt_data)} records")
        print(f"  Decryption: {len(decrypt_data)} records")

        fig, encrypt_optimal, decrypt_optimal = create_advanced_plots(encrypt_data, decrypt_data)
        fig.savefig("advanced_performance_analysis.png", dpi=300, bbox_inches="tight")
        print("\nAdvanced charts saved to advanced_performance_analysis.png")

        print_analysis_summary(encrypt_data, decrypt_data, encrypt_optimal, decrypt_optimal)
        plt.show()

    except FileNotFoundError:
        print("Error: file 'input.txt' not found")
    except Exception as e:
        print(f"Error: {e}")
        traceback.print_exc()


if __name__ == "__main__":
    main()
