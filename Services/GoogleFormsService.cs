using Google.Apis.Auth.OAuth2;
using Google.Apis.Forms.v1;
using Google.Apis.Forms.v1.Data;
using Google.Apis.Services;
using ExamManager.Models;

namespace ExamManager.Services
{
    public class GoogleFormsService : IGoogleFormsService
    {
        private readonly ILogger<GoogleFormsService> _logger;

        public GoogleFormsService(ILogger<GoogleFormsService> logger)
        {
            _logger = logger;
        }

        public async Task<string> CreateFormAsync(string title, List<ExamQuestion> questions, string accessToken)
        {
            try
            {
                _logger.LogInformation("=== BẮT ĐẦU TẠO FORM ===");
                _logger.LogInformation($"📝 Title: {title}");
                _logger.LogInformation($"📊 Số câu hỏi: {questions.Count}");

                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Access token is null or empty");
                }

                var credential = GoogleCredential.FromAccessToken(accessToken);
                var service = new FormsService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Exam Grading App"
                });

                // Tạo form mới
                var form = new Form
                {
                    Info = new Info
                    {
                        Title = title,
                        DocumentTitle = title
                    }
                };

                var createRequest = service.Forms.Create(form);
                var createdForm = await createRequest.ExecuteAsync();
                _logger.LogInformation($"✅ Form đã tạo với ID: {createdForm.FormId}");

                // Kiểm tra có câu hỏi nào có đáp án không
                var hasCorrectAnswers = questions.Any(q => q.CorrectAnswerIndices.Any());

                // ✅ FIX 1: Nếu có đáp án, bật chế độ Quiz
                var requests = new List<Request>();

                if (hasCorrectAnswers)
                {
                    requests.Add(new Request
                    {
                        UpdateSettings = new UpdateSettingsRequest
                        {
                            Settings = new FormSettings
                            {
                                QuizSettings = new QuizSettings
                                {
                                    IsQuiz = true
                                }
                            },
                            UpdateMask = "quizSettings.isQuiz"
                        }
                    });
                    _logger.LogInformation("✅ Đã bật chế độ Quiz cho form");
                }

                var questionLimit = Math.Min(questions.Count, 50);

                // ✅ FIX 2: Duyệt NGƯỢC từ cuối lên đầu, nhưng vẫn thêm vào Index 0
                // Cách này đảm bảo thứ tự đúng: câu 1 -> câu 2 -> ... -> câu N
                for (int i = questionLimit - 1; i >= 0; i--)
                {
                    var question = questions[i];
                    var hasAnswer = question.CorrectAnswerIndices.Any();

                    // ✅ FIX 3: Xử lý cả câu hỏi đơn lựa chọn và nhiều lựa chọn
                    var questionType = question.CorrectAnswerIndices.Count > 1 ? "CHECKBOX" : "RADIO";

                    var item = new Item
                    {
                        Title = question.Question,
                        QuestionItem = new QuestionItem
                        {
                            Question = new Question
                            {
                                Required = true,
                                ChoiceQuestion = new ChoiceQuestion
                                {
                                    Type = questionType,
                                    Options = question.Options.Select(opt => new Option { Value = opt }).ToList()
                                }
                            }
                        }
                    };

                    // ✅ FIX 4: Nếu có đáp án, set grading
                    if (hasAnswer && hasCorrectAnswers)
                    {
                        item.QuestionItem.Question.Grading = new Grading
                        {
                            PointValue = 1,
                            CorrectAnswers = new CorrectAnswers
                            {
                                Answers = question.CorrectAnswerIndices
                                    .Where(idx => idx >= 0 && idx < question.Options.Count)
                                    .Select(idx => new CorrectAnswer
                                    {
                                        Value = question.Options[idx]
                                    })
                                    .ToList()
                            }
                        };

                        var answerList = string.Join(", ", question.CorrectAnswerIndices.Select(idx =>
                            idx < question.Options.Count ? question.Options[idx] : "?"));
                        _logger.LogInformation($"✅ Câu {i + 1}: {questionType} - Đáp án: {answerList}");
                    }
                    else
                    {
                        _logger.LogInformation($"⚠️ Câu {i + 1}: Không có đáp án");
                    }

                    requests.Add(new Request
                    {
                        CreateItem = new CreateItemRequest
                        {
                            Item = item,
                            Location = new Location { Index = 0 }
                        }
                    });
                }

                if (requests.Any())
                {
                    _logger.LogInformation($"📤 Đang thêm {requests.Count} requests (bao gồm settings + câu hỏi)...");

                    var batchUpdate = new BatchUpdateFormRequest { Requests = requests };
                    await service.Forms.BatchUpdate(batchUpdate, createdForm.FormId).ExecuteAsync();

                    _logger.LogInformation($"✅ Đã thêm thành công!");
                }

                var formUrl = $"https://docs.google.com/forms/d/{createdForm.FormId}/edit";
                _logger.LogInformation($"🎉 HOÀN THÀNH! URL: {formUrl}");

                return formUrl;
            }
            catch (Google.GoogleApiException apiEx)
            {
                _logger.LogError(apiEx, "❌ Google API Error");
                _logger.LogError($"Status: {apiEx.HttpStatusCode}");
                _logger.LogError($"Message: {apiEx.Message}");

                if (apiEx.Error?.Errors != null)
                {
                    foreach (var error in apiEx.Error.Errors)
                    {
                        _logger.LogError($"  - {error.Reason}: {error.Message}");
                    }
                }

                throw new Exception($"Lỗi Google API: {apiEx.Message}. Vui lòng đăng xuất và đăng nhập lại.", apiEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi chung khi tạo Google Form");
                throw new Exception($"Không thể tạo Google Form: {ex.Message}", ex);
            }
        }
    }
}