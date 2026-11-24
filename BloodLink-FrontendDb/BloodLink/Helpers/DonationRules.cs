using BloodLink.Data;
using BloodLink.Models.Entities;

namespace BloodLink.Helpers
{
    // The rest rule after a donation, in one place:
    //   donate → not available → 3 months later → available again.
    public static class DonationRules
    {
        public const int RestMonths = 3;

        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

        // Call this every time a donation is recorded for a donor.
        public static void MarkDonated(User donor, DateOnly donationDate)
        {
            if (donor.LastDonationDate == null || donationDate > donor.LastDonationDate)
                donor.LastDonationDate = donationDate;

            var availableAgain = donor.LastDonationDate.Value.AddMonths(RestMonths);
            if (availableAgain > Today)
            {
                donor.IsAvailable = false;
                donor.AvailableAgainOn = availableAgain;
            }
        }

        // Is the donor still in the 3-month rest period?
        public static bool IsResting(User donor)
        {
            return donor.AvailableAgainOn != null && donor.AvailableAgainOn > Today;
        }

        // Used after "undo donation": calculate the rest period again from the donations that are left.
        public static void Recalculate(User donor, BloodLinkDbContext db)
        {
            var last = db.DonationRecords
                .Where(r => r.DonorId == donor.Id)
                .OrderByDescending(r => r.DonationDate)
                .Select(r => (DateOnly?)r.DonationDate)
                .FirstOrDefault();

            donor.LastDonationDate = last;
            if (last != null && last.Value.AddMonths(RestMonths) > Today)
            {
                donor.IsAvailable = false;
                donor.AvailableAgainOn = last.Value.AddMonths(RestMonths);
            }
            else if (donor.AvailableAgainOn != null)
            {
                donor.IsAvailable = true;
                donor.AvailableAgainOn = null;
            }
        }

        // Turns back on every donor whose 3 months are over.
        // Called when the site starts, and when the donors list / a profile is loaded.
        public static void ReactivateDonors(BloodLinkDbContext db)
        {
            var today = Today;
            var donors = db.Users
                .Where(u => u.AvailableAgainOn != null && u.AvailableAgainOn <= today)
                .ToList();

            foreach (var donor in donors)
            {
                donor.IsAvailable = true;
                donor.AvailableAgainOn = null;
            }
            if (donors.Count > 0) db.SaveChanges();
        }
    }
}
