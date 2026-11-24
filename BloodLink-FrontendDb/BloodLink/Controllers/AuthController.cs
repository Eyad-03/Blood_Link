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
    //
    // How JWT works here:
    //   1. login → we check the password → we create a token (Helpers/JwtHelper.cs) and send it.
    //   2. api.js saves the token in localStorage.
    //   3. Every next request has the header "Authorization: Bearer <token>".
    //   4. ASP.NET reads the token (Program.cs → AddJwtBearer), so [Authorize] knows who you are.
    public class AuthController : BaseController
    {
        private readonly IConfiguration config;
        private readonly PasswordHasher<User> hasher = new();

        public AuthController(BloodLinkDbContext db, IConfiguration config) : base(db)
        {
            this.config = config;
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
                    Email = email,
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
