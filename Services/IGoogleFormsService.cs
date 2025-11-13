using ExamManager.Models;

namespace ExamManager.Services
{
    public interface IGoogleFormsService
    {
        Task<string> CreateFormAsync(string title, List<ExamQuestion> questions, string accessToken);
    }
}