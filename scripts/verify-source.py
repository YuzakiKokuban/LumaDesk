"""Reject obsolete product identifiers in tracked source and paths."""
from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[1]
pattern = re.compile('open' + r'[-_ ]?' + 'revo' + '|tau' + 'ri', re.IGNORECASE)
violations = []
for name in subprocess.check_output(['git', 'ls-files'], cwd=root, text=True).splitlines():
    path = root / name
    if not path.is_file():
        continue
    if pattern.search(name):
        violations.append(name)
    try:
        content = path.read_text(encoding='utf-8-sig')
    except UnicodeError:
        continue
    for number, line in enumerate(content.splitlines(), 1):
        if pattern.search(line):
            violations.append(f'{name}:{number}')
if violations:
    raise SystemExit('Obsolete identifiers: ' + ', '.join(violations))
print('Tracked source and filenames verified')
