# Bìa kiếng + lò xo và bố cục lập đơn tại quầy – 13/10/2026

## Đã sửa

- Cho phép **Đóng bìa kiếng + Đóng lò xo** cùng một file/bản in trên Web và app; không tự bỏ bìa kiếng khi chọn lò xo nữa.
- Giữ loại trừ lò xo + bấm kim góc, giới hạn kim góc 50 tờ giấy thực tế/bản, ép plastic vẫn đi cùng lò xo. Các dịch vụ cùng nhóm khác vẫn loại trừ như trước.
- Thu gọn ô thanh toán/mã khuyến mãi khoảng một nửa độ rộng cũ trên desktop; đưa tốc độ xử lý xuống cùng hàng. Tablet/điện thoại tự xuống hàng để không tràn ngang; giữ màu/font hiện có.
- Rút nhãn hỏa tốc để thấy đủ phụ phí trong ô gọn, phần giờ nhận vẫn ghi rõ ưu tiên xử lý.
- Sửa nội dung mô tả kết hợp gia công trên bảng giá/danh mục dịch vụ.

## SQL

Đã chạy `Database/012-GiaCong_BiaKieng_LoXo.sql` theo xác nhận: chỉ sửa thủ tục `sp_TinhTien`, không thêm/xóa bảng/cột, không thay giá hoặc đơn cũ. Đã kiểm tra định nghĩa thủ tục đang dùng khớp bản SQL gốc trước khi áp dụng. Có bản khôi phục tại `Database/Rollback/012-ThuTucTruocBiaKiengLoXo.sql` (không chạy trừ khi muốn quay lại quy tắc cũ).

`Database/04_StoredProcedure.sql` đã sửa cho cài đặt mới; `02_DuLieuMau.sql` chỉ cập nhật chú thích. Không sửa lại các migration lịch sử đã chạy.

## File code chính

Tính từ `MaNguon/PhotocopyChain`:

- `PhotocopyShared/QuyCachIn/QuyTacIn.cs`: ngoại lệ kết hợp đúng cặp bìa kiếng/lò xo.
- `PhotocopyApi/Controllers/DonHangController.cs`: luồng in lại dùng quy tắc chung thay kiểm nhóm gộp.
- `PhotocopyAdmin/Pages/LapDonTaiQuay.razor`: sắp xếp các ô, nhãn và hướng dẫn gia công.
- `PhotocopyAdmin/wwwroot/css/app-ui-v2.css`, `wwwroot/index.html`: grid responsive và cập nhật cache CSS.
- `PhotocopyAdmin/Pages/BangGia.razor`, `Pages/DichVu.razor`: mô tả quy tắc mới.
- `../photocopy_mobile/lib/quy_cach_in.dart`, `lib/screens/dat_in/cau_hinh_in_screen.dart`: quy tắc và hướng dẫn đồng nhất trên app.
- Tests: `QuyCachInTests.cs`, `QuyCachInCSDLTests.cs`, `LapDonTaiQuayWebTests.cs`, mobile `test/quy_cach_in_test.dart`; trình duyệt `verification/quy-cach-tai-quay.cjs` tích hợp trong harness dashboard.

## Kiểm chứng

- Test-first đã tái hiện lỗi loại trừ cặp bìa/lò xo và tốc độ nằm sai hàng trước sửa.
- .NET toàn solution: 352 passed, 17 skipped, 0 failed (test CSDL opt-in cũ không tự bật).
- Riêng báo giá trên database thật: passed; tổng tiền gia công cặp bìa/lò xo bằng tổng tiền hai gia công riêng; các ràng buộc kim góc còn hiệu lực. Chỉ báo giá, không tạo đơn/file/thu tiền thật.
- Flutter: 98 tests passed; analyze không có lỗi/cảnh báo; APK debug đã build.
- Trình duyệt UI Blazor thật với API fixture: giữ cả hai checkbox và gửi đủ hai mã gia công; kiểm loại trừ kim góc, đổi hỏa tốc và layout không tràn ở 390/768/1366/1920px. Không gọi API ghi dữ liệu thật.
- Web build/publish thành công; API build kiểm thử ở thư mục tạm để không dừng tiến trình đang phục vụ giao dịch.

## Chạy/test lại

Khi không có đơn đang thanh toán/in, chạy lại `MaNguon/scripts/chay_web.cmd` để nạp API mới, rồi Ctrl+F5 Web. Nếu tiến trình API cũ chưa được thay, dừng cửa sổ API bằng Ctrl+C rồi chạy launcher lại. App: dừng và chạy Flutter lại/cài APK mới.

Chọn bìa kiếng + lò xo → cả hai còn được chọn → Tính lại giá phải có tiền cả hai. Chọn kim góc → lò xo bị bỏ, bìa kiếng vẫn giữ.

Phần chia ca 07:30–21:30 chưa triển khai; đang chờ xác nhận nhập toàn bộ tiền két (gồm 200.000đ đầu ca) hay chỉ COD.
