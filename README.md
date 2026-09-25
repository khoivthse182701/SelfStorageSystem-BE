# Self Storage Facility Rental and Management System - Backend & Database

Dự án Hệ thống Quản lý và Cho thuê Kho Lưu Trữ Tự Phục Vụ (Self-Storage Facility Rental and Management System) được phát triển theo kiến trúc **Clean Architecture** sử dụng **ASP.NET Core 8 Web API** và **SQL Server**.

---

## 📁 Cấu trúc Thư mục Repository

```text
.
├── BE/                                    # Mã nguồn Backend (.NET 8 Clean Architecture)
│   ├── SelfStorageSystem.sln              # Solution chính của hệ thống
│   ├── SelfStorageSystem/                 # Web API Project (Controllers, Middlewares, Program.cs)
│   │   ├── Controllers/                   # RESTful API Endpoints (AuthController, v.v.)
│   │   ├── Middlewares/                   # SecurityHeadersMiddleware, Rate Limiter
│   │   ├── appsettings.json               # Cấu hình hệ thống (Template/Safe config)
│   │   ├── appsettings.Local.json         # (Git-ignored) Cấu hình bí mật cá nhân chạy local
│   │   └── appsettings.example.json       # File mẫu tham khảo cấu hình
│   ├── SelfStorageSystem.Application/     # Tầng Application (Interfaces, DTOs, Settings)
│   ├── SelfStorageSystem.Contracts/       # Tầng Contracts (Request, Response, Common ApiResponse)
│   ├── SelfStorageSystem.Domain/          # Tầng Domain (Entities, Constants, Enums)
│   ├── SelfStorageSystem.Infrastructure/  # Tầng Infrastructure (EF Core, Services, Email, Auth)
│   └── SelfStorageSystem.Tests/           # Dự án Unit Test kiểm thử hệ thống (xUnit)
├── DB/                                    # Cơ sở dữ liệu SQL Server
│   └── SelfStoragePRN222.sql              # Kịch bản SQL duy nhất khởi tạo hoàn chỉnh CSDL
└── README.md                              # Tài liệu hướng dẫn tổng quan dự án
```

---

## 🚀 Tính năng Cốt lõi & Cơ chế Bảo mật

### 1. Xác thực & Phân quyền (Authentication & Authorization)
- **JWT Bearer Token**: Đăng nhập trả về Access Token bảo mật kèm thông tin người dùng và phân quyền (`storage_customer`, `admin`, `manager`, `staff`).
- **Google OAuth 2.0**: Hỗ trợ đăng nhập / đăng ký trực tiếp bằng tài khoản Google (`POST /api/auth/google-login`) thông qua Google ID Token.
- **Xác thực OTP qua Gmail**:
  - Đăng ký tài khoản mới tự động sinh mã OTP ngẫu nhiên gửi về hòm thư Gmail của khách hàng.
  - Hỗ trợ gửi lại OTP (`POST /api/auth/resend-otp`).
  - Kích hoạt tài khoản an toàn sau khi xác thực OTP thành công (`POST /api/auth/verify-otp`).

### 2. Cơ chế Chống Spam & Brute-Force (Anti-Spam & Rate Limiting)
- **Cooldown gửi lại OTP**: Giới hạn tối thiểu 60 giây giữa các lần yêu cầu gửi mã mới.
- **Giới hạn số lần yêu cầu theo giờ**: Tối đa 5 lần yêu cầu OTP trong vòng 1 giờ cho mỗi địa chỉ email.
- **Chống tấn công dò mã (Brute-Force Protection)**:
  - Cho phép tối đa 5 lần nhập sai mã OTP liên tiếp.
  - Sau 5 lần nhập sai: Mã OTP lập tức bị hủy và tính năng xác thực bị khóa tạm thời trong 15 phút.
- **ASP.NET Core Rate Limiting Middleware**:
  - Global Rate Limiter: Tối đa 100 requests/phút theo IP cho toàn bộ hệ thống API.
  - Auth Strict Limiter: Tối đa 10 requests/phút theo IP riêng cho các endpoint xác thực (`/api/auth/*`).
  - Trả về mã lỗi HTTP `429 Too Many Requests` với format chuẩn `ApiResponse.Fail(...)`.

### 3. Tiêu chuẩn An toàn Thông tin (Information Disclosure Hardening)
- **Không hardcode**: Toàn bộ cấu hình (Chuỗi kết nối, JWT Secret, MailKit SMTP, Google ClientId, Rate Limiting, OTP Settings, CORS) được quản lý qua `IOptions<T>` và `appsettings.json`.
- **Bảo vệ Secret Keys**: Hỗ trợ `appsettings.Local.json` (được đưa vào `.gitignore`) để lưu mật khẩu email thật và Google Client Secret cục bộ, tránh rò rỉ lên GitHub.
- **Chống rò rỉ mã lỗi 500**: Các ngoại lệ nội bộ của hệ thống (SQL, SMTP) chỉ ghi log tại máy chủ; client chỉ nhận thông báo thân thiện chuẩn hóa, không lộ chuỗi kết nối hay chi tiết CSDL.
- **Chống dò quét tài khoản (User Enumeration)**: Đồng nhất mã lỗi `401 Unauthorized` cho cả trường hợp sai email lẫn sai mật khẩu khi đăng nhập.
- **Security Response Headers**: Tích hợp middleware tự động bổ sung `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `X-XSS-Protection: 1; mode=block`, `Referrer-Policy: strict-origin-when-cross-origin` và ẩn server banner `Server: Kestrel`.
- **Vô hiệu hóa Cache nhạy cảm**: Header `Cache-Control: no-store` cho các endpoint xác thực, chống lộ token tại proxy/trình duyệt.

---

## 🗄️ Cài đặt Cơ sở Dữ liệu (Database Setup)

File SQL duy nhất chứa toàn bộ 58 bảng, triggers và dữ liệu mẫu nằm tại:
👉 **[`DB/SelfStoragePRN222.sql`](DB/SelfStoragePRN222.sql)**

### Cách cài đặt:
- **Cách 1**: Mở file `DB/SelfStoragePRN222.sql` trong **SQL Server Management Studio (SSMS)** và nhấn **Execute (F5)**.
- **Cách 2**: Chạy bằng dòng lệnh `sqlcmd`:
  ```powershell
  sqlcmd -S localhost -E -i "DB\SelfStoragePRN222.sql"
  ```

---

## 💻 Hướng dẫn Chạy Backend (.NET 8)

### 1. Yêu cầu hệ thống
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/sql-server) (2019 hoặc mới hơn)

### 2. Cấu hình
Mở file `BE/SelfStorageSystem/appsettings.json` (hoặc tạo `BE/SelfStorageSystem/appsettings.Local.json` để chạy local):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SelfStoragePRN222;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "JwtSettings": {
    "Issuer": "SelfStorageSystem",
    "Audience": "SelfStorageSystem.Api",
    "Secret": "SuperSecretKeyWithAtLeast32CharsLength123456789!",
    "AccessTokenMinutes": 60,
    "RefreshTokenDays": 7
  },
  "MailSettings": {
    "Host": "smtp.gmail.com",
    "Port": 465,
    "SenderEmail": "your-email@gmail.com",
    "SenderName": "Self Storage System",
    "Username": "your-email@gmail.com",
    "Password": "your-gmail-16-digit-app-password",
    "EnableSsl": true
  },
  "GoogleAuthSettings": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
  }
}
```

### 3. Khởi chạy ứng dụng
Di chuyển vào thư mục backend và chạy lệnh:
```powershell
cd BE
dotnet restore
dotnet build
dotnet run --project SelfStorageSystem
```

- Truy cập Swagger API Documentation: `http://localhost:5000/swagger` (hoặc cổng HTTPS tương ứng được cấu hình).

### 4. Chạy Unit Tests
```powershell
dotnet test SelfStorageSystem.sln
```
*Tất cả 6/6 tests tự động kiểm thử toàn bộ luồng Cooldown, Hourly Limit, Lockout và Countdown số lần thử sai đều đạt 100% Passed.*
