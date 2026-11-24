using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Controllers
{
    // Donation appointments — donor side (/Donor/Appointments).
    //   GET  /api/facilities                  hospitals & centers that accept bookings (anyone)
    //   GET  /api/appointments/mine           my appointments
    //   POST /api/appointments                book a new appointment
    //   PUT  /api/appointments/{id}/cancel    cancel my appointment
    //   POST /api/appointments/notified       "I have seen the latest changes"
    public class AppointmentsController : BaseController
    {
        public AppointmentsController(BloodLinkDbContext db) : base(db) { }

        [HttpGet("/api/facilities")]
        public IActionResult Facilities()
        {
            var orgs = db.Organizations
                .Where(o => o.Status == OrganizationStatus.Active)
                .OrderBy(o => o.Name)
                .ToList();

            return Json(orgs.Select(o => new
            {
                id = o.Id.ToString(),
                name = o.Name,
                type = Format.OrgType(o.Type),
                city = o.City,
                address = o.Address ?? o.City,
                open = Format.Time(o.OpenTime),
                close = Format.Time(o.CloseTime)
            }));
        }

        [Authorize(Roles = "Donor")]
        [HttpGet("/api/appointments/mine")]
        public IActionResult Mine()
        {
            var list = db.DonationAppointments
                .Include(a => a.Organization)
                .Where(a => a.DonorId == CurrentUserId)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return Json(list.Select(ToJson));
        }

        [Authorize(Roles = "Donor")]
        [HttpPost("/api/appointments")]
        public IActionResult Create([FromBody] AppointmentForm form)
        {
            int.TryParse(form.FacilityId, out int orgId);
            var org = db.Organizations.FirstOrDefault(o => o.Id == orgId && o.Status == OrganizationStatus.Active);
            if (org == null) return Error("Please choose a hospital or donation center.");

            var date = Format.ParseDate(form.RequestedDate);
            var time = Format.ParseTime(form.RequestedTime);
            var bloodType = Format.ParseBlood(form.Donor.Type);
            if (date == null || time == null) return Error("Please choose a date and time.");
            if (bloodType == null) return Error("Please select your blood type.");
            if (date <= DateOnly.FromDateTime(DateTime.Now)) return Error("Choose a date from tomorrow onwards.");
            if (date.Value.DayOfWeek == DayOfWeek.Friday) return Error("Centers are closed on Fridays.");
            if (form.Eligibility.State == "blocked") return Error("You are not eligible to donate right now.");

            // Still resting after the last donation?
            var me = CurrentUser()!;
            if (me.AvailableAgainOn != null && date < me.AvailableAgainOn)
                return Error($"You donated recently. You can book from {me.AvailableAgainOn.Value:dd MMM yyyy}.");

            var e = form.Eligibility;
            var appointment = new DonationAppointment
            {
                DonorId = me.Id,
                OrganizationId = org.Id,
                DonorName = form.Donor.Name.Trim(),
                DonorPhone = form.Donor.Phone.Trim(),
                DonorEmail = form.Donor.Email,
                BloodType = bloodType.Value,
                RequestedDate = date.Value,
                RequestedTime = time.Value,
                Notes = form.Notes,
                Age = e.Age,
                WeightKg = e.Weight,
                LastDonationDate = Format.ParseDate(e.LastDonation),
                FeelingWell = e.Well != "no",
                TakesMedication = e.Meds == "yes",
                MedicationDetail = e.MedsDetail,
                TravelledAbroad = e.Travel == "yes",
                TravelDetail = e.TravelDetail,
                RecentProcedure = e.Procedure == "yes",
                EligibilityState = e.State == "review" ? EligibilityState.Review : EligibilityState.Ok,
                EligibilityFlags = string.Join("\n", e.Flags),
                Status = AppointmentStatus.Pending,
                DonorNotified = true
            };
            db.DonationAppointments.Add(appointment);

            // Keep the donor's profile up to date with what he typed.
            if (me.BloodType == null) me.BloodType = bloodType;
            if (string.IsNullOrEmpty(me.Phone)) me.Phone = appointment.DonorPhone;

            db.SaveChanges();
            appointment.Organization = org;
            return Json(ToJson(appointment));
        }

        [Authorize(Roles = "Donor")]
        [HttpPut("/api/appointments/{id}/cancel")]
        public IActionResult Cancel(int id)
        {
            var a = db.DonationAppointments.FirstOrDefault(x => x.Id == id && x.DonorId == CurrentUserId);
            if (a == null) return Error("Appointment not found.", 404);
            if (a.Status != AppointmentStatus.Pending && a.Status != AppointmentStatus.Approved)
                return Error("This appointment can't be cancelled.");

            a.Status = AppointmentStatus.Cancelled;
            a.CancelledBy = "donor";
            a.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
            return Json(new { ok = true });
        }

        [Authorize(Roles = "Donor")]
        [HttpPost("/api/appointments/notified")]
        public IActionResult MarkNotified()
        {
            var list = db.DonationAppointments.Where(a => a.DonorId == CurrentUserId && !a.DonorNotified).ToList();
            foreach (var a in list) a.DonorNotified = true;
            db.SaveChanges();
            return Json(new { ok = true });
        }

        // Turns an appointment into the JSON shape used by donorAppointments.js and hospital.js.
        // (The Organization must be loaded with .Include.)
        public static object ToJson(DonationAppointment a)
        {
            return new
            {
                id = a.Id.ToString(),
                ownerId = a.DonorId.ToString(),
                facilityId = a.OrganizationId.ToString(),
                facilityName = a.Organization.Name,
                facilityType = Format.OrgType(a.Organization.Type),
                facilityCity = a.Organization.City,
                donor = new { name = a.DonorName, phone = a.DonorPhone, email = a.DonorEmail ?? "", type = Format.Blood(a.BloodType) },
                requestedDate = Format.Date(a.RequestedDate),
                requestedTime = Format.Time(a.RequestedTime),
                notes = a.Notes ?? "",
                eligibility = new
                {
                    age = a.Age,
                    weight = a.WeightKg,
                    lastDonation = Format.Date(a.LastDonationDate),
                    well = a.FeelingWell ? "yes" : "no",
                    meds = a.TakesMedication ? "yes" : "no",
                    medsDetail = a.MedicationDetail ?? "",
                    travel = a.TravelledAbroad ? "yes" : "no",
                    travelDetail = a.TravelDetail ?? "",
                    procedure = a.RecentProcedure ? "yes" : "no",
                    state = a.EligibilityState.ToString().ToLower(),
                    flags = string.IsNullOrEmpty(a.EligibilityFlags) ? new string[0] : a.EligibilityFlags.Split('\n')
                },
                status = a.Status.ToString(),
                confirmedDate = Format.Date(a.ConfirmedDate),
                confirmedTime = Format.Time(a.ConfirmedTime),
                hospitalNote = a.HospitalNote ?? "",
                declineReason = a.DeclineReason ?? "",
                cancelledBy = a.CancelledBy ?? "",
                donorNotified = a.DonorNotified,
                createdAt = Format.Iso(a.CreatedAt),
                updatedAt = Format.Iso(a.UpdatedAt)
            };
        }
    }
}
