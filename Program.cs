using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using ExamManager.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port from environment variable (Render uses PORT)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

// Đăng ký services
builder.Services.AddSingleton<IOcrService, TesseractOcrService>();
builder.Services.AddSingleton<IPdfReader, PdfReaderService>();
builder.Services.AddSingleton<IDocxReader, DocxReaderService>();
builder.Services.AddScoped<IGoogleFormsService, GoogleFormsService>();

// Configure authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;

    // Configure cookie for production
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
        ?? throw new InvalidOperationException("Google ClientId not configured");
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
        ?? throw new InvalidOperationException("Google ClientSecret not configured");

    // Thêm scope có quyền tạo và chỉnh sửa form
    options.Scope.Add("https://www.googleapis.com/auth/forms.body");
    options.Scope.Add("https://www.googleapis.com/auth/drive.file");

    // Lưu token để sử dụng sau
    options.SaveTokens = true;

    // Xử lý lỗi
    options.Events.OnRemoteFailure = context =>
    {
        var errorMessage = context.Failure?.Message ?? "Unknown error";
        context.Response.Redirect("/Login?error=" + Uri.EscapeDataString(errorMessage));
        context.HandleResponse();
        return Task.CompletedTask;
    };
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddRazorPages()
    .AddSessionStateTempDataProvider();

// Add antiforgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Configure forwarded headers for reverse proxy (Render)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// HTTPS handling: Skip redirect in production (Render handles SSL termination)
if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("Environment is Development — enabling HTTPS redirection.");
    app.UseHttpsRedirection();
}
else
{
    app.Logger.LogInformation("Production environment — skipping HTTPS redirection (handled by Render).");
}

app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Log startup information
app.Logger.LogInformation("Application starting on port {Port}", port);
app.Logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

app.Run();