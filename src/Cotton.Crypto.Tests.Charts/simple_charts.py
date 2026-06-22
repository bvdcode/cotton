"""Generate the polished 4-panel throughput figure (performance_charts.png)."""

import matplotlib.pyplot as plt

from chart_common import create_simple_plots, parse_test_results


def print_summary(encrypt_data, decrypt_data) -> None:
    """Print a short summary of the best and average results."""
    print("\n" + "=" * 50)
    print("BRIEF ANALYSIS SUMMARY")
    print("=" * 50)

    encrypt_best = encrypt_data.loc[encrypt_data["Throughput"].idxmax()]
    decrypt_best = decrypt_data.loc[decrypt_data["Throughput"].idxmax()]

    print("\n🏆 TOP RESULTS:")
    print(f"   Encryption: {encrypt_best['Throughput']:.1f} MB/s")
    print(f"   ({encrypt_best['Threads']:.0f} threads, {encrypt_best['ChunkMB']:.0f}MB chunks)")
    print(f"   Decryption: {decrypt_best['Throughput']:.1f} MB/s")
    print(f"   ({decrypt_best['Threads']:.0f} threads, {decrypt_best['ChunkMB']:.0f}MB chunks)")

    print("\n📊 AVERAGE VALUES:")
    print(f"   Encryption: {encrypt_data['Throughput'].mean():.1f} MB/s")
    print(f"   Decryption: {decrypt_data['Throughput'].mean():.1f} MB/s")
    advantage = (decrypt_data["Throughput"].mean() / encrypt_data["Throughput"].mean() - 1) * 100
    print(f"   Decryption is {advantage:.1f}% faster")

    print("\n" + "=" * 50)


def main() -> None:
    """Parse input.txt, build the 4-panel figure and save it."""
    try:
        encrypt_data, decrypt_data = parse_test_results("input.txt")

        if encrypt_data.empty or decrypt_data.empty:
            print("Error: failed to find data in input.txt")
            return

        print("✅ Data successfully loaded:")
        print(f"   Encryption: {len(encrypt_data)} records")
        print(f"   Decryption: {len(decrypt_data)} records")

        fig = create_simple_plots(encrypt_data, decrypt_data)
        fig.savefig("performance_charts.png", dpi=300, bbox_inches="tight", facecolor="white", edgecolor="none")
        print("\n💾 Charts saved to performance_charts.png")

        print_summary(encrypt_data, decrypt_data)
        plt.show()

    except FileNotFoundError:
        print("❌ Error: file 'input.txt' not found")
    except Exception as e:
        print(f"❌ Error: {e}")


if __name__ == "__main__":
    main()
