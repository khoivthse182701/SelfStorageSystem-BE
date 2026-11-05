-- Seed Fee Policies (2,000đ - 10,000đ) for Admin/Manager Pricing
USE [SelfStoragePRN222];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DELETE FROM [core].[fee_rules] WHERE code IN ('FEE_LATE_2K', 'FEE_CLEAN_5K', 'FEE_LOCK_8K', 'FEE_TRANSFER_10K', 'FEE_KEY_4K', 'FEE_MAINT_6K', 'FEE_BOOKING_2K', 'FEE_OVERSTAY_7K');
GO

INSERT INTO [core].[fee_rules]
(facility_id, code, fee_type, calculation_method, amount, rate_percent, grace_days, conditions, valid_from, valid_to, is_active, created_by, created_at)
VALUES
(NULL, 'FEE_LATE_2K', 'late_fee', 'flat', 2000.00, NULL, 0, N'{"description":"Phí phạt trễ hạn thanh toán hợp đồng (2.000 VNĐ)"}', '2026-01-01', NULL, 1, 12, SYSDATETIMEOFFSET()),
(NULL, 'FEE_CLEAN_5K', 'damage_fee', 'flat', 5000.00, NULL, 0, N'{"description":"Phí vệ sinh dọn dẹp kho khi trả/chuyển kho (5.000 VNĐ)"}', '2026-01-01', NULL, 1, 12, SYSDATETIMEOFFSET()),
(NULL, 'FEE_LOCK_8K', 'damage_fee', 'flat', 8000.00, NULL, 0, N'{"description":"Phí xử lý cắt khóa / bồi thường hỏng khóa (8.000 VNĐ)"}', '2026-01-01', NULL, 1, 12, SYSDATETIMEOFFSET()),
(NULL, 'FEE_TRANSFER_10K', 'other', 'flat', 10000.00, NULL, 0, N'{"description":"Phí dịch vụ chuyển cơ sở lưu trữ (10.000 VNĐ)"}', '2026-01-01', NULL, 1, 12, SYSDATETIMEOFFSET()),
(NULL, 'FEE_KEY_4K', 'lost_key_fee', 'flat', 4000.00, NULL, 0, N'{"description":"Phí cấp lại chìa khóa / thẻ từ (4.000 VNĐ)"}', '2026-01-01', NULL, 1, 12, SYSDATETIMEOFFSET()),
(NULL, 'FEE_MAINT_6K', 'maintenance_fee', 'flat', 6000.00, NULL, 0, N'{"description":"Phí bảo trì định kỳ cơ sở hạ tầng (6.000 VNĐ)"}', '2026-01-01', NULL, 1, 12, SYSDATETIMEOFFSET()),
(NULL, 'FEE_BOOKING_2K', 'other', 'flat', 2000.00, NULL, 0, N'{"description":"Phí giữ chỗ & khởi tạo hợp đồng trực tuyến (2.000 VNĐ)"}', '2026-01-01', NULL, 1, 12, SYSDATETIMEOFFSET()),
(NULL, 'FEE_OVERSTAY_7K', 'other', 'per_day', 7000.00, NULL, 0, N'{"description":"Phụ phí lưu kho quá hạn hợp đồng chưa thanh lý (7.000 VNĐ/ngày)"}', '2026-01-01', NULL, 1, 12, SYSDATETIMEOFFSET());
GO

SELECT id, code, fee_type, calculation_method, amount, is_active FROM [core].[fee_rules];
GO
