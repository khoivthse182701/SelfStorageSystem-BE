# API CONTRACT SPECIFICATION (`API_CONTRACT.md`)

> **Project:** Self-Storage Management System  
> **API Version:** `v1` (Base Route: `/api/v1`)  
> **Status:** **DESIGN FOR APPROVAL — AUTH/PROFILE + FLOW 1, FLOW 2 & FLOW 3 (WITH FLOW 4–7 FRAMEWORK)**  
> **Roles (4):** `Customer` (`storage_customer`), `Staff` (`facility_staff`), `Manager` (`facility_manager` — Global Authority), `Admin` (`system_administrator`)

---

## 1. Quy Ước Chung & Phân Quyền

### 1.1. Standard Response Envelope (`ApiResponse<T>`)
```json
{
  "success": true,
  "message": "Human-readable message",
  "data": {},
  "errorCode": null,
  "errors": null,
  "traceId": "00-..."
}
```
- Lỗi validation (`FluentValidation`) trả `422 Unprocessable Entity` kèm `errors: Record<string, string[]>`.
- `Manager` có **toàn quyền vận hành trên mọi chi nhánh** (không bị chặn bởi `staff_facility_assignments`).
- `Staff` làm việc theo chi nhánh được phân công (`EnsureStrictFacilityAccessAsync`).

---

## 2. Xác Thực, Đăng Ký & Quản Lý Profile (`Customer`)

### `2.1` `POST /api/v1/auth/register` — `[AllowAnonymous]`
- **Nghiệp vụ:** Khách hàng đăng ký bắt buộc nhập đầy đủ `FullName`, `PhoneNumber`, `IdentityNumber (CCCD)`, `Email`, `Password`.
- **Request (`RegisterRequest` + FluentValidation):**
  ```json
  {
    "fullName": "Nguyễn Minh Anh",
    "phoneNumber": "0901234567",
    "identityNumber": "079200001234",
    "email": "minhanh@example.com",
    "password": "Password@123"
  }
  ```
  - **Validation:**
    - `fullName`: required, `2..100` ký tự.
    - `phoneNumber`: required, định dạng SĐT hợp lệ (`^(0|\+84)\d{9,10}$`).
    - `identityNumber`: required, `9..12` chữ số (`^\d{9,12}$`), duy nhất trong `customer_profiles.identity_number`.
    - `email`: required, email hợp lệ, duy nhất trong `users.email`.
    - `password`: required, tối thiểu 6 ký tự.
- **Response `200 OK`:** `{ "email": "minhanh@example.com", "otpCooldownSeconds": 60, "message": "OTP sent to email." }`
- **Codes:** `200`, `409` (`Auth.EmailAlreadyInUse`, `Auth.IdentityNumberAlreadyInUse`), `422`, `429`.

### `2.2` `POST /api/v1/auth/verify-otp`, `POST /api/v1/auth/login`, `GET /api/v1/auth/me`
- `GET /api/v1/auth/me` (`[Authorize]`): trả thông tin user + `profile` + trạng thái cảnh báo nợ cọc âm (`hasNegativeDepositDebt`, `totalNegativeDebtAmount`, `pendingOverdueBankPrompts[]`) để Frontend tự động bật **Banner cảnh báo đỏ cố định** hoặc **Popup nhập STK hoàn cọc quá hạn 10 ngày** ngay khi đăng nhập!

### `2.3` `GET /api/v1/profile` — `[Authorize(Roles = "Customer")]`
- **Response `200 OK` (`CustomerProfileDto`):**
  ```json
  {
    "userId": 12,
    "email": "minhanh@example.com",
    "fullName": "Nguyễn Minh Anh",
    "phoneNumber": "0901234567",
    "identityNumber": "079200001234",
    "identityNumberLocked": true,
    "address": "TP. Thủ Đức, TP.HCM",
    "currentEditMonth": "2026-11",
    "editsUsedThisMonth": 1,
    "maxEditsPerMonth": 2,
    "remainingEditsThisMonth": 1,
    "createdAt": "2026-10-01T08:00:00Z",
    "updatedAt": "2026-11-10T09:30:00Z"
  }
  ```

### `2.4` `PUT /api/v1/profile` — `[Authorize(Roles = "Customer")]`
- **Nghiệp vụ:**
  - Trường `identityNumber (CCCD)` **bị khóa hoàn toàn** (không nhận trong request hoặc nếu gửi khác giá trị gốc sẽ trả `422 Profile.IdentityNumberLocked`).
  - Khi `fullName` hoặc `phoneNumber` thay đổi so với giá trị hiện tại trong DB:
    - Kiểm tra `currentMonth = UtcNow.ToString("yyyy-MM")` (theo múi giờ Việt Nam).
    - Nếu `profile.ProfileEditMonth != currentMonth` -> reset `ProfileEditMonth = currentMonth`, `ProfileEditCount = 0`.
    - Nếu `ProfileEditCount >= 2` -> chặn và trả **`409 Conflict` (`Profile.MonthlyEditLimitReached`: `"Bạn chỉ được phép chỉnh sửa Họ tên hoặc Số điện thoại tối đa 2 lần trong 1 tháng."`)**.
    - Nếu `< 2` -> cập nhật `fullName` / `phoneNumber` và tăng `ProfileEditCount += 1`.
- **Request (`UpdateCustomerProfileRequest`):**
  ```json
  {
    "fullName": "Nguyễn Minh Anh",
    "phoneNumber": "0909888777",
    "address": "Quận 1, TP.HCM",
    "emergencyContactName": "Nguyễn Minh Tâm",
    "emergencyContactPhone": "0909111222"
  }
  ```
- **Response `200 OK`:** `CustomerProfileDto` (cập nhật `editsUsedThisMonth` và `remainingEditsThisMonth`).
- **Codes:** `200`, `401`, `409` (`Profile.MonthlyEditLimitReached`), `422` (`Profile.IdentityNumberLocked`, validation).

---

## 3. FLOW 1: STORAGE UNIT RESERVATION FLOW (ĐẶT THUÊ KHO)

### `3.1` `GET /api/v1/facilities` & `GET /api/v1/facilities/{facilityId:long}` & `GET /api/v1/unit-types` — `[AllowAnonymous]`
- Trả danh sách chi nhánh, loại kho, kích thước (`widthM`, `lengthM`, `heightM`, `areaM2`, `volumeM3`), bảng giá niêm yết (`monthlyRate`, `depositAmount`, `bookingFee`) và số lượng ô kho trống.

### `3.2` `GET /api/v1/facilities/{facilityId:long}/units/available` — `[AllowAnonymous]`
- **Query:** `unitTypeId?: long`, `areaId?: long`, `startDate?: DateOnly`, `durationMonths?: int (1..12, default 1)`
- **Rule:** Chỉ trả các ô kho `is_listed = 1`, không nằm trong `maintenance | out_of_service | damaged | cleaning | pending_inspection` và không có `unit_allocations (status = 'active')` giao khoảng `[startDate, startDate + durationMonths)`.
- **Response `200 OK`:** `AvailableUnitDto[]` (`[{ id, facilityId, unitTypeId, unitCode, floorLabel, zoneLabel, widthM, lengthM, heightM, areaM2, volumeM3, monthlyRate, depositAmount, mapPosition }]`).

### `3.3` `POST /api/v1/pricing/quote` — `[AllowAnonymous]`
- **Request:** `{ "facilityId": 1, "unitTypeId": 2, "startDate": "2026-11-20", "durationMonths": 3, "promotionCode": "WELCOME10" }`
- **Response `200 OK` (`PricingQuoteDto`):**
  ```json
  {
    "baseMonthlyRate": 1500000,
    "durationMonths": 3,
    "rentSubtotal": 4500000,
    "depositAmount": 1500000,
    "bookingFee": 50000,
    "discountAmount": 150000,
    "vatRatePercent": 10,
    "taxAmount": 140000,
    "firstPaymentAmount": 3040000,
    "totalContractAmount": 6040000,
    "appliedPromotionCode": "WELCOME10"
  }
  ```

### `3.4` `POST /api/v1/reservations` — `[Authorize(Roles = "Customer")]`
- **Request (`CreateReservationRequest`):**
  ```json
  {
    "facilityId": 1,
    "unitTypeId": 2,
    "storageUnitId": 12,
    "startDate": "2026-11-20",
    "durationMonths": 3,
    "promotionCode": "WELCOME10",
    "idempotencyKey": "optional-client-uuid"
  }
  ```
- **Rule:** Khóa `UPDLOCK, ROWLOCK` (hoặc `READPAST` nếu `storageUnitId == null` để hệ thống tự gán ô trống), kiểm tra chống đặt trùng trên `unit_allocations`, tạo `Reservation(status = 'pending', holdUntil = now + 15m)`, `UnitAllocation(reservation_hold, active)`, và `Invoice(open)`.
- **Response `201 Created` (`CreateReservationResponse`):** Trả `reservationId`, `reservationCode`, `status: "pending"`, `holdUntil`, `expiresInSeconds: 900`, `invoiceId`, `invoiceNo`, `unitId`, `unitCode`, `firstPaymentAmount`, `quotedTotal`.
- **Codes:** `201`, `401`, `404`, `409` (`Reservation.UnitNotAvailable`, `Reservation.NoAvailableUnits`), `422`.

### `3.5` `POST /api/v1/payments/checkout` & `GET /api/v1/payments/{paymentId:long}/status` & `POST /api/v1/webhooks/sepay`
- **`POST /api/v1/payments/checkout` (`[Authorize(Roles = "Customer")]`):**
  - Request: `{ "reservationId": 105, "invoiceId": null, "renewalId": null, "transferId": null, "paymentMethod": "bank_transfer" }`
  - Response `200 OK`: trả `paymentId`, `invoiceId`, `amount`, `expiresInSeconds`, và `vietQr { bankCode, accountNo, accountName, amount, transferContent: "DH310", qrImageUrl }`.
- **`POST /api/v1/webhooks/sepay` (`[AllowAnonymous]`, Header `Authorization: Apikey <key>`):**
  - Chống trùng lặp qua `[core].[integration_events] (source = 'sepay', external_event_id)`.
  - Khi thanh toán đơn đặt kho (`Reservation`):
    1. Ghi `Payment(succeeded)` + `PaymentAllocation`, cập nhật `Invoice -> paid`.
    2. Chuyển `Reservation -> confirmed`, tạo `RentalAgreement(status = 'draft')`.
    3. Sinh mã `checkInQrToken` ký HMAC-SHA256 (`v1.<payload>.<signature>`).
    4. Lưu `Notification` và bắn SignalR realtime (`ReservationConfirmed`, `UnitStatusChanged`) cho cả `Customer` (`user_{customerId}`) và `Staff` tại chi nhánh (`facility_{facilityId}`).

---

## 4. FLOW 2: STORAGE CHECK-IN AND HANDOVER FLOW (NHẬN KHO & BÀN GIAO TẠI QUẦY)

### `4.1` `GET /api/v1/staff/reservations/lookup` — `[Authorize(Roles = "Staff,Manager")]`
- **Query:** `facilityId?: long`, `query?: string` *(mã đơn / tên / SĐT / CCCD)*, `status?: string (default "confirmed")`
- **Response `200 OK`:** Danh sách đơn đặt chỗ `confirmed` tại chi nhánh kèm thông tin khách hàng (`customerName`, `customerPhone`, `identityNumberMasked`) và ô kho hiện tại (`assignedUnitId`, `assignedUnitCode`, `unitTypeId`, `unitTypeName`, `dimensions`).

### `4.2` `POST /api/v1/staff/check-ins/verify-qr` — `[Authorize(Roles = "Staff,Manager")]`
- **Nghiệp vụ (Xác thực kép Bước 1 + Trả sẵn danh sách ô kho dự phòng cùng loại & kích thước):**
  - Staff quét mã QR Check-in của Customer (`qrToken`) **VÀ** có thể nhập ngay số `identityNumber (CCCD)` của Customer (hoặc nhập ở bước xác nhận).
  - Hệ thống kiểm tra chữ ký HMAC của `qrToken`, kiểm tra đơn ở trạng thái `confirmed`, đối chiếu `identityNumber` (nếu truyền lên) với `customer_profiles.identity_number`, và **luôn truy vấn trả về danh sách `availableBackupUnits[]`** gồm toàn bộ các ô kho đang trống (`available`) có **cùng `unitTypeId` và đúng kích thước** tại chi nhánh đó để Staff thấy ngay trên màn hình check-in!
- **Request:**
  ```json
  {
    "qrToken": "v1....",
    "facilityId": 1,
    "customerIdentityNumber": "079200001234"
  }
  ```
- **Response `200 OK` (`StaffVerifyCheckInResponse`):**
  ```json
  {
    "actionType": "check_in",
    "valid": true,
    "identityVerified": true,
    "customer": {
      "customerId": 12,
      "fullName": "Nguyễn Minh Anh",
      "phoneNumber": "0901234567",
      "email": "minhanh@example.com",
      "identityNumberMasked": "********1234",
      "hasRegisteredIdentityNumber": true
    },
    "reservation": {
      "reservationId": 105,
      "reservationCode": "RSV-20261117-0105",
      "startDate": "2026-11-20",
      "endDate": "2027-02-20",
      "status": "confirmed"
    },
    "agreement": {
      "agreementId": 88,
      "agreementNo": "AGR-HCM01-20261117-0105",
      "status": "draft"
    },
    "unit": {
      "unitId": 12,
      "unitCode": "A-102",
      "facilityId": 1,
      "unitTypeId": 2,
      "unitTypeName": "Medium Dry Unit 6m2",
      "dimensions": "2.0m x 3.0m x 2.5m",
      "areaM2": 6.0,
      "physicalStatus": "reserved"
    },
    "availableBackupUnits": [
      {
        "unitId": 15,
        "unitCode": "A-105",
        "floorLabel": "1",
        "zoneLabel": "A",
        "unitTypeId": 2,
        "unitTypeName": "Medium Dry Unit 6m2",
        "dimensions": "2.0m x 3.0m x 2.5m",
        "areaM2": 6.0,
        "physicalStatus": "available"
      }
    ],
    "allowedActions": ["reassign_unit", "confirm_checkin", "report_issue"]
  }
  ```
- **Codes:** `200`, `401`, `403`, `404`, `409` (`Staff.ReservationNotConfirmed`), `422` (`Staff.QrCodeInvalid`, `Staff.IdentityNumberMismatch`).

### `4.3` `POST /api/v1/staff/reservations/{reservationId:long}/assign-unit` — `[Authorize(Roles = "Staff,Manager")]`
- **Nghiệp vụ (Đổi kho dự phòng tại chỗ):** Nếu ô kho ban đầu gặp rủi ro/sự cố khi kiểm tra thực tế, Staff chọn 1 ô trong danh sách `availableBackupUnits` cùng `unitTypeId` & kích thước để đổi ngay cho khách trước khi chốt check-in.
- **Request:** `{ "storageUnitId": 15, "markOldUnitStatus": "maintenance", "reason": "Ổ khóa cửa cuốn ô A-102 bị kẹt" }`
- **Response `200 OK`:** `{ "reservationId": 105, "oldUnitId": 12, "oldUnitCode": "A-102", "oldUnitStatus": "maintenance", "newUnitId": 15, "newUnitCode": "A-105", "unitStatus": "reserved" }`

### `4.4` `POST /api/v1/staff/check-ins/confirm` — `[Authorize(Roles = "Staff,Manager")]`
- **Nghiệp vụ (Xác thực kép bắt buộc + Kiểm tra thực tế + Chốt bàn giao & Kích hoạt PIN):**
  1. **Kiểm tra Xác thực kép:** Bắt buộc `identityVerification.documentNumber` (số CCCD Staff nhập tại quầy) phải **khớp chính xác** với `customer_profiles.identity_number` của khách hàng đã đăng ký (nếu lệch trả `422 Staff.IdentityNumberMismatch`: `"Số CCCD nhập tại quầy không khớp với CCCD đăng ký của khách hàng."`).
  2. **Kiểm tra thực tế & Bàn giao:** Lưu `IdentityVerification(verified)`, `Inspection(check_in, completed)`, `InspectionItems` và `HandoverRecord(check_in)`.
  3. **Chuyển trạng thái & Kích hoạt mã PIN:**
     - Chuyển `Reservation -> checked_in` (hoặc `converted`), `RentalAgreement -> active` (`checked_in_at = now`, `signed_at = now`).
     - Chuyển `UnitAllocation (reservation_hold -> consumed)` và tạo `UnitAllocation (rental, active)`.
     - Chuyển `StorageUnit -> occupied`.
     - Sinh mã PIN 6 số, lưu `AccessCredential (credential_type = 'pin', status = 'active', secret_digest = BCrypt(pin), display_hint = PinProtectionHelper.Encrypt(pin))` để Customer xem và đổi được ngay trên trang `Access Control`.
- **Request (`ConfirmCheckInRequest`):**
  ```json
  {
    "reservationId": 105,
    "overrideUnitId": null,
    "identityVerification": {
      "method": "government_id",
      "documentNumber": "079200001234",
      "fullNameMatched": true,
      "notes": "Đã đối chiếu CCCD gốc khớp hồ sơ"
    },
    "inspection": {
      "overallCondition": "good",
      "summary": "Ô kho sạch sẽ, khóa điện tử hoạt động tốt",
      "items": [
        { "itemName": "Cửa & Khóa điện tử", "condition": "good", "notes": "", "photoUrl": null }
      ]
    },
    "customerSignatureRef": "sig_customer_checked",
    "staffSignatureRef": "sig_staff_checked",
    "notes": "Đã bàn giao ô kho và kích hoạt PIN"
  }
  ```
- **Response `200 OK`:**
  ```json
  {
    "handoverId": 44,
    "inspectionId": 71,
    "identityVerificationId": 39,
    "agreementId": 88,
    "agreementNo": "AGR-HCM01-20261117-0105",
    "agreementStatus": "active",
    "reservationStatus": "checked_in",
    "unitId": 12,
    "unitCode": "A-102",
    "unitStatus": "occupied",
    "issuedPin": "482910",
    "checkedInAt": "2026-11-20T02:15:00Z"
  }
  ```

---

## 5. FLOW 3: RENTED STORAGE UNIT MANAGEMENT FLOW (QUẢN LÝ KHO ĐANG THUÊ)

### `5.1` Truy Cập Kho, Hiển Thị Mã PIN & Đổi Mã PIN (Khôi phục từ Git History `AccessControl.jsx`)
- **Kết quả kiểm tra Git History (`git log` trên `FE/src/pages/AccessControl.jsx`):**
  - Trong commit `673117b` & `5f94dce`, trang `AccessControl.jsx` hiển thị thẻ **Main PIN Code** (nút con mắt ẩn/hiện mã PIN 6 số + nút **Change PIN** mở modal nhập `Current PIN` & `New PIN`) nằm cạnh cột bên phải chứa **2 nút thao tác QR / Mở kho**.
  - Ở commit `ed6b64a` (`AccessControl.jsx:303`), điều kiện `isAwaitingCheckIn || (!displayPin && !credentialsLoading)` khiến toàn bộ khung hiển thị Mã PIN hiện tại và nút Đổi mã PIN bị ẩn đi nếu `displayPin` rỗng hoặc seed thiếu `DisplayHint` mã hóa.
  - **Quy tắc chuẩn hóa:**
    - Khi hợp đồng chưa Check-in (`status == 'draft'` hoặc `checkedInAt == null`): hiển thị thông báo *"Chờ Check-in tại quầy để kích hoạt mã PIN"* (khách chưa xem/đổi được PIN).
    - Ngay sau khi hoàn tất Check-in (`status == 'active'` và `checkedInAt != null`): **luôn hiển thị khung Mã PIN hiện tại (`keypadPin`) + nút Đổi mã PIN (`Change PIN`) đặt cùng với 2 nút quét QR hiện có**!
- **Endpoints:**
  - `GET /api/v1/rentals/my` (`[Authorize(Roles = "Customer")]`) -> `RentalSummaryDto[]` (kèm `depositBalance`, `hasNegativeDepositDebt`, `isExpiringSoon`, `checkedInAt`).
  - `GET /api/v1/rentals/{agreementId:long}/access-credentials` (`[Authorize(Roles = "Customer")]`):
    - Chỉ cho phép khi `agreement.Status == "active"` và `agreement.CheckedInAt != null` (nếu chưa check-in trả `409 Rental.CheckInRequired`).
    - Trả `{ credentialId, credentialType: "pin", keypadPin: "482910", status: "active", gateQrToken: "v1....", qrExpiresInSeconds: 60 }`.
  - `PUT /api/v1/rentals/{agreementId:long}/pin` (`[Authorize(Roles = "Customer")]`):
    - Request: `{ "currentPin": "482910", "newPin": "739204" }` (6 chữ số, không trùng PIN cũ, không thuộc chuỗi yếu `123456` / `000000`).
    - Chỉ cho phép khi hợp đồng đang `active` (chặn `409` nếu chưa check-in hoặc đang `overdue`).
    - Response `200 OK`: `{ "changedAt": "...", "keypadPin": "739204", "syncStatus": "synced", "message": "Đổi mã PIN thành công." }`.
  - `POST /api/v1/rentals/{agreementId:long}/unlock` & `GET /api/v1/rentals/{agreementId:long}/access-logs`.

### `5.2` Quản Lý Người Ủy Quyền (`AuthorizedAccessMembers`)
- `GET /api/v1/rentals/{agreementId:long}/authorized-members` (`[Authorize(Roles = "Customer")]`) -> `AuthorizedMemberDto[]`
- `POST /api/v1/rentals/{agreementId:long}/authorized-members` (`[Authorize(Roles = "Customer")]`):
  - Request: `{ "fullName": "Nguyễn Minh Tâm", "identityFingerprint": "079200009999", "relationshipToCustomer": "Anh/Em", "validTo": "2027-02-20T00:00:00Z", "issuePin": true }`
  - Response `201 Created`: `AuthorizedMemberDto` (kèm `issuedMemberPin` nếu `issuePin = true`).
- `DELETE /api/v1/rentals/{agreementId:long}/authorized-members/{memberId:long}` (`[Authorize(Roles = "Customer")]`) -> `200 OK` (thu hồi quyền & mã PIN của người ủy quyền).

### `5.3` Nâng / Hạ Hạng Kho Tự Động (Không Cần Manager Duyệt)

1. **`GET /api/v1/rentals/{agreementId:long}/transfer/preview`** — `[Authorize(Roles = "Customer")]`
   - **Query:** `requestedFacilityId?: long`, `requestedUnitTypeId: long`, `targetUnitId?: long`, `effectiveDate?: DateOnly`
   - **Quy tắc tính toán:**
     - So sánh `targetRate.MonthlyRate` với `agreement.MonthlyRateSnapshot` (và diện tích `targetUnitType.AreaM2` với `currentUnitType.AreaM2`):
       - **Nâng hạng (`upgrade` — kho rộng hơn / giá cao hơn):**
         - `remainingDays = max(1, agreement.EndDate.DayNumber - today.DayNumber)`
         - `diffRentAmount = round((targetRate.MonthlyRate - agreement.MonthlyRateSnapshot) * remainingDays / 30)`
         - `diffDepositAmount = max(0, targetRate.DepositAmount - agreement.DepositSnapshot)`
         - `totalDueAmount = diffRentAmount + diffDepositAmount` (`requiresPayment = true`, `autoCompleteOnPayment = true`).
       - **Hạ hạng (`downgrade` — kho nhỏ hơn / giá thấp hơn):**
         - `diffRentAmount = 0`, `diffDepositAmount = 0`, `totalDueAmount = 0`, `refundAmount = 0` (**không cần thanh toán thêm và không hoàn/bù tiền chênh lệch**).
         - `requiresPayment = false`, `autoCompleteImmediately = true`.
   - **Response `200 OK` (`TransferPreviewDto`):**
     ```json
     {
       "agreementId": 88,
       "transferType": "upgrade",
       "currentUnitCode": "A-101",
       "currentUnitTypeName": "Small Dry Unit 3m2",
       "currentMonthlyRate": 900000,
       "targetUnitTypeId": 2,
       "targetUnitTypeName": "Medium Dry Unit 6m2",
       "targetMonthlyRate": 1500000,
       "availableTargetUnits": [
         { "unitId": 15, "unitCode": "A-105", "floorLabel": "1", "zoneLabel": "A" }
       ],
       "remainingDays": 45,
       "diffRentAmount": 900000,
       "diffDepositAmount": 600000,
       "totalDueAmount": 1500000,
       "requiresPayment": true,
       "autoCompleteWithoutManagerApproval": true
     }
     ```

2. **`POST /api/v1/rentals/{agreementId:long}/transfer`** — `[Authorize(Roles = "Customer")]`
   - **Request:**
     ```json
     {
       "requestedFacilityId": 1,
       "requestedUnitTypeId": 2,
       "targetUnitId": 15,
       "reason": "Cần nâng cấp sang kho rộng hơn"
     }
     ```
   - **Nghiệp vụ tự động (Không qua Manager duyệt):**
     - **Trường hợp 1 — Hạ hạng (`downgrade`, `totalDueAmount == 0`):**
       - Trong 1 Transaction: Khóa ô kho mới (`targetUnitId` hoặc auto-assign ô trống cùng `requestedUnitTypeId`), tạo `UnitTransferRequest(transfer_type = 'downgrade', status = 'completed', completed_at = now)`, giải phóng `UnitAllocation` cũ (`status = 'released'`), chuyển ô kho cũ sang `available`, tạo `UnitAllocation(rental, active)` cho ô kho mới, chuyển ô kho mới sang `occupied`, cập nhật `agreement.MonthlyRateSnapshot = targetRate.MonthlyRate` (không hoàn/bù tiền chênh lệch).
       - Trả **`200 OK`**: `{ "transferId": 55, "transferType": "downgrade", "status": "completed", "requiresPayment": false, "newUnitId": 11, "newUnitCode": "A-101", "oldUnitReleased": "A-105" }`.
     - **Trường hợp 2 — Nâng hạng (`upgrade`, `totalDueAmount > 0`):**
       - Tạo `Invoice(open, totalAmount = totalDueAmount)` + `UnitTransferRequest(transfer_type = 'upgrade', status = 'pending_payment', target_invoice_id = invoice.Id)` + giữ chỗ tạm thời ô kho mới.
       - Trả **`201 Created`**: `{ "transferId": 56, "transferType": "upgrade", "status": "pending_payment", "requiresPayment": true, "invoiceId": 412, "invoiceNo": "INV-...", "totalDueAmount": 1500000, "targetUnitCode": "A-105" }`.
       - Khách hàng gọi `POST /api/v1/payments/checkout` (`{ "invoiceId": 412 }`) để lấy mã VietQR -> Khi **SePay Webhook (`POST /api/v1/webhooks/sepay`)** xác nhận thanh toán thành công `Invoice #412`, hệ thống **tự động thực thi đổi kho ngay lập tức**: chuyển `UnitTransferRequest -> completed`, cập nhật hợp đồng sang ô kho mới (`occupied`), cộng `DepositSnapshot` & `DepositBalance` thêm `diffDepositAmount`, và giải phóng ô kho cũ về `available`!

### `5.4` Trả Kho Sớm, Nghiệm Thu Khấu Trừ Tiền Cọc & Cảnh Báo Nợ Âm (`< 0`)

1. **`GET /api/v1/rentals/{agreementId:long}/move-out/refund-preview`** — `[Authorize(Roles = "Customer")]`
   - Trả `{ originalDeposit, currentDepositBalance, unpaidDebt, estimatedNetRefund, note }`.

2. **`POST /api/v1/rentals/{agreementId:long}/move-out`** — `[Authorize(Roles = "Customer")]`
   - **Nghiệp vụ:** Khách hàng gửi yêu cầu trả kho sớm kèm bắt buộc **thông tin tài khoản ngân hàng** để nhận hoàn cọc.
   - **Request (`CreateMoveOutRequest` + FluentValidation):**
     ```json
     {
       "requestedMoveOutDate": "2026-11-25",
       "reason": "Đã chuyển hết hàng về kho công ty",
       "bankName": "MB Bank",
       "bankAccountNo": "0817495759",
       "bankAccountHolder": "NGUYEN MINH ANH"
     }
     ```
     *(Validation: `requestedMoveOutDate >= facilityToday`, `bankName` required, `bankAccountNo` required `6..32` ký tự, `bankAccountHolder` required).*
   - **Response `201 Created`:**
     `{ "moveOutRequestId": 31, "agreementId": 88, "status": "requested", "requestedMoveOutDate": "2026-11-25", "bankName": "MB Bank", "bankAccountNo": "0817495759", "bankAccountHolder": "NGUYEN MINH ANH", "moveOutQrToken": "v1...." }`

3. **`POST /api/v1/staff/move-outs/{moveOutId:long}/inspect`** — `[Authorize(Roles = "Staff,Manager")]`
   - **Nghiệp vụ (Staff nghiệm thu thực tế & Khấu trừ tiền cọc / Kích hoạt cảnh báo nợ âm):**
     - Staff kiểm tra ô kho và nhập báo cáo tình trạng + danh sách hư hại (`damageItems`).
     - Hệ thống tính:
       - `totalDamageDeduction = sum(damageItems.chargeAmount)`
       - `newDepositBalance = agreement.DepositBalance - totalDamageDeduction - unpaidOverdueDebt`
       - Cập nhật `agreement.DepositBalance = newDepositBalance`, `moveOut.DamageDeductionAmount = totalDamageDeduction`, `moveOut.NetRefundAmount = newDepositBalance`, `moveOut.Status = "completed"`, `agreement.Status = "completed"`, giải phóng `UnitAllocation -> released`, chuyển `StorageUnit -> cleaning` (hoặc `damaged`/`maintenance` nếu hư hại).
     - **Xử lý 3 kịch bản số dư cọc (`newDepositBalance`):**
       - **Kịch bản 1 (`newDepositBalance > 0`):** Tự động tạo bản ghi `Refund` (`amount = newDepositBalance`, `status = 'approved'`, kèm thông tin ngân hàng từ `moveOut`) để hoàn cọc cho Customer.
       - **Kịch bản 2 (`newDepositBalance == 0`):** Không phát sinh hoàn tiền cũng không nợ thêm.
       - **Kịch bản 3 (`newDepositBalance < 0` — Tiền thiệt hại vượt quá tiền cọc, số dư cọc bị ÂM):**
         1. Giữ `agreement.DepositBalance = newDepositBalance` (ví dụ `-350000`).
         2. Tự động phát hành `Invoice` công nợ bồi thường thiếu hụt (`total_amount = abs(newDepositBalance)`, `status = 'overdue'`, `line_type = 'damage'`).
         3. **Kích hoạt chế độ cảnh báo nợ âm liên tục:**
            - Ghi `Notification` khẩn cấp trong ứng dụng.
            - Gửi ngay **Email cảnh báo công nợ âm** kèm link thanh toán VietQR.
            - Bật cờ `hasNegativeDepositDebt = true` trên tài khoản Customer -> Frontend hiển thị **Banner cảnh báo đỏ cố định trên đầu mọi trang Web** cho đến khi Customer quét VietQR tất toán hóa đơn nợ âm (khi webhook xác nhận thanh toán đủ, `agreement.DepositBalance` được đưa về `0` và tắt cảnh báo)!
   - **Request (`InspectMoveOutRequest`):**
     ```json
     {
       "overallCondition": "damaged",
       "summary": "Móp cửa cuốn và hỏng đèn cảm biến do va chạm khi dọn đồ",
       "setUnitStatus": "maintenance",
       "damageItems": [
         {
           "itemName": "Cửa cuốn hợp kim",
           "condition": "damaged",
           "chargeAmount": 1200000,
           "notes": "Thay thanh nan cửa cuốn bị gãy",
           "photoUrl": "/uploads/media/20261117/door-damage.jpg"
         }
       ]
     }
     ```
   - **Response `200 OK` (`InspectMoveOutResultDto`):**
     ```json
     {
       "moveOutRequestId": 31,
       "agreementId": 88,
       "inspectionId": 92,
       "originalDepositBalance": 900000,
       "totalDamageDeduction": 1200000,
       "finalDepositBalance": -300000,
       "isNegativeDepositDebt": true,
       "negativeDebtAmount": 300000,
       "deficitInvoiceId": 502,
       "deficitInvoiceNo": "INV-20261117-0502",
       "refundId": null,
       "netRefundAmount": 0,
       "unitStatus": "maintenance",
       "agreementStatus": "completed"
     }
     ```

4. **`GET /api/v1/rentals/my-debt-alerts`** — `[Authorize(Roles = "Customer")]`
   - Trả trạng thái cảnh báo nợ âm (`deposit_balance < 0`) và danh sách hợp đồng quá hạn `>= 10` ngày chưa nhập thông tin ngân hàng hoàn cọc để hiển thị **Banner đỏ cố định** và **Popup nhắc nhập STK** trên Web.

---

## 6. KHUNG ROUTE CHUẨN CHO FLOW 4, FLOW 5, FLOW 6, FLOW 7 & ADMIN (SẼ TRIỂN KHAI Ở CỤM TIẾP THEO)

- **Flow 4 (`Manager` — Toàn quyền hệ thống, không giới hạn chi nhánh):**
  - `/api/v1/manager/pricing/price-ranges` (`GET`, `PUT`)
  - `/api/v1/manager/pricing/rates` (`GET`, `PUT`, `POST /bulk-sync`)
  - `/api/v1/manager/pricing/fees` (`GET`, `PUT`)
  - `/api/v1/manager/policies` (`GET`, `PUT`)
  - `/api/v1/manager/promotions` (`GET`, `POST`, `PATCH /{id}/status`)
  - `/api/v1/manager/reports/revenue` (`GET`) & `/api/v1/manager/reports/export` (`GET` CSV/Excel toàn hệ thống hoặc theo từng chi nhánh)
- **Flow 5 (`Manager` toàn hệ thống & `Staff` tại chi nhánh):**
  - `Manager`: `/api/v1/manager/facilities*`, `/api/v1/manager/facilities/{id}/units*`, `/api/v1/manager/facilities/{id}/map-layout`, `/api/v1/manager/facilities/{id}/shifts*`, `/api/v1/manager/facilities/{id}/staff-tasks`
  - `Staff`: `GET /api/v1/staff/daily-queue` (khách nhận kho, trả kho, cần hỗ trợ trong ngày), `PATCH /api/v1/staff/units/{unitId}/physical-status`
- **Flow 6 (`System`, `Customer`, `Staff`, `Manager`):**
  - `Customer`: `POST /api/v1/rentals/{agreementId}/renew`, `POST /api/v1/rentals/{agreementId}/overdue-refund-bank-info` (mở tự động khi quá hạn `>= 10` ngày)
  - `System Background Job`: Chạy hằng ngày để: (1) chuyển `Overdue`, khóa PIN & cộng phí phạt/ngày; (2) nhắc nợ âm (`deposit_balance < 0`); (3) với hợp đồng quá hạn `>= 10` ngày chưa nhập STK ngân hàng, gửi **Email nhắc nhở hằng ngày** kèm link trực tiếp và bật cờ hiển thị **Form nhập thông tin ngân hàng ngay trên Web** khi Customer đăng nhập.
- **Flow 7 (`Customer`, `Staff`, `Manager`):**
  - `Customer`: `/api/v1/support-tickets` (`POST`, `GET /my`, `GET /{id}`, `POST /{id}/messages`, `POST /{id}/confirm-close`)
  - `Staff`: `/api/v1/staff/tickets` (`GET`, `POST /{id}/claim`, `POST /{id}/report`, `POST /{id}/propose-charge`)
  - `Manager`: `/api/v1/manager/tickets*` (xem toàn hệ thống, giao việc, đổi kho, duyệt hoàn phí/đền bù `PUT /api/v1/manager/charge-proposals/{id}/decide`, duyệt hoàn cọc `PUT /api/v1/manager/refund-approvals/{id}/decide`)
- **`Admin` (`[Authorize(Roles = "Admin")]`):**
  - `/api/v1/admin/users` (`GET`, `POST`, `PUT /{id}`, `PATCH /{id}/status`, `PUT /{id}/roles`), `/api/v1/admin/staff-assignments`, `/api/v1/admin/login-history`, `/api/v1/admin/audit-logs`.
