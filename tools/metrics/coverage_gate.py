"""Coverage gate over a Cobertura report: fails when line or branch coverage is under the minimum, overall or
for a named assembly. CI runs this script (the test job's "Enforce coverage thresholds" step) with the floors
from the workflow's env block, so a local run and CI use the same code.

Usage (from the repo root):
  dotnet test Portfolio.Tests -c Release --collect:"XPlat Code Coverage" \\
      --settings coverlet.runsettings --results-directory TestResults
  py -I tools/metrics/coverage_gate.py [results_dir=TestResults] [--min-line 80] [--min-branch 60]
                                       [--package-floor NAME LINE BRANCH ...] [--summary PATH]

results_dir is searched recursively for coverage.cobertura.xml (all matches must be byte-identical,
otherwise the gate refuses), or may be the XML file itself. MIN_LINE_COVERAGE / MIN_BRANCH_COVERAGE environment variables are honoured as defaults
(CI compatibility). --package-floor adds a per-assembly floor (repeatable), e.g. --package-floor Portfolio.Web 83 66;
the assembly must be present in the report. A markdown summary is written only when --summary PATH is given
(nothing is written by default).
Exit status: 0 = pass, 1 = below a minimum, 2 = no report found, more than one distinct report, or a named assembly
missing from the report.
"""
import argparse
import glob
import hashlib
import os
import sys
import xml.etree.ElementTree as ET

ap = argparse.ArgumentParser(description='Gate on Cobertura line/branch coverage.')
ap.add_argument('results_dir', nargs='?', default='TestResults',
                help='directory searched for coverage.cobertura.xml, or the XML file (default: TestResults)')
ap.add_argument('--min-line', type=float, default=float(os.environ.get('MIN_LINE_COVERAGE', '80')))
ap.add_argument('--min-branch', type=float, default=float(os.environ.get('MIN_BRANCH_COVERAGE', '60')))
ap.add_argument('--package-floor', nargs=3, action='append', default=[], metavar=('NAME', 'LINE', 'BRANCH'),
                help='per-assembly minimum line and branch coverage, e.g. Portfolio.Web 83 66 (repeatable)')
ap.add_argument('--summary', default='', help='write a markdown summary to this path (opt-in; default: none)')
args = ap.parse_args()

if os.path.isfile(args.results_dir):
    reports = [args.results_dir]
else:
    reports = sorted(glob.glob(os.path.join(args.results_dir, '**', 'coverage.cobertura.xml'), recursive=True))
if not reports:
    print(f'::error::No coverage.cobertura.xml found under {args.results_dir}.')
    sys.exit(2)
distinct = {hashlib.sha256(open(r, 'rb').read()).hexdigest() for r in reports}
if len(distinct) > 1:
    print(f'::error::Expected exactly one distinct coverage report under {args.results_dir}; found {len(distinct)}.')
    sys.exit(2)

root = ET.parse(reports[0]).getroot()
lc, lv = int(root.get('lines-covered')), int(root.get('lines-valid'))
bc, bv = int(root.get('branches-covered')), int(root.get('branches-valid'))
line = lc / lv * 100 if lv else 0.0
branch = bc / bv * 100 if bv else 0.0
min_line, min_branch = args.min_line, args.min_branch

# Per-assembly floors read the package rates cobertura reports (one <package> per assembly).
packages = {p.get('name'): p for p in root.iter('package')}
package_rows = []  # (name, line %, branch %, min line, min branch)
for name, floor_line, floor_branch in args.package_floor:
    package = packages.get(name)
    if package is None:
        print(f'::error::Assembly {name} is not in the coverage report (found: {", ".join(sorted(packages))}).')
        sys.exit(2)
    package_rows.append((name, float(package.get('line-rate')) * 100, float(package.get('branch-rate')) * 100,
                         float(floor_line), float(floor_branch)))

print(f'Line coverage   {line:6.2f}%  (min {min_line}%)  {lc}/{lv}')
print(f'Branch coverage {branch:6.2f}%  (min {min_branch}%)  {bc}/{bv}')
for name, p_line, p_branch, p_min_line, p_min_branch in package_rows:
    print(f'{name} line coverage   {p_line:6.2f}%  (min {p_min_line}%)')
    print(f'{name} branch coverage {p_branch:6.2f}%  (min {p_min_branch}%)')

if args.summary:
    with open(args.summary, 'w', encoding='utf-8') as fh:
        fh.write('| Metric | Covered | Total | Rate | Min |\n|---|---|---|---|---|\n')
        fh.write(f'| Line | {lc} | {lv} | {line:.2f}% | {min_line}% |\n')
        fh.write(f'| Branch | {bc} | {bv} | {branch:.2f}% | {min_branch}% |\n')
        for name, p_line, p_branch, p_min_line, p_min_branch in package_rows:
            fh.write(f'| Line ({name}) | | | {p_line:.2f}% | {p_min_line}% |\n')
            fh.write(f'| Branch ({name}) | | | {p_branch:.2f}% | {p_min_branch}% |\n')

failed = False
if line < min_line:
    print(f'::error::Line coverage {line:.2f}% is below the {min_line}% minimum.')
    failed = True
if branch < min_branch:
    print(f'::error::Branch coverage {branch:.2f}% is below the {min_branch}% minimum.')
    failed = True
for name, p_line, p_branch, p_min_line, p_min_branch in package_rows:
    if p_line < p_min_line:
        print(f'::error::{name} line coverage {p_line:.2f}% is below the {p_min_line}% minimum.')
        failed = True
    if p_branch < p_min_branch:
        print(f'::error::{name} branch coverage {p_branch:.2f}% is below the {p_min_branch}% minimum.')
        failed = True
sys.exit(1 if failed else 0)
