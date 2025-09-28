import os
import sys
import argparse
import importlib
from pathlib import Path

# Ensure non-interactive backend for matplotlib in child modules before they import pyplot
os.environ.setdefault("MPLBACKEND", "Agg")

ROOT = Path(__file__).parent.resolve()
INPUT_DEFAULT = ROOT / "input.txt"


def _run_module_main(module_name: str) -> None:
    """Import module, patch plt.show to no-op, then call its main()."""
    mod = importlib.import_module(module_name)
    # Patch plt.show to avoid GUI blocking
    if hasattr(mod, "plt") and hasattr(mod.plt, "show"):
        try:
            mod.plt.show = lambda *args, **kwargs: None  # type: ignore[attr-defined]
        except Exception:
            pass
    # Call module main()
    if hasattr(mod, "main"):
        mod.main()
    else:
        raise RuntimeError(f"Module '{module_name}' has no main() function")


def generate_simple():
    print("\n=== ✅ Генерация простых графиков ===")
    _run_module_main("simple_charts")


def generate_advanced():
    print("\n=== ✅ Генерация расширенных графиков ===")
    _run_module_main("advanced_analysis")


def generate_mega():
    print("\n=== ✅ Генерация MEGA-графиков ===")
    _run_module_main("mega_advanced_analysis")


def run_selected(sets: list[str]):
    # Always run from script directory so child scripts find input.txt relative to this file
    os.chdir(ROOT)

    # Basic presence check for input
    if not INPUT_DEFAULT.exists():
        print(f"❌ Не найден файл с данными: {INPUT_DEFAULT}")
        sys.exit(1)

    for s in sets:
        if s == "simple":
            generate_simple()
        elif s == "advanced":
            generate_advanced()
        elif s == "mega":
            generate_mega()
        else:
            raise ValueError(f"Unknown set: {s}")

    print("\n🎉 Готово. Созданы файлы (если данных было достаточно):")
    print("  • performance_charts.png")
    print("  • advanced_performance_analysis.png")
    print("  • mega_performance_analysis.png")


def interactive_menu() -> list[str]:
    print("\nЧто создать? Выберите опцию и нажмите Enter:")
    print("  1) Только простые графики")
    print("  2) Только расширенные графики")
    print("  3) Только MEGA-графики")
    print("  4) Всё сразу")

    choice = input("> ").strip()
    mapping = {
        "1": ["simple"],
        "2": ["advanced"],
        "3": ["mega"],
        "4": ["simple", "advanced", "mega"],
    }
    return mapping.get(choice, ["simple", "advanced", "mega"])  # по умолчанию — всё


def parse_args(argv: list[str]):
    p = argparse.ArgumentParser(description="Создание графиков производительности (simple/advanced/mega)")
    g = p.add_mutually_exclusive_group()
    g.add_argument("--simple", action="store_true", help="Создать только простые графики")
    g.add_argument("--advanced", action="store_true", help="Создать только расширенные графики")
    g.add_argument("--mega", action="store_true", help="Создать только MEGA-графики")
    g.add_argument("--all", action="store_true", help="Создать все графики (по умолчанию)")
    p.add_argument("--menu", action="store_true", help="Показать интерактивное меню выбора")
    return p.parse_args(argv)


def main(argv: list[str] | None = None):
    args = parse_args(sys.argv[1:] if argv is None else argv)

    if args.menu:
        sets = interactive_menu()
    else:
        if args.simple:
            sets = ["simple"]
        elif args.advanced:
            sets = ["advanced"]
        elif args.mega:
            sets = ["mega"]
        else:
            # --all или по умолчанию
            sets = ["simple", "advanced", "mega"]

    run_selected(sets)


if __name__ == "__main__":
    main()
