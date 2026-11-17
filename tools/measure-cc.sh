#!/usr/bin/env bash
# Цикломатична складність кожного методу src/ за інструментом CA1502.
# Тимчасово підключає tools/metrics.props і tools/editorconfig-cc, після запуску все прибирає.
# Використання: bash tools/measure-cc.sh [файл-результату]   (за замовчуванням — лише на екран)
set -u
cd "$(dirname "$0")/.."
out="${1:-/dev/null}"
printf '<Project>\n  <Import Project="tools/metrics.props" />\n</Project>\n' > Directory.Build.props
cp tools/editorconfig-cc .editorconfig
dotnet build src --no-incremental 2>&1 | grep 'warning CA1502' \
    | sed -E "s#^.*/src/([A-Za-z]+\.cs)\(([0-9]+),[0-9]+\): warning CA1502: '([^']+)' has a cyclomatic complexity of '([0-9]+)'.*#\1:\2 \3 = \4#" \
    | sort -u -t: -k1,1 -k2,2n | tee "$out"
rm -f Directory.Build.props .editorconfig
