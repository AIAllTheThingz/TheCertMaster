using Microsoft.EntityFrameworkCore;
using TheCertMaster.Data;
using TheCertMaster.Models;

namespace TheCertMaster.Services
{
    public class SampleDataSeeder
    {
        private readonly QuizDbContext _db;
        private readonly IConfiguration _configuration;

        public SampleDataSeeder(QuizDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        public async Task SeedAsync()
        {
            var enabled = _configuration.GetValue<bool?>("SampleData:Enabled") ?? true;
            if (!enabled)
                return;

            if (await _db.Quizzes.AnyAsync())
                return;

            var quizzes = BuildSampleQuizzes();
            await _db.Quizzes.AddRangeAsync(quizzes);
            await _db.SaveChangesAsync();
        }

        private static List<Quiz> BuildSampleQuizzes()
        {
            return new List<Quiz>
            {
                new Quiz
                {
                    Title = "Networking Basics Demo",
                    Category = "Networking",
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Text = "Which protocol is commonly used to securely browse websites?",
                            OrderIndex = 1,
                            AllowMultiple = false,
                            Answers = new List<Answer>
                            {
                                new Answer { Text = "HTTPS", IsCorrect = true, OrderIndex = 1 },
                                new Answer { Text = "FTP", IsCorrect = false, OrderIndex = 2 },
                                new Answer { Text = "Telnet", IsCorrect = false, OrderIndex = 3 },
                                new Answer { Text = "SMTP", IsCorrect = false, OrderIndex = 4 }
                            }
                        },
                        new Question
                        {
                            Text = "Which device typically assigns local IP addresses using DHCP?",
                            OrderIndex = 2,
                            AllowMultiple = false,
                            Answers = new List<Answer>
                            {
                                new Answer { Text = "Router", IsCorrect = true, OrderIndex = 1 },
                                new Answer { Text = "Monitor", IsCorrect = false, OrderIndex = 2 },
                                new Answer { Text = "Keyboard", IsCorrect = false, OrderIndex = 3 },
                                new Answer { Text = "Printer cable", IsCorrect = false, OrderIndex = 4 }
                            }
                        }
                    }
                },
                new Quiz
                {
                    Title = "Security Basics Demo",
                    Category = "Security",
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Text = "Which practice most improves account security?",
                            OrderIndex = 1,
                            AllowMultiple = false,
                            Answers = new List<Answer>
                            {
                                new Answer { Text = "Using multi-factor authentication", IsCorrect = true, OrderIndex = 1 },
                                new Answer { Text = "Reusing the same password everywhere", IsCorrect = false, OrderIndex = 2 },
                                new Answer { Text = "Sharing passwords in email", IsCorrect = false, OrderIndex = 3 },
                                new Answer { Text = "Disabling updates", IsCorrect = false, OrderIndex = 4 }
                            }
                        },
                        new Question
                        {
                            Text = "Which two items are examples of sensitive credentials?",
                            OrderIndex = 2,
                            AllowMultiple = true,
                            Answers = new List<Answer>
                            {
                                new Answer { Text = "API tokens", IsCorrect = true, OrderIndex = 1 },
                                new Answer { Text = "Database passwords", IsCorrect = true, OrderIndex = 2 },
                                new Answer { Text = "Public documentation", IsCorrect = false, OrderIndex = 3 },
                                new Answer { Text = "Company logo files", IsCorrect = false, OrderIndex = 4 }
                            }
                        }
                    }
                }
            };
        }
    }
}
