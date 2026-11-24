using System.Text.RegularExpressions;
using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Controllers
{
    // The data of the hospital dashboard (/Hospital). Only hospital staff can use it.
    // (HospitalController only returns the page; this controller sends the data.)
    // Each hospital user belongs to one Organization (User.OrganizationId).
    //   GET/PUT /api/hospital/profile
    //   GET     /api/hospital/inventory
    //   GET/POST /api/hospital/records          PUT /api/hospital/records/{id}/verify
    //   GET     /api/hospital/donors            POST /api/hospital/donors/email
    //   GET     /api/hospital/appointments      PUT /api/hospital/appointments/{id}
    [Authorize(Roles = "Hospital")]
    [ActiveHospital]   // only after the admin approved the institution (Helpers/ActiveHospitalAttribute.cs)
    public class HospitalPanelController : BaseController
    {
        private readonly EmailSender emailSender;

        public HospitalPanelController(BloodLinkDbContext db, EmailSender emailSender) : base(db)
        {
            this.emailSender = emailSender;
        }

        // The organization id of the signed-in staff member.
        private int MyOrgId()
        {
            return CurrentUser()?.OrganizationId ?? 0;
        }

        // ---------------- Profile ----------------

        [HttpGet("/api/hospital/profile")]
        public IActionResult GetProfile()
        {
            var org = db.Organizations.Find(MyOrgId());
            if (org == null) return Error("Hospital not found.", 404);
            return Json(new { name = org.Name, email = org.Email ?? "", phone = org.Phone ?? "", city = org.City, address = org.Address ?? "" });
        }

        [HttpPut("/api/hospital/profile")]
        public IActionResult SaveProfile([FromBody] HospitalProfileForm form)
        {
            var org = db.Organizations.Find(MyOrgId());
            if (org == null) return Error("Hospital not found.", 404);
            if (string.IsNullOrWhiteSpace(form.Name)) return Error("Hospital name is required.");

            org.Name = form.Name.Trim();
            org.Email = form.Email;
            org.Phone = form.Phone;
            org.City = form.City ?? org.City;
            org.Address = form.Address;
            db.SaveChanges();
            return Json(new { ok = true });
        }

        // ---------------- Blood inventory ----------------

        // Returns { "O+": 52, "O-": 12, ... }
        [HttpGet("/api/hospital/inventory")]
        public IActionResult Inventory()
        {
            int orgId = MyOrgId();
            var rows = db.BloodInventory.Where(i => i.OrganizationId == orgId).ToList();

            var result = new Dictionary<string, int>();
            foreach (var row in rows) result[Format.Blood(row.BloodType)] = row.Units;
            return Json(result);
        }

        // ---------------- Donation records ----------------

        [HttpGet("/api/hospital/records")]
        public IActionResult Records()
        {
            int orgId = MyOrgId();
            var rows = db.DonationRecords
                .Where(r => r.OrganizationId == orgId)
                .OrderByDescending(r => r.DonationDate).ThenByDescending(r => r.Id)
                .ToList();
            return Json(rows.Select(RecordJson));
        }

        [HttpPost("/api/hospital/records")]
        public IActionResult AddRecord([FromBody] DonationRecordForm form)
        {
            var type = Format.ParseBlood(form.Type);
            var date = Format.ParseDate(form.Date);
            if (string.IsNullOrWhiteSpace(form.Donor) || string.IsNullOrWhiteSpace(form.Recipient) || type == null || date == null || form.Units < 1)
                return Error("Please fill in all the donation details.");

            string donorEmail = form.DonorEmail.Trim().ToLower();
            string recipientEmail = form.RecipientEmail.Trim().ToLower();
            if (!IsEmail(donorEmail)) return Error("Please enter a valid donor email.");
            if (!IsEmail(recipientEmail)) return Error("Please enter a valid recipient email.");

            // If the donor has a BloodLink account with this email, link it
            // (the donation counts on their profile and starts the 3-month rest).
            var donorUser = db.Users.FirstOrDefault(u => u.Role == UserRole.Donor && u.Email == donorEmail);

            var record = new DonationRecord
            {
                OrganizationId = MyOrgId(),
                DonorId = donorUser?.Id,
                DonorName = form.Donor.Trim(),
                DonorEmail = donorEmail,
                RecipientName = form.Recipient.Trim(),
                RecipientEmail = recipientEmail,
                BloodType = type.Value,
                Units = form.Units,
                DonationDate = date.Value,
                Status = DonationRecordStatus.Verified,
                Notes = form.Notes
            };
            db.DonationRecords.Add(record);
            if (donorUser != null) DonationRules.MarkDonated(donorUser, date.Value);
            db.SaveChanges();
            return Json(RecordJson(record));
        }

        [HttpPut("/api/hospital/records/{id}/verify")]
        public IActionResult VerifyRecord(int id)
        {
            int orgId = MyOrgId();
            var record = db.DonationRecords.FirstOrDefault(r => r.Id == id && r.OrganizationId == orgId);
            if (record == null) return Error("Record not found.", 404);

            record.Status = DonationRecordStatus.Verified;
            db.SaveChanges();
            return Json(RecordJson(record));
        }

        private static object RecordJson(DonationRecord r)
        {
            return new
            {
                id = r.Id.ToString(),
                donor = r.DonorName,
                donorEmail = r.DonorEmail ?? "",
                recipient = r.RecipientName,
                recipientEmail = r.RecipientEmail ?? "",
                type = Format.Blood(r.BloodType),
                units = r.Units,
                date = Format.Date(r.DonationDate),
                status = r.Status == DonationRecordStatus.Verified ? "Verified" : "Pending verification",
                notes = r.Notes ?? ""
            };
        }

        private static bool IsEmail(string text)
        {
            return Regex.IsMatch(text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        // ---------------- Donor directory ----------------

        // Only the donors who gave blood at THIS hospital (from its verified donation records).
        [HttpGet("/api/hospital/donors")]
        public IActionResult Donors()
        {
            return Json(MyDonors().Select(d => new
            {
                name = d.Name,
                email = d.Email,
                type = d.BloodType,
                location = d.Location,
                last = d.LastDonation.ToString("dd MMM yyyy"),
                donations = d.Donations,
                availability = d.Available ? "Available" : "Not active",
                availableAgainOn = Format.Date(d.AvailableAgainOn)
            }));
        }

        // "Send email" on the Donor directory: one email per donor.
        // Donors who are not available (resting after a donation) are skipped.
        [HttpPost("/api/hospital/donors/email")]
        public IActionResult EmailDonors([FromBody] EmailDonorsForm form)
        {
            if (!emailSender.IsSetUp)
                return Error("Email sending is not set up yet (appsettings.json → Email).");

            var org = db.Organizations.Find(MyOrgId())!;
            var wanted = form.Emails.Select(e => e.Trim().ToLower()).ToList();
            // Only donors from this hospital's directory can be emailed.
            var donors = MyDonors().Where(d => wanted.Contains(d.Email)).ToList();

            int sent = 0, skipped = 0, failed = 0;
            foreach (var donor in donors)
            {
                if (!donor.Available) { skipped++; continue; }
                try
                {
                    emailSender.Send(donor.Email, $"{org.Name} needs blood donors", DonationRequestEmail(org, donor.Name, form.BloodType));
                    sent++;
                }
                catch (Exception)
                {
                    failed++;   // e.g. wrong SMTP password or the mail server is down
                }
            }

            if (sent == 0 && failed > 0)
                return Error("The emails could not be sent. Check the Email settings.", 502);
            return Json(new { sent, skipped, failed });
        }

        // One donor in the directory (built from the donation records).
        private class DirectoryDonor
        {
            public string Name = "", Email = "", BloodType = "", Location = "";
            public DateOnly LastDonation;
            public int Donations;
            public bool Available;
            public DateOnly? AvailableAgainOn;
        }

        private List<DirectoryDonor> MyDonors()
        {
            int orgId = MyOrgId();
            var records = db.DonationRecords
                .Where(r => r.OrganizationId == orgId && r.Status == DonationRecordStatus.Verified && r.DonorEmail != null)
                .ToList();

            var result = new List<DirectoryDonor>();
            // One row per donor email.
            foreach (var group in records.GroupBy(r => r.DonorEmail!.ToLower()))
            {
                var last = group.OrderByDescending(r => r.DonationDate).First();
                var user = db.Users.FirstOrDefault(u => u.Email == group.Key);

                var donor = new DirectoryDonor
                {
                    Name = user?.FullName ?? last.DonorName,
                    Email = group.Key,
                    BloodType = Format.Blood(last.BloodType),
                    Location = user == null ? "" : string.Join(", ", new[] { user.Area, user.City }.Where(x => !string.IsNullOrEmpty(x))),
                    LastDonation = last.DonationDate,
                    Donations = group.Count()
                };

                if (user != null)
                {
                    // Registered donor: use the availability from their account.
                    donor.Available = user.IsAvailable && user.Status == AccountStatus.Active;
                    donor.AvailableAgainOn = user.AvailableAgainOn;
                }
                else
                {
                    // No account: available again 3 months after the last donation here.
                    var again = last.DonationDate.AddMonths(DonationRules.RestMonths);
                    donor.Available = again <= DateOnly.FromDateTime(DateTime.Now);
                    donor.AvailableAgainOn = donor.Available ? null : again;
                }
                result.Add(donor);
            }
            return result.OrderBy(d => d.Name).ToList();
        }

        // The email the donors receive.
        private static string DonationRequestEmail(Organization org, string donorName, string? bloodType)
        {
            string e(string? v) => System.Net.WebUtility.HtmlEncode(v ?? "");
            string need = string.IsNullOrWhiteSpace(bloodType) ? "blood" : $"<b>{e(bloodType)}</b> blood";
            return $@"
<div style=""font-family:Arial,sans-serif;max-width:560px;margin:auto;color:#1b1c28"">
  <h2 style=""color:#c91835;margin-bottom:4px"">♥ BloodLink</h2>
  <p>Hello {e(donorName)},</p>
  <p><b>{e(org.Name)}</b> urgently needs {need} donors. You donated with us before —
     if you feel well and are able to donate again, we would be very grateful.</p>
  <p>📍 {e(org.Address ?? org.City)}<br>📞 {e(org.Phone ?? "")}</p>
  <p>You can book a donation appointment on BloodLink, or contact the hospital directly.</p>
  <p style=""color:#7a7b8d;font-size:12px"">Please only donate if you meet the medical requirements and at least 3 months have passed since your last donation.</p>
  <p>Thank you for saving lives.<br>{e(org.Name)} — BloodLink</p>
</div>";
        }

        // ---------------- Donation appointments ----------------

        [HttpGet("/api/hospital/appointments")]
        public IActionResult Appointments()
        {
            int orgId = MyOrgId();
            var list = db.DonationAppointments
                .Include(a => a.Organization)
                .Where(a => a.OrganizationId == orgId)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
            return Json(list.Select(AppointmentsController.ToJson));
        }

        // One action for all the hospital decisions: approve / decline / cancel / complete.
        [HttpPut("/api/hospital/appointments/{id}")]
        public IActionResult Decide(int id, [FromBody] AppointmentDecisionForm form)
        {
            int orgId = MyOrgId();
            var a = db.DonationAppointments.Include(x => x.Organization)
                .FirstOrDefault(x => x.Id == id && x.OrganizationId == orgId);
            if (a == null) return Error("Appointment not found.", 404);

            if (form.Action == "approve")
            {
                var date = Format.ParseDate(form.Date);
                var time = Format.ParseTime(form.Time);
                if (date == null || time == null) return Error("Please choose a date and time.");
                if (date < DateOnly.FromDateTime(DateTime.Now)) return Error("The date can't be in the past.");
                if (date.Value.DayOfWeek == DayOfWeek.Friday) return Error("Fridays are closed. Please choose another day.");

                a.Status = AppointmentStatus.Approved;
                a.ConfirmedDate = date;
                a.ConfirmedTime = time;
                a.HospitalNote = form.Note;
                a.DeclineReason = null;
            }
            else if (form.Action == "decline" || form.Action == "cancel")
            {
                if (string.IsNullOrWhiteSpace(form.Reason) || form.Reason.Trim().Length < 3)
                    return Error("Please write a short reason for the donor.");

                a.Status = form.Action == "decline" ? AppointmentStatus.Declined : AppointmentStatus.Cancelled;
                a.DeclineReason = form.Reason.Trim();
                if (form.Action == "cancel") a.CancelledBy = "hospital";
            }
            else if (form.Action == "complete")
            {
                if (a.Status != AppointmentStatus.Approved) return Error("Only confirmed appointments can be completed.");
                a.Status = AppointmentStatus.Completed;
                var donationDate = a.ConfirmedDate ?? DateOnly.FromDateTime(DateTime.Now);

                // Count the donation for the donor (profile page) → donor rests for 3 months.
                var donor = db.Users.Find(a.DonorId);
                if (donor != null) DonationRules.MarkDonated(donor, donationDate);

                db.DonationRecords.Add(new DonationRecord
                {
                    OrganizationId = orgId,
                    DonorId = a.DonorId,
                    DonorName = a.DonorName,
                    DonorEmail = (a.DonorEmail ?? donor?.Email)?.ToLower(),   // → this hospital's Donor directory
                    RecipientName = "Blood bank stock",
                    BloodType = a.BloodType,
                    Units = 1,
                    DonationDate = donationDate,
                    Status = DonationRecordStatus.Verified,
                    AppointmentId = a.Id,
                    Notes = "Appointment " + a.Id
                });

                // Optional: add one unit to the blood stock.
                if (form.AddStock)
                {
                    var stock = db.BloodInventory.FirstOrDefault(i => i.OrganizationId == orgId && i.BloodType == a.BloodType);
                    if (stock == null)
                    {
                        stock = new BloodInventory { OrganizationId = orgId, BloodType = a.BloodType, Units = 0 };
                        db.BloodInventory.Add(stock);
                    }
                    stock.Units += 1;
                    stock.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                return Error("Unknown action.");
            }

            a.DonorNotified = false;   // the donor will see a message next time he opens his page
            a.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
            return Json(AppointmentsController.ToJson(a));
        }
    }
}
