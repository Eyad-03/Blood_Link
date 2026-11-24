using System.Security.Claims;
using BloodLink.Data;
using BloodLink.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace BloodLink.Controllers
{
    // Every controller that sends data to the JavaScript pages inherits from this class.
    // It is a normal MVC Controller. It gives:
    //   db              → the database
    //   CurrentUserId   → the id of the signed-in user (read from the JWT token)
    //   CurrentUser()   → the signed-in user from the database
    //   Error(...)      → an error answer: { "message": "..." } with a status code
    public abstract class BaseController : Controller
    {
        protected readonly BloodLinkDbContext db;

        protected BaseController(BloodLinkDbContext db)
        {
            this.db = db;
        }

        // The user id saved inside the JWT token (0 when nobody is signed in).
        protected int CurrentUserId
        {
            get
            {
                string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(value, out int id) ? id : 0;
            }
        }

        // Loads the signed-in user from the database (null when nobody is signed in).
        protected User? CurrentUser()
        {
            if (CurrentUserId == 0) return null;
            return db.Users.Find(CurrentUserId);
        }

        // Error answer for the page. The JavaScript shows "message" to the user.
        //   400 = wrong data, 401 = not signed in, 403 = not allowed, 404 = not found, 409 = already exists
        protected JsonResult Error(string message, int statusCode = 400)
        {
            return new JsonResult(new { message }) { StatusCode = statusCode };
        }

        // Saves a line in the admin "Recent activity" list.
        protected void LogAdminAction(string text, string icon, string color)
        {
            db.AdminActivityLogs.Add(new AdminActivityLog
            {
                AdminId = CurrentUserId == 0 ? null : CurrentUserId,
                Text = text,
                Icon = icon,
                Color = color
            });
        }
    }
}
