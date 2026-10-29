# Task 4 Thanh toán VietQR

Phạm vi: HeThong/ThanhToanVietQR.png. Đối chiếu source ngày 26/10/2026. Gồm 0 đặc tả nghiệp vụ và 6 đặc tả hệ thống.

- Ranh giới nên là Hệ thống PhotocopyChain hoặc phân hệ thanh toán của PhotocopyChain. payOS nằm ngoài ranh giới; ngân hàng thực hiện chuyển tiền ở nghiệp vụ.
- Bộ hẹn giờ là cơ chế bên trong ứng dụng. Nếu biểu diễn actor đồng hồ để chỉ kích hoạt thời gian thì chú thích đây là mô hình hóa khái niệm, không phải một dịch vụ ngoài thực sự.
- Không dùng include/extend chỉ để nối trước–sau. Xếp in sau webhook là phản ứng nghiệp vụ có điều kiện; sơ đồ Activity/Sequence phù hợp hơn để thể hiện giao dịch tiền và ngoại lệ.
- Return/cancel URL chỉ điều hướng/hiển thị. Web có thể quay về hàng đợi; App hiển thị kết quả khi API xác nhận, không cam kết một deep link tự mở App từ mọi trình duyệt.

## HT-4.4-01 Hiển thị và sử dụng mã VietQR thanh toán

**Use case**
Hiển thị và sử dụng mã VietQR thanh toán

**Use case ID**
HT-4.4-01

**Tác nhân chính**
Khách hàng

**Tổng quan**
Xem QR đúng đơn trong App và chuyển khoản bằng ứng dụng ngân hàng; phần mềm theo dõi kết quả xác thực.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Dùng HT-4.4-02 để tạo/lấy dữ liệu QR. Nhận tiền là HT-4.4-03 hoặc HT-4.4-05; không do thao tác quét tự xác nhận.

**Dòng sự kiện chính**
1. App mở Thanh toán, GET api/thanh-toan/{id} để lấy trạng thái và hạn.
2. App POST api/thanh-toan/{id}/payos; kiểm mã đơn, số tiền và dữ liệu QR trả về.
3. App vẽ QR trong màn hình, hiển thị số tiền, ngân hàng/nội dung và đếm ngược; khách dùng ngân hàng quét/chuyển.
4. App nghe sự kiện SignalR và hỏi lại API khoảng 5 giây khi màn đang hoạt động; khi trở lại App hỏi ngay.
5. Chỉ khi API cho biết đã trả, App hiển thị kết quả và cho xem tiến độ đơn.

**Dòng sự kiện phụ**
Bước 2 dữ liệu QR không khớp hoặc không hợp lệ: không vẽ QR đó, báo lỗi và thử lại.
Link cũ không còn cache QR: có thể dùng checkout payOS hợp lệ; không tự dựng số tiền/nội dung khác.
Khách chọn mở checkout ngoài: chỉ chấp nhận HTTPS pay.payos.vn; quay lại App phải đối soát.
Đóng/chuyển nền màn hình: không tiếp tục polling như lúc hoạt động; resume kiểm tra lại.
Hết hạn/đã hủy: không cho tạo hoặc tiếp tục trả trên UI; nếu tiền vẫn tới sau hủy thì xử lý 4.11.
Thông báo trên trang quay về hoặc ảnh chụp ngân hàng không phải căn cứ API tự đổi trạng thái.

**Tiền điều kiện**
Đơn VietQR thuộc khách, chưa hủy, chưa trả và còn hạn thanh toán.

**Hậu điều kiện**
Giao diện phản ánh trạng thái trả về máy chủ; chưa xác thực tiền thì vẫn chưa trả. Quét QR đơn thuần không đổi CSDL.

**Căn cứ** PAYMENT, PAY_UI

## HT-4.4-02 Tạo hoặc lấy lại liên kết thanh toán payOS

**Use case**
Tạo hoặc lấy lại liên kết thanh toán payOS

**Use case ID**
HT-4.4-02

**Tác nhân chính**
Khách hàng hoặc nhân sự có quyền với đơn; payOS hỗ trợ

**Tổng quan**
Cấp QR/checkout cho đúng orderCode, tổng tiền và thời hạn của đơn.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Được gọi từ HT-4.4-01; không tạo lại DonHang và không tự xác nhận nhận tiền.

**Dòng sự kiện chính**
1. Giao diện POST api/thanh-toan/{id}/payos.
2. API kiểm quyền chủ đơn/phạm vi và điều kiện thanh toán.
3. API hỏi payOS bằng mã đơn; nếu có link còn dùng và tiền khớp thì lấy lại link và QR cache nếu còn.
4. Nếu chưa có link, API tạo orderCode từ mã đơn, amount từ CSDL, mô tả chuẩn, return/cancel URL theo kênh và ExpiredAt theo hạn.
5. API trả QR cùng metadata, checkout URL và hạn; cố lưu cache QR cho lần mở lại.

**Dòng sự kiện phụ**
Bước 2 sai quyền, COD, đã trả, đã hủy hoặc hết hạn: từ chối cấp link.
Bước 3 link có amount khác CSDL hoặc trạng thái cancelled/expired/failed: báo lỗi, không thay bằng QR tùy ý.
Bước 4 thiếu địa chỉ HTTPS công khai hoặc WebAdmin URL hợp lệ: báo cấu hình chưa sẵn sàng.
payOS lỗi: báo chưa kết nối được, cho thử lại.
Ghi cache QR lỗi: log cảnh báo, vẫn trả QR vừa nhận; không giả báo thanh toán thành công.

**Tiền điều kiện**
Phiên hợp lệ, đơn trong phạm vi, phương thức VietQR, chưa trả, chưa hủy và còn hạn; có cấu hình payOS/HTTPS công khai.

**Hậu điều kiện**
Có dữ liệu QR/link đúng đơn nếu thành công; tạo link không làm ThanhToan thành DA_THANH_TOAN.

**Căn cứ** PAYMENT, PAY_RULE, QR_STORE

## HT-4.4-03 Nhận webhook xác nhận thanh toán

**Use case**
Nhận webhook xác nhận thanh toán

**Use case ID**
HT-4.4-03

**Tác nhân chính**
Cổng payOS

**Tổng quan**
Tiếp nhận thông báo có chữ ký và chỉ ghi nhận khoản tiền đáp ứng đủ đối chiếu.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Sau ghi nhận có thể gọi HT-4.4-04 cho đơn chưa hủy; đơn đã hủy chuyển xử lý 4.11. Không coi trình duyệt quay về là webhook.

**Dòng sự kiện chính**
1. payOS POST api/thanh-toan/webhook kèm dữ liệu và chữ ký.
2. API dùng SDK VerifyAsync; kiểm sự kiện thành công và mã 00.
3. API tìm đơn, kiểm phương thức VietQR, currency VND, mô tả đúng mã đơn, reference và số tiền khớp.
4. API khóa đơn và thanh toán, gọi sp_XacNhanChuyenKhoan khi chưa ghi nhận trả; không ghi lùi khoản đã hoàn.
5. Commit tiền trước; nếu đơn chưa hủy thì thử xếp in, nếu đã hủy giữ nguyên để hoàn.
6. API gửi thông tin cập nhật tới chủ đơn/chi nhánh và phản hồi webhook.

**Dòng sự kiện phụ**
Bước 2 chữ ký sai: trả lỗi, không cập nhật.
Mẫu thử, sự kiện thất bại, orderCode không hợp lệ hoặc đơn không tồn tại: nhận phản hồi nhưng không sửa đơn.
Bước 3 sai tiền/nội dung/tiền tệ/thiếu reference: không xác nhận đã trả; không in.
Webhook lặp: không thu tiền hoặc tạo thanh toán thứ hai; có thể thử lại xếp in cho đơn đã trả còn chờ.
Đơn DA_HUY: nhận tiền hợp lệ vẫn ghi để hoàn, tuyệt đối không phục hồi đơn.
Đã DA_HOAN_TIEN: không chuyển trở lại DA_THANH_TOAN.

**Tiền điều kiện**
API webhook có thể được payOS truy cập; khóa xác minh được cấu hình. Endpoint không dùng JWT người dùng nhưng bắt buộc kiểm chữ ký.

**Hậu điều kiện**
Khoản hợp lệ được lưu một lần theo trạng thái hiện hành. Webhook không hợp lệ không tạo quyền in; lỗi xếp máy không làm mất khoản tiền đã commit.

**Căn cứ** PAYMENT, SQL_PAY, AUTO_PRINT

## HT-4.4-04 Tự động tiếp nhận và xếp đơn đã thanh toán vào quy trình in

**Use case**
Tự động tiếp nhận và xếp đơn đã thanh toán vào quy trình in

**Use case ID**
HT-4.4-04

**Tác nhân chính**
Hệ thống; kích hoạt bởi kết quả nhận tiền hoặc bộ thử xếp lại

**Tổng quan**
Hành vi nội bộ chuyển đơn VietQR đã trả sang chờ in hoặc đang in tùy điều kiện; không phải thao tác độc lập của khách.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Được kích hoạt sau HT-4.4-03/05 cho đơn chưa hủy; bàn giao cho use case máy in/connector 4.7–4.8 của Tài.

**Dòng sự kiện chính**
1. TuDongInService khóa đơn, đọc trạng thái và điều kiện nhận tiền.
2. Nếu CHO_XAC_NHAN thì tiếp nhận thành DA_XAC_NHAN.
3. Kiểm tra mọi PDF còn sẵn trên máy chủ và máy phù hợp đang có heartbeat kết nối.
4. Chọn máy cùng chi nhánh, đủ khổ/màu/hai mặt và ràng buộc connector; ưu tiên máy đã gán, rồi số đơn đang in ít hơn, rồi mã máy.
5. Gọi sp_PhanCongMayIn và sp_CapNhatTrangThaiIn sang DANG_IN trong giao dịch.
6. Gửi cập nhật; connector tiếp tục lấy và thực thi lệnh theo cơ chế của máy in.

**Dòng sự kiện phụ**
Bước 1 chưa trả hoặc đã hủy: không xếp in.
Đơn đã in/đang in/đã hoàn tất: không tạo thêm lượt in chỉ vì webhook lặp.
Bước 3 file mất: giữ DA_XAC_NHAN, ghi cảnh báo khi còn chỗ và yêu cầu xử lý file.
Bước 3–4 chưa có máy phù hợp kết nối: giữ chờ in; hệ thống có thể thử lại sau.
Bước 5 lỗi SQL/thiếu kho: rollback phần xếp in, giữ khoản tiền đã nhận; chưa coi đã bắt đầu in thành công.
DANG_IN là trạng thái quy trình, không chứng minh giấy đã ra; hoàn tất vật lý/queue thuộc connector.

**Tiền điều kiện**
ThanhToan DA_THANH_TOAN, phương thức VIETQR và đơn còn ở giai đoạn được phép xếp in.

**Hậu điều kiện**
Đủ điều kiện thì đơn vào DANG_IN có máy; chưa đủ thì ở DA_XAC_NHAN và vẫn đã thanh toán. Không tự phục hồi đơn hủy.

**Căn cứ** AUTO_PRINT, ORDER, PRINT_JOB

## HT-4.4-05 Đối soát trạng thái thanh toán với payOS

**Use case**
Đối soát trạng thái thanh toán với payOS

**Use case ID**
HT-4.4-05

**Tác nhân chính**
Khách hoặc nhân sự đúng phạm vi; payOS hỗ trợ

**Tổng quan**
Khôi phục kết quả khi webhook bị lỡ bằng đọc trực tiếp payOS; App cũng gọi khi theo dõi thanh toán.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Có thể chạy từ GET thanh toán của HT-4.4-01 hoặc nút Kiểm tra thanh toán trên Web. Dùng chung ghi nhận và xếp in HT-4.4-03/04.

**Dòng sự kiện chính**
1. Giao diện gọi GET api/thanh-toan/{id}.
2. API kiểm chủ đơn/phạm vi và đọc ThanhToan.
3. Nếu VietQR chưa trả, API GET payment request từ payOS theo mã đơn.
4. Chỉ khi status Paid, orderCode, Amount và AmountPaid đều khớp thì lấy reference để gọi ghi nhận tiền chung.
5. API đọc lại trạng thái sau ghi nhận và trả cho giao diện.
6. Giao diện hiện kết quả mới, không chỉ đổi nhãn cục bộ.

**Dòng sự kiện phụ**
Bước 2 không đúng quyền: không lộ trạng thái đơn khác.
Bước 3 chưa có link/chưa Paid hoặc tiền không khớp: giữ chưa thanh toán.
payOS báo lỗi truy vấn: ghi cảnh báo, chưa xác nhận tiền; người dùng có thể thử lại.
Đơn đã hủy mà tiền đúng: giữ hủy để hoàn, không in.
Đơn đã hoàn tiền: không ghi lùi sang đã trả.
Không dùng status=PAID trong URL hoặc dữ liệu client làm chứng cứ đối soát.

**Tiền điều kiện**
Người gọi có quyền với đơn; đơn VietQR hiện chưa được ghi nhận thanh toán.

**Hậu điều kiện**
Trạng thái được đồng bộ khi payOS cung cấp bằng chứng đúng; chưa có bằng chứng thì giữ nguyên. Đối soát không thu thêm tiền khách.

**Căn cứ** PAYMENT, PAY_UI

## HT-4.4-06 Tự động hủy đơn VietQR quá hạn chưa thanh toán

**Use case**
Tự động hủy đơn VietQR quá hạn chưa thanh toán

**Use case ID**
HT-4.4-06

**Tác nhân chính**
Bộ hẹn giờ nội bộ DonQuaHanJob

**Tổng quan**
Thực thi chính sách kết thúc đơn đang chờ sau hạn trả; không phải extension của hành vi truy vấn payOS.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Cùng hành vi đã đặc tả NV-3.5-03; tham chiếu hủy và tiền đến muộn trong Task 1. Không bắt buộc gọi đối soát mỗi lần job chạy.

**Dòng sự kiện chính**
1. Job chạy lượt kiểm tra định kỳ khoảng mỗi phút.
2. Job gọi sp_HuyDonQuaHan.
3. SQL chọn đúng đơn CHO_XAC_NHAN, VIETQR, CHUA_THANH_TOAN có NgayDat cộng 15 phút nhỏ hơn hiện tại.
4. SQL đổi DA_HUY và lưu ghi chú Quá 15 phút chưa thanh toán.
5. Lượt tiếp theo tiếp tục kiểm tra các đơn còn phù hợp; khách đọc lại sẽ thấy đã hủy.

**Dòng sự kiện phụ**
Đơn chưa quá hạn/đã trả/đã tiếp nhận: không bị hủy bởi điều kiện này.
API tắt hoặc CSDL lỗi: không bảo đảm hủy đúng giây; log và thử lượt sau khi hoạt động.
Tiền về sau khi hủy: xử lý như HT-4.11-05; không đưa lại vào in.
Không giả có actor quản lý bấm hủy quá hạn hoặc có timer của điện thoại điều khiển CSDL.
Dọn file của job là việc bảo trì riêng, không dùng làm điều kiện công nhận thanh toán.

**Tiền điều kiện**
API đang chạy background job và truy cập được CSDL.

**Hậu điều kiện**
Chỉ đơn đúng điều kiện được kết thúc; không tự hoàn khoản tiền chưa nhận và không xóa lịch sử đơn.

**Căn cứ** TIMEOUT, SQL_TIMEOUT

## Đối chiếu nhãn trên hình gốc

| Nhãn | Đặc tả | Kết luận | Cách hiểu và điều chỉnh |
| --- | --- | --- | --- |
| Quét mã VietQR thanh toán | HT-4.4-01 | Chỉnh tên cho sát ranh giới | App hiển thị QR, ngân hàng quét/chuyển; App không tự quét để chuyển tiền. |
| Tạo liên kết thanh toán PayOS | HT-4.4-02 | Đúng | Tạo hoặc lấy lại link/QR của đúng đơn. |
| Nhận Webhook xác nhận thanh toán | HT-4.4-03 | Đúng | Bắt buộc chữ ký và đối chiếu dữ liệu. |
| Tự động xếp đơn vào hàng đợi | HT-4.4-04 | Chỉnh include | Là xử lý có điều kiện sau nhận tiền; không luôn thành công/không chạy lại cho đơn hủy. |
| Đối soát trạng thái với PayOS | HT-4.4-05 | Bổ sung actor | Cả khách App và nhân sự đúng quyền có thể kích hoạt; không riêng nhân viên. |
| Tự động hủy đơn quá hạn sau 15p | HT-4.4-06 | Chỉnh quan hệ | Job độc lập; bỏ extend hướng vào đối soát nếu muốn mô tả đúng code. |

## Căn cứ mã nguồn

- **AUTO_PRINT** [MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs:17) — Xếp máy sau khi trả tiền; giữ chờ nếu chưa đủ điều kiện.
- **ORDER** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32) — Đặt App, lọc chi nhánh, chi tiết, tiếp nhận, hủy và đánh giá.
- **PAYMENT** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30) — QR, chữ ký webhook, đối soát, tiền đến muộn và điều hướng.
- **PAY_RULE** [MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/QuyTacPayOS.cs:8](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/QuyTacPayOS.cs:8) — Chuẩn hóa URL, mô tả đơn và so tiền.
- **PAY_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart:24](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart:24) — Vẽ QR, polling 5 giây khi hoạt động, đối soát khi resume.
- **PRINT_JOB** [MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/XepLaiDonChoInJob.cs:7](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/XepLaiDonChoInJob.cs:7) — Bộ thử xếp lại đơn đã thanh toán đang chờ.
- **QR_STORE** [MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/DuLieuQrPayOSStore.cs:8](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/DuLieuQrPayOSStore.cs:8) — Cache dữ liệu QR theo đơn/link/tiền.
- **SQL_PAY** [Database/04_StoredProcedure.sql:320](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:320) — Ghi nhận thanh toán chuyển khoản và thông báo.
- **SQL_TIMEOUT** [Database/04_StoredProcedure.sql:339](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:339) — Điều kiện VietQR chưa trả, chờ tiếp nhận và quá 15 phút.
- **TIMEOUT** [MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:12](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:12) — Chạy hủy quá hạn khoảng mỗi phút, dọn file là tác vụ riêng.
