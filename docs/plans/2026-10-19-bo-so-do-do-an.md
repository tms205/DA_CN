# Bộ sơ đồ PhotocopyChain Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Tạo bộ sơ đồ có thể chỉnh sửa, ảnh xuất sẵn, thuyết minh và đối chiếu code cho đồ án; không thay đổi ứng dụng hoặc database.

**Architecture:** Tài liệu as-is được chụp theo mã nguồn ngày 19/10/2026. Tách mô hình nghiệp vụ, tương tác, dữ liệu và triển khai; các chức năng dự kiến không được coi là đã có. Dùng PlantUML cho ký pháp UML thực (đặc biệt Use Case), SVG cho ảnh phóng to không vỡ và trang HTML đọc ngoại tuyến.

**Tech Stack:** Markdown, PlantUML, SVG, Node.js; renderer Java chỉ dùng làm công cụ xuất ảnh, không thêm phụ thuộc vào dự án ứng dụng.

---

## Task 1: Kiểm kê nguồn và ranh giới

- Đọc controller, middleware phạm vi, dịch vụ in/thanh toán/giao hàng, schema và thủ tục.
- Không đọc .env hoặc dữ liệu đăng nhập; không gọi API phát sinh giao dịch.
- Lập kiểm kê endpoint và bảng/cột/FK từ source; ghi rõ đây không phải kiểm tra schema trực tiếp trên SQL đang chạy.
- Nguồn: `MaNguon/PhotocopyChain/PhotocopyApi`, `PhotocopyPrinterSimulator`, `PhotocopyShared`, `Database/01_TaoBang.sql`, `04_StoredProcedure.sql`, migration 003–013.

## Task 2: Mô hình chức năng và quy trình

- Tạo `docs/so-do/BO_SO_DO.md`: kiến trúc, Use Case, activity, sequence, state, ERD, class và deployment.
- Thuyết minh tiền/hậu điều kiện, nhánh lỗi, quyền và nguồn đối chiếu đi cùng sơ đồ.
- Không đưa lịch tự đóng 3 ca hoặc scanner vật lý vào hiện trạng nếu chưa tìm thấy triển khai.
- Đánh dấu hoàn tiền là xác nhận sau khi xử lý tiền ngoài hệ thống, không vẽ API tự chuyển tiền.

## Task 3: Xuất tài sản tài liệu

- Tạo `docs/so-do/xuat-so-do.cjs` để trích khối PlantUML, sinh ERD đầy đủ từ schema, kiểm kê API và dữ liệu.
- Xuất `nguon/*.puml`, `hinh/*.svg`, mục lục `index.html`, từ điển dữ liệu, ma trận endpoint → sơ đồ và manifest nguồn.
- Không ghi đè tệp người dùng ngoài thư mục bộ sơ đồ; không chỉnh code ứng dụng.

## Task 4: Kiểm chứng và bàn giao

- Chạy renderer trên từng sơ đồ; tất cả phải biên dịch được và không chứa ảnh báo lỗi.
- Kiểm tra số bảng/cột/FK, tất cả endpoint kiểm kê đều được ánh xạ vào ít nhất một sơ đồ/phần thuyết minh.
- Kiểm tra liên kết ảnh và xem ảnh đại diện, bảo đảm tiếng Việt và chữ không bị cắt.
- Ghi kết quả thực tế trong `docs/so-do/KIEM_CHUNG.md`; không đánh đồng kiểm chứng tài liệu với nghiệm thu runtime toàn hệ thống.
- Bàn giao đường dẫn mở HTML, đọc MD và chỉnh nguồn UML; nêu hạn chế và phần còn cần hội đồng/nhóm xác nhận.

## Giới hạn áp dụng skill

Đây là tài liệu của working tree hiện có, không phải thay đổi code cần TDD/commit. Không tạo worktree, không tự commit và không gọi skill `superpowers:executing-plans` không có trong môi trường. Thực hiện trực tiếp theo yêu cầu người dùng, không mở phiên/agent khác.
