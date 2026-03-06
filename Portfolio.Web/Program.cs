using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Configuration;
using Portfolio.Common.DTOs;
using Portfolio.Repositories;
using Portfolio.Repositories.Interfaces;
using Portfolio.Repositories.Repositories;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.Middleware;
using System.Reflection;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// --------------------------
// Razor Pages & Services
// --------------------------
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

// --------------------------
// Dependency Injection
// --------------------------
builder.Services.AddScoped<ISavedFeatureRepository, SavedFeatureRepository>();
builder.Services.AddScoped<ISavedFeatureService, SavedFeatureService>();
builder.Services.AddHttpClient<IArcGisService, ArcGisService>();
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();

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
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    options.Events = new CookieAuthenticationEvents
    {
        OnSignedIn = async context =>
        {
            var principal = context.Principal;
            if (principal?.Identity?.IsAuthenticated != true)
                return;

            var googleId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.FindFirstValue(ClaimTypes.Email);
            var name = principal.FindFirstValue(ClaimTypes.Name);
            var picture = principal.FindFirstValue("picture");

            if (string.IsNullOrEmpty(googleId))
                return;

            var profileService = context.HttpContext.RequestServices
                .GetRequiredService<IUserProfileService>();

            var userId = await profileService.CreateOrUpdateFromGoogleAsync(new GoogleProfileDto
            {
                GoogleId = googleId,
                Email = email ?? string.Empty,
                Name = name ?? string.Empty,
                Picture = picture
            });

            context.HttpContext.Response.Cookies.Append("AnonUserId", userId.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true
            });

            context.HttpContext.Items["AnonUserId"] = userId;
        }
    };
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("Admin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("role", "admin"));
});

builder.Services.AddControllers();

// --------------------------
// Swagger / API Explorer
// --------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    options.AddSecurityDefinition("cookieAuth", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Cookie",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
        Description = "Cookie-based authentication"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "cookieAuth"
                }
            },
            Array.Empty<string>()
        }
    });
});

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

app.UseMiddleware<AnonymousUserMiddleware>();

// --------------------------
// Swagger UI
// --------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Portfolio API V1");
    });
}

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
