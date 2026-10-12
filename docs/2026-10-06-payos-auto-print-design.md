# Thiết kế tự động in sau thanh toán payOS

Ngày chốt thiết kế: 06/10/2026

## Mục tiêu

- Đơn VietQR lập tại quầy hoặc trên ứng dụng được tự động xác nhận và đưa sang `DANG_IN` sau khi webhook payOS hợp lệ báo đã nhận đủ tiền.
- Máy in mô phỏng chạy thành một chương trình riêng, nhận lệnh qua API và chỉ chuyển đơn sang `DA_IN` sau khi báo in xong.
- Có thể thay bộ mô phỏng bằng connector máy in Windows hoặc IPP trong tương lai mà không đổi luồng thanh toán và đơn hàng.
- Đơn COD tiếp tục được nhân viên tiếp nhận và bắt đầu in thủ công.

## Quyết định chính

1. Dùng `PhotocopyPrinterSimulator` chạy riêng thay vì nhúng bộ đếm thời gian vào API.
2. Không thêm bảng hoặc cột CSDL. Tận dụng `MayIn`, `DonHang`, `ChiTietDonHang`, `FileIn` và `ThanhToan` hiện có.
3. `MaDonHang` là mã lệnh in ổn định giữa API và connector.
4. Connector chủ động gọi API để nhận việc, phù hợp cả máy USB và máy mạng nằm trong chi nhánh.
5. Tiến trình mô phỏng đang chạy được lưu trong file cục bộ để khởi động lại không làm mất hàng đợi.
6. Chỉ webhook payOS đã kiểm tra chữ ký, mã đơn, nội dung, loại tiền và số tiền mới được xác nhận thanh toán.

## Luồng xử lý

1. payOS gửi webhook thành công.
2. API cập nhật `ThanhToan.TrangThai = DA_THANH_TOAN` bằng thủ tục hiện có.
3. API tự chuyển đơn VietQR từ `CHO_XAC_NHAN` sang `DA_XAC_NHAN`.
4. API chọn máy đang hoạt động, cùng chi nhánh, đủ khổ giấy, màu và hai mặt; ưu tiên máy đang có ít đơn `DANG_IN` nhất.
5. API dùng `sp_PhanCongMayIn` và `sp_CapNhatTrangThaiIn` để phân công, trừ vật tư và chuyển đơn sang `DANG_IN`.
6. Connector lấy đơn tiếp theo của từng máy, tải thử các file in và tính thời gian theo cấu hình mẫu máy.
7. Connector báo hoàn thành; API gọi lại thủ tục trạng thái, tăng `MayIn.SoTrangDaIn`, gửi SignalR và chuyển đơn sang `DA_IN`.

Nếu đã nhận tiền nhưng hết vật tư hoặc chưa có máy phù hợp, thanh toán vẫn được ghi nhận; đơn dừng ở `DA_XAC_NHAN` để nhân viên xử lý.

## Giao tiếp connector

- `POST /api/may-in-ao/{maMayIn}/heartbeat`: báo máy còn kết nối.
- `GET /api/may-in-ao/{maMayIn}/lenh-tiep-theo`: lấy đơn `DANG_IN` tiếp theo đã phân cho máy.
- `GET /api/may-in-ao/don/{maDonHang}/file/{maFile}`: tải file bằng quyền connector.
- `POST /api/may-in-ao/don/{maDonHang}/hoan-tat`: xác nhận in xong.
- `POST /api/may-in-ao/don/{maDonHang}/loi`: báo lỗi mô phỏng; đơn giữ ở `DANG_IN` để không ghi nhận sai.

Các endpoint dùng khóa riêng trong .NET User Secrets và so sánh khóa theo thời gian cố định. Khóa không được ghi vào mã nguồn hoặc tài liệu.

## Tính thời gian

Mỗi mẫu máy có cấu hình tốc độ A4, A3, hai mặt và thời gian ra trang đầu. Thời gian dự kiến là tổng thời gian của từng dòng in:

`thời gian = thời gian trang đầu + tổng(số trang × số bản × 60 / tốc độ phù hợp)`

Cấu hình ban đầu dùng bốn mẫu đang có trong dữ liệu: Ricoh MP 2001L, Canon imageRUNNER ADVANCE C3525i, Xerox WorkCentre 3345 và HP LaserJet Pro M404dn.

## Điều hướng payOS

- Link tạo từ Web quản trị mang kênh `admin`. Trang kết quả trả về hàng đợi tại `http://localhost:5064/` và kèm mã đơn để mở đúng bản ghi.
- Link tạo từ tài khoản khách hàng mang kênh `mobile`. Do workspace chưa có source mobile, trang kết quả hiển thị thành công và hướng dẫn quay lại ứng dụng; trạng thái vẫn cập nhật bằng API/SignalR.
- Tham số `status=PAID` trên URL không cập nhật CSDL.

## Kiểm thử và tiêu chí hoàn thành

- Kiểm thử chọn đúng máy theo chi nhánh và khả năng in.
- Kiểm thử công thức thời gian A4, A3, một mặt và hai mặt.
- Kiểm thử webhook lặp lại không tạo hai lần in.
- Build toàn solution và chạy toàn bộ unit test.
- Chạy API, Admin và Simulator; xác nhận một đơn VietQR đi từ thanh toán đến `DANG_IN`, sau đó thành `DA_IN` khi simulator hoàn tất.
- Đơn COD không tự động đổi trạng thái.

## Ngoài phạm vi hiện tại

- Chưa gửi lệnh tới máy in vật lý trong lần triển khai này.
- Connector máy thật sau này sẽ thay bộ xử lý mô phỏng bằng Windows Print Queue hoặc IPP và giữ nguyên hợp đồng API ở trên.
