# Thư viện mẫu đơn tải sẵn – bàn giao 14/10/2026

## Kết quả
Đã thay 3 mẫu tự dựng bằng **18 PDF tải sẵn, 21 trang**. Web và app khách hàng có tìm mẫu theo tên/nguồn, lọc nhóm, xem đúng từng trang, phóng to rồi chọn để in. Không tự điền thông tin khách vào mẫu; vẫn in bản trống để viết tay.

Thư mục:
`C:\Users\USER\Desktop\DA_CN\CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD\MaNguon\PhotocopyChain\PhotocopyApi\TaiNguyen\MauDon`

[Danh sách mẫu và nguồn](../MaNguon/PhotocopyChain/PhotocopyApi/TaiNguyen/MauDon/README.md).
[Danh mục có SHA256/URL tải gốc](../MaNguon/PhotocopyChain/PhotocopyApi/TaiNguyen/MauDon/catalog.json).
[Bản nguồn chưa kích hoạt](../MaNguon/PhotocopyChain/PhotocopyApi/TaiNguyen/MauDon/NguonGoc/README.md).

Nguồn đang dùng: 2 mẫu tham khảo việc làm/nghỉ phép, 5 mẫu học vụ Đại học Duy Tân, 11 mẫu Đại học Hồng Đức. Tên trường, mốc ban hành và phạm vi được giữ/ghi rõ. **Không khẳng định mẫu được chấp nhận ở mọi cơ quan hoặc mẫu chính sách cũ còn hiệu lực hiện hành.**

## Luồng hoạt động
1. Ứng dụng lấy danh sách bằng `GET /api/file-in/mau`.
2. Chọn Xem mẫu: nạp PNG của trang cần xem bằng `GET /api/file-in/mau/{maMau}/trang/{soTrang}`. PNG kết xuất từ chính PDF, không dựng lại biểu mẫu; chỉ nạp một trang mỗi lần.
3. Người dùng có thể chuyển trang/phóng to. Web dùng cửa sổ dialog, có đóng/ESC và điều khiển bàn phím; mobile dùng màn hình riêng, vùng chạm 48dp. Tải lỗi có Thử lại, không tự chọn khi chưa có ảnh.
4. Chỉ khi bấm Chọn để in mới gọi `POST /api/file-in/mau/{maMau}`. Tại quầy gửi mã khách, app dùng tài khoản khách đang đăng nhập.
5. API tìm đúng mã trong catalog, đọc PDF local và đối chiếu SHA256. File lạ/mã sai không được đọc; thiếu hoặc sai nội dung báo lỗi thay vì tự sinh một bản đơn khác.
6. PDF được sao chép qua hàm tải file cũ, giữ kiểm tra quyền/khách hàng, đếm trang và lưu FileIn độc lập. Không dùng chung file đang in với tài nguyên mẫu.
7. Tiếp tục báo giá/quy cách/chi nhánh/số bản/đặt đơn/thanh toán/in như luồng tài liệu cũ. Ảnh PNG không dùng làm file in, không làm tăng số trang tính giá.
8. Đơn đã tạo trước đây vẫn giữ file riêng; thay catalog không sửa đơn cũ.

Endpoint PDF gốc `GET /api/file-in/mau/{maMau}/pdf` vẫn được giữ. Quyền: CUSTOMER, STAFF, BRANCH_ADMIN, SUPER_ADMIN; chưa đăng nhập 401, SHIPPER 403. Không đưa token vào URL xem mẫu.

## Files đã thay đổi

| File (tính từ MaNguon) | Nội dung |
|---|---|
| PhotocopyChain/PhotocopyApi/DichVu/MauDonPdf.cs | Bỏ vẽ đơn bằng PdfDocumentBuilder/font Arial. Đọc catalog/PDF/ảnh local, whitelist tên file và SHA256. Giữ tên lớp/hàm để hạn chế refactor. |
| PhotocopyChain/PhotocopyApi/Controllers/FileInController.cs | Giữ các route danh sách/PDF/chọn mẫu; thêm route ảnh từng trang và thông báo 404/503. Chọn mẫu vẫn đi qua TaiLen. |
| PhotocopyChain/PhotocopyApi/PhotocopyApi.csproj | Copy catalog, PDF, PNG vào output/publish. Không đóng gói NguonGoc. |
| PhotocopyChain/PhotocopyShared/Dtos/DatIn/MauDonDto.cs | Thêm nhóm, nguồn, URL, phạm vi, số trang, tên file, SHA256; giữ constructor cũ. |
| PhotocopyChain/PhotocopyAdmin/Pages/LapDonTaiQuay.razor | Tìm/lọc mẫu, nguồn/phạm vi, dialog xem trang/phóng to và chọn in; không tạo file khi chỉ xem. |
| PhotocopyChain/PhotocopyAdmin/Pages/LapDonTaiQuay.razor.css | Bố cục thư viện và dialog responsive, dùng màu/font/token hiện có. |
| PhotocopyChain/PhotocopyAdmin/wwwroot/index.html | Hai hàm nhỏ mở/đóng native dialog mẫu, không thêm thư viện UI. |
| photocopy_mobile/lib/api.dart | Tải ảnh xem trước kèm bearer header, kiểm tra PNG/dung lượng và xử lý lỗi. |
| photocopy_mobile/lib/screens/dat_in/chon_file_screen.dart | Thêm tìm/nhóm/nguồn/số trang; mở preview trước, xác nhận mới tạo file. Reset bộ lọc khi đổi dịch vụ. |
| photocopy_mobile/lib/screens/dat_in/xem_mau_don_screen.dart | Màn hình mới xem từng trang, phóng/thu, chuyển trang, lỗi/Thử lại, nguồn; back không tạo file. |
| PhotocopyChain/PhotocopyApi/TaiNguyen/MauDon/ | PDF, catalog, 21 PNG, README nguồn và bản chưa kích hoạt. |
| PhotocopyChain/PhotocopyApi.Tests/MauDonTaiSanTests.cs | Kiểm tra bytes đúng file tải sẵn, SHA256, số trang/nguồn, từ chối mã/path lạ. |
| PhotocopyChain/PhotocopyApi.Tests/MauDonHttpTests.cs | HTTP đọc mọi PDF/trang; 401/403 và quyền các vai trò, không dùng database. |
| PhotocopyChain/PhotocopyApi.Tests/KiemTraHttpNhapTests.cs | Cập nhật danh mục nhiều mẫu, MIME PDF/PNG, từ chối trang không có. |
| PhotocopyChain/PhotocopyApi.Tests/AnhTheTests.cs | Cập nhật nhãn chữ đúng PDF xin việc tải sẵn, không còn kỳ vọng chữ của bản tự dựng. |
| PhotocopyChain/PhotocopyApi.Tests/LapDonTaiQuayWebTests.cs | Bỏ qua thuộc tính CSS isolation khi so HTML; không bỏ kiểm tra quy cách/giờ nhận. |
| photocopy_mobile/test/mau_don_tai_san_test.dart | Xem trước/back/chọn, chuyển trang, phóng to, retry, màn 320/390px với chữ 1,3x; header/token và PNG sai. |
| PhotocopyChain/verification/kiem_tra_mau_don.py | Script bảo trì: SHA/số trang/quyền in, kiểm tra script/file nhúng, render PNG/contact sheet. Không phải phần mềm quét malware toàn diện. |
| PhotocopyChain/verification/mau-don-tai-san.cjs | Smoke test trên Blazor thật, PDF/PNG thật; API fixture chặn ghi CSDL/thanh toán thật. |

## Kiểm chứng
- TDD: hai kiểm thử .NET mới ban đầu thất bại vì còn 3 mẫu tự dựng; kiểm thử mobile ban đầu thất bại vì chạm mẫu đã gửi POST trước khi xem. Sau triển khai đã qua.
- Bộ .NET: **397 đạt, 0 lỗi, 20 bỏ qua**. Các bài CSDL/bản đồ tích hợp cần biến môi trường/cho phép riêng chưa bật trong lượt này; không tính phần bỏ qua là đã nghiệm thu.
- Flutter: **110 đạt**; analyze không có issue. Các kiểm thử mẫu mới dùng HTTP fixture và ảnh PNG thực tế.
- Browser: **390/768/1366px**, tìm/lọc/rỗng, modal mở/đóng/ESC, bàn phím, ảnh giải mã hiển thị thật, chuyển trang, phóng/thu và chọn mẫu vào báo giá. Không lỗi JavaScript, không tràn ngang trang. API fixture, không tạo đơn hay gọi thu tiền thật.
- Kiểm tra PDF/ảnh: **18 mẫu/21 trang**, SHA/số trang đúng; đã render và xem hình. PDF Duy Tân cho đọc/in, khóa sửa: giữ nguyên quyền/file, không gỡ khóa.
- Build API/web không warning/error; APK debug đã build thành công.
- Publish API kiểm tra đủ 18 PDF, 21 PNG và catalog; NguonGoc không được đóng gói.
- API/web đã khởi động lại theo sự đồng ý của người dùng. Swagger có endpoint ảnh mới; endpoint mẫu trên API đang chạy trả 401 nếu không có đăng nhập.
- Cơ chế giữ adb reverse đã được bật lại sau restart; tại lúc kiểm tra không có điện thoại ADB đang kết nối.
- Chưa tạo FileIn/đặt đơn/thanh toán/in thử trên database thật trong lượt này; kiểm thử chọn mẫu web/app dùng fixture. Chưa nghiệm thu máy in vật lý.

## Cách thử
### Web
Mở [Web quản trị](http://localhost:5064), Ctrl+F5 nếu còn giao diện cũ. Vào Lập đơn tại quầy → chọn khách → dịch vụ In mẫu đơn trống → lọc Việc làm hoặc Học vụ → Xem mẫu → Chọn mẫu này để in → quy cách/báo giá/đặt đơn như trước.

### App
Chạy lại app từ mã mới để có màn xem trước (chỉ F5 web không cập nhật app đã cài). Có thể dùng launcher hiện có:

```cmd
cd /d C:\Users\USER\Desktop\DA_CN\CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD\MaNguon\scripts
chay_web.cmd app
```

Cần điện thoại được ADB nhận/cho phép gỡ lỗi. Launcher giữ kết nối cổng 5268. Vào Đặt in → In mẫu đơn trống → chạm mẫu → xem/chọn → chọn chi nhánh/quy cách và thanh toán theo luồng cũ.

APK: `MaNguon\photocopy_mobile\build\app\outputs\flutter-apk\app-debug.apk`. Build debug phục vụ thử, không phải bản release ký để phát hành.

### Chạy toàn bộ như cũ
Dùng `MaNguon\scripts\chay_web.cmd` để build/mở API, web, máy in ảo và Swagger. Không sửa launcher trong công việc thư viện mẫu này. Dừng giao dịch đang làm trước khi chạy lại vì launcher restart dịch vụ.

## Chưa kích hoạt / không thay đổi
- 10 DOC HUIT và 1 DOC UTT đã tải: bộ chuyển Office bị treo, không có PDF nghiệm thu; chỉ giữ bản nguồn, không phục vụ khách. Script chuyển thử bị lỗi đã được bỏ. Cần xuất PDF đúng từ nguồn rồi nghiệm thu trước khi bổ sung.
- Mẫu cũ có năm sinh “19…”/năm “201…” và tài liệu dài có kèm quy trình/danh sách được tách khỏi catalog; không sửa nội dung để giả thành mẫu hiện hành.
- Không thêm/sửa schema SQL, không xóa đơn/file khách cũ, không sửa giá, payOS, giao hàng hoặc logic hàng đợi.
- Kết nối máy in thật **để sau**, đúng yêu cầu. Máy in ảo hiện có giữ nguyên.

## Điều chỉnh UI bổ sung theo ảnh người dùng

- Web xem trước mẫu mở ở 150% (lớn hơn 50%); khung tối đa từ 1000px lên 1500px nhưng không vượt màn hình. Ảnh giữ đúng tỉ lệ, cuộn bên trong; nút phóng/thu theo bước 50%, chuyển trang vẫn mở ở 150%.
- Ba bảng Bảng giá / Chi nhánh / Máy in dùng class `bang-thao-tac-trai`: tiêu đề và nút Thao tác cùng căn trái. Không thay căn lề các bảng khác.
- Chỉ sửa `LapDonTaiQuay.razor`, CSS riêng, `BangGia.razor`, `QuanTri.razor`, `app-ui-v2.css` và version CSS trong `index.html`; không thay backend, SQL hoặc app mobile trong lượt điều chỉnh này.
- Hướng thiết kế: giữ Industrial / Utilitarian, Be Vietnam Pro/navy/tokens cũ, không thêm thư viện hay hiệu ứng. DFII (3+4+4+4)−3=12.
- Test preview đo kích thước đạt đúng 1,5 lần; kiểm tra ba bảng tại 390/768/1366/1920px, không tràn ngang. Script `verification/can-cot-thao-tac.cjs` kiểm tra UI thật với API fixture chỉ đọc. Test đã thất bại trước khi sửa và qua sau khi sửa.
- Build web: 0 warning/error; 12 kiểm thử hồi quy form quầy đạt. Người dùng Ctrl+F5 để nhận giao diện mới, không cần restart API.

### Xem mẫu toàn màn hình

Theo yêu cầu tiếp theo, khung giới hạn 1500px đã được đổi thành toàn bộ viewport trình duyệt (100vw × 100dvh). Thanh công cụ ở trên, phần tài liệu giãn đầy phần còn lại và cuộn riêng, giữ zoom 150% cùng màu/font cũ. Không dùng F11 hay sửa backend. Build web, 12 test hồi quy và kiểm tra browser 390/768/1366px đều qua; test đo khung phủ đúng viewport, chuyển trang/phóng to/chọn/đóng/ESC vẫn hoạt động.

