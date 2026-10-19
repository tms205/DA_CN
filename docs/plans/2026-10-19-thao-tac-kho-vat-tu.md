# Thao tác vật tư và mức cảnh báo — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Thêm thao tác sửa thông tin vật tư và mức cảnh báo bằng popup theo phạm vi người dùng đã đồng ý.

**Architecture:** Giữ Controller/Dapper và HopThoaiQuanTri hiện có. Sửa VatTu dùng chung chỉ CEO; sửa TonKho.MucToiThieu theo đúng chi nhánh cho CEO/quản lý. Không thêm bảng/cột, không sửa số lượng tồn, không xóa dữ liệu.

**Tech Stack:** ASP.NET Core 8, SQL Server, Blazor WASM, xUnit; Chrome fixture cho thao tác UI.

## Phạm vi và quyết định

- Người dùng đã đồng ý bổ sung hai thao tác được đề xuất; không mở nghiệp vụ bán lẻ hoặc in A0.
- Thông tin gồm tên, đơn vị, giá nhập tham khảo, không phải giá bán dịch vụ. Nêu rõ tác động toàn chuỗi trong popup.
- Đơn vị chỉ được đổi nếu không còn tồn ở bất kỳ chi nhánh nào, chưa có phiếu nhập và chưa được dịch vụ sử dụng. Server kiểm tra lại trong transaction; UI khóa tương ứng.
- Mức cảnh báo là số nguyên >=0 và chỉ ảnh hưởng cặp chi nhánh/vật tư đã chọn. Server chặn truy cập ngoài phạm vi, không dựa vào việc ẩn nút.
- Giao diện quản trị thực dụng: giữ màu navy, font, khoảng cách, bảng responsive và popup cũ; không thêm animation hoặc dependency. DFII 11 (impact 2, fit 5, feasibility 5, performance 4, consistency risk 5 với yêu cầu thay thiết kế cũ; chọn tái sử dụng để tránh rủi ro đó).
- Kiểm thử ghi chỉ dùng bảng # riêng/HTTP fixture, không cập nhật kho thật. Không tự dừng API đang chạy nếu chưa được cho phép.

## Kế hoạch TDD

1. Viết test HTTP cho PUT danh-muc-vat-tu/{id}, PUT kho/{chi-nhanh}/{vat-tu}/muc-canh-bao: dữ liệu thiếu/sai, quyền và phạm vi. Chạy để thấy thất bại do chưa có route.
2. Viết test render nút theo vai trò, popup thông tin đã điền/khóa đơn vị, popup mức cảnh báo đúng chi nhánh. Chạy RED.
3. Viết test SQL riêng kiểm tra không đổi tồn/chi nhánh khác, tên trùng, vật tư mất, chặn đổi đơn vị khi có tồn/phiếu/dịch vụ. Bảng # không ảnh hưởng dữ liệu nghiệp vụ.
4. Bổ sung DTO mức cảnh báo, cờ đổi đơn vị; query stock; API PUT với kiểm tra và SQL tham số. Tái sử dụng request tên/đơn vị/giá hiện có cho sửa vật tư.
5. Thêm cột Thao tác, dùng popup tạo vật tư hiện có cho sửa thông tin; popup riêng mức cảnh báo. Không thêm sửa tồn hoặc xóa.
6. Chạy lại test mục tiêu, toàn bộ .NET, build tách riêng nếu file đang khóa; browser fixture 1440/390/320px. Ghi hướng dẫn ngắn và trạng thái nạp bản mới.

File chính: PhotocopyApi/Controllers/QuanTriDanhMucController.cs; PhotocopyApi/Controllers/QuanTriController.cs; PhotocopyShared/Dtos/Tuan3/QuanLyDanhMucDtos.cs; PhotocopyShared/Dtos/Tuan3/Tuan3Dtos.cs; PhotocopyAdmin/Pages/KhoVatTu.razor.

Không tạo worktree/subagent hoặc commit vì workspace có nhiều thay đổi trước đó và người dùng yêu cầu tiếp tục trong phiên này.

## Kết quả

- Đã chạy RED trước khi thêm route/popup/SQL và GREEN sau triển khai.
- Toàn bộ .NET: 500 pass, 27 bài tích hợp tùy chọn không chạy mặc định. Riêng SuaKhoVatTuSqlTests bật cấu hình kết nối: 3 pass, chỉ ghi bảng #.
- Đã được người dùng cho phép khởi động lại khi không có giao dịch. Launcher build API/Admin/PrinterSimulator thành công, 0 warning/0 error; Web HTTP 200 và Swagger có cả hai route mới.
- Chỉ thêm/sửa nguồn code và tài liệu; không đổi schema, không tạo/sửa/xóa vật tư thật để nghiệm thu, không gọi payOS hoặc tạo lệnh in test.
