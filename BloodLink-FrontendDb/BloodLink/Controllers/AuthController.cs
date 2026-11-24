using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BloodLink.Controllers
{
    // Sign up and sign in with a JWT token.
    //   POST /api/auth/register   create an account  (register.js)
    //   POST /api/auth/login      sign in            (login.js)
    //   GET  /api/auth/me         who am I?
    //   GET  /api/auth/google-config   the Google Client ID for the page (google-auth.js)
    //   POST /api/auth/google          sign in / sign up with Google      (google-auth.js)
    //
    // How JWT works here:
    //   1. login → we check the password → we create a token (Helpers/JwtHelper.cs) and send it.
    //   2. api.js saves the token in localStorage.
    //   3. Every next request has the header "Authorization: Bearer <token>".
    //   4. ASP.NET reads the token (Program.cs → AddJwtBearer), so [Authorize] knows who you are.
    public class AuthController : BaseController
    {
        private readonly IConfiguration config;
        private readonly IHttpClientFactory httpFactory;
        private readonly PasswordHasher<User> hasher = new();

        public AuthController(BloodLinkDbContext db, IConfiguration config, IHttpClientFactory httpFactory) : base(db)
        {
            this.config = config;
            this.httpFactory = httpFactory;
        }

        [HttpPost("/api/auth/register")]
        public IActionResult Register([FromBody] RegisterForm form)
        {
            if (form == null) return Error("Please fill in the form.");
            string email = form.Email.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(form.FullName) || email == "")
                return Error("Please fill in your name and email.");
            if (form.Password.Length < 8)
                return new JsonResult(new { message = "Password must be at least 8 characters.", field = "password" }) { StatusCode = 400 };
            if (db.Users.Any(u => u.Email == email))
                return new JsonResult(new { message = "An account with this email already exists.", field = "email" }) { StatusCode = 409 };

            var user = new User { FullName = form.FullName.Trim(), Email = email, Status = AccountStatus.Active };

            SetRole(user, form);   // donor / requester / hospital

            // Never save the password itself, only its hash.
            user.PasswordHash = hasher.HashPassword(user, form.Password);

            db.Users.Add(user);
            db.SaveChanges();

            // Sign the new user in straight away.
            return Json(LoginResult(user));
        }

        [HttpPost("/api/auth/login")]
        public IActionResult Login([FromBody] LoginForm form)
        {
            if (form == null) return Error("Please enter your email and password.");
            string email = form.Email.Trim().ToLower();
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            // Same message for "wrong email" and "wrong password" (safer).
            if (user == null || hasher.VerifyHashedPassword(user, user.PasswordHash, form.Password) == PasswordVerificationResult.Failed)
                return Error("Incorrect email or password.", 401);

            if (user.Status == AccountStatus.Suspended)
                return Error("Your account is suspended. Please contact BloodLink support.", 403);

            user.LastActiveAt = DateTime.UtcNow;
            db.SaveChanges();

            return Json(LoginResult(user));
        }

        [Authorize]
        [HttpGet("/api/auth/me")]
        public IActionResult Me()
        {
            var user = CurrentUser();
            if (user == null) return Error("Please sign in.", 401);
            return Json(new { id = user.Id, name = user.FullName, email = user.Email, role = user.Role.ToString() });
        }

        // ===================== Google sign-in =====================
        // Steps:
        //   1. The page asks for our Google Client ID        → GoogleConfig()
        //   2. Google shows its button, the user picks an account,
        //      and Google gives the page a "credential" (a token).
        //   3. The page sends the credential here             → Google()
        //   4. We ask Google "is this token real?" (tokeninfo URL).
        //   5. Email found → sign in.  Not found → create the account (Register page only).

        // The Client ID is not secret, the page needs it to draw the Google button.
        // Settings: appsettings.json → Google:ClientId   (empty = Google button hidden)
        [HttpGet("/api/auth/google-config")]
        public IActionResult GoogleConfig()
        {
            return Json(new { clientId = config["Google:ClientId"] ?? "" });
        }

        [HttpPost("/api/auth/google")]
        public async Task<IActionResult> Google([FromBody] GoogleForm form)
        {
            string clientId = config["Google:ClientId"] ?? "";
            if (clientId == "") return Error("Google sign-in is not set up.");
            if (form == null || form.Credential == "") return Error("Google did not send a sign-in token.");

            // ---- Step 4: ask Google to check the token ----
            var http = httpFactory.CreateClient();
            var answer = await http.GetAsync("https://oauth2.googleapis.com/tokeninfo?id_token=" + Uri.EscapeDataString(form.Credential));
            if (!answer.IsSuccessStatusCode) return Error("Google sign-in failed. Please try again.", 401);

            var info = await answer.Content.ReadFromJsonAsync<GoogleTokenInfo>();
            // The token must be made for OUR app (aud = our Client ID) and the email must be verified.
            if (info == null || info.aud != clientId || info.email_verified != "true" || string.IsNullOrEmpty(info.email))
                return Error("Google sign-in failed. Please try again.", 401);

            string email = info.email.Trim().ToLower();

            // ---- Step 5a: the account already exists → sign in ----
            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                if (user.Status == AccountStatus.Suspended)
                    return Error("Your account is suspended. Please contact BloodLink support.", 403);

                user.LastActiveAt = DateTime.UtcNow;
                db.SaveChanges();
                return Json(LoginResult(user));
            }

            // ---- Step 5b: no account yet ----
            // From the Login page we don't know the role, so we ask the user to register first.
            if (string.IsNullOrEmpty(form.Role))
                return Error("No BloodLink account uses this Google email. Please create an account first.", 404);

            // From the Register page → create the account with the chosen role.
            user = new User
            {
                FullName = string.IsNullOrWhiteSpace(info.name) ? email.Split('@')[0] : info.name,
                Email = email,
                Status = AccountStatus.Active,
                LastActiveAt = DateTime.UtcNow
            };
            SetRole(user, form);

            // Google users sign in with Google, so they get a random password nobody knows.
            // (They can still use "forgot password" later if you add it.)
            user.PasswordHash = hasher.HashPassword(user, Guid.NewGuid().ToString());

            db.Users.Add(user);
            db.SaveChanges();
            return Json(LoginResult(user));
        }

        // The fields we read from Google's answer (names must match Google's JSON).
        private class GoogleTokenInfo
        {
            public string? aud { get; set; }             // which app the token is for
            public string? email { get; set; }
            public string? email_verified { get; set; }  // "true" or "false"
            public string? name { get; set; }
        }

        // Fills the role part of a new user (used by Register and by Google sign-up).
        private static void SetRole(User user, RegisterForm form)
        {
            if (form.Role == "requester")
            {
                // Needs blood
                user.Role = UserRole.Recipient;
                user.BloodType = Format.ParseBlood(form.RequiredBloodType);
                user.City = form.RequestCity;
            }
            else if (form.Role == "hospital")
            {
                // A hospital account = a new organization (waiting for the admin) + a staff user.
                var org = new Organization
                {
                    Name = string.IsNullOrWhiteSpace(form.InstitutionName) ? user.FullName : form.InstitutionName.Trim(),
                    City = form.InstitutionCity ?? "",
                    Email = user.Email,
                    ContactPerson = user.FullName,
                    Status = OrganizationStatus.Pending
                };
                // The three documents the hospital must upload and the admin must check.
                org.Documents.Add(new OrganizationDocument { Name = "Ministry of Health operating license", FilePath = "not-uploaded-yet" });
                org.Documents.Add(new OrganizationDocument { Name = "Registration certificate", FilePath = "not-uploaded-yet" });
                org.Documents.Add(new OrganizationDocument { Name = "Authorized representative ID", FilePath = "not-uploaded-yet" });

                user.Role = UserRole.Hospital;
                user.City = org.City;
                user.Organization = org;
            }
            else
            {
                user.Role = UserRole.Donor;
                user.IsAvailable = true;
            }
        }

        // What the browser gets after login or register: the token, the user and the page to open.
        private object LoginResult(User user)
        {
            return new
            {
                token = JwtHelper.CreateToken(user, config),
                redirect = HomePageFor(user.Role),
                user = new { id = user.Id, name = user.FullName, email = user.Email, role = user.Role.ToString() }
            };
        }

        // The page each role lands on after signing in.
        private static string HomePageFor(UserRole role)
        {
            if (role == UserRole.Donor) return "/Donor/Requests";
            if (role == UserRole.Recipient) return "/Donor/Search";
            if (role == UserRole.Hospital) return "/Hospital";
            return "/Admin";
        }
    }
}
