# Lập đơn tại quầy: tên/SĐT và giờ nhận dự kiến

## Đã thay đổi

- Chỉ cần **họ tên hoặc số điện thoại**, không bắt buộc cả hai; không chấp nhận bỏ trống/whitespace cả hai. Tên được nhập vẫn cần ít nhất 2, tối đa 100 ký tự; SĐT được nhập vẫn cần 10 số ASCII bắt đầu bằng 0.
- Có SĐT: tìm khách đã có theo SĐT, giữ kiểm tra tài khoản khóa. Chưa có và chỉ nhập số: tạo khách với tên `Khách tại quầy`.
- Chỉ tên: tạo khách vãng lai riêng, lưu SĐT NULL và email nội bộ có GUID duy nhất. Không gộp khách trùng tên, không tạo SĐT giả; khách không có SĐT vẫn có mã khách để giữ khóa ngoại của file/đơn.
- Trang web có lựa chọn rõ **In thường** / **In hỏa tốc**. Khi đổi lựa chọn, tự báo giá lại và hiện **Giờ nhận dự kiến: HH:mm dd/MM/yyyy**. Giữ phụ phí hỏa tốc 10.000đ hiện có.
- Giờ nhận lấy trực tiếp từ `BaoGiaResponse.NgayHenTra`, do `sp_BaoGia` và `fn_UocTinhGioXong` hiện có trả về. Không tự cộng mốc 15/30 phút, không thay cách lập lịch/phân máy hoặc chuyển trạng thái.
- Đang tính báo giá: hiện trạng thái chờ, bỏ giá/giờ cũ. Mất mạng/API báo lỗi: không giữ báo giá cũ; không có giờ: ghi rõ chưa ước tính được. Thay quy cách/số bản/mã khuyến mãi/gia công sẽ bỏ báo giá cũ; kết quả HTTP lỗi thời không được ghi đè quy cách mới.
- Chặn gửi lặp và tải/xóa file trong lúc đang xử lý; không cho lập đơn khi chưa có báo giá hợp lệ hoặc đang upload. Thanh toán và cơ chế phân máy giữ nguyên.

## File thao tác

Các đường dẫn dưới đây tính từ `MaNguon/PhotocopyChain`:

| File | Thay đổi |
|---|---|
| `PhotocopyShared/Dtos/Tuan3/Tuan3Dtos.cs` (dòng 9–14) | Hai trường đầu vào nullable; bỏ Required riêng từng ô. |
| `PhotocopyShared/Dtos/Tuan3/KiemTraQuanTri.cs` (dòng 1–21) | Validation tên **hoặc** SĐT và định dạng nếu được nhập. |
| `PhotocopyApi/Controllers/DonHangController.cs` (using ở dòng 24, hàm `ChonKhachTaiQuay` dòng 277–324) | Dùng validation chung, tìm theo SĐT khi có, tạo khách chỉ tên/SĐT an toàn. |
| `PhotocopyAdmin/Pages/LapDonTaiQuay.razor` | Form khách dòng 46–62; khóa upload/xóa dòng 73–80; lựa chọn/giờ nhận dòng 127–168; nút gửi dòng 213–218; trạng thái kiểu in/báo giá dòng 237–254; kiểm tra form/file/gia công và bỏ giá cũ dòng 343–456; báo giá/lập đơn dòng 473–536. |
| `PhotocopyApi.Tests/LapDonTaiQuayTests.cs` | Validation và controller trước truy cập DB. |
| `PhotocopyApi.Tests/LapDonTaiQuayWebTests.cs` | Component web thực với HTTP giả; lấy tiền/giờ đúng kiểu in, bỏ giá lỗi/cũ, render HTML và tùy chọn đang chọn, trường khách không Required riêng. |
| `PhotocopyApi.Tests/KiemTraHttpNhapTests.cs` | Bổ sung payload rỗng/whitespace cả hai và SĐT sai; MVC phải chặn trước truy cập DB. |
| `docs/plans/2026-10-12-lap-don-tai-quay-ten-sdt-gio-nhan.md` | Yêu cầu được xác nhận, quyết định và kế hoạch kiểm thử trước khi sửa. |

Các số dòng là bản mã tại lúc bàn giao; những lần sửa tiếp theo có thể làm thay đổi vị trí.

## Kiểm chứng và giới hạn

- Viết/chạy kiểm thử trước khi sửa: đã quan sát lỗi bắt buộc cả hai trường, không có giờ hiển thị, giá cũ còn giữ sau lỗi và response cũ ghi đè quy cách.
- Bản cuối: **230 kiểm thử .NET đạt, 13 bỏ qua, 0 lỗi**. Các bài bỏ qua là tích hợp DB cũ vì chưa có `PHOTOCOPY_TEST_DB`; không coi chúng là kiểm chứng trên DB thật.
- Build `PhotocopyChain.sln`: thành công, **0 warning / 0 error**. Build/test đặt artifacts ở `C:/Users/USER/AppData/Local/Temp/PhotocopyQuayBuild-20261012`, không ghi đè binary dịch vụ đang chạy.
- Không thay schema hoặc chạy migration; không tạo khách/đơn thử vào database thật, không gửi giao dịch/Gmail. Các bài web dùng logic component/DTO thực và HTTP giả ở biên, có render HTML bằng HtmlRenderer; chưa kiểm thử tương tác end-to-end trên trình duyệt với database thật trong lượt này.
- Giờ hiển thị là **ước tính**, không phải cam kết nhận hàng đúng phút. Công thức SQL hiện tại tính số trang chờ và tốc độ máy nhanh nhất; hỏa tốc được ưu tiên. Độ chính xác phụ thuộc tình trạng hàng đợi/máy thực tế, không phải một bộ lập lịch mới xét đủ mọi máy, sự cố và thời gian gia công/giao hàng.
- Không sửa mobile hoặc các thay đổi quên mật khẩu có sẵn trong worktree.

## Chạy và thử

1. Chạy lại `MaNguon/scripts/chay_web.cmd` để launcher build/nạp API và web mới; F5 hoặc Ctrl+F5 trình duyệt. Không chỉ F5 trên server đang chạy binary cũ.
2. Mở **Lập đơn tại quầy**; thử chỉ tên, chỉ SĐT, và bỏ trống cả hai. Trường hợp cuối phải báo lỗi, không tạo khách.
3. Chọn khách, tải file, chọn **In thường** rồi **In hỏa tốc**: tiền và giờ nhận được cập nhật theo báo giá API. Không cần tạo đơn hay thanh toán chỉ để xem giờ.
4. Thay số bản/quy cách, kiểm tra giờ cũ biến mất và bấm **Tính lại giá** để cập nhật. Nếu API lỗi, không được dùng giá/giờ trước đó để lập đơn.
5. Hai kiểu in có thể cùng giờ khi hàng đợi trống hoặc làm tròn về cùng phút; không cộng chậm giả cho in thường chỉ để tạo khác biệt.

## Bổ sung bố cục thanh toán và khuyến mãi

- `PhotocopyAdmin/Pages/LapDonTaiQuay.razor`: chuyển mã khuyến mãi xuống hàng riêng cùng thanh toán, sau các quy cách in. Thanh toán bên trái, mã khuyến mãi bên phải; giữ nguyên binding, lựa chọn QR/COD và thao tác bỏ báo giá khi đổi mã.
- `PhotocopyAdmin/wwwroot/css/app-ui-v2.css`: CSS riêng cho hàng mới; ô thanh toán rộng hơn, hai ô tự xuống hàng nếu thiếu chỗ. Giữ màu và cỡ chữ; điều chỉnh padding ô thanh toán để đọc đủ cả hai lựa chọn trên màn hình nhỏ.
- `PhotocopyAdmin/wwwroot/index.html`: đổi phiên bản URL stylesheet để trình duyệt tải CSS mới, tránh giữ bản cũ trong cache.
- `PhotocopyApi.Tests/LapDonTaiQuayWebTests.cs`: kiểm tra HTML thực có hai trường ở cùng nhóm riêng, không còn nằm trong nhóm quy cách, có nhãn liên kết đúng và đủ tên phương thức thanh toán. Đã thấy kiểm thử thất bại trước khi sửa và đạt sau khi sửa; toàn bộ .NET vẫn 230 đạt, 13 bài DB bỏ qua.
- Kiểm tra Chrome headless riêng phần markup thanh toán/khuyến mãi lấy từ trang Razor cùng CSS hiện tại ở 320, 390, 768, 1024, 1366 và 1440px: cả hai tên phương thức đủ chỗ, không tràn ngang, cùng hàng khi đủ rộng và xếp dọc khi hẹp; nhập mã và đổi lựa chọn được. Đây là kiểm tra bố cục cô lập, không phải end-to-end trên trang có dữ liệu thật. Script và ảnh kiểm tra ở `C:/Users/USER/AppData/Local/Temp/PhotocopyQuayBuild-20261012`.
- Không sửa API, database, logic thanh toán hoặc mobile trong lần chỉnh bố cục này.
