using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Mvc;

namespace BloodLink.Controllers
{
    // POST /api/reports  → "Report an issue" page. Anyone can send a report.
    // The admin reads them in AdminPanelController.
    public class ReportsController : BaseController
    {
        public ReportsController(BloodLinkDbContext db) : base(db) { }

        [HttpPost("/api/reports")]
        public IActionResult Create([FromBody] ReportForm form)
        {
            if (form == null || string.IsNullOrWhiteSpace(form.Title) || string.IsNullOrWhiteSpace(form.Email) || form.Body.Trim().Length < 20)
                return Error("Check the highlighted fields and try again.");

            var report = new Report
            {
                ReporterId = CurrentUserId == 0 ? null : CurrentUserId,   // signed in? link the account
                FromName = form.From.Trim(),
                ReporterRole = form.Role,
                Email = form.Email.Trim(),
                Category = form.Category,
                Related = form.Related,
                Title = form.Title.Trim(),
                Body = form.Body.Trim(),
                Priority = form.Priority == "high" ? ReportPriority.High : form.Priority == "low" ? ReportPriority.Low : ReportPriority.Medium
            };
            // Only the file names are saved (the files themselves are not uploaded in this version).
            foreach (var name in form.Attachments.Take(5))
                report.Attachments.Add(new ReportAttachment { FilePath = name });

            db.Reports.Add(report);
            db.SaveChanges();
            return Json(ToJson(report));
        }

        // "InReview" -> "In review"
        public static string StatusText(ReportStatus status)
        {
            return status == ReportStatus.InReview ? "In review" : status.ToString();
        }

        // JSON shape used by admin.js (Attachments must be loaded with .Include).
        public static object ToJson(Report r)
        {
            return new
            {
                id = r.Id.ToString(),
                title = r.Title,
                category = r.Category,
                priority = r.Priority.ToString().ToLower(),
                from = r.FromName,
                role = r.ReporterRole,
                email = r.Email,
                createdAt = Format.Iso(r.CreatedAt),
                status = StatusText(r.Status),
                read = r.IsRead,
                related = r.Related,
                attachments = r.Attachments.Select(a => a.FilePath),
                body = r.Body,
                response = r.Response ?? ""
            };
        }
    }
}
