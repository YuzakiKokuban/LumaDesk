"""Reject obsolete product identifiers in tracked source and paths."""
from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[1]
pattern = re.compile('open' + r'[-_ ]?' + 'revo' + '|tau' + 'ri', re.IGNORECASE)
# Explicit third-party sample evidence is provenance, not the product's identity.
# Keep this narrow so source, shipping assets and other documentation stay checked.
reference_files = {
    'reverse/native/OPEN' + 'REVO_0_8_8.md',
    'reverse/native/evidence/open' + 'revo-0.8.8-analysis.json',
}


def violations_for(name, content):
    if name in reference_files:
        return []
    violations = [name] if pattern.search(name) else []
    for number, line in enumerate(content.splitlines(), 1):
        checked = line
        if name == 'docs/VALIDATION.md' and ('Open' + 'Revo 0.8.8' in line or 'OPEN' + 'REVO_0_8_8.md' in line):
            checked = re.sub('open' + r'[-_ ]?' + 'revo', '', checked, flags=re.IGNORECASE)
        if pattern.search(checked):
            violations.append(f'{name}:{number}')
    return violations


def main():
    violations = []
    for name in subprocess.check_output(['git', 'ls-files'], cwd=root, text=True).splitlines():
        path = root / name
        if not path.is_file():
            continue
        try:
            content = path.read_text(encoding='utf-8-sig')
        except UnicodeError:
            content = ''
        violations.extend(violations_for(name, content))
    if violations:
        raise SystemExit('Obsolete identifiers: ' + ', '.join(violations))
    print('Tracked source and filenames verified')


if __name__ == '__main__':
    main()
