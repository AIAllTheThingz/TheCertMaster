# Deployment Scope Instructions

These instructions apply under `Deployment/` in addition to the repository root `AGENTS.md`.

- Treat deployment scripts as privileged operational automation.
- Preserve audit-first and validation-first behavior. Installation, migration, service, firewall, certificate, SMTP, IIS, and SQL changes require explicit target and environment confirmation.
- Do not embed production passwords, JWT keys, SMTP secrets, private keys, or environment-specific credentials.
- Prefer prerequisite, discovery, preview, and smoke-test phases before consequential changes.
- Preserve rollback or recovery guidance for deployment changes. If rollback cannot be demonstrated, state the limitation.
- Treat SQL Server, IIS, certificate, DNS, SMTP, Windows service, and network assumptions as environment-specific until validated.
- Do not infer successful deployment from static repository validation or from a standards composition result.
- Production execution and production verification remain human-authorized environment operations unless explicitly delegated.
