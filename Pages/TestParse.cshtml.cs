using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExamManager.Services;
using ExamManager.Models;
using System.Text.RegularExpressions;

namespace ExamManager.Pages
{
    public class LogItem
    {
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = ""; // log-success, log-warning, log-error
    }

    public class TestParseModel : PageModel
    {
        private readonly IDocxReader _docxReader;
        private readonly IPdfReader _pdfReader;
        private readonly ILogger<TestParseModel> _logger;

        public string RawText { get; set; } = string.Empty;
        public List<ExamQuestion> Questions { get; set; } = new();
        public List<LogItem> Logs { get; set; } = new();

        public TestParseModel(IDocxReader docxReader, IPdfReader pdfReader, ILogger<TestParseModel> logger)
        {
            _docxReader = docxReader;
            _pdfReader = pdfReader;
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync(IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                AddLog($"📁 File: {file.FileName} ({file.Length / 1024:F2} KB)", "log-success");

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                var extension = Path.GetExtension(file.FileName).ToLower();

                try
                {
                    RawText = extension switch
                    {
                        ".docx" => await _docxReader.ExtractTextAsync(stream),
                        ".pdf" => await _pdfReader.ExtractTextAsync(stream),
                        ".txt" => await new StreamReader(stream).ReadToEndAsync(),
                        _ => "Không hỗ trợ định dạng này"
                    };

                    AddLog($"✅ Trích xuất: {RawText.Length} ký tự", "log-success");
                    Questions = ParseQuestions(RawText);
                    AddLog($"🎉 KẾT QUẢ: {Questions.Count} câu hỏi", Questions.Count > 0 ? "log-success" : "log-error");
                }
                catch (Exception ex)
                {
                    AddLog($"❌ LỖI: {ex.Message}", "log-error");
                    RawText = $"Lỗi: {ex.Message}";
                }
            }

            return Page();
        }

        private void AddLog(string message, string type = "")
        {
            Logs.Add(new LogItem { Message = message, Type = type });
        }

        private List<ExamQuestion> ParseQuestions(string text)
        {
            var questions = new List<ExamQuestion>();

            try
            {
                AddLog("🔍 Bắt đầu parse...", "");
                AddLog($"📄 Text gốc: {text.Length} ký tự", "");

                // Chuẩn hóa: thêm xuống dòng
                text = text.Replace("\r\n", "\n").Replace("\r", "\n");

                // Thêm xuống dòng trước số câu hỏi (trừ câu đầu)
                text = Regex.Replace(text, @"(\d+)\.\s*([^\d])", "\n$1. $2");

                // Thêm xuống dòng trước đáp án
                text = Regex.Replace(text, @"([A-D])\.\s*", "\n$1. ");

                // Loại bỏ dòng trống thừa
                text = Regex.Replace(text, @"\n{3,}", "\n\n");

                AddLog($"✨ Text sau chuẩn hóa: {text.Length} ký tự", "log-success");

                var questionBlocks = Regex.Split(text, @"(?=\n\d+\.\s)");
                AddLog($"📦 Tìm thấy {questionBlocks.Length} blocks", "");

                foreach (var block in questionBlocks)
                {
                    if (string.IsNullOrWhiteSpace(block)) continue;

                    var lines = block.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                    if (!lines.Any()) continue;

                    var firstLine = lines[0];
                    var qNumMatch = Regex.Match(firstLine, @"^(\d+)\.\s*(.*)");

                    if (!qNumMatch.Success) continue;

                    var questionNum = qNumMatch.Groups[1].Value;
                    var questionText = qNumMatch.Groups[2].Value.Trim();
                    var fullQuestion = questionText;
                    var options = new List<string>();

                    for (int i = 1; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        var optMatch = Regex.Match(line, @"^([A-D])\.\s*(.+)$");

                        if (optMatch.Success)
                        {
                            var optText = optMatch.Groups[2].Value.Trim();
                            if (!string.IsNullOrEmpty(optText))
                            {
                                options.Add(optText);
                            }
                        }
                        else if (options.Count == 0)
                        {
                            fullQuestion += " " + line;
                        }
                    }

                    if (options.Count >= 2)
                    {
                        questions.Add(new ExamQuestion
                        {
                            Question = $"{questionNum}. {fullQuestion.Trim()}",
                            Options = options,
                            CorrectAnswer = options[0]
                        });

                        AddLog($"✅ Câu {questionNum}: {options.Count} đáp án", "log-success");
                    }
                    else
                    {
                        AddLog($"❌ Câu {questionNum}: Chỉ có {options.Count} đáp án", "log-warning");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"💥 LỖI: {ex.Message}", "log-error");
            }

            return questions;
        }
    }
}