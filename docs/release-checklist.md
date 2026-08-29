# Release Checklist

Use this short checklist before pushing a tagged release.

## Before Push

- update [CHANGELOG.md](../CHANGELOG.md)
- update the release notes file under [docs/releases](./releases)
- run `dotnet restore TheCertMaster.sln --locked-mode`
- run `dotnet build TheCertMaster.sln -c Release --no-restore`
- build and test `TheCertMaster.Bootstrapper`
- run `dotnet test TheCertMaster.Tests\TheCertMaster.Tests.csproj -c Release --no-build`
- verify [README.md](../README.md) reflects any new user-facing setup changes
- verify [DEPLOYMENT.md](../DEPLOYMENT.md) still matches production expectations
- verify the generated `TheCertMaster-<commit-sha>.zip` contains the prebuilt `publish`, `bootstrapper`, `migrations`, scripts, root `artifact-manifest.json`, and exact-commit `source/commit-manifest.json` paths required by the installer
- verify the archive checksum and both manifests identify the same release commit
- for a GitHub workflow artifact, verify SLSA provenance with `gh attestation verify` using `--repo AIAllTheThingz/TheCertMaster`, the expected `--source-digest`, and the workflow signer; repeat with `--predicate-type https://spdx.dev/Document` for the separately attested SPDX SBOM
- verify rate-limit guidance still matches the configured app defaults
- verify [external-user-testing.md](./external-user-testing.md) still matches the current flows

## Before Tagging

- confirm the working tree is clean
- confirm sample/dev-only changes are not leaking into production defaults
- confirm JWT and connection string guidance is still accurate
- confirm the target install uses the prebuilt archive and migration bundle without compiling repository source, and stages IIS under a fresh `releases/<commit>-<guid>/publish` path
- confirm the target uses a dedicated IIS application pool; the installer rejects a pool shared with another site
- confirm security logging and rate-limit behavior still match the current release
- confirm `/health/live`, `/health/ready`, `/health`, and `/version` return the expected payloads
- confirm the sample package and import docs still work end to end

## Before Publishing The GitHub Release

- push `main`
- push the release tag
- paste the release notes from the matching file in [docs/releases](./releases)
- call out known limitations honestly

## Quick Smoke Test

- open `/`
- open `/swapi.html`
- verify `/health`
- verify `/health/live`
- verify `/health/ready`
- verify `/version`
- log in through [manage.html](../wwwroot/manage.html)
- verify stale or non-admin sessions are rejected cleanly by [manage.html](../wwwroot/manage.html)
- upload and import a sample package
- open [quiz.html](../wwwroot/quiz.html) and verify quiz images render
- confirm the quiz setup summary reflects the selected category count and question count
