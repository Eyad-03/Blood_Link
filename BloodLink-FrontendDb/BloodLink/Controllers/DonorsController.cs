using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace BloodLink.Controllers
{
    // GET /api/donors  → list of donors for the map on /Donor/Search (anyone can see it).
    public class DonorsController : BaseController
    {
        public DonorsController(BloodLinkDbContext db) : base(db) { }

        [HttpGet("/api/donors")]
        public IActionResult GetDonors()
        {
            DonationRules.ReactivateDonors(db);   // donors whose 3 months are over become available

            // Distances are measured from the signed-in user's city (or Irbid by default).
            var me = CurrentUser();
            var from = Format.Cities["Irbid"];
            if (me?.City != null && Format.Cities.ContainsKey(me.City)) from = Format.Cities[me.City];

            var donors = db.Users
                .Where(u => u.Role == UserRole.Donor && u.Status == AccountStatus.Active)
                .ToList();

            var result = new List<object>();
            foreach (var d in donors)
            {
                // Exact point if shared, else the centre of the donor's city.
                var point = Format.PointOf(d) ?? from;

                result.Add(new
                {
                    id = d.Id,
                    name = d.FullName,
                    city = d.City ?? "",
                    area = d.Area ?? "",
                    type = Format.Blood(d.BloodType),
                    distance = Format.DistanceKm(from.Lat, from.Lng, point.Lat, point.Lng),
                    available = d.IsAvailable,
                    availableAgainOn = Format.Date(d.AvailableAgainOn),
                    initials = Format.Initials(d.FullName),
                    lat = point.Lat,
                    lng = point.Lng,
                    // true when the donor did not share an exact point (city centre is used instead)
                    approximate = !(d.ShareLocation && d.Latitude != null && d.Longitude != null)
                });
            }
            return Json(result);
        }
    }
}
