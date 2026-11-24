# BloodLink — ASP.NET Core MVC (.NET 10)

موقع BloodLink: الواجهات (Views) + قاعدة البيانات (SQL Server) + الـ Backend بـ **ASP.NET MVC فقط** وتسجيل الدخول بـ **JWT**.

## التشغيل في Visual Studio

1. ثبّت SQL Server Express (الـ connection string في `BloodLink/appsettings.json`).
2. افتح `BloodLink.sln` في Visual Studio 2026.
3. اضغط **F5**.

عند التشغيل البرنامج بيعمل الداتابيس تلقائياً (`Database.Migrate()`) وبيضيف حسابات تجريبية:

| Role      | Email                 | Password        |
|-----------|-----------------------|-----------------|
| Admin     | admin@bloodlink.jo    | Admin@123       |
| Hospital  | hospital@alhayat.jo   | Hospital@123    |
| Recipient | ahmad@example.com     | Recipient@123   |
| Donor     | lina@example.com (و omar@, zaid@, noor@ … @example.com) | Donor@123 |

## ترتيب المشروع (Model – View – Controller)

```
BloodLink/
  Models/
    Entities/        جداول قاعدة البيانات (User, BloodRequest, ...)
    Forms/           البيانات اللي بتبعتها الصفحات (LoginForm, RegisterForm, ...)
  Views/             الصفحات (.cshtml)
  Controllers/
    HomeController, AccountController, DonorController, ...   ← بترجّع الصفحات (View)
    BaseController   ← الأب لكل controllers البيانات (db + CurrentUser + Error)
    AuthController, ProfileController, RequestsController,   ← بترجّع بيانات JSON
    ChatController, DonorsController, AppointmentsController,     للجافاسكربت
    HospitalPanelController, OnboardingController,
    AdminPanelController, AdsController, HallsController,
    ReportsController, ChatbotController
  Data/              BloodLinkDbContext + Migrations + بيانات تجريبية
  Helpers/           JwtHelper (إنشاء التوكن), Format, DonationRules, EmailSender, UploadStorage
  wwwroot/           css + js + images
  Program.cs
Database/BloodLink.sql   سكربت SQL كامل لإنشاء الداتابيس (اختياري)
```

كل الـ controllers هي **MVC Controller عادي** (`: Controller`) — ما في `[ApiController]` ولا `ControllerBase`.
controllers الصفحات بترجّع `View()`، و controllers البيانات بترجّع `Json(...)`.

## كيف بيشتغل الـ JWT

1. صفحة الـ Login بتبعت الإيميل والباسورد لـ `POST /api/auth/login` (`AuthController.Login`).
2. السيرفر بيتأكد من الباسورد وبيعمل **token** (`Helpers/JwtHelper.cs`) وبيرجعه.
3. `wwwroot/js/api.js` بيحفظ التوكن في `localStorage` وبيبعته مع كل طلب:
   `Authorization: Bearer <token>`
4. `Program.cs` (`AddJwtBearer`) بيقرأ التوكن، فـ `[Authorize]` و `[Authorize(Roles = "Admin")]` بيشتغلوا.
5. `CurrentUserId` في `BaseController` بيجيب رقم المستخدم من التوكن.

## الدفع (Payment)

مش موجود لسا. الإعلانات (Ads) بتنحفظ بـ `AwaitingPayment = true` (مخفية لحد ما تندفع)،
وحجز القاعات بيضل `Approved` لحد الدفع. الصفحات بتنادي على:
- `POST /api/payments/ad/{id}` → لما ينجح الدفع: `ad.AwaitingPayment = false`
- `POST /api/payments/hall-booking/{id}` → لما ينجح الدفع: `booking.Status = HallBookingStatus.Paid`
- `GET /api/payments/config`

## إعدادات اختيارية (appsettings.json)

- `Jwt:Key` — غيّره لنص طويل وسري.
- `Email` — عشان المستشفى يبعت إيميلات للمتبرعين (Gmail: استخدم App Password).
- `Ollama` — للمساعد (Chatbot). إذا Ollama مش شغّال، المساعد بيقول إنه offline وباقي الموقع بيشتغل عادي.
