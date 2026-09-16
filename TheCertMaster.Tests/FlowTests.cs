using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCertMaster.Data;
using TheCertMaster.Models;
using Xunit;

namespace TheCertMaster.Tests;

public class FlowTests : IAsyncLifetime
{
    private readonly TheCertMasterWebApplicationFactory _factory;

    public FlowTests()
    {
        _factory = new TheCertMasterWebApplicationFactory();
    }

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsJwtToken()
    {
        var client = _factory.CreateClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "admin@thecertmaster.local",
            ["password"] = "ChangeThisLocalDevPassword!2026"
        });

        using var response = await client.PostAsync("/api/auth/login", content);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);

        Assert.True(doc.RootElement.TryGetProperty("token", out var tokenElement));
        Assert.False(string.IsNullOrWhiteSpace(tokenElement.GetString()));
    }

    [Fact]
    public async Task HealthEndpoints_ReturnStructuredHealthyPayloads()
    {
        var client = _factory.CreateClient();

        foreach (var path in new[] { "/health/live", "/health/ready", "/health" })
        {
            using var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);

            Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
            Assert.True(doc.RootElement.TryGetProperty("checks", out _));
            Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
        }
    }

    [Fact]
    public async Task VersionEndpoint_ReturnsApplicationMetadata()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/version");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);

        Assert.Equal("TheCertMaster", doc.RootElement.GetProperty("application").GetString());
        Assert.True(doc.RootElement.TryGetProperty("version", out _));
        Assert.True(doc.RootElement.TryGetProperty("utc", out _));
        Assert.False(doc.RootElement.TryGetProperty("environment", out _));
    }


    [Fact]
    public async Task GetQuizList_ReturnsSeededQuiz()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/quiz");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("Seed Quiz", payload);
    }

    [Fact]
    public async Task GetRandomizedQuiz_ReturnsSeededImageUrl()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/quiz");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(payload);
        var quizId = listDoc.RootElement.EnumerateArray()
            .First(e => string.Equals(e.GetProperty("Title").GetString(), "Seed Quiz", StringComparison.Ordinal))
            .GetProperty("Id")
            .GetGuid();

        using var randomizedResponse = await client.GetAsync($"/api/quiz/{quizId}/random");
        randomizedResponse.EnsureSuccessStatusCode();

        var randomizedPayload = await randomizedResponse.Content.ReadAsStringAsync();
        Assert.Contains("/uploads/images/test/seed-diagram.png", randomizedPayload);
    }

    [Fact]
    public async Task GuestRandomizedQuiz_IsClampedToFiveQuestions()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/quiz");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(payload);
        var quizId = listDoc.RootElement.EnumerateArray()
            .First(e => string.Equals(e.GetProperty("Title").GetString(), "Seed Quiz", StringComparison.Ordinal))
            .GetProperty("Id")
            .GetGuid();

        using var randomizedResponse = await client.GetAsync($"/api/quiz/{quizId}/random?questionCount=25");
        randomizedResponse.EnsureSuccessStatusCode();

        var randomizedPayload = await randomizedResponse.Content.ReadAsStringAsync();
        using var randomizedDoc = JsonDocument.Parse(randomizedPayload);
        Assert.InRange(randomizedDoc.RootElement.GetProperty("Questions").GetArrayLength(), 1, 5);
    }

    [Fact]
    public async Task GuestCustomQuiz_ReturnsFiveQuestionsPerSelectedCategory()
    {
        var client = _factory.CreateClient();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();

            foreach (var category in new[] { "Routing", "Security" })
            {
                var quiz = new Quiz
                {
                    Title = $"{category} Guest Clamp Quiz",
                    Category = category
                };

                for (var i = 1; i <= 10; i++)
                {
                    var question = new Question
                    {
                        Text = $"{category} Guest Question {i}?",
                        OrderIndex = i,
                        AllowMultiple = false
                    };

                    question.Answers.Add(new Answer { Text = $"Correct {i}", IsCorrect = true, OrderIndex = 1 });
                    question.Answers.Add(new Answer { Text = $"Incorrect {i}", IsCorrect = false, OrderIndex = 2 });
                    quiz.Questions.Add(question);
                }

                db.Quizzes.Add(quiz);
            }

            await db.SaveChangesAsync();
        }

        using var randomizedResponse = await client.GetAsync("/api/quiz/custom/random?categories=Routing&categories=Security&questionCount=25");
        randomizedResponse.EnsureSuccessStatusCode();

        var randomizedBody = await randomizedResponse.Content.ReadAsStringAsync();
        using var randomizedDoc = JsonDocument.Parse(randomizedBody);
        Assert.Equal(10, randomizedDoc.RootElement.GetProperty("Questions").GetArrayLength());
    }

    [Fact]
    public async Task UploadAndProcessCsv_ImportsQuizIntoDatabase()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var csv = string.Join(Environment.NewLine, new[]
        {
            "QuizTitle,Category,QuestionText,AnswerText,IsCorrect,QuestionImgKey",
            "Integration Import Quiz,Security,What improves account security?,Multi-factor authentication,true,",
            "Integration Import Quiz,Security,What improves account security?,Reused passwords,false,",
            "Integration Import Quiz,Security,Which item is a credential?,API token,true,",
            "Integration Import Quiz,Security,Which item is a credential?,Public logo,false,"
        });

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "File", "integration-import.csv");

        using var uploadResponse = await client.PostAsync("/api/import/upload", multipart);
        uploadResponse.EnsureSuccessStatusCode();

        var uploadMessage = await uploadResponse.Content.ReadAsStringAsync();
        Assert.Contains("Saved import file:", uploadMessage);

        var savedFileName = uploadMessage.Split(':', 2)[1].Trim();

        using var processResponse = await client.PostAsync($"/api/import/process/{savedFileName}", content: null);
        processResponse.EnsureSuccessStatusCode();

        using var verifyResponse = await client.GetAsync("/api/quiz?category=Security");
        verifyResponse.EnsureSuccessStatusCode();

        var verifyPayload = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("Integration Import Quiz", verifyPayload);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
        Assert.True(db.Quizzes.Any(q => q.Title == "Integration Import Quiz"));
    }

    [Fact]
    public async Task ImportedCategory_PreservesOriginalDisplayCasing()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var csv = string.Join(Environment.NewLine, new[]
        {
            "QuizTitle,Category,QuestionText,AnswerText,IsCorrect",
            "Security Plus Practice Quiz,CompTIA Security+,What does CIA stand for?,Confidentiality Integrity Availability,true"
        });

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "File", "casing-import.csv");

        using var uploadResponse = await client.PostAsync("/api/import/upload", multipart);
        uploadResponse.EnsureSuccessStatusCode();

        var uploadMessage = await uploadResponse.Content.ReadAsStringAsync();
        var savedFileName = uploadMessage.Split(':', 2)[1].Trim();

        using var processResponse = await client.PostAsync($"/api/import/process/{savedFileName}", content: null);
        processResponse.EnsureSuccessStatusCode();

        using var categoriesResponse = await client.GetAsync("/api/categories");
        categoriesResponse.EnsureSuccessStatusCode();
        var categoriesBody = await categoriesResponse.Content.ReadAsStringAsync();

        Assert.Contains("\"CompTIA Security+\"", categoriesBody);
        Assert.DoesNotContain("Comptia Security+", categoriesBody);

        using var quizListResponse = await client.GetAsync("/api/quiz?category=comptia%20security%2B");
        quizListResponse.EnsureSuccessStatusCode();
        var quizListBody = await quizListResponse.Content.ReadAsStringAsync();

        Assert.Contains("\"CompTIA Security+\"", quizListBody);
        Assert.Contains("Security Plus Practice Quiz", quizListBody);
    }

    [Fact]
    public async Task UploadPackage_ImportsQuizAndImagesInOneStep()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        byte[] packageBytes;
        using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var csvEntry = archive.CreateEntry("package-import.csv");
                await using (var writer = new StreamWriter(csvEntry.Open(), Encoding.UTF8))
                {
                    await writer.WriteAsync(string.Join(Environment.NewLine, new[]
                    {
                        "QuizTitle,Category,QuestionText,AnswerText,IsCorrect,QuestionImgKey",
                        "Package Import Quiz,Networking,What port does HTTPS use?,443,true,diagram-01",
                        "Package Import Quiz,Networking,What port does HTTPS use?,80,false,diagram-01"
                    }));
                }

                var imageEntry = archive.CreateEntry("images/diagram-01.png");
                await using (var imageStream = imageEntry.Open())
                {
                    await imageStream.WriteAsync(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                }
            }

            packageBytes = ms.ToArray();
        }

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(packageBytes), "File", "package-import.zip");

        using var uploadResponse = await client.PostAsync("/api/import/upload-package", multipart);
        uploadResponse.EnsureSuccessStatusCode();

        var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
        using var uploadDoc = JsonDocument.Parse(uploadBody);

        Assert.Equal("package-import.csv", uploadDoc.RootElement.GetProperty("CsvEntry").GetString());
        Assert.Equal(1, uploadDoc.RootElement.GetProperty("QuizzesImported").GetInt32());
        Assert.Equal(1, uploadDoc.RootElement.GetProperty("QuestionsImported").GetInt32());
        Assert.Equal(2, uploadDoc.RootElement.GetProperty("AnswersImported").GetInt32());
        Assert.Equal(1, uploadDoc.RootElement.GetProperty("ImagesSaved").GetInt32());
        Assert.Contains("Imported 1 quiz(es)", uploadDoc.RootElement.GetProperty("ImportMessage").GetString());

        using var verifyResponse = await client.GetAsync("/api/quiz?category=Networking");
        verifyResponse.EnsureSuccessStatusCode();

        var verifyPayload = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("Package Import Quiz", verifyPayload);
    }

    [Fact]
    public async Task UploadPackage_WithMissingImageKey_ReturnsValidationError()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        byte[] packageBytes;
        using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var csvEntry = archive.CreateEntry("missing-image.csv");
                await using (var writer = new StreamWriter(csvEntry.Open(), Encoding.UTF8))
                {
                    await writer.WriteAsync(string.Join(Environment.NewLine, new[]
                    {
                        "QuizTitle,Category,QuestionText,AnswerText,IsCorrect,QuestionImgKey",
                        "Broken Package Quiz,Networking,Which port does HTTPS use?,443,true,missing-diagram",
                        "Broken Package Quiz,Networking,Which port does HTTPS use?,80,false,missing-diagram"
                    }));
                }
            }

            packageBytes = ms.ToArray();
        }

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(packageBytes), "File", "broken-package.zip");

        using var uploadResponse = await client.PostAsync("/api/import/upload-package", multipart);
        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);

        var body = await uploadResponse.Content.ReadAsStringAsync();
        Assert.Contains("Missing image files for key(s): missing-diagram", body);
    }

    [Fact]
    public async Task UploadPackage_WithSvgOnlyImage_IsTreatedAsMissingRatherThanImported()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        byte[] packageBytes;
        using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var csvEntry = archive.CreateEntry("svg-only.csv");
                await using (var writer = new StreamWriter(csvEntry.Open(), Encoding.UTF8))
                {
                    await writer.WriteAsync(string.Join(Environment.NewLine, new[]
                    {
                        "QuizTitle,Category,QuestionText,AnswerText,IsCorrect,QuestionImgKey",
                        "Svg Package Quiz,Networking,What port does HTTPS use?,443,true,diagram-01",
                        "Svg Package Quiz,Networking,What port does HTTPS use?,80,false,diagram-01"
                    }));
                }

                // SVG is not a supported question-image extension, so this entry should
                // never be extracted, leaving "diagram-01" unresolved.
                var imageEntry = archive.CreateEntry("images/diagram-01.svg");
                await using (var imageStream = imageEntry.Open())
                await using (var writer = new StreamWriter(imageStream, Encoding.UTF8))
                {
                    await writer.WriteAsync("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>");
                }
            }

            packageBytes = ms.ToArray();
        }

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(packageBytes), "File", "svg-only-package.zip");

        using var uploadResponse = await client.PostAsync("/api/import/upload-package", multipart);
        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);

        var body = await uploadResponse.Content.ReadAsStringAsync();
        Assert.Contains("Missing image files for key(s): diagram-01", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
        Assert.False(db.Quizzes.Any(q => q.Title == "Svg Package Quiz"));
    }

    [Fact]
    public async Task ProcessCsv_WhenLaterQuizFailsToSave_RollsBackPreviouslyReplacedQuiz()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var initialCsv = string.Join(Environment.NewLine, new[]
        {
            "QuizTitle,Category,QuestionText,AnswerText,IsCorrect",
            "Rollback Quiz A,Rollback,What is the original question?,Original answer,true"
        });

        using (var multipart = new MultipartFormDataContent())
        {
            multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(initialCsv)), "File", "rollback-initial.csv");
            using var uploadResponse = await client.PostAsync("/api/import/upload", multipart);
            uploadResponse.EnsureSuccessStatusCode();

            var uploadMessage = await uploadResponse.Content.ReadAsStringAsync();
            var savedFileName = uploadMessage.Split(':', 2)[1].Trim();

            using var processResponse = await client.PostAsync($"/api/import/process/{savedFileName}", content: null);
            processResponse.EnsureSuccessStatusCode();
        }

        // The second import replaces "Rollback Quiz A" but also contains a quiz
        // whose title is too long for the database column. That forces the
        // deferred SaveChangesAsync() (covering every group in this import) to
        // fail after quiz A has already been removed from the change tracker.
        var overlongTitle = new string('B', 300);
        var failingCsv = string.Join(Environment.NewLine, new[]
        {
            "QuizTitle,Category,QuestionText,AnswerText,IsCorrect",
            "Rollback Quiz A,Rollback,What is the REPLACEMENT question?,Replacement answer,true",
            $"{overlongTitle},Rollback,Some question,Some answer,true"
        });

        using var failingMultipart = new MultipartFormDataContent();
        failingMultipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(failingCsv)), "File", "rollback-failing.csv");
        using var failingUploadResponse = await client.PostAsync("/api/import/upload", failingMultipart);
        failingUploadResponse.EnsureSuccessStatusCode();

        var failingUploadMessage = await failingUploadResponse.Content.ReadAsStringAsync();
        var failingSavedFileName = failingUploadMessage.Split(':', 2)[1].Trim();

        using var failingProcessResponse = await client.PostAsync($"/api/import/process/{failingSavedFileName}", content: null);
        Assert.False(failingProcessResponse.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();

        var quizA = db.Quizzes
            .Include(q => q.Questions).ThenInclude(q => q.Answers)
            .SingleOrDefault(q => q.Title == "Rollback Quiz A");

        Assert.NotNull(quizA);
        Assert.Contains(quizA!.Questions, q => q.Text == "What is the original question?");
        Assert.DoesNotContain(quizA.Questions, q => q.Text == "What is the REPLACEMENT question?");
        Assert.False(db.Quizzes.Any(q => q.Title == overlongTitle));
    }

    [Fact]
    public async Task PublicRegistration_RequiresEmailVerification_BeforeLoginSucceeds()
    {
        _factory.EmailService.Clear();
        var client = _factory.CreateClient();

        var registerPayload = JsonSerializer.Serialize(new
        {
            firstName = "New",
            lastName = "User",
            email = "newuser@example.com",
            password = "Password!123",
            confirmPassword = "Password!123"
        });

        using var registerResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(registerPayload, Encoding.UTF8, "application/json"));

        registerResponse.EnsureSuccessStatusCode();
        Assert.Single(_factory.EmailService.Messages);

        using var unverifiedLoginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "newuser@example.com",
            ["password"] = "Password!123"
        });

        using var unverifiedLoginResponse = await client.PostAsync("/api/auth/login", unverifiedLoginContent);
        Assert.Equal(HttpStatusCode.Unauthorized, unverifiedLoginResponse.StatusCode);

        var email = _factory.EmailService.Messages[0];
        var verificationLink = email.BodyText
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith("http", StringComparison.OrdinalIgnoreCase));

        var verificationPath = new Uri(verificationLink).PathAndQuery;
        using var verificationResponse = await client.GetAsync(verificationPath.Replace("/verify-email.html", "/api/auth/confirm-email", StringComparison.OrdinalIgnoreCase));
        verificationResponse.EnsureSuccessStatusCode();

        using var verifiedLoginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "newuser@example.com",
            ["password"] = "Password!123"
        });

        using var verifiedLoginResponse = await client.PostAsync("/api/auth/login", verifiedLoginContent);
        verifiedLoginResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("newuser@example.com");
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
        Assert.Equal("New", user.FirstName);
        Assert.Equal("User", user.LastName);
    }

    [Fact]
    public async Task PublicRegistration_ForExistingConfirmedEmail_ReturnsGenericSuccessResponse_AndSendsNoEmail()
    {
        _factory.EmailService.Clear();
        var client = _factory.CreateClient();

        var registerPayload = JsonSerializer.Serialize(new
        {
            firstName = "Already",
            lastName = "Registered",
            email = "alreadyregistered@example.com",
            password = "Password!123",
            confirmPassword = "Password!123"
        });

        using var registerResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(registerPayload, Encoding.UTF8, "application/json"));
        registerResponse.EnsureSuccessStatusCode();
        var successBody = await registerResponse.Content.ReadAsStringAsync();

        var verificationLink = _factory.EmailService.Messages[0].BodyText
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith("http", StringComparison.OrdinalIgnoreCase));
        var verificationPath = new Uri(verificationLink).PathAndQuery;
        using var verificationResponse = await client.GetAsync(
            verificationPath.Replace("/verify-email.html", "/api/auth/confirm-email", StringComparison.OrdinalIgnoreCase));
        verificationResponse.EnsureSuccessStatusCode();

        _factory.EmailService.Clear();

        using var repeatResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(registerPayload, Encoding.UTF8, "application/json"));

        Assert.True(repeatResponse.IsSuccessStatusCode);
        var repeatBody = await repeatResponse.Content.ReadAsStringAsync();
        Assert.Equal(successBody, repeatBody);
        Assert.Empty(_factory.EmailService.Messages);
    }

    [Fact]
    public async Task PublicRegistration_WhenVerificationEmailFails_ReturnsFriendlySuccessMessage()
    {
        _factory.EmailService.Clear();
        _factory.EmailService.ThrowOnSend = true;
        var client = _factory.CreateClient();

        var registerPayload = JsonSerializer.Serialize(new
        {
            firstName = "Email",
            lastName = "Pending",
            email = "emailpending@example.com",
            password = "Password!123",
            confirmPassword = "Password!123"
        });

        using var registerResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(registerPayload, Encoding.UTF8, "application/json"));

        registerResponse.EnsureSuccessStatusCode();
        var body = await registerResponse.Content.ReadAsStringAsync();
        Assert.Contains("could not send the verification email right now", body, StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("emailpending@example.com");
        Assert.NotNull(user);
        Assert.False(user!.EmailConfirmed);
    }

    [Fact]
    public async Task PublicRegistration_RejectsSpecialCharactersInProfileNames()
    {
        var client = _factory.CreateClient();

        var registerPayload = JsonSerializer.Serialize(new
        {
            firstName = "N3w!",
            lastName = "User",
            email = "invalidname@example.com",
            password = "Password!123",
            confirmPassword = "Password!123"
        });

        using var registerResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(registerPayload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, registerResponse.StatusCode);
        var body = await registerResponse.Content.ReadAsStringAsync();
        Assert.Contains("First name can only contain letters, spaces, hyphens, and apostrophes.", body);
    }

    [Fact]
    public async Task PublicRegistration_AcceptsRealNamesWithHyphensApostrophesAndAccents()
    {
        var client = _factory.CreateClient();
        _factory.EmailService.Clear();

        var registerPayload = JsonSerializer.Serialize(new
        {
            firstName = "Mary-Jane",
            lastName = "O'Brien",
            email = "maryjane@example.com",
            password = "Password!123",
            confirmPassword = "Password!123"
        });

        using var registerResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(registerPayload, Encoding.UTF8, "application/json"));
        registerResponse.EnsureSuccessStatusCode();

        var accentedPayload = JsonSerializer.Serialize(new
        {
            firstName = "José",
            lastName = "García",
            email = "jose@example.com",
            password = "Password!123",
            confirmPassword = "Password!123"
        });

        using var accentedResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(accentedPayload, Encoding.UTF8, "application/json"));
        accentedResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var maryJane = await userManager.FindByEmailAsync("maryjane@example.com");
        Assert.NotNull(maryJane);
        Assert.Equal("Mary-Jane", maryJane!.FirstName);
        Assert.Equal("O'Brien", maryJane.LastName);

        var jose = await userManager.FindByEmailAsync("jose@example.com");
        Assert.NotNull(jose);
        Assert.Equal("José", jose!.FirstName);
        Assert.Equal("García", jose.LastName);
    }

    [Fact]
    public async Task PublicRegistration_StillRejectsDigitsAndMarkup_InProfileNames()
    {
        var client = _factory.CreateClient();

        var scriptPayload = JsonSerializer.Serialize(new
        {
            firstName = "<script>",
            lastName = "User",
            email = "scriptname@example.com",
            password = "Password!123",
            confirmPassword = "Password!123"
        });

        using var scriptResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(scriptPayload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, scriptResponse.StatusCode);

        var digitPayload = JsonSerializer.Serialize(new
        {
            firstName = "John3",
            lastName = "User",
            email = "digitname@example.com",
            password = "Password!123",
            confirmPassword = "Password!123"
        });

        using var digitResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(digitPayload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, digitResponse.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedQuizSubmission_IsRecordedInAccountHistory()
    {
        _factory.EmailService.Clear();
        var client = _factory.CreateClient();

        var email = "historyuser@example.com";
        var password = "Password!123";

        var registerPayload = JsonSerializer.Serialize(new
        {
            firstName = "History",
            lastName = "User",
            email,
            password,
            confirmPassword = password
        });

        using var registerResponse = await client.PostAsync(
            "/api/auth/public-register",
            new StringContent(registerPayload, Encoding.UTF8, "application/json"));
        registerResponse.EnsureSuccessStatusCode();

        var verificationLink = _factory.EmailService.Messages[0].BodyText
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith("http", StringComparison.OrdinalIgnoreCase));

        using var verificationResponse = await client.GetAsync(new Uri(verificationLink).PathAndQuery
            .Replace("/verify-email.html", "/api/auth/confirm-email", StringComparison.OrdinalIgnoreCase));
        verificationResponse.EnsureSuccessStatusCode();

        using var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password
        });

        using var loginResponse = await client.PostAsync("/api/auth/login", loginContent);
        loginResponse.EnsureSuccessStatusCode();

        var loginPayload = await loginResponse.Content.ReadAsStringAsync();
        using var loginDoc = JsonDocument.Parse(loginPayload);
        var token = loginDoc.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var quizListResponse = await client.GetAsync("/api/quiz");
        quizListResponse.EnsureSuccessStatusCode();
        var quizListPayload = await quizListResponse.Content.ReadAsStringAsync();
        using var quizListDoc = JsonDocument.Parse(quizListPayload);
        var quizId = quizListDoc.RootElement.EnumerateArray()
            .First(e => string.Equals(e.GetProperty("Title").GetString(), "Seed Quiz", StringComparison.Ordinal))
            .GetProperty("Id")
            .GetGuid();

        using var randomizedResponse = await client.GetAsync($"/api/quiz/{quizId}/random");
        randomizedResponse.EnsureSuccessStatusCode();
        var randomizedPayload = await randomizedResponse.Content.ReadAsStringAsync();
        using var randomizedDoc = JsonDocument.Parse(randomizedPayload);

        var question = randomizedDoc.RootElement.GetProperty("Questions")[0];
        var questionId = question.GetProperty("QuestionId").GetGuid();
        var correctAnswerId = question.GetProperty("Answers").EnumerateArray()
            .First(a => string.Equals(a.GetProperty("Text").GetString(), "HTTPS", StringComparison.Ordinal))
            .GetProperty("AnswerId")
            .GetGuid();

        var submitPayload = JsonSerializer.Serialize(new
        {
            Answers = new[]
            {
                new
                {
                    QuestionId = questionId,
                    SelectedAnswerIds = new[] { correctAnswerId }
                }
            }
        });

        using var submitResponse = await client.PostAsync(
            $"/api/quiz/{quizId}/submit",
            new StringContent(submitPayload, Encoding.UTF8, "application/json"));
        submitResponse.EnsureSuccessStatusCode();

        var submitBody = await submitResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"Passed\":true", submitBody, StringComparison.OrdinalIgnoreCase);

        using var profileResponse = await client.GetAsync("/api/account/me");
        profileResponse.EnsureSuccessStatusCode();
        var profilePayload = await profileResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"TotalAttempts\":1", profilePayload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"FirstName\":\"History\"", profilePayload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"LastName\":\"User\"", profilePayload, StringComparison.OrdinalIgnoreCase);

        using var attemptsResponse = await client.GetAsync("/api/account/attempts");
        attemptsResponse.EnsureSuccessStatusCode();
        var attemptsPayload = await attemptsResponse.Content.ReadAsStringAsync();
        Assert.Contains("Seed Quiz", attemptsPayload);
        Assert.Contains("\"Passed\":true", attemptsPayload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminUserManagement_CanUpdateNames_And_SendPasswordResetLink()
    {
        _factory.EmailService.Clear();
        var client = _factory.CreateClient();
        var adminToken = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createPayload = JsonSerializer.Serialize(new
        {
            firstName = "Taylor",
            lastName = "Original",
            email = "manageduser@example.com",
            password = "Password!123",
            role = "User"
        });

        using var createResponse = await client.PostAsync(
            "/api/users",
            new StringContent(createPayload, Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();

        var updatePayload = JsonSerializer.Serialize(new
        {
            email = "manageduser@example.com",
            firstName = "Taylor",
            lastName = "Updated"
        });

        using var updateResponse = await client.PutAsync(
            "/api/users/manageduser@example.com/profile",
            new StringContent(updatePayload, Encoding.UTF8, "application/json"));
        updateResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync("manageduser@example.com");
            Assert.NotNull(user);
            Assert.Equal("Taylor", user!.FirstName);
            Assert.Equal("Updated", user.LastName);
            Assert.True(user.EmailConfirmed);
        }

        using var sendLinkResponse = await client.PostAsync("/api/users/manageduser@example.com/send-reset-link", content: null);
        sendLinkResponse.EnsureSuccessStatusCode();

        var message = Assert.Single(_factory.EmailService.Messages);
        Assert.Equal("manageduser@example.com", message.ToEmail);
        Assert.Contains("/reset-password.html?", message.BodyText, StringComparison.OrdinalIgnoreCase);

        var resetLink = message.BodyText
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith("http", StringComparison.OrdinalIgnoreCase));

        var uri = new Uri(resetLink);
        var query = QueryHelpers.ParseQuery(uri.Query);
        var email = query["email"].ToString();
        var code = query["code"].ToString();

        Assert.Equal("manageduser@example.com", email);
        Assert.False(string.IsNullOrWhiteSpace(code));

        var resetPayload = JsonSerializer.Serialize(new
        {
            email,
            code,
            newPassword = "Password!456",
            confirmPassword = "Password!456"
        });

        using var resetResponse = await client.PostAsync(
            "/api/auth/reset-password",
            new StringContent(resetPayload, Encoding.UTF8, "application/json"));
        resetResponse.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = null;

        using var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "manageduser@example.com",
            ["password"] = "Password!456"
        });

        using var loginResponse = await client.PostAsync("/api/auth/login", loginContent);
        loginResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ForgotPassword_ReturnsIdenticalResponse_ForExistingAndNonExistentEmail_AndOnlySendsRealEmailToExistingAccount()
    {
        _factory.EmailService.Clear();
        var client = _factory.CreateClient();

        using var existingContent = new StringContent(
            JsonSerializer.Serialize(new { email = "admin@thecertmaster.local" }),
            Encoding.UTF8,
            "application/json");
        using var existingResponse = await client.PostAsync("/api/auth/forgot-password", existingContent);
        existingResponse.EnsureSuccessStatusCode();
        var existingBody = await existingResponse.Content.ReadAsStringAsync();

        using var missingContent = new StringContent(
            JsonSerializer.Serialize(new { email = "no-such-user@example.com" }),
            Encoding.UTF8,
            "application/json");
        using var missingResponse = await client.PostAsync("/api/auth/forgot-password", missingContent);
        missingResponse.EnsureSuccessStatusCode();
        var missingBody = await missingResponse.Content.ReadAsStringAsync();

        Assert.Equal(existingResponse.StatusCode, missingResponse.StatusCode);
        Assert.Equal(existingBody, missingBody);

        var sentMessage = Assert.Single(_factory.EmailService.Messages);
        Assert.Equal("admin@thecertmaster.local", sentMessage.ToEmail);
        Assert.Contains("/reset-password.html?", sentMessage.BodyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminUserManagement_RejectsSpecialCharactersInProfileNames()
    {
        var client = _factory.CreateClient();
        var adminToken = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createPayload = JsonSerializer.Serialize(new
        {
            firstName = "Taylor",
            lastName = "User",
            email = "plainnamesonly@example.com",
            password = "Password!123",
            role = "User"
        });

        using var createResponse = await client.PostAsync(
            "/api/users",
            new StringContent(createPayload, Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();

        var updatePayload = JsonSerializer.Serialize(new
        {
            email = "plainnamesonly@example.com",
            firstName = "Taylor@",
            lastName = "User"
        });

        using var updateResponse = await client.PutAsync(
            "/api/users/plainnamesonly@example.com/profile",
            new StringContent(updatePayload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        var body = await updateResponse.Content.ReadAsStringAsync();
        Assert.Contains("First name can only contain letters, spaces, hyphens, and apostrophes.", body);
    }

    [Fact]
    public async Task RandomizedQuiz_CanLimitQuestionCount_And_SubmitScoresOnlyDeliveredSubset()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid quizId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();

            var quiz = new Quiz
            {
                Title = "Question Count Quiz",
                Category = "Security"
            };

            for (var i = 1; i <= 25; i++)
            {
                var question = new Question
                {
                    Text = $"Question {i}?",
                    OrderIndex = i,
                    AllowMultiple = false
                };

                var correctAnswer = new Answer { Text = $"Correct {i}", IsCorrect = true, OrderIndex = 1 };
                question.Answers.Add(correctAnswer);
                question.Answers.Add(new Answer { Text = $"Incorrect {i}", IsCorrect = false, OrderIndex = 2 });
                quiz.Questions.Add(question);
            }

            db.Quizzes.Add(quiz);
            await db.SaveChangesAsync();

            quizId = quiz.Id;
        }

        using var randomizedResponse = await client.GetAsync($"/api/quiz/{quizId}/random?questionCount=20");
        randomizedResponse.EnsureSuccessStatusCode();

        var randomizedPayload = await randomizedResponse.Content.ReadAsStringAsync();
        using var randomizedDoc = JsonDocument.Parse(randomizedPayload);
        var questions = randomizedDoc.RootElement.GetProperty("Questions").EnumerateArray().ToList();

        Assert.Equal(20, questions.Count);
        Assert.Equal(25, randomizedDoc.RootElement.GetProperty("TotalAvailableQuestions").GetInt32());

        var deliveredQuestionIds = questions
            .Select(q => q.GetProperty("QuestionId").GetGuid())
            .ToArray();

        var answeredQuestionId = deliveredQuestionIds[0];
        var selectedAnswerId = questions[0]
            .GetProperty("Answers")
            .EnumerateArray()
            .First()
            .GetProperty("AnswerId")
            .GetGuid();

        var submitPayload = JsonSerializer.Serialize(new
        {
            deliveredQuestionIds = deliveredQuestionIds,
            Answers = new[]
            {
                new
                {
                    QuestionId = answeredQuestionId,
                    SelectedAnswerIds = new[] { selectedAnswerId }
                }
            }
        });

        using var submitResponse = await client.PostAsync(
            $"/api/quiz/{quizId}/submit",
            new StringContent(submitPayload, Encoding.UTF8, "application/json"));
        submitResponse.EnsureSuccessStatusCode();

        var submitBody = await submitResponse.Content.ReadAsStringAsync();
        using var submitDoc = JsonDocument.Parse(submitBody);

        Assert.Equal(20, submitDoc.RootElement.GetProperty("TotalQuestions").GetInt32());
        Assert.Equal(20, submitDoc.RootElement.GetProperty("Questions").GetArrayLength());
    }

    [Fact]
    public async Task QuizSubmission_PreservesFirstDuplicateAnswer_DeduplicatesSelections_AndGradesUnansweredFalse()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid quizId;
        Guid singleQuestionId;
        Guid multipleQuestionId;
        Guid unansweredQuestionId;
        Guid singleCorrectId;
        Guid singleIncorrectId;
        Guid multipleCorrectAId;
        Guid multipleCorrectBId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
            var quiz = new Quiz
            {
                Title = "Grading Edge Case Quiz",
                Category = "Security"
            };

            var singleQuestion = new Question
            {
                Text = "Which protocol secures web traffic?",
                OrderIndex = 1,
                AllowMultiple = false
            };
            var singleCorrect = new Answer { Text = "HTTPS", IsCorrect = true, OrderIndex = 1 };
            var singleIncorrect = new Answer { Text = "FTP", IsCorrect = false, OrderIndex = 2 };
            singleQuestion.Answers.Add(singleCorrect);
            singleQuestion.Answers.Add(singleIncorrect);

            var multipleQuestion = new Question
            {
                Text = "Which are secure transport protocols?",
                OrderIndex = 2,
                AllowMultiple = true
            };
            var multipleCorrectA = new Answer { Text = "HTTPS", IsCorrect = true, OrderIndex = 1 };
            var multipleCorrectB = new Answer { Text = "SSH", IsCorrect = true, OrderIndex = 2 };
            multipleQuestion.Answers.Add(multipleCorrectA);
            multipleQuestion.Answers.Add(multipleCorrectB);
            multipleQuestion.Answers.Add(new Answer { Text = "FTP", IsCorrect = false, OrderIndex = 3 });

            var unansweredQuestion = new Question
            {
                Text = "Which protocol is intentionally left unanswered?",
                OrderIndex = 3,
                AllowMultiple = false
            };
            unansweredQuestion.Answers.Add(new Answer { Text = "HTTPS", IsCorrect = true, OrderIndex = 1 });

            quiz.Questions.Add(singleQuestion);
            quiz.Questions.Add(multipleQuestion);
            quiz.Questions.Add(unansweredQuestion);
            db.Quizzes.Add(quiz);
            await db.SaveChangesAsync();

            quizId = quiz.Id;
            singleQuestionId = singleQuestion.Id;
            multipleQuestionId = multipleQuestion.Id;
            unansweredQuestionId = unansweredQuestion.Id;
            singleCorrectId = singleCorrect.Id;
            singleIncorrectId = singleIncorrect.Id;
            multipleCorrectAId = multipleCorrectA.Id;
            multipleCorrectBId = multipleCorrectB.Id;
        }

        var deliveredQuestionIds = new[] { singleQuestionId, multipleQuestionId, unansweredQuestionId, unansweredQuestionId };
        var answers = new object[]
        {
            new { QuestionId = singleQuestionId, SelectedAnswerIds = new[] { singleIncorrectId } },
            new { QuestionId = singleQuestionId, SelectedAnswerIds = new[] { singleCorrectId } },
            new { QuestionId = multipleQuestionId, SelectedAnswerIds = new[] { multipleCorrectBId, multipleCorrectAId, multipleCorrectAId } }
        };
        var submitPayload = JsonSerializer.Serialize(new { deliveredQuestionIds, Answers = answers });

        using var response = await client.PostAsync(
            $"/api/quiz/{quizId}/submit",
            new StringContent(submitPayload, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();

        using var resultDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = resultDoc.RootElement;
        Assert.Equal(3, result.GetProperty("TotalQuestions").GetInt32());
        Assert.Equal(1, result.GetProperty("CorrectCount").GetInt32());
        Assert.False(result.GetProperty("Passed").GetBoolean());

        var questionResults = result.GetProperty("Questions").EnumerateArray()
            .ToDictionary(q => q.GetProperty("QuestionId").GetGuid());
        Assert.False(questionResults[singleQuestionId].GetProperty("IsCorrect").GetBoolean());
        Assert.Equal(new[] { singleIncorrectId }, questionResults[singleQuestionId]
            .GetProperty("SelectedAnswerIds").EnumerateArray().Select(id => id.GetGuid()));
        Assert.True(questionResults[multipleQuestionId].GetProperty("IsCorrect").GetBoolean());
        Assert.Equal(new[] { multipleCorrectAId, multipleCorrectBId }.OrderBy(id => id), questionResults[multipleQuestionId]
            .GetProperty("SelectedAnswerIds").EnumerateArray().Select(id => id.GetGuid()));
        Assert.False(questionResults[unansweredQuestionId].GetProperty("IsCorrect").GetBoolean());
        Assert.Empty(questionResults[unansweredQuestionId].GetProperty("SelectedAnswerIds").EnumerateArray());

        using var regularAllQuestionsResponse = await client.PostAsync(
            $"/api/quiz/{quizId}/submit",
            new StringContent(JsonSerializer.Serialize(new { Answers = answers }), Encoding.UTF8, "application/json"));
        regularAllQuestionsResponse.EnsureSuccessStatusCode();
        using var regularAllQuestionsDoc = JsonDocument.Parse(await regularAllQuestionsResponse.Content.ReadAsStringAsync());
        Assert.Equal(3, regularAllQuestionsDoc.RootElement.GetProperty("TotalQuestions").GetInt32());

        using var customResponse = await client.PostAsync(
            "/api/quiz/custom/submit",
            new StringContent(JsonSerializer.Serialize(new
            {
                quizTitle = "Grading Edge Case Custom Quiz",
                selectedCategories = new[] { "Security" },
                deliveredQuestionIds,
                Answers = answers
            }), Encoding.UTF8, "application/json"));
        customResponse.EnsureSuccessStatusCode();
        using var customResultDoc = JsonDocument.Parse(await customResponse.Content.ReadAsStringAsync());
        Assert.Equal(Guid.Empty, customResultDoc.RootElement.GetProperty("QuizId").GetGuid());
        Assert.Equal(3, customResultDoc.RootElement.GetProperty("TotalQuestions").GetInt32());
        Assert.Equal(1, customResultDoc.RootElement.GetProperty("CorrectCount").GetInt32());

        using var emptyCustomResponse = await client.PostAsync(
            "/api/quiz/custom/submit",
            new StringContent(JsonSerializer.Serialize(new { deliveredQuestionIds = Array.Empty<Guid>(), Answers = Array.Empty<object>() }), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, emptyCustomResponse.StatusCode);
    }

    [Fact]
    public async Task CustomCategoryQuiz_DefaultTwentyPerCategory_ReturnsTwentyFromEachSelectedCategory()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();

            foreach (var category in new[] { "Routing", "Security", "Cloud" })
            {
                var quiz = new Quiz
                {
                    Title = $"{category} Pool Quiz",
                    Category = category
                };

                for (var i = 1; i <= 30; i++)
                {
                    var question = new Question
                    {
                        Text = $"{category} Question {i}?",
                        OrderIndex = i,
                        AllowMultiple = false
                    };

                    question.Answers.Add(new Answer { Text = $"Correct {i}", IsCorrect = true, OrderIndex = 1 });
                    question.Answers.Add(new Answer { Text = $"Incorrect {i}", IsCorrect = false, OrderIndex = 2 });
                    quiz.Questions.Add(question);
                }

                db.Quizzes.Add(quiz);
            }

            await db.SaveChangesAsync();
        }

        using var availabilityResponse = await client.GetAsync("/api/quiz/custom/availability?categories=Routing&categories=Security&categories=Cloud");
        availabilityResponse.EnsureSuccessStatusCode();

        var availabilityBody = await availabilityResponse.Content.ReadAsStringAsync();
        using var availabilityDoc = JsonDocument.Parse(availabilityBody);
        Assert.True(availabilityDoc.RootElement.GetProperty("TotalAvailableQuestions").GetInt32() >= 90);

        using var randomizedResponse = await client.GetAsync("/api/quiz/custom/random?categories=Routing&categories=Security&categories=Cloud&questionCount=20");
        randomizedResponse.EnsureSuccessStatusCode();

        var randomizedBody = await randomizedResponse.Content.ReadAsStringAsync();
        using var randomizedDoc = JsonDocument.Parse(randomizedBody);
        var deliveredQuestions = randomizedDoc.RootElement.GetProperty("Questions").EnumerateArray().ToList();

        Assert.Equal(60, deliveredQuestions.Count);

        var deliveredQuestionIds = deliveredQuestions
            .Select(q => q.GetProperty("QuestionId").GetGuid())
            .ToArray();

        var submitPayload = JsonSerializer.Serialize(new
        {
            quizTitle = "Mixed Category Pool",
            selectedCategories = new[] { "Routing", "Security", "Cloud" },
            deliveredQuestionIds,
            Answers = Array.Empty<object>()
        });

        using var submitResponse = await client.PostAsync(
            "/api/quiz/custom/submit",
            new StringContent(submitPayload, Encoding.UTF8, "application/json"));
        submitResponse.EnsureSuccessStatusCode();

        var submitBody = await submitResponse.Content.ReadAsStringAsync();
        using var submitDoc = JsonDocument.Parse(submitBody);
        Assert.Equal(60, submitDoc.RootElement.GetProperty("TotalQuestions").GetInt32());
    }

    [Fact]
    public async Task CustomCategoryQuiz_RequestedTwentyFiveAcrossTwoCategories_ReturnsFiftyTotalQuestions()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();

            foreach (var category in new[] { "Networking", "Security" })
            {
                var quiz = new Quiz
                {
                    Title = $"{category} Mixed Pool Quiz",
                    Category = category
                };

                for (var i = 1; i <= 30; i++)
                {
                    var question = new Question
                    {
                        Text = $"{category} Mixed Question {i}?",
                        OrderIndex = i,
                        AllowMultiple = false
                    };

                    question.Answers.Add(new Answer { Text = $"Correct {i}", IsCorrect = true, OrderIndex = 1 });
                    question.Answers.Add(new Answer { Text = $"Incorrect {i}", IsCorrect = false, OrderIndex = 2 });
                    quiz.Questions.Add(question);
                }

                db.Quizzes.Add(quiz);
            }

            await db.SaveChangesAsync();
        }

        using var randomizedResponse = await client.GetAsync("/api/quiz/custom/random?categories=Networking&categories=Security&questionCount=25");
        randomizedResponse.EnsureSuccessStatusCode();

        var randomizedBody = await randomizedResponse.Content.ReadAsStringAsync();
        using var randomizedDoc = JsonDocument.Parse(randomizedBody);
        var deliveredQuestions = randomizedDoc.RootElement.GetProperty("Questions").EnumerateArray().ToList();

        Assert.Equal(50, deliveredQuestions.Count);
    }

    [Fact]
    public async Task CustomCategoryQuiz_RequestedThirtySixAcrossFourCategories_ReturnsOneHundredFortyFourTotalQuestions()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();

            foreach (var category in new[] { "Data Center Virtualization", "Networking", "Security", "Systems" })
            {
                var quiz = new Quiz
                {
                    Title = $"{category} Large Pool Quiz",
                    Category = category
                };

                for (var i = 1; i <= 40; i++)
                {
                    var question = new Question
                    {
                        Text = $"{category} Large Question {i}?",
                        OrderIndex = i,
                        AllowMultiple = false
                    };

                    question.Answers.Add(new Answer { Text = $"Correct {i}", IsCorrect = true, OrderIndex = 1 });
                    question.Answers.Add(new Answer { Text = $"Incorrect {i}", IsCorrect = false, OrderIndex = 2 });
                    quiz.Questions.Add(question);
                }

                db.Quizzes.Add(quiz);
            }

            await db.SaveChangesAsync();
        }

        using var randomizedResponse = await client.GetAsync("/api/quiz/custom/random?categories=Data%20Center%20Virtualization&categories=Networking&categories=Security&categories=Systems&questionCount=36");
        randomizedResponse.EnsureSuccessStatusCode();

        var randomizedBody = await randomizedResponse.Content.ReadAsStringAsync();
        using var randomizedDoc = JsonDocument.Parse(randomizedBody);
        var deliveredQuestions = randomizedDoc.RootElement.GetProperty("Questions").EnumerateArray().ToList();

        Assert.Equal(144, deliveredQuestions.Count);
    }

    [Fact]
    public async Task SubmitCustom_GuestCannotInflateQuestionCap_WithFabricatedCategoryNames()
    {
        var client = _factory.CreateClient();

        List<Guid> questionIds;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();

            foreach (var category in new[] { "Routing", "Security" })
            {
                var quiz = new Quiz { Title = $"{category} Cap Bypass Quiz", Category = category };

                for (var i = 1; i <= 10; i++)
                {
                    var question = new Question { Text = $"{category} Cap Question {i}?", OrderIndex = i, AllowMultiple = false };
                    question.Answers.Add(new Answer { Text = "Correct", IsCorrect = true, OrderIndex = 1 });
                    question.Answers.Add(new Answer { Text = "Incorrect", IsCorrect = false, OrderIndex = 2 });
                    quiz.Questions.Add(question);
                }

                db.Quizzes.Add(quiz);
            }

            await db.SaveChangesAsync();

            questionIds = await db.Questions
                .Where(q => q.Quiz!.Title == "Routing Cap Bypass Quiz" || q.Quiz!.Title == "Security Cap Bypass Quiz")
                .Select(q => q.Id)
                .ToListAsync();
        }

        Assert.Equal(20, questionIds.Count);

        // Only two real categories are actually resolved (10 questions each), so the
        // server-verified guest cap should be 2 * 5 = 10, regardless of how many
        // fabricated category names the client claims in SelectedCategories.
        var fabricatedCategories = new[] { "FakeCat1", "FakeCat2", "FakeCat3", "FakeCat4", "FakeCat5" };
        var payload = JsonSerializer.Serialize(new
        {
            quizTitle = "Cap Bypass Attempt",
            selectedCategories = fabricatedCategories,
            deliveredQuestionIds = questionIds,
            Answers = Array.Empty<object>()
        });

        using var response = await client.PostAsync(
            "/api/quiz/custom/submit",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("up to 10 questions", body);
    }

    [Fact]
    public async Task Submit_WithExcessiveDeliveredQuestionIds_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        using var listResponse = await client.GetAsync("/api/quiz");
        listResponse.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var quizId = listDoc.RootElement.EnumerateArray()
            .First(e => string.Equals(e.GetProperty("Title").GetString(), "Seed Quiz", StringComparison.Ordinal))
            .GetProperty("Id")
            .GetGuid();

        var oversizedIds = Enumerable.Range(0, 600).Select(_ => Guid.NewGuid()).ToList();
        var payload = JsonSerializer.Serialize(new { deliveredQuestionIds = oversizedIds, Answers = Array.Empty<object>() });

        using var response = await client.PostAsync(
            $"/api/quiz/{quizId}/submit",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_RepeatedRapidGuestRequests_AreRateLimited_LikeQuizLoad()
    {
        var client = _factory.CreateClient();

        using var listResponse = await client.GetAsync("/api/quiz");
        listResponse.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var quizId = listDoc.RootElement.EnumerateArray()
            .First(e => string.Equals(e.GetProperty("Title").GetString(), "Seed Quiz", StringComparison.Ordinal))
            .GetProperty("Id")
            .GetGuid();

        HttpStatusCode? rateLimitedStatus = null;
        for (var i = 0; i < 65 && rateLimitedStatus == null; i++)
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"/api/quiz/{quizId}/submit", content);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                rateLimitedStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedStatus);
    }
}
