# TheCertMaster Agent Instructions

## Adopted engineering baseline

This repository adopts **Public Access Agents `v0.10.0`** from `AIAllTheThingz/Public-AI-Governance`, source commit `83c73f3ab9a049ff2321d463164fcf98fb453a9c`, as an external engineering baseline. The generated manifest, traceable standards composition, and adoption evidence are under `docs/public-ai-governance/`.

Repository facts and explicit project requirements remain authoritative. The external baseline strengthens engineering discipline; it does not invent project facts or authorize production actions.

### Mandatory pinned upstream extensions

The local `AGENTS.md` hierarchy is the composition layer that makes the external baseline applicable to repository work. The files under `docs/public-ai-governance/standards-bundle/` are a traceable index and hash record; because the pilot deliberately used `--no-copy-sources`, their presence alone does **not** make upstream instructions active for application files.

Before modifying code, tests, configuration, deployment automation, or documentation, an agent must:

1. Read `docs/public-ai-governance/standards-bundle/COMPOSITION_MANIFEST.json`.
2. Retrieve and read every upstream source path listed in that manifest from **exactly** `AIAllTheThingz/Public-AI-Governance` at tag `v0.10.0` / source commit `83c73f3ab9a049ff2321d463164fcf98fb453a9c`.
3. For each selected upstream `AGENTS.md`, follow and read its referenced supporting standards from that same exact pinned release when they apply to the changed scope.
4. Apply those pinned upstream requirements together with this repository's project facts and any more-specific local `AGENTS.md` instructions.
5. If the exact pinned source or a required transitive supporting standard cannot be obtained or read, record the external-baseline review as **`Blocked`** and do not claim that Public Access Agents was applied to the change.

Do not substitute `main`, a newer tag, cached prose from another repository, or the generated composition index for the exact pinned source without an explicit adoption update and review.

## Project facts

- ASP.NET Core 9 / .NET 9 web application and API.
- C# application code with Entity Framework Core 9 and SQL Server.
- ASP.NET Core Identity plus JWT bearer authentication.
- Browser-delivered public, account, quiz, and administrative pages.
- Windows/IIS-oriented deployment tooling is tracked under `Deployment/`.
- The repository contains configuration, upload/import, SMTP, authentication, database, and deployment trust boundaries.

## Working rules

- Preserve the declared .NET 9 / ASP.NET Core 9 boundary unless migration is explicitly in scope.
- Treat authentication, authorization, JWT signing, email verification, upload/import paths, SQL access, SMTP configuration, CORS, and administrative endpoints as security-sensitive.
- Keep secrets and environment-specific credentials out of source, logs, examples, and generated evidence.
- Validate authorization server-side. Browser state or hidden UI controls are not authorization.
- Treat database schema, public API behavior, configuration keys, deployment paths, and externally consumed DTOs as compatibility surfaces.
- Prefer focused changes. Do not combine unrelated cleanup with behavioral or deployment work.
- Record validation that actually ran. Missing environment-dependent checks are `NotRun` or `Blocked`, never implied passing.
- Do not claim production readiness from a successful build, test run, or repository-level standards composition.

## Validation baseline

Use the repository's actual supported commands and preserve its test prerequisites:

```powershell
dotnet restore TheCertMaster.sln
dotnet build TheCertMaster.sln --no-restore
dotnet test TheCertMaster.Tests/TheCertMaster.Tests.csproj --no-build --verbosity normal
```

The integration/flow tests require the repository's configured SQL Server test service and `TEST_SQLSERVER_BASE_CONNECTION`. A test failure caused by omitting that prerequisite is an environment/setup failure, not application behavior evidence.

Add migration, database, browser, authorization, upload/import, deployment, and security checks when the changed scope requires them.

## Precedence

More-specific `AGENTS.md` files may strengthen these requirements for their subtree. They must not silently weaken security, validation, evidence, or authorization requirements.
