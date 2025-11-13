using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace ExamManager.Services
{
    public class DocxReaderService : IDocxReader
    {
        private readonly ILogger<DocxReaderService> _logger;

        public DocxReaderService(ILogger<DocxReaderService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ExtractTextAsync(Stream docxStream)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var text = new StringBuilder();
                    using var doc = WordprocessingDocument.Open(docxStream, false);
                    var body = doc.MainDocumentPart?.Document.Body;

                    if (body != null)
                    {
                        foreach (var paragraph in body.Elements<Paragraph>())
                        {
                            var paragraphText = paragraph.InnerText;
                            if (!string.IsNullOrWhiteSpace(paragraphText))
                            {
                                text.AppendLine(paragraphText);
                            }
                        }
                    }

                    return text.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi đọc DOCX");
                    throw new Exception("Không thể đọc file DOCX", ex);
                }
            });
        }
    }
}