# Changelog

All notable changes to this project. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-11-22

First version.

### Added
- `StartUninstall.exe`: a windowless Native AOT helper that reacts to Windows' own **Uninstall** item in Start (pinned tiles, All apps and Start search) for desktop apps: it finds the app's uninstall entry in the registry, closes the Settings page Windows opened and starts the app's uninstaller. With no single clear match it leaves Settings open on Installed apps, filtered to the app's name. Store apps are left to Windows.
- Event-driven through UI Automation (focus changes, Invoked, window opened); no polling. Single instance; `--quit` stops the running copy.
- Decision log in `%LOCALAPPDATA%\StartUninstall\log.txt` (rolls over at 1 MB); `STARTUNINSTALL_DEBUG=1` adds every relevant focus change.
- `install.ps1` / `uninstall.ps1`: build, install to `%LOCALAPPDATA%\Programs\StartUninstall`, start with Windows, Start menu shortcut.
- `bootstrap.ps1`: one-line install from the latest release (`irm .../bootstrap.ps1 | iex`), with SHA-256 check, `-Uninstall`, `-Purge`, `-NoAutostart`, `-Tag`. Merging a version bump publishes the release automatically.
- Core library: ProductCode matching (MSI advertised shortcuts), Start tile id and search result parsing, the localized "Uninstall" names.
