namespace ExamManager.Services
{
    public interface IOcrService
    {
        Task<string> ExtractTextFromImageAsync(Stream imageStream);
    }
}