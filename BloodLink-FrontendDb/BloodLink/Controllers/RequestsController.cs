using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Controllers
{
    // Blood requests between a recipient and a donor.
    //   POST /api/requests                 recipient sends a request   (Donor → Search)
    //   GET  /api/requests/sent            recipient's requests        (Recipient → Requests)
    //   GET  /api/requests/received        donor's incoming requests   (Donor → Requests)
    //   PUT  /api/requests/{id}/status     accept / decline / complete
    [Authorize]
    public class RequestsController : BaseController
    {
        public RequestsController(BloodLinkDbContext db) : base(db) { }

        [Authorize(Roles = "Recipient")]
        [HttpPost("/api/requests")]
        public IActionResult Create([FromBody] BloodRequestForm form)
        {
            var me = CurrentUser()!;
            var donor = db.Users.FirstOrDefault(u => u.Id == form.DonorId && u.Role == UserRole.Donor);
            if (donor == null) return Error("Donor not found.", 404);
            if (!donor.IsAvailable) return Error("This donor is not available right now. Please choose another donor.");

            var bloodType = Format.ParseBlood(form.BloodType) ?? me.BloodType ?? donor.BloodType;
            if (bloodType == null) return Error("Please choose the blood type you need.");

            if (!Enum.TryParse(form.Priority, out RequestPriority priority)) priority = RequestPriority.Normal;

            // The hospital must be an approved (active) hospital or center.
            var hospital = db.Organizations.FirstOrDefault(o => o.Id == form.HospitalId && o.Status == OrganizationStatus.Active);
            if (hospital == null) return Error("Please choose the hospital where the donation will happen.");

            var request = new BloodRequest
            {
                RecipientId = me.Id,
                DonorId = donor.Id,
                BloodType = bloodType.Value,
                Priority = priority,
                Units = 1,
                HospitalId = hospital.Id,
                Location = me.City
            };
            db.BloodRequests.Add(request);
            db.SaveChanges();

            return Json(new { id = request.Id.ToString(), priority = request.Priority.ToString() });
        }

        // Requests the signed-in recipient has sent.
        [Authorize(Roles = "Recipient")]
        [HttpGet("/api/requests/sent")]
        public IActionResult Sent()
        {
            var requests = db.BloodRequests
                .Include(r => r.Donor)
                .Include(r => r.Recipient)
                .Include(r => r.Hospital)
                .Where(r => r.RecipientId == CurrentUserId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            var result = new List<object>();
            foreach (var r in requests)
            {
                // Contact details are shown only after the donor accepts.
                bool showContact = r.Status == BloodRequestStatus.Accepted || r.Status == BloodRequestStatus.Completed;
                result.Add(new
                {
                    id = r.Id.ToString(),
                    donorName = r.Donor.FullName,
                    donorInitials = Format.Initials(r.Donor.FullName),
                    bloodType = Format.Blood(r.BloodType),
                    location = string.Join(", ", new[] { r.Donor.Area, r.Donor.City }.Where(x => !string.IsNullOrEmpty(x))),
                    distance = Format.DistanceText(r.Donor, r.Recipient),
                    sentAt = Format.Friendly(r.CreatedAt),
                    dateValue = Format.Iso(r.CreatedAt),
                    status = r.Status.ToString(),
                    priority = r.Priority.ToString(),
                    units = r.Units,
                    hospital = r.Hospital?.Name ?? "",
                    donorPhone = showContact ? r.Donor.Phone ?? "" : "",
                    donorEmail = showContact ? r.Donor.Email : ""
                });
            }
            return Json(result);
        }

        // Requests the signed-in donor has received.
        [Authorize(Roles = "Donor")]
        [HttpGet("/api/requests/received")]
        public IActionResult Received()
        {
            var requests = db.BloodRequests
                .Include(r => r.Recipient)
                .Include(r => r.Donor)
                .Include(r => r.Hospital)
                .Where(r => r.DonorId == CurrentUserId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            var result = new List<object>();
            foreach (var r in requests)
            {
                bool showContact = r.Status == BloodRequestStatus.Accepted || r.Status == BloodRequestStatus.Completed;
                result.Add(new
                {
                    id = r.Id.ToString(),
                    recipientName = r.Recipient.FullName,
                    initials = Format.Initials(r.Recipient.FullName),
                    bloodType = Format.Blood(r.BloodType),
                    location = r.Location ?? r.Recipient.City ?? "",
                    distance = Format.DistanceText(r.Donor, r.Recipient),
                    sentAt = Format.Friendly(r.CreatedAt),
                    dateValue = Format.Iso(r.CreatedAt),
                    status = r.Status.ToString(),
                    priority = r.Priority.ToString(),
                    units = r.Units,
                    hospital = r.Hospital?.Name ?? "",
                    recipientPhone = showContact ? r.Recipient.Phone ?? "" : "",
                    recipientEmail = showContact ? r.Recipient.Email : ""
                });
            }
            return Json(result);
        }

        [HttpPut("/api/requests/{id}/status")]
        public IActionResult ChangeStatus(int id, [FromBody] StatusForm form)
        {
            var request = db.BloodRequests.Include(r => r.Donor).Include(r => r.Recipient).FirstOrDefault(r => r.Id == id);
            if (request == null) return Error("Request not found.", 404);

            bool iAmDonor = request.DonorId == CurrentUserId;
            bool iAmRecipient = request.RecipientId == CurrentUserId;
            if (!iAmDonor && !iAmRecipient) return Error("This is not your request.", 403);

            if (form.Status == "Accepted" || form.Status == "Declined")
            {
                // Only the donor can accept or decline, and only while the request is pending.
                if (!iAmDonor) return Error("Only the donor can answer this request.", 403);
                if (request.Status != BloodRequestStatus.Pending) return Error("This request was already answered.");

                if (form.Status == "Accepted")
                {
                    if (DonationRules.IsResting(request.Donor))
                        return Error($"You donated recently. You can accept requests again from {request.Donor.AvailableAgainOn!.Value:dd MMM yyyy}.");
                    if (!request.Donor.IsAvailable)
                        return Error("Turn on your availability before accepting a request.");

                    request.Status = BloodRequestStatus.Accepted;
                    AddSystemMessage(request.Id, "Request accepted by the donor. You can now chat to coordinate the donation safely.");
                }
                else
                {
                    request.Status = BloodRequestStatus.Declined;
                }
                request.RespondedAt = DateTime.UtcNow;
            }
            else if (form.Status == "Completed")
            {
                // Donor or recipient can mark an accepted request as completed.
                if (request.Status != BloodRequestStatus.Accepted) return Error("Only accepted requests can be completed.");

                request.Status = BloodRequestStatus.Completed;
                request.CompletedAt = DateTime.UtcNow;
                AddSystemMessage(request.Id, "Donation marked as completed. This conversation is now read-only.");

                // Count it as a donation on the donor's profile.
                var today = DateOnly.FromDateTime(DateTime.Now);
                db.DonationRecords.Add(new DonationRecord
                {
                    OrganizationId = request.HospitalId,
                    DonorId = request.DonorId,
                    DonorName = request.Donor.FullName,
                    DonorEmail = request.Donor.Email,
                    RecipientName = request.Recipient.FullName,
                    RecipientEmail = request.Recipient.Email,
                    BloodType = request.BloodType,
                    Units = request.Units,
                    DonationDate = today,
                    Status = DonationRecordStatus.Verified,
                    BloodRequestId = request.Id,
                    Notes = "Blood request #" + request.Id
                });
                // The donor rests for 3 months now.
                DonationRules.MarkDonated(request.Donor, today);
            }
            else
            {
                return Error("Unknown status.");
            }

            db.SaveChanges();
            return Json(new { id = request.Id.ToString(), status = request.Status.ToString() });
        }

        // A grey message in the chat written by BloodLink (not by a person).
        private void AddSystemMessage(int requestId, string text)
        {
            db.ChatMessages.Add(new ChatMessage { BloodRequestId = requestId, SenderType = ChatSender.System, Text = text });
        }
    }
}
