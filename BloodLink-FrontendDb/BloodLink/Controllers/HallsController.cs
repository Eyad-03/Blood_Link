using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Controllers
{
    // Halls that can be booked for campaigns and conferences.
    //   GET  /api/halls                       list of halls (anyone)
    //   POST /api/halls/bookings              send a booking request
    //   GET  /api/halls/bookings/mine         my booking requests
    //
    // PAYMENT: after the admin approves a booking, the user pays it.
    // The payment step (POST /api/payments/hall-booking/{id}) is not written yet — add it later,
    // and when the payment succeeds set: booking.Status = HallBookingStatus.Paid; booking.PaidAt = DateTime.UtcNow;
    public class HallsController : BaseController
    {
        public HallsController(BloodLinkDbContext db) : base(db) { }

        // (The Hall pages themselves are returned by HallController, without "s".)
        [HttpGet("/api/halls")]
        public IActionResult All()
        {
            var halls = db.Halls.Where(h => h.IsActive).OrderBy(h => h.Id).ToList();
            return Json(halls.Select(h => new
            {
                id = h.Id,
                name = h.Name,
                governorate = h.Governorate,
                entity = h.EntityType ?? "",
                events = (h.SupportedEvents ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries),
                capacity = h.Capacity,
                price = h.Price,
                rating = h.Rating,
                tag = h.Tag ?? "",
                tagClass = h.Tag == "Popular choice" ? "" : h.Tag == "Affordable" ? "amber" : "green",
                location = h.Location ?? "",
                image = h.ImageUrl ?? "",
                description = h.Description ?? "",
                features = (h.Features ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            }));
        }

        [Authorize]
        [HttpPost("/api/halls/bookings")]
        public IActionResult Book([FromBody] HallBookingForm form)
        {
            var hall = db.Halls.Find(form.HallId);
            if (hall == null) return Error("Hall not found.", 404);

            var eventDate = Format.ParseDate(form.EventDate);
            if (eventDate == null || eventDate <= DateOnly.FromDateTime(DateTime.Now))
                return Error("Please choose an event date from tomorrow onwards.");
            if (form.Documents.Count == 0)
                return Error("Please attach all required documents.");

            var booking = new HallBooking
            {
                HallId = hall.Id,
                RequestedById = CurrentUserId,
                OrganizationName = form.Organization,
                ContactName = form.ContactName,
                Email = form.Email,
                Phone = form.Phone,
                EventType = form.EventType,
                EventDate = eventDate.Value,
                EventTime = form.EventTime,
                Attendees = form.Attendees,
                Description = form.Description,
                Price = hall.Price,
                Status = HallBookingStatus.PendingApproval
            };
            // Only the file names are saved (the files themselves are not uploaded in this version).
            foreach (var doc in form.Documents)
                booking.Documents.Add(new HallBookingDocument { Label = doc.Label, FilePath = doc.FileName });

            db.HallBookings.Add(booking);
            db.SaveChanges();

            booking.Hall = hall;
            return Json(ToJson(booking));
        }

        [Authorize]
        [HttpGet("/api/halls/bookings/mine")]
        public IActionResult Mine()
        {
            var list = db.HallBookings
                .Include(b => b.Hall).Include(b => b.Documents)
                .Where(b => b.RequestedById == CurrentUserId)
                .OrderByDescending(b => b.CreatedAt)
                .ToList();
            return Json(list.Select(ToJson));
        }

        // "PendingApproval" -> "Pending approval" (the text used by the pages)
        public static string StatusText(HallBookingStatus status)
        {
            return status == HallBookingStatus.PendingApproval ? "Pending approval" : status.ToString();
        }

        // JSON shape used by hall.js, hallRequests.js, hall-bookings.js and admin.js.
        // (Hall and Documents must be loaded with .Include.)
        public static object ToJson(HallBooking b)
        {
            bool paid = b.Status == HallBookingStatus.Paid || b.Status == HallBookingStatus.Completed;
            return new
            {
                id = b.Id.ToString(),
                hallId = b.HallId,
                hallName = b.Hall.Name,
                governorate = b.Hall.Governorate,
                price = b.Price,
                organization = b.OrganizationName,
                contactName = b.ContactName,
                email = b.Email,
                phone = b.Phone,
                eventType = b.EventType,
                eventDate = Format.Date(b.EventDate),
                eventTime = b.EventTime ?? "",
                attendees = b.Attendees,
                description = b.Description ?? "",
                documents = b.Documents.Select(d => new { label = d.Label, fileName = d.FilePath }),
                status = StatusText(b.Status),
                adminNote = b.AdminNote ?? "",
                paymentMethod = b.PaymentMethod ?? "",
                payMethod = b.PaymentMethod ?? "",
                paidAt = Format.Iso(b.PaidAt),
                amountPaid = paid ? b.Price : 0,
                receipt = "HB-" + b.Id,
                createdAt = Format.Iso(b.CreatedAt)
            };
        }
    }
}
