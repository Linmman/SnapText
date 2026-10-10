# Contributing

Thanks for wanting to help. SnapText is deliberately small and narrow — the fastest way to get a change merged is to respect that.

## The three design rules

Every new capability has to satisfy all three, or it does not ship:

1. **Zero configuration by default** — install and use; no switch to flip, no engine to pick, no rules to author.
2. **Zero added steps** — the main flow is always "hotkey → drag".
3. **No new interrupting UI** — reuse the existing toast; never a dialog, never a new window.

Details, plus what is explicitly out of scope, are in [docs/Development.md](docs/Development.md). The reasoning behind the architecture is in [docs/Architecture.md](docs/Architecture.md).

## Reporting a bug

Please include:

- what you did, what you expected, what happened instead;
- `winver` output, monitor count and display scale (e.g. 150 %), SnapText version, and the OCR engine you selected;
- the tail of `%LOCALAPPDATA%\SnapText\logs\app-*.log`.

> ⚠️ **Read the log before posting it.** The log can contain fragments of the text you grabbed — redact anything private.

Known non-bugs: on a locked desktop no hotkey and no clipboard access works (Windows blocks both), and HDR / exclusive-fullscreen capture is a documented limitation.

## Suggesting a feature

Lead with the scenario: what were you doing, and why did the current flow annoy you? Then the idea.
If your suggestion needs a new setting or an extra step, explain why it is worth breaking rule 1 or 2 — otherwise expect a smaller variant of it to be built instead.

## Building and testing

```powershell
dotnet build   src\SnapText\SnapText.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke-pipeline.ps1   # content-agnostic smoke test
```

Requires the .NET 10 SDK. `docs/Development.md` lists every test script and what it needs (some require an unlocked desktop). Please run `smoke-pipeline.ps1` before opening a PR and paste the result in the description.

## Code style

- Match the file you are editing. Existing comments are in Chinese; keep one language per file (English comments in new files are fine).
- One feature or fix per PR, kept small.
- New dependencies need a sentence in the PR explaining why — download size and license both matter for a portable app.

## Commit messages

`type: summary`, with `type` one of `feat` / `fix` / `docs` / `chore` / `refactor`. English or Chinese are both fine.
Example: `fix: 带「打开链接」的提示停留 6 秒（原来 2.6 秒来不及点击）`

## Documentation and translations

- `README.md` (English) is the source of truth; then sync [README.zh-CN.md](README.zh-CN.md) and [README.ja.md](README.ja.md). If a translation lags, keep the "English version is canonical" note rather than leaving half-translated content.
- Author-facing material (build, test scripts, verification records, project layout) belongs in `docs/`, **not** in the README — the README is for users who just want to install and use the tool.
- The other docs are Chinese-only today. Translating [docs/Troubleshooting.md](docs/Troubleshooting.md) would be a genuinely useful first PR.

## License

By contributing you agree that your work is released under the MIT license — see [LICENSE](LICENSE).
