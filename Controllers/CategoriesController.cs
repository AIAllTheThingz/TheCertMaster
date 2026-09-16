using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheCertMaster.Data;
using TheCertMaster.Services;

namespace TheCertMaster.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly QuizDbContext _db;

        public CategoriesController(QuizDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var raw = await _db.Quizzes
                .AsNoTracking()
                .OrderBy(q => q.CreatedUtc)
                .Select(q => q.Category)
                .ToListAsync();

            // Distinct keeps the first-encountered casing for each case-insensitive
            // group, so the earliest-imported quiz's casing wins for display.
            var categories = raw
                .Select(CategoryNormalizer.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!categories.Contains("Uncategorized", StringComparer.OrdinalIgnoreCase))
                categories.Add("Uncategorized");

            return Ok(categories);
        }
    }
}
