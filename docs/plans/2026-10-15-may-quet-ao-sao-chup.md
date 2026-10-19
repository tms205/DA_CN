# Máy quét ảo và sao chụp thẻ — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Sao chụp hai mặt thẻ lên cùng một mặt A4 tại quầy, có máy quét ảo để thử và ranh giới lấy ảnh có thể thay bằng connector máy thật.

**Architecture:** Máy ảo nhận JPG/PNG hoặc ảnh minh họa không có dữ liệu cá nhân. API chuẩn hóa ảnh, cắt/xoay theo lựa chọn, ghép PDF một trang và trả ảnh xem trước. Chỉ khi nhân viên xác nhận mới tải PDF vào FileIn và báo giá qua luồng cũ. Nguồn máy quét có hợp đồng riêng; adapter WIA/connector chưa được triển khai hay kiểm chứng khi chưa có máy thật.

**Tech Stack:** .NET 8, Blazor WebAssembly, PdfPig, System.Drawing sẵn có trên Windows. Không thêm package, bảng, cột hoặc sửa giá.

## Hiểu biết và phạm vi đã duyệt

- Người dùng xác nhận chưa có máy thật, cần máy ảo và có đường mở rộng sang máy thật.
- Dịch vụ tại quầy: hai mặt thẻ, cắt/xoay, xem trước A4, số bản và đen trắng/màu.
- Máy ảo là mô phỏng phần mềm, không giả lập driver USB hoặc tuyên bố đã kết nối phần cứng.
- Tận dụng FileIn, quyền nhân sự, bảng giá A4, thanh toán và hàng đợi in; chưa tính phí scan riêng.
- Giữ nguyên các thay đổi thư viện mẫu và GPS đang có. Không tự restart dịch vụ hay gửi lệnh in.
- Không OCR, không tạo/sửa nội dung giấy tờ, không dùng CCCD thật cho kiểm thử.

## Giả định và giới hạn

- Quy mô hiện tại của đồ án; mỗi phiên trình duyệt chỉ xử lý một thao tác scan/ghép/lưu tại một thời điểm.
- Giới hạn ảnh 10 MB và 20 megapixel mỗi mặt; chuẩn hóa có giới hạn kích thước, không lưu lâu dài ảnh nguồn/preview trên API. ASP.NET có thể buffer multipart bằng tệp tạm trong request; framework quản lý dọn tệp đó. PDF đã xác nhận dùng chính sách FileIn hiện có.
- Chỉ nhân sự được dùng API scan, không có URL công khai, không log nội dung ảnh. Kết quả scan tạm ở bộ nhớ trình duyệt, không lưu localStorage.
- Hủy/đổi ảnh/lỗi phải làm mất bản ghép cũ; mất kết nối không cho tạo bản in từ kết quả cũ. Lỗi không tự tạo đơn hoặc in.
- Giao diện industrial/utilitarian theo navy và font Be Vietnam Pro/IBM Plex Mono hiện có. Neo nhận diện: mặt kính và hai vị trí thẻ, màn trạng thái máy; không đổi bố cục toàn web. DFII: 3+5+5+5−3=15.
- Máy thật sau này cần driver và kiểm tra model. WIA là API Windows, không phải giao thức mạng; LAN có thể dùng WS-Scan/WSD nếu máy hỗ trợ. Connector tại tiệm trao đổi với backend bằng REST có xác thực, HTTPS khi triển khai qua mạng.

## Decision log

| Quyết định | Phương án khác | Lý do |
|---|---|---|
| Máy ảo nhận ảnh, trả cùng kết quả ảnh quét | Giả lập driver USB | Không cần cài driver giả, có thể nghiệm thu toàn bộ bước xử lý |
| Tách IMayQuet và xử lý/ghép ảnh | Nhúng WIA vào controller/PDF | Phần cứng/connector có thể thay nguồn mà không viết lại xử lý và đặt đơn |
| Kết quả tạm trong request và bộ nhớ web | Tạo bảng phiên scan và lưu CCCD tạm | Không cần schema và giảm dữ liệu nhạy cảm tồn đọng |
| PDF A4 một mặt; khung thẻ 86×54 mm, giữ tỉ lệ | Kéo méo ảnh; mỗi mặt thành một trang | Hai mặt nằm trên cùng một mặt giấy, số trang tính tiền đúng |
| Giá A4 chi nhánh hiện có | Thêm phí scan hoặc loại dịch vụ SQL | Chưa có yêu cầu giá scan riêng; không tự thay đổi database |
| Chưa viết adapter WIA cụ thể | Hứa hỗ trợ mọi máy USB/LAN | Không có phần cứng để xác minh driver, trạng thái và ảnh thực |

## Task 1 — Kiểm thử trước

Files: `MaNguon/PhotocopyChain/PhotocopyApi.Tests/SaoChupHttpTests.cs`, `SaoChupPdfTests.cs`, `SaoChupWebTests.cs`.

1. Viết kiểm thử HTTP thực không nối DB: quyền, scan, crop/xoay, ảnh giả/thiếu/quá lớn, ghép hai mặt và PDF một trang.
2. Chạy lọc SaoChup, ghi nhận RED do route/chức năng chưa có.
3. Bổ sung kiểm thử PDF/preview với hai ảnh tổng hợp và kiểm thử cấu hình A4 một mặt ở quầy trước khi sửa UI.

## Task 2 — API và máy quét ảo

Files mới: `PhotocopyShared/Dtos/DatIn/SaoChupDtos.cs`; `PhotocopyApi/MayQuet/IMayQuet.cs`, `MayQuetAo.cs`, `AnhQuetXuLy.cs`, `SaoChupPdf.cs`; `PhotocopyApi/Controllers/MayQuetController.cs`. Sửa: `PhotocopyApi/Program.cs`.

1. Định nghĩa kết quả ảnh JPEG và bản ghép PDF/PNG; validate crop trong ảnh, góc xoay hợp lệ và kích thước.
2. Máy ảo chờ mô phỏng có hủy, chuẩn hóa ảnh và bỏ metadata. Cung cấp ảnh minh họa có chữ DEMO, không giống giấy tờ thật.
3. Ghép hai mặt trên cùng một A4, giữ tỉ lệ; trả PDF và PNG cùng bố cục, no-store.
4. Chạy lại RED thành GREEN; không thay đổi SQL hay FileInController.

## Task 3 — Bảng điều khiển tại quầy

Files mới: `PhotocopyAdmin/Components/MayQuetAo.razor` và `.razor.css`. Sửa tối thiểu: `PhotocopyAdmin/Pages/LapDonTaiQuay.razor`.

1. Thêm lựa chọn Sao chụp giấy tờ/thẻ; chọn nguồn từng mặt hoặc dùng ảnh minh họa.
2. Trạng thái máy, crop phần trăm có giải thích, xoay 90°, quét lại, mô phỏng lỗi/offline và khôi phục; mọi thao tác có nhãn, focus và khóa khi bận.
3. Đổi ảnh/quy cách hủy preview cũ. Hiển thị PNG A4, chỉ tải PDF vào FileIn sau xác nhận; khóa sửa khi PDF đã chọn.
4. Lọc dịch vụ A4 một mặt; giữ hướng giấy đứng, số bản/màu; callback báo giá dùng luồng cũ. Không thay đổi giá ảnh thẻ hoặc tài liệu.
5. Kiểm thử trình duyệt 1440/768/390 px, lỗi/khôi phục/quét lại/nhấp đúp, báo giá và không tạo dữ liệu thật.

## Task 4 — Nghiệm thu và hướng dẫn

1. `dotnet test PhotocopyApi.Tests --artifacts-path <thư mục QA riêng>`: tất cả test không yêu cầu DB phải pass; báo rõ DB skip.
2. Build API/Web/connector trong thư mục riêng, rồi build Web mặc định nếu không bị khóa. Không tự restart API đang có giao dịch.
3. Lưu `docs/HUONG_DAN_MAY_QUET_AO.md`: cách thử, file thay đổi, quyền/lưu trữ, phân biệt mô phỏng và connector WIA tương lai, điều kiện USB/LAN.
4. Không tự commit hoặc xóa dữ liệu hiện có.

## Nguồn kỹ thuật đã kiểm tra

- https://learn.microsoft.com/en-us/windows-hardware/drivers/image/wia-architecture-overview
- https://learn.microsoft.com/en-us/windows-hardware/drivers/image/wia-with-web-services-for-devices
- https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/scan-from-your-app

## Hoàn tất phần máy quét ảo — 16/10/2026

- Các task API, giao diện tại quầy, kiểm thử và hướng dẫn đã hoàn tất. Giữ nguyên các chỉnh sửa mẫu đơn/GPS có trước.
- Toàn bộ suite có bật browser: 437 passed, 20 test cần database skipped, 0 failed. Browser 1440/768/390 px đều qua; không dùng dữ liệu giấy tờ thật, không thu tiền hoặc gửi lệnh in.
- API/Web build mặc định 0 cảnh báo, 0 lỗi. Không tự chạy lại launcher chính; cần người dùng chạy `MaNguon/scripts/chay_web.cmd` để nạp bản mới khi không có giao dịch.
- Hướng dẫn và giới hạn chi tiết: `docs/HUONG_DAN_MAY_QUET_AO.md`.
- Adapter WIA/connector scan thật, xác thực/khóa lệnh theo thiết bị và nghiệm thu USB/LAN chưa triển khai; không đánh dấu hoàn tất phần cứng chỉ vì đã có interface.
