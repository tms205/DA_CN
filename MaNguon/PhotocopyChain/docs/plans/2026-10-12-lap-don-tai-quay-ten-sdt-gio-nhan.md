# Lập đơn tại quầy: tên/SĐT và giờ nhận Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Khách tại quầy chỉ cần tên hoặc SĐT; chọn thường/hỏa tốc hiển thị giờ nhận dự kiến rõ ràng.

**Architecture:** Giữ Blazor WebAssembly, DTO dùng chung, controller Dapper và SQL hiện có. Validation liên trường ở DTO dùng chung để web/API cùng áp dụng; chỉ tìm khách cũ khi có SĐT. Dùng `BaoGiaResponse.NgayHenTra`, không tạo công thức hẹn giờ hay sửa schema.

**Tech Stack:** .NET 8, Blazor, DataAnnotations, Dapper, SQL Server, xUnit.

---

## Yêu cầu và quyết định đã chốt

- Người dùng đã xác nhận phương án trong chat và yêu cầu triển khai ngay ở phiên hiện tại.
- Phạm vi: web lập đơn tại quầy và API tương ứng; không sửa mobile, thanh toán hoặc cơ chế phân máy.
- Bắt buộc có ít nhất một trong tên/SĐT sau trim. Nếu nhập tên, giữ ràng buộc tối đa 100 ký tự và tối thiểu 2 ký tự như controller cũ. Nếu nhập SĐT, vẫn kiểm tra 10 số ASCII bắt đầu 0.
- SĐT: tìm khách cũ và giữ chặn tài khoản khóa. Chỉ SĐT mà chưa có khách: dùng tên `Khách tại quầy`. Chỉ tên: khách vãng lai mới có email nội bộ duy nhất, SĐT NULL; không gộp người trùng tên và không bịa SĐT.
- Giờ nhận là **dự kiến**, lấy từ báo giá hiện có (số trang, hàng đợi, tốc độ máy, ưu tiên hỏa tốc), trình bày HH:mm dd/MM/yyyy. Không cam kết đúng phút, không thay công thức tính hay thời gian giao hàng.
- Đổi thường/hỏa tốc phải bỏ báo giá cũ, gọi lại API và cập nhật cả tiền lẫn giờ; lỗi/đang tính thì không hiện giờ cũ hay cho lập đơn với báo giá cũ.
- Không thêm thư viện, không thay schema/dữ liệu thật để kiểm thử. Giữ thay đổi có sẵn trong worktree; không tự commit, không tạo worktree khác vì người dùng đang chạy dự án này.
- Hiệu năng/độ tin cậy: dùng API báo giá hiện có, chặn gửi lặp và bỏ kết quả lỗi thời khi thay quy cách trong lúc đang chờ. Mất mạng phải báo lỗi, không tự tạo giờ nhận giả. Quyền chi nhánh và lịch sử khách có SĐT giữ nguyên.
- Phương án đã chọn: dùng dữ liệu báo giá sẵn có; không dùng mốc cố định 15/30 phút và không viết lại bộ lập lịch máy in. Ưu điểm: sửa ít, dễ bảo trì; hạn chế: độ chính xác phụ thuộc công thức ước tính hiện tại.

## Task 1: Validation và khách vãng lai

**Files:**
- Modify: `PhotocopyShared/Dtos/Tuan3/Tuan3Dtos.cs`, `KiemTraQuanTri.cs`.
- Modify: `PhotocopyApi/Controllers/DonHangController.cs` (ChonKhachTaiQuay).
- Test: `PhotocopyApi.Tests/LapDonTaiQuayTests.cs`, `KiemTraHttpNhapTests.cs`.

1. Viết test tên-only/SĐT-only/hai trường hợp lệ; rỗng/null/whitespace cả hai bị chặn; SĐT sai/tên quá dài vẫn bị chặn; controller hợp lệ đi tới truy cập DB, không trả lỗi thiếu trường.
2. Chạy `dotnet test PhotocopyApi.Tests --filter FullyQualifiedName~LapDonTaiQuayTests --artifacts-path <thư mục tạm riêng>`; xác nhận test tên-only/SĐT-only thất bại trước khi sửa.
3. Bỏ Required ở từng trường, dùng IValidatableObject kiểm tra OR. Controller dùng `QuyTacNhap.LayLoi(req)`, chỉ SELECT theo SĐT khi có SĐT; tạo email GUID riêng nếu không có số, ghi NULL thay vì chuỗi rỗng vào DB.
4. Chạy lại kiểm thử; không gửi request tạo khách vào DB thật trong bài test.

## Task 2: Giờ nhận và form web

**Files:**
- Modify: `PhotocopyAdmin/Pages/LapDonTaiQuay.razor`.
- Test: `PhotocopyApi.Tests/LapDonTaiQuayWebTests.cs`.

1. Viết test form không còn Required trên cả hai ô; lỗi thiếu tên/SĐT được chặn trước HTTP. Test đổi thường/hỏa tốc gọi API với đúng InGap và nhận giờ/mức giá tương ứng; lỗi/xử lý chậm không giữ báo giá cũ, không để response cũ ghi đè lựa chọn mới.
2. Chạy test để thấy thiếu hành vi mới; dùng HTTP giả ở biên, giữ logic component/DTO thực.
3. Form có hướng dẫn ít nhất một thông tin; bỏ Required từng ô; chỉ hiển thị SĐT trong thẻ khách nếu có. Thay checkbox bằng lựa chọn thường/hỏa tốc và tính lại báo giá khi đổi. Hiển thị giờ nhận dưới lựa chọn bằng trường NgayHenTra; nếu NULL ghi chưa ước tính được, không tự cộng thời gian cố định.
4. Ràng buộc cập nhật số bản/quy cách bỏ báo giá cũ; bộ đếm phiên báo giá loại kết quả lỗi thời trong lúc chờ HTTP. Không tự tạo đơn/thanh toán khi đổi lựa chọn.

## Task 3: Kiểm chứng và bàn giao

1. Chạy toàn bộ .NET tests bằng artifacts-path riêng; các test DB không có PHOTOCOPY_TEST_DB phải ghi rõ skipped.
2. Build solution vào artifacts-path riêng, tránh ghi đè binary API/web đang chạy.
3. Kiểm tra trình duyệt nếu có phiên phù hợp, không tạo khách/đơn thật chỉ để thử. Ghi rõ giới hạn nếu chưa kiểm DB thật.
4. Cập nhật `docs/LapDonTaiQuay_TenSdt_GioNhan_2026-10-12.md`: file thay đổi, cách hoạt động, kiểm chứng và cách chạy.
5. Không tự commit. Người dùng chạy lại `MaNguon/scripts/chay_web.cmd` và F5 để dùng bản mới.
