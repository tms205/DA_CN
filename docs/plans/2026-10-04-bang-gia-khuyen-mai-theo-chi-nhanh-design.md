# Thiết kế bảng giá và khuyến mãi theo chi nhánh

## Yêu cầu đã thống nhất

- Danh mục dịch vụ in ấn và gia công dùng chung toàn chuỗi.
- Mỗi chi nhánh có đơn giá và trạng thái hiển thị riêng cho từng dịch vụ.
- CEO chọn chi nhánh để quản lý giá và khuyến mãi của chi nhánh đó.
- CEO xem được tổng quan và so sánh giá giữa các chi nhánh.
- Mỗi khuyến mãi thuộc một chi nhánh; cùng mã code được phép xuất hiện ở nhiều chi nhánh.
- Quản lý chi nhánh chỉ xem, không được thay đổi giá hoặc khuyến mãi.
- Không xóa cứng dữ liệu đã được đơn hàng sử dụng.

## Giả định vận hành

- Quy mô hiện tại nhỏ, ưu tiên truy vấn và code dễ đọc hơn tối ưu sớm.
- Các thao tác ghi chỉ dành cho CEO và vẫn dùng cơ chế xác thực hiện có.
- Đơn giá được lưu vào chi tiết đơn để lịch sử không đổi khi bảng giá được cập nhật.
- Dữ liệu hiện tại phải được chuyển đổi mà không làm mất đơn hàng hoặc khuyến mãi cũ.

## Thiết kế dữ liệu

### Dịch vụ và bảng giá

`DichVu` tiếp tục lưu thông tin chung: tên, loại dịch vụ, khổ giấy, loại giấy, mặt in, màu sắc, nhóm gia công và vật tư.

Tạo bảng `BangGiaChiNhanh`:

- `MaChiNhanh`: khóa ngoại tới `ChiNhanh`.
- `MaDichVu`: khóa ngoại tới `DichVu`.
- `DonGia`: đơn giá của dịch vụ tại chi nhánh.
- `TrangThai`: `HIEN` hoặc `AN` tại chi nhánh.
- Khóa chính: `(MaChiNhanh, MaDichVu)`.

Giá hiện tại trong `DichVu` được sao chép sang mọi chi nhánh. Các thủ tục tính giá và API danh mục chỉ lấy dòng giá đúng `MaChiNhanh`.

### Khuyến mãi

Thêm `MaChiNhanh` vào `KhuyenMai` và tạo khóa ngoại tới `ChiNhanh`. Bỏ ràng buộc duy nhất toàn cục của `MaCode`, thay bằng ràng buộc duy nhất `(MaChiNhanh, MaCode)`.

Khi báo giá, khuyến mãi chỉ hợp lệ nếu đồng thời khớp mã code và chi nhánh của đơn.

## Giao diện và API

- Trang `Bảng giá in ấn` dùng phạm vi chi nhánh CEO đang chọn.
- Khi CEO chọn `Tổng quan tất cả`, trang hiển thị bảng so sánh giá, không cho ghi vì chưa có chi nhánh đích.
- Khi CEO chọn một chi nhánh, trang cho thêm dịch vụ chung, sửa giá và bật/tắt từng bản ghi tại chi nhánh đó.
- Thêm trang `Quản lý khuyến mãi` riêng trong menu CEO.
- Trang khuyến mãi hỗ trợ thêm, sửa và bật/tắt từng bản ghi; bắt buộc CEO chọn một chi nhánh trước khi ghi.
- API luôn lấy `MaChiNhanh` từ phạm vi đã xác thực, không tin mã chi nhánh do giao diện tự gửi khi thực hiện thao tác ghi.

## Chuyển đổi dữ liệu

1. Tạo `BangGiaChiNhanh` và sao chép giá hiện tại sang các chi nhánh.
2. Thêm `MaChiNhanh` tạm thời cho `KhuyenMai`.
3. Tạo bản khuyến mãi theo từng chi nhánh và cập nhật `DonHang.MaKhuyenMai` theo chi nhánh của đơn.
4. Thêm khóa ngoại và ràng buộc duy nhất theo chi nhánh.
5. Cập nhật các stored procedure tính giá/tạo đơn.

Script cập nhật phải chạy lại an toàn và không xóa dữ liệu.

## Xử lý lỗi và kiểm thử

- Từ chối sửa giá/khuyến mãi khi CEO đang ở `Tổng quan tất cả`.
- Từ chối giá âm, khoảng ngày sai, phần trăm ngoài 1–100 và khuyến mãi sai chi nhánh.
- Kiểm thử middleware phạm vi, truy vấn bảng giá theo chi nhánh, mã khuyến mãi theo chi nhánh và quyền ghi của CEO.
- Build toàn solution và kiểm tra giao diện bằng CEO ở cả chế độ tổng quan và một chi nhánh.

## Nhật ký quyết định

1. Chọn bảng `BangGiaChiNhanh` thay vì nhân bản `DichVu`: tránh lặp thông tin dịch vụ và dễ bảo trì.
2. Thêm `MaChiNhanh` trực tiếp vào `KhuyenMai`: mô hình đơn giản, phù hợp quy mô hiện tại.
3. Cho phép trùng `MaCode` giữa các chi nhánh: mỗi chi nhánh quản lý chiến dịch độc lập.
4. Chỉ CEO được ghi: giữ đúng quyền quản trị hiện có.
5. Không xóa cứng: bảo toàn lịch sử đơn hàng và đối soát.

## Rủi ro đã ghi nhận

- Chuyển khuyến mãi cũ phải cập nhật đúng khóa ngoại của các đơn đã tồn tại.
- Mọi nơi đọc `DichVu.DonGia` phải được đổi sang `BangGiaChiNhanh`; nếu bỏ sót sẽ dùng sai giá.
- Thêm chi nhánh hoặc dịch vụ mới phải tự tạo các dòng giá chi nhánh tương ứng.
