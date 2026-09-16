# Release Plan

## Beta Qualification Target: v0.2.0-beta

`v0.2.0-beta` should mean the application is ready for broader external testing with a stable core workflow.

## Required For v0.2.0-beta

### Release Workflow

- `CHANGELOG.md` is maintained for each tagged release
- GitHub release notes exist for the beta tag
- release scope and known limitations are documented
- CI produces one immutable deployment archive tied to the tagged commit
- the archive includes the prebuilt application, Bootstrapper, migration bundle, scripts, root artifact manifest, and exact-commit source evidence
- deployment verifies archive checksum, manifests, and hosted provenance/SBOM attestations before execution

### Quality

- integration tests for auth, quiz, and import flows remain passing
- critical import/image regressions are covered by tests
- manual smoke testing is completed on the tagged build

### Import UX

- ZIP package workflow is clearly explained in the admin UI
- import errors are understandable to a non-developer admin user
- file listing and import actions remain consistent with actual storage behavior

### Production Safety

- production secrets are not committed
- startup fails fast on missing production configuration
- deployment steps consume the immutable CI archive without compiling source on the target server
- IIS stages each release under a fresh commit/GUID path while preserving runtime state for rollback recovery

### Documentation

- README is current
- deployment guide exists
- sample package usage is documented

### Operational Readiness

- health check endpoint exists
- separate liveness and readiness endpoints exist
- a post-deploy smoke-test script exists
- CI build/test automation exists
- CI builds and tests the Bootstrapper and emits SBOM/provenance evidence for the deployment artifact
- versioned release notes exist for the beta tag

### External User Readiness

- a documented outside-tester script exists
- guest flow, registration, verification, password reset, and quiz submission can be exercised without developer guidance

## Not Required Yet

- complete visual polish
- advanced analytics
- fully automated deployment
- enterprise-grade role model expansion

## Post-Beta Hardening Backlog

Known limitations accepted for now, tracked for a deliberate follow-up rather than a quick patch:

- **Email transport.** `Services/EmailService.cs` uses `System.Net.Mail.SmtpClient`, which Microsoft no longer recommends for new development (no modern/OAuth2 auth, limited TLS negotiation) and is a plausible contributor to the Gmail-delivery friction in `docs/FAQ.md`. Migrating to MailKit was evaluated during the 2026-09-15 design-review remediation: MailKit is **not currently a project dependency** (it and several other packages were removed from `TheCertMaster.csproj` in the "Refactor deployment and shared application code" commit, which also deleted a large duplicated `Deployment/source/` copy of the app that predated it). The open `dependabot/nuget/MailKit-4.16.0` branch predates that refactor and bumps a `MailKit` reference that no longer exists on `main`; it is stale and should be closed rather than merged as-is. Adding MailKit now would be a genuinely new dependency, not a wire-up of an existing one, and the migration touches SMTP authentication and TLS negotiation (a security-sensitive trust boundary per this repo's `AGENTS.md`). It deserves its own change with real STARTTLS/SSL and authenticated-credential verification, not a bundled hygiene fix.
