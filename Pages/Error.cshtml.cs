using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExamManager.Pages
{
    public class ErrorModel : PageModel
    {
        public string ErrorMessage { get; set; } = "Đã xảy ra lỗi không xác định";

        public void OnGet(string? message = null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                ErrorMessage = message;
            }
        }
    }
}