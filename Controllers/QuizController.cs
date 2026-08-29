using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TheCertMaster.Data;
using TheCertMaster.DTO;
using TheCertMaster.Models;
using TheCertMaster.Services;
using System.Security.Claims;

namespace TheCertMaster.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        private const double PassingScorePercent = 70.0;
        private const int GuestQuestionLimit = 5;
        private readonly QuizQueryService _query;
        private readonly QuizDbContext _db;

        public QuizController(QuizQueryService query, QuizDbContext db)
        {
            _query = query;
            _db = db;
        }

        // Lightweight quiz listing for browser pickers / admin UIs
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? category)
        {
            var query = _db.Quizzes.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(category) &&
                !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedCategory = CategoryNormalizer.Normalize(category);

                query = query.Where(q =>
                    (q.Category ?? "Uncategorized") == normalizedCategory);
            }

            var quizzes = await query
                .OrderByDescending(q => q.CreatedUtc)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    Category = string.IsNullOrWhiteSpace(q.Category) ? "Uncategorized" : q.Category,
                    q.CreatedUtc
                })
                .ToListAsync();

            return Ok(quizzes);
        }

        // Returns randomized questions & answers on each call
        [EnableRateLimiting("QuizLoad")]
        [HttpGet("{quizId:guid}/random")]
        public async Task<IActionResult> GetRandomized(Guid quizId, [FromQuery] int? questionCount)
        {
            var dto = await _query.GetRandomizedAsync(quizId, NormalizeRequestedQuestionCount(questionCount));
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpGet("custom/availability")]
        public async Task<IActionResult> GetCustomAvailability([FromQuery] List<string> categories)
        {
            if (categories == null || categories.Count == 0)
                return BadRequest("At least one category is required.");

            var dto = await _query.GetAvailabilityForCategoriesAsync(categories);
            return Ok(dto);
        }

        [EnableRateLimiting("QuizLoad")]
        [HttpGet("custom/random")]
        public async Task<IActionResult> GetRandomizedCustom([FromQuery] List<string> categories, [FromQuery] int? questionCount)
        {
            if (categories == null || categories.Count == 0)
                return BadRequest("At least one category is required.");

            var dto = await _query.GetRandomizedByCategoriesAsync(categories, NormalizeRequestedQuestionCount(questionCount));
            return dto is null ? NotFound() : Ok(dto);
        }

        // Submits an attempt and returns a score. Correct answers never leave the server.
        [HttpPost("{quizId:guid}/submit")]
        public async Task<IActionResult> Submit(Guid quizId, [FromBody] QuizAttemptSubmitDto attempt)
        {
            if (attempt is null)
                return BadRequest("Missing request body.");

            var quiz = await _db.Quizzes
                .Include(q => q.Questions)
                    .ThenInclude(qn => qn.Answers)
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz is null)
                return NotFound();

            var deliveredQuestionIds = (attempt.DeliveredQuestionIds ?? new List<Guid>())
                .Distinct()
                .ToHashSet();

            var gradedQuestions = deliveredQuestionIds.Count == 0
                ? quiz.Questions.ToList()
                : quiz.Questions.Where(q => deliveredQuestionIds.Contains(q.Id)).ToList();

            if (IsGuestUser() && gradedQuestions.Count > GuestQuestionLimit)
                return BadRequest($"Guest users can submit up to {GuestQuestionLimit} questions per quiz.");

            var result = GradeQuestions(gradedQuestions, attempt, quizId);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                _db.UserQuizAttempts.Add(new UserQuizAttempt
                {
                    UserId = userId,
                    QuizId = quiz.Id,
                    QuizTitle = quiz.Title,
                    QuizCategory = string.IsNullOrWhiteSpace(quiz.Category) ? "Uncategorized" : quiz.Category,
                    TotalQuestions = result.TotalQuestions,
                    CorrectCount = result.CorrectCount,
                    ScorePercent = result.ScorePercent,
                    Passed = result.Passed,
                    SubmittedUtc = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
            }

            return Ok(result);
        }

        [HttpPost("custom/submit")]
        public async Task<IActionResult> SubmitCustom([FromBody] QuizAttemptSubmitDto attempt)
        {
            if (attempt is null)
                return BadRequest("Missing request body.");

            var deliveredQuestionIds = (attempt.DeliveredQuestionIds ?? new List<Guid>())
                .Distinct()
                .ToList();

            if (deliveredQuestionIds.Count == 0)
                return BadRequest("DeliveredQuestionIds is required for custom category quizzes.");

            var questions = await _db.Questions
                .Include(q => q.Answers)
                .Include(q => q.Quiz)
                .AsNoTracking()
                .Where(q => deliveredQuestionIds.Contains(q.Id))
                .ToListAsync();

            if (questions.Count == 0)
                return NotFound();

            if (IsGuestUser())
            {
                var guestCustomQuestionLimit = Math.Max(1, (attempt.SelectedCategories ?? new List<string>())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()) * GuestQuestionLimit;

                if (questions.Count > guestCustomQuestionLimit)
                    return BadRequest($"Guest users can submit up to {guestCustomQuestionLimit} questions for this category selection.");
            }

            var result = GradeQuestions(questions, attempt, Guid.Empty);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var historyTitle = string.IsNullOrWhiteSpace(attempt.QuizTitle) ? "Custom Category Quiz" : attempt.QuizTitle;
                var selectedCategories = (attempt.SelectedCategories ?? new List<string>())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();

                var historyCategory = selectedCategories.Count == 0
                    ? "Mixed Categories"
                    : string.Join(", ", selectedCategories);

                _db.UserQuizAttempts.Add(new UserQuizAttempt
                {
                    UserId = userId,
                    QuizId = Guid.Empty,
                    QuizTitle = historyTitle,
                    QuizCategory = historyCategory,
                    TotalQuestions = result.TotalQuestions,
                    CorrectCount = result.CorrectCount,
                    ScorePercent = result.ScorePercent,
                    Passed = result.Passed,
                    SubmittedUtc = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
            }

            return Ok(result);
        }

        private int? NormalizeRequestedQuestionCount(int? requestedQuestionCount)
        {
            if (!IsGuestUser())
                return requestedQuestionCount;

            return GuestQuestionLimit;
        }

        private bool IsGuestUser()
        {
            return User?.Identity?.IsAuthenticated != true;
        }

        private static QuizAttemptResultDto GradeQuestions(
            IReadOnlyCollection<Question> questions,
            QuizAttemptSubmitDto attempt,
            Guid quizId)
        {
            var incoming = (attempt.Answers ?? new List<QuestionAttemptDto>())
                .GroupBy(a => a.QuestionId)
                .ToDictionary(g => g.Key, g => g.First());
            var result = new QuizAttemptResultDto
            {
                QuizId = quizId,
                TotalQuestions = questions.Count
            };

            foreach (var question in questions)
            {
                incoming.TryGetValue(question.Id, out var answer);
                var selectedIds = (answer?.SelectedAnswerIds ?? new List<Guid>())
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();
                var correctIds = question.Answers
                    .Where(answer => answer.IsCorrect)
                    .Select(answer => answer.Id)
                    .OrderBy(id => id)
                    .ToList();
                var isCorrect = !question.AllowMultiple
                    ? selectedIds.Count == 1 && correctIds.Count == 1 && selectedIds[0] == correctIds[0]
                    : selectedIds.SequenceEqual(correctIds);

                if (isCorrect)
                    result.CorrectCount++;

                result.Questions.Add(new QuestionResultDto
                {
                    QuestionId = question.Id,
                    IsCorrect = isCorrect,
                    SelectedAnswerIds = selectedIds
                });
            }

            result.ScorePercent = result.TotalQuestions == 0
                ? 0
                : Math.Round((double)result.CorrectCount / result.TotalQuestions * 100.0, 2);
            result.Passed = result.ScorePercent >= PassingScorePercent;
            return result;
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{quizId:guid}")]
        public async Task<IActionResult> Delete(Guid quizId)
        {
            var quiz = await _db.Quizzes
             .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null)
             return NotFound($"Quiz not found: {quizId}");

            _db.Quizzes.Remove(quiz);
            await _db.SaveChangesAsync();

            return Ok($"Deleted quiz {quizId}");
        }

    }
}
