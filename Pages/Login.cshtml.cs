using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExamManager.Pages
{
    public class LoginModel : PageModel
    {
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(ILogger<LoginModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                return RedirectToPage("/Index");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return Page();
        }

        public IActionResult OnPostGoogleLogin(string? returnUrl = null)
        {
            _logger.LogInformation("Bắt đầu Google OAuth flow");

            var redirectUrl = Url.Page("/Index");
            if (!string.IsNullOrEmpty(returnUrl))
            {
                redirectUrl = returnUrl;
            }

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl,
                Items =
                {
                    { "LoginProvider", GoogleDefaults.AuthenticationScheme }
                }
            };

            _logger.LogInformation($"Redirect URI: {redirectUrl}");

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }
    }
}