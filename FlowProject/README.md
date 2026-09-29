# HỆ THỐNG QUẢN LÝ VÀ CHO THUÊ KHO LƯU TRỮ TỰ PHỤC VỤ
## (SELF-STORAGE FACILITY RENTAL AND MANAGEMENT SYSTEM)

Tài liệu phân tích chi tiết quy trình nghiệp vụ (Main Flows), Ma trận phân quyền (RBAC Matrix), Chi tiết chức năng theo Actor, cùng Hướng dẫn Kiến trúc kỹ thuật và Chạy local cho hệ thống Backend (**SelfStorageSystem**).

---

## 1. MA TRẬN PHÂN QUYỀN (RBAC MATRIX)

| Actor \ Main Flow | Flow 1: Reservation | Flow 2: Check-in & Handover | Flow 3: Unit Management | Flow 4: Business & Fee | Flow 5: Facility & Staff | Flow 6: Renewal & Overdue | Flow 7: Support & Issue | Admin & Security |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Storage Customer** | **X** | **X** | **X** | | | **X** | **X** | |
| **Facility Staff** | | **X** | | | **X** | **X** | **X** | |
| **Facility Manager** | | **X** | | | **X** | **X** | **X** | |
| **Business Operations Manager** | | | | **X** | | | | |
| **System Administrator** | | | | | | | | **X** |

---

## 2. CHI TIẾT CHỨC NĂNG THEO ACTOR

### 1. Storage Customer (Khách hàng thuê kho)
* **Xem thông tin kho:** Tra cứu danh sách điểm kho, loại kho (mát/thường/ngoài trời), kích thước (diện tích/thể tích), giá thuê niêm yết và số lượng ô kho còn trống theo thời gian thực.
* **Đặt kho trực tuyến:** Chọn cơ sở, loại kho, ngày bắt đầu thuê và thời hạn thuê để khởi tạo yêu cầu đặt chỗ.
* **Thanh toán:** Thực hiện thanh toán trực tuyến cho các khoản: tiền đặt cọc, tiền thuê kho định kỳ, phí gia hạn, hoặc các chi phí phát sinh (phí phạt, phí dịch vụ).
* **Check-in & Nhận kho:** Thực hiện thủ tục nhận kho theo lịch hẹn, nhận mã PIN/thẻ từ/chìa khóa và biên bản bàn giao.
* **Quản lý kho đang thuê:** Theo dõi danh sách các ô kho đang vận hành, thời hạn hợp đồng, lịch sử thanh toán, quản lý mã truy cập.
* **Gửi yêu cầu hỗ trợ (Support Ticket):** Tạo yêu cầu hỗ trợ khi gặp sự cố liên quan đến: ô kho, ổ khóa, mã truy cập, sự cố thanh toán hoặc tài sản lưu trữ.

### 2. Facility Staff (Nhân viên điểm kho)
* **Xác minh đơn đặt chỗ:** Kiểm tra thông tin đặt chỗ của khách hàng khi khách đến điểm kho theo lịch hẹn.
* **Hỗ trợ Check-in & Bàn giao:** Hướng dẫn vị trí kho, hoàn tất thủ tục check-in, bàn giao ổ khóa, thẻ từ hoặc kích hoạt mã PIN truy cập.
* **Cập nhật trạng thái ô kho:** Cập nhật trạng thái kho theo thời gian thực (Trống/Available, Đang sử dụng/In-Use, Chờ trả/Pending Return, Cần bảo trì/Maintenance).
* **Kiểm tra và xác nhận trả kho:** Đánh giá tình trạng thực tế của ô kho khi khách hoàn trả (vệ sinh, hư hỏng, thiết bị khóa).
* **Xử lý sự cố tại chỗ:** Tiếp nhận và giải quyết trực tiếp các vấn đề như mất chìa khóa, lỗi mã PIN, hỏng hóc ô kho hoặc hỗ trợ khách hàng.
* **Theo dõi danh sách công việc hàng ngày:** Quản lý và thực hiện danh sách công việc theo ngày (lịch khách đến nhận kho, lịch trả kho, lịch bảo trì, ticket hỗ trợ).

### 3. Facility Manager (Quản lý điểm kho)
* **Quản lý hạ tầng kho tại cơ sở:** Khai báo và quản lý chi tiết từng ô kho tại chi nhánh (loại kho, kích thước, vị trí sơ đồ, giá thuê, trạng thái vận hành).
* **Gán ô kho (Unit Assignment):** Phân bổ ô kho cụ thể phù hợp cho khách hàng dựa trên loại kho, thời gian thuê và tình trạng kho trống.
* **Giám sát khách hàng & Hợp đồng:** Theo dõi danh sách khách hàng đang thuê, hợp đồng hiện hữu, thời hạn hợp đồng và tình trạng công nợ/thanh toán.
* **Quản lý quy trình vận hành:** Giám sát toàn bộ quy trình bàn giao kho, trả kho, gia hạn hợp đồng và xử lý các trường hợp quá hạn tại cơ sở.
* **Phân công công việc cho nhân viên:** Gán ca trực, phân công công việc cụ thể cho Facility Staff (hỗ trợ bàn giao, kiểm tra kho, xử lý sự cố).
* **Xem báo cáo cơ sở:** Theo dõi các báo cáo vận hành tại điểm kho phụ trách: tỷ lệ lấp đầy (occupancy rate), danh sách kho trống, doanh thu cơ sở, danh sách hợp đồng quá hạn.

### 4. Business Operations Manager (Quản lý vận hành toàn hệ thống)
* **Quản lý chuỗi điểm kho:** Quản lý danh mục tất cả các cơ sở kho trong toàn bộ hệ thống (thêm mới, cập nhật thông tin chi nhánh).
* **Ban hành chính sách nghiệp vụ:** Thiết lập các quy định chung áp dụng toàn hệ thống: chính sách tiền cọc, chính sách hủy/đổi kho, quy định gia hạn, quy trình trả kho và xử lý vi phạm quá hạn.
* **Quản lý bảng giá & Phí:** Cấu hình khung giá thuê, phí dịch vụ bổ sung, phí phạt quá hạn, phí bồi thường và các chương trình ưu đãi/giảm giá.
* **Giám sát hiệu quả kinh doanh:** Theo dõi các chỉ số KPI vận hành, doanh thu, tỷ lệ sử dụng kho của từng cơ sở và toàn hệ thống.
* **Báo cáo & Trích xuất dữ liệu:** Xem và xuất các báo cáo tổng hợp cấp hệ thống theo chi nhánh, loại kho, doanh thu và trạng thái hợp đồng.

### 5. System Administrator (Quản trị hệ thống)
* **Quản lý tài khoản người dùng:** Khởi tạo, cập nhật, khóa hoặc kích hoạt tài khoản của người dùng trên toàn hệ thống.
* **Phân quyền người dùng (RBAC):** Gán vai trò (Role) cho người dùng: Storage Customer, Facility Staff, Facility Manager, Business Operations Manager, System Admin.
* **Cấu hình phạm vi truy cập dữ liệu (Data Access Scope):** Phân quyền truy cập dữ liệu theo chi nhánh phụ trách cho Facility Staff và Facility Manager.
* **Ghi log & Trích xuất nhật ký (Audit Logs):** Theo dõi lịch sử đăng nhập, lịch sử thay đổi dữ liệu và nhật ký hoạt động của người dùng trên hệ thống.

---

## 3. PHÂN TÍCH CHI TIẾT 7 MẢNG QUY TRÌNH NGHIỆP VỤ (MAIN FLOWS)

### Flow 1: Storage Unit Reservation Flow (Quy trình Đặt chỗ Kho)
* **Mục tiêu:** Cho phép khách hàng tìm kiếm, chọn ô kho phù hợp và thực hiện đặt chỗ trực tuyến.
* **Các bước thực hiện:**
  1. Khách hàng truy cập ứng dụng/website, tìm kiếm kho theo vị trí, loại kho, kích thước mong muốn và thời gian thuê.
  2. Hệ thống hiển thị danh sách các điểm kho và ô kho trống phù hợp kèm thông tin chi tiết (kích thước, giá thuê, phí cọc, chính sách).
  3. Khách hàng chọn thông số (ngày bắt đầu, thời gian thuê) và nhấn "Đặt chỗ".
  4. Hệ thống thực hiện tóm tắt đơn hàng, giữ kho tạm thời (Hold Unit - ví dụ: 15-30 phút) và tính toán số tiền cần thanh toán (Tiền cọc + Tiền thuê kỳ đầu).
  5. Khách hàng tiến hành thanh toán trực tuyến qua cổng thanh toán tích hợp.
  6. Sau khi thanh toán thành công, hệ thống ghi nhận trạng thái đơn đặt chỗ, tạo Mã Đặt Chỗ (Reservation Code) và gửi xác nhận kèm lịch hẹn check-in cho khách hàng qua Email/SMS/App.

### Flow 2: Storage Check-in and Handover Flow (Quy trình Check-in & Bàn giao Kho)
* **Mục tiêu:** Xác minh thông tin đặt chỗ và hoàn tất bàn giao ô kho thực tế cho khách hàng.
* **Các bước thực hiện:**
  1. Khách hàng đến điểm kho theo lịch hẹn và cung cấp Mã Đặt Chỗ/Thông tin cá nhân cho Facility Staff.
  2. Facility Staff tra cứu thông tin đặt chỗ trên danh sách công việc trong ngày.
  3. Facility Manager hoặc hệ thống gán một ô kho cụ thể (Unit Number) cho khách hàng dựa trên loại kho đã đặt (nếu chưa gán ở Flow 1).
  4. Facility Staff hướng dẫn khách hàng đến vị trí ô kho, thực hiện bàn giao ổ khóa, thẻ từ hoặc hướng dẫn/kích hoạt mã PIN truy cập.
  5. Facility Staff và Khách hàng đồng kiểm tra tình trạng ô kho (vệ sinh, đèn, cửa, khóa).
  6. Facility Staff xác nhận hoàn tất bàn giao trên hệ thống. Hợp đồng chuyển sang trạng thái "Active", ô kho chuyển trạng thái sang "In-Use".

### Flow 3: Rented Storage Unit Management Flow (Quy trình Quản lý Kho Đang Thuê)
* **Mục tiêu:** Quản lý hoạt động sử dụng kho hàng ngày của khách hàng và công tác bảo trì của điểm kho.
* **Các bước thực hiện:**
  1. Khách hàng đăng nhập vào ứng dụng để xem danh sách các ô kho đang thuê, thời hạn hợp đồng, mã PIN truy cập và lịch sử giao dịch.
  2. Khách hàng có thể thực hiện các thao tác tự phục vụ: đổi mã PIN truy cập, đăng ký người được ủy quyền ra vào kho, hoặc gửi yêu cầu thay đổi loại kho/kích thước.
  3. Facility Staff/Manager thực hiện kiểm tra định kỳ tình trạng an ninh, an toàn PCCC toàn bộ điểm kho.
  4. Nếu phát hiện ô kho cần bảo trì hoặc kiểm tra kỹ thuật, Facility Staff cập nhật ghi chú và trạng thái bảo trì trên hệ thống.

### Flow 4: Business Rules, Fee Management, and Revenue Monitoring Flow (Quy trình Cấu hình Nghiệp vụ & Quản lý Doanh thu)
* **Mục tiêu:** Giúp Quản lý vận hành thiết lập chính sách giá, quy tắc nghiệp vụ và giám sát doanh thu toàn hệ thống.
* **Các bước thực hiện:**
  1. Business Operations Manager cấu hình khung giá niêm yết theo diện tích/thể tích ô kho, vị trí và loại kho.
  2. Business Operations Manager thiết lập chính sách nghiệp vụ: mức tiền đặt cọc (ví dụ: 1 tháng tiền thuê), phí phạt thanh toán chậm, phí đền bù mất chìa/hỏng kho, quy định hủy hợp đồng.
  3. Business Operations Manager tạo và phát hành các chương trình khuyến mại, mã giảm giá (Discount/Voucher).
  4. Hệ thống tự động áp dụng các quy tắc tài chính này vào quá trình tính toán hóa đơn cho khách hàng.
  5. Business Operations Manager theo dõi Dashboard doanh thu, tỷ lệ lấp đầy kho (Occupancy rate) và xuất báo cáo tài chính/vận hành định kỳ.

### Flow 5: Facility Storage and Staff Management Flow (Quy trình Quản lý Hạ tầng & Nhân sự tại Điểm kho)
* **Mục tiêu:** Quản lý danh mục ô kho tại cơ sở và điều phối nhân sự vận hành.
* **Các bước thực hiện:**
  1. Facility Manager thiết lập sơ đồ điểm kho, khai báo danh mục tất cả ô kho (Mã kho, Tầng, Vị trí, Loại kho, Kích thước, Bảng giá áp dụng).
  2. Facility Manager phân công ca trực và gán nhiệm vụ hàng ngày cho Facility Staff (tiếp đón khách, hỗ trợ bàn giao, kiểm tra trả kho, xử lý sự cố).
  3. Facility Staff tiếp nhận danh sách nhiệm vụ trên hệ thống và cập nhật tiến độ thực hiện.
  4. Khi ô kho xảy ra sự cố kỹ thuật hoặc hư hỏng, Facility Staff/Manager chuyển trạng thái kho sang "Under Maintenance" để hệ thống tạm dừng cho đặt chỗ.

### Flow 6: Storage Renewal and Overdue Handling Flow (Quy trình Gia hạn & Xử lý Quá hạn)
* **Mục tiêu:** Tự động hóa quá trình nhắc nợ/gia hạn hợp đồng và xử lý các trường hợp quá hạn thanh toán.
* **Các bước thực hiện:**
  1. Trước khi hết hạn hợp đồng $N$ ngày (ví dụ: 7 ngày), hệ thống tự động tạo hóa đơn kỳ tiếp theo và gửi thông báo nhắc gia hạn cho Khách hàng qua App/Email.
  2. **Trường hợp Gia hạn:** Khách hàng thực hiện thanh toán hóa đơn gia hạn trên App. Hệ thống ghi nhận thanh toán và tự động gia hạn hợp đồng.
  3. **Trường hợp Trả kho (Check-out):**
     - Khách hàng gửi thông báo trả kho trên App.
     - Facility Staff kiểm tra tình trạng thực tế ô kho khi khách dọn dẹp xong.
     - Facility Manager duyệt hoàn trả tiền cọc (sau khi trừ các khoản phí hỏng hóc/vệ sinh nếu có) và cập nhật trạng thái kho về "Available".
  4. **Trường hợp Quá hạn (Overdue):**
     - Nếu hết hạn hợp đồng mà khách hàng chưa thanh toán gia hạn hoặc chưa làm thủ tục trả kho, hệ thống chuyển hợp đồng sang trạng thái "Overdue".
     - Hệ thống tự động tính phí phạt quá hạn theo cấu hình.
     - Hệ thống gửi cảnh báo đến Facility Manager/Staff. Facility Staff tiến hành tạm khóa quyền truy cập kho của khách (vô hiệu hóa mã PIN/Thẻ từ).
     - Facility Manager thực hiện các biện pháp xử lý nợ hoặc xử lý tài sản lưu trữ theo quy định pháp lý và chính sách đã ban hành.

### Flow 7: Support Request and Issue Handling Flow (Quy trình Xử lý Yêu cầu Hỗ trợ & Sự cố)
* **Mục tiêu:** Tiếp nhận và xử lý kịp thời các yêu cầu hỗ trợ hoặc sự cố phát sinh từ khách hàng.
* **Các bước thực hiện:**
  1. Khách hàng tạo Ticket hỗ trợ trên ứng dụng (chọn loại sự cố: Khóa/Mã PIN, Ô kho hỏng, Vấn đề thanh toán, Hàng hóa, khác) kèm hình ảnh/mô tả.
  2. Hệ thống phân loại và tự động chuyển Ticket đến Facility Staff đang ca trực tại điểm kho tương ứng.
  3. Facility Staff tiếp nhận Ticket, liên hệ khách hàng hoặc đến kiểm tra xử lý trực tiếp tại ô kho.
  4. Nếu sự cố phát sinh chi phí đền bù hoặc dịch vụ bổ sung (ví dụ: phá khóa, thay ổ khóa mới), Facility Staff lập đề xuất chi phí để Facility Manager phê duyệt và gửi hóa đơn cho Khách hàng.
  5. Sau khi xử lý xong, Facility Staff cập nhật trạng thái Ticket thành "Resolved".
  6. Khách hàng xác nhận kết quả xử lý và đánh giá chất lượng dịch vụ trên ứng dụng.

---

## 4. CẤU TRÚC KIẾN TRÚC HỆ THỐNG (CLEAN ARCHITECTURE OVERVIEW)

Dự án Backend được tổ chức chuẩn theo mô hình **Clean Architecture** (ASP.NET Core 8) cho giải pháp **SelfStorageSystem**:

* **`SelfStorageSystem`** (Presentation Layer - Web API):
  * Chứa `Program.cs`, `appsettings.json`, API Controllers (`Controllers/`), Middlewares, Filters, Hubs, và static files (`wwwroot/`).
* **`SelfStorageSystem.Application`** (Application Layer):
  * Định nghĩa Interfaces, DTOs, Use Cases / Features, Validators, Business Services interfaces.
* **`SelfStorageSystem.Contracts`** (Contracts Layer):
  * DTOs và Request/Response models giao tiếp qua API.
  * Phân chia theo các mảng nghiệp vụ: `Customers/`, `Facilities/`, `Units/`, `Reservations/`, `Handovers/`, `Payments/`, `Deposits/`, `Fees/`, `Renewals/`, `Overdue/`, `Support/`, `Dashboard/`.
* **`SelfStorageSystem.Domain`** (Domain Layer):
  * Chứa Domain Entities, Enums, Exceptions, Domain Events, Business Rules độc lập với các thư viện bên ngoài.
* **`SelfStorageSystem.Infrastructure`** (Infrastructure Layer):
  * Thực thi DB Context (`SelfStorageDbContext`), Persistence (EF Core / PostgreSQL), External Services, Identity, Payment Gateway implementation.
* **`SelfStorageSystem.Tests`** (Test Layer):
  * Unit Test & Integration Test (xUnit).
* **`docs/`**:
  * Chứa tài liệu kỹ thuật, kiến trúc và phân tích nghiệp vụ tại `docs/architecture/`.

---

## 5. HƯỚNG DẪN CHẠY LOCAL (LOCAL SETUP & EXECUTION)

### 1. Build Solution
```bash
dotnet build SelfStorageSystem.sln
```

### 2. Chạy Web API
```bash
dotnet run --project SelfStorageSystem
```

Sau khi ứng dụng khởi chạy thành công:
- **Swagger UI:** `http://localhost:5000/swagger`
- **Health Check API:** `GET /api/health`
