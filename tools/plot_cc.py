"""Стовпчикова діаграма цикломатичної складності з файлів метрик.

Використання:
  python3 tools/plot_cc.py docs/metrics-before.csv docs/cc-before.png
  python3 tools/plot_cc.py docs/metrics-before.csv docs/cc-compare.png docs/metrics-after.csv
"""
import csv
import sys

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib.ticker import MaxNLocator  # noqa: E402

THRESHOLD = 8
TOP = 15
INK, MUTED, GRID = "#1f2328", "#59636e", "#d0d7de"
BEFORE, AFTER = "#2f6fb3", "#d07a1f"


def load(path):
    with open(path, encoding="utf-8") as f:
        rows = list(csv.DictReader(f, delimiter=";"))
    return {f"{r['Клас']}.{r['Метод']}": int(r["CC"]) for r in rows}


def style(ax, title):
    ax.set_title(title, color=INK, fontsize=12, loc="left")
    ax.set_xlabel("Цикломатична складність (CC)", color=MUTED)
    ax.grid(axis="x", color=GRID, linewidth=0.8)
    ax.set_axisbelow(True)
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    for side in ("left", "bottom"):
        ax.spines[side].set_color(GRID)
    ax.tick_params(colors=MUTED)
    ax.xaxis.set_major_locator(MaxNLocator(integer=True))
    ax.axvline(THRESHOLD, color=INK, linestyle="--", linewidth=1.2)
    ax.text(THRESHOLD + 0.2, -0.9, f"поріг CC = {THRESHOLD}", color=INK, fontsize=9, va="bottom")


def main():
    before = load(sys.argv[1])
    names = sorted(before, key=lambda n: before[n], reverse=True)[:TOP][::-1]
    fig, ax = plt.subplots(figsize=(9, 6.2), dpi=150)
    if len(sys.argv) > 3:
        after = load(sys.argv[3])
        y = range(len(names))
        b = [before[n] for n in names]
        a = [after.get(n, 0) for n in names]
        ax.barh([i + 0.2 for i in y], b, height=0.38, color=BEFORE, label="до рефакторингу")
        ax.barh([i - 0.2 for i in y], a, height=0.38, color=AFTER, label="після рефакторингу")
        for i, n in enumerate(names):
            ax.text(b[i] + 0.2, i + 0.2, str(b[i]), va="center", fontsize=8, color=INK)
            label = str(a[i]) if n in after else "0 (метод видалено)"
            ax.text(a[i] + 0.2, i - 0.2, label, va="center", fontsize=8, color=INK)
        ax.legend(loc="lower right", frameon=False)
        title = "Цикломатична складність методів до і після рефакторингу"
    else:
        vals = [before[n] for n in names]
        ax.barh(range(len(names)), vals, height=0.6, color=BEFORE)
        for i, v in enumerate(vals):
            ax.text(v + 0.2, i, str(v), va="center", fontsize=9, color=INK)
        title = "Цикломатична складність методів до рефакторингу"
    ax.set_yticks(range(len(names)))
    ax.set_yticklabels(names, fontsize=9, color=INK)
    ax.set_ylabel("Метод", color=MUTED)
    style(ax, title)
    fig.tight_layout()
    fig.savefig(sys.argv[2])


if __name__ == "__main__":
    main()
