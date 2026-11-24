using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Controllers
{
    // Hospital advertisements.
    //   GET  /api/ads                     all ads (public Ads page + home page)
    //   POST /api/ads/{id}/view           count one view
    //   GET  /api/hospital/ads            my hospital's ads
    //   POST /api/hospital/ads            create an ad
    //   PUT  /api/hospital/ads/{id}/stop  stop an ad
    //
    // PAYMENT: a new ad is saved with AwaitingPayment = true, so it is hidden until it is paid.
    // The payment step (POST /api/payments/ad/{id}) is not written yet — add it later,
    // and when the payment succeeds set: ad.AwaitingPayment = false.
    public class AdsController : BaseController
    {
        // Same plans as ads-data.js: days on the page and price in JOD.
        private static readonly Dictionary<string, (int Days, decimal Price)> Plans = new()
        {
            ["basic"] = (7, 25),
            ["featured"] = (14, 50),
            ["premium"] = (30, 90)
        };
        private const decimal VatRate = 0.16m;

        public AdsController(BloodLinkDbContext db) : base(db) { }

        [HttpGet("/api/ads")]
        public IActionResult All()
        {
            var ads = db.Advertisements.Include(a => a.Organization)
                .Where(a => !a.AwaitingPayment)   // unpaid ads are not shown
                .OrderByDescending(a => a.CreatedAt).ToList();
            return Json(ads.Select(ToJson));
        }

        [HttpPost("/api/ads/{id}/view")]
        public IActionResult AddView(int id)
        {
            var ad = db.Advertisements.Find(id);
            if (ad == null) return Error("Ad not found.", 404);
            ad.Views++;
            db.SaveChanges();
            return Json(new { ok = true });
        }

        [Authorize(Roles = "Hospital")]
        [ActiveHospital]
        [HttpGet("/api/hospital/ads")]
        public IActionResult Mine()
        {
            int orgId = CurrentUser()?.OrganizationId ?? 0;
            var ads = db.Advertisements.Include(a => a.Organization)
                .Where(a => a.OrganizationId == orgId && !a.AwaitingPayment)
                .OrderByDescending(a => a.CreatedAt).ToList();
            return Json(ads.Select(ToJson));
        }

        [Authorize(Roles = "Hospital")]
        [ActiveHospital]
        [HttpPost("/api/hospital/ads")]
        public IActionResult Create([FromBody] AdForm form)
        {
            int orgId = CurrentUser()?.OrganizationId ?? 0;
            var org = db.Organizations.Find(orgId);
            if (org == null) return Error("Hospital not found.", 404);

            string planName = Plans.ContainsKey(form.Plan) ? form.Plan : "basic";
            var startDate = Format.ParseDate(form.StartDate) ?? DateOnly.FromDateTime(DateTime.Now);
            var eventDate = Format.ParseDate(form.EventDate);
            if (string.IsNullOrWhiteSpace(form.Title) || eventDate == null)
                return Error("Please fill in all required fields.");
            if (eventDate < startDate)
                return Error("The ad must go live on or before the event date.");

            var plan = Plans[planName];
            decimal vat = Math.Round(plan.Price * VatRate, 2);

            var ad = new Advertisement
            {
                OrganizationId = orgId,
                Title = form.Title.Trim(),
                Department = form.Department,
                Category = form.Category,
                Badge = form.Badge,
                Description = form.Description,
                EventDate = eventDate.Value,
                StartTime = Format.ParseTime(form.StartTime),
                EndTime = Format.ParseTime(form.EndTime),
                Location = form.Location,
                Phone = form.Phone,
                BookingRequired = form.Booking,
                Plan = Enum.Parse<AdPlan>(planName, ignoreCase: true),
                StartDate = startDate,
                EndDate = startDate.AddDays(plan.Days - 1),
                Fee = plan.Price,
                Vat = vat,
                TotalPaid = plan.Price + vat,
                AwaitingPayment = true   // hidden until it is paid (payment comes later)
            };
            db.Advertisements.Add(ad);
            db.SaveChanges();

            ad.Organization = org;
            return Json(ToJson(ad));
        }

        [Authorize(Roles = "Hospital")]
        [ActiveHospital]
        [HttpPut("/api/hospital/ads/{id}/stop")]
        public IActionResult Stop(int id)
        {
            int orgId = CurrentUser()?.OrganizationId ?? 0;
            var ad = db.Advertisements.FirstOrDefault(a => a.Id == id && a.OrganizationId == orgId);
            if (ad == null) return Error("Ad not found.", 404);
            ad.IsCancelled = true;
            db.SaveChanges();
            return Json(new { ok = true });
        }

        // The JSON shape used by ads-data.js.
        private object ToJson(Advertisement a)
        {
            int myOrgId = CurrentUser()?.OrganizationId ?? 0;
            return new
            {
                id = a.Id.ToString(),
                owner = a.OrganizationId == myOrgId ? "self" : "other",
                hospital = a.Organization.Name,
                title = a.Title,
                department = a.Department,
                category = a.Category,
                badge = a.Badge ?? "",
                description = a.Description ?? "",
                eventDate = Format.Date(a.EventDate),
                startTime = Format.Time(a.StartTime),
                endTime = Format.Time(a.EndTime),
                location = a.Location ?? "",
                phone = a.Phone ?? "",
                booking = a.BookingRequired,
                plan = a.Plan.ToString().ToLower(),
                startDate = Format.Date(a.StartDate),
                endDate = Format.Date(a.EndDate),
                fee = a.Fee,
                vat = a.Vat,
                paid = a.TotalPaid,
                receipt = "RC-" + a.Id,
                views = a.Views,
                cancelled = a.IsCancelled,
                createdAt = Format.Iso(a.CreatedAt)
            };
        }
    }
}
