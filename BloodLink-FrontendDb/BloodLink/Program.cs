using System.Text;
using BloodLink.Data;
using BloodLink.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// MVC: Controllers + Views.
//   - Page controllers (HomeController, AccountController, ...) return the Views.
//   - Data controllers (AuthController, RequestsController, ...) return JSON to the JavaScript pages.
builder.Services.AddControllersWithViews();

// Database (SQL Server). Connection string: appsettings.json → ConnectionStrings:DefaultConnection
builder.Services.AddDbContext<BloodLinkDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- JWT authentication ----------
// The browser gets a token from POST /api/auth/login and sends it back in the header
// "Authorization: Bearer <token>". Here we tell ASP.NET how to check that token.
// Settings: appsettings.json → Jwt
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
builder.Services.AddAuthorization();

// HttpClient for the chatbot (ChatbotController talks to Ollama).
builder.Services.AddHttpClient();

// Sends emails with SMTP (Helpers/EmailSender.cs, settings: appsettings.json → Email).
builder.Services.AddSingleton<EmailSender>();

var app = builder.Build();

// Create / update the database, add the demo accounts, and turn on donors whose 3 months are over.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
    db.Database.Migrate();
    DbInitializer.Seed(db);
    DonationRules.ReactivateDonors(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();   // reads the JWT token (must come before UseAuthorization)
app.UseAuthorization();    // checks [Authorize] and [Authorize(Roles = "...")]

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
