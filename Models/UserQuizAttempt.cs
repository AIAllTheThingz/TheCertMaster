namespace TheCertMaster.Models
{
    public class UserQuizAttempt
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public Guid QuizId { get; set; }
        public string QuizTitle { get; set; } = string.Empty;
        public string QuizCategory { get; set; } = "Uncategorized";
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public double ScorePercent { get; set; }
        public bool Passed { get; set; }
        public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
    }
}
