# Task 1 Đặc tả hủy đơn và hoàn tiền

**Phạm vi hoàn thành:** hai hình `Usecase/NghiepVu/HuyDon_HoanTien.png` và `Usecase/HeThong/HuyDon_HoanTien.png`; 5 đặc tả nghiệp vụ, 5 đặc tả hệ thống và đối chiếu toàn bộ 13 nhãn trong hai ảnh. Ngày đối chiếu: 26/10/2026.

Mẫu nghiệp vụ dùng ba phần: giới thiệu, các dòng cơ bản, các dòng thay thế. Mẫu hệ thống dùng đủ mười trường trong tài liệu tham khảo. Nội dung dưới đây phản ánh code hiện hành, không suy diễn thêm bước duyệt hủy hoặc API chuyển tiền. Ảnh gốc được giữ nguyên; các điểm khác biệt được ghi ở bảng cuối.

## Quy tắc dùng chung

- Khách đúng chủ đơn; quản lý/CEO đúng phạm vi. Chỉ hủy ở `CHO_XAC_NHAN` hoặc `DA_XAC_NHAN`. Từ `DANG_IN` trở đi không hủy/hoàn qua luồng thông thường.
- Đơn đã hủy vẫn `DA_HUY` sau khi nhận tiền muộn hoặc xác nhận hoàn. Không có trạng thái mới `CHO_HOAN_TIEN`; chờ hoàn là kết quả lọc đơn hủy trước in có thanh toán `DA_THANH_TOAN`.
- Chỉ CEO xem danh sách cần hoàn và xác nhận. Khách cung cấp thông tin ngân hàng; không tự xác nhận đã chuyển hoàn.
- Ghi chú nhận hoàn có ba dòng; lý do tối đa 100 ký tự, ngân hàng 60 ký tự, tài khoản 6–30 chữ số. Tài khoản là chuỗi, giữ số 0 đầu. Chưa thanh toán thì lý do hủy tối đa 255 ký tự và không bắt buộc ngân hàng.
- Chuyển tiền thật do CEO thực hiện bên ngoài. Phần mềm chỉ ghi nhận xác nhận, chưa lưu chứng từ ngân hàng hoặc tự kiểm chứng tiền đã chuyển trả.

## Đặc tả nghiệp vụ

### NV-3.5-01 Giải quyết yêu cầu hủy đơn

**Giới thiệu về use case nghiệp vụ**
Khách hàng không còn nhu cầu in hoặc quản lý quyết định hủy đơn trong phạm vi phụ trách. Mục tiêu là kết thúc đúng đơn chưa bắt đầu in, giữ thông tin để đối soát và xác định khoản cần hoàn. Khách là tác nhân ngoài tổ chức; quản lý và CEO là người xử lý nội bộ. Việc chấp nhận hủy hiện được kiểm tra trực tiếp, không có bước quản lý duyệt một yêu cầu đang chờ.

**Các dòng cơ bản**
1. Khách hoặc người quản lý xác định đơn cần hủy.
2. Người yêu cầu cung cấp lý do hủy. Nếu khách đã trả tiền, khách cung cấp thêm lý do hoàn, ngân hàng và số tài khoản nhận tiền.
3. Cửa hàng kiểm tra đúng người, đúng chi nhánh và đơn vẫn ở giai đoạn chờ tiếp nhận hoặc đã tiếp nhận nhưng chưa in.
4. Khi hợp lệ, cửa hàng ghi nhận đơn đã hủy và giữ lý do cùng thông tin nhận hoàn nếu đã cung cấp.
5. Với đơn chưa nhận tiền, quy trình hủy kết thúc, không phát sinh khoản hoàn.
6. Với đơn đã nhận tiền, khoản phải trả lại được đưa vào danh sách để CEO xử lý. Khách nhận kết quả hủy; hủy đơn không đồng nghĩa đã chuyển hoàn tiền.

**Các dòng thay thế**
- Tại bước 3, đơn đã bắt đầu in, đã in, đang giao, giao thất bại, hoàn tất hoặc đã hủy: từ chối hủy; kết thúc mà không thay đổi đơn.
- Tại bước 2, lý do hoặc thông tin nhận hoàn bắt buộc còn thiếu: yêu cầu bổ sung rồi quay lại bước 2.
- Quản lý/CEO hủy đơn đã trả nhưng chưa có tài khoản nhận: vẫn ghi nhận hủy hợp lệ; chờ khách bổ sung trên App trước khi CEO xác nhận hoàn.
- Tiền được ngân hàng báo về sau lúc hủy: giữ đơn đã hủy và chuyển sang giải quyết khoản hoàn, không khởi động lại việc in.
- Không đúng chủ đơn hoặc ngoài phạm vi chi nhánh: không chấp nhận yêu cầu; không tiết lộ thông tin đơn khác.

### NV-3.5-02 Rà soát danh sách đơn chờ hoàn tiền

**Giới thiệu về use case nghiệp vụ**
CEO xác định những đơn đã hủy trước khi in nhưng cửa hàng đã nhận tiền để tổ chức chuyển trả. Mục tiêu là tránh bỏ sót tiền của khách. Quản lý chi nhánh không có chức năng xem danh sách hoàn tiền chuyên biệt này trong phần mềm hiện tại.

**Các dòng cơ bản**
1. CEO xác định phạm vi cần rà soát: toàn chuỗi hoặc chi nhánh đang chọn.
2. Cửa hàng tập hợp các đơn đã hủy trước khi in, đã nhận tiền và chưa ghi nhận hoàn.
3. CEO xem mã đơn, khách hàng, số điện thoại, chi nhánh, số tiền và ghi chú.
4. CEO kiểm tra ghi chú gồm lý do hoàn tiền, ngân hàng và số tài khoản.
5. CEO phân biệt khoản đã đủ thông tin để chuyển trả và khoản phải chờ khách bổ sung.
6. Danh sách được làm mới sau khi khách cập nhật hoặc sau khi xác nhận hoàn; chưa có chuyển tiền trong bước rà soát.

**Các dòng thay thế**
- Bước 2 không có đơn phù hợp: thông báo danh sách trống, kết thúc.
- Bước 4 thiếu hoặc sai định dạng thông tin nhận hoàn: chờ khách cập nhật; không xác nhận đã hoàn.
- Khách vừa cập nhật: CEO làm mới danh sách rồi quay lại bước 3.
- CEO đang xem một chi nhánh: chỉ rà soát đơn thuộc phạm vi đó.
- Đơn chưa trả tiền hoặc đã xác nhận hoàn: không đưa vào danh sách cần hoàn. Đơn hủy cũ đã in còn mã máy không xử lý theo luồng hoàn thông thường.

### NV-3.5-03 Kết thúc đơn quá hạn thanh toán

**Giới thiệu về use case nghiệp vụ**
Đơn chuyển khoản còn chờ tiếp nhận nhưng quá thời hạn thanh toán phải được kết thúc để tránh giữ yêu cầu không còn hiệu lực. Đây là chính sách nghiệp vụ được phần mềm thực hiện tự động; không yêu cầu quản lý bấm nút hủy quá hạn.

**Các dòng cơ bản**
1. Cửa hàng quy định thời hạn chuyển khoản là 15 phút tính từ thời điểm đặt đơn.
2. Trong các lượt kiểm tra định kỳ, phần mềm xác định đơn còn chờ tiếp nhận, chọn VietQR và chưa được ghi nhận thanh toán.
3. Khi thời điểm đặt cộng 15 phút đã nhỏ hơn thời điểm kiểm tra, đơn được chuyển sang đã hủy.
4. Lý do quá hạn được lưu trong ghi chú; thông tin đơn vẫn được giữ để tra cứu.
5. Khách xem lại đơn sẽ thấy trạng thái đã hủy; vì chưa nhận tiền nên chưa có khoản hoàn.

**Các dòng thay thế**
- Chưa vượt hạn hoặc đơn đã được nhận tiền/đã rời trạng thái chờ tiếp nhận: bỏ qua trong lượt kiểm tra, đơn giữ nguyên.
- Bộ kiểm tra chạy khoảng mỗi phút nên không cam kết đổi trạng thái đúng từng giây ở phút thứ 15.
- Tiền hợp lệ đến sau khi đơn bị hủy: ghi nhận khoản nhận tiền để hoàn, không tự khôi phục đơn; khách bổ sung tài khoản nếu còn thiếu.
- Khi bộ kiểm tra gặp lỗi kết nối: ghi nhận lỗi và thử ở lượt sau; không giả định đơn đã được xử lý thành công.

### NV-3.5-04 Chuyển trả tiền cho khách

**Giới thiệu về use case nghiệp vụ**
CEO chuyển trả toàn bộ khoản tiền đã nhận của đơn hủy trước in sau khi đã có thông tin nhận hoàn. Ngân hàng là tổ chức ngoài tham gia chuyển khoản thực tế. Phần mềm PhotocopyChain không thực hiện chuyển khoản và không gọi API hoàn tiền tự động.

**Các dòng cơ bản**
1. CEO chọn một khoản chờ hoàn đã có đủ thông tin nhận tiền.
2. CEO đối chiếu đúng khách, mã đơn, số tiền, ngân hàng và số tài khoản.
3. CEO sử dụng kênh ngân hàng bên ngoài phần mềm để chuyển trả tiền.
4. CEO kiểm tra kết quả chuyển tiền trên kênh ngân hàng.
5. Chỉ khi chuyển trả thực tế thành công, CEO thực hiện nghiệp vụ xác nhận đã hoàn trên Web.
6. Cửa hàng giữ nguyên trạng thái đơn đã hủy; việc chuyển trả không làm đơn được in lại.

**Các dòng thay thế**
- Thiếu hoặc nghi ngờ sai tài khoản tại bước 2: liên hệ khách để làm rõ và chờ cập nhật; chưa chuyển tiền.
- Ngân hàng báo thất bại hoặc kết quả chưa chắc chắn: kiểm tra giao dịch ngân hàng trước khi thử lại; chưa xác nhận đã hoàn trên Web.
- CEO đã chuyển tiền nhưng Web mất kết nối: kiểm tra trạng thái ghi nhận trước khi xác nhận lại; không chuyển ngân hàng thêm lần nữa chỉ vì Web chưa phản hồi.
- Không có trường lưu chứng từ/mã giao dịch hoàn hoặc cơ chế xác minh tiền ra ngân hàng trong chức năng hiện tại; không mô tả phần mềm như đã tự đối soát được khoản chuyển trả.

### NV-3.5-05 Xác nhận khoản tiền đã chuyển hoàn

**Giới thiệu về use case nghiệp vụ**
Sau khi chuyển trả thực tế thành công, CEO xác nhận kết quả để kết thúc khoản cần hoàn trong sổ theo dõi của phần mềm. Đây là xác nhận của CEO, không phải thao tác khách hàng tự đánh dấu đã nhận tiền.

**Các dòng cơ bản**
1. CEO mở lại đúng đơn trong danh sách chờ xác nhận hoàn tiền.
2. CEO kiểm tra lần cuối số tiền và ba dòng thông tin nhận hoàn.
3. CEO chọn Đã chuyển hoàn, đọc nội dung xác nhận và đồng ý.
4. Cửa hàng ghi nhận thanh toán đã hoàn tiền khi đơn vẫn đủ điều kiện và chưa được xác nhận trước đó.
5. Đơn vẫn đã hủy, được loại khỏi danh sách khoản cần hoàn và có cập nhật kết quả cho người liên quan.
6. Khách xem lại trạng thái đã hoàn tiền trên App; thông tin ghi chú vẫn gắn với đơn để tra cứu.

**Các dòng thay thế**
- CEO chọn hủy hộp xác nhận: quay về danh sách, không đổi trạng thái.
- Chưa đủ lý do/ngân hàng/số tài khoản hoặc đơn không còn đủ điều kiện: từ chối xác nhận và yêu cầu kiểm tra lại.
- Khoản đã được xác nhận hoàn: không ghi nhận hoàn thêm lần nữa.
- Khách cố sửa tài khoản sau khi đã xác nhận hoàn: không cho cập nhật qua chức năng bổ sung thông tin.
- Việc xác nhận không tự chuyển tiền và không tạo bằng chứng ngân hàng; chỉ được thực hiện sau bước chuyển trả thực tế.

## Đặc tả chức năng hệ thống

### HT-4.11-01 Hủy đơn hàng trước khi bắt đầu in

**Use case**
Hủy đơn hàng trước khi bắt đầu in

**Use case ID**
HT-4.11-01

**Tác nhân chính**
Khách hàng; Quản lý chi nhánh; CEO

**Tổng quan**
Kết thúc đúng đơn còn CHO_XAC_NHAN hoặc DA_XAC_NHAN. Kích hoạt khi người dùng chọn Hủy đơn trên màn QR/tiến độ của App hoặc Hàng đợi đơn hàng trên Web.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Tác nhân trực tiếp: khách đúng chủ; quản lý/CEO đúng phạm vi. Kiểm tra trạng thái và nhập lý do là các bước bắt buộc của use case này. Hoàn tiền là quy trình riêng, chỉ phát sinh khi thực sự đã nhận tiền; không coi việc hủy luôn include việc hoàn.

**Dòng sự kiện chính**
1. Tác nhân mở đúng đơn và chọn Hủy đơn.
2. Giao diện mở hộp nhập lý do; với khách đã trả tiền, hiển thị thêm ngân hàng và số tài khoản nhận hoàn.
3. Tác nhân nhập dữ liệu, đọc cảnh báo chỉ hủy trước in và xác nhận.
4. API kiểm quyền, mở transaction và khóa đọc đơn để tránh đồng thời bắt đầu in.
5. API kiểm chủ đơn hoặc phạm vi chi nhánh và trạng thái chỉ thuộc hai bước trước in; kiểm dữ liệu bắt buộc.
6. SQL kiểm lại điều kiện, đổi đơn sang DA_HUY, lưu ghi chú và xóa MaMayIn đã phân công ở giai đoạn trước in.
7. Hệ thống commit, thực hiện chính sách dọn tệp kết thúc, phát cập nhật và trả kết quả có hay không khoản cần hoàn.
8. Giao diện tải lại đơn đã hủy; nếu đã nhận tiền thì khách chờ CEO xử lý hoàn, không hiển thị như đã chuyển tiền hoàn.

**Dòng sự kiện phụ**
Biến thể 2a — Đơn chưa trả: chỉ nhập lý do hủy, tối đa 255 ký tự; tiếp tục bước 3.
Biến thể 2b — Khách đã trả: lý do hoàn tối đa 100 ký tự, ngân hàng 60 ký tự, tài khoản 6–30 chữ số; ghi chú gồm ba dòng. Tiếp tục bước 3.
Biến thể 5a — Quản lý/CEO hủy đơn đã trả nhưng chưa có tài khoản: ghi nhận hủy trước in; khách bổ sung theo HT-4.11-03.
Ngoại lệ 5b — Đơn đã sang DANG_IN hoặc bước sau in/giao, hoàn tất hay đã hủy: từ chối, không thay đổi đơn; kết thúc.
Ngoại lệ 5c — Không đúng chủ hoặc sai phạm vi: từ chối; không thay đổi dữ liệu; kết thúc.
Ngoại lệ 5d — Dữ liệu thiếu/sai: hiển thị lỗi, người dùng sửa và quay lại bước 2; chưa hủy đơn.
Ngoại lệ 7a — Mất phản hồi: người dùng tải lại để xác định trạng thái đã commit; không coi thao tác chắc chắn thất bại và không chuyển tiền thủ công lặp.

**Tiền điều kiện**
Đã đăng nhập với vai trò cho phép. Đơn tồn tại, thuộc chủ/phạm vi hợp lệ, đang CHO_XAC_NHAN hoặc DA_XAC_NHAN. Thợ in và shipper không được gọi chức năng này.

**Hậu điều kiện**
Thành công: đơn DA_HUY, có lý do và MaMayIn trống; thanh toán vẫn CHUA_THANH_TOAN hoặc DA_THANH_TOAN. Thất bại trước commit: không hủy hoặc hoàn tiền. Không sinh trạng thái CHO_HOAN_TIEN mới.

### HT-4.11-02 Xem danh sách đơn cần hoàn tiền

**Use case**
Xem danh sách đơn cần hoàn tiền

**Use case ID**
HT-4.11-02

**Tác nhân chính**
CEO

**Tổng quan**
Cung cấp các khoản đã nhận tiền của đơn hủy trước in để CEO chuyển trả. Kích hoạt khi CEO mở Phê duyệt hoặc bấm Làm mới thông tin.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Chỉ CEO là tác nhân. HT-4.11-04 sử dụng một đơn từ danh sách nhưng không vì vậy mặc nhiên có quan hệ include/extend. Việc khách bổ sung thông tin được mô tả riêng ở HT-4.11-03.

**Dòng sự kiện chính**
1. CEO mở Phê duyệt trong phạm vi toàn chuỗi hoặc chi nhánh đang chọn.
2. Web yêu cầu danh sách đơn cần hoàn; API kiểm vai trò SUPER_ADMIN và phạm vi.
3. API lọc đơn DA_HUY, MaMayIn trống, thanh toán DA_THANH_TOAN và thuộc phạm vi hiện tại.
4. API trả mã đơn, khách, số điện thoại, chi nhánh, số tiền và ghi chú.
5. Web hiển thị ghi chú giữ xuống dòng, gồm lý do hoàn, ngân hàng và số tài khoản khi đã có đủ dữ liệu hợp lệ.
6. Web khóa nút Đã chuyển hoàn nếu chưa đủ thông tin và thông báo chờ khách bổ sung.
7. CEO làm mới thông tin khi cần rồi chọn xử lý khoản đủ điều kiện; use case này chưa đổi thanh toán.

**Dòng sự kiện phụ**
Biến thể 3a — Chỉ chọn một chi nhánh: trả danh sách đúng chi nhánh đó; tiếp tục bước 4.
Biến thể 5a — Ghi chú cũ chưa có đủ ba dòng hợp lệ: hiển thị ghi chú hiện có và cảnh báo chờ khách; quay lại danh sách.
Biến thể 7a — Khách đã cập nhật nhưng trang còn dữ liệu cũ: bấm Làm mới thông tin rồi lặp từ bước 2.
Ngoại lệ 2a — Quản lý/thợ in/shipper/khách gọi danh sách chuyên biệt: từ chối truy cập; kết thúc.
Ngoại lệ 3b — Không có khoản phù hợp: hiển thị danh sách trống; kết thúc thành công không có dữ liệu.
Ngoại lệ 2b — Lỗi tải API: hiển thị lỗi tải danh sách; không trình bày dữ liệu như khoản đã được xử lý.

**Tiền điều kiện**
CEO có phiên đăng nhập hợp lệ; phạm vi chi nhánh được xác định theo phiên và lựa chọn hiện tại.

**Hậu điều kiện**
Hiển thị khoản đủ điều kiện đúng phạm vi hoặc báo trống/lỗi; không chuyển tiền hay sửa trạng thái.

### HT-4.11-03 Cung cấp hoặc sửa thông tin nhận hoàn tiền

**Use case**
Cung cấp hoặc sửa thông tin nhận hoàn tiền

**Use case ID**
HT-4.11-03

**Tác nhân chính**
Khách hàng sở hữu đơn

**Tổng quan**
Bổ sung tài khoản nhận cho đơn hủy trước in đã nhận tiền nhưng chưa hoàn. Kích hoạt ở Theo dõi tiến độ bằng nút Bổ sung thông tin hoàn tiền hoặc Sửa thông tin hoàn tiền.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Hỗ trợ HT-4.11-04 khi đơn còn thiếu thông tin; là nhánh cần thiết cho đơn cũ hoặc tiền đến muộn. Không phải CEO phê duyệt yêu cầu hủy và không phải khách tự xác nhận đã hoàn.

**Dòng sự kiện chính**
1. Khách mở tiến độ đơn đã hủy đang chờ hoàn và chọn bổ sung hoặc sửa thông tin.
2. App mở popup ba trường: lý do hoàn tiền, ngân hàng, số tài khoản; nếu đã lưu thì điền lại đúng dữ liệu cũ.
3. Khách kiểm tra dữ liệu và chọn Lưu thông tin hoàn tiền.
4. App kiểm bắt buộc/định dạng; API kiểm lại lý do tối đa 100 ký tự, ngân hàng 60 ký tự, số tài khoản 6–30 chữ số.
5. API khóa đọc đơn và thanh toán, kiểm đúng chủ, DA_HUY, MaMayIn trống và thanh toán DA_THANH_TOAN.
6. API lưu GhiChu thành ba dòng Lý do hoàn tiền, Ngân hàng, Số tài khoản bằng câu lệnh có điều kiện chủ đơn và trạng thái.
7. API commit và phát cập nhật; App tải lại ghi chú. Số tài khoản được giữ dưới dạng chuỗi, không mất số 0 đầu.
8. Khách có thể sửa lại khi còn chờ hoàn; CEO làm mới trang để đọc thông tin mới trước khi chuyển tiền.

**Dòng sự kiện phụ**
Biến thể 2a — Chọn Sửa: dữ liệu đã lưu được điền sẵn; quay lại bước 3 sau khi điều chỉnh.
Biến thể 3a — Chọn Để sau: đóng popup, không gửi cập nhật; kết thúc giữ nguyên thông tin.
Ngoại lệ 4a — Thiếu/sai/qua giới hạn: báo lỗi và quay lại bước 2; không lưu một phần hoặc cắt bỏ thông tin.
Ngoại lệ 5a — Đơn không thuộc khách: từ chối và không tiết lộ dữ liệu; kết thúc.
Ngoại lệ 5b — Đã hoàn tiền, chưa nhận tiền, đơn chưa hủy hoặc là đơn hủy cũ đã in: không cho cập nhật; kết thúc.
Ngoại lệ 6a — Trạng thái đã thay đổi: yêu cầu tải lại; không sửa ghi chú của đơn khác và không đổi trạng thái thanh toán.
Ngoại lệ 7a — Mất phản hồi: tải lại đơn để kiểm tra thông tin đã lưu trước khi gửi lại.

**Tiền điều kiện**
CUSTOMER đăng nhập, đúng chủ đơn. Đơn đã hủy trước in, đã nhận tiền và chưa hoàn. App nhận cờ cho phép bổ sung từ API.

**Hậu điều kiện**
Thành công: GhiChu chứa đủ ba dòng trong giới hạn 255 ký tự; đơn vẫn DA_HUY, thanh toán vẫn DA_THANH_TOAN. Nếu bị từ chối thì ghi chú và thanh toán không bị cập nhật bởi yêu cầu đó.

### HT-4.11-04 Xác nhận đã chuyển hoàn tiền

**Use case**
Xác nhận đã chuyển hoàn tiền

**Use case ID**
HT-4.11-04

**Tác nhân chính**
CEO

**Tổng quan**
Ghi nhận việc CEO đã chuyển trả thực tế, không thực hiện giao dịch ngân hàng. Kích hoạt khi CEO chọn Đã chuyển hoàn trên Web và đồng ý popup.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Tác nhân duy nhất là CEO. Điều kiện đầu vào là tiền đã nhận hợp lệ và thông tin nhận hoàn đầy đủ. Chuyển tiền thực tế thuộc NV-3.5-04, nằm ngoài phần mềm; không include một API chuyển tiền chưa được triển khai.

**Dòng sự kiện chính**
1. CEO chọn khoản đủ thông tin trong danh sách cần hoàn.
2. CEO đã thực hiện chuyển trả qua ngân hàng bên ngoài và kiểm tra kết quả thành công.
3. CEO chọn Đã chuyển hoàn; popup nhắc lại mã đơn, khách, số tiền và ghi chú ngân hàng/tài khoản.
4. CEO đồng ý xác nhận sau khi đối chiếu.
5. API kiểm SUPER_ADMIN, khóa đọc đơn trong transaction và kiểm phạm vi, DA_HUY, MaMayIn trống, đủ thông tin nhận hoàn hợp lệ.
6. Thủ tục SQL kiểm CEO và chỉ cập nhật thanh toán đang DA_THANH_TOAN của đúng đơn sang DA_HOAN_TIEN.
7. API commit và phát cập nhật; Web tải lại danh sách, khoản đó không còn nằm trong danh sách cần hoàn.
8. App tải lại sẽ hiện Đã hoàn tiền; đơn vẫn DA_HUY và không còn cho sửa thông tin tài khoản.

**Dòng sự kiện phụ**
Biến thể 4a — CEO không đồng ý popup: đóng xác nhận và quay về danh sách; không gọi thao tác hoàn.
Ngoại lệ 5a — Vai trò khác hoặc sai phạm vi: từ chối; kết thúc không đổi thanh toán.
Ngoại lệ 5b — Chưa có đủ lý do/ngân hàng/tài khoản: từ chối; chờ khách bổ sung theo HT-4.11-03.
Ngoại lệ 6a — Đơn không còn đủ điều kiện hoặc đã hoàn: SQL không cập nhật và trả lỗi; không ghi nhận hoàn thêm lần nữa.
Ngoại lệ 7a — Web không nhận được phản hồi: CEO kiểm tra lại trạng thái trước khi xác nhận lại; không thực hiện thêm chuyển khoản chỉ vì lỗi mạng.
Giới hạn — Hệ thống không tự kiểm chứng giao dịch ngân hàng; người dùng phải chịu trách nhiệm xác nhận sau khi đã thực sự chuyển trả.

**Tiền điều kiện**
CEO đăng nhập đúng phạm vi. Đơn hủy trước in đã nhận tiền, chưa hoàn; ghi chú có đủ ba trường hợp lệ. CEO đã chuyển trả tiền thật bên ngoài phần mềm.

**Hậu điều kiện**
Thành công: thanh toán DA_HOAN_TIEN, đơn vẫn DA_HUY, ghi chú được giữ. Lỗi trước commit: không ghi nhận hoàn; hệ thống không tự làm phát sinh chuyển tiền bên ngoài.

### HT-4.11-05 Ghi nhận tiền hợp lệ đến sau khi hủy đơn

**Use case**
Ghi nhận tiền hợp lệ đến sau khi hủy đơn

**Use case ID**
HT-4.11-05

**Tác nhân chính**
payOS là hệ thống ngoài cung cấp kết quả thanh toán

**Tổng quan**
Không bỏ sót tiền ngân hàng nhận được sau lúc đơn bị hủy thủ công hoặc tự hủy quá hạn. Kích hoạt bởi webhook hợp lệ hoặc thao tác đối soát đọc trực tiếp kết quả payOS.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Đây là nhánh của use case ghi nhận thanh toán VietQR thuộc Hình 4.4; Task 1 mô tả riêng phần đơn đã hủy. Khách/quầy có thể kích hoạt đối soát nhưng không được tự khai thanh toán thành công. Không biến tác vụ kiểm tra nội bộ thành tác nhân ngoài.

**Dòng sự kiện chính**
1. API nhận webhook payOS hoặc đọc kết quả đối soát đáng tin cậy của đúng đơn.
2. Với webhook, API xác minh chữ ký, kết quả thành công, mã đơn, mô tả, tiền tệ, mã giao dịch và số tiền; đối soát trực tiếp kiểm Paid, mã đơn, số tiền và tiền đã trả.
3. API xác định đơn đã DA_HUY, khóa đọc đơn và thanh toán để xử lý nhất quán với yêu cầu hủy.
4. Nếu thanh toán còn CHUA_THANH_TOAN, API ghi nhận khoản tiền và thông tin giao dịch bằng thủ tục thanh toán.
5. API giữ nguyên DA_HUY, không khôi phục đơn và không gọi xếp in cho đơn đã hủy.
6. API phát cập nhật. Đơn hủy trước in đã nhận tiền xuất hiện trong danh sách cần hoàn của CEO.
7. Nếu thiếu ngân hàng/tài khoản, khách cung cấp theo HT-4.11-03; CEO xử lý chuyển trả và xác nhận theo các use case tương ứng.

**Dòng sự kiện phụ**
Ngoại lệ 2a — Chữ ký hoặc dữ liệu đối chiếu không hợp lệ, sai số tiền hay đơn không thuộc hệ thống: không tự ghi nhận đã thanh toán cho đơn; kết thúc theo kết quả kiểm tra của API.
Biến thể 4a — Webhook lặp khi đã DA_THANH_TOAN: không ghi nhận lại khoản tiền; vẫn không khôi phục đơn.
Biến thể 4b — Khoản đã DA_HOAN_TIEN: giữ nguyên đã hoàn, không đổi ngược về đã thanh toán và không gửi in.
Biến thể 6a — Đơn hủy cũ đã in còn mã máy: không đưa vào danh sách hoàn thông thường; cần xử lý ngoại lệ ngoài phạm vi này.
Ngoại lệ 1a — Không gọi được payOS: không suy diễn đã nhận tiền; cho đối soát lại khi kết nối phục hồi.
Query return/cancel của trang thanh toán chỉ điều hướng/hiển thị, không thay thế bằng chứng giao dịch.

**Tiền điều kiện**
Có thanh toán VietQR của đơn trong hệ thống và kết quả ngân hàng/payOS đã được xác minh theo kênh gọi. Đây là phần nhận tiền thật, không phải thao tác giả lập thanh toán.

**Hậu điều kiện**
Khoản hợp lệ chưa ghi nhận được cập nhật, đơn vẫn DA_HUY; khoản đã hoàn không bị mở lại. Giao dịch không hợp lệ không tạo quyền hoàn tiền bằng dữ liệu do khách tự khai.

## Đối chiếu toàn bộ nhãn trên hai hình gốc

| Hình | Nhãn trên hình | Đặc tả tương ứng | Kết luận | Cách hiểu theo code |
| --- | --- | --- | --- | --- |
| Nghiệp vụ | Gửi yêu cầu hủy đơn | NV-3.5-01 | Có triển khai | Chấp nhận hủy trực tiếp khi hợp lệ; không tạo yêu cầu chờ quản lý duyệt. |
| Nghiệp vụ | Xem danh sách chờ hoàn tiền | NV-3.5-02 | Có, cần sửa tác nhân | Chức năng danh sách chuyên biệt chỉ cho CEO; hình gốc đang nối cả quản lý chi nhánh. |
| Nghiệp vụ | Duyệt yêu cầu hủy | NV-3.5-01 bước 3 | Không có bước duyệt riêng | Thể hiện thành kiểm tra quyền/trạng thái trong luồng hủy, không mô tả nút Duyệt chưa có. |
| Nghiệp vụ | Hủy đơn quá hạn thanh toán | NV-3.5-03 | Có, tự động | Job nội bộ thực hiện theo hạn 15 phút và nhịp kiểm tra; quản lý không bấm tác vụ này. |
| Nghiệp vụ | Hoàn trả tiền cho khách | NV-3.5-04 | Có, ngoài phần mềm | CEO chuyển trả bằng kênh ngân hàng; không phải API refund tự động. |
| Nghiệp vụ | Xác nhận hoàn tiền | NV-3.5-05 | Có, CEO thực hiện | Ghi nhận sau khi đã chuyển trả thật; không dùng extend/include để diễn tả thứ tự. |
| Hệ thống | Hủy đơn hàng | HT-4.11-01 | Có triển khai | Cho cả khách đúng chủ và quản lý/CEO đúng phạm vi, chỉ hai trạng thái trước in. |
| Hệ thống | Kiểm tra trạng thái đơn | HT-4.11-01 bước 4–6 | Bước kiểm tra bắt buộc | Không phải tác nhân ngoài; hướng include trên ảnh gốc không được xem là căn cứ triển khai. |
| Hệ thống | Nhập lý do hủy đơn | HT-4.11-01 bước 2–3 | Bước nhập dữ liệu | Khách đã trả phải nhập cả thông tin nhận hoàn; không coi đây là xác nhận thanh toán. |
| Hệ thống | Xác nhận đã hoàn tiền | HT-4.11-04 | Có, cần sửa tác nhân | Chỉ CEO được xác nhận; khách chỉ xem kết quả và cung cấp thông tin nhận tiền. |
| Hệ thống | Thực hiện hoàn tiền | NV-3.5-04 và HT-4.11-04 | Cần tách ranh giới | Chuyển ngân hàng ở ngoài phần mềm; endpoint trong hệ thống chỉ ghi nhận đã chuyển hoàn. |
| Hệ thống | Xác nhận đã thanh toán | HT-4.11-05; Task 4 | Có, thuộc nhận tiền | Nhận tiền từ payOS/đối soát hợp lệ; không phải bước CEO tự đánh dấu để được hoàn. |
| Hệ thống | Xem đơn cần hoàn tiền | HT-4.11-02 | Có, CEO thực hiện | Lọc đúng phạm vi, đơn hủy trước in và thanh toán đã trả. |
| Bổ sung hiện hành | Cung cấp hoặc sửa thông tin nhận hoàn | HT-4.11-03 | Có trong code mới | Ảnh gốc chưa thể hiện rõ lý do hoàn, ngân hàng, số tài khoản và khóa sửa sau hoàn. |

## Căn cứ mã nguồn và giới hạn

Các vị trí dưới đây đã được mở đối chiếu trực tiếp. Đây là đặc tả từ mã nguồn, không phải biên bản thử giao dịch ngân hàng. Không sửa ứng dụng, cấu hình, database hay hai ảnh gốc trong Task 1. Các mã NV/HT là mã đặc tả của bộ tài liệu này, không phải tên endpoint.

| Mã | File và dòng | Nội dung đối chiếu |
| --- | --- | --- |
| E01 | [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:937](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:937) | Hủy đúng chủ/phạm vi/trước in, ghi chú; endpoint bổ sung và xác nhận hoàn trong cùng controller. |
| E02 | [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:1009](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:1009) | Bổ sung tài khoản nhận hoàn chỉ cho chủ đơn đang chờ hoàn. |
| E03 | [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:1039](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:1039) | Chỉ CEO, đủ thông tin nhận hoàn, transaction và gọi SQL. |
| E04 | [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/QuanTriController.cs:565](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/QuanTriController.cs:565) | Danh sách chuyên biệt chỉ SUPER_ADMIN; lọc trạng thái/mã máy/phạm vi. |
| E05 | [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:365](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:365) | Tiền đến muộn không khôi phục đơn hủy; không mở lại khoản đã hoàn. |
| E06 | [MaNguon/PhotocopyChain/PhotocopyShared/Constants/TrangThaiDonHang.cs:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/Constants/TrangThaiDonHang.cs:17) | Chỉ CHO_XAC_NHAN hoặc DA_XAC_NHAN. |
| E07 | [MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Tuan3/ThongTinHoanTienRequest.cs:7](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Tuan3/ThongTinHoanTienRequest.cs:7) | Bắt buộc/giới hạn từng trường, định dạng và đọc ghi chú ba dòng. |
| E08 | [Database/04_StoredProcedure.sql:645](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:645) | Kiểm trạng thái khi hủy, lưu lý do, bỏ máy và tạo thông báo. |
| E09 | [Database/04_StoredProcedure.sql:675](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:675) | Chuyển DA_THANH_TOAN sang DA_HOAN_TIEN chỉ cho đơn hủy trước in. |
| E10 | [Database/04_StoredProcedure.sql:339](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:339) | Đúng điều kiện quá 15 phút, VietQR chưa trả và đang chờ tiếp nhận. |
| E11 | [MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:24](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:24) | Chạy kiểm tra nội bộ khoảng mỗi phút. |
| E12 | [MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/PheDuyet.razor:167](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/PheDuyet.razor:167) | Hiển thị thông tin nhận tiền, khóa nút khi thiếu, popup xác nhận chuyển ngoài. |
| E13 | [MaNguon/photocopy_mobile/lib/widgets/huy_don.dart:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/widgets/huy_don.dart:11) | Form hủy và bổ sung thông tin nhận hoàn trên App. |
| E14 | [MaNguon/photocopy_mobile/lib/screens/don_hang/tien_do_screen.dart:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/don_hang/tien_do_screen.dart:17) | Chỉ hiện tác vụ theo điều kiện, cập nhật ghi chú và trạng thái nhận hoàn. |

## Kiểm tra độ phủ Task 1

- Đã đặc tả 5 nghiệp vụ với đủ ba phần theo mẫu.
- Đã đặc tả 5 use case hệ thống với đủ mười trường, luồng chính, biến thể, ngoại lệ và hậu điều kiện.
- Đã đối chiếu cả 6 nhãn nghiệp vụ và 7 nhãn hệ thống, không bỏ qua các nhãn chưa khớp code.
- Đã bổ sung tình huống tiền đến muộn và thông tin ngân hàng mới có trong hệ thống.
- Những phần không triển khai được ghi rõ; không tự biến thành tính năng đã có.
- Bộ đầy đủ các nhóm đã được tổng hợp trong DAC_TA_USECASE_MANH_DAY_DU.docx và bản Markdown cùng tên; xem kế hoạch để biết kết quả từng task.
