# Release Checklist

Use this short checklist before pushing a tagged release.

## Before Push

- update [CHANGELOG.md](D:\Quiz_Application\DevQuizAPI\CHANGELOG.md)
- update the release notes file under [docs/releases](D:\Quiz_Application\DevQuizAPI\docs\releases)
- run `dotnet build QuizAPI.sln -c Release`
- run `dotnet test QuizAPI.Tests\QuizAPI.Tests.csproj -c Release`
- verify [README.md](D:\Quiz_Application\DevQuizAPI\README.md) reflects any new user-facing setup changes
- verify [DEPLOYMENT.md](D:\Quiz_Application\DevQuizAPI\DEPLOYMENT.md) still matches production expectations

## Before Tagging

- confirm the working tree is clean
- confirm sample/dev-only changes are not leaking into production defaults
- confirm JWT and connection string guidance is still accurate
- confirm the sample package and import docs still work end to end

## Before Publishing The GitHub Release

- push `main`
- push the release tag
- paste the release notes from the matching file in [docs/releases](D:\Quiz_Application\DevQuizAPI\docs\releases)
- call out known limitations honestly

## Quick Smoke Test

- open `/`
- open `/swapi.html`
- verify `/health`
- log in through [upload.html](D:\Quiz_Application\DevQuizAPI\wwwroot\upload.html)
- upload and import a sample package
- open [quiz.html](D:\Quiz_Application\DevQuizAPI\wwwroot\quiz.html) and verify quiz images render
