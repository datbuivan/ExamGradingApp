using Tesseract;

namespace ExamManager.Services
{
    public class TesseractOcrService : IOcrService
    {
        private readonly string _tessDataPath;
        private readonly ILogger<TesseractOcrService> _logger;

        public TesseractOcrService(IConfiguration configuration, ILogger<TesseractOcrService> logger)
        {
            _tessDataPath = configuration["TesseractDataPath"] ?? "./tessdata";
            _logger = logger;
        }

        public async Task<string> ExtractTextFromImageAsync(Stream imageStream)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ms = new MemoryStream();
                    imageStream.CopyTo(ms);
                    var imageBytes = ms.ToArray();

                    using var engine = new TesseractEngine(_tessDataPath, "vie+eng", EngineMode.Default);
                    using var img = Pix.LoadFromMemory(imageBytes);
                    using var page = engine.Process(img);

                    return page.GetText();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi OCR");
                    throw new Exception("Không thể đọc text từ ảnh. Vui lòng kiểm tra file tessdata.", ex);
                }
            });
        }
    }
}