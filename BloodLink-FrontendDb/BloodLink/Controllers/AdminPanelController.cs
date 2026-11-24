using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Controllers
{
    // The data of the admin console (/Admin). Only admins can use it.
    // (AdminController only returns the page; this controller sends the data.)
    //   accounts = hospitals / centers waiting for verification
    //   users    = donors and recipients
    //   bookings = hall booking requests
    //   reports  = "Report an issue" inbox
    [Authorize(Roles = "Admin")]
    public class AdminPanelController : BaseController
    {
        private readonly IWebHostEnvironment env;

        public AdminPanelController(BloodLinkDbContext db, IWebHostEnvironment env) : base(db)
        {
            this.env = env;
        }

        // ---------------- Accounts (organizations) ----------------

        [HttpGet("/api/admin/accounts")]
        public IActionResult Accounts()
        {
            var orgs = db.Organizations.Include(o => o.Documents)
                .OrderByDescending(o => o.RegisteredAt).ToList();

            return Json(orgs.Select(o => new
            {
                id = o.Id.ToString(),
                name = o.Name,
                type = Format.OrgType(o.Type),
                city = o.City,
                license = o.LicenseNumber ?? "—",
                email = o.Email ?? "—",
                phone = o.Phone ?? "—",
                contact = o.ContactPerson ?? "—",
                address = o.Address ?? "—",
                registeredAt = Format.Iso(o.RegisteredAt),
                status = o.Status.ToString(),
                decidedAt = Format.Iso(o.DecidedAt),
                reason = o.RejectionReason ?? "",
                submitted = o.SubmittedAt != null,       // the hospital pressed "Send for review"
                submittedAt = Format.Iso(o.SubmittedAt),
                docs = o.Documents.OrderBy(d => d.Id).Select(d => new
                {
                    id = d.Id,
                    name = d.Name,
                    uploaded = d.FileSizeBytes != null,
                    file = d.FileName ?? "Not uploaded yet",
                    size = UploadStorage.SizeText(d.FileSizeBytes),
                    @checked = d.IsChecked
                })
            }));
        }

        // Tick / untick one document while reviewing an organization.
        [HttpPut("/api/admin/accounts/{id}/docs/{docId}")]
        public IActionResult ToggleDocument(int id, int docId)
        {
            var doc = db.OrganizationDocuments.FirstOrDefault(d => d.Id == docId && d.OrganizationId == id);
            if (doc == null) return Error("Document not found.", 404);
            if (doc.FileSizeBytes == null) return Error("This document was not uploaded yet.");

            doc.IsChecked = !doc.IsChecked;
            db.SaveChanges();
            return Json(new { @checked = doc.IsChecked });
        }

        // action: activate | reject-account | revoke | reopen-account
        [HttpPut("/api/admin/accounts/{id}")]
        public IActionResult DecideAccount(int id, [FromBody] AdminDecisionForm form)
        {
            var org = db.Organizations.Include(o => o.Documents).FirstOrDefault(o => o.Id == id);
            if (org == null) return Error("Account not found.", 404);

            if (form.Action == "activate")
            {
                if (org.Documents.Count < 3 || org.Documents.Any(d => d.FileSizeBytes == null || !d.IsChecked))
                    return Error("Check all three documents first.");
                org.Status = OrganizationStatus.Active;
                org.DecidedAt = DateTime.UtcNow;
                org.RejectionReason = null;
                LogAdminAction($"Activated {org.Name}", "check", "#13895f");
            }
            else if (form.Action == "reject-account")
            {
                if (string.IsNullOrWhiteSpace(form.Note)) return Error("Write the rejection reason.");
                org.Status = OrganizationStatus.Rejected;
                org.RejectionReason = form.Note;
                org.SubmittedAt = null;   // the hospital must upload and send again
                org.DecidedAt = DateTime.UtcNow;
                LogAdminAction($"Rejected {org.Name}", "x", "#d41d3d");
            }
            else if (form.Action == "revoke")
            {
                org.Status = OrganizationStatus.Pending;
                foreach (var d in org.Documents) d.IsChecked = false;
                LogAdminAction($"Revoked activation of {org.Name}", "x", "#d41d3d");
            }
            else if (form.Action == "reopen-account")
            {
                org.Status = OrganizationStatus.Pending;
                org.RejectionReason = null;
                LogAdminAction($"Reopened {org.Name} for review", "clock", "#bd7c12");
            }
            else return Error("Unknown action.");

            db.SaveChanges();
            return Json(new { ok = true });
        }

        // Opens an uploaded verification document (only admins can do this).
        [HttpGet("/api/admin/documents/{docId}")]
        public IActionResult Document(int docId)
        {
            var doc = db.OrganizationDocuments.Find(docId);
            if (doc == null || doc.FileSizeBytes == null) return Error("The file was not found.", 404);

            string fullPath = UploadStorage.FullPath(env, doc.FilePath);
            if (!System.IO.File.Exists(fullPath)) return Error("The file is missing on the server.", 404);

            string ext = Path.GetExtension(fullPath).ToLower();
            string contentType = UploadStorage.AllowedTypes.GetValueOrDefault(ext, "application/octet-stream");
            return PhysicalFile(fullPath, contentType);
        }

        // ---------------- Users (donors & recipients) ----------------

        [HttpGet("/api/admin/users")]
        public IActionResult Users()
        {
            var users = db.Users
                .Where(u => u.Role == UserRole.Donor || u.Role == UserRole.Recipient)
                .OrderByDescending(u => u.CreatedAt).ToList();

            return Json(users.Select(u => new
            {
                id = u.Id.ToString(),
                name = u.FullName,
                email = u.Email,
                phone = u.Phone ?? "—",
                city = u.City ?? "—",
                bloodType = u.BloodType == null ? "—" : Format.Blood(u.BloodType),
                joinedAt = Format.Iso(u.CreatedAt),
                lastActive = u.LastActiveAt == null ? null : Format.Iso(u.LastActiveAt),
                donations = db.DonationRecords.Count(r => r.DonorId == u.Id),
                status = u.Status.ToString(),
                role = u.Role == UserRole.Donor ? "donor" : "recipient",
                roleLabel = u.Role == UserRole.Donor ? "Donor" : "Recipient",
                suspendReason = u.SuspendReason ?? "",
                suspendedAt = u.SuspendedAt == null ? null : Format.Iso(u.SuspendedAt),
                _src = "real"
            }));
        }

        // action: suspend-user | reactivate-user
        [HttpPut("/api/admin/users/{id}")]
        public IActionResult DecideUser(int id, [FromBody] AdminDecisionForm form)
        {
            var user = db.Users.Find(id);
            if (user == null) return Error("User not found.", 404);

            if (form.Action == "suspend-user")
            {
                if (string.IsNullOrWhiteSpace(form.Note)) return Error("Write the suspension reason.");
                user.Status = AccountStatus.Suspended;
                user.SuspendReason = form.Note;
                user.SuspendedAt = DateTime.UtcNow;
                LogAdminAction($"Suspended {user.FullName}", "x", "#d41d3d");
            }
            else if (form.Action == "reactivate-user")
            {
                user.Status = AccountStatus.Active;
                user.SuspendReason = null;
                user.SuspendedAt = null;
                LogAdminAction($"Reactivated {user.FullName}", "check", "#13895f");
            }
            else return Error("Unknown action.");

            db.SaveChanges();
            return Json(new { ok = true });
        }

        // ---------------- Hall bookings ----------------

        [HttpGet("/api/admin/bookings")]
        public IActionResult Bookings()
        {
            var list = db.HallBookings.Include(b => b.Hall).Include(b => b.Documents)
                .OrderByDescending(b => b.CreatedAt).ToList();
            return Json(list.Select(HallsController.ToJson));
        }

        // action: approve | reject-booking | complete
        [HttpPut("/api/admin/bookings/{id}")]
        public IActionResult DecideBooking(int id, [FromBody] AdminDecisionForm form)
        {
            var booking = db.HallBookings.Find(id);
            if (booking == null) return Error("Booking not found.", 404);

            if (form.Action == "approve")
            {
                booking.Status = HallBookingStatus.Approved;
                booking.AdminNote = string.IsNullOrWhiteSpace(form.Note) ? "Your request has been approved. You can now pay to confirm." : form.Note;
                LogAdminAction($"Approved booking {id}", "check", "#13895f");
            }
            else if (form.Action == "reject-booking")
            {
                if (string.IsNullOrWhiteSpace(form.Note)) return Error("Add a short reason for the requester.");
                booking.Status = HallBookingStatus.Rejected;
                booking.AdminNote = form.Note;
                LogAdminAction($"Rejected booking {id}", "x", "#d41d3d");
            }
            else if (form.Action == "complete")
            {
                booking.Status = HallBookingStatus.Completed;
                booking.AdminNote = string.IsNullOrWhiteSpace(form.Note) ? "Your booking has been processed and completed." : form.Note;
                LogAdminAction($"Marked booking {id} as completed", "star", "#6a4bc4");
            }
            else return Error("Unknown action.");

            db.SaveChanges();
            return Json(new { ok = true });
        }

        // ---------------- Reports ----------------

        [HttpGet("/api/admin/reports")]
        public IActionResult Reports()
        {
            var list = db.Reports.Include(r => r.Attachments)
                .OrderByDescending(r => r.CreatedAt).ToList();
            return Json(list.Select(ReportsController.ToJson));
        }

        // action: read | review | resolve | reopen
        [HttpPut("/api/admin/reports/{id}")]
        public IActionResult DecideReport(int id, [FromBody] AdminDecisionForm form)
        {
            var report = db.Reports.Find(id);
            if (report == null) return Error("Report not found.", 404);

            if (form.Action == "read")
            {
                report.IsRead = true;
            }
            else if (form.Action == "review")
            {
                report.Status = ReportStatus.InReview;
                LogAdminAction($"Started reviewing report {id}", "clock", "#bd7c12");
            }
            else if (form.Action == "resolve")
            {
                if (string.IsNullOrWhiteSpace(form.Note)) return Error("Add a short resolution note before resolving.");
                report.Status = ReportStatus.Resolved;
                report.Response = form.Note;
                report.ResolvedAt = DateTime.UtcNow;
                LogAdminAction($"Resolved report {id}", "check", "#13895f");
            }
            else if (form.Action == "reopen")
            {
                report.Status = ReportStatus.InReview;
                LogAdminAction($"Reopened report {id}", "clock", "#bd7c12");
            }
            else return Error("Unknown action.");

            db.SaveChanges();
            return Json(new { ok = true });
        }

        // ---------------- Recent activity ----------------

        [HttpGet("/api/admin/activity")]
        public IActionResult Activity()
        {
            var list = db.AdminActivityLogs.OrderByDescending(l => l.CreatedAt).Take(30).ToList();
            return Json(list.Select(l => new { text = l.Text, icon = l.Icon ?? "check", color = l.Color ?? "#13895f", at = Format.Iso(l.CreatedAt) }));
        }
    }
}
