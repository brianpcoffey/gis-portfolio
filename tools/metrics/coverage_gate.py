"""Coverage gate over a Cobertura report: fails when line or branch coverage is under the minimum.

Usage (from the repo root):
  dotnet test Portfolio.Tests -c Release --collect:"XPlat Code Coverage" \\
      --settings coverlet.runsettings --results-directory TestResults
  py -I tools/metrics/coverage_gate.py [results_dir=TestResults] [--min-line 80] [--min-branch 60]
                                       [--summary PATH]

results_dir is searched recursively for coverage.cobertura.xml (all matches must be byte-identical,
otherwise the gate refuses), or may be the XML file itself. MIN_LINE_COVERAGE / MIN_BRANCH_COVERAGE environment variables are honoured as defaults
(CI compatibility). A markdown summary is written only when --summary PATH is given (nothing is written by default).
Exit status: 0 = pass, 1 = below a minimum, 2 = no report found, or more than one distinct report.
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

print(f'Line coverage   {line:6.2f}%  (min {min_line}%)  {lc}/{lv}')
print(f'Branch coverage {branch:6.2f}%  (min {min_branch}%)  {bc}/{bv}')

if args.summary:
    with open(args.summary, 'w', encoding='utf-8') as fh:
        fh.write('| Metric | Covered | Total | Rate | Min |\n|---|---|---|---|---|\n')
        fh.write(f'| Line | {lc} | {lv} | {line:.2f}% | {min_line}% |\n')
        fh.write(f'| Branch | {bc} | {bv} | {branch:.2f}% | {min_branch}% |\n')

failed = False
if line < min_line:
    print(f'::error::Line coverage {line:.2f}% is below the {min_line}% minimum.')
    failed = True
if branch < min_branch:
    print(f'::error::Branch coverage {branch:.2f}% is below the {min_branch}% minimum.')
    failed = True
sys.exit(1 if failed else 0)
