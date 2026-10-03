# Project guidance

## Development and release

- Work directly on `dev` for routine changes. Verify, commit and push to `origin/dev`; create a feature branch or PR only when the user explicitly requests it.
- Preserve existing work. Do not force-push routine changes or publish a release unless requested.
- Release integration remains `dev` to `main` through Rebase and merge, with the required `Windows x64` check. Follow `docs/WORKFLOW.md` for branch alignment and tags.

## Product identity

- 机耀处 and LumaDesk are the same product. Use only `机耀处` for the main window title and Chinese display name.
- The desktop executable is `LumaDesk.exe`; its managed assembly, resource index and runtime configuration use `LumaDesk` too.
- Keep existing JiYaoChu namespaces, data directories, native DLL and CLI names compatible unless their migration is explicitly requested.

## Verification

- Follow the locked toolchains and build steps in `docs/BUILD.md`.
- For executable or packaging changes, build the ZIP and installer and run `scripts/verify-package.py` with both artifacts. Verify startup and UI using the isolated mock backend before reporting success.
- Keep physical hardware verification separate from mock and CI results; record pending device checks in `docs/VALIDATION.md`.

## Repository skills and plugins

- Project skills live in `.agents/skills`; their provenance is documented in `.agents/README.md`.
- Use `winui-app` for WinUI changes and `gh-fix-ci` when investigating CI failures. Use `gh-address-comments` for requested PR review work and `security-threat-model` only for explicitly requested threat modeling.
- Existing repository conventions and explicit user instructions take priority over generic skill guidance.
- Plugins are installed per Codex account, not distributed with the application. Never store connector credentials in this repository or report an unconfirmed plugin installation as complete.
