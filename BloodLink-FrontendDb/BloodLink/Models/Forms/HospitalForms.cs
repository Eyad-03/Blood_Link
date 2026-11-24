namespace BloodLink.Models.Forms
{
    // Hospital → Profile form.
    public class HospitalProfileForm
    {
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
    }

    // Hospital → "New donation record" form.
    public class DonationRecordForm
    {
        public string Donor { get; set; } = "";
        public string DonorEmail { get; set; } = "";
        public string Recipient { get; set; } = "";
        public string RecipientEmail { get; set; } = "";
        public string Type { get; set; } = "";
        public int Units { get; set; } = 1;
        public string Date { get; set; } = "";
        public string? Notes { get; set; }
    }

    // Hospital → Donor directory → "Send email".
    public class EmailDonorsForm
    {
        public List<string> Emails { get; set; } = new();
        public string? BloodType { get; set; }             // the blood type the hospital needs (optional)
    }

    // Hospital → new advertisement.
    public class AdForm
    {
        public string Title { get; set; } = "";
        public string Department { get; set; } = "";
        public string Category { get; set; } = "";
        public string? Badge { get; set; }
        public string? Description { get; set; }
        public string EventDate { get; set; } = "";
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Location { get; set; }
        public string? Phone { get; set; }
        public bool Booking { get; set; }
        public string Plan { get; set; } = "basic";        // basic | featured | premium
        public string StartDate { get; set; } = "";
    }
}
