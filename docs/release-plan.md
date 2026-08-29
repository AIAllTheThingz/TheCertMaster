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
