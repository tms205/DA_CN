# Giao hàng nội bộ Implementation Plan

> Kế hoạch triển khai và kiểm chứng; không tự commit hoặc thay đổi cấu trúc database.

**Goal:** Thay Ahamove bằng Geoapify, lấy vị trí Android và hoàn thiện giao đơn đã thanh toán QR qua shipper nội bộ.

**Architecture:** Giữ ASP.NET Core/Dapper, Blazor và Flutter hiện tại. Một dịch vụ Geoapify dùng chung cho báo giá và đặt đơn; bảng GiaoHang hiện có ghi lượt giao. API khóa đơn bằng giao dịch SQL khi nhận và cập nhật giao hàng, không thêm bảng/cột, không tự đặt tài xế hãng ngoài.

**Tech Stack:** .NET 8, SQL Server, xUnit, Blazor, Flutter, geolocator, Geoapify REST.

## Yêu cầu đã thống nhất và giả định

- Khách chọn vị trí/địa chỉ, bổ sung số nhà/tầng/phòng và ghi chú; từ chối GPS vẫn nhập địa chỉ được.
- Thanh toán QR toàn bộ trước; đơn DA_IN mới giao, shipper không thu COD.
- Shipper chỉ nhận đơn chi nhánh của mình; quầy/CEO có thể giao đơn cho shipper đúng chi nhánh.
- Giao thành công → HOAN_THANH; thất bại phải ghi lý do, tối đa ba lượt; sau đó cửa hàng xử lý với khách.
- Không theo dõi nền, không lưu API Key trên app, không tin phí/khoảng cách do client tự gửi.
- Quy mô đồ án vài chi nhánh; danh sách có giới hạn, dùng SignalR hiện có và nút tải lại. Timeout bản đồ 10 giây, GPS 15 giây; lỗi không biến phí thành 0.
- Một backend giữ bộ nhớ đệm và hạn mức API. Không SLA trả phí; chỉ bật giao tận nơi thật khi có key và đã kiểm tra tuyến địa phương.

## Decision log

1. Đội nội bộ thay hãng ngoài: người dùng chọn; Lalamove cần nạp ví Production và hướng dẫn tích hợp cả đặt đơn.
2. Geoapify thay Ahamove: người dùng đồng ý; gói Free 3.000 credits/ngày, tối đa 5 request/giây, có yêu cầu attribution (https://www.geoapify.com/pricing/).
3. Bảng phí đề xuất ngày 13/10/2026: 15.000đ/2km đầu + 4.000đ/km vượt; làm tròn tổng lên 1.000đ. Đây là giá cửa hàng, không phải thu nhập của tài xế hay báo giá nhà cung cấp. Không thêm phụ phí giờ cao điểm/mưa vì chưa có dữ liệu đáng tin.
   Tham khảo và kiểm tra lại ngày 14/10/2026: Lalamove https://www.lalamove.com/vi-vn/bang-gia-giao-hang (cấu trúc giá theo phương tiện/khu vực/quãng đường); Ahamove https://ahamove.com/service/aha-delivery (Nhanh 13.745đ/2km đầu, Siêu tốc 15.709đ/2km đầu). Phải xem tuyến/khu vực/phụ phí thực tế; không coi một bảng giá đại diện toàn Việt Nam.
4. Giữ cấu trúc CSDL: địa chỉ chi tiết/ghi chú giao ghép vào DiaChiGiao tối đa 255 ký tự; GhiChu cho thợ in giữ riêng. Không đổi phí/địa chỉ các đơn cũ.
5. Không tự hủy/hoàn tiền khi giao thất bại. Nếu thêm chuyển nhận tại quầy, giữ số tiền đã trả và phải ghi rõ không tự hoàn phí; cần chính sách riêng khi phát sinh hoàn tiền.

## Checkpoint thiết kế

- Android/Flutter, điện thoại trước, giữ tab hiện tại; trạng thái giao tiền quan trọng chỉ đổi sau xác nhận server, không xử lý offline.
- MFRI: (5+5)−(2+1+3)=4; thêm kiểm thử mất mạng, quyền GPS và font lớn.
- UI thực dụng navy hiện có: địa chỉ và phí rõ, CTA nhận/giao rộng ≥48dp, loading/lỗi/thử lại; không đổi font, màu hay kiến trúc chung. DFII=(3+5+5+4)−2=15.
- Đọc các tài liệu mobile-design thinking/touch/performance/backend/testing/debugging/Android. Không theo dõi GPS liên tục, không gesture-only, không nạp toàn bộ lịch sử vào ListView không lazy.

## Các bước thực hiện và kiểm chứng

1. Viết test xUnit quy tắc phí, phạm vi/đơn chưa trả tiền, snapshot báo giá; chạy RED trước khi thêm production code.
2. Thêm PhotocopyApi/GiaoHang/GeoapifyService.cs và QuyTacGiaoHang.cs; test HTTP fixture cho địa chỉ/tuyến/lỗi/quota; không gọi dịch vụ ngoài bằng key thật trong unit test.
3. Thay hai helper Ahamove trong Controllers/BaoGiaController.cs và DonHangController.cs; bổ sung DTO và đăng ký dịch vụ; bảo toàn luồng nhận tại quầy.
4. Viết test phân quyền/validation giao hàng, sau đó thêm GiaoHangController.cs: danh sách chờ/đang giao/lịch sử, phân công, tự nhận, thành công/thất bại; khóa và cập nhật trong một giao dịch, callback lặp không tăng lượt/thông báo.
5. Viết widget test địa chỉ, sau đó thêm lib/widgets/dia_chi_giao.dart và vị trí geolocator; cập nhật chon_chi_nhanh_screen.dart gửi tọa độ/địa chỉ chi tiết/snapshot. Android khai quyền foreground, không background.
6. Viết widget test shipper; thay shipper_screen.dart placeholder bằng danh sách và thao tác thật; gọi điện/mở đường đi bằng url_launcher sẵn có; dùng SignalR và cập nhật khi quay lại app.
7. Thêm PhotocopyAdmin/Pages/GiaoHang.razor và menu Vận hành; quản lý xem đơn/lượt/lý do, chọn shipper và phân công, không giả giao thành công thay tài xế.
8. .env thêm key trống và các mức phí; bỏ cấu hình Ahamove khỏi runtime, giữ lịch sử migration/rollback. Không in secret; không tự tạo tài khoản/nạp tiền.
9. Chạy dotnet test/build với artifacts-path riêng (API hiện chạy giữ khóa), flutter analyze/test/build APK; kiểm tra tuyến thật khi người dùng điền key. Không khởi động lại API hoặc cài đè app khi có giao dịch đang làm.
10. Ghi bàn giao docs/BAN_GIAO_GIAO_HANG_NOI_BO.md: file thay đổi, cấu hình, giá/nguồn/ngày, cách chạy, kiểm thử đã chạy và phần chưa xác minh. Không tự commit.

**Lệnh kiểm tra:** dotnet test PhotocopyChain.sln --artifacts-path C:/Users/USER/AppData/Local/Temp/PhotocopyGiaoHangBuild-20261013; C:/SDK/flutter/bin/flutter.bat analyze; C:/SDK/flutter/bin/flutter.bat test; C:/SDK/flutter/bin/flutter.bat build apk --debug.
