# Privacy Data Inventory

This inventory describes the data flows currently implemented in TheCertMaster. It is written for maintainers, operators, and reviewers. It records implementation behavior only; it does not create a retention schedule or data-rights process. The implementation anchors are [ApplicationUser and UserQuizAttempt](../Models/ApplicationUser.cs), [account and user controllers](../Controllers/AccountController.cs), [quiz and import controllers](../Controllers/QuizController.cs), [the import service](../Services/QuizImportService.cs), and [the SMTP settings store](../Services/FileSmtpSettingsStore.cs).

## Accounts

**Collection/source:** Email address, password credentials, first name, last name, email-confirmation state, roles, and Identity account metadata are collected through public registration, administrator user management, login, email verification, password reset, and profile operations. Passwords are processed by ASP.NET Core Identity rather than stored as plaintext application fields.

**Purpose:** Authenticate users, verify email addresses, authorize administrator and user operations, support password recovery, and display account profile information.

**Storage location:** ASP.NET Core Identity tables in the configured SQL Server database, including `AspNetUsers`, `AspNetRoles`, and their relationship tables. Data Protection keys used by the application are persisted under `App_Data/keys`.

**Access:** A signed-in user can read their own profile and change their password. Administrators can list users, create and update users, reset passwords, send reset links, and delete accounts through the administrator API. Password values are not returned by the SMTP or account APIs.

**Retention:** No application retention period or scheduled purge is implemented. Account records remain until an administrator deletes them or an operator changes the database outside the application.

**Deletion:** `DELETE /api/users/{email}` calls ASP.NET Core Identity account deletion. There is no user-facing self-service deletion flow. Quiz attempt history has no foreign-key relationship to `ApplicationUser`, so account deletion does not currently cascade to that history.

**Export:** No account export endpoint or built-in export process is implemented.

## Quiz attempt history

**Collection/source:** A signed-in quiz submission records the user identifier, quiz identifier, quiz title and category, total question count, correct answer count, score percentage, pass/fail result, and UTC submission time. Anonymous submissions are not recorded in account history.

**Purpose:** Show a signed-in user their attempt history and calculate total, passed, and failed attempt summaries.

**Storage location:** `UserQuizAttempts` in the configured SQL Server database. The table has an index on user identifier and submission time, but no foreign-key relationship to `ApplicationUser`.

**Access:** The owning signed-in user can read their own summary and attempts through `GET /api/account/me` and `GET /api/account/attempts`. No dedicated administrator history or export endpoint is implemented.

**Retention:** No application retention period, archival rule, or scheduled purge is implemented. Rows remain until removed directly by an operator or database process.

**Deletion:** There is no API to delete an individual attempt or a user's history. Deleting an account does not cascade to these rows because the model has no user foreign key.

**Export:** No attempt-history export endpoint or built-in export process is implemented.

## Imported quiz files and derived records

**Collection/source:** Administrators upload CSV, TXT, and ZIP quiz packages. The importer reads quiz titles, categories, question text, answer text, correctness flags, and optional image keys; ZIP images are also accepted. Import processing writes derived quiz, question, answer, and image records and appends import run metadata.

**Purpose:** Create and replace quizzes for administrators and make their questions, answers, and referenced images available to quiz users.

**Storage location:** Uploaded source files and extracted CSV files are stored below `App_Data/uploads`. Extracted images are stored below `wwwroot/uploads/images/{packageId}`. Derived quiz, question, answer, and image metadata are stored in SQL Server. Import history is appended to `App_Data/import_history.jsonl`.

**Access:** Administrators can upload, process, and view import history through the import API. Quiz content and image URLs are available through the public quiz APIs and browser pages according to the application's current authorization rules. `App_Data` is intended as private application storage; the host must enforce that boundary.

**Retention:** No application retention period or automatic cleanup for source files, extracted images, import history, or derived quiz records is implemented. Existing quiz records with the same title and normalized category are replaced during import.

**Deletion:** Administrators can delete a quiz through the quiz API; cascading removes its questions, answers, and image metadata. Administrators can also delete an individual source file with `DELETE /api/files/{fileName}`. Neither operation removes all related uploaded files, extracted package images, or import-history lines, and no propagation or cleanup job is implemented.

**Export:** No export endpoint or built-in package export process is implemented.

## SMTP settings and credentials

**Collection/source:** An administrator supplies SMTP host, port, TLS/SSL flags, username, password, sender address, and sender name through the SMTP administrator API. The deployment helper `configure-smtp-test.ps1` can also write SMTP settings for a host.

**Purpose:** Send email verification, password-reset, and administrator test messages.

**Storage location:** The application reads defaults from configuration and persists settings to `App_Data/smtp_settings.json`. On Windows, the application protects Data Protection keys under `App_Data/keys` with local-machine DPAPI. Current reads retain a legacy plaintext `Password` fallback for old settings files. When the application saves a newly supplied password, it writes `ProtectedPassword` and leaves the serialized `Password` field empty; when `KeepExistingPasswordWhenBlank` is used, an existing legacy plaintext value can be copied forward. The deployment helper [configure-smtp-test.ps1](../scripts/configure-smtp-test.ps1) writes the supplied `Password` in plaintext and clears `ProtectedPassword`.

**Access:** Only administrators can read non-secret SMTP settings or save them through the API. The read response exposes only a `passwordSet` flag, not the password. The application uses the configured password when sending mail. Operators with access to the host's application-data directory or configuration can access the persisted settings according to host permissions.

**Retention:** No application retention period or scheduled cleanup is implemented. Settings remain in configuration or `App_Data/smtp_settings.json` until replaced or removed by an operator.

**Deletion:** Administrators can overwrite settings, but there is no dedicated API to delete SMTP settings or securely erase legacy values. Removing the settings file or configuration is an operator action and is not automated by the application.

**Export:** No SMTP settings export endpoint is implemented. Passwords are intentionally not returned by the administrator GET endpoint.

## Known lifecycle gaps

The application currently has no implemented general retention schedule, account or attempt-history export, self-service deletion, source-file cleanup policy, or verified deletion propagation across backups and derived files. These are documented gaps, not commitments that the application currently fulfills.
