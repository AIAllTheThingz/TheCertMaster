# Deployment Guide

This document describes a practical deployment path for `TheCertMaster`.

## Deployment Model

Recommended production shape:

- ASP.NET Core application hosted on Windows with IIS or as a Kestrel-backed service
- SQL Server database
- production secrets supplied through environment variables or secure host configuration

This repository now also includes two PowerShell 5.1 server-installation scripts intended for a bare Windows Server where IIS, SMTP, and SQL Server Express 2019 are present but the application itself is not yet configured.

Current deployment intent for this environment is to serve the app over both `http` and `https` during the transition from dev/test to production.

## Server Sizing

These are practical baseline recommendations for `Windows Server 2022 + IIS + SQL Server 2019` when the application and SQL Server are running on the same machine.

### Minimum

- `2 vCPU`
- `8 GB RAM`
- `80 GB SSD`
- Windows Server 2022
- IIS
- SQL Server 2019 or SQL Server Express 2019

This is suitable for a small internal deployment, technical preview, or light pilot use.

### Recommended

- `4 vCPU`
- `16 GB RAM`
- `120 GB SSD` or larger
- Windows Server 2022
- IIS
- SQL Server 2019 Standard or better if growth is expected

This is the preferred starting point for a more realistic beta or early production deployment.

### Production Growth

- `4-8 vCPU`
- `16-32 GB RAM`
- `200 GB+ SSD` with room for database growth, uploads, logs, and backups
- consider separating SQL Server from the web server as usage grows
- use SQL Server Standard or higher when Express limits become restrictive

Growth pressure will come more from SQL Server, uploads, quiz history, and operational storage than from the ASP.NET Core application binaries themselves.

## Immutable Deployment Artifact

The tagged CI workflow generates one immutable workflow artifact per release commit, named `TheCertMaster-<commit-sha>.zip`. Download the archive and its checksum from the successful CI run for the intended commit. The workflow artifact contains:

- `artifact-manifest.json`: SHA-256 hashes for every shipped file and the release commit
- `publish\`: prebuilt application output
- `bootstrapper\`: prebuilt Bootstrapper output
- `migrations\efbundle.exe`: prebuilt EF migration bundle
- `scripts\`: prerequisite, install, SMTP, and smoke-test scripts
- `source\`: exact-commit source evidence and `commit-manifest.json`

Verify the archive checksum, `artifact-manifest.json`, and `source\commit-manifest.json` before execution. For a hosted release, verify GitHub artifact provenance with `gh attestation verify` against the trusted repository and expected source digest, for example:

```powershell
gh attestation verify TheCertMaster-<commit-sha>.zip `
  --repo AIAllTheThingz/TheCertMaster `
  --source-digest <commit-sha> `
  --signer-workflow AIAllTheThingz/TheCertMaster/.github/workflows/ci.yml
```

That command verifies the default SLSA provenance predicate. Repeat it with `--predicate-type https://spdx.dev/Document` when verifying the separately attested SPDX SBOM.

The local validation checklist does not claim hosted attestation was run. After verification, extract the archive as a fresh artifact root such as `C:\Deployment`; do not overlay it onto an active release. The server consumes the prebuilt outputs and migration bundle; it does not compile repository source or rebuild the application. Keep `source\` for traceability only.

For deployment servers, use this artifact flow instead of installing Git and cloning the repository onto the server. Use a reviewed sample package when validating quiz import. If the release source evidence includes `samples/`, that copy is traceability evidence; it is not uploaded into the running application as runtime data.

## Simple Step-By-Step Install

Step 1: Fresh install `Windows Server 2022` and fully patch it with Windows Update.

Step 2: Open `Server Manager`.

Step 3: Select `Add Roles and Features`.

Step 4: Under `Server Roles`, select `Web Server (IIS)` and click `Next`.

Step 5: Under `Features`, if `.NET Framework 3.5 Features` is requested, install it.
If Windows asks for an alternate source, point it to:
`D:\sources\sxs`
Use your Windows Server 2022 ISO or media.

Step 6: Finish the Roles and Features wizard and wait for IIS installation to complete.

Step 7: Close `Add Roles and Features`.

Step 8: Download and install `SQL Server Express 2019`.
Official Microsoft page:
[SQL Server 2019](https://www.microsoft.com/en-us/sql-server/sql-server-2019)
Official Microsoft eval/download page:
[SQL Server 2019 Eval Center](https://www.microsoft.com/en-us/evalcenter/evaluate-sql-server-2019)

Step 9: Download and install `SSMS`.
Official Microsoft page:
[SQL Server Management Studio](https://learn.microsoft.com/en-us/ssms/)
Install page:
[Download and install SSMS](https://learn.microsoft.com/en-us/ssms/install/install)

Step 10: Copy the verified `TheCertMaster-<commit-sha>.zip` artifact from the CI run to the server.

Step 11: Verify `artifact-manifest.json`, the archive checksum, and `source\commit-manifest.json`, then extract the archive as a fresh artifact root at:
`C:\Deployment`

Step 12: Open:
`C:\Deployment\scripts\production-settings.template.psd1`

Step 13: Save it as:
`C:\Deployment\scripts\production-settings.psd1`

Step 14: Edit `production-settings.psd1` and set at minimum:
- `PublicBaseUrl`
- `JwtKey`
- `BootstrapAdminEmail`
- `BootstrapAdminPassword`
- `CorsOrigins`

Step 15: Open `PowerShell` as `Administrator`.

Step 16: Run Script 1:
```powershell
powershell.exe -ExecutionPolicy Bypass -File C:\Deployment\scripts\ensure-server-prerequisites.ps1
```

Step 17: Run Script 2:
```powershell
powershell.exe -ExecutionPolicy Bypass -File C:\Deployment\scripts\install-production-application.ps1 -SettingsFile C:\Deployment\scripts\production-settings.psd1
```

Step 18: If SQL blocks database creation, open `SSMS` as a SQL admin and run:
```sql
USE master;
GO
CREATE DATABASE [TheCertMaster];
GO
```

Step 19: If you had to run that SQL command, run Script 2 again:
```powershell
powershell.exe -ExecutionPolicy Bypass -File C:\Deployment\scripts\install-production-application.ps1 -SettingsFile C:\Deployment\scripts\production-settings.psd1
```

Step 20: Optional SMTP setup:
```powershell
$publishedSitePath = (Get-Website -Name 'Default Web Site').physicalPath
powershell.exe -ExecutionPolicy Bypass -File C:\Deployment\scripts\configure-smtp-test.ps1 `
  -DeploymentRoot C:\Deployment `
  -PublishedSitePath $publishedSitePath
```

Step 21: Run the smoke test:
```powershell
powershell.exe -ExecutionPolicy Bypass -File C:\Deployment\scripts\post-deploy-smoke-test.ps1
```

Step 22: Open the site:
[http://localhost/](http://localhost/)
Admin page:
[http://localhost/manage.html](http://localhost/manage.html)

Scripts in order:
1. `ensure-server-prerequisites.ps1`
SQL command: none

2. `install-production-application.ps1`
SQL command: none if database creation succeeds automatically

3. `CREATE DATABASE [TheCertMaster];`
Only if Script 2 fails with `CREATE DATABASE permission denied`

4. `install-production-application.ps1` again
Only if you had to create the database manually

5. `configure-smtp-test.ps1`
Optional

6. `post-deploy-smoke-test.ps1`
Always recommended

Important note:
- I would not bundle the SQL Server or SSMS installers inside the deployment package unless you specifically want an offline install package. The current package is cleaner if it only contains TheCertMaster files and scripts.

## Required Production Configuration

Do not rely on checked-in `appsettings.json` values for production.

Set these values in the target environment:

```text
ConnectionStrings__DefaultConnection=Server=YOURSERVER;Database=YOURDB;User Id=YOURUSER;Password=YOURPASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True;
Jwt__Key=YOUR-LONG-RANDOM-SECRET-AT-LEAST-32-CHARS
Jwt__Issuer=TheCertMaster
Jwt__Audience=TheCertMasterUsers
Cors__AllowedOrigins__0=https://your-production-site.example
PublicApp__BaseUrl=https://your-production-site.example
```

Optional production settings:

```text
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft=Warning
Swagger__Enabled=false
RateLimiting__AuthAttemptsPerMinute=8
RateLimiting__GuestQuizLoadsPerMinute=20
RateLimiting__AuthenticatedQuizLoadsPerMinute=60
```

## Build And Publish

Release builds, the Bootstrapper, the migration bundle, the deployment scripts, and exact-commit source evidence are produced by CI. Deploy the resulting `TheCertMaster-<commit-sha>.zip` archive. Local `dotnet build` and `dotnet publish` commands are development checks only and do not replace the immutable release artifact.

## Bare Server Installation Scripts

These scripts are written for PowerShell `5.1.20348.4294` compatibility and assume Administrator elevation.

Place the deployment package under a root folder named `Deployment`, then run:

1. [ensure-server-prerequisites.ps1](scripts/ensure-server-prerequisites.ps1)
2. [install-production-application.ps1](scripts/install-production-application.ps1)
3. [configure-smtp-test.ps1](scripts/configure-smtp-test.ps1) if you want local SMTP testing wired up immediately
4. Copy and edit [production-settings.template.psd1](scripts/production-settings.template.psd1)

Artifact layout on the target server before installation:

```text
C:\Deployment\
  artifact-manifest.json
  publish\                 prebuilt application output
  bootstrapper\            prebuilt Bootstrapper output
  migrations\efbundle.exe  prebuilt migration bundle
  scripts\
  source\                  exact-commit traceability evidence only
  releases\                created for staged IIS releases
```

### Script 1: Ensure Server Prerequisites

[ensure-server-prerequisites.ps1](scripts/ensure-server-prerequisites.ps1) will:

- install required IIS role services and management tools if missing
- download the current .NET 9 IIS Hosting Bundle from official Microsoft release metadata
- restart IIS so the hosting bundle is active

Example:

```powershell
powershell.exe -ExecutionPolicy Bypass -File C:\Deployment\scripts\ensure-server-prerequisites.ps1
```

### Script 2: Install The Application And Run Migrations

[install-production-application.ps1](scripts/install-production-application.ps1) will:

- validate the root artifact and source manifests before any deployment changes
- consume the prebuilt application from the verified artifact root
- use the prebuilt Bootstrapper and migration bundle from the artifact
- stage the application into a fresh `C:\Deployment\releases\<commit>-<guid>\publish` directory
- preserve `App_Data` and `wwwroot\uploads\images` from the existing IIS site path
- use a dedicated application pool; it rejects a current or requested pool that serves another IIS site
- create/configure the IIS application pool using `No Managed Code`
- point the IIS site root at the deployed publish folder
- bind the IIS site on both HTTP and HTTPS
- write production environment variables into the published `web.config`
- create required runtime folders and assign app pool permissions
- apply the bundled `migrations\efbundle.exe`
- grant the IIS app pool identity database access for local SQL Express

If no certificate thumbprint is supplied, the installer can create a self-signed certificate for the host name derived from `PublicBaseUrl`.

Recommended settings-file workflow:

```powershell
Copy-Item C:\Deployment\scripts\production-settings.template.psd1 C:\Deployment\scripts\production-settings.psd1
notepad C:\Deployment\scripts\production-settings.psd1
powershell.exe -ExecutionPolicy Bypass -File C:\Deployment\scripts\install-production-application.ps1 `
  -SettingsFile C:\Deployment\scripts\production-settings.psd1
```

The installer reads the PowerShell data file and uses those values as defaults. Any directly passed installer parameters still win if you need to override a value for one run. Run it from the `scripts` directory in the extracted artifact; it does not compile or publish source. If migration is attempted and fails, the installer stops IIS and leaves it stopped for manual database/application compatibility recovery.

Template file:

- [production-settings.template.psd1](scripts/production-settings.template.psd1)

The checked-in template now defaults to a local server profile:

- `PublicBaseUrl = 'http://thecertmaster.local'`
- `BootstrapAdminEmail = 'admin@thecertmaster.local'`
- `CorsOrigins = @('http://thecertmaster.local', 'https://thecertmaster.local')`

That gives you a consistent starting point for an internal server or first production-style install.

Minimal required values to edit before production use:

- `PublicBaseUrl`
- `JwtKey`
- `BootstrapAdminEmail`
- `BootstrapAdminPassword`
- `DatabaseName` if you do not want the default
- `ConnectionString` if you are not using the default local SQL Express pattern
- `CorsOrigins` if more than the public base URL should be allowed
- `CertificateThumbprint` if you want to use a real server certificate instead of a generated self-signed certificate

HTTP and HTTPS notes:

- the installer configures IIS for both `http` and `https`
- the application-level `HttpsRedirection` setting is left disabled by default so both schemes stay usable during this phase
- once you are ready to force secure traffic, set `EnableHttpsRedirection = $true` in the settings file and rerun the installer

Bootstrap admin notes:

- production does not create the development admin account
- the installer now creates the first admin account intentionally during installation
- `BootstrapAdminEmail` is not automatically derived from `PublicBaseUrl`
- you should usually make them match the same host naming convention for clarity

Recommended example:

```powershell
PublicBaseUrl = 'http://thecertmaster.local'
BootstrapAdminEmail = 'admin@thecertmaster.local'
CorsOrigins = @(
    'http://thecertmaster.local',
    'https://thecertmaster.local'
)
```

What each setting means:

- `PublicBaseUrl` is the canonical URL the app uses for generated links, deployment identity, and HTTPS host inference
- `BootstrapAdminEmail` is simply the email address of the first admin account the installer creates
- `CorsOrigins` should include every browser origin that is allowed to call the API

If you use `thecertmaster.local`, make sure that host name resolves on the server and on any client machine that will browse the site. That usually means a DNS entry or a local `hosts` file entry.

If you leave `PublicBaseUrl = 'http://localhost'` and also set `BootstrapAdminEmail = 'admin@localhost'`, the installer will create `admin@localhost`. If you want the login to be `admin@thecertmaster.local`, set that email explicitly before running the installer.

Default installation assumptions:

- IIS site name: `Default Web Site`
- application pool name: `TheCertMaster`
- the application pool must be dedicated to this site; do not reuse a pool serving another IIS site
- SQL instance: `.\SQLEXPRESS`
- database name: `TheCertMaster`
- deployment root: `C:\Deployment`

If your production host needs different names or paths, override the script parameters.

### Script 3: Configure SMTP For Application Testing

[configure-smtp-test.ps1](scripts/configure-smtp-test.ps1) will:

- verify the Windows SMTP service is installed
- start `IISADMIN` and `SMTPSVC` if needed
- write `App_Data\smtp_settings.json` under the explicitly selected published site path for TheCertMaster
- default the sender address from `PublicBaseUrl` when a settings file is present
- optionally send a direct SMTP test email

The script is meant for local application SMTP testing after the install completes. It does not replace real production relay hardening, but it gives the app a working SMTP profile quickly.

Example:

```powershell
$publishedSitePath = (Get-Website -Name 'Default Web Site').physicalPath
powershell.exe -ExecutionPolicy Bypass -File C:\Deployment\scripts\configure-smtp-test.ps1 `
  -DeploymentRoot C:\Deployment `
  -PublishedSitePath $publishedSitePath `
  -TestRecipientEmail you@example.com
```

How sender defaults are chosen:

- if `production-settings.psd1` exists and `PublicBaseUrl = 'http://thecertmaster.local'`, the script defaults `FromEmail` to `no-reply@thecertmaster.local`
- if no host can be derived, it falls back to `BootstrapAdminEmail`
- if neither is available, it falls back to `no-reply@localhost`

This keeps SMTP testing aligned with the same hostname pattern used by `PublicBaseUrl` and `BootstrapAdminEmail`.

Important note about external email tests:

- this script configures TheCertMaster to talk to the local Windows SMTP service
- it does not fully configure Windows SMTP relay rules, smart-host routing, or outbound mail policy for your network
- sending to an external address such as Gmail can still fail if the server cannot relay outbound mail on port `25`
- on our server test, Windows SMTP accepted configuration but `inetinfo.exe` crashed inside `SMTPSVC.dll` during send attempts, so local Windows SMTP should be treated as optional testing infrastructure, not the preferred production delivery path

So a successful script run means:

- TheCertMaster now has SMTP settings written for application testing
- the Windows SMTP service is installed and running

It does not automatically guarantee:

- external mail delivery to providers like Gmail
- relay permission for anonymous local submissions
- smart-host forwarding through your organization or ISP

Recommended production SMTP path:

- use a real authenticated SMTP relay instead of relying on local anonymous relay
- prefer a provider such as Microsoft 365, SendGrid, SMTP2GO, or another managed relay
- use port `587` with `-UseStartTls:$true` when the provider requires STARTTLS
- set `FromEmail` to a real mailbox or approved sender address for that provider

Example for an authenticated relay:

```powershell
powershell.exe -ExecutionPolicy Bypass -File C:\Deployment\scripts\configure-smtp-test.ps1 `
  -DeploymentRoot C:\Deployment `
  -PublishedSitePath $publishedSitePath `
  -SmtpHost smtp.office365.com `
  -SmtpPort 587 `
  -Username your-real-mailbox@yourdomain.com `
  -Password 'your-real-password-or-app-password' `
  -UseStartTls:$true `
  -FromEmail your-real-mailbox@yourdomain.com `
  -FromName 'TheCertMaster' `
  -TestRecipientEmail you@example.com
```

Practical guidance:

- use the local Windows SMTP service only if you specifically want local relay testing and you have verified the service is stable on that host
- for real verification emails and password reset delivery, an authenticated external SMTP relay is the safer default
- if the SMTP provider returns `5.7.3 STARTTLS is required`, rerun the script with `-UseStartTls:$true`

## Database

The application automatically migrates the database only in development.

For production:

1. Verify the immutable release artifact checksum, artifact manifest, source commit manifest, and hosted attestation where available
2. Set production environment variables
3. Apply the bundled migration executable deliberately before switching traffic to the new app build

The release artifact contains the prebuilt migration bundle. The production installer sets the required production environment values and invokes this executable from the verified artifact:

```powershell
& 'C:\Deployment\migrations\efbundle.exe' --connection "$env:ConnectionStrings__DefaultConnection"
if ($LASTEXITCODE -ne 0) { throw 'The bundled EF Core migration failed.' }
```

Run that only after setting the production connection, JWT issuer/audience/key, and public base URL environment values in a controlled deployment step. The production installation script invokes the same bundled executable. The target server does not need the .NET SDK or `dotnet-ef`, and it does not compile repository source.

Migration expectations:

- do not depend on startup to mutate the production database
- run migrations as a deliberate release step
- confirm the target environment is pointing at the intended database before applying migrations
- if the release contains schema changes, do not mark the deployment complete until the migration succeeds
- if migration fails after IIS was stopped, leave IIS stopped and complete database/application compatibility recovery manually before starting the site

## Rollback And Recovery

Before replacing a live release, back up the database and copy the current `App_Data` (including Data Protection keys) and `wwwroot\uploads\images` directories. Extract each verified archive into a fresh release root and switch IIS only after manifest, migration, and smoke checks pass. Never overwrite the active publish directory in place. If a migration has run, restoring the prior application binaries alone may not be schema-compatible; use the database recovery plan and the prior verified artifact together. The installer does not automatically roll back a failed migration.

`Program.cs` protects the stored SMTP password with ASP.NET Core Data Protection using `ProtectKeysWithDpapi(protectToLocalMachine: true)`, which ties the encryption key to the specific machine that wrote it. Restoring `App_Data` (including `smtp_settings.json`) onto **new or replacement hardware** will not carry over a usable SMTP password: the app cannot decrypt it on the new machine and treats it as blank, silently disabling outbound email until reconfigured. After a restore to new hardware, re-enter the SMTP credentials (e.g. via `configure-smtp-test.ps1` or the admin SMTP settings page) rather than assuming the restored `App_Data` is sufficient.

## ASPNETCORE_ENVIRONMENT

Set:

```text
ASPNETCORE_ENVIRONMENT=Production
```

Do not run production as `Development`.

## Public Files And Uploads

Be aware of runtime storage:

- imported CSV/ZIP files are stored under `App_Data/uploads`
- extracted package images are served from `wwwroot/uploads/images`
- data protection keys are stored under `App_Data/keys`

These runtime folders should be persisted appropriately on the host and not treated as source-controlled assets.

## Security Notes

Production behavior now includes:

- rate limits on public authentication-sensitive endpoints
- separate quiz-load rate limits for guests and authenticated users
- automatic admin-session rejection in the browser when a token expires or lacks the `Admin` role
- structured logs for auth failures, rate-limit rejections, password-reset activity, SMTP changes, and admin management actions

If you tighten or relax rate limits in production, record those settings alongside the deployment so operators know the expected thresholds.

Monitoring surfaces now available:

- `/health/live`: process liveness
- `/health/ready`: app + database readiness
- `/health`: combined health payload
- `/version`: app/environment metadata for quick verification

Unhandled API failures return a trace id in the response body. That trace id should be included in any support or operator notes so the matching log entry can be found quickly.

## Pre-Deployment Checklist

- production connection string is set
- JWT key is set and strong
- CORS origins are correct
- SMTP settings are configured if email features are needed
- rate-limiting values are confirmed for the target environment
- `PublicApp__BaseUrl` is set correctly for email verification links
- database migration has been applied
- sample/dev credentials are not relied on in production
- `ASPNETCORE_ENVIRONMENT` is set to `Production`
- `/health` is reachable from the deployment target
- `/health/live` and `/health/ready` are reachable from the deployment target
- runtime folders for `App_Data` and `wwwroot/uploads/images` are writable
- the release notes and changelog match the code being deployed
- the deployment artifact includes `artifact-manifest.json`, `publish`, `bootstrapper`, `migrations\efbundle.exe`, scripts, and exact-commit source evidence
- the artifact checksum, artifact manifest, and `source\commit-manifest.json` match the intended release commit
- hosted provenance and SBOM attestations are verified with the trusted repository and source digest when the artifact came from GitHub

## Release Checklist

- run a release build
- build and test the Bootstrapper
- run the integration tests
- restore in locked mode and review dependency vulnerability output
- generate and inspect the immutable deployment archive and SBOM/provenance outputs
- apply the bundled migration in a controlled step
- smoke test `/`, `/swapi.html`, and `/health`
- smoke test `/health/live`, `/health/ready`, and `/version`
- verify admin login and a sample quiz import
- verify public login / reset / resend flows return sensible `429` messages under repeated requests
- publish the matching Git tag and GitHub release notes

Recommended automated smoke test:

```powershell
pwsh .\scripts\post-deploy-smoke-test.ps1 -BaseUrl https://your-production-site.example
```

## Post-Deployment Smoke Test

1. Open the landing page
2. Verify `/health/live` returns success
3. Verify `/health/ready` returns success
4. Verify `/version` returns the expected environment
5. Verify `/swapi.html` loads only if Swagger was intentionally enabled
6. Test admin login
7. Test quiz list retrieval
8. Upload and import a sample quiz package
9. Open a quiz and confirm images render
