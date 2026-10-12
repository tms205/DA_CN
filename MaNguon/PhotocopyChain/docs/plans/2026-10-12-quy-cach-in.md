# Kế hoạch bổ sung quy cách in

## Yêu cầu đã chốt

- Ép plastic được chọn cùng lò xo. Giữ quy tắc cũ: lò xo và bìa kiếng loại trừ nhau.
- Lò xo và bấm kim góc loại trừ nhau.
- Kim góc tối đa 50 tờ giấy thực tế **mỗi file, mỗi bản**. Hai mặt: số tờ = số trang / 2 làm tròn lên; không nhân số bản.
- Giấy đứng/ngang dùng mã DUNG/NGANG, mặc định DUNG, không thay đổi giá.
- Áp dụng web, mobile, API/SQL, chi tiết đơn và lệnh máy in; in lại/đơn phát sinh giữ hướng của dòng gốc.
- Người dùng đã đồng ý migration thêm HuongGiay vào ChiTietDonHang. Không xóa dữ liệu hay sửa cấu trúc khác.

## Thiết kế tối giản

Giữ kiến trúc hiện tại, không thêm thư viện. Dùng tên dịch vụ hiện có để nhận diện lò xo/kim góc, không hard-code ID. Giữ nhóm DONG_GAY hiện tại; không chuyển kim góc vào nhóm này vì sẽ vô tình cấm bìa kiếng + kim góc.

Không đổi kiểu bảng SQL đang dùng. Thêm tham số JSON tùy chọn cuối sp_TaoDonHang để lưu hướng theo MaFile trong cùng transaction. Client cũ không gửi tham số vẫn dùng DUNG. sp_TinhTien kiểm tra gia công cho cả báo giá, tạo đơn và đơn phát sinh.

## Điểm kiểm tra mobile

Flutter/Android, giữ luồng chọn file → quy cách → chi nhánh → thanh toán. Hai lựa chọn hướng giấy trong form hiện tại; vùng chạm tối thiểu 48dp, thông báo lý do khi kim góc không hợp lệ. Đổi mặt in phải xét lại kim góc trước báo giá. API/SQL kiểm tra lại, không tin số trang client. Kiểm thử màn 320/390px và chữ phóng to. Rủi ro vừa phải: tác động cấu hình, không thay đổi thanh toán hay điều hướng QR.

## Các bước và nghiệm thu

1. Viết kiểm thử quy tắc ở PhotocopyApi.Tests/QuyCachInTests.cs; chạy RED trước khi thêm helper shared.
2. Thêm helper quy cách, HuongGiay trong DTO yêu cầu/chi tiết/lệnh in. Kiểm tra hướng hợp lệ ở API.
3. Sửa LapDonTaiQuay.razor và HangDoiDon.razor; thêm lựa chọn và cảnh báo gia công. Mobile thêm helper, lựa chọn, truyền hướng qua bước chi nhánh, hiển thị chi tiết; widget tests kiểm tra payload và thao tác.
4. Tạo Database/009-QuyCachIn_HuongGiay_GiaCong.sql; đồng bộ 01_TaoBang.sql và 04_StoredProcedure.sql. Sao lưu định nghĩa thủ tục cũ; áp dụng transaction, kiểm tra khả năng chạy lại. Kiểm thử SQL có transaction rollback, không giữ đơn thử.
5. Bổ sung truy vấn chi tiết/lệnh in và giữ hướng ở đơn phát sinh. Chạy dotnet test/build vào artifacts tạm, flutter test/analyze/build apk.
6. Ghi kết quả, file thay đổi và cách thử trong docs/QuyCachIn_2026-10-12.md. Không coi simulator là bằng chứng đã in giấy vật lý.

## Trường hợp biên

50/51 trang một mặt; 100/101 trang hai mặt; 10 bản vẫn tính giới hạn từng bản; plastic + lò xo được; lò xo + kim góc bị từ chối; bìa kiếng + kim góc vẫn được; thay hai mặt sang một mặt bỏ kim góc không hợp lệ và báo rõ; hướng mặc định/NGANG lưu đúng; giá không đổi theo hướng; in lại giữ hướng, giới hạn theo phạm vi trang in.
