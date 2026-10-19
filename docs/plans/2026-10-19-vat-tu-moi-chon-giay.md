# Vật tư mới và chọn giấy trong kho — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** CEO thêm vật tư bằng popup; web tại quầy và Flutter chọn đúng giấy đã liên kết với dịch vụ đang bán.

**Architecture:** Giữ ASP.NET Core/Dapper, Blazor và Flutter hiện có. Dùng VatTu, TonKho, DichVu, BangGiaChiNhanh; không thay schema. Giá nhập không phải giá bán; số lượng chỉ tăng khi nhận phiếu nhập.

**Tech Stack:** .NET 8, SQL Server, Blazor WASM, Flutter; xUnit và flutter_test sẵn có.

## Thiết kế đã thống nhất

- Người dùng đã đồng ý phương án CEO thêm tên, đơn vị và giá nhập tham khảo; kho khởi tạo 0 ở các chi nhánh.
- Không tự tạo giá bán hoặc sửa đơn cũ khi thêm vật tư. CEO chọn vật tư trong Dịch vụ, khai báo quy cách và giá khởi tạo; giá bán từng nơi chỉnh tại Bảng giá.
- Không đưa mực/bìa/lò xo vào ô chọn giấy. Ô này chỉ lấy dịch vụ IN_AN đang bán của chi nhánh, giữ mã vật tư và mã dịch vụ.
- Hai vật tư cùng A4/80gsm vẫn là hai lựa chọn nếu khác mã vật tư. API trả tên kho để hai client hiển thị thống nhất; dữ liệu cũ thiếu trường mới dùng nhãn khổ/loại cũ.
- Giấy tồn 0 không tự biến mất: hệ thống vẫn áp dụng luồng chờ đủ vật tư trước khi in hiện có.

## Giả định và giới hạn

- Danh mục quy mô cửa hàng/chuỗi nhỏ, tải theo yêu cầu, không thêm bộ nhớ đệm hay dependency.
- Server kiểm tra quyền CEO, chuỗi rỗng/độ dài/ký tự điều khiển/giá; khóa giao dịch để ngăn tạo trùng. Mất mạng không gửi lại thao tác ghi tự động.
- Mobile Android/Flutter, giữ điều hướng và cơ chế báo giá lại khi chọn chi nhánh. Giữ loading/thử lại; vùng chạm giấy tối thiểu 48dp.
- Rà soát phát hiện bước cấu hình chỉ lấy bảng giá chi nhánh đầu. Bổ sung chọn chi nhánh xem giấy ngay tại bước này để không bỏ sót giấy bán riêng; bước sau ưu tiên chi nhánh đó nếu đáp ứng, vẫn báo giá lại trên server.
- Không kiểm thử ghi lên bảng nghiệp vụ thật, không khởi động lại API/máy in hoặc đụng thanh toán khi chưa được cho phép. SQL kiểm tra trên bảng # tạm riêng.

## Quyết định

1. Chọn tái sử dụng bảng hiện có thay vì tạo bảng loại giấy mới: ít rủi ro, giữ dữ liệu cũ.
2. Chọn vật tư qua dịch vụ có giá thay vì lấy tất cả kho làm bảng giá: tránh bán giấy chưa cấu hình hoặc dùng nhầm giá mua.
3. Không tự suy diễn quy cách từ tên giấy: người quản trị xác định khổ/mặt/màu/vật tư trong Dịch vụ.

## Các bước

1. RED: bổ sung kiểm thử HTTP quyền/dữ liệu, popup tạo vật tư, SQL tạo kho 0 và chống trùng trên bảng tạm; kiểm thử chọn hai giấy cùng quy cách trên web/mobile.
2. GREEN: thêm TaoVatTuRequest trong PhotocopyShared/Dtos/Tuan3/QuanLyDanhMucDtos.cs; POST danh-muc-vat-tu trong PhotocopyApi/Controllers/QuanTriDanhMucController.cs, transaction và SQL tham số.
3. Thêm popup và hướng dẫn quy trình tại PhotocopyAdmin/Pages/KhoVatTu.razor, giữ phiếu nhập hiện tại.
4. Mở rộng DichVuDto và truy vấn DanhMucController với mã/tên vật tư; sửa khóa lựa chọn ở LapDonTaiQuay.razor; đọc cùng trường trong lib/models.dart và cập nhật chip giấy ở cau_hinh_in_screen.dart.
5. Chạy dotnet test PhotocopyApi.Tests và Flutter test/analyze cho phần thay đổi; build web/API không đụng tiến trình đang chạy. Ghi rõ kiểm thử nào chưa làm trên thiết bị thật.
6. Bàn giao hướng dẫn CEO: thêm vật tư → nhập/nhận hàng → tạo quy cách gắn vật tư → điều chỉnh giá chi nhánh → chọn trên web/app.

Không tự commit: cây Git đang có nhiều thay đổi của người dùng. Thực hiện trong phiên hiện tại theo yêu cầu tiếp tục, không mở worktree hoặc sub-agent mới.

## Kết quả kiểm tra

- Đã chạy RED trước thay đổi cho route, popup, phân biệt vật tư cùng quy cách và chọn giấy theo chi nhánh, rồi chạy lại GREEN.
- .NET: 496 pass, 25 bài tích hợp tùy chọn bỏ qua trong lần chạy mặc định.
- SQL bổ sung: 2 pass khi bật kết nối riêng cho ThemVatTuSqlTests; chỉ tạo bảng # trong phiên kết nối, kiểm tra chống trùng, tồn mới 0, tồn/giá cũ nguyên vẹn.
- Flutter: toàn bộ 120 test pass. Analyze 4 file thay đổi không có lỗi/cảnh báo.
- Chưa nghiệm thu ghi vật tư/đơn lên dữ liệu thật hoặc thao tác trên điện thoại thật. Không gọi payOS và không tạo lệnh in khi kiểm thử.
