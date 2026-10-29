# Task 3 Đặt in trực tuyến và thanh toán trước

Phạm vi: NghiepVu/DatIn_ThanhToan.png, HeThong/DatInTrenApp.png. Đối chiếu source ngày 26/10/2026. Gồm 6 đặc tả nghiệp vụ và 10 đặc tả hệ thống.

- Hình 3.2 mô tả công việc cửa hàng với khách và ngân hàng; không đưa API, SMTP hoặc Geoapify vào tác nhân nghiệp vụ.
- Các đường include từ Đặt đơn tới Tiếp nhận chỉ để diễn tả xảy ra sau nhau là chưa chặt. Nên diễn đạt quy trình nối tiếp bằng mô tả/Activity; include chỉ dùng cho hành vi bắt buộc được gọi trong phạm vi use case.
- Tên Ahamove còn trong comment/thông báo SQL cũ; code gọi bản đồ hiện tại là Geoapify và tự tính phí ship nội bộ. Không suy từ comment thành một tích hợp hãng giao hàng đang chạy.
- API nhận danh sách file nhưng giao diện App hiện gửi một file trong mỗi lượt. Không mô tả App có trình chọn nhiều file mà chưa triển khai.

## NV-3.2-01 Đặt đơn in trực tuyến

**Giới thiệu về use case nghiệp vụ**
Khách muốn cửa hàng in tài liệu, mẫu đơn trống hoặc ảnh thẻ. Mục tiêu là thống nhất nội dung, giá, nơi nhận và tiền thanh toán trước khi tổ chức in. Khách và ngân hàng là bên ngoài; nhân viên và shipper là bên thực hiện trong tổ chức.

**Các dòng cơ bản**
1. Khách cung cấp tài liệu hoặc chọn mẫu/ảnh và xác định số lượng cần in.
2. Cửa hàng cho khách lựa chọn quy cách có bán, chi nhánh đáp ứng và cách nhận.
3. Cửa hàng báo tiền in, gia công, phụ phí, giao hàng, giảm giá và thời điểm dự kiến.
4. Khách kiểm tra, đồng ý đặt và nhận mã đơn cùng hướng dẫn chuyển khoản.
5. Khách chuyển đủ tiền; cửa hàng xác minh khoản tiền thực nhận.
6. Cửa hàng tiếp nhận/xếp in khi đủ điều kiện, sau đó chuyển sang nhận tại quầy hoặc giao nội bộ.

**Các dòng thay thế**
- Tài liệu lỗi hoặc cấu hình không được bán: yêu cầu thay tài liệu/quy cách, quay lại bước 1–2.
- Không có chi nhánh đáp ứng: chưa nhận đơn theo lựa chọn đó.
- Chưa nhận đủ tiền: chưa coi đã thanh toán; đơn quá hạn được xử lý riêng.
- Khách chọn giao tận nơi: lấy điểm nhận và báo phí trước bước 4; không thu COD khi giao.
- Khách muốn hủy trước khi bắt đầu in: chuyển nghiệp vụ 3.5; đã bắt đầu in không hủy theo luồng thông thường.

**Căn cứ** FILE, ORDER, QUOTE, PAYMENT, AUTO_PRINT

## NV-3.2-02 Cung cấp tài liệu và lựa chọn quy cách in

**Giới thiệu về use case nghiệp vụ**
Khách giao nội dung cần in và thỏa thuận thành phẩm với cửa hàng. Nội dung có thể là tài liệu tải lên, biểu mẫu trắng có sẵn hoặc ảnh thẻ; không yêu cầu khách tự tạo biểu mẫu bằng tay trong phần mềm.

**Các dòng cơ bản**
1. Khách chọn nguồn tài liệu và kiểm tra đúng nội dung.
2. Cửa hàng tiếp nhận, chuyển về định dạng in được và xác định số trang hoặc số ảnh.
3. Khách chọn giấy/dịch vụ, một hoặc hai mặt, màu sắc, hướng giấy và số bản phù hợp.
4. Khách chọn các gia công được phép và tốc độ thường/hỏa tốc.
5. Cửa hàng kiểm tra các lựa chọn có thể thực hiện và chuyển sang báo giá.

**Các dòng thay thế**
- Không đọc được tài liệu: yêu cầu file thay thế; chưa báo giá thành đơn.
- Mẫu đã thay hoặc ngừng cung cấp: xem danh sách mới và chọn lại.
- Ảnh thẻ: chọn kích thước, số ảnh; không áp gia công tài liệu hoặc tùy ý thay bố trí PDF đã ghép.
- Bìa kiếng có thể đi với lò xo; lò xo không đi với kim góc; kim góc quá 50 tờ mỗi bản bị từ chối.
- Giấy tồn kho mới chưa có dịch vụ, giá hoặc máy phù hợp không tự trở thành lựa chọn in.

**Căn cứ** FILE, CATALOG, PRINT_RULE, PHOTO_RULE, SQL_PRICE

## NV-3.2-03 Thanh toán chuyển khoản trước

**Giới thiệu về use case nghiệp vụ**
Khách thanh toán toàn bộ đơn App qua ngân hàng theo hướng dẫn QR. Cửa hàng chỉ công nhận đã trả khi nhận được căn cứ xác thực từ cổng thanh toán; không dựa vào lời khách báo hoặc trang quay về.

**Các dòng cơ bản**
1. Cửa hàng cấp thông tin QR của đúng đơn, đúng số tiền và hạn trả.
2. Khách kiểm tra người nhận, số tiền, nội dung và chuyển khoản trên ngân hàng.
3. Ngân hàng/cổng thanh toán cung cấp kết quả giao dịch cho cửa hàng.
4. Cửa hàng đối chiếu đơn, tiền và thông tin giao dịch.
5. Khoản hợp lệ được ghi nhận đã trả và chuyển điều kiện cho xếp in.

**Các dòng thay thế**
- Khách chỉ mở QR hoặc đóng màn ngân hàng: chưa đủ căn cứ thanh toán.
- Không khớp tiền hoặc chưa nhận thông tin xác thực: chờ đối soát, không tự đánh dấu đã trả.
- Quá 15 phút khi còn chờ và chưa thanh toán: kết thúc đơn theo chính sách quá hạn.
- Tiền về sau khi đã hủy: giữ đơn hủy và xử lý hoàn, không đưa lại vào in.
- Ngân hàng báo lặp: không thu thêm hoặc tạo khoản thanh toán mới cho cùng đơn.

**Căn cứ** PAYMENT, SQL_PAY, TIMEOUT

## NV-3.2-04 Tiếp nhận đơn in đã thanh toán

**Giới thiệu về use case nghiệp vụ**
Cửa hàng chuyển đơn đã nhận đủ tiền sang quy trình sản xuất. Nhân viên theo dõi và xử lý ngoại lệ; các đơn VietQR đủ điều kiện có thể được tiếp nhận/xếp in tự động, không bắt buộc có thao tác nhân viên ở mọi đơn.

**Các dòng cơ bản**
1. Cửa hàng xác định đơn đã thanh toán còn chờ tiếp nhận/chờ in.
2. Kiểm tra tài liệu còn sẵn và chi nhánh có máy đáp ứng.
3. Tiếp nhận đơn; chọn máy đang kết nối phù hợp năng lực và hàng đợi.
4. Đưa lệnh sang quy trình in; nhân viên theo dõi tiến độ do connector báo.
5. Sau khi in xong, tổ chức gia công, bàn giao tại quầy hoặc chuyển giao hàng.

**Các dòng thay thế**
- Chưa có máy phù hợp kết nối hoặc file không còn sẵn: giữ đơn đã trả ở chờ in và báo cần xử lý.
- Không đủ vật tư hoặc bước xếp máy lỗi: chưa coi bắt đầu in thành công; giữ khoản đã trả.
- Nhân viên tiếp nhận thủ công phải đúng chi nhánh và có ca đang mở.
- Máy trục trặc khi đang in: thuộc xử lý máy/connector; không biến thành hủy đơn trước in.
- Khách nhận tại quầy hoặc shipper nhận hàng là nghiệp vụ tiếp sau, không dùng include để diễn tả đơn thuần thứ tự thời gian.

**Căn cứ** AUTO_PRINT, ORDER, SQL_ACCEPT

## NV-3.2-05 Áp dụng khuyến mãi cho đơn trực tuyến

**Giới thiệu về use case nghiệp vụ**
Khách cung cấp mã để được giảm phần tiền in và gia công nếu thỏa điều kiện của chi nhánh. Đây là lựa chọn tùy ý, không bắt buộc trong mọi lần đặt in.

**Các dòng cơ bản**
1. Khách nhập mã khuyến mãi khi xem tổng tiền.
2. Cửa hàng kiểm tra chi nhánh, thời hạn, trạng thái, số lượt và mức tối thiểu.
3. Nếu hợp lệ, tính giảm theo tỷ lệ và trần giảm.
4. Hiển thị mức giảm và tổng tiền mới để khách quyết định đặt.
5. Khi tạo đơn có mã hợp lệ, ghi nhận mã và tăng lượt sử dụng.

**Các dòng thay thế**
- Mã sai/hết hạn/hết lượt: thông báo lý do, không giảm và vẫn cho đặt giá thường.
- Chưa đủ tiền hàng tối thiểu: cho khách đổi nhu cầu hoặc tiếp tục không giảm.
- Đổi chi nhánh/quy cách: tính lại điều kiện và giá.
- Xem báo giá chưa chiếm một lượt mã; tạo đơn mới là thời điểm ghi lượt.
- Không mô tả tự hoàn lại lượt mã khi hủy; thủ tục hủy hiện không có bước đó.

**Căn cứ** SQL_PRICE, SQL_CREATE, BRANCH_UI

## NV-3.2-06 Giao tài liệu tận nơi bằng shipper nội bộ

**Giới thiệu về use case nghiệp vụ**
Khách chọn giao thay vì đến quầy; cửa hàng thu trước cả tiền hàng và phí ship, giao bằng shipper của chi nhánh. Mô tả ở mức nối tiếp đặt in, chi tiết nghiệp vụ giao hàng thuộc phần Tài 3.4 và 4.10.

**Các dòng cơ bản**
1. Khách xác định vị trí nhận, bổ sung số nhà/tầng/phòng, điện thoại và chỉ dẫn cần thiết.
2. Cửa hàng kiểm tra vùng phục vụ và báo phí trước khi đặt.
3. Khách thanh toán đủ tiền; cửa hàng in và hoàn thiện tài liệu.
4. Shipper nội bộ đúng chi nhánh nhận hoặc được phân công đơn đã in.
5. Shipper giao cho khách và ghi kết quả; thành công thì đơn hoàn tất, không thu thêm tiền.

**Các dòng thay thế**
- Không xác định được điểm giao hoặc ngoài bán kính: chọn lại phương án nhận; không báo phí giả.
- Khách từ chối quyền vị trí: giao tận nơi hiện chưa hoàn thành được trên App; có thể chọn nhận tại quầy.
- Giao thất bại: lưu lý do và lượt giao; còn dưới ba lượt thì cửa hàng có thể cho nhận lại.
- Đủ ba lượt thất bại: cần cửa hàng liên hệ khách; không tự hoàn tiền hoặc tự tiếp tục vô hạn.
- Không gọi hãng giao hàng ngoài để tạo vận đơn; Geoapify chỉ hỗ trợ địa chỉ và tuyến đường.

**Căn cứ** LOCATION_UI, GEO, SHIP, SHIP_RULE

## HT-4.3-01 Tải tài liệu in

**Use case**
Tải tài liệu in

**Use case ID**
HT-4.3-01

**Tác nhân chính**
Khách hàng

**Tổng quan**
Đưa tài liệu từ điện thoại lên máy chủ để chuyển PDF và đếm trang; App hiện xử lý từng file trong một lượt đặt.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Là một cách cung cấp nội dung cho HT-4.3-10; hai cách thay thế là mẫu đơn HT-4.3-02 và ảnh thẻ HT-4.3-03.

**Dòng sự kiện chính**
1. Khách chọn In tài liệu và mở bộ chọn file.
2. Khách chọn file được hỗ trợ; App gửi multipart POST api/file-in.
3. API kiểm tra tên, rỗng, dung lượng cấu hình và đuôi; gắn file với khách lấy từ JWT.
4. API giữ PDF có sẵn hoặc đổi Office/ảnh sang PDF, đếm số trang; file gốc chuyển đổi được dọn.
5. API lưu FileIn và trả mã, tên, số trang, trạng thái; App mở cấu hình in.

**Dòng sự kiện phụ**
Bước 1 khách hủy chọn file: giữ màn hiện tại, không gửi lên.
Bước 3 file rỗng/quá dung lượng/đuôi không hỗ trợ: báo lỗi và chọn lại bước 1.
Bước 4 chuyển đổi hoặc đếm trang lỗi: dọn file của lượt thất bại; không trả file sẵn sàng giả.
Mất mạng khi tải: báo lỗi, chọn thử lại; không khẳng định đơn đã được tạo.
PDF, Word, Excel, PowerPoint, TXT và PNG/JPG là các nhóm hỗ trợ; mức dung lượng đọc từ cấu hình và chịu giới hạn request.

**Tiền điều kiện**
Khách đã đăng nhập; có file ở thiết bị và API truy cập được kho lưu trữ.

**Hậu điều kiện**
Thành công có FileIn của đúng khách cùng PDF và số trang; chưa có DonHang hoặc thanh toán.

**Căn cứ** FILE, FILE_UI

## HT-4.3-02 In mẫu đơn trống có sẵn

**Use case**
In mẫu đơn trống có sẵn

**Use case ID**
HT-4.3-02

**Tác nhân chính**
Khách hàng

**Tổng quan**
Chọn bản mẫu trắng đã được quản trị thư viện; xem trước đúng bản rồi đưa vào quy trình đặt in.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Thay thế tải tài liệu trong HT-4.3-01. Chọn mẫu chưa phải in vật lý; còn cấu hình, chi nhánh, tạo đơn và thanh toán.

**Dòng sự kiện chính**
1. Khách chọn In mẫu đơn trống; App gọi GET api/file-in/mau.
2. Khách tìm theo tên, lọc nhóm và mở xem trước mẫu/trang.
3. API trả ảnh xem trước hoặc PDF của mã và phiên bản mẫu.
4. Khách chọn dùng mẫu; App POST api/file-in/mau/{maMau} kèm phiên bản.
5. API đọc đúng PDF và dùng lại đường tải file để lưu bản FileIn cho khách.
6. App chuyển sang quy cách; nội dung PDF mẫu không được tự điền hộ khách.

**Dòng sự kiện phụ**
Bước 1 thư viện trống/lỗi: hiển thị trống/lỗi và cho làm mới.
Bước 3 mã hoặc trang đã bị gỡ/ẩn: không chọn được; tải danh sách lại.
Bước 4 phiên bản đã đổi: báo xung đột và yêu cầu xem bản mới trước khi chọn.
Khách đóng xem trước: trở về thư viện, không tạo file đặt in.
Mẫu đã thành bản file của đơn cũ không bị mô tả là tự thay theo mẫu mới.

**Tiền điều kiện**
Khách có phiên; thư viện có mẫu đang hiển thị và tài sản PDF/ảnh xem trước đọc được.

**Hậu điều kiện**
Có bản PDF mẫu thuộc khách để đặt in; thao tác xem trước đơn thuần không tạo đơn và không gửi máy in.

**Căn cứ** FILE, FILE_UI, TEMPLATE

## HT-4.3-03 In ảnh thẻ theo kích thước

**Use case**
In ảnh thẻ theo kích thước

**Use case ID**
HT-4.3-03

**Tác nhân chính**
Khách hàng

**Tổng quan**
Ghép JPG/PNG thành PDF ảnh thẻ với số ảnh và kích thước chính xác; tính giá theo từng ảnh.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Cách chọn nội dung thay thế HT-4.3-01. Dùng cấu hình ảnh thẻ riêng trong HT-4.3-04.

**Dòng sự kiện chính**
1. Khách chọn In ảnh thẻ; chọn 2×3, 3×4, 4×6, 3,5×4,5 hoặc 5×5 cm và số ảnh 1–100.
2. App gửi ảnh tới POST api/file-in với kichThuocAnh và soLuongAnh.
3. API kiểm tra ảnh/tham số, ghép PDF và lưu số trang, kích thước, số ảnh ở FileIn.
4. App chọn dòng dịch vụ đúng kích thước; giữ một bản PDF, hướng đứng và không gia công tài liệu.
5. Báo giá tính số ảnh nhân đơn giá; trang PDF vẫn dùng cho lượng giấy và quy trình máy in.

**Dòng sự kiện phụ**
Bước 1 số ảnh ngoài 1–100 hoặc mã kích thước không tồn tại: sửa trước khi tải.
Bước 2 chỉ gửi một trong hai tham số ảnh hoặc dùng file Office/PDF: từ chối.
Bước 4 cố đổi số bản, hướng ngang, kích thước dịch vụ sai hoặc thêm gia công: không cho tạo đơn không khớp.
Muốn đổi số ảnh/kích thước sau khi ghép: tải ảnh và ghép lại từ bước 1.
Không có bước AI sửa mặt/xóa phông hoặc cam kết ảnh đạt chuẩn giấy tờ trong luồng hiện tại.

**Tiền điều kiện**
Khách có phiên, có JPG/PNG và dịch vụ ảnh thẻ phù hợp trong bảng giá chi nhánh.

**Hậu điều kiện**
Có PDF ảnh thẻ và số ảnh nhất quán; chưa in thật cho tới khi đơn vào quy trình máy.

**Căn cứ** FILE, PHOTO_RULE, SQL_PRICE, SQL_CREATE, FILE_UI

## HT-4.3-04 Cấu hình quy cách in

**Use case**
Cấu hình quy cách in

**Use case ID**
HT-4.3-04

**Tác nhân chính**
Khách hàng

**Tổng quan**
Chọn các dịch vụ đang bán theo bảng giá chi nhánh, không lấy mọi vật tư tồn kho làm quy cách in.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Sau một trong HT-4.3-01/02/03; kết quả dùng cho chi nhánh HT-4.3-05, báo giá HT-4.3-09 và tạo đơn HT-4.3-10.

**Dòng sự kiện chính**
1. App tải chi nhánh hoạt động và GET api/danh-muc/bang-gia với mã chi nhánh cụ thể.
2. Khách chọn giấy/dịch vụ, mặt in, màu, hướng đứng/ngang, số bản và ghi chú.
3. Khách chọn gia công phù hợp và in thường/hỏa tốc; App kiểm tra các cặp loại trừ.
4. App gửi báo giá sơ bộ với mã file, mã dịch vụ, số bản, hướng và gia công.
5. API/SQL kiểm lại dịch vụ, file, số trang và ràng buộc trước khi trả tiền tạm tính.
6. Khách tiếp tục chọn chi nhánh và cách nhận; quy cách được giữ lại.

**Dòng sự kiện phụ**
Bước 1 chưa có bảng giá: hiển thị lỗi/thử lại; không gọi bang-gia với mã 0.
Bước 3 bìa kiếng + lò xo hợp lệ; plastic có thể đi lò xo; lò xo + kim góc bị loại trừ.
Bước 3 kim góc tối đa 50 tờ mỗi bản; hai mặt tính ceil(số trang/2), không nhân số bản để kiểm giới hạn.
Dịch vụ gia công cùng nhóm vẫn loại trừ theo cấu hình, ngoại lệ rõ là bìa kiếng đi lò xo.
Ảnh thẻ dùng ràng buộc riêng HT-4.3-03.
Giấy A0 mới trong kho chưa có dịch vụ hoặc máy phù hợp không tự xuất hiện/in được.

**Tiền điều kiện**
Có FileIn sẵn sàng; đọc được danh sách chi nhánh và bảng giá có hiệu lực.

**Hậu điều kiện**
Lựa chọn hợp lệ được chuyển bước; chưa lưu đơn. Số trang báo giá do máy chủ đọc từ FileIn, không tin số trang client.

**Căn cứ** CONFIG_UI, CATALOG, PRINT_RULE, SQL_PRICE, QUOTE

## HT-4.3-05 Chọn chi nhánh và hình thức nhận

**Use case**
Chọn chi nhánh và hình thức nhận

**Use case ID**
HT-4.3-05

**Tác nhân chính**
Khách hàng

**Tổng quan**
Chọn cửa hàng đáp ứng cấu hình và nhận tại quầy hoặc giao tận nơi.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Bao gồm chọn nơi xử lý cho HT-4.3-10. Chỉ nhánh giao hàng mới dùng HT-4.3-07 và HT-4.3-08.

**Dòng sự kiện chính**
1. App POST api/don-hang/chi-nhanh-phu-hop với các mã dịch vụ in.
2. API đọc chi nhánh hoạt động, bảng giá và năng lực máy; trả cờ đáp ứng cùng lý do khi không đáp ứng.
3. App gợi ý chi nhánh phù hợp; khách chọn chi nhánh có thể in.
4. Khách chọn TAI_QUAY hoặc GIAO_HANG; khi giao, cung cấp điểm nhận và điện thoại.
5. App xóa báo giá cũ và gọi lại báo giá theo chi nhánh/hình thức mới.

**Dòng sự kiện phụ**
Bước 2 không chi nhánh nào đáp ứng: chưa cho tạo đơn, quay lại quy cách.
Bước 3 chi nhánh không có giá đủ hoặc thiếu máy A3/màu/hai mặt: vô hiệu hóa kèm lý do.
Bước 4 chưa có tọa độ xác thực: chưa tính được phí, yêu cầu lấy vị trí hoặc chuyển nhận tại quầy.
API tạo đơn kiểm tra lại tình trạng chi nhánh/năng lực; kết quả bộ lọc trước đó không bảo đảm máy còn sẵn sau này.
Bộ lọc năng lực này không cam kết giấy luôn đủ, driver khỏe hoặc connector đang online.

**Tiền điều kiện**
Có quy cách và mã dịch vụ đã chọn.

**Hậu điều kiện**
Có chi nhánh và cách nhận hợp lệ cho bước báo giá; chưa tạo đơn.

**Căn cứ** ORDER, CATALOG, BRANCH_UI

## HT-4.3-06 Áp dụng mã khuyến mãi

**Use case**
Áp dụng mã khuyến mãi

**Use case ID**
HT-4.3-06

**Tác nhân chính**
Khách hàng

**Tổng quan**
Kiểm tra và tính giảm trên báo giá/đơn của chi nhánh đã chọn.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Lựa chọn tùy ý trong HT-4.3-09; không phải mọi đơn bắt buộc nhập mã. Chốt điều kiện lại trong HT-4.3-10.

**Dòng sự kiện chính**
1. Khách nhập mã ở bước chọn chi nhánh.
2. App gọi lại POST api/bao-gia; API trim và chuyển mã thành chữ hoa.
3. sp_TinhTien kiểm chi nhánh, trạng thái, thời gian, lượt và mức tiền tối thiểu.
4. Nếu hợp lệ, tính phần trăm tiền in cộng gia công và giới hạn giảm tối đa; trả mức giảm, tổng và kết quả mã.
5. App hiển thị kết quả; khi khách tạo đơn, SQL kiểm lại và tăng DaDung cho mã được áp dụng.

**Dòng sự kiện phụ**
Bước 3 mã không tồn tại/hết hạn/hết lượt: trả lý do, giảm 0; vẫn được đặt đơn giá thường.
Bước 3 chưa đạt tối thiểu: báo lý do; khách sửa nhu cầu hoặc tiếp tục không giảm.
Đổi chi nhánh hoặc quy cách: xóa kết quả cũ, tính lại bước 2.
Chỉ xem báo giá chưa tăng lượt; không mô tả giữ chỗ mã trước khi đặt.
Hủy đơn không có bước trả lại lượt mã hiện hành; đây là giới hạn cần biết khi trình bày.

**Tiền điều kiện**
Có file, quy cách và chi nhánh; khách có mã muốn dùng.

**Hậu điều kiện**
Báo giá có trạng thái mã và mức giảm chính xác tại thời điểm tính; báo giá không tạo đơn.

**Căn cứ** SQL_PRICE, SQL_CREATE, QUOTE, BRANCH_UI

## HT-4.3-07 Lấy địa chỉ giao hàng từ vị trí hiện tại

**Use case**
Lấy địa chỉ giao hàng từ vị trí hiện tại

**Use case ID**
HT-4.3-07

**Tác nhân chính**
Khách hàng; Geoapify hỗ trợ

**Tổng quan**
Xin quyền vị trí theo thao tác khách, chuyển GPS thành địa chỉ chính tự điền và cho bổ sung chi tiết.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Dùng khi GIAO_HANG trong HT-4.3-05. Không có nút tìm địa chỉ thủ công riêng trên App hiện tại.

**Dòng sự kiện chính**
1. Khách bấm Dùng vị trí hiện tại; App xóa điểm/báo giá cũ để tránh đặt sai điểm.
2. App xin quyền nếu cần, lấy GPS chính xác; kiểm tra số hữu hạn và sai số không quá 150m.
3. App POST api/giao-hang/dia-chi với tọa độ; API gọi reverse geocoding và cấp mã vị trí gắn khách.
4. App điền địa chỉ chính từ phản hồi, giữ tọa độ/mã; khách kiểm tra và nhập số nhà/tầng/phòng, ghi chú riêng.
5. Địa chỉ cùng mã vị trí được dùng để báo giá; chi tiết không thay đổi điểm tính tuyến.

**Dòng sự kiện phụ**
Bước 2 GPS tắt/từ chối quyền/hết thời gian hoặc sai số quá lớn: báo cách bật và thử lại; không dùng tọa độ cũ.
Bước 3 dịch vụ chưa cấu hình/lỗi/hết hạn mức: báo không lấy được địa chỉ; khách thử lại hoặc nhận tại quầy.
Bước 4 địa chỉ/tọa độ không hợp lệ: chưa chấp nhận làm điểm giao.
Mã vị trí hết hiệu lực hoặc địa chỉ chính bị thay không khớp mã: API báo giá từ chối; phải lấy vị trí lại.
Địa chỉ mặc định có sẵn chỉ là chuỗi, không thay thế được mã vị trí xác thực để giao.

**Tiền điều kiện**
Khách chọn giao tận nơi, thiết bị có dịch vụ vị trí; API đã cấu hình Geoapify.

**Hậu điều kiện**
Có địa chỉ chính và tọa độ xác thực với mã vị trí tạm 30 phút; lỗi thì chưa có điểm giao hợp lệ.

**Căn cứ** LOCATION_UI, GPS, SHIP, GEO

## HT-4.3-08 Ước tính phí giao hàng

**Use case**
Ước tính phí giao hàng

**Use case ID**
HT-4.3-08

**Tác nhân chính**
Khách hàng; Geoapify hỗ trợ tuyến đường

**Tổng quan**
Tính phí ship nội bộ từ quãng đường tuyến scooter, không gọi API báo cước hay tạo đơn của hãng ngoài.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Nhánh có điều kiện trong HT-4.3-09 khi GIAO_HANG; không chạy khi khách nhận tại quầy.

**Dòng sự kiện chính**
1. App gửi báo giá gồm chi nhánh, địa chỉ, tọa độ và mã vị trí.
2. API kiểm tra mã điểm nhận; GeoapifyService yêu cầu tuyến scooter từ chi nhánh tới điểm giao.
3. Dịch vụ đọc khoảng cách và thời gian, kiểm tra bán kính phục vụ.
4. QuyTacGiaoHang tính phí từ cấu hình cửa hàng và làm tròn.
5. API trả khoảng cách/phút di chuyển/phí và giữ snapshot báo phí 15 phút với mã báo giá giao.
6. Khi đặt đơn, API đọc snapshot gắn đúng khách, chi nhánh, địa chỉ, tọa độ; không nhận số tiền ship tùy ý từ client.

**Dòng sự kiện phụ**
Bước 2 không có tuyến hoặc dịch vụ lỗi: báo lỗi, không tính khoảng cách đường chim bay thay thế âm thầm.
Bước 3 vượt bán kính: từ chối giao, cho chọn lại điểm hoặc nhận tại quầy.
Mặc định code: 15.000đ trong 2km, thêm 4.000đ/km vượt, làm tròn lên 1.000đ; giá thực tế lấy cấu hình, không gọi đây là bảng giá hãng vận chuyển.
Bước 6 snapshot hết hạn hoặc dữ liệu đổi: yêu cầu báo giá lại.
Không có API đặt shipper ngoài; shipper nội bộ thực hiện giao sau in.

**Tiền điều kiện**
Có chi nhánh tọa độ/bán kính hợp lệ và mã vị trí giao thuộc khách.

**Hậu điều kiện**
Có phí và vé báo giá giao tạm nếu hợp lệ; chưa tạo đơn hoặc thu tiền.

**Căn cứ** GEO, SHIP_RULE, QUOTE, ORDER

## HT-4.3-09 Xem báo giá và giờ nhận dự kiến

**Use case**
Xem báo giá và giờ nhận dự kiến

**Use case ID**
HT-4.3-09

**Tác nhân chính**
Khách hàng

**Tổng quan**
Tính tiền và ước tính thời gian trước khi đặt; kết quả là dự kiến theo trạng thái hiện tại.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Dùng file/quy cách/chi nhánh; mã giảm tùy chọn HT-4.3-06 và phí giao có điều kiện HT-4.3-08.

**Dòng sự kiện chính**
1. Khách chọn hoặc thay quy cách, chi nhánh, cách nhận, tốc độ và mã giảm.
2. App chờ thay đổi ngắn rồi POST api/bao-gia; chỉ nhận phản hồi của lựa chọn mới nhất.
3. API đọc số trang thật, lập danh sách in/gia công, tính tuyến giao nếu cần.
4. sp_BaoGia dùng sp_TinhTien trả tiền in, gia công, phụ phí, ship, giảm, tổng và NgayHenTra.
5. App hiển thị số tiền, kết quả mã, thời điểm dự kiến và thời gian di chuyển khi có.
6. Khách kiểm tra và chuyển HT-4.3-10 nếu đồng ý.

**Dòng sự kiện phụ**
Bước 3 file không thuộc khách/chưa sẵn sàng hoặc giá không có: từ chối, yêu cầu chọn lại.
Phụ phí hỏa tốc hiện 10.000đ/đơn; ước tính dùng tốc độ máy nhanh nhất hoạt động, số trang hàng đợi; in gấp bỏ phần trang chờ trong công thức.
Giờ ước tính không phải cam kết SLA và chưa mô hình đầy đủ thời gian gia công, giờ đóng cửa hay lỗi máy; không trình bày như lịch hẹn chắc chắn.
Đổi địa chỉ/chi nhánh làm vô hiệu báo giá cũ; phải đợi phản hồi mới.
Báo giá không lưu DonHang; lúc đặt tính lại nên không bảo đảm mọi mức giá đã được khóa.

**Tiền điều kiện**
File của khách sẵn sàng, dịch vụ có giá tại chi nhánh; giao hàng có điểm nhận hợp lệ.

**Hậu điều kiện**
Có bảng tiền và thời gian dự kiến hiện hành; lỗi thì nút đặt không dựa vào báo giá cũ.

**Căn cứ** QUOTE, SQL_PRICE, SQL_QUOTE, SQL_ETA, BRANCH_UI

## HT-4.3-10 Xác nhận đặt in trên App

**Use case**
Xác nhận đặt in trên App

**Use case ID**
HT-4.3-10

**Tác nhân chính**
Khách hàng

**Tổng quan**
Lưu đơn thật từ dữ liệu đã chọn và chuyển sang thanh toán trước bằng VietQR.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Kết thúc đặt in, mở thanh toán 4.4; tạo đơn không đồng nghĩa đã trả tiền hoặc đã in.

**Dòng sự kiện chính**
1. Khách kiểm tra tổng tiền và bấm Đặt in; App khóa bấm lặp trong khi gửi.
2. App POST api/don-hang với mã file/dịch vụ, số bản, hướng, gia công, chi nhánh, cách nhận và ghi chú.
3. API lấy khách từ JWT, ép phương thức VIETQR; kiểm chi nhánh, file thuộc khách, trang thật và năng lực máy.
4. Nếu giao, API xác thực snapshot phí theo đúng dữ liệu; SQL tính lại tiền và điều kiện dịch vụ/khuyến mãi.
5. sp_TaoDonHang lưu đơn, chi tiết in/gia công và thanh toán chưa trả trong giao dịch; lưu hướng giấy và giá của đơn.
6. API trả mã DH, tổng và yêu cầu trả trong 15 phút; thông báo đơn mới cho chi nhánh.
7. App thay màn sang Thanh toán để lấy QR; chưa gửi lệnh in nếu chưa nhận tiền.

**Dòng sự kiện phụ**
Bước 3 giả chủ file, chi nhánh dừng hoạt động hoặc cấu hình không đáp ứng: từ chối, không tạo đơn.
Bước 4 giá/khuyến mãi đã đổi: dùng kiểm tra và giá lúc tạo; báo giá sơ bộ không phải hợp đồng khóa mọi giá.
Bước 4 snapshot giao hết hạn/đổi điểm: yêu cầu tính lại trước khi đặt.
Bước 5 SQL thất bại: không báo tạo thành công; giao dịch đơn được rollback.
Mất phản hồi sau khi gửi có thể chưa biết đơn đã lưu; kiểm tra danh sách trước khi đặt lại. API này chưa thể hiện khóa idempotency cho mọi lần retry.
Đơn App không có lựa chọn COD, dù payload client cố gửi phương thức khác.

**Tiền điều kiện**
Có phiên CUSTOMER, FileIn sẵn sàng, lựa chọn hợp lệ và báo giá mới; giao hàng có snapshot phí.

**Hậu điều kiện**
Thành công có mã đơn CHO_XAC_NHAN và thanh toán CHUA_THANH_TOAN; chưa nhận tiền hoặc in. Lỗi không báo thành công.

**Căn cứ** ORDER, SQL_CREATE, BRANCH_UI, PAYMENT

## Đối chiếu nhãn trên hình gốc

| Nhãn | Đặc tả | Kết luận | Cách hiểu và điều chỉnh |
| --- | --- | --- | --- |
| NV: Đặt đơn in trực tuyến | NV-3.2-01 | Đúng | Kịch bản tổng hợp; các bước kỹ thuật được tách ở hình hệ thống. |
| NV: Tải tài liệu và lựa chọn quy cách in | NV-3.2-02 | Đúng | Có cả mẫu đơn và ảnh thẻ thay thế tải tài liệu thường. |
| NV: Chuyển khoản trước | NV-3.2-03 | Đúng | Đơn App trả QR trước cả tiền ship; không COD giao hàng. |
| NV: Tiếp nhận đơn in | NV-3.2-04 | Chỉnh diễn giải | Có xếp in tự động; không mọi đơn đều cần nhân viên bấm tiếp nhận. |
| NV: Áp dụng khuyến mại | NV-3.2-05 | Tùy chọn | Nhánh bổ sung khi khách có mã; sai mã vẫn đặt giá thường. |
| NV: Giao hàng tận nơi | NV-3.2-06 | Tùy chọn | Liên kết sang 3.4/4.10; không thu tiền lúc giao. |
| HT: Tải tài liệu in | HT-4.3-01 | Đúng | Tạo FileIn trước, chưa tạo đơn. |
| HT: In mẫu đơn trống | HT-4.3-02 | Đúng | Thư viện có phiên bản, xem trước và chọn bản PDF. |
| HT: In ảnh thẻ theo kích thước | HT-4.3-03 | Đúng | Ghép PDF và tính theo số ảnh, không số trang. |
| HT: Cấu hình quy cách | HT-4.3-04 | Đúng | Theo dịch vụ/giá bán, có kiểm gia công và hướng giấy. |
| HT: Chọn chi nhánh và hình thức nhận | HT-4.3-05 | Đúng | Năng lực và giá được kiểm, không bảo đảm driver/giấy luôn sẵn. |
| HT: Áp dụng mã khuyến mãi | HT-4.3-06 | Đúng | Kiểm trong báo giá và tạo đơn, không có endpoint áp mã độc lập. |
| HT: Lấy địa chỉ giao hàng | HT-4.3-07 | Đúng theo UI hiện tại | GPS chủ động, reverse địa chỉ tự điền; không nút tìm địa chỉ riêng. |
| HT: Ước tính phí giao hàng | HT-4.3-08 | Chỉnh vai trò dịch vụ | Geoapify trả tuyến, cửa hàng tự tính phí; chỉ chạy nhánh giao hàng. |
| HT: Xem báo giá và giờ nhận dự kiến | HT-4.3-09 | Đúng với giới hạn | Thời điểm dự kiến, không SLA; tính lại lúc đặt. |
| HT: Xác nhận đặt in | HT-4.3-10 | Đúng | Lưu đơn, bắt đầu thanh toán; chưa bắt đầu in chỉ vì tạo đơn. |

## Căn cứ mã nguồn

- **AUTO_PRINT** [MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs:17) — Xếp máy sau khi trả tiền; giữ chờ nếu chưa đủ điều kiện.
- **BRANCH_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/chon_chi_nhanh_screen.dart:19](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/chon_chi_nhanh_screen.dart:19) — Chọn chi nhánh/nhận, tính lại giá và tạo đơn.
- **CATALOG** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DanhMucController.cs:13](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DanhMucController.cs:13) — Chi nhánh hoạt động và dịch vụ có giá hiển thị.
- **CONFIG_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/cau_hinh_in_screen.dart:27](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/cau_hinh_in_screen.dart:27) — Bảng giá theo chi nhánh, giấy/dịch vụ và các quy cách.
- **FILE** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/FileInController.cs:28](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/FileInController.cs:28) — Quyền tải, chuyển PDF/đếm trang, ảnh thẻ và chọn mẫu có phiên bản.
- **FILE_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/chon_file_screen.dart:25](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/chon_file_screen.dart:25) — Ba nguồn file, chọn mẫu/xem trước, mỗi lượt một file.
- **GEO** [MaNguon/PhotocopyChain/PhotocopyApi/GiaoHang/GeoapifyService.cs:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/GiaoHang/GeoapifyService.cs:11) — Reverse GPS, routing scooter, vé vị trí và snapshot phí giao.
- **GPS** [MaNguon/photocopy_mobile/lib/dich_vu_vi_tri.dart:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/dich_vu_vi_tri.dart:11) — Xin quyền và đọc GPS khi người dùng chủ động yêu cầu.
- **LOCATION_UI** [MaNguon/photocopy_mobile/lib/widgets/dia_chi_giao.dart:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/widgets/dia_chi_giao.dart:11) — Tự điền địa chỉ GPS, sai số, chi tiết và vô hiệu điểm cũ.
- **ORDER** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32) — Đặt App, lọc chi nhánh, chi tiết, tiếp nhận, hủy và đánh giá.
- **PAYMENT** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30) — QR, chữ ký webhook, đối soát, tiền đến muộn và điều hướng.
- **PHOTO_RULE** [MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyCachAnhThe.cs:5](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyCachAnhThe.cs:5) — Năm kích thước ảnh thẻ.
- **PRINT_RULE** [MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyTacIn.cs:3](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyTacIn.cs:3) — Gia công tương thích và số tờ kim góc.
- **QUOTE** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/BaoGiaController.cs:33](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/BaoGiaController.cs:33) — Báo giá từ file thật, lấy tuyến và giữ phí giao.
- **SHIP** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/GiaoHangController.cs:18](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/GiaoHangController.cs:18) — Điểm nhận, phân công/nhận và kết quả giao không COD.
- **SHIP_RULE** [MaNguon/PhotocopyChain/PhotocopyApi/GiaoHang/QuyTacGiaoHang.cs:3](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/GiaoHang/QuyTacGiaoHang.cs:3) — Phí nội bộ, bán kính, điều kiện nhận và ba lượt giao.
- **SQL_ACCEPT** [Database/04_StoredProcedure.sql:351](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:351) — Ca, nhân viên, trạng thái và tiền khi tiếp nhận thủ công.
- **SQL_CREATE** [Database/04_StoredProcedure.sql:226](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:226) — Giao dịch đơn, chi tiết, hướng giấy, giá, thanh toán và lượt mã.
- **SQL_ETA** [Database/03_View_Function.sql:41](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/03_View_Function.sql:41) — Công thức ước tính dùng tốc độ/hàng đợi.
- **SQL_PAY** [Database/04_StoredProcedure.sql:320](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:320) — Ghi nhận thanh toán chuyển khoản và thông báo.
- **SQL_PRICE** [Database/04_StoredProcedure.sql:71](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:71) — Tiền in/gia công/ảnh, phụ phí, giảm giá và ràng buộc.
- **SQL_QUOTE** [Database/04_StoredProcedure.sql:203](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:203) — Báo giá, tổng tiền và thời gian dự kiến.
- **TEMPLATE** [MaNguon/PhotocopyChain/PhotocopyApi/DichVu/ThuVienMauDon.cs:9](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DichVu/ThuVienMauDon.cs:9) — Thư viện động, phiên bản và tài sản PDF/xem trước.
- **TIMEOUT** [MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:12](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:12) — Chạy hủy quá hạn khoảng mỗi phút, dọn file là tác vụ riêng.
