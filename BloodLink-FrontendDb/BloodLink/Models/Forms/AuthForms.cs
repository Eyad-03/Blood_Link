namespace BloodLink.Models.Forms
{
    // Data sent by the Register page (register.js).
    public class RegisterForm
    {
        public string Role { get; set; } = "";              // donor | requester | hospital
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";

        // Only for "requester" (needs blood)
        public string? RequiredBloodType { get; set; }
        public string? RequestCity { get; set; }

        // Only for "hospital"
        public string? InstitutionName { get; set; }
        public string? InstitutionCity { get; set; }
    }

    // Data sent by the Login page (login.js).
    public class LoginForm
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
