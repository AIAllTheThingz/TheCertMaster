# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog, and this project follows semantic-style version tags.

## [Unreleased]

### Added

- Self-service "forgot password" flow: `POST /api/auth/forgot-password` lets a public user request a password reset link without admin involvement
- Public user account management page
- Persisted signed-in quiz history with pass/fail tracking
- Public email verification flow support in the documented user path
- Quiz result home page for the most recent submitted attempt
- Rate limiting for auth-sensitive public endpoints and quiz loading
- Structured security logging for auth failures, admin actions, SMTP changes, and import failures
- Liveness, readiness, and version endpoints for deployment monitoring
- Post-deploy smoke-test script and external user testing guide

### Changed

- README now documents the public registration, account, and result-home flows
- FAQ now covers account history, signed-in quiz tracking, and the post-submit Home button flow
- Quiz runner now unlocks a `Home` button after submit and routes users into a result summary page
- Admin dashboard now restores only valid admin sessions and clears stale or unauthorized tokens automatically
- User-facing browser pages now surface cleaner API error messages
- Profile-oriented text inputs are now normalized to plain letters and spaces where appropriate
- Production startup now also requires `PublicApp:BaseUrl`
- Swagger is now explicitly controlled by configuration instead of environment alone
- Quiz setup now shows a clearer total-question summary before loading

### Fixed

- Bulk quiz CSV import now runs each import as a single database transaction, so a failure partway through an import (e.g. a later quiz's data failing to save) rolls back any quizzes already removed for replacement instead of leaving them permanently deleted
- Public registration no longer reveals whether an email address already has a confirmed account; a repeat registration attempt now gets the same generic success response as a first-time registration, and no duplicate email is sent
- Quiz submission endpoints (`/api/quiz/{id}/submit`, `/api/quiz/custom/submit`) now share the same rate-limiting policy as quiz loading, reject requests with an unreasonably large number of delivered questions or answers, and derive the guest per-category question cap from the categories actually present in the resolved questions instead of trusting the client-supplied category list
- Certification category names imported with vendor-specific capitalization (e.g. "CompTIA Security+") are now displayed exactly as imported instead of being rewritten to title case (e.g. "Comptia Security+"); matching/de-duplication across different casings of the same category is unaffected and uses first-seen casing for display
- Profile name fields now accept hyphens, apostrophes, and accented/non-Latin letters (e.g. "Mary-Jane", "O'Brien", "José"), not just plain ASCII letters and spaces; digits, angle brackets, and other unsafe characters are still rejected
- SVG is no longer accepted as a question-image type in quiz package imports; an import referencing only an SVG image now fails the same "missing image" validation as a genuinely missing file, instead of writing an unsanitized SVG under the public uploads path

## [v0.1.0-alpha] - 2026-03-17

### Added

- Initial Git repository, README, and public sample quiz package
- Development sample data seeding for first-run local environments
- Browser landing page at `/`
- Swagger UI served from `/swapi.html`
- Integration test project covering auth, quiz, and import flows
- ZIP package image import and quiz image display support

### Changed

- Dev environment now uses automatic migrations on startup
- Upload/import UI now handles ZIP packages more clearly
- Uploaded file listing now reads from the correct private upload storage
- Quiz runner now relies on quiz/category selection instead of pasted quiz IDs
- Production configuration now fails fast when required secrets or connection settings are missing

### Removed

- Pre-employment quiz feature and related static pages from `TheCertMaster`

### Fixed

- Login request media-type mismatch
- Dev admin seeding and development DB bootstrapping
- Missing quiz image associations during package import
- Uploaded ZIP visibility in the admin file list
- Landing page routing and root-path navigation issues

[v0.1.0-alpha]: https://github.com/AIAllTheThingz/TheCertMaster/releases/tag/v0.1.0-alpha
