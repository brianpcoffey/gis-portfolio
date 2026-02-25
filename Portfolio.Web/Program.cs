using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Portfolio.Repositories;
using Portfolio.Repositories.Interfaces;
using Portfolio.Repositories.Repositories;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;

var builder = WebApplication.CreateBuilder(args);

// --------------------------
// Razor Pages & Services
// --------------------------
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

// --------------------------
// Dependency Injection
// --------------------------
builder.Services.AddScoped<ISavedFeatureRepository, SavedFeatureRepository>();
builder.Services.AddScoped<IUserNoteRepository, UserNoteRepository>();
builder.Services.AddScoped<ISavedFeatureService, SavedFeatureService>();
builder.Services.AddScoped<IArcGisService, ArcGisService>();
builder.Services.AddHttpClient<IArcGisService, ArcGisService>();

// Collections repository & service registrations
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddScoped<ICollectionService, CollectionService>();

// --------------------------
// Session
// --------------------------
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
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
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
builder.Services.AddControllers();

// --------------------------
// Swagger / API Explorer
// --------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --------------------------
// Database Context
// --------------------------
builder.Services.AddDbContext<PortfolioDbContext>(options =>
      options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// --------------------------
// Swagger UI
// --------------------------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Portfolio API V1");
});

// --------------------------
// Map endpoints
// --------------------------
app.MapControllers();
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
    db.Database.Migrate();
}

app.Run();
