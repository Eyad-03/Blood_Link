namespace BloodLink.Models.Forms
{
    // Recipient sends a blood request to a donor (Donor → Search page).
    public class BloodRequestForm
    {
        public int DonorId { get; set; }
        public string? BloodType { get; set; }        // the blood type the recipient needs
        public string Priority { get; set; } = "Normal";
        public int HospitalId { get; set; }            // where the donation will happen
    }

    // Answer to a request: Accepted / Declined / Completed.
    public class StatusForm
    {
        public string Status { get; set; } = "";
    }

    // New chat message.
    public class ChatMessageForm
    {
        public string Text { get; set; } = "";
    }
}
