using BloodLink.Data;
using BloodLink.Helpers;
using BloodLink.Models.Entities;
using BloodLink.Models.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodLink.Controllers
{
    // Chat between donor and recipient — one conversation per blood request (chat.js).
    //   GET  /api/chat/unread        unread count for each of my requests
    //   GET  /api/chat/{requestId}   messages (also marks them as read)
    //   POST /api/chat/{requestId}   send a message
    [Authorize]
    public class ChatController : BaseController
    {
        public ChatController(BloodLinkDbContext db) : base(db) { }

        [HttpGet("/api/chat/unread")]
        public IActionResult Unread()
        {
            int me = CurrentUserId;
            var myRequestIds = db.BloodRequests
                .Where(r => r.DonorId == me || r.RecipientId == me)
                .Select(r => r.Id)
                .ToList();

            // { "12": 3, "15": 1 }  → request id : number of new messages
            var result = new Dictionary<string, int>();
            foreach (int requestId in myRequestIds)
            {
                var state = db.ChatReadStates.FirstOrDefault(s => s.BloodRequestId == requestId && s.UserId == me);
                DateTime lastRead = state == null ? DateTime.MinValue : state.LastReadAt;

                int count = db.ChatMessages.Count(m => m.BloodRequestId == requestId
                    && m.SenderId != null && m.SenderId != me && m.SentAt > lastRead);

                if (count > 0) result[requestId.ToString()] = count;
            }
            return Json(result);
        }

        [HttpGet("/api/chat/{requestId}")]
        public IActionResult Messages(int requestId)
        {
            var request = db.BloodRequests.Find(requestId);
            if (request == null) return Error("Chat not found.", 404);
            int me = CurrentUserId;
            if (request.DonorId != me && request.RecipientId != me) return Error("This is not your chat.", 403);

            var messages = db.ChatMessages
                .Where(m => m.BloodRequestId == requestId)
                .OrderBy(m => m.SentAt)
                .ToList()
                .Select(m => new
                {
                    id = m.Id.ToString(),
                    from = m.SenderType.ToString().ToLower(),   // system | donor | recipient
                    text = m.Text,
                    at = Format.Iso(m.SentAt)
                });

            // When did the OTHER person last open the chat? (used for the "seen" ticks)
            int otherId = request.DonorId == me ? request.RecipientId : request.DonorId;
            var otherRead = db.ChatReadStates.FirstOrDefault(s => s.BloodRequestId == requestId && s.UserId == otherId);

            MarkAsRead(requestId, me);
            db.SaveChanges();

            return Json(new { messages, peerReadAt = Format.Iso(otherRead?.LastReadAt) });
        }

        [HttpPost("/api/chat/{requestId}")]
        public IActionResult Send(int requestId, [FromBody] ChatMessageForm form)
        {
            var request = db.BloodRequests.Find(requestId);
            if (request == null) return Error("Chat not found.", 404);
            int me = CurrentUserId;
            if (request.DonorId != me && request.RecipientId != me) return Error("This is not your chat.", 403);

            if (request.Status != BloodRequestStatus.Accepted)
                return Error("You can chat only while the request is accepted.");

            string text = (form?.Text ?? "").Trim();
            if (text.Length == 0) return Error("Write a message first.");
            if (text.Length > 500) text = text.Substring(0, 500);

            db.ChatMessages.Add(new ChatMessage
            {
                BloodRequestId = requestId,
                SenderId = me,
                SenderType = request.DonorId == me ? ChatSender.Donor : ChatSender.Recipient,
                Text = text
            });
            MarkAsRead(requestId, me);
            db.SaveChanges();
            return Json(new { ok = true });
        }

        // Remember that this user opened the chat now.
        private void MarkAsRead(int requestId, int userId)
        {
            var state = db.ChatReadStates.FirstOrDefault(s => s.BloodRequestId == requestId && s.UserId == userId);
            if (state == null)
            {
                state = new ChatReadState { BloodRequestId = requestId, UserId = userId };
                db.ChatReadStates.Add(state);
            }
            state.LastReadAt = DateTime.UtcNow;
        }
    }
}
