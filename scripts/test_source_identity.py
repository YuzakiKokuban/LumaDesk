import importlib.util
from pathlib import Path
import unittest


spec = importlib.util.spec_from_file_location('source_identity', Path(__file__).with_name('verify-source.py'))
identity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(identity)
sample_name = 'Open' + 'Revo'


class SourceIdentityTests(unittest.TestCase):
    def test_explicit_sample_provenance_is_allowed(self):
        for name in identity.reference_files:
            self.assertEqual(identity.violations_for(name, sample_name + ' 0.8.8 sample'), [])
        self.assertEqual(identity.violations_for('docs/VALIDATION.md', '## ' + sample_name + ' 0.8.8 静态分析'), [])

    def test_shipping_identity_and_other_documents_stay_checked(self):
        for name in ['app/App.cs', 'README.md', 'installer/LumaDesk.iss', 'reverse/native/new-report.md']:
            self.assertEqual(identity.violations_for(name, sample_name), [name + ':1'])
        obsolete_path = 'app/open-' + 'revo.cs'
        self.assertEqual(identity.violations_for(obsolete_path, ''), [obsolete_path])

    def test_other_obsolete_identity_cannot_hide_in_provenance_link(self):
        self.assertEqual(identity.violations_for('docs/VALIDATION.md', sample_name + ' 0.8.8 Tau' + 'ri'), ['docs/VALIDATION.md:1'])
        self.assertEqual(identity.violations_for('docs/VALIDATION.md', 'Old product ' + sample_name), ['docs/VALIDATION.md:1'])
