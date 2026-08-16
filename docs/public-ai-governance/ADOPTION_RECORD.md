# Public-AI-Governance downstream adoption record

## Pilot identity

- Downstream repository: `AIAllTheThingz/TheCertMaster`
- Pilot shape: application/service software
- Downstream source revision before adoption: `4d93d1193a7a5f2c314726197b8c8198e5f37190`
- Public Access Agents release: `v0.10.0`
- Public Access Agents source commit: `83c73f3ab9a049ff2321d463164fcf98fb453a9c`
- Adoption date: 2026-08-16

## Selected composition

- Profile: `WEB_APPLICATION`
- Languages: `csharp`, `dotnet`
- Framework: `aspnet-core`
- Operating-system boundary: `windows-server`
- Generated disciplines: `application-security`, `architecture`, `testing`, `accessibility`, `privacy`, `observability`, `ci-cd`, `supply-chain`, `release-engineering`, `api-engineering`, `database`, `integration`, `sre`, `documentation`

The exact generated manifest is `project-manifest.json`. The traceable composition is under `standards-bundle/`; upstream standards were not flattened into this repository.

## Commands exercised

### Generic pilot runner

- Published standards checkout at `v0.10.0`: **Passed**
- `tools/generate-manifest/generate_manifest.py`: **Passed**
- `tools/compose-agents/compose_agents.py --no-copy-sources`: **Passed**
- `dotnet --info`: **Passed**
- `dotnet restore`: **Passed**
- Release build: **Passed**
- Deployment PowerShell parser pass under `pwsh`: **Passed**
- `dotnet test`: **Failed** because the generic pilot runner omitted this repository's SQL Server test service and `TEST_SQLSERVER_BASE_CONNECTION`

### Project-native CI prerequisites

The pilot then reran the repository's normal validation model using SQL Server 2022 and the same test connection contract declared in `.github/workflows/ci.yml`:

- project CI restore: **Passed**
- project CI build: **Passed**
- project CI test suite: **Passed**

Detailed command output and return-code evidence are retained beside this record.

## Adoption outcome

The published standards could be selected and composed without repository-author context. A real root `AGENTS.md` and a tighter `Deployment/AGENTS.md` were tailored from actual repository facts so browser/API application work and privileged deployment work have different instruction scopes.

The generated profile composition was broad but appropriate for a real ASP.NET Core application: it included security, architecture, testing, accessibility, privacy, observability, CI/CD, supply chain, release, API, database, integration, SRE, and documentation overlays.

## Friction and findings

1. **Adopter-specific test infrastructure:** a generic `dotnet test` invocation was insufficient because the project integration/flow suite requires SQL Server and `TEST_SQLSERVER_BASE_CONNECTION`. Once the pilot used the repository's actual CI prerequisites, the same project restore/build/tests passed. This is project-specific tailoring, not an upstream standards defect.
2. **Adopter-specific runtime facts:** the manifest can select C#/.NET, ASP.NET Core, Windows Server, and disciplines, but it correctly does not infer the project's exact .NET 9, ASP.NET Core 9, SQL Server, IIS, JWT, SMTP, upload/import, and authentication boundaries. Those facts remain in downstream instructions.
3. **No upstream selection/composition defect identified:** manifest generation and traceable composition completed without schema, path, or package-slug failure.

## Limitations

- No IIS deployment, production SQL migration, certificate validation, SMTP delivery, browser accessibility exercise, external identity integration, or production authorization test was executed.
- The initial generic test failure is retained as Failed evidence because the command actually ran; the successful project-native rerun explains the missing prerequisite rather than erasing that result.
- Successful project CI and standards composition do not certify security, compliance, deployment readiness, or production readiness.
- This pilot does not by itself satisfy stable-maturity promotion requirements.

## Follow-up

No focused upstream defect was created from this pilot because the observed friction is project-specific CI/environment tailoring already expected by the standards model. This record is suitable as the application/service downstream adoption input for Public-AI-Governance issue #41 and later maturity review.
