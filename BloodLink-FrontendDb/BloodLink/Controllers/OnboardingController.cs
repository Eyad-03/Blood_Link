using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Controllers
{
    // Verification of a new hospital account (the window on /Hospital before the admin approves it).
    //   GET  /api/hospital/onboarding                    status + the 3 documents
    //   POST /api/hospital/onboarding/documents/{docId}  upload one document (form-data, field "file")
    //   POST /api/hospital/onboarding/submit             "Send for review"
    // These work even while the institution is Pending / Rejected.
    [Authorize(Roles = "Hospital")]
    public class OnboardingController : BaseController
    {
        private readonly IWebHostEnvironment env;

        public OnboardingController(BloodLinkDbContext db, IWebHostEnvironment env) : base(db)
        {
            this.env = env;
        }

        // The organization of the signed-in staff member, with its documents.
        private Organization? MyOrg()
        {
            int orgId = CurrentUser()?.OrganizationId ?? 0;
            return db.Organizations.Include(o => o.Documents).FirstOrDefault(o => o.Id == orgId);
        }

        [HttpGet("/api/hospital/onboarding")]
        public IActionResult Status()
        {
            var org = MyOrg();
            if (org == null) return Error("Hospital not found.", 404);
            return Json(StatusJson(org));
        }

        [HttpPost("/api/hospital/onboarding/documents/{docId}")]
        [RequestSizeLimit(UploadStorage.MaxBytes + 100_000)]
        public async Task<IActionResult> Upload(int docId, IFormFile? file)
        {
            var org = MyOrg();
            if (org == null) return Error("Hospital not found.", 404);
            if (org.Status == OrganizationStatus.Active) return Error("Your institution is already approved.");

            var doc = org.Documents.FirstOrDefault(d => d.Id == docId);
            if (doc == null) return Error("Document not found.", 404);

            // ---- check the file ----
            if (file == null || file.Length == 0) return Error("Please choose a file.");
            if (file.Length > UploadStorage.MaxBytes) return Error("The file is larger than 10 MB.");
            string ext = Path.GetExtension(file.FileName).ToLower();
            if (!UploadStorage.AllowedTypes.ContainsKey(ext)) return Error("Only PDF, JPG or PNG files are allowed.");

            // ---- save it: App_Data/uploads/org-5/doc-12.pdf ----
            string relativePath = Path.Combine($"org-{org.Id}", $"doc-{doc.Id}{ext}");
            string fullPath = UploadStorage.FullPath(env, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            // Remove the old file if the new one has another extension (e.g. .png → .pdf).
            if (doc.FileSizeBytes != null && doc.FilePath != relativePath)
            {
                string oldPath = UploadStorage.FullPath(env, doc.FilePath);
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            doc.FilePath = relativePath;
            doc.FileName = Path.GetFileName(file.FileName);
            doc.FileSizeBytes = file.Length;
            doc.UploadedAt = DateTime.UtcNow;
            doc.IsChecked = false;   // a new file must be checked again by the admin
            db.SaveChanges();

            return Json(StatusJson(org));
        }

        [HttpPost("/api/hospital/onboarding/submit")]
        public IActionResult Submit()
        {
            var org = MyOrg();
            if (org == null) return Error("Hospital not found.", 404);
            if (org.Status == OrganizationStatus.Active) return Error("Your institution is already approved.");
            if (org.Documents.Count == 0 || org.Documents.Any(d => d.FileSizeBytes == null))
                return Error("Please upload all the required documents first.");

            org.SubmittedAt = DateTime.UtcNow;
            if (org.Status == OrganizationStatus.Rejected)
            {
                // Sending new documents after a rejection puts the request back in the admin's list.
                org.Status = OrganizationStatus.Pending;
                org.RejectionReason = null;
            }
            db.SaveChanges();
            return Json(StatusJson(org));
        }

        // The JSON shape used by hospital-onboarding.js.
        private static object StatusJson(Organization org)
        {
            return new
            {
                status = org.Status.ToString(),          // Pending | Active | Rejected
                submitted = org.SubmittedAt != null,     // pressed "Send for review"?
                name = org.Name,
                reason = org.RejectionReason ?? "",
                documents = org.Documents.OrderBy(d => d.Id).Select(d => new
                {
                    id = d.Id,
                    name = d.Name,
                    uploaded = d.FileSizeBytes != null,
                    fileName = d.FileName ?? "",
                    size = UploadStorage.SizeText(d.FileSizeBytes)
                })
            };
        }
    }
}
