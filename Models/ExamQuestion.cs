namespace ExamManager.Models
{
    public class ExamQuestion
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public string CorrectAnswer { get; set; } = string.Empty;

        public int CorrectAnswerIndex { get; set; } = -1; // Index của đáp án đúng (0-based)
        public List<int> CorrectAnswerIndices { get; set; } = new(); // ✅ THÊM: Danh sách index cho nhiều đáp án
        public bool IsMultipleChoice => CorrectAnswerIndices.Count > 1; // ✅ THÊM: Có nhiều đáp án không?
    }
}