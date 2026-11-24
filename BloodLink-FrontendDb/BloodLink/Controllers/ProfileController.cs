using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodLink.Controllers
{
    // The signed-in user's own profile (/Account/Profile, /Donor/Donate and the availability switch).
    //   GET    /api/profile                      my details + donations + certificate
    //   PUT    /api/profile                      edit my details
    //   PUT    /api/profile/availability         available to donate? true / false
    //   PUT    /api/profile/donor-offer          Donate page: blood type, phone, location
    //   POST   /api/profile/donations            add a donation I made myself
    //   DELETE /api/profile/donations/last       undo the last donation I added
    //   POST   /api/profile/certificate          create my certificate (after 5 donations)
    //   POST   /api/send-certificate             mark the certificate as sent
    [Authorize]
    public class ProfileController : BaseController
    {
        private const int CertificateGoal = 5;

        public ProfileController(BloodLinkDbContext db) : base(db) { }

        [HttpGet("/api/profile")]
        public IActionResult Get()
        {
            DonationRules.ReactivateDonors(db);   // maybe my 3 months are over
            var me = CurrentUser();
            if (me == null) return Error("Please sign in.", 401);
            return Json(ProfileJson(me));
        }

        [HttpPut("/api/profile")]
        public IActionResult Update([FromBody] ProfileForm form)
        {
            var me = CurrentUser()!;
            string email = (form?.Email ?? "").Trim().ToLower();
            if (form == null || string.IsNullOrWhiteSpace(form.Name) || email == "")
                return Error("Name and email are required.");
            if (db.Users.Any(u => u.Email == email && u.Id != me.Id))
                return Error("Another account already uses this email.", 409);

            me.FullName = form.Name.Trim();
            me.Email = email;
            me.Phone = form.Phone;
            me.City = form.Location;
            db.SaveChanges();
            return Json(ProfileJson(me));
        }

        [HttpPut("/api/profile/availability")]
        public IActionResult SetAvailability([FromBody] AvailabilityForm form)
        {
            var me = CurrentUser()!;
            // During the 3 months after a donation the donor can't switch back on.
            if (form.Available && DonationRules.IsResting(me))
                return Error($"You donated recently. You will be available again on {me.AvailableAgainOn!.Value:dd MMM yyyy}.");

            me.IsAvailable = form.Available;
            db.SaveChanges();
            return Json(new { available = me.IsAvailable });
        }

        // The Donate page form. After this the donor appears on the map of /Donor/Search.
        [Authorize(Roles = "Donor")]
        [HttpPut("/api/profile/donor-offer")]
        public IActionResult DonorOffer([FromBody] DonorOfferForm form)
        {
            var me = CurrentUser()!;
            var type = Format.ParseBlood(form.BloodType);
            if (type == null) return Error("Please select your blood type.");
            if (string.IsNullOrWhiteSpace(form.Phone)) return Error("Please enter your phone number.");

            // "Irbid, University Street" → City = "Irbid", Area = "University Street"
            var parts = form.City.Split(',', 2, StringSplitOptions.TrimEntries);
            me.City = parts[0];
            me.Area = parts.Length > 1 && parts[1] != "Jordan" ? parts[1] : null;

            if (!string.IsNullOrWhiteSpace(form.FullName)) me.FullName = form.FullName.Trim();
            me.BloodType = type;
            me.Phone = form.Phone.Trim();
            me.PreferredContactMethod = form.ContactMethod;
            me.IsAvailable = !DonationRules.IsResting(me);   // stays off while resting after a donation

            // Save the exact point only if the donor agreed to share it.
            me.ShareLocation = form.ShareExactLocation && form.Latitude != null && form.Longitude != null;
            me.Latitude = me.ShareLocation ? form.Latitude : null;
            me.Longitude = me.ShareLocation ? form.Longitude : null;

            db.SaveChanges();
            return Json(ProfileJson(me));
        }

        [HttpPost("/api/profile/donations")]
        public IActionResult AddDonation()
        {
            var me = CurrentUser()!;
            if (DonationRules.IsResting(me))
                return Error($"You donated recently. You can donate again from {me.AvailableAgainOn!.Value:dd MMM yyyy}.");
            if (!me.IsAvailable) return Error("Switch your status to “Available” first.");

            var today = DateOnly.FromDateTime(DateTime.Now);
            db.DonationRecords.Add(new DonationRecord
            {
                OrganizationId = null,              // added by the donor, not by a hospital
                DonorId = me.Id,
                DonorName = me.FullName,
                RecipientName = "Self-reported",
                BloodType = me.BloodType ?? BloodType.OPositive,
                Units = 1,
                DonationDate = today,
                Status = DonationRecordStatus.PendingVerification
            });
            DonationRules.MarkDonated(me, today);   // now not available for 3 months
            db.SaveChanges();
            return Json(ProfileJson(me));
        }

        [HttpDelete("/api/profile/donations/last")]
        public IActionResult UndoDonation()
        {
            var me = CurrentUser()!;
            // Only donations the user added himself can be removed.
            var last = db.DonationRecords
                .Where(r => r.DonorId == me.Id && r.OrganizationId == null && r.BloodRequestId == null && r.AppointmentId == null)
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();
            if (last == null) return Error("There is no donation you can undo.");

            db.DonationRecords.Remove(last);
            db.SaveChanges();
            DonationRules.Recalculate(me, db);   // maybe the donor is available again
            db.SaveChanges();
            return Json(ProfileJson(me));
        }

        [HttpPost("/api/profile/certificate")]
        public IActionResult IssueCertificate()
        {
            var me = CurrentUser()!;
            if (db.Certificates.Any(c => c.UserId == me.Id)) return Json(ProfileJson(me));

            int count = db.DonationRecords.Count(r => r.DonorId == me.Id);
            if (count < CertificateGoal)
                return Error($"You need {CertificateGoal} donations to get a certificate.");

            db.Certificates.Add(new Certificate
            {
                UserId = me.Id,
                Code = "BL-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                DonationCount = count
            });
            db.SaveChanges();
            return Json(ProfileJson(me));
        }

        // The certificate page calls this to "send" the certificate.
        // We only save who it was sent to.
        [HttpPost("/api/send-certificate")]
        public IActionResult SendCertificate()
        {
            var me = CurrentUser()!;
            var cert = db.Certificates.FirstOrDefault(c => c.UserId == me.Id);
            if (cert == null) return Error("Create your certificate first.");

            cert.SentToEmail = me.Email;
            cert.SentAt = DateTime.UtcNow;
            db.SaveChanges();
            return Json(new { sentTo = cert.SentToEmail, sentAt = Format.Iso(cert.SentAt) });
        }

        // The JSON shape used by profile.js and donate.js.
        private object ProfileJson(User me)
        {
            var donations = db.DonationRecords
                .Where(r => r.DonorId == me.Id)
                .OrderBy(r => r.DonationDate).ThenBy(r => r.Id)
                .Select(r => r.DonationDate)
                .ToList()
                .Select(d => d.ToString("yyyy-MM-dd") + "T12:00:00");

            var cert = db.Certificates.FirstOrDefault(c => c.UserId == me.Id);

            return new
            {
                name = me.FullName,
                email = me.Email,
                phone = me.Phone ?? "",
                location = me.City ?? "",
                area = me.Area ?? "",
                shareLocation = me.ShareLocation,
                latitude = me.ShareLocation ? me.Latitude : null,
                longitude = me.ShareLocation ? me.Longitude : null,
                role = me.Role.ToString(),
                bloodType = Format.Blood(me.BloodType),
                available = me.IsAvailable,
                availableAgainOn = Format.Date(me.AvailableAgainOn),   // "" when not resting
                donations,
                certificate = cert == null ? null : new
                {
                    id = cert.Code,
                    issuedAt = Format.Iso(cert.IssuedAt),
                    donations = cert.DonationCount,
                    name = me.FullName,
                    sentAt = cert.SentAt == null ? null : Format.Iso(cert.SentAt),
                    sentTo = cert.SentToEmail
                }
            };
        }
    }
}
