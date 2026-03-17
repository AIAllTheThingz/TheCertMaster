# DevQuizAPI

`DevQuizAPI` is a .NET 9 quiz platform for building, importing, managing, and taking quizzes through both API endpoints and simple browser-based pages.

It is the active source-of-truth application in this workspace.

## What It Does

- JWT-based authentication for admin and user access
- quiz listing and quiz-taking APIs
- CSV/TXT quiz import workflow
- uploaded file management
- admin user management
- SMTP configuration and email test endpoints
- browser-based utility pages for auth testing, quiz running, and admin upload/import work

## Tech Stack

- ASP.NET Core 9
- Entity Framework Core 9
- SQL Server / SQL Server Express
- ASP.NET Core Identity
- JWT bearer authentication
- Swagger / OpenAPI

## Project Structure

- [Program.cs](D:\Quiz_Application\DevQuizAPI\Program.cs): app startup, auth, CORS, Swagger, migrations, dev seeding
- [Controllers](D:\Quiz_Application\DevQuizAPI\Controllers): API endpoints
- [Data](D:\Quiz_Application\DevQuizAPI\Data): EF Core `DbContext`
- [Models](D:\Quiz_Application\DevQuizAPI\Models): entities
- [DTO](D:\Quiz_Application\DevQuizAPI\DTO): request/response models
- [Services](D:\Quiz_Application\DevQuizAPI\Services): quiz import, query, SMTP, sample data seeding
- [Migrations](D:\Quiz_Application\DevQuizAPI\Migrations): database schema history
- [wwwroot](D:\Quiz_Application\DevQuizAPI\wwwroot): static browser pages

## Local Development

### Requirements

- .NET 9 SDK
- SQL Server Express or another reachable SQL Server instance

### Default Development Configuration

Development settings live in [appsettings.Development.json](D:\Quiz_Application\DevQuizAPI\appsettings.Development.json).

By default, development uses:

- environment: `Development`
- database: `DevQuizDB`
- base URL: `http://localhost:5185`
- HTTPS URL: `https://localhost:7193`

### First Run

From your local project directory or Git-cloned repository folder:

```powershell
dotnet build
dotnet run
```

On startup in `Development`, the app will:

- apply EF Core migrations automatically
- create the development roles if needed
- create the development admin account if needed
- seed fake sample quiz data if the quiz tables are empty

### Default Development Admin

- email: `admin@quizapi.local`
- password: `Admin@123`

You should change these for any shared or non-local environment.

## Sample Data

This repository is set up to be safe for public use.

- real database files are not committed
- real user data is not committed
- fake demo quiz data can be seeded automatically for local development

Sample data behavior:

- controlled by `SampleData:Enabled`
- defaults to `true` in development
- only seeds when there are no quizzes in the database

## Browser Entry Points

- [index.html](D:\Quiz_Application\DevQuizAPI\wwwroot\index.html): simple auth/login test page
- [upload.html](D:\Quiz_Application\DevQuizAPI\wwwroot\upload.html): admin dashboard for login, file upload, imports, user management, SMTP settings
- [quiz.html](D:\Quiz_Application\DevQuizAPI\wwwroot\quiz.html): quiz runner UI

In development, Swagger is also available at the app root.

## Main API Areas

- `POST /api/auth/login`
- `POST /api/auth/register`
- `GET /api/categories`
- `GET /api/quiz`
- `GET /api/quiz/{quizId}/random`
- `POST /api/quiz/{quizId}/submit`
- `GET /api/files`
- `POST /api/import/upload`
- `POST /api/import/process/{fileName}`
- `GET /api/users`
- `POST /api/users`
- `GET /api/admin/smtp`
- `POST /api/admin/smtp`
- `POST /api/admin/smtp/test`

## Configuration Notes

Important configuration keys:

- `ConnectionStrings:DefaultConnection`
- `Jwt:Key`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Cors:AllowedOrigins`
- `DevAdmin:Email`
- `DevAdmin:Password`
- `SampleData:Enabled`

For production-like deployments, prefer environment variables or secret storage for:

- JWT signing keys
- SMTP credentials
- connection strings

## Build

```powershell
dotnet build
```

## Git

This project is tracked in Git and uses [main](D:\Quiz_Application\DevQuizAPI) as the default branch.

Remote:

- [https://github.com/mezuccolini/QuizAPI.git](https://github.com/mezuccolini/QuizAPI.git)

## Notes

- `DevQuizAPI` is the maintained application going forward.
- The separate `QuizAPI` folder elsewhere in the workspace is treated as a different application.
