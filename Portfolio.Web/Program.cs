using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);

// --------------------------
// Razor Pages & Services
// --------------------------
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

// Optional: Session (needed if you want to clear session on logout)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// --------------------------
// Authentication & Authorization
// --------------------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; // "Cookies"
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
})
.AddGoogle(options =>
{
    var googleSettings = builder.Configuration.GetSection("Authentication:Google")
        .Get<GoogleAuthSettings>()
        ?? throw new InvalidOperationException("Google auth configuration missing.");

    options.ClientId = googleSettings.ClientId;
    options.ClientSecret = googleSettings.ClientSecret;
    options.CallbackPath = "/signin-google";
    options.SaveTokens = true;
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.ClaimActions.MapJsonKey("picture", "picture");
});

builder.Services.AddAuthorization();

// Swagger for API testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --------------------------
// Middleware
// --------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Session must come **before** Authentication/Authorization if you use session
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Map Razor Pages
app.MapRazorPages();

app.Run();