namespace BloodLink.Models.Forms
{
    // Profile page: edit personal details.
    public class ProfileForm
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string? Location { get; set; }
    }

    // Donor availability switch.
    public class AvailabilityForm
    {
        public bool Available { get; set; }
    }

    // Donate page: "Submit donor offer" form (donate.js).
    public class DonorOfferForm
    {
        public string FullName { get; set; } = "";
        public string BloodType { get; set; } = "";
        public string Phone { get; set; } = "";
        public string City { get; set; } = "";             // "Irbid, University Street"
        public string? ContactMethod { get; set; }         // phone | sms | email
        public bool ShareExactLocation { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
