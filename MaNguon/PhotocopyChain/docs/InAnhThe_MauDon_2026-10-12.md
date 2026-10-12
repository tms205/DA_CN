# Bổ sung in ảnh thẻ và mẫu đơn trống — 12/10/2026

## Đã triển khai

- Web lập đơn tại quầy và app khách hàng có ba lựa chọn: in tài liệu, in mẫu đơn trống, in ảnh thẻ.
- Ba mẫu tham khảo: đơn xin việc, đơn xin nghỉ, đơn đề nghị. Mẫu để in và viết tay; không tự điền thông tin khách. Web có nút xem trước PDF.
- Ảnh thẻ nhận JPG/JPEG/PNG, năm kích thước, 1–100 ảnh cho mỗi ảnh gốc. Mỗi file có thể là một người khác nhau.
- Hệ thống căn ảnh vừa khung, giữ tỉ lệ, thêm nền trắng nếu khác tỉ lệ; không tự cắt mặt, đổi nền hay chỉnh chân dung.
- Ghép đúng số ảnh vào PDF A4, có đường cắt. Tiền tính theo số ảnh; giấy, thời gian và máy in tính theo số trang PDF thực tế. Không nhân thêm số bản PDF hoặc chọn gia công tài liệu cho ảnh thẻ.
- Giá theo từng chi nhánh; giá và số ảnh trong đơn đã lập được giữ nguyên khi sửa giá bán.
- Hàng đợi và tiến độ mobile hiển thị thông tin ảnh. Phục hồi file phải giữ đúng kích thước/số ảnh. In lại tại quầy và mua thêm dùng toàn bộ bộ ảnh đã chốt; đổi số lượng/kích thước thì lập đơn mới.
- Không thay đổi cơ chế xác nhận thanh toán payOS, quyền chi nhánh hoặc giao tiếp máy in hiện có. Chưa thêm thư viện mới; giữ cách tổ chức code hiện tại.

## Giá khởi tạo, đồng/ảnh

| Kích thước | Giá đề xuất |
| --- | ---: |
| 2 × 3 cm | 3.000 |
| 3 × 4 cm | 5.000 |
| 4 × 6 cm | 10.000 |
| 3,5 × 4,5 cm | 7.000 |
| 5 × 5 cm | 10.000 |

Giá 3×4 và 4×6 được suy ra từ gói 40.000đ/8 ảnh và 40.000đ/4 ảnh của [Ruaanh](https://ruaanh.com/in-anh-the-online/), nơi có kèm chỉnh ảnh; đây không phải báo giá bán lẻ từng ảnh của họ. Ba mức còn lại là giá đề xuất cho đồ án, chưa phải giá thị trường đã xác minh. Có đối chiếu [MEU Print](https://www.meuprint.shop/bang-gia/anh-id), nhưng bên đó dùng giấy Couche nên không dùng trực tiếp cho giấy ảnh. Quản trị sửa giá thực tế tại Bảng giá theo chi nhánh.

## Database

Đã chạy [010-DichVuAnhThe_MauDon.sql](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/010-DichVuAnhThe_MauDon.sql) trên database đang dùng, và chạy lại để kiểm tra không seed trùng/ghi đè giá.

- Thêm `DichVu.KichThuocAnh`; `FileIn.KichThuocAnh/SoLuongAnh`; `ChiTietDonHang.KichThuocAnh/SoLuongAnh` để lưu số ảnh đã chốt. Không tạo bảng mới, không xóa dữ liệu cũ.
- Thêm ràng buộc kích thước, số lượng và quy cách ảnh; dịch vụ vẫn là `IN_AN`, giấy ảnh A4, màu, một mặt.
- Cập nhật `sp_TinhTien` và `sp_TaoDonHang`; ảnh tính theo số ảnh, tài liệu vẫn tính theo trang/bản. Giữ nguyên kiểu bảng tham số SQL cũ.
- Seed năm dịch vụ, giá cho hai chi nhánh hiện có và vật tư Giấy ảnh A4. Tồn ban đầu **0**, không tự cộng kho giả. Giá vốn vật tư khởi tạo 0 là chỗ chờ cấu hình, không có nghĩa giấy ảnh miễn phí; cần cập nhật giá vốn thực tế trước khi dùng báo cáo lợi nhuận.
- SQL gốc đã đồng bộ: `01_TaoBang.sql`, `02_DuLieuMau.sql`, `04_StoredProcedure.sql`. Không chạy lại SQL tạo database trên database hiện có; chỉ dùng migration.
- Bản sao hai thủ tục trước thay đổi nằm trong `Database/Rollback/010-ThuTuc_TruocInAnhThe.sql`. Đây **không phải** bản backup đầy đủ database hay rollback toàn tính năng; không chạy khi đang dùng đơn ảnh thẻ.

Các kiểm thử ghi database đều chạy trong transaction rồi rollback; không gửi thanh toán thật. Số đơn hiện có vẫn là 17 sau kiểm thử. Identity có thể nhảy số do tạo rồi rollback — không có đơn thử được giữ lại.

## Cách chạy và thử

1. Chạy lại [chay_web.cmd](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/scripts/chay_web.cmd) để launcher build và nạp API/Web mới. Không dùng cửa sổ API/Web cũ đang chạy; sau khi mở web nhấn Ctrl+F5 nếu còn cache.
2. Vào Kho vật tư, tạo phiếu nhập **Giấy ảnh A4**, duyệt và xác nhận nhận hàng theo luồng hiện có. Kiểm tra giá vốn. Chọn chi nhánh có máy màu A4 đang kết nối; máy đen trắng không đáp ứng ảnh thẻ.
3. Web: Lập đơn tại quầy → chọn khách → phần tải file chọn **In ảnh thẻ** → kích thước và số ảnh → tải JPG/PNG → kiểm tra giá theo ảnh → thanh toán như đơn thông thường.
4. Mẫu đơn: cùng phần tải file chọn **In mẫu đơn trống**, xem/chọn mẫu rồi chọn quy cách in tài liệu thông thường. Giá tính theo trang và số bản, không có phụ phí mẫu tự tạo.
5. Mobile: chạy lại app để nạp code mới (hot restart/rebuild, không chỉ mở lại APK cũ). Tab Đặt in có cùng ba lựa chọn. Sau tải ảnh/chọn mẫu, chọn chi nhánh và thanh toán QR theo luồng hiện có.
6. Nếu dùng launcher cho điện thoại: tại thư mục scripts chạy `chay_web.cmd app` sau khi web/API đã mở và điện thoại đã được adb nhận. APK debug mới cũng có ở `MaNguon/photocopy_mobile/build/app/outputs/flutter-apk/app-debug.apk`.

Ví dụ: 8 ảnh 3×4, giá 5.000đ/ảnh → tiền in 40.000đ, không phải 5.000đ vì PDF chỉ một trang. Nếu nhiều ảnh cần nhiều tờ thì máy in và kho xử lý số tờ PDF, không coi mỗi ảnh là một tờ A4.

## Các nhóm file thay đổi

- API: `FileInController` (upload, mẫu), `DanhMucController` (giá), `QuanTriController` (dịch vụ), `DonHangController` (phục hồi/phát sinh), `TruyVanSql` (chi tiết). PDF ở [AnhThePdf.cs](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DichVu/AnhThePdf.cs) và [MauDonPdf.cs](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DichVu/MauDonPdf.cs).
- Shared: danh mục kích thước `QuyCachAnhThe`; DTO FileIn/DichVu/DongInChiTiet/MauDon và kiểm tra LuuDichVuRequest.
- Web: [LapDonTaiQuay.razor](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/LapDonTaiQuay.razor), `BangGia.razor`, `HangDoiDon.razor`, `BanGiao.razor`.
- Flutter: `api.dart`, `models.dart`, `quy_cach_anh_the.dart`, các màn chọn file/quy cách/chi nhánh và tiến độ đơn.
- Kiểm thử: `AnhTheTests.cs`, `AnhTheCSDLTests.cs`, `KiemTraHttpNhapTests.cs`, `LapDonTaiQuayWebTests.cs`, `dich_vu_anh_the_test.dart` và `Database/KiemThu/010-InAnhThe.sql`.

## Kiểm chứng

- Build toàn solution .NET thành công, 0 warning / 0 error. Build sang thư mục artifacts tạm để không đụng file đang được API/Web cũ sử dụng; launcher sẽ build vào vị trí chạy bình thường khi khởi động lại.
- Bộ .NET: 250 test qua; 15 test database skip trong lượt không cấu hình database. Đã chạy riêng 5 test database (2 ảnh thẻ + 3 quy cách cũ) với database thật: cả 5 qua. Mười test database còn lại không chạy lại trong đợt này.
- SQL test báo giá 8 ảnh, tạo đơn, snapshot số ảnh và số tờ: qua. Giá chi nhánh khác/giá đơn cũ giữ nguyên; sai kích thước, số bản, phạm vi và gia công bị chặn.
- PDF: kiểm tra năm kích thước, số ảnh, biên bố trí, khổ A4; render và xem ba mẫu đơn, chữ Việt đọc được và có chỗ viết tay.
- Flutter analyze không có lỗi; 90 test qua, gồm ảnh thẻ ở 320px và 390px, QR, OTP, quy cách và chọn chi nhánh. APK debug build thành công.
- Chưa nghiệm thu luồng ảnh mới trên điện thoại vật lý/máy in vật lý, chưa chạy thanh toán tiền thật cho đơn ảnh trong đợt này. Widget/API/SQL test không thay thế nghiệm thu thiết bị thật.

## Giới hạn cần biết

- PDF/ảnh dùng các thư viện đã có, triển khai hiện tại dành cho API Windows; mẫu PDF dùng font Arial trên máy chủ. Chưa hỗ trợ Linux.
- Ảnh tối đa 20 megapixel, 100 ảnh/file; giới hạn dung lượng upload hiện có giữ nguyên. Không tăng chất lượng thực nếu ảnh gốc mờ.
- Mẫu đơn là mẫu tham khảo tự soạn, không cam kết đúng biểu mẫu của cơ quan nhận hồ sơ.
- Kích thước đúng không đồng nghĩa ảnh hợp lệ cho visa/hộ chiếu. Khách phải kiểm tra yêu cầu nơi nhận; hệ thống không kiểm tra nền, tỉ lệ đầu/khuôn mặt hay điều kiện hồ sơ.
- Khi dùng máy thật phải chọn thiết bị hỗ trợ loại/định lượng giấy ảnh tương ứng và in **100% / Actual size**, không Fit to page. Khả năng in màu A4 trong phần mềm không bảo đảm máy vật lý in được mọi loại giấy ảnh. Máy ảo mô phỏng số trang/thời gian, không kiểm chứng chất lượng màu hoặc cắt ảnh thực tế.
