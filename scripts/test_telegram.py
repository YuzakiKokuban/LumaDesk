import io
import json
from pathlib import Path
import tempfile
import unittest
import urllib.error
from test_release_tools import load

notify = load("notify-telegram")


class DeliveryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.context = {"GITHUB_SHA": "a" * 40, "GITHUB_REPOSITORY": "owner/LumaDesk",
                        "GITHUB_RUN_ID": "123", "GITHUB_REF": "refs/heads/dev"}
        self.make_assets("0.2.0-beta.5")

    def make_assets(self, version):
        for suffix in (".zip", "-Setup.exe"):
            package = self.directory / ("LumaDesk-" + version + "-win-x64" + suffix)
            package.write_bytes(b"installer or zip fixture")
            digest = notify.hashlib.sha256(package.read_bytes()).hexdigest()
            package.with_name(package.name + ".sha256").write_text(digest + "  " + package.name)

    def prepared(self):
        return notify.prepare(self.directory, "dev", self.context, "<unescaped & commit>" * 100)

    def test_dev_delivery_identifies_exact_source_without_markup(self):
        installer, filename, caption = self.prepared()
        self.assertIn("-dev-aaaaaaaa-", filename)
        self.assertLessEqual(len(caption), 1024)
        body, kind = notify.multipart(installer, filename, caption, "@channel")
        self.assertIn(b'name="document"', body)
        self.assertNotIn(b'name="parse_mode"', body)
        self.assertIn(caption.encode(), body)
        self.assertIn("multipart/form-data", kind)

    def test_tampered_installer_never_reaches_upload(self):
        next(self.directory.glob("*.exe")).write_bytes(b"changed")
        with self.assertRaises(ValueError):
            self.prepared()

    def test_delivery_cannot_use_pr_main_or_wrong_release_tag(self):
        for ref in ("refs/pull/1/merge", "refs/heads/main", "refs/tags/v0.2.0-beta.5"):
            self.context["GITHUB_REF"] = ref
            with self.subTest(ref=ref), self.assertRaises(ValueError):
                self.prepared()
        self.context["GITHUB_REF"] = "refs/tags/v0.2.0-beta.5"
        notify.prepare(self.directory, "prerelease", self.context)
        with self.assertRaises(ValueError):
            notify.prepare(self.directory, "stable", self.context)
        self.context["GITHUB_REF"] = "refs/tags/v0.2.0-beta.6"
        with self.assertRaises(ValueError):
            notify.prepare(self.directory, "prerelease", self.context)

    def test_stable_delivery_uses_published_filename(self):
        with tempfile.TemporaryDirectory() as directory:
            self.directory = Path(directory)
            self.make_assets("0.2.0")
            self.context["GITHUB_REF"] = "refs/tags/v0.2.0"
            _, filename, caption = notify.prepare(self.directory, "stable", self.context)
            self.assertEqual(filename, "LumaDesk-0.2.0-win-x64-Setup.exe")
            self.assertIn("/releases/tag/v0.2.0", caption)

    def upload(self, opener, sleep=lambda seconds: None):
        return notify.send(*self.prepared(), "123456:private_token", "@channel", opener=opener, sleep=sleep)

    def test_success_requires_document_confirmation(self):
        result = {"ok": True, "result": {"message_id": 9, "document": {"file_name": "setup.exe"}}}
        self.assertEqual(self.upload(lambda *a, **k: io.BytesIO(json.dumps(result).encode()))["message_id"], 9)
        with self.assertRaises(RuntimeError):
            self.upload(lambda *a, **k: io.BytesIO(b'{"ok":true,"result":{}}'))

    def test_rejection_is_visible_without_leaking_credentials(self):
        with self.assertRaisesRegex(RuntimeError, "code 403") as error:
            self.upload(lambda *a, **k: io.BytesIO(b'{"ok":false,"error_code":403,"description":"private_token"}'))
        self.assertNotIn("private_token", str(error.exception))

    def test_only_explicit_rate_limit_retries(self):
        calls, delays = [], []
        def opener(request, **kwargs):
            calls.append(request)
            if len(calls) == 1:
                raise urllib.error.HTTPError(request.full_url, 429, "limit", {}, io.BytesIO(b'{"ok":false,"error_code":429,"parameters":{"retry_after":1}}'))
            return io.BytesIO(b'{"ok":true,"result":{"message_id":3,"document":{}}}')
        self.upload(opener, delays.append)
        self.assertEqual((len(calls), delays), (2, [1]))
        calls.clear()
        def ambiguous(request, **kwargs):
            calls.append(request)
            raise urllib.error.URLError(request.full_url)
        with self.assertRaisesRegex(RuntimeError, "check delivery") as error:
            self.upload(ambiguous)
        self.assertEqual(len(calls), 1)
        self.assertNotIn("private_token", str(error.exception))


if __name__ == "__main__":
    unittest.main()
