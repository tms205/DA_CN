# Task 5 Theo dõi đơn cá nhân

Phạm vi: HeThong/TheoDoiDon.png. Đối chiếu source ngày 26/10/2026. Gồm 0 đặc tả nghiệp vụ và 7 đặc tả hệ thống.

- Những thao tác thanh toán/hủy trùng giữa các hình dùng một quy tắc nghiệp vụ và dẫn chiếu ID, không tạo điều kiện khác nhau cho cùng chức năng.
- Xem tiến độ hiện dựa trạng thái và mốc API dựng, không giả mọi mốc có thời điểm riêng hoặc màn khách luôn hiển thị phần trăm giấy đã in của driver.
- Hủy, đánh giá và đánh dấu đọc là hành động tùy chọn có điều kiện; các đường extend chỉ hợp lý khi ghi rõ điểm mở rộng tương ứng trong mô tả.

## HT-4.5-01 Tra cứu danh sách đơn cá nhân

**Use case**
Tra cứu danh sách đơn cá nhân

**Use case ID**
HT-4.5-01

**Tác nhân chính**
Khách hàng

**Tổng quan**
Xem và lọc các đơn của chính khách trên tab Đơn hàng.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Mở sang chi tiết HT-4.5-02 hoặc thanh toán HT-4.5-03 theo trạng thái. Không phải màn quản trị tra đơn toàn chuỗi.

**Dòng sự kiện chính**
1. Khách mở tab Đơn hàng.
2. App GET api/don-hang/cua-toi; API lấy mã khách từ JWT và truy vấn đơn mới nhất trước.
3. App hiển thị mã, chi nhánh, trạng thái, số tiền và thông tin tóm tắt.
4. Khách lọc Tất cả, Đang xử lý, Đang giao, Hoàn tất hoặc Đã hủy; App lọc danh sách đã nhận.
5. Khách chọn đơn; đơn VietQR còn nợ chưa hủy mở thanh toán, các đơn khác mở tiến độ.
6. Khi có sự kiện hoặc quay về danh sách, App tải lại trạng thái.

**Dòng sự kiện phụ**
Bước 2 không có đơn: hiển thị trạng thái trống, cho sang đặt in.
Lỗi mạng/token hết hạn: báo lỗi/thử lại hoặc đăng nhập, không hiện đơn người khác.
GIAO_THAT_BAI thuộc nhóm Đang giao; DA_IN vẫn thuộc Đang xử lý cho đến bàn giao.
Các nhóm là nhóm UI, không phải trạng thái mới trong CSDL.
App không có API tìm toàn hệ thống bằng mã tùy ý cho khách; chỉ trả đơn của chính khách.

**Tiền điều kiện**
Có phiên CUSTOMER hợp lệ.

**Hậu điều kiện**
Danh sách/lọc được hiển thị; thao tác đọc không đổi trạng thái đơn hoặc tiền.

**Căn cứ** ORDER, ORDER_SQL, ORDERS_UI, MOBILE_MODELS

## HT-4.5-02 Xem chi tiết đơn và thông tin thanh toán

**Use case**
Xem chi tiết đơn và thông tin thanh toán

**Use case ID**
HT-4.5-02

**Tác nhân chính**
Khách hàng

**Tổng quan**
Xem dòng in, gia công, tiền, ghi chú, nơi nhận, máy/nhân sự và các mốc tiến độ của đơn.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Từ HT-4.5-01; hành vi hủy, đánh giá và thông tin nhận hoàn là lựa chọn theo điều kiện. Nhãn hóa đơn trên hình cần phân biệt với hóa đơn điện tử thuế.

**Dòng sự kiện chính**
1. Khách mở một đơn; App GET api/don-hang/{id}.
2. API đọc đơn rồi kiểm MaKhachHang từ JWT; đọc các dòng in/gia công và thông tin giao liên quan.
3. API dựng các mốc theo trạng thái; đơn hủy chỉ hiển thị mốc đã đặt và đã hủy.
4. App hiển thị tổng tiền/các khoản, quy cách và mốc tiến độ; nút thao tác theo điều kiện.
5. App nghe sự kiện đúng mã đơn để đọc lại; kéo xuống cho phép làm mới.
6. Khách rời màn; App bỏ đăng ký lắng nghe của màn.

**Dòng sự kiện phụ**
Bước 2 không tồn tại/không phải chủ: trả không tìm thấy, không lộ nội dung.
Mất SignalR: có thể kéo làm mới; không hứa có push khi App đã tắt.
Nhiều sự kiện đồng thời: màn gom lượt tải để không chồng truy vấn vô hạn.
CSDL không có timestamp riêng cho mọi mốc; không giả giờ tiếp nhận/bắt đầu in/in xong nếu không được lưu.
Thông tin tiền là chi tiết đơn; chưa có chức năng xuất hóa đơn điện tử VAT/XML hoặc số hóa đơn thuế trong luồng này.

**Tiền điều kiện**
Khách có phiên và đơn thuộc chính khách.

**Hậu điều kiện**
Đọc được chi tiết hợp lệ và phản ánh trạng thái; xem đơn không tự in, hủy hay thanh toán.

**Căn cứ** ORDER, DETAIL_UI, HUB, MOBILE_MODELS

## HT-4.5-03 Tiếp tục thanh toán đơn VietQR còn hạn

**Use case**
Tiếp tục thanh toán đơn VietQR còn hạn

**Use case ID**
HT-4.5-03

**Tác nhân chính**
Khách hàng

**Tổng quan**
Mở lại QR của đơn cá nhân chưa trả để khách hoàn tất khoản chuyển.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Tái sử dụng HT-4.4-01/02/05, không tạo đơn mới. Có thể mở trực tiếp từ danh sách.

**Dòng sự kiện chính**
1. Khách chọn đơn đang chờ chuyển khoản từ danh sách.
2. App mở Thanh toán, đọc trạng thái/hạn từ API.
3. Nếu còn đủ điều kiện, App lấy lại link/QR theo HT-4.4-02.
4. Khách chuyển khoản; App nghe sự kiện, polling khi hoạt động và đối soát lúc resume.
5. API xác nhận đúng tiền thì hiển thị đã trả và cho chuyển xem tiến độ.

**Dòng sự kiện phụ**
UI danh sách chỉ biết chưa trả/chưa hủy; hạn cuối cùng được màn thanh toán/API kiểm tra, không tin ảnh danh sách cũ.
Đã trả trong lúc mở: hiển thị kết quả mới, không yêu cầu trả lần nữa.
Đơn đã hủy/hết hạn: dừng thanh toán trên giao diện; hướng dẫn đặt lại nếu cần.
Link lỗi hoặc payOS chưa phản hồi: cho kiểm tra/thử lại, không gán đã thanh toán cục bộ.
Tiền vào sau hủy: chuyển luồng hoàn 4.11, không khôi phục.

**Tiền điều kiện**
Đơn thuộc khách, VIETQR, chưa trả, chưa hủy và còn hạn do máy chủ xác nhận.

**Hậu điều kiện**
Khoản trả được phản ánh theo máy chủ; không nhân bản đơn vì mở lại thanh toán.

**Căn cứ** ORDERS_UI, PAY_UI, PAYMENT

## HT-4.5-04 Hủy đơn khi chưa bắt đầu in

**Use case**
Hủy đơn khi chưa bắt đầu in

**Use case ID**
HT-4.5-04

**Tác nhân chính**
Khách hàng

**Tổng quan**
Hủy đơn cá nhân từ QR/tiến độ khi còn chờ tiếp nhận hoặc đã tiếp nhận nhưng chưa in.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Tái sử dụng HT-4.11-01; nhãn Hủy đơn chờ xác nhận trên hình 4.5 hẹp hơn điều kiện thực tế. Bổ sung tài khoản nhận hoàn theo HT-4.11-03.

**Dòng sự kiện chính**
1. App kiểm trạng thái và hiện nút Hủy đơn.
2. Khách nhập lý do; nếu đã trả thì nhập ngân hàng và số tài khoản cùng lý do hoàn.
3. App xác nhận và POST api/don-hang/{id}/huy.
4. API khóa, kiểm lại chủ đơn/trạng thái/dữ liệu nhận hoàn rồi gọi thủ tục hủy.
5. App tải lại; đơn DA_HUY, có tiền thì chờ CEO chuyển hoàn, chưa trả thì không có khoản hoàn.

**Dòng sự kiện phụ**
Đơn vừa chuyển DANG_IN trước lúc gửi: API từ chối dù nút cũ từng hiện.
Lý do thiếu, tài khoản nhận sai hoặc thiếu trường khi đã trả: sửa trong popup rồi gửi lại.
Khách hủy popup: giữ đơn hiện tại.
Đơn đã hủy/đã hoàn: không hủy lại.
Khách chưa có thông tin nhận hoàn ở đơn hủy đã trả có thể bổ sung khi còn được phép; không tự xác nhận CEO đã hoàn.

**Tiền điều kiện**
Đơn thuộc khách, trạng thái CHO_XAC_NHAN hoặc DA_XAC_NHAN.

**Hậu điều kiện**
Hủy thành công không tự chuyển tiền; trạng thái nhận tiền và hoàn được theo dõi riêng. Từ DANG_IN không hủy/hoàn qua chức năng này.

**Căn cứ** ORDER, CANCEL_UI, MOBILE_MODELS, REFUND_DTO

## HT-4.5-05 Đánh giá chất lượng dịch vụ

**Use case**
Đánh giá chất lượng dịch vụ

**Use case ID**
HT-4.5-05

**Tác nhân chính**
Khách hàng

**Tổng quan**
Gửi một đánh giá cho đơn của mình đã hoàn tất.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Lựa chọn tùy ý từ chi tiết HT-4.5-02 khi HOAN_THANH và chưa có đánh giá; không bắt buộc để hoàn tất đơn.

**Dòng sự kiện chính**
1. App hiển thị Đánh giá trên đơn đủ điều kiện.
2. Khách chọn 1–5 sao và nhập nhận xét nếu muốn.
3. App POST api/don-hang/{id}/danh-gia.
4. API kiểm sao; sp_DanhGia kiểm chủ đơn, HOAN_THANH và chưa có đánh giá.
5. SQL lưu đánh giá, API cảm ơn; App tải lại và không hiện lời mời đánh giá mới cho cùng đơn.

**Dòng sự kiện phụ**
Bước 2 số sao ngoài 1–5/nhận xét quá giới hạn 500 ký tự: yêu cầu sửa.
Bước 4 đơn chưa hoàn tất hoặc không thuộc khách: từ chối.
Bước 4 đơn đã có đánh giá: từ chối gửi thêm; không mô tả có sửa/xóa đánh giá cho khách khi chưa có API.
Quản lý có quyền ẩn đánh giá ở phân hệ riêng; không có nghĩa khách tự sửa trạng thái hiển thị.
Mất phản hồi: tải lại chi tiết kiểm đã đánh giá trước khi gửi lại.

**Tiền điều kiện**
Phiên CUSTOMER; đơn thuộc khách, HOAN_THANH, chưa đánh giá.

**Hậu điều kiện**
Có một đánh giá nếu hợp lệ; đơn và tiền giữ nguyên. Không tự đổi đánh giá thành khiếu nại/hoàn tiền.

**Căn cứ** ORDER, SQL_REVIEW, DETAIL_UI, ORDER_DTO

## HT-4.5-06 Xem thông báo cá nhân

**Use case**
Xem thông báo cá nhân

**Use case ID**
HT-4.5-06

**Tác nhân chính**
Khách hàng

**Tổng quan**
Đọc các thông báo nghiệp vụ trong App, gồm thông báo đơn và thông báo hệ thống.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Có thể gọi đánh dấu đã đọc HT-4.5-07. SignalR hỗ trợ cập nhật khi còn kết nối, không phải dịch vụ push lúc tắt App.

**Dòng sự kiện chính**
1. Khách mở tab Thông báo.
2. App GET api/thong-bao, API lấy người nhận từ JWT.
3. API trả thông báo mới nhất và tổng số chưa đọc; mặc định 50, giới hạn yêu cầu trong 1–200.
4. App hiển thị tiêu đề, nội dung, thời điểm và dấu đọc/chưa đọc.
5. Khi có tin realtime hoặc kéo làm mới, App lấy danh sách lại.
6. Khách chạm thông báo đơn: đánh dấu đọc rồi mở tab Đơn hàng theo xử lý giao diện hiện tại.

**Dòng sự kiện phụ**
Danh sách trống: hiển thị chưa có thông báo, không báo lỗi.
Mất mạng: báo lỗi/thử lại; không giả có tin mới.
Số chưa đọc là toàn bộ thông báo của người dùng, có thể lớn hơn số item đang tải.
Chạm thông báo đơn hiện mở tab Đơn hàng, không khẳng định deep link luôn mở thẳng đúng màn chi tiết.
Không có cam kết nhận thông báo đẩy khi ứng dụng đã tắt; người dùng đã yêu cầu bỏ lựa chọn này.

**Tiền điều kiện**
Có phiên; thông báo nếu có được lưu cho đúng người dùng.

**Hậu điều kiện**
Thông báo của chính khách được trình bày; chỉ xem danh sách chưa mặc định đánh dấu toàn bộ đã đọc.

**Căn cứ** NOTICE, NOTICE_UI, HUB

## HT-4.5-07 Đánh dấu thông báo đã đọc

**Use case**
Đánh dấu thông báo đã đọc

**Use case ID**
HT-4.5-07

**Tác nhân chính**
Khách hàng

**Tổng quan**
Ghi nhận khách đã đọc một hoặc toàn bộ thông báo cá nhân.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Thao tác tùy chọn từ HT-4.5-06. Có thể mô hình extend với điểm mở rộng chọn đọc, không dùng để khẳng định phải đọc hết khi xem.

**Dòng sự kiện chính**
1. Khách chạm một thông báo chưa đọc hoặc chọn Đọc tất cả.
2. App PUT api/thong-bao/{id}/da-doc hoặc api/thong-bao/da-doc-tat-ca.
3. API lấy mã người dùng từ JWT, cập nhật chỉ bản ghi cùng chủ sở hữu.
4. API trả kết quả; App tải lại danh sách và số chưa đọc.
5. Nếu chạm thông báo gắn đơn, App điều hướng sang tab đơn sau khi ghi đọc thành công.

**Dòng sự kiện phụ**
Bước 3 mã không tồn tại/không thuộc người dùng: báo không tìm thấy, không sửa thông báo người khác.
Thông báo đã đọc: không làm phát sinh thông báo mới hoặc nhân đôi bản ghi.
Đọc tất cả khi không còn tin chưa đọc: kết quả hợp lệ, số chưa đọc bằng 0.
Mất mạng hoặc cập nhật lỗi: không giả đã lưu; hiển thị lỗi, thử lại.
Không xóa thông báo hay hủy đơn khi đánh dấu đã đọc.

**Tiền điều kiện**
Có phiên, thông báo thuộc người gọi hoặc người dùng muốn đánh dấu toàn bộ.

**Hậu điều kiện**
Cờ DaDoc và số đếm được cập nhật đúng chủ; dữ liệu nội dung/đơn/tiền không đổi.

**Căn cứ** NOTICE, NOTICE_UI

## Đối chiếu nhãn trên hình gốc

| Nhãn | Đặc tả | Kết luận | Cách hiểu và điều chỉnh |
| --- | --- | --- | --- |
| Tra cứu đơn | HT-4.5-01 | Đúng | Danh sách cá nhân, lọc nhóm ở App. |
| Thanh toán đơn VietQR còn hạn | HT-4.5-03 | Đúng có kiểm máy chủ | Danh sách điều hướng theo chưa trả/chưa hủy; API QR kiểm hạn cuối. |
| Hủy đơn chờ xác nhận | HT-4.5-04 và HT-4.11-01 | Cần đổi tên | Hủy cả CHO_XAC_NHAN và DA_XAC_NHAN; từ DANG_IN không hủy. |
| Xem chi tiết và hóa đơn | HT-4.5-02 | Cần làm rõ tên | Có chi tiết tiền của đơn; chưa có hóa đơn điện tử thuế, nên dùng Xem chi tiết đơn và thanh toán. |
| Đánh giá chất lượng dịch vụ | HT-4.5-05 | Đúng có điều kiện | Chỉ HOAN_THANH, đúng khách, một lần. |
| Xem thông báo cá nhân | HT-4.5-06 | Đúng | Thông báo lưu CSDL và realtime khi kết nối; không push khi tắt App. |
| Đánh dấu thông báo đã đọc | HT-4.5-07 | Đúng | Có một hoặc tất cả, cập nhật theo chủ sở hữu. |

## Căn cứ mã nguồn

- **CANCEL_UI** [MaNguon/photocopy_mobile/lib/widgets/huy_don.dart:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/widgets/huy_don.dart:11) — Form hủy và cập nhật ba trường nhận hoàn trên App.
- **DETAIL_UI** [MaNguon/photocopy_mobile/lib/screens/don_hang/tien_do_screen.dart:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/don_hang/tien_do_screen.dart:17) — Mốc tiến độ, gom lượt đọc và tác vụ theo trạng thái.
- **HUB** [MaNguon/PhotocopyChain/PhotocopyApi/Hubs/CapNhatHub.cs:16](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Hubs/CapNhatHub.cs:16) — Nhóm SignalR theo người/chi nhánh/toàn chuỗi từ JWT.
- **MOBILE_MODELS** [MaNguon/photocopy_mobile/lib/models.dart:216](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/models.dart:216) — Các getter nhóm, chờ chuyển khoản và điều kiện hủy/hoàn.
- **NOTICE** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThongBaoController.cs:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThongBaoController.cs:17) — Danh sách/đếm và đánh dấu đọc đúng người nhận.
- **NOTICE_UI** [MaNguon/photocopy_mobile/lib/screens/thong_bao/thong_bao_screen.dart:15](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/thong_bao/thong_bao_screen.dart:15) — Đọc một/tất cả, thông báo đơn mở tab đơn hàng.
- **ORDER** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32) — Đặt App, lọc chi nhánh, chi tiết, tiếp nhận, hủy và đánh giá.
- **ORDERS_UI** [MaNguon/photocopy_mobile/lib/screens/don_hang/don_hang_screen.dart:71](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/don_hang/don_hang_screen.dart:71) — Danh sách, lọc nhóm và điều hướng thanh toán/tiến độ.
- **ORDER_DTO** [MaNguon/PhotocopyChain/PhotocopyShared/Dtos/DonHang/DonHangDtos.cs:8](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/Dtos/DonHang/DonHangDtos.cs:8) — Hợp đồng danh sách/chi tiết đơn và đánh giá.
- **ORDER_SQL** [MaNguon/PhotocopyChain/PhotocopyApi/DuLieu/TruyVanSql.cs:6](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DuLieu/TruyVanSql.cs:6) — Các truy vấn phục vụ danh sách/chi tiết đơn.
- **PAYMENT** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30) — QR, chữ ký webhook, đối soát, tiền đến muộn và điều hướng.
- **PAY_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart:24](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart:24) — Vẽ QR, polling 5 giây khi hoạt động, đối soát khi resume.
- **REFUND_DTO** [MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Tuan3/ThongTinHoanTienRequest.cs:7](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Tuan3/ThongTinHoanTienRequest.cs:7) — Giới hạn lý do, ngân hàng, số tài khoản và ghi chú ba dòng.
- **SQL_REVIEW** [Database/04_StoredProcedure.sql:692](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:692) — Chỉ chủ đơn HOAN_THANH và một đánh giá mỗi đơn.
