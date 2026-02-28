using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Portfolio.Repositories;
using Portfolio.Repositories.Interfaces;
using Portfolio.Repositories.Repositories;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.Middleware;

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
builder.Services.AddScoped<ISavedFeatureService, SavedFeatureService>();
builder.Services.AddScoped<IArcGisService, ArcGisService>();
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
})
.AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy =>
        policy.RequireAuthenticatedUser());
    // Add more policies as needed
});

builder.Services.AddControllers();

// --------------------------
// Swagger / API Explorer
// --------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Enable XML comments for controllers and models
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    // Add security definition for cookie authentication
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

// Insert anonymous user middleware BEFORE controllers/pages so the profile is available
app.UseMiddleware<AnonymousUserMiddleware>();

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
