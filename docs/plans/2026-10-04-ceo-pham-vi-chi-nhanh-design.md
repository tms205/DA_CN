# Thiết kế phạm vi chi nhánh cho CEO

## Tóm tắt yêu cầu

- CEO tương ứng với vai trò `SUPER_ADMIN`.
- CEO chọn được một chi nhánh cụ thể hoặc `Tổng quan tất cả chi nhánh`.
- Phạm vi đã chọn áp dụng cho toàn bộ màn hình quản trị.
- Khi chọn một chi nhánh, CEO có thể xem và thao tác đầy đủ như quản lý của chi nhánh đó.
- Ở chế độ toàn chuỗi, CEO vẫn thao tác được trên từng bản ghi thuộc mọi chi nhánh.
- Trình duyệt ghi nhớ lựa chọn gần nhất của CEO.
- Không thay đổi quyền của quản lý chi nhánh, nhân viên hoặc cấu trúc cơ sở dữ liệu.

## Giả định và ràng buộc

- Chỉ `SUPER_ADMIN` được phép đặt phạm vi chi nhánh tùy chọn.
- API luôn kiểm tra quyền; giao diện không phải lớp bảo vệ duy nhất.
- Nếu chi nhánh đã chọn không còn hoạt động hoặc không tồn tại, giao diện trở về toàn chuỗi.
- Quy mô danh sách chi nhánh hiện tại nhỏ hoặc vừa, chưa cần phân trang trong bộ chọn.
- Không thêm thư viện mới và không thay đổi kiến trúc tổng thể.
- Code mới dùng cách đặt tên tiếng Việt, bố cục và mức chú thích giống code hiện có.

## Phương án được chọn

Dùng một trạng thái phạm vi chi nhánh dùng chung ở Web Admin. Trạng thái được lưu trong trình duyệt và tự gắn mã chi nhánh vào mọi request API bằng một HTTP header riêng. Khi không chọn mã chi nhánh, request mang nghĩa toàn chuỗi.

Phía API chỉ đọc header tùy chọn khi người gọi là `SUPER_ADMIN`. Các vai trò khác luôn dùng mã chi nhánh trong JWT, vì vậy không thể đổi phạm vi bằng cách tự sửa request. Các endpoint lấy danh sách hoặc số liệu dùng phạm vi hiệu lực để lọc; endpoint thao tác một bản ghi tiếp tục xác định chi nhánh từ bản ghi và kiểm quyền trước khi thay đổi.

## Luồng giao diện

1. Sau khi khôi phục phiên đăng nhập, Web khôi phục chi nhánh CEO đã chọn.
2. Sidebar tải danh sách chi nhánh đang hoạt động và hiển thị bộ chọn cho CEO.
3. Chọn `Tổng quan tất cả chi nhánh` sẽ xóa phạm vi đang chọn.
4. Chọn một chi nhánh sẽ lưu mã chi nhánh và thông báo cho các trang tải lại dữ liệu.
5. Các màn hình chỉ dành cho quản lý chi nhánh cũng hiển thị cho CEO; thao tác tạo mới cần có chi nhánh cụ thể hoặc lấy được chi nhánh từ bản ghi.

## Xử lý lỗi và trường hợp biên

- Bỏ qua header phạm vi nếu người dùng không phải CEO.
- Trả lỗi rõ ràng nếu thao tác tạo mới cần một chi nhánh nhưng CEO đang ở chế độ toàn chuỗi.
- Không cho dùng mã chi nhánh không tồn tại hoặc đã ngừng hoạt động làm phạm vi mới.
- Khi đổi chi nhánh, dữ liệu cũ không được giữ lại như thể thuộc chi nhánh mới.
- Đăng xuất xóa trạng thái phạm vi của phiên người dùng khỏi bộ nhớ; lựa chọn đã lưu chỉ được dùng lại cho tài khoản CEO phù hợp.

## Kiểm thử

- CEO không chọn chi nhánh nhận dữ liệu toàn chuỗi.
- CEO chọn chi nhánh nhận đúng dữ liệu và thao tác như quản lý chi nhánh đó.
- Quản lý chi nhánh không thể dùng header để truy cập chi nhánh khác.
- Mã chi nhánh không hợp lệ bị từ chối hoặc tự trở về toàn chuỗi theo đúng ngữ cảnh.
- Lựa chọn còn hiệu lực sau khi chuyển trang và tải lại trình duyệt.
- Build và chạy toàn bộ test hiện có của solution.

## Decision log

1. Chọn HTTP header chung thay vì thêm tham số vào từng URL để tránh lặp và giảm nguy cơ bỏ sót request.
2. Không cấp lại JWT khi đổi chi nhánh để phiên đăng nhập ổn định và không giả mạo tài khoản quản lý.
3. Giữ chế độ toàn chuỗi có quyền thao tác trên từng bản ghi theo yêu cầu; các thao tác tạo mới không gắn với bản ghi phải xác định chi nhánh rõ ràng.
4. Không sửa database và không thêm dependency.
5. Ưu tiên thay đổi nhỏ, tên tiếng Việt và cách trình bày giống code hiện tại.
