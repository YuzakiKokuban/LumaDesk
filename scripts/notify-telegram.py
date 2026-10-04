"""Send one checksum-verified installer; credentials are read only from the environment."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
from version import Version

spec = importlib.util.spec_from_file_location("release_assets", Path(__file__).with_name("verify-release-assets.py"))
assets = importlib.util.module_from_spec(spec)
spec.loader.exec_module(assets)


def prepare(directory, channel, context, changes=""):
    installers = list(directory.glob("LumaDesk-*-win-x64-Setup.exe"))
    if len(installers) != 1:
        raise ValueError("Expected exactly one LumaDesk installer")
    installer = installers[0]
    match = re.fullmatch(r"LumaDesk-(.+)-win-x64-Setup\.exe", installer.name)
    info = Version.parse(match[1])
    assets.verify(directory, info)
    if installer.stat().st_size > 50_000_000:
        raise ValueError("Installer exceeds the Telegram Bot API upload limit")
    sha = context["GITHUB_SHA"]
    repository = context["GITHUB_REPOSITORY"]
    run_id = context["GITHUB_RUN_ID"]
    if not re.fullmatch(r"[0-9a-f]{40}", sha) or not re.fullmatch(r"[\w.-]+/[\w.-]+", repository) or not run_id.isdigit():
        raise ValueError("Invalid GitHub delivery provenance")
    ref = context["GITHUB_REF"]
    if channel == "dev":
        if ref != "refs/heads/dev":
            raise ValueError("Development delivery requires dev")
        label = "日常构建 🌱 · dev"
        filename = installer.name.replace("-win-x64", f"-dev-{sha[:8]}-win-x64")
        link = f"https://github.com/{repository}/actions/runs/{run_id}"
    elif channel in ("prerelease", "stable"):
        tag = "v" + info.version
        if ref != "refs/tags/" + tag or info.prerelease != (channel == "prerelease"):
            raise ValueError("Release delivery must match its exact tag and stage")
        label = "预发布 🧪" if info.prerelease else "正式发布 🌾"
        filename = installer.name
        link = f"https://github.com/{repository}/releases/tag/{tag}"
    else:
        raise ValueError("Unknown delivery channel")
    digest = hashlib.sha256(installer.read_bytes()).hexdigest()
    caption = f"机耀处 · LumaDesk\n{label}\n版本：{info.version}\n提交：{sha[:8]}\nSHA256：{digest}\n{link}"
    if changes:
        caption += "\n\n最近提交：\n" + changes.strip()[:max(0, 1000 - len(caption) - 12)]
    return installer, filename, caption


def multipart(installer, filename, caption, chat_id):
    boundary = "LumaDesk-" + uuid.uuid4().hex
    body = bytearray()
    for name, value in (("chat_id", chat_id), ("caption", caption)):
        body.extend(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode("utf-8"))
    body.extend(f'--{boundary}\r\nContent-Disposition: form-data; name="document"; filename="{filename}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode())
    body.extend(installer.read_bytes())
    body.extend(f"\r\n--{boundary}--\r\n".encode())
    return bytes(body), "multipart/form-data; boundary=" + boundary


def send(installer, filename, caption, token, chat_id, opener=urllib.request.urlopen, sleep=time.sleep):
    if not re.fullmatch(r"\d+:[A-Za-z0-9_-]+", token) or not re.fullmatch(r"(?:-?\d+|@[A-Za-z0-9_]+)", chat_id):
        raise ValueError("Missing or invalid Telegram credentials")
    body, content_type = multipart(installer, filename, caption, chat_id)
    request = urllib.request.Request("https://api.telegram.org/bot" + token + "/sendDocument", data=body,
                                     headers={"Content-Type": content_type}, method="POST")
    for attempt in range(3):
        try:
            with opener(request, timeout=120) as response:
                result = json.load(response)
        except urllib.error.HTTPError as error:
            # Never print an exception or request URL: both may include the bot token.
            try:
                result = json.loads(error.read())
            except (ValueError, OSError):
                raise RuntimeError("Telegram returned an invalid HTTP response") from None
        except (OSError, ValueError):
            # An ambiguous transport failure must not resend a possibly delivered file.
            raise RuntimeError("Telegram upload failed; check delivery before rerunning") from None
        if result.get("ok") is True:
            message = result.get("result", {})
            if not isinstance(message.get("message_id"), int) or "document" not in message:
                raise RuntimeError("Telegram response did not confirm document delivery")
            return message
        delay = result.get("parameters", {}).get("retry_after")
        if result.get("error_code") == 429 and isinstance(delay, int) and 0 < delay <= 60 and attempt < 2:
            sleep(delay)
            continue
        # API descriptions can contain user-controlled text; only log the numeric code.
        raise RuntimeError(f"Telegram rejected the upload (code {result.get('error_code', 'unknown')})")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--channel", choices=("dev", "prerelease", "stable"), required=True)
    args = parser.parse_args()
    changes = subprocess.run(["git", "log", "-3", "--format=%h %s"], capture_output=True, text=True, encoding="utf-8", check=True).stdout
    installer, filename, caption = prepare(args.directory, args.channel, os.environ, changes)
    message = send(installer, filename, caption, os.environ.get("TELEGRAM_BOT_TOKEN", ""), os.environ.get("TELEGRAM_CHAT_ID", ""))
    username = message.get("chat", {}).get("username", "")
    link = f"https://t.me/{username}/{message['message_id']}" if re.fullmatch(r"[A-Za-z0-9_]+", username) else f"message {message['message_id']}"
    summary = f"Delivered {filename}: {link}"
    print(summary)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as report:
            report.write(summary + "\n")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, KeyError, OSError, subprocess.SubprocessError) as error:
        # Only our sanitized validation/delivery errors reach the CI log.
        print(f"Telegram delivery failed: {error}" if isinstance(error, (ValueError, RuntimeError)) else "Telegram delivery failed: invalid input or local process failure", file=sys.stderr)
        sys.exit(1)
