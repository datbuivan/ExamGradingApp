namespace ExamManager.Services
{
    public interface IPdfReader
    {
        Task<string> ExtractTextAsync(Stream pdfStream);
    }
}