using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using ExamManager.Services;
using ExamManager.Models;
using System.Text.RegularExpressions;

namespace ExamManager.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IOcrService _ocrService;
        private readonly IPdfReader _pdfReader;
        private readonly IDocxReader _docxReader;
        private readonly IGoogleFormsService _formsService;
        private readonly ILogger<IndexModel> _logger;

        public string ExtractedText { get; set; } = string.Empty;
        public string FormUrl { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsProcessing { get; set; }
        public int QuestionCount { get; set; }

        public IndexModel(
            IOcrService ocrService,
            IPdfReader pdfReader,
            IDocxReader docxReader,
            IGoogleFormsService formsService,
            ILogger<IndexModel> logger)
        {
            _ocrService = ocrService;
            _pdfReader = pdfReader;
            _docxReader = docxReader;
            _formsService = formsService;
            _logger = logger;
        }

        public void OnGet()
        {
            if (TempData["ExtractedText"] != null)
                ExtractedText = TempData["ExtractedText"]?.ToString() ?? string.Empty;

            if (TempData["FormUrl"] != null)
                FormUrl = TempData["FormUrl"]?.ToString() ?? string.Empty;

            if (TempData["ErrorMessage"] != null)
                ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty;

            if (TempData["QuestionCount"] != null)
                QuestionCount = (int)(TempData["QuestionCount"] ?? 0);
        }

        public async Task<IActionResult> OnPostUploadAsync(string formTitle, List<IFormFile> files)
        {
            try
            {
                _logger.LogInformation("Bắt đầu xử lý upload");

                if (string.IsNullOrWhiteSpace(formTitle))
                {
                    TempData["ErrorMessage"] = "Vui lòng nhập tiêu đề đề thi";
                    return Page();
                }

                if (files == null || !files.Any())
                {
                    TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một file";
                    return Page();
                }

                var allText = string.Empty;

                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        _logger.LogInformation($"Đang xử lý file: {file.FileName}");

                        using var stream = new MemoryStream();
                        await file.CopyToAsync(stream);
                        stream.Position = 0;

                        var extension = Path.GetExtension(file.FileName).ToLower();

                        try
                        {
                            var text = extension switch
                            {
                                ".jpg" or ".jpeg" or ".png" => await _ocrService.ExtractTextFromImageAsync(stream),
                                ".pdf" => await _pdfReader.ExtractTextAsync(stream),
                                ".docx" => await _docxReader.ExtractTextAsync(stream),
                                _ => string.Empty
                            };

                            allText += text + "\n\n";
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Lỗi khi xử lý file {file.FileName}");
                            TempData["ErrorMessage"] = $"Lỗi khi xử lý file {file.FileName}: {ex.Message}";
                            return Page();
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(allText))
                {
                    TempData["ErrorMessage"] = "Không thể trích xuất text từ các file.";
                    return Page();
                }

                TempData["ExtractedText"] = allText.Trim();
                _logger.LogInformation($"Đã trích xuất: {allText.Length} ký tự");

                var (questions, answerKey) = ParseQuestionsWithAnswers(allText);
                _logger.LogInformation($"✅ Parse xong: {questions.Count} câu hỏi, {answerKey.Count} đáp án");

                TempData["QuestionCount"] = questions.Count;

                if (!questions.Any())
                {
                    TempData["ErrorMessage"] = "Không parse được câu hỏi. Vui lòng kiểm tra:\n" +
                        "✓ Câu hỏi có số thứ tự: 1. 2. 3.\n" +
                        "✓ Đáp án có chữ cái: A. B. C. D.";
                    return Page();
                }

                var questionsWithAnswers = questions.Count(q => q.CorrectAnswerIndices.Any());
                if (questionsWithAnswers > 0)
                {
                    _logger.LogInformation($"✅ Có {questionsWithAnswers}/{questions.Count} câu có đáp án");
                    TempData["SuccessMessage"] = $"✅ Tạo form thành công với {questionsWithAnswers}/{questions.Count} câu có đáp án tự động chấm!";
                }
                else
                {
                    _logger.LogWarning("⚠️ Không tìm thấy đáp án nào - Form sẽ là dạng khảo sát thường");
                    TempData["WarningMessage"] = $"⚠️ Không tìm thấy đáp án trong file. Form sẽ không tự động chấm điểm.";
                }

                var accessToken = await HttpContext.GetTokenAsync("access_token");

                if (string.IsNullOrEmpty(accessToken))
                {
                    TempData["ErrorMessage"] = "Token hết hạn. Vui lòng đăng xuất và đăng nhập lại.";
                    return Page();
                }

                try
                {
                    var formUrl = await _formsService.CreateFormAsync(formTitle, questions, accessToken);
                    TempData["FormUrl"] = formUrl;
                    TempData["ErrorMessage"] = null;
                    _logger.LogInformation($"✅ Đã tạo form: {formUrl}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi tạo form");
                    TempData["ErrorMessage"] = $"Lỗi tạo Google Form: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tổng thể");
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
            }

            return Page();
        }

        private (List<ExamQuestion> questions, Dictionary<int, string> answerKey) ParseQuestionsWithAnswers(string text)
        {
            var questions = new List<ExamQuestion>();
            var answerKey = new Dictionary<int, string>();

            try
            {
                _logger.LogInformation("🔍 BẮT ĐẦU PARSE CÂU HỎI VÀ ĐÁP ÁN...");
                _logger.LogInformation($"📄 Text length: {text.Length} chars");

                // ✅ BƯỚC 1: Loại bỏ text "(Nhiều đáp án)" để tránh nhầm lẫn
                text = Regex.Replace(text, @"\s*\(Nhiều đáp án\)\s*", " ", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\s*\(Multiple answers?\)\s*", " ", RegexOptions.IgnoreCase);

                // Chuẩn hóa: thêm xuống dòng
                text = text.Replace("\r\n", "\n").Replace("\r", "\n");

                // Thêm xuống dòng trước số câu hỏi (trừ câu đầu)
                text = Regex.Replace(text, @"(\d+)\.\s*([^\d])", "\n$1. $2");

                // Thêm xuống dòng trước đáp án
                text = Regex.Replace(text, @"([A-D])\.\s*", "\n$1. ");

                // Loại bỏ dòng trống thừa
                text = Regex.Replace(text, @"\n{3,}", "\n\n");

                _logger.LogInformation($"✨ Text sau chuẩn hóa: {text.Length} ký tự");

                // ✅ BƯỚC 2: TÌM PHẦN ĐÁP ÁN (tìm pattern có số để tránh nhầm với text trong câu hỏi)
                // Pattern: "Đáp án: 1-B" hoặc "Answer key: 1-A" (phải có số ngay sau dấu hai chấm)
                string questionText = text;
                string answerText = "";
                int answerStartIndex = -1;

                // Tìm tất cả các vị trí có "đáp án:" hoặc "answer key:"
                var matches = Regex.Matches(text, @"(?i)(đáp\s*án|answer\s*key)\s*:", RegexOptions.Multiline);

                foreach (Match match in matches)
                {
                    // Kiểm tra xem sau "đáp án:" có phải là pattern "số-chữ cái" không
                    var afterColon = text.Substring(match.Index + match.Length).TrimStart();
                    if (Regex.IsMatch(afterColon, @"^\s*\d+\s*[-:]\s*[A-D]", RegexOptions.IgnoreCase))
                    {
                        // Đây là phần đáp án thật sự
                        answerStartIndex = match.Index;
                        _logger.LogInformation($"✅ Tìm thấy phần đáp án tại vị trí {answerStartIndex}");
                        break;
                    }
                    else
                    {
                        _logger.LogInformation($"⚠️ Bỏ qua 'đáp án' tại vị trí {match.Index} (không phải answer key)");
                    }
                }

                if (answerStartIndex >= 0)
                {
                    questionText = text.Substring(0, answerStartIndex).Trim();
                    answerText = text.Substring(answerStartIndex).Trim();

                    // Loại bỏ phần "Đáp án:" khỏi answerText
                    answerText = Regex.Replace(answerText, @"(?i)^(đáp\s*án|answer\s*key)\s*:\s*", "", RegexOptions.Multiline);

                    _logger.LogInformation($"✅ Phần đáp án: {answerText.Length} ký tự");
                    _logger.LogInformation($"📋 Preview: {answerText.Substring(0, Math.Min(150, answerText.Length))}...");

                    // ✅ PARSE ĐÁP ÁN: Format "1-B, 2-A, 3-C và D"
                    var answerMatches = Regex.Matches(answerText,
                        @"(\d+)\s*[-:]\s*([A-D](?:\s*(?:và|and|,|，)\s*[A-D])*)",
                        RegexOptions.IgnoreCase);

                    if (answerMatches.Count > 0)
                    {
                        _logger.LogInformation($"📝 Phát hiện {answerMatches.Count} đáp án");

                        foreach (Match match in answerMatches)
                        {
                            var questionNum = int.Parse(match.Groups[1].Value);
                            var answerStr = match.Groups[2].Value.Trim();

                            // Trích xuất TẤT CẢ chữ cái A-D
                            var letters = Regex.Matches(answerStr, @"[A-D]", RegexOptions.IgnoreCase)
                                .Cast<Match>()
                                .Select(m => m.Value.ToUpper())
                                .Distinct()
                                .OrderBy(x => x)
                                .ToList();

                            if (letters.Any())
                            {
                                answerKey[questionNum] = string.Join(",", letters);
                                _logger.LogInformation($"  ✓ Câu {questionNum}: {string.Join(",", letters)} (từ '{answerStr}')");
                            }
                        }
                    }

                    _logger.LogInformation($"✅ Parse được {answerKey.Count} đáp án");
                }
                else
                {
                    _logger.LogWarning("⚠️ Không tìm thấy phần đáp án hợp lệ");
                }

                // ✅ BƯỚC 3: PARSE CÂU HỎI
                _logger.LogInformation("🔍 Bắt đầu parse câu hỏi...");
                _logger.LogInformation($"Question text {questionText}");
                var questionBlocks = Regex.Split(questionText, @"(?=\n\d+\.\s)");
                _logger.LogInformation($"📦 Tìm thấy {questionBlocks.Length} blocks");

                foreach (var block in questionBlocks)
                {
                    if (string.IsNullOrWhiteSpace(block)) continue;

                    var lines = block.Split('\n')
                        .Select(l => l.Trim())
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();

                    if (!lines.Any()) continue;

                    var firstLine = lines[0];
                    var qNumMatch = Regex.Match(firstLine, @"^(\d+)\.\s*(.*)");

                    if (!qNumMatch.Success)
                    {
                        _logger.LogWarning($"⚠️ Không match: {firstLine.Substring(0, Math.Min(50, firstLine.Length))}");
                        continue;
                    }

                    var questionNum = int.Parse(qNumMatch.Groups[1].Value);
                    var questionTextLine = qNumMatch.Groups[2].Value.Trim();
                    var fullQuestion = questionTextLine;
                    var options = new List<string>();

                    for (int i = 1; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        var optMatch = Regex.Match(line, @"^([A-D])\.\s*(.+)$", RegexOptions.IgnoreCase);

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
                        var correctAnswers = new List<string>();
                        var correctIndices = new List<int>();

                        if (answerKey.ContainsKey(questionNum))
                        {
                            var answerStr = answerKey[questionNum];
                            var answerLetters = answerStr.Split(',').Select(a => a.Trim()).ToList();

                            foreach (var letter in answerLetters)
                            {
                                int index = letter switch
                                {
                                    "A" => 0,
                                    "B" => 1,
                                    "C" => 2,
                                    "D" => 3,
                                    _ => -1
                                };

                                if (index >= 0 && index < options.Count)
                                {
                                    correctIndices.Add(index);
                                    correctAnswers.Add(options[index]);
                                }
                            }
                        }

                        var singleCorrectIndex = correctIndices.FirstOrDefault(-1);
                        var singleCorrectAnswer = correctAnswers.FirstOrDefault("");

                        questions.Add(new ExamQuestion
                        {
                            Question = $"{questionNum}. {fullQuestion.Trim()}",
                            Options = options,
                            CorrectAnswer = singleCorrectAnswer,
                            CorrectAnswerIndex = singleCorrectIndex,
                            CorrectAnswerIndices = correctIndices
                        });

                        var answerInfo = correctIndices.Any()
                            ? $"(Đáp án: {answerKey[questionNum]}{(correctIndices.Count > 1 ? " - NHIỀU LỰA CHỌN" : "")})"
                            : "(Chưa có đáp án)";
                        _logger.LogInformation($"✅ Câu {questionNum}: {options.Count} options {answerInfo}");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ Câu {questionNum}: Chỉ có {options.Count} options");
                    }
                }

                _logger.LogInformation($"🎉 HOÀN THÀNH: {questions.Count} câu hỏi, {answerKey.Count} đáp án");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 LỖI PARSE");
            }

            return (questions, answerKey);
        }
    }
}