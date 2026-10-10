# Security Policy / 安全策略

SnapText is a fully local, offline tool. It never connects to the network and never uploads anything: captures, recognized text and history stay on this machine (`%LOCALAPPDATA%\SnapText`, plus `Pictures\SnapText` if you enable saving shots).

SnapText 是完全本地离线的工具：不联网、不上传任何数据。截图、识别出的文字与历史只存放在本机（`%LOCALAPPDATA%\SnapText`；若开启保存截图，还有 `Pictures\SnapText`）。

## Reporting a Vulnerability / 报告漏洞

Please report privately via [GitHub Security Advisories](https://github.com/Linmman/SnapText/security/advisories/new) instead of a public issue.

请通过 [GitHub Security Advisories](https://github.com/Linmman/SnapText/security/advisories/new) 私下报告，不要先开公开 issue。

## Notes / 说明

- `SnapText.exe --grab` triggers a grab in the running instance via a named event; the event is only accessible to the current Windows user.
- `--grab` 通过命名事件触发正在运行的实例取字；该事件仅当前 Windows 用户可访问。
- `history.jsonl` and logs store text in **plaintext** — keep that in mind before sharing them.
- `history.jsonl` 与日志中的文字为**明文**存储，对外分享前请自行斟酌。
