# Bổ sung quy cách in trên web và mobile — 12/10/2026

## Những gì đã làm

- Ép plastic vẫn chọn cùng đóng lò xo.
- Đóng lò xo và bấm kim góc loại trừ nhau, chọn cái sau bỏ cái trước và thông báo. Giữ quy tắc cũ lò xo/bìa kiếng; bìa kiếng + kim góc vẫn được.
- Bấm kim góc tối đa **50 tờ giấy thực tế mỗi file, mỗi bản**. Một mặt: 50 trang được, 51 không được. Hai mặt: 100 trang được, 101 không được. Số bản không nhân vào giới hạn.
- Đổi mặt in hoặc số trang làm vượt giới hạn: bỏ kim góc và báo rõ. SQL kiểm tra lại nên không thể vượt bằng cách gửi API trực tiếp.
- Thêm Giấy đứng/Giấy ngang. Mặc định đứng; không tăng giá. Lưu riêng từng dòng đơn, hiển thị trong hàng đợi/chi tiết mobile và truyền tới máy in ảo.
- In lại và đơn bổ sung kế thừa hướng giấy dòng gốc. Đơn bổ sung kiểm tra kim góc theo phạm vi trang và quy cách mới.
- Sửa thông báo lỗi dấu trong sp_TaoDonHang. Không sửa luồng thanh toán/QR hay phân quyền.

## Các file thay đổi trong đợt này

Các đường dẫn dưới đây tính từ thư mục dự án `CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD`.

| File | Thay đổi |
| --- | --- |
| Database/009-QuyCachIn_HuongGiay_GiaCong.sql | Migration thêm HuongGiay, CHECK/default; cập nhật sp_TinhTien và sp_TaoDonHang. |
| Database/01_TaoBang.sql | Đồng bộ cột và ràng buộc cho cài DB mới. |
| Database/04_StoredProcedure.sql | Đồng bộ thủ tục gốc; không đổi kiểu bảng TVP. |
| Database/Rollback/009-ThuTucTruocQuyCachIn.sql | Sao lưu định nghĩa thủ tục trước cập nhật, không tự chạy. |
| MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyTacIn.cs | Hàm tính tờ, hướng hợp lệ, loại trừ gia công dùng chung trên .NET. |
| MaNguon/PhotocopyChain/PhotocopyShared/Dtos/DatIn/DatInDtos.cs | Thêm HuongGiay vào yêu cầu từng file. |
| MaNguon/PhotocopyChain/PhotocopyShared/Dtos/DonHang/DonHangDtos.cs | Chi tiết đơn trả hướng giấy. |
| MaNguon/PhotocopyChain/PhotocopyShared/Dtos/MayInAo/MayInAoDtos.cs | Lệnh connector mang hướng giấy. |
| MaNguon/PhotocopyChain/PhotocopyApi/Controllers/BaoGiaController.cs | Chặn hướng không hợp lệ trước SQL. |
| MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs | Kiểm tra/truyền hướng khi tạo đơn web/mobile và kế thừa khi phát sinh. |
| MaNguon/PhotocopyChain/PhotocopyApi/Controllers/MayInAoController.cs | Đọc hướng đã lưu để dựng lệnh in. |
| MaNguon/PhotocopyChain/PhotocopyApi/DuLieu/TruyVanSql.cs | Hướng giấy trong chi tiết và mô tả hàng đợi. |
| MaNguon/PhotocopyChain/PhotocopyPrinterSimulator/MayInSimulator.cs | Kiểm tra hướng, ghi quy cách nhận được vào log máy ảo. |
| MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/LapDonTaiQuay.razor | Chọn hướng; loại trừ gia công; khóa/bỏ kim góc vượt giới hạn. |
| MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/HangDoiDon.razor | Hiển thị hướng giấy từng tài liệu. |
| MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/BanGiao.razor | Gia công bổ sung, giới hạn theo phạm vi; hiển thị hướng kế thừa. |
| MaNguon/photocopy_mobile/lib/quy_cach_in.dart | Quy tắc gia công và số tờ tương ứng bản .NET/SQL. |
| MaNguon/photocopy_mobile/lib/models.dart | Đọc/hiển thị hướng, tương thích dữ liệu cũ. |
| MaNguon/photocopy_mobile/lib/screens/dat_in/cau_hinh_in_screen.dart | Chọn hướng, kiểm tra kim góc; vô hiệu giá cũ khi đổi lựa chọn; vùng chạm tối thiểu 48dp cho gia công; sửa tràn chữ thẻ hỏa tốc. |
| MaNguon/photocopy_mobile/lib/screens/dat_in/chon_chi_nhanh_screen.dart | Giữ hướng trong báo giá và payload đặt đơn. |
| MaNguon/PhotocopyChain/PhotocopyApi.Tests/QuyCachInTests.cs | Quy tắc thuần, DTO mặc định, API chặn hướng sai. |
| MaNguon/PhotocopyChain/PhotocopyApi.Tests/QuyCachInCSDLTests.cs | Báo giá, lưu hướng/giá, JSON sai/thiếu/trùng; transaction rollback. |
| MaNguon/PhotocopyChain/PhotocopyApi.Tests/MayInSimulatorTests.cs | Máy ảo nhận DUNG/NGANG, từ chối hướng sai trước tải file. |
| MaNguon/photocopy_mobile/test/cau_hinh_in_test.dart | Thao tác gia công, payload hướng từ bước 2 đến tạo đơn, màn 320px/390px và chữ 130%. |
| MaNguon/photocopy_mobile/test/quy_cach_in_test.dart | Các biên số tờ, loại trừ và hiển thị hướng đơn cũ/mới. |

Không liệt kê các thay đổi QR, launcher và giá chi nhánh có từ đợt trước là thay đổi của đợt này.

## Database đã áp dụng

Đã chạy migration 009 vào `localhost / PhotocopyChain` bằng Windows Authentication, mã hóa UTF-8, QUOTED_IDENTIFIER ON. Đã dry-run và rollback trước, áp dụng thật sau; chạy lại migration không tạo thêm cột. Không xóa bảng/cột/dữ liệu. Số bản ghi đơn, chi tiết, thanh toán trước và sau đều là 17. Các dòng cũ mặc định DUNG; constraint đang bật và được SQL tin cậy.

sp_TaoDonHang nhận thêm tham số tùy chọn cuối `@HuongGiayJson`. Không gửi vẫn dùng DUNG; gửi thì phải đủ mỗi file đúng một hướng DUNG/NGANG. Hướng được lưu trong cùng transaction tạo đơn, không phải cập nhật rời sau đó.

Bản rollback chỉ khôi phục thủ tục cũ và giữ cột/dữ liệu hướng. Nếu dùng rollback phải chạy cùng API cũ không gửi tham số mới. Không tự ý chạy rollback hoặc xóa cột.

## Kiểm thử và phạm vi xác minh

- RED trước triển khai: quy tắc chưa có, mobile chưa gửi hướng, SQL chưa chặn gia công/lưu hướng; các bài kiểm thử đã phát hiện đúng thiếu chức năng.
- 142 bài kiểm thử .NET được chọn đã đạt, bao gồm 3 bài CSDL mới trên DB thật. Hai lớp CSDL cũ `CaVaPhieuNhapCSDLTests` và `PhamViCSDLTests` không nằm trong lượt này vì dùng kịch bản dữ liệu/phạm vi riêng.
- 36 bài Flutter đạt, gồm QR/OTP cũ và quy cách mới. Màn quy cách không tràn ở 320px, 390px và chữ 130% trên 390px.
- flutter analyze: No issues found. Đã bổ sung ngoặc khối cho hai chỗ if trong màn cấu hình để hết cảnh báo style cũ.
- dotnet build toàn solution đạt, 0 cảnh báo, 0 lỗi. Artifacts .NET nằm ở thư mục tạm để không ghi đè chương trình đang chạy.
- APK debug Android arm64 build thành công. Không tự cài lên điện thoại hoặc phát sinh thanh toán thật.
- File/đơn kiểm thử đều rollback; không còn FileIn tên KIEM_THU_QUY_CACH.pdf. SQL identity có thể có khoảng trống sau test rollback, đây là hành vi bình thường.
- Chưa chạy nghiệm thu web bằng trình duyệt với API mới hoặc in giấy trên máy vật lý. Máy in ảo xử lý metadata hướng và mô phỏng thời gian, không xoay/sửa nội dung PDF. Connector máy thật phải chuyển HuongGiay sang thiết lập orientation của Windows Print Queue/IPP.
- Danh mục hiện nhận diện hai dịch vụ bằng tên chuẩn “Đóng lò xo” và “Bấm kim góc”, không theo ID. Nếu đổi tên hai dịch vụ này phải cập nhật đồng bộ quy tắc .NET/Dart/SQL; chưa thêm cột mã loại gia công ngoài schema đã duyệt.

## Cách chạy và thử

1. Dừng bản web/API/máy in ảo đang chạy rồi mở lại `MaNguon/scripts/chay_web.cmd` để launcher build mã mới. F5 trang web.
2. Android Studio: Stop rồi Run lại app, không chỉ hot reload bản đang dùng mã cũ. Nếu mất kết nối backend sau nối lại điện thoại, chạy lại launcher chế độ app/adb reverse như trước.
3. Web Lập đơn tại quầy hoặc mobile Cấu hình in: chọn Giấy ngang, kiểm tra bước tiếp theo/chi tiết đơn vẫn hiện ngang.
4. Thử plastic + lò xo, sau đó chọn kim góc: plastic giữ nguyên, lò xo bỏ chọn. Thử chọn lò xo lại: kim góc bỏ chọn.
5. File 100 trang: hai mặt chọn kim góc được; đổi một mặt kim góc bị bỏ. File 101 trang hai mặt không chọn được. Tăng số bản không làm thay đổi giới hạn mỗi bản.
6. Khi xử lý đơn ngang, log máy in ảo hiển thị Giấy ngang; đơn in lại/bổ sung vẫn giữ hướng dòng gốc.
