using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;

namespace ExamManager.Services
{
    public class PdfReaderService : IPdfReader
    {
        private readonly ILogger<PdfReaderService> _logger;
        public PdfReaderService(ILogger<PdfReaderService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ExtractTextAsync(Stream pdfStream)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var sb = new StringBuilder();
                    using var reader = new PdfReader(pdfStream);
                    using var pdfDoc = new PdfDocument(reader);

                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var page = pdfDoc.GetPage(i);
                        var strategy = new SimpleTextExtractionStrategy();
                        sb.Append(PdfTextExtractor.GetTextFromPage(page, strategy));
                    }

                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi đọc PDF");
                    throw new Exception("Không thể đọc file PDF", ex);
                }
            });
        }
    }
}