# MỤC: QUY TẮC NGHIỆP VỤ (BUSINESS RULES)

### 1. Nhóm Quy tắc Đặt chỗ & Bàn giao (BR-RSV)

* **BR-RSV-01 (Giữ chỗ tạm thời - Hold Unit):** Khi khách hàng thực hiện thao tác đặt kho trực tuyến, hệ thống chuyển ô kho/loại kho sang trạng thái *Pending Payment* và giữ kho tối đa **15 phút**. Quá 15 phút nếu giao dịch chưa thành công, hệ thống tự động hủy giữ chỗ và trả kho về trạng thái *Available*.
* **BR-RSV-02 (Thời hạn thuê tối thiểu & tối đa):** Thời gian thuê tối thiểu cho một hợp đồng là **01 tháng** và tối đa là **12 tháng** cho mỗi lần ký/gia hạn (khách hàng có thể gia hạn nhiều lần).
* **BR-RSV-03 (Phân ô kho cụ thể - Unit Assignment):** Khách hàng đặt chỗ theo Loại kho (Unit Type) và Diện tích. Mã ô kho cụ thể (*Unit ID*) chỉ được gán tự động hoặc thủ công bởi Facility Manager/Staff trước thời điểm Check-in tối đa **24 giờ** hoặc ngay tại thời điểm làm thủ tục Check-in.
* **BR-RSV-04 (Xác minh thông tin Check-in):** Khách hàng chỉ được bàn giao kho và kích hoạt mã PIN/Thẻ từ khi Mã đặt chỗ (*Reservation Code*) hợp lệ, hợp đồng đã được thanh toán đủ (Tiền cọc + Tiền thuê kỳ đầu) và thông tin định danh (CCCD/CMND/Passport) trùng khớp với dữ liệu đăng ký.

---

### 2. Nhóm Quy tắc Tài chính, Giá & Tiền cọc (BR-FIN)

* **BR-FIN-01 (Tiền đặt cọc - Security Deposit):** Mức cọc mặc định bằng **100% giá thuê của 01 tháng** tương ứng với loại kho đó. Tiền cọc không được tính vào tiền thuê hàng tháng và được giữ lại đến khi chấm dứt hợp đồng.
* **BR-FIN-02 (Công thức hoàn trả tiền cọc):** Khi khách hàng hoàn trả kho (Check-out), tiền cọc hoàn trả được tính toán tự động theo công thức:
  $$Tiền\,hoàn\,trả = Tiền\,cọc - (Phí\,sửa\,chữa\,hư\,hỏng + Phí\,vệ\,sinh + Phí\,quá\,hạn\,chưa\,thanh\,toán)$$
* **BR-FIN-03 (Chính sách Phí dịch vụ bổ sung):** Các dịch vụ phát sinh (làm lại thẻ từ, thay ổ khóa do mất chìa, phí dọn dẹp vệ sinh đặc biệt) được áp dụng theo Khung phí niêm yết do Business Operations Manager cấu hình trên hệ thống.
* **BR-FIN-04 (Áp dụng Ưu đãi & Voucher):** Mỗi đơn đặt chỗ/hóa đơn gia hạn chỉ được áp dụng tối đa **01 Mã giảm giá (Promotional Code)**. Khuyến mãi chỉ có hiệu lực khi đơn hàng đáp ứng đủ điều kiện về thời hạn thuê tối thiểu và thời gian áp dụng của chương trình.

---

### 3. Nhóm Quy tắc Gia hạn, Thanh toán chậm & Quá hạn (BR-REN)

* **BR-REN-01 (Thông báo nhắc gia hạn tự động):** Hệ thống tự động khởi tạo hóa đơn kỳ tiếp theo và gửi thông báo nhắc thanh toán (qua App Push/Email/SMS) trước khi hợp đồng hiện tại hết hạn vào các mốc: **07 ngày**, **03 ngày** và **01 ngày**.
* **BR-REN-02 (Tự động tính phí phạt quá hạn):** Nếu hợp đồng trễ hạn thanh toán từ ngày $N+1$ (ngày đầu tiên sau khi hết hạn), hệ thống tự động áp dụng phí phạt quá hạn theo ngày. Mức phí phạt bằng **150% giá thuê niêm yết tính theo ngày** của ô kho đó.
* **BR-REN-03 (Tạm khóa quyền truy cập - Overdue Access Block):** Nếu hợp đồng trễ hạn thanh toán quá **01 ngày** (từ ngày $N+1$), hệ thống tự động vô hiệu hóa mã PIN / Thẻ từ / Quyền truy cập ứng dụng của khách hàng đối với ô kho đó cho đến khi phát sinh giao dịch thanh toán bổ sung thành công.
* **BR-REN-04 (Xử lý vi phạm quá hạn kéo dài - Defaulted Contract):** Nếu hợp đồng quá hạn quá **30 ngày** và khách hàng không hoàn tất thanh toán hoặc không phản hồi liên lạc, hợp đồng chuyển sang trạng thái *Defaulted*. Facility Manager có quyền phối hợp niêm phong, kiểm kê và xử lý tài sản bên trong kho theo Điều khoản hợp đồng đã ký kết.

---

### 4. Nhóm Quy tắc Phân quyền & Quản lý Vận hành (BR-OPS)

* **BR-OPS-01 (Phạm vi truy cập dữ liệu theo Cơ sở - Data Scope):**
  * **Facility Staff & Facility Manager:** Chỉ có quyền xem, chỉnh sửa dữ liệu, duyệt thủ tục check-in/check-out và nhận ticket hỗ trợ thuộc về **Điểm kho (Facility)** mà mình được phân công.
  * **Business Operations Manager & System Admin:** Có quyền truy cập, cấu hình và xem báo cáo trên **Toàn bộ hệ thống (System-wide)**.
* **BR-OPS-02 (Ràng buộc Trạng thái Ô kho - Unit Status):**
  * Ô kho đang ở trạng thái *In-Use* (Đang sử dụng) hoặc *Under Maintenance* (Bảo trì) không được phép hiển thị trên danh sách kho trống để khách hàng đặt chỗ.
  * Ô kho chỉ chuyển sang trạng thái *Available* (Sẵn sàng cho thuê) sau khi Facility Staff hoàn tất khâu kiểm tra vệ sinh/hạ tầng và xác nhận "Đạt yêu cầu" trên hệ thống.
* **BR-OPS-03 (Phân công công việc & Xử lý Ticket):** Ticket hỗ trợ sự cố từ khách hàng tại một cơ sở sẽ tự động điều phối đến danh sách công việc (Task List) của Facility Staff đang trong ca trực tại cơ sở đó. Thời gian phản hồi Ticket ban đầu (SLA) không quá **30 phút** trong giờ vận hành.
