# DotNetAgent.md

# Baseline .NET Development Contract

This file defines the default software development standards, architecture expectations, security rules, testing requirements, documentation requirements, and implementation guardrails for .NET projects.

Use this file as the baseline contract for .NET application development.

Project-specific agent files may extend this contract, but they should not weaken safety, security, maintainability, testability, or documentation requirements.

Codex must follow this file when generating, modifying, reviewing, testing, or documenting .NET code.

---

# 1. Purpose

The goal of this contract is to produce .NET software that is:

1. Safe
2. Secure
3. Maintainable
4. Testable
5. Documented
6. Observable
7. Deployable
8. Administrator-friendly
9. Developer-friendly
10. Production-aware

This contract applies to:

- ASP.NET Core web applications
- ASP.NET Core APIs
- Razor Pages applications
- MVC applications
- Blazor applications when explicitly requested
- Worker services
- Windows Services
- Console applications
- Class libraries
- Entity Framework Core projects
- Internal tools
- Enterprise applications
- Background processing systems
- Automation control applications
- Integration services

---

# 2. Non-Negotiable Rules

These rules override all other preferences.

1. Do not hardcode secrets.
2. Do not commit secrets.
3. Do not store passwords, API keys, tokens, private keys, or connection strings with credentials in source control.
4. Do not bypass authentication for production.
5. Do not bypass authorization for convenience.
6. Do not trust user input.
7. Do not expose stack traces or sensitive internal details to users.
8. Do not log secrets.
9. Do not hide errors silently.
10. Do not create destructive database migrations unless explicitly requested.
11. Do not delete user, audit, job, or business history casually.
12. Do not introduce paid third-party dependencies unless explicitly requested.
13. Do not add unnecessary architectural complexity.
14. Do not use a frontend framework unless requested or already present.
15. Do not leave the project in a broken build state.
16. Do not ignore security-sensitive edge cases.
17. Do not place business logic in views.
18. Do not scatter magic strings across the codebase.
19. Do not implement fake success states.
20. Do not claim something is complete unless it builds, is documented, and has a test or validation path.

The preferred priority order is:

1. Safe
2. Secure
3. Clear
4. Testable
5. Maintainable
6. Documented
7. Observable
8. Performant
9. Convenient

Convenience must never override safety, security, or correctness.

---

# 3. Design-First Requirement

Before generating or modifying code, Codex must first produce a short design summary unless the task is a minor formatting-only change, comment-only change, or very small bug fix.

For any new feature, architectural change, security change, database change, background service change, API change, or UI flow change, the design summary must include:

1. Purpose of the change
2. Affected projects
3. Affected files
4. New or changed models
5. New or changed services
6. New or changed routes, pages, controllers, or endpoints
7. New or changed database tables or migrations
8. Authentication impact
9. Authorization impact
10. Input validation behavior
11. Logging impact
12. Error handling behavior
13. Testing approach
14. Documentation updates
15. Deployment or configuration impact
16. Risks and assumptions

Do not begin implementation until the design is clear.

For small fixes, Codex may provide a brief planned-change summary instead of a full design.

For authentication, authorization, database schema, file upload, background processing, external integration, security, or deployment changes, design-first reasoning is mandatory.

---

# 4. Required Response Pattern for Code Changes

When asked to create or modify a .NET project, respond in this order unless the task is very small:

1. Design summary
2. Assumptions
3. Files to create or modify
4. Implementation
5. Configuration changes
6. Database or migration changes
7. Testing steps
8. Documentation updates
9. Build/run commands
10. Notes or limitations

When generating a Codex prompt, make it copy/paste-ready.

When generating project files, clearly label each file path.

---

# 5. Recommended Solution Structure

Use a structure appropriate to the application type.

For a standard web app or API:

```text
ProjectName/
├── src/
│   ├── ProjectName.Web/
│   ├── ProjectName.Api/
│   ├── ProjectName.Application/
│   ├── ProjectName.Domain/
│   ├── ProjectName.Infrastructure/
│   └── ProjectName.Data/
├── tests/
│   ├── ProjectName.Tests/
│   ├── ProjectName.IntegrationTests/
│   └── ProjectName.WebTests/
├── docs/
├── samples/
├── README.md
└── DotNetAgent.md
```

For smaller applications, fewer projects are acceptable:

```text
ProjectName/
├── src/
│   ├── ProjectName.Web/
│   └── ProjectName.Data/
├── tests/
│   └── ProjectName.Tests/
├── docs/
├── README.md
└── DotNetAgent.md
```

For worker/service applications:

```text
ProjectName/
├── src/
│   ├── ProjectName.Worker/
│   ├── ProjectName.Shared/
│   └── ProjectName.Data/
├── tests/
│   └── ProjectName.Tests/
├── docs/
├── README.md
└── DotNetAgent.md
```

Do not over-engineer the solution.

Use the smallest structure that still keeps responsibilities clear.

---

# 6. Architecture Standards

Prefer a layered architecture.

Recommended responsibility separation:

## Presentation Layer

Examples:

- Razor Pages
- MVC Controllers
- API Controllers
- Minimal APIs
- Blazor components when requested

Responsibilities:

- Accept requests
- Validate request shape
- Enforce authorization
- Call application services
- Return responses
- Display UI
- Avoid business logic

## Application Layer

Responsibilities:

- Business workflows
- Use case services
- Validation orchestration
- Authorization-aware operations
- DTO mapping
- Transaction coordination
- Result handling

## Domain Layer

Responsibilities:

- Core business rules
- Domain entities
- Value objects
- Domain enums
- Domain constants
- Business invariants

## Infrastructure Layer

Responsibilities:

- File system access
- Email
- External APIs
- Background job providers
- Secret stores
- System integrations
- Vendor SDKs

## Data Layer

Responsibilities:

- EF Core DbContext
- Entities or persistence models
- Migrations
- Entity configuration
- Seed data
- Database-specific behavior

Keep business logic out of UI views, Razor Pages, controllers, and EF migrations.

---

# 7. Coding Standards

Use clear, maintainable C#.

Prefer:

1. Meaningful names
2. Small methods
3. Small classes
4. Dependency injection
5. Async/await where appropriate
6. Strongly typed options
7. Strongly typed models
8. Nullable reference types when enabled
9. Constants or enums for repeated values
10. Guard clauses for invalid input
11. Structured logging
12. Clear error handling
13. Explicit validation
14. Simple control flow

Avoid:

1. Clever unreadable code
2. Large controllers
3. Large Razor Page handlers
4. Business logic in views
5. Static service access
6. Hidden side effects
7. Magic strings
8. Unbounded retries
9. Swallowed exceptions
10. Overly broad catch blocks
11. Global mutable state
12. Tight coupling
13. Premature abstraction
14. Unnecessary frameworks

---

# 8. Comments and Documentation in Code

Code must be understandable by developers and administrators who did not write it.

Every important class should include a short comment explaining its purpose.

Every public method should have a clear purpose.

Add comments for:

- Security-sensitive logic
- Authorization checks
- Validation rules
- File handling
- Database transaction behavior
- Background worker behavior
- External system calls
- Retry logic
- Error handling choices
- Non-obvious business rules

Do not add useless comments that only repeat the code.

Good comments explain why.

Bad comments merely repeat what.

---

# 9. Configuration Standards

Use strongly typed configuration classes.

Prefer the options pattern:

```text
IOptions<T>
IOptionsSnapshot<T>
IOptionsMonitor<T>
```

Validate configuration at startup.

Configuration should be grouped by responsibility.

Examples:

```json
{
  "Application": {
    "Name": "ProjectName",
    "EnvironmentLabel": "Development"
  },
  "Security": {
    "RequireHttps": true
  },
  "Uploads": {
    "MaxUploadSizeMB": 10,
    "AllowedExtensions": [ ".csv", ".txt" ]
  }
}
```

Do not scatter configuration lookups throughout the application.

Do not hardcode environment-specific values.

Do not store secrets in committed `appsettings.json`.

Use safe sources for secrets:

- Environment variables
- User secrets for local development
- Windows Credential Manager
- Azure Key Vault
- CyberArk
- DPAPI-protected values
- Enterprise-approved secret store

Document the selected approach.

---

# 10. Secrets Management

Never store secrets in:

- Source code
- Committed appsettings files
- Logs
- Reports
- Browser-visible pages
- Query strings
- Hidden fields
- Cookies unless properly protected
- Plaintext database columns
- Test files
- Sample files

Secrets include:

- Passwords
- API keys
- Bearer tokens
- Refresh tokens
- Private keys
- Client secrets
- Database passwords
- SMTP passwords
- Signing keys
- Encryption keys

Mask sensitive values before display or logging.

Use placeholders in examples.

Example placeholder:

```text
REPLACE_WITH_SECRET_FROM_APPROVED_SECRET_STORE
```

---

# 11. Authentication Standards

Authentication must be explicit and documented.

Supported approaches may include:

- ASP.NET Core Identity
- Windows Authentication
- OpenID Connect
- SAML through approved middleware
- JWT bearer authentication for APIs
- Development authentication for local development only

Development authentication must:

1. Work only in Development.
2. Never work in Production.
3. Be clearly labeled when active.
4. Not bypass authorization policies.
5. Not bypass audit logging.

If Development Authentication is enabled outside Development, the application must fail startup.

Do not create production authentication shortcuts.

---

# 12. Authorization Standards

Authorization must be enforced server-side.

UI hiding is not authorization.

Every privileged action must check authorization.

Use:

- Roles
- Policies
- Claims
- Resource-based authorization when needed

Define role and policy names as constants.

Do not scatter role strings throughout the code.

Examples:

```text
Viewer
Operator
Manager
Administrator
SystemAdministrator
```

Authorization checks are required for:

- Admin pages
- Data modification
- File downloads
- Report access
- User management
- Role management
- Background job submission
- Configuration changes
- Sensitive data views
- API endpoints

Deny by default.

Grant intentionally.

---

# 13. Input Validation Standards

Never trust user input.

Validate:

- Required fields
- Length
- Format
- Allowed characters
- Allowed values
- Numeric ranges
- Dates
- Email addresses
- URLs
- File names
- File extensions
- File sizes
- Uploaded file content where practical
- Enum values
- IDs
- Ownership and access rights

Client-side validation is helpful but not sufficient.

Server-side validation is required.

Use view models or request DTOs.

Avoid overposting.

Bind only expected fields.

---

# 14. File Upload Standards

File uploads are untrusted.

When uploads are supported:

1. Restrict file size.
2. Restrict extensions.
3. Restrict content type where practical.
4. Generate server-side file names.
5. Do not trust original file names.
6. Store files outside public web root unless explicitly safe.
7. Validate file contents where practical.
8. Prevent path traversal.
9. Prevent overwriting existing files.
10. Associate uploads with a user or record.
11. Log upload metadata.
12. Scan with antivirus if required by enterprise policy.
13. Do not execute uploaded files.

Never allow uploaded executable code to run unless the system is explicitly designed and reviewed for that purpose.

---

# 15. File and Path Safety

All file paths must be controlled.

Do not accept raw absolute paths from users unless specifically required and heavily validated.

Protect against:

- Path traversal
- Absolute path injection
- UNC path injection
- Symlink abuse where applicable
- Unauthorized file access
- Insecure direct object references

Use configured safe root directories.

Resolve full paths before use.

Confirm resolved paths remain under approved roots.

Use file IDs or artifact IDs for downloads instead of raw paths.

---

# 16. Database and EF Core Standards

Use EF Core carefully and intentionally.

Database changes must be deliberate.

For entity changes, document:

1. Reason for change
2. Migration name
3. Data impact
4. Rollback consideration
5. Seed data impact
6. Testing steps

Do not create destructive migrations unless explicitly requested.

Prefer soft delete for important business records.

Do not casually delete:

- Users
- Audit events
- Job history
- Transaction history
- Reports
- Security events

Use UTC timestamps for persisted dates.

Recommended audit fields:

```text
CreatedAtUtc
CreatedByUserId
UpdatedAtUtc
UpdatedByUserId
IsDeleted
DeletedAtUtc
DeletedByUserId
```

Use concurrency tokens where appropriate.

Use transactions where consistency matters.

---

# 17. API Standards

For APIs:

1. Use clear route names.
2. Use consistent HTTP verbs.
3. Validate request models.
4. Return appropriate status codes.
5. Do not expose internal exception details.
6. Use DTOs instead of exposing EF entities directly.
7. Protect endpoints with authorization.
8. Use pagination for lists.
9. Use filtering carefully.
10. Use correlation IDs where practical.
11. Document endpoints.

Expected HTTP behavior:

- `200 OK` for successful reads
- `201 Created` for successful creates
- `204 No Content` for successful deletes or updates without body
- `400 Bad Request` for validation failures
- `401 Unauthorized` for unauthenticated requests
- `403 Forbidden` for unauthorized requests
- `404 Not Found` for missing resources
- `409 Conflict` for concurrency conflicts
- `500 Internal Server Error` for unexpected failures

---

# 18. UI Standards

The UI should be clean, functional, and understandable.

Prioritize:

- Clear navigation
- Clear page titles
- Clear action buttons
- Clear validation messages
- Clear success/failure messages
- Clear status indicators
- Safe defaults
- Confirmation for risky actions
- No unnecessary clutter

Do not expose:

- Secrets
- Stack traces
- Raw exception dumps
- Internal physical paths
- Sensitive environment details
- Unauthorized data

For admin tools, clarity matters more than flashy design.

---

# 19. Background Worker Standards

Worker services must be reliable and observable.

Workers should:

1. Validate configuration at startup.
2. Log startup and shutdown.
3. Use cancellation tokens.
4. Avoid tight error loops.
5. Use configured polling intervals.
6. Use safe retry logic.
7. Record progress where appropriate.
8. Use health checks where practical.
9. Handle partial failure.
10. Avoid duplicate processing.
11. Be idempotent where practical.
12. Not lose work silently.

For job processing:

- Use explicit job states
- Use concurrency-safe claiming
- Record attempts
- Record errors
- Record start and end timestamps
- Record duration
- Record worker identity
- Avoid infinite retries

---

# 20. Job and Workflow State Standards

For workflow/job systems, use explicit states.

Recommended states:

```text
Created
Queued
Claimed
Validating
ValidationFailed
Running
Succeeded
Failed
Cancelled
TimedOut
Skipped
NeedsReview
```

Do not allow invalid transitions.

Record status changes with:

- Previous status
- New status
- Timestamp
- Actor
- Reason/message
- Correlation ID

Do not mark success unless the expected result was verified where practical.

---

# 21. Logging Standards

Use structured logging.

Prefer `ILogger<T>`.

Log:

- Startup
- Shutdown
- Configuration validation result
- Authentication events where appropriate
- Authorization failures where appropriate
- Important user actions
- Data changes
- Background job events
- External system calls
- Errors
- Warnings
- Security-relevant events

Logs should include:

- Timestamp
- Level
- Event ID where useful
- Correlation ID
- User ID where applicable
- Job ID where applicable
- Entity ID where applicable
- Operation name
- Result

Do not log secrets.

Do not log entire request bodies unless reviewed and masked.

---

# 22. Audit Logging Standards

Audit logging is required for security-sensitive and business-critical actions.

Audit:

- Login
- Logout where available
- Failed login where available
- Create/update/delete actions
- Role changes
- User changes
- Permission changes
- Admin actions
- File downloads for sensitive files
- Report views for sensitive reports
- Job submissions
- Job cancellations
- Configuration changes
- Security failures

Audit events should include:

- Timestamp UTC
- Actor
- Action
- Entity type
- Entity ID
- Result
- Source IP where available
- User agent where available
- Correlation ID
- Details with secrets masked

Audit logs should be append-oriented.

Do not allow normal users to edit audit events.

---

# 23. Error Handling Standards

Handle errors safely.

User-facing errors must be useful but not overly detailed.

Do not expose:

- Stack traces
- Connection strings
- Secret values
- Physical paths
- Raw SQL errors
- Internal exception dumps
- Sensitive environment details

Detailed errors belong in logs.

Use consistent error pages or problem responses.

For expected failures, return clear validation messages.

For unexpected failures, log the details and show a safe general message.

Do not swallow exceptions silently.

---

# 24. External Integration Standards

External calls must be isolated behind services.

Examples:

- Email
- APIs
- File shares
- Message queues
- Identity providers
- Vendor SDKs
- Payment systems
- Monitoring systems
- Automation systems

External integration services must:

1. Validate configuration.
2. Use timeouts.
3. Use cancellation tokens where practical.
4. Handle transient errors.
5. Log failures.
6. Avoid logging secrets.
7. Return clear result objects.
8. Be mockable for tests.

Do not call external systems directly from views or controllers.

---

# 25. Email Standards

When email is required:

1. Use configuration.
2. Do not hardcode SMTP settings.
3. Do not hardcode recipients unless explicitly required.
4. Do not log SMTP passwords.
5. Make email optional when appropriate.
6. Handle email failure without hiding main workflow failure.
7. Log email success/failure.
8. Document SMTP configuration.

Do not send secrets by email.

---

# 26. Security Standards

Follow secure development practices.

Protect against:

- Broken access control
- Injection
- Cross-site scripting
- Cross-site request forgery
- Path traversal
- Overposting
- Insecure direct object references
- Sensitive data exposure
- Insecure file upload
- Unsafe redirects
- Excessive error disclosure
- Weak authentication
- Weak authorization
- Misconfigured development features

Security-sensitive code must be simple and reviewable.

Do not be clever with security.

---

# 27. Dependency Standards

Use built-in .NET features where practical.

Do not add dependencies casually.

Before adding a package, consider:

1. Is it necessary?
2. Is it actively maintained?
3. Is it widely trusted?
4. Is it compatible with the target .NET version?
5. Does it introduce licensing concerns?
6. Does it introduce security concerns?
7. Can the same result be achieved simply without it?

Do not introduce paid dependencies unless explicitly requested.

Document required packages.

---

# 28. Testing Standards

Every meaningful change should include a testing plan.

Prefer automated tests where practical.

Test categories:

## Unit Tests

Use for:

- Validation logic
- Business rules
- Mapping logic
- Authorization helper logic
- File path safety
- State transitions
- Configuration validation

## Integration Tests

Use for:

- EF Core behavior
- Database queries
- Service interactions
- Authentication/authorization behavior where practical
- API endpoints where practical

## UI/Manual Tests

Use for:

- Login flow
- Navigation
- Forms
- Validation messages
- Admin pages
- File uploads
- Downloads
- Error pages

Tests must not require production infrastructure.

Mock external dependencies where practical.

---

# 29. Build and Quality Requirements

Before considering work complete, the solution should pass:

```text
dotnet restore
dotnet build
dotnet test
```

When applicable, also run:

```text
dotnet format
```

Do not leave the project in a broken build state.

Do not ignore compiler warnings that indicate real problems.

Do not suppress warnings without explaining why.

---

# 30. Documentation Requirements

Every project must include a README.

The README should include:

1. Project purpose
2. Architecture overview
3. Prerequisites
4. Configuration
5. Secret handling
6. Database setup if applicable
7. Build instructions
8. Run instructions
9. Test instructions
10. Deployment instructions
11. Security notes
12. Troubleshooting
13. Known limitations
14. Future roadmap where useful

Create a `docs/` folder for larger projects.

Recommended docs:

```text
README.md
docs/ARCHITECTURE.md
docs/CONFIGURATION.md
docs/SECURITY.md
docs/DEPLOYMENT.md
docs/TESTING.md
docs/TROUBLESHOOTING.md
docs/ROADMAP.md
```

---

# 31. Deployment Standards

Deployment must be documented.

Document:

- Target hosting model
- Environment variables
- appsettings overrides
- Database setup
- Migration process
- File permissions
- Service account permissions
- IIS configuration if applicable
- Windows Service installation if applicable
- Reverse proxy configuration if applicable
- TLS/HTTPS requirements
- Logging location
- Backup requirements
- Rollback process

Use least privilege for service accounts.

Do not run application services as domain admin or local administrator unless explicitly required and justified.

---

# 32. IIS Hosting Standards

For IIS-hosted ASP.NET Core apps, document:

1. ASP.NET Core Hosting Bundle installation.
2. Site or application creation.
3. Application pool configuration.
4. No Managed Code setting.
5. Authentication settings.
6. Authorization requirements.
7. Physical path.
8. File permissions.
9. Environment variables.
10. Logging path.
11. web.config behavior.
12. Health check URL.
13. Troubleshooting startup failures.

Do not assume IIS is already configured correctly.

---

# 33. Windows Service Standards

For Worker Services hosted as Windows Services, document:

1. Publish command.
2. Service creation command.
3. Service account.
4. Required permissions.
5. Start command.
6. Stop command.
7. Restart command.
8. Logging location.
9. Configuration location.
10. Recovery options.
11. Troubleshooting startup failures.

Use least privilege.

---

# 34. Health Check Standards

Add health checks where practical.

Health checks may include:

- Application running
- Database connectivity
- External service availability
- Required directory exists
- Required executable exists
- Worker heartbeat
- Queue availability
- Configuration validity

Health checks must not expose secrets.

Health checks must not perform destructive actions.

---

# 35. Performance Standards

Write efficient code, but do not prematurely optimize.

For lists:

- Use pagination
- Use filtering
- Avoid loading unnecessary data
- Avoid N+1 queries
- Use projections where appropriate
- Add indexes where appropriate

For background work:

- Use batching where appropriate
- Use timeouts
- Avoid unbounded concurrency
- Avoid infinite loops without delay
- Respect cancellation tokens

Performance improvements must not reduce correctness or security.

---

# 36. Accessibility and Usability

For web UI projects:

1. Use semantic HTML where practical.
2. Label form fields.
3. Show validation messages clearly.
4. Ensure buttons describe the action.
5. Avoid color-only status indicators.
6. Keep navigation predictable.
7. Make error messages actionable.
8. Use confirmation for risky actions.

Internal tools still deserve usable interfaces.

---

# 37. Versioning and Changelog

Use versioning where appropriate.

For larger projects, include:

```text
CHANGELOG.md
```

Use semantic-style versioning where practical:

- Major version for breaking changes
- Minor version for new features
- Patch version for bug fixes

Document meaningful behavior changes.

---

# 38. Git and Repository Hygiene

Do not commit:

- Secrets
- Build output
- Local user settings
- Temporary files
- Logs
- Generated reports
- Uploaded files
- Database files unless intentionally included
- Personal environment files

Use `.gitignore`.

Include safe examples.

Use `.sample`, `.template`, or `.example` files for configuration.

---

# 39. AI/Codex Behavior Rules

When Codex works on this repository:

1. Follow this contract.
2. Follow any more specific project agent files.
3. Prefer small, reviewable changes.
4. Do not rewrite unrelated code.
5. Do not change architecture without explaining why.
6. Do not introduce new dependencies without justification.
7. Do not remove safety checks.
8. Do not weaken authentication or authorization.
9. Do not skip validation.
10. Do not skip documentation.
11. Do not skip tests or manual validation steps.
12. Preserve existing conventions unless they are unsafe.
13. Explain assumptions.
14. Explain limitations.
15. Keep the project buildable.

---

# 40. Project-Specific Extension Guidance

Project-specific agent files may add:

- Business rules
- Required routes
- Required entities
- Required workflows
- Required roles
- Required integrations
- Required deployment targets
- Required documentation
- Required testing scenarios

Project-specific files may be named:

```text
AGENTS.md
AGENT.md
DotNetAgent.md
ProjectAgent.md
```

Project-specific files should extend this baseline.

They should not weaken this baseline.

If there is a conflict, the stricter and safer rule wins unless the user explicitly says otherwise.

---

# 41. Final Completion Checklist

Before marking work complete, verify:

1. The project builds.
2. Tests pass or a manual test plan is provided.
3. Configuration is documented.
4. Secrets are not hardcoded.
5. Authentication is not bypassed.
6. Authorization is enforced server-side.
7. Input validation exists.
8. Errors are handled safely.
9. Logs do not contain secrets.
10. Database changes are documented.
11. New dependencies are justified.
12. README or docs are updated.
13. Deployment impact is documented.
14. Known limitations are stated.
15. The final answer explains what changed.

---

# 42. Final Reminder

Build .NET applications like they will be maintained by someone else at 2:00 AM during an outage.

That means:

1. Clear structure.
2. Safe defaults.
3. Strong validation.
4. Real authorization.
5. Useful logging.
6. Helpful errors.
7. Documented configuration.
8. Repeatable deployment.
9. Testable behavior.
10. No magic.

The code should not require archaeology, telepathy, or a blood sacrifice to understand.

Build accordingly.
