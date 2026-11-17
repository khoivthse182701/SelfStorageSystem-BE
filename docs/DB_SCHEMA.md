# DATABASE SCHEMA SPECIFICATION (`DB_SCHEMA.md`)

> **Project:** Self-Storage Management System (`SelfStoragePRN222`)  
> **Database Engine:** SQL Server 2019+ (`[core]` schema, EF Core 8 Code-First Migrations)  
> **Status:** **DESIGN FOR APPROVAL (FLOW 1, FLOW 2, FLOW 3 + CORE 7-FLOW SCHEMA)**  
> **Roles (4):** `Customer` (`storage_customer`), `Staff` (`facility_staff`), `Manager` (`facility_manager`), `Admin` (`system_administrator`)

---

## 1. Rà Soát Hiện Trạng Database Trên Nhánh `main` & Các Điều Chỉnh Cốt Lõi

| Bảng / Nhóm Bảng | Hiện trạng trên `main` | Vấn đề đối chiếu với đặc tả 7 Flow mới | Thiết kế điều chỉnh trong `DB_SCHEMA.md` |
| :--- | :--- | :--- | :--- |
| **`users` & `customer_profiles`** | `users.phone_number` cho phép `NULL`; `customer_profiles.identity_number` cho phép `NULL`; chưa có cột đếm số lần sửa Profile theo tháng. | Đăng ký bắt buộc `FullName`, `PhoneNumber`, `IdentityNumber (CCCD)`, `Email`, `Password`. Trang Profile khóa `CCCD` và giới hạn sửa `FullName`/`PhoneNumber` tối đa **2 lần/tháng** (`YYYY-MM`). | Bắt buộc `phone_number` & `identity_number` khi Customer đăng ký; thêm `profile_edit_month char(7) NULL` (`'YYYY-MM'`) và `profile_edit_count int NOT NULL DEFAULT 0 CHECK (profile_edit_count BETWEEN 0 AND 2)` vào `[core].[customer_profiles]`. |
| **`roles` & `staff_facility_assignments`** | 4 roles (`storage_customer`, `facility_staff`, `facility_manager`, `system_administrator`) + alias cũ `business_operations_manager`. `Manager` đang bị chặn bởi `ValidateFacilityAccessAsync` theo từng chi nhánh. | `Manager` là quyền vận hành cao nhất **toàn hệ thống** (không giới hạn theo chi nhánh). `Staff` làm việc theo chi nhánh được phân công. `Admin` quản lý User, Role, `login_history`, `audit_logs`. | Giữ chuẩn 4 Roles (`Customer`, `Staff`, `Manager`, `Admin`), xóa `business_operations_manager`. Chỉ kiểm tra `staff_facility_assignments` bắt buộc đối với `Staff`; gỡ bỏ hoàn toàn rào cản chi nhánh đối với `Manager`. |
| **`reservations` & `unit_allocations` (Flow 1 & 2)** | Đã có giữ chỗ 15 phút (`hold_until`), `unit_allocations` lưu khoảng ngày `[allocation_start_date, allocation_end_date)`. | Cần đảm bảo index chống đặt trùng ô kho và hỗ trợ Staff lấy danh sách **ô kho dự phòng cùng loại & kích thước** ngay trên màn hình Check-in. | Giữ composite index `unit_allocations_availability_idx (storage_unit_id, status, allocation_start_date, allocation_end_date)` + index `storage_units_catalog_idx (facility_id, unit_type_id, physical_status) WHERE is_listed = 1`. |
| **`identity_verifications` & `handover_records` (Flow 2)** | Bảng `identity_verifications` tồn tại trong DB nhưng luồng Check-in cũ chưa đối chiếu bắt buộc số CCCD khách nhập tại quầy với `customer_profiles.identity_number`. | Flow 2 yêu cầu **Xác thực kép**: Quét mã QR Check-in **VÀ** nhập trực tiếp số `CCCD` để hệ thống đối chiếu khớp với hồ sơ đăng ký trước khi kích hoạt `Active` & mã PIN. | Khi Check-in, đối chiếu `input.IdentityNumber == customer_profiles.identity_number`, lưu bản ghi vào `identity_verifications` (`verification_method = 'government_id'`, `document_fingerprint = identityNumber`, `result = 'verified'`). |
| **`access_credentials` & `authorized_access_members` (Flow 3)** | `access_credentials.display_hint` trong một số seed lưu chuỗi che `'****10'` thay vì mã hóa AES 2 chiều, làm `AccessControl.jsx` nhận `keypadPin = null` và ẩn mất khung hiển thị/đổi PIN. `authorized_access_members` từng bị chặn `410 Gone`. | Flow 3 yêu cầu hiển thị **Mã PIN hiện tại** + **Đổi mã PIN** (cùng 2 nút quét QR) khi hợp đồng `Active`, và cho phép thêm/xóa **Người ủy quyền**. | Luôn lưu `display_hint = PinProtectionHelper.Encrypt(pin)` khi tạo/đổi PIN (tự động đồng bộ cho dữ liệu seed) và mở đầy đủ CRUD `[core].[authorized_access_members]`. |
| **`unit_transfer_requests` (Flow 3)** | Thiết kế cũ bắt buộc chờ `Manager` duyệt (`status = 'pending' -> 'approved'`). | Flow 3 mới yêu cầu **Nâng/Hạ hạng kho tự động không cần Manager duyệt**: Nâng hạng quét VietQR xong tự động đổi sang ô kho mới; Hạ hạng (`0đ`, không hoàn chênh lệch) tự động đổi sang ô kho mới ngay lập tức. | Bổ sung `transfer_type nvarchar(32) NOT NULL DEFAULT 'upgrade' CHECK (transfer_type IN ('upgrade', 'downgrade', 'same_tier', 'relocation'))` và `target_invoice_id bigint NULL REFERENCES [core].[invoices](id)`. Hạ hạng chuyển thẳng `status = 'completed'`; Nâng hạng tạo `Invoice` và chuyển thẳng `status = 'completed'` ngay trong SePay webhook. |
| **`move_out_requests` & `rental_agreements` (Flow 3 & Flow 6)** | `move_out_requests` chưa có cột lưu tài khoản ngân hàng hoàn cọc (`FE` phải lưu tạm vào `localStorage`). `rental_agreements.deposit_balance` đang bị ràng buộc `CHECK (deposit_balance >= 0)`. | Khi trả kho sớm (Flow 3) hoặc quá hạn sau 10 ngày (Flow 6), khách nhập **Thông tin tài khoản ngân hàng** trên Web. Khi nghiệm thu trả kho, phí thiệt hại trừ vào tiền cọc; nếu **tiền cọc bị âm (`< 0`)**, kích hoạt cảnh báo nợ âm liên tục (Banner đỏ + Notification + Email định kỳ). | 1. Thêm `bank_name nvarchar(128) NULL`, `bank_account_no nvarchar(64) NULL`, `bank_account_holder nvarchar(128) NULL`, `bank_submitted_at datetimeoffset(7) NULL`, `damage_deduction_amount numeric(14,2) NOT NULL DEFAULT 0`, `net_refund_amount numeric(14,2) NULL` vào `[core].[move_out_requests]`.<br/>2. Cho phép `rental_agreements.deposit_balance < 0` (bỏ điều kiện `deposit_balance >= 0` trong `CK_rental_agreements_amounts`), thêm `negative_debt_notified_at datetimeoffset(7) NULL` và `overdue_bank_reminder_sent_at datetimeoffset(7) NULL` vào `[core].[rental_agreements]`. |

---

## 2. Phân Quyền 4 Roles Chính Thức

| Role (`UserRoleCode`) | Giá trị `roles.code` trong DB | JWT Role Claim | Phạm vi dữ liệu & Quyền hạn cốt lõi |
| :--- | :--- | :--- | :--- |
| **`Customer`** | `storage_customer` | `Customer` | Đăng ký (kèm CCCD + SĐT), quản lý Profile (sửa Tên/SĐT tối đa 2 lần/tháng, khóa CCCD), đặt kho, thanh toán VietQR, nhận QR Check-in, xem/đổi PIN (khi `Active`), thêm/xóa người ủy quyền, tự động nâng/hạ hạng kho, yêu cầu trả kho sớm kèm STK ngân hàng, gia hạn kho quá hạn, nhập STK nhận cọc dư sau 10 ngày quá hạn, gửi ticket hỗ trợ. |
| **`Staff`** | `facility_staff` | `Staff` | Giới hạn tại chi nhánh được phân công (`staff_facility_assignments`): xem danh sách khách trong ngày (check-in, trả kho, hỗ trợ), xác thực kép Check-in (QR + CCCD), đổi ô kho dự phòng cùng loại tại quầy, nghiệm thu trả kho & lập báo cáo hư hại khấu trừ cọc, cập nhật trạng thái vệ sinh/bảo trì ô kho, xử lý ticket tại chỗ. |
| **`Manager`** | `facility_manager` | `Manager` | **Toàn quyền vận hành toàn hệ thống (Không giới hạn theo chi nhánh):** Quản lý tất cả chi nhánh, sơ đồ mặt bằng, ô kho (`Maintenance`), ca trực & giao việc cho `Staff`; cấu hình khung giá (`price_ranges`), bảng giá (`facility_rates`), quy tắc phí/phạt (`fee_rules`), chính sách (`policy_versions`), khuyến mãi (`promotions`); duyệt đền bù/hoàn phí ticket; xem & xuất báo cáo doanh thu/lấp đầy/công nợ từng chi nhánh hoặc toàn hệ thống. |
| **`Admin`** | `system_administrator` | `Admin` | Quản lý tài khoản người dùng (`users`), gán Role (`Customer`, `Staff`, `Manager`, `Admin`), phân công chi nhánh cho `Staff` (`staff_facility_assignments`), giám sát `login_history` và `audit_logs`. |

---

## 3. Đặc Tả Chi Tiết Các Bảng Cốt Lõi (Flow 1, Flow 2, Flow 3 & Liên Thông Flow 4–7)

### 3.1. `[core].[users]` & `[core].[customer_profiles]` (Auth & Profile)
```sql
CREATE TABLE [core].[users] (
    id bigint IDENTITY(1,1) PRIMARY KEY,
    email nvarchar(255) NOT NULL UNIQUE,
    phone_number nvarchar(32) NULL,
    password_hash nvarchar(255) NOT NULL,
    status nvarchar(255) NOT NULL DEFAULT 'active'
        CONSTRAINT CK_users_status CHECK (status IN ('active', 'locked', 'disabled')),
    last_login_at datetimeoffset(7) NULL,
    created_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE [core].[customer_profiles] (
    user_id bigint PRIMARY KEY REFERENCES [core].[users](id) ON DELETE NO ACTION,
    full_name nvarchar(255) NOT NULL,
    identity_number nvarchar(32) NULL, -- Bắt buộc NOT NULL ở tầng API khi Customer đăng ký; khóa không cho Customer tự sửa
    date_of_birth date NULL,
    address nvarchar(255) NULL,
    emergency_contact_name nvarchar(255) NULL,
    emergency_contact_phone nvarchar(32) NULL,
    profile_edit_month char(7) NULL, -- Định dạng 'YYYY-MM' (VD: '2026-11')
    profile_edit_count int NOT NULL DEFAULT 0
        CONSTRAINT CK_customer_profiles_edit_count CHECK (profile_edit_count BETWEEN 0 AND 2),
    created_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX customer_profiles_identity_number_uidx
    ON [core].[customer_profiles](identity_number)
    WHERE identity_number IS NOT NULL;
```

### 3.2. `[core].[reservations]`, `[core].[unit_allocations]` & `[core].[identity_verifications]` (Flow 1 & Flow 2)
- **`[core].[reservations]`**:
  - `status`: `'pending' | 'awaiting_deposit' | 'confirmed' | 'checked_in' | 'converted' | 'completed' | 'no_show' | 'cancelled' | 'expired'`
  - `hold_until`: `created_at + 15 minutes`
  - Snapshot giá: `monthly_rate_snapshot`, `deposit_snapshot`, `booking_fee_snapshot`, `discount_snapshot`, `quoted_total`.
- **`[core].[unit_allocations]`**:
  - `allocation_kind`: `'reservation_hold' | 'rental' | 'transfer'`
  - `status`: `'active' | 'consumed' | 'released' | 'expired'`
  - Index chống trùng lịch: `unit_allocations_availability_idx (storage_unit_id, status, allocation_start_date, allocation_end_date) WHERE status = 'active'`.
- **`[core].[identity_verifications]`**:
  - Lưu bằng chứng xác thực kép tại quầy ở Flow 2: `reservation_id`, `verified_by` (`Staff`), `verification_method = 'government_id'`, `document_fingerprint` (số CCCD đã đối chiếu khớp `customer_profiles.identity_number`), `result = 'verified'`, `verified_at`.

### 3.3. `[core].[rental_agreements]` (Flow 2, Flow 3, Flow 6)
```sql
CREATE TABLE [core].[rental_agreements] (
    id bigint IDENTITY(1,1) PRIMARY KEY,
    agreement_no nvarchar(255) NOT NULL UNIQUE,
    reservation_id bigint NOT NULL UNIQUE REFERENCES [core].[reservations](id),
    customer_id bigint NOT NULL REFERENCES [core].[customer_profiles](user_id),
    facility_id bigint NOT NULL REFERENCES [core].[facilities](id),
    policy_version_id bigint NOT NULL REFERENCES [core].[policy_versions](id),
    start_date date NOT NULL,
    end_date date NOT NULL,
    monthly_rate_snapshot numeric(14, 2) NOT NULL,
    deposit_snapshot numeric(14, 2) NOT NULL,
    deposit_balance numeric(14, 2) NOT NULL, -- Cho phép < 0 khi chi phí hư hại/quá hạn vượt quá tiền cọc!
    status nvarchar(255) NOT NULL DEFAULT 'draft'
        CONSTRAINT CK_rental_agreements_status CHECK (status IN (
            'draft', 'scheduled', 'active', 'expiring_soon', 'extended', 'overdue',
            'defaulted', 'move_out_scheduled', 'checkout_pending', 'expired',
            'terminated', 'completed', 'closed', 'cancelled'
        )),
    signed_at datetimeoffset(7) NULL,
    checked_in_at datetimeoffset(7) NULL,
    checked_out_at datetimeoffset(7) NULL,
    actual_end_date date NULL,
    negative_debt_notified_at datetimeoffset(7) NULL,      -- Mốc gửi email/thông báo nhắc nợ cọc âm (< 0) gần nhất
    overdue_bank_reminder_sent_at datetimeoffset(7) NULL,  -- Mốc gửi email nhắc nhập STK hoàn cọc sau 10 ngày quá hạn
    created_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_rental_agreements_dates CHECK (end_date > start_date),
    CONSTRAINT CK_rental_agreements_amounts CHECK (monthly_rate_snapshot >= 0 AND deposit_snapshot >= 0),
    CONSTRAINT CK_rental_agreements_checkout CHECK (checked_out_at IS NULL OR (checked_in_at IS NOT NULL AND checked_out_at >= checked_in_at))
);
```

### 3.4. `[core].[access_credentials]` & `[core].[authorized_access_members]` (Flow 3)
- **`[core].[access_credentials]`**:
  - `secret_digest`: BCrypt hash của mã PIN 6 số (dùng để verify khi mở kho).
  - `display_hint`: AES-GCM encrypted PIN qua `PinProtectionHelper.Encrypt(pin)` (dùng để giải mã trả về `keypadPin` cho chính chủ hợp đồng trên trang `AccessControl` sau khi đã Check-in `Active`).
  - `status`: `'pending' | 'active' | 'suspended' | 'revoked' | 'expired'`.
- **`[core].[authorized_access_members]`**:
  - Lưu danh sách người được Customer ủy quyền ra/vào kho (`agreement_id`, `full_name`, `identity_fingerprint` (CCCD/SĐT), `relationship_to_customer`, `valid_from`, `valid_to`, `status` `'active' | 'revoked'`).

### 3.5. `[core].[unit_transfer_requests]` (Flow 3 — Nâng / Hạ Hạng Tự Động)
```sql
CREATE TABLE [core].[unit_transfer_requests] (
    id bigint IDENTITY(1,1) PRIMARY KEY,
    agreement_id bigint NOT NULL REFERENCES [core].[rental_agreements](id),
    requested_unit_type_id bigint NOT NULL REFERENCES [core].[unit_types](id),
    requested_facility_id bigint NOT NULL REFERENCES [core].[facilities](id),
    from_unit_id bigint NOT NULL REFERENCES [core].[storage_units](id),
    to_unit_id bigint NULL REFERENCES [core].[storage_units](id),
    transfer_type nvarchar(32) NOT NULL DEFAULT 'upgrade'
        CONSTRAINT CK_unit_transfer_requests_type CHECK (transfer_type IN ('upgrade', 'downgrade', 'same_tier', 'relocation')),
    target_invoice_id bigint NULL REFERENCES [core].[invoices](id),
    requested_effective_date date NOT NULL,
    move_out_date date NOT NULL,
    move_in_date date NOT NULL,
    reschedule_count int NOT NULL DEFAULT 0,
    diff_rent_amount numeric(14, 2) NOT NULL DEFAULT 0,
    diff_deposit_amount numeric(14, 2) NOT NULL DEFAULT 0,
    cleaning_fee numeric(14, 2) NOT NULL DEFAULT 0,
    penalty_amount numeric(14, 2) NOT NULL DEFAULT 0,
    credit_amount numeric(14, 2) NOT NULL DEFAULT 0,
    total_paid_amount numeric(14, 2) NOT NULL DEFAULT 0,
    reason nvarchar(255) NOT NULL,
    status nvarchar(255) NOT NULL
        CONSTRAINT CK_unit_transfer_requests_status CHECK (status IN (
            'pending_payment', 'paid', 'moved_out_in_transit', 'cancelled_overdue_move_out',
            'pending', 'approved', 'scheduled', 'completed', 'rejected', 'cancelled'
        )),
    requested_by bigint NOT NULL REFERENCES [core].[users](id),
    reviewed_by bigint NULL REFERENCES [core].[employee_profiles](user_id),
    reviewed_at datetimeoffset(7) NULL,
    completed_at datetimeoffset(7) NULL,
    created_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
```

### 3.6. `[core].[move_out_requests]` (Flow 3 & Flow 6 — Trả Kho Sớm, Khấu Trừ Cọc & Thông Tin Ngân Hàng)
```sql
CREATE TABLE [core].[move_out_requests] (
    id bigint IDENTITY(1,1) PRIMARY KEY,
    agreement_id bigint NOT NULL UNIQUE REFERENCES [core].[rental_agreements](id),
    requested_by bigint NOT NULL REFERENCES [core].[users](id),
    requested_move_out_date date NOT NULL,
    appointment_id bigint NULL REFERENCES [core].[appointments](id),
    bank_name nvarchar(128) NULL,
    bank_account_no nvarchar(64) NULL,
    bank_account_holder nvarchar(128) NULL,
    bank_submitted_at datetimeoffset(7) NULL,
    damage_deduction_amount numeric(14, 2) NOT NULL DEFAULT 0,
    overdue_deduction_amount numeric(14, 2) NOT NULL DEFAULT 0,
    net_refund_amount numeric(14, 2) NULL, -- Có thể < 0 nếu tiền phạt/hư hại vượt quá tiền cọc
    status nvarchar(255) NOT NULL DEFAULT 'requested'
        CONSTRAINT CK_move_out_requests_status CHECK (status IN (
            'requested', 'scheduled', 'in_progress', 'inspection_pending',
            'settlement_pending', 'completed', 'cancelled'
        )),
    reason nvarchar(255) NULL,
    finalized_at datetimeoffset(7) NULL,
    created_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
```
