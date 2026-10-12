#!/usr/bin/env bash
# C++ cognitive complexity + branch/line counts per function via clang-tidy (bundled with VS 2022 LLVM tools).
# Usage (from the repo root):  bash tools/metrics/cpp-complexity.sh [repoRoot=.] [out.log=clang-tidy.log]
# Then:                        py -I tools/metrics/parse_clang_tidy.py clang-tidy.log
# clang-tidy is taken from $CLANG_TIDY, else PATH. The LLVM tools bundled with Visual Studio's
# "C++ Clang tools for Windows" component also work: set CLANG_TIDY to its clang-tidy.exe.
CT="${CLANG_TIDY:-$(command -v clang-tidy)}"
[ -n "$CT" ] || { echo "clang-tidy not found; set CLANG_TIDY or add it to PATH" >&2; exit 1; }
OUT="$(realpath -m "${2:-clang-tidy.log}")"
cd "${1:-.}" || exit 1; : > "$OUT"
for d in native/*/; do for f in "$d"src/*.cpp; do
  "$CT" "$f" -checks='-*,readability-function-cognitive-complexity,readability-function-size' \
    -config='{CheckOptions: {readability-function-cognitive-complexity.Threshold: 0, readability-function-cognitive-complexity.DescribeBasicIncrements: false, readability-function-size.BranchThreshold: 0, readability-function-size.LineThreshold: 0}}' \
    -- -std=c++20 -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH -I"${d}include" >> "$OUT" 2>&1
done; done
