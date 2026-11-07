# Changelog

All notable changes to this repository are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project does not yet use semantic versions. Until a release is tagged, changes live under Unreleased. Older behavior is in git history and is not restated here as a fabricated release.

## [Unreleased]

### Added

- Engineering documentation under `docs/`, including architecture, system design, decisions, database, API, security, testing, deployment, observability, and roadmap.
- Architecture decision records in `docs/adr/` that describe the implementation as it exists. They are not backdated design minutes.
- Repository guidance: `CONTRIBUTING.md`, root `SECURITY.md`, `.editorconfig`, and `.gitattributes`.
- GitHub issue and pull request templates.
- A GitHub Actions workflow that restores, builds, tests, and uploads test and coverage artifacts.

### Changed

- Rewrote `README.md` so it describes the scan API that the source implements.
- Replaced the placeholder copyright line in `LICENSE.txt` with the author named by the repository.

### Fixed

- None. This documentation pass does not change scan behavior, authentication, or persistence.
