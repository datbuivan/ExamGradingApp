namespace ExamManager.Services
{
    public interface IDocxReader
    {
        Task<string> ExtractTextAsync(Stream docxStream);
    }
}