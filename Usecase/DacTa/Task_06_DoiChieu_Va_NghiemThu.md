# Task 6 Rà soát độ phủ và tính nhất quán

Đã đối chiếu cả 7 hình gốc, 52 nhãn và một nhánh nhận hoàn bổ sung. Có 49 đặc tả, gồm 11 nghiệp vụ và 38 hệ thống/hành vi được mô tả. Các bước con được gộp đúng chức năng, không buộc số đặc tả bằng số oval.

| Hình | File gốc | Nhãn gốc | Đặc tả | Điểm cần sửa trên hình |
| --- | --- | --- | --- | --- |
| 3.2 | NghiepVu/DatIn_ThanhToan.png | 6 | NV-3.2-01 đến 06 | Khuyến mãi/giao là tùy chọn; tiếp nhận có tự động; không diễn tả trước–sau bằng include. |
| 3.5 | NghiepVu/HuyDon_HoanTien.png | 6 | NV-3.5-01 đến 05 | Không có duyệt hủy riêng; quá hạn tự động; danh sách hoàn chỉ CEO. |
| 4.2 | HeThong/TaiKhoan_XacThuc.png | 10 | HT-4.2-01 đến 10 | Khách kế thừa Người dùng; phân biệt Google, đăng ký OTP và đặt lại mật khẩu. |
| 4.3 | HeThong/DatInTrenApp.png | 10 | HT-4.3-01 đến 10 | Địa chỉ từ GPS; tuyến Geoapify, cước nội bộ; tạo đơn luôn VIETQR. |
| 4.4 | HeThong/ThanhToanVietQR.png | 6 | HT-4.4-01 đến 06 | QR hiển thị trong App; job độc lập; xếp in có điều kiện; tiền đến muộn không khôi phục đơn. |
| 4.5 | HeThong/TheoDoiDon.png | 7 | HT-4.5-01 đến 07 | Hủy cả hai trạng thái trước in; đổi tên hóa đơn để tránh hiểu là hóa đơn thuế. |
| 4.11 | HeThong/HuyDon_HoanTien.png | 7 | HT-4.11-01 đến 05 | Khách không xác nhận hoàn; chuyển ngân hàng ngoài hệ thống; bổ sung ba trường nhận hoàn. |

## Quan hệ UML và ranh giới cần thống nhất

1. Khái quát hóa: Khách hàng là loại Người dùng hệ thống, mũi tên tam giác rỗng hướng về tác nhân khái quát. Quyền cụ thể vẫn kiểm theo vai trò/kênh.
2. include: từ use case gọi tới hành vi dùng chung bắt buộc. Ví dụ Đăng ký email bao gồm xác minh OTP; không đảo chiều vì hành vi xảy ra trước/sau.
3. extend: từ hành vi bổ sung tới use case nền, có điều kiện và điểm mở rộng. Có thể dùng cho khuyến mãi, giao tùy chọn, đánh giá khi hoàn tất; không nối mọi use case để biến thành flowchart.
4. Thanh toán thành công gọi xử lý xếp in có điều kiện; đơn đã hủy, đã vào quy trình hoặc thiếu tài nguyên có nhánh khác. Không vẽ xếp in như luôn bắt buộc thành công.
5. Hủy quá hạn là job độc lập, không phải quản lý duyệt hoặc extension của nút kiểm tra payOS.
6. Chuyển hoàn thực tế nằm ngoài ranh giới phần mềm. Trong hệ thống chỉ có nhập tài khoản, xem khoản cần hoàn và CEO xác nhận đã chuyển.
7. Phân biệt ba mức: nghiệp vụ cửa hàng, chức năng người dùng và bước kỹ thuật bên trong. Dùng Activity/Sequence cho thứ tự gọi API/SQL, không cố biểu diễn mọi chi tiết bằng quan hệ Use Case.

## Kiểm tra điều kiện dùng chung

- **Tác nhân và quyền:** Khách hàng là một loại Người dùng hệ thống. App cho CUSTOMER/SHIPPER; Web cho STAFF/BRANCH_ADMIN/SUPER_ADMIN. Các hành vi khách trong bộ sơ đồ này chỉ dành CUSTOMER đúng chủ dữ liệu; không suy rộng quyền đặt in cho shipper.
- **Vòng đời đơn:** CHO_XAC_NHAN → DA_XAC_NHAN → DANG_IN → DA_IN → HOAN_THANH khi nhận tại quầy; giao hàng đi qua DANG_GIAO và có thể GIAO_THAT_BAI trước khi thử lại hoặc HOAN_THANH. Hủy chỉ từ hai trạng thái đầu tới DA_HUY.
- **Vòng đời tiền:** CHUA_THANH_TOAN → DA_THANH_TOAN → DA_HOAN_TIEN khi đủ điều kiện. Đơn App luôn VIETQR; COD thuộc quầy. Chờ hoàn là bộ lọc đơn DA_HUY trước in đã trả nhưng chưa hoàn, không phải trạng thái mới.
- **Nguồn xác nhận tiền:** Webhook có chữ ký hoặc API payOS đối soát hợp lệ. URL quay về, thao tác mở QR và lời khách báo đã chuyển không đủ để cập nhật tiền.
- **Xếp in:** Nhận tiền trước, xếp máy sau. Chưa đủ file/máy/vật tư có thể dừng chờ in; đã vào DANG_IN không hủy/hoàn theo luồng trước in. Bàn giao connector, hoàn tất giấy thực tế thuộc phần Tài.
- **Hoàn tiền:** CEO chuyển tiền qua ngân hàng bên ngoài rồi xác nhận; khách nhập lý do/ngân hàng/tài khoản và chỉ xem kết quả. Không có tự động refund ngân hàng, chứng từ hoàn hoặc bước quản lý duyệt hủy riêng.
- **Thông báo và thời gian:** SignalR khi còn kết nối, polling thanh toán khi màn hoạt động; không hứa push khi tắt App. Giờ hẹn là ước tính; chưa có timestamp cho mọi mốc hoặc mô hình SLA đầy đủ.
- **Cách đọc luồng phụ:** Dòng bắt đầu Bước n chỉ điểm phân nhánh. Yêu cầu sửa/nhập lại quay về bước được chỉ; từ chối hoặc kết thúc giữ trạng thái cũ. Lỗi mất phản hồi cần đọc lại trạng thái, không suy rằng yêu cầu chưa được lưu.

## Điểm bàn giao với thành viên khác

- Tài: sau thanh toán/tiếp nhận sang 4.7–4.8; giấy ra và hoàn tất máy không do màn thanh toán quyết định. Sau in sang 4.9 nhận quầy hoặc 3.4/4.10 giao nội bộ.
- Mai: đăng nhập STAFF/BRANCH_ADMIN tự mở/lấy ca riêng mặc định 200.000đ; CEO mở thủ công. Quyền và ca tuân theo phân hệ ca, không tự đóng ca khi đăng xuất.
- Hoàn tiền và trạng thái hủy dùng cùng luật trong App, Web, SQL và các hình 4.4/4.5/4.11. Không có hoàn thường sau khi bắt đầu in.

## Giới hạn và bằng chứng

Đây là rà soát tĩnh tài liệu với source, không phải biên bản nghiệm thu giao dịch payOS, GPS, Gmail, in giấy hay ngân hàng hoàn tiền. Không tự xác nhận các dịch vụ ngoài đã chạy thành công trên mọi thiết bị. Các file nguồn và ảnh gốc được kiểm hash trong quá trình tạo; không đổi để che chỗ chưa khớp.

Bản Word giữ mẫu bảng ba phần nghiệp vụ và mười trường hệ thống; mục lục và số trang được cập nhật khi xuất kiểm tra. Nội dung đã gộp trong DAC_TA_USECASE_MANH_DAY_DU.docx và bản MD cùng tên.

## Kết quả kiểm tra bản bàn giao

Bản Word tổng hợp đã kiểm tra 72 trang, Task 1 riêng 18 trang; đủ 49 ID và 49 bảng theo mẫu. Không còn trang trắng hoặc trang chỉ có phần đuôi ngắn bị rơi. Tài liệu mẫu, 7 ảnh gốc và các file source đối chiếu giữ nguyên. Cả 6 task đã hoàn thành trong phạm vi đặc tả.
