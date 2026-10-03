# Project development skills

The following official OpenAI skills are installed under `skills/`, the repository scope supported by Codex:

| Skill | Purpose |
| --- | --- |
| `winui-app` | WinUI 3 development, window behavior and build/startup verification |
| `gh-fix-ci` | Diagnose GitHub Actions failures and implement requested fixes |
| `gh-address-comments` | Address requested PR review comments |
| `security-threat-model` | Model this repository's threats when explicitly requested |

Source: [openai/skills](https://github.com/openai/skills/tree/main/skills/.curated). Installed on 2026-10-03 with the bundled `skill-installer`; retain upstream instructions and assets. Upstream license information accompanies this directory. Review upstream changes before updating these copies.

One whitespace-only line in the upstream threat-model prompt template was normalized for the repository's whitespace checks.

Codex discovers `.agents/skills` for this project automatically; restart Codex if a newly installed skill does not appear. See [official skill documentation](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills).

The GitHub and Codex Security plugins are installed and enabled in the current Codex account. Plugin connection state and credentials are local to the account and are not part of the application or these vendored skills.
