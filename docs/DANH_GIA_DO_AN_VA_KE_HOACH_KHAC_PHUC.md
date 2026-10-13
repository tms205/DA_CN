# Đánh giá đồ án PhotocopyChain và kế hoạch khắc phục

- **Đề tài:** Xây dựng hệ thống quản lý dịch vụ photocopy trực tuyến.
- **Mã đề tài:** CNTT-KLCN031.
- **Ngày đánh giá:** 12/10/2026.
- **Mục đích:** Lưu đánh giá trong phiên chat để nhóm đối chiếu, phân công và cải thiện trước khi bảo vệ.
- **Tính chất:** Đánh giá kỹ thuật tham khảo theo góc nhìn hội đồng chấm đồ án ứng dụng CNTT; không phải điểm chính thức của giảng viên hoặc nhà trường.

## 1. Phạm vi và căn cứ đánh giá

Đánh giá dựa trên mã nguồn .NET, Flutter, SQL và các tài liệu hiện có trong thư mục dự án. Đây là rà soát có chọn lọc các thành phần trọng yếu, không phải chứng nhận đã kiểm toán toàn bộ hệ thống.

**Kết luận nhanh: phần mềm đạt mức Tốt về phạm vi nghiệp vụ; hồ sơ đồ án hiện ở mức Khá, khoảng 7,0/10.** Điểm yếu chính là tài liệu chưa đồng bộ và bằng chứng nghiệm thu chưa đầy đủ, không phải thiếu số lượng chức năng.

Chưa tìm thấy một bản báo cáo tốt nghiệp hoàn chỉnh trong phạm vi rà soát. Điểm về nội dung, lý thuyết và trình bày là **điểm tạm thời cho tài liệu đang có**, không kết luận bản báo cáo nhóm lưu ở nơi khác cũng thiếu. Cần đánh giá lại các tiêu chí này khi có báo cáo đầy đủ.

### Kiểm chứng đã thực hiện trong lượt đánh giá

- .NET: **250 test đạt, 15 test database bỏ qua** vì lượt chạy này chưa cấu hình database kiểm thử.
- Flutter: **90 test đạt**; `flutter analyze` không báo lỗi.
- Đã đọc và đối chiếu các phần liên quan đến phân quyền, xác thực, thanh toán, tự động in, xử lý file, báo cáo và migration.
- **Chưa kiểm lại** giao dịch tiền thật, toàn bộ thao tác trình duyệt, điện thoại và máy in vật lý trong lần đánh giá này.
- Không lấy build thành công hoặc số test đạt làm bằng chứng mọi chức năng đã được nghiệm thu.

### Các tài liệu và mã nguồn tiêu biểu

- [Đặc tả chức năng](../DacTaChucNang_CNTT-KLCN031.md).
- [Migration 007: ca riêng và CEO duyệt phiếu nhập](../Database/007-CaRiengNhanVien_CEODuyetPhieuNhap.sql).
- [Thủ tục SQL](../Database/04_StoredProcedure.sql).
- [Luồng tự động in](../MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs).
- [Xử lý file in](../MaNguon/PhotocopyChain/PhotocopyApi/Controllers/FileInController.cs).
- [Báo cáo triển khai ảnh thẻ và mẫu đơn](../MaNguon/PhotocopyChain/docs/InAnhThe_MauDon_2026-10-12.md).

## 2. Bảng điểm tổng hợp

Các tiêu chí được tính trọng số bằng nhau. Tổng điểm thành phần là 42/60; điểm trung bình là 7,0/10.

| Tiêu chí | Điểm | Nhận xét ngắn |
|----------|------|---------------|
| 1. Tính đầy đủ nội dung | 6,5/10 | Đặc tả nghiệp vụ khá đầy đủ, nhưng chưa có đủ bằng chứng về một báo cáo học thuật hoàn chỉnh |
| 2. Chất lượng kỹ thuật | 8,0/10 | Hệ thống liên kết nhiều thành phần, kiểm soát nghiệp vụ tốt; còn hạn chế bảo trì và vận hành |
| 3. Độ sâu lý thuyết | 5,5/10 | Có áp dụng kỹ thuật, nhưng phần giải thích nền tảng và lựa chọn thiết kế còn mỏng |
| 4. Chất lượng thực nghiệm | 7,5/10 | Có kiểm thử tự động thực chất; thiếu bộ nghiệm thu xuyên suốt và số liệu hiệu năng |
| 5. Tính chuyên nghiệp của báo cáo | 6,0/10 | Có nhiều tài liệu hướng dẫn, nhưng phân tán và tồn tại mâu thuẫn với code |
| 6. Tính sáng tạo và hướng phát triển | 8,5/10 | Nghiệp vụ sát thực tế, có thanh toán xác thực và connector máy in mô phỏng |
| **TỔNG — trung bình** | **7,0/10** | **Khá; phần mềm có nền tảng tốt để nâng chất lượng đồ án** |

## 3. Đánh giá chi tiết

### 3.1. Tính đầy đủ nội dung — 6,5/10

Đã có mục tiêu, khảo sát nhu cầu, vai trò, ma trận quyền, quy trình đơn hàng, ngoại lệ và tiêu chí nghiệm thu. Đặc tả chức năng là tài liệu có giá trị, không chỉ liệt kê màn hình.

Tuy nhiên, **đặc tả chức năng chưa thay thế báo cáo tốt nghiệp**. Trong tài liệu đã rà soát, chưa thấy bộ nội dung hoàn chỉnh gồm cơ sở lý thuyết, thiết kế CSDL kèm giải thích, kết quả thực nghiệm, kết luận và tài liệu tham khảo thống nhất.

Phần khảo sát có các tỷ lệ như 81,3% hoặc 68,8%, nhưng ngay phần trình bày đó chưa nêu rõ cỡ mẫu, thời gian, cách tuyển người trả lời và dữ liệu gốc. Hội đồng có thể hỏi: “Khảo sát bao nhiêu người, có đại diện không?”

**Hướng khắc phục:** đối chiếu báo cáo với mẫu của khoa; bổ sung nguồn dữ liệu khảo sát, phương pháp và giới hạn mẫu. Nếu có dữ liệu ở nơi khác, đưa vào phụ lục và dẫn chiếu rõ ràng. Không tạo số liệu để lấp chỗ thiếu.

### 3.2. Chất lượng kỹ thuật — 8,0/10

Điểm đáng ghi nhận:

- Web quản trị, mobile, API, SQL và simulator có kết nối nghiệp vụ, không chỉ là các màn CRUD rời rạc.
- Có BCrypt, JWT, kiểm tra quyền theo chi nhánh, kiểm tra đầu vào và bảo vệ OTP.
- Xác nhận thanh toán dựa vào webhook có kiểm chữ ký hoặc đối soát payOS, không tin tham số `status=PAID` trên URL.
- Có transaction và khóa dữ liệu khi xếp in, cùng cơ chế giữ quyền nhận lệnh để hạn chế xử lý trùng.

Các điểm bị trừ:

- `DonHangController.cs` tại thời điểm đánh giá khoảng **1.747 dòng**: tập trung nhiều trách nhiệm, làm tăng khó khăn khi sửa và kiểm thử.
- Nhiều endpoint trả trực tiếp `ex.Message`; thông báo lỗi SQL có thể lộ chi tiết nội bộ.
- Chuyển đổi tài liệu phụ thuộc môi trường Windows và Office/LibreOffice.
- Chưa có bằng chứng đo hiệu năng để kết luận hệ thống đã “tối ưu”.

Đây là kỹ thuật khá tốt cho đồ án ứng dụng, nhưng chưa đủ căn cứ gọi là sẵn sàng triển khai thương mại.

**Hướng khắc phục:** ưu tiên lỗi đúng/sai và vận hành trước; chuẩn hóa thông báo lỗi và giới hạn tiến trình chuyển đổi file. Không thực hiện tái cấu trúc lớn sát ngày bảo vệ chỉ để giảm số dòng.

### 3.3. Độ sâu lý thuyết — 5,5/10

Code cho thấy có áp dụng xác thực, phân quyền, giao dịch, khóa đồng thời, xử lý thời gian thực và trạng thái nghiệp vụ. Nhưng tài liệu hiện thiên về **“đã làm gì”**, chưa giải thích đủ **“tại sao làm như vậy”**.

Cần bổ sung các lập luận:

- Vì sao dùng Blazor, Flutter, Dapper và SQL Server?
- Phân quyền theo vai trò khác gì kiểm tra quyền trên từng bản ghi?
- Transaction giải quyết trường hợp hai request cùng thu tiền hoặc trừ kho như thế nào?
- Vì sao webhook có thể gửi lại, và hệ thống tránh ghi nhận trùng ra sao?
- Vì sao trạng thái thanh toán phải tách khỏi trạng thái đơn hàng?
- Công thức ETA dựa vào giả định gì và sai ở những tình huống nào?

**Không có căn cứ để kết luận sinh viên copy–paste.** Tuy nhiên, code chạy được cũng chưa chứng minh sinh viên hiểu lý thuyết; điều đó cần thể hiện trong báo cáo và phần vấn đáp.

**Hướng khắc phục:** mỗi lựa chọn thiết kế quan trọng cần có lý do, phương án thay thế, ưu/nhược điểm và liên hệ với đoạn triển khai thực tế. Dẫn nguồn kỹ thuật chính thống; không chép định nghĩa dài mà thiếu liên hệ với đề tài.

### 3.4. Chất lượng thực nghiệm — 7,5/10

Kiểm thử là điểm mạnh thực chất: các test bao phủ OTP, dữ liệu nhập, phạm vi chi nhánh, QR, quy cách in, ảnh thẻ và một số quy tắc simulator.

Nhưng **340 test đạt không đồng nghĩa 340 chức năng đã được nghiệm thu**. Có test dùng mock hoặc gọi trực tiếp hàm/controller; chúng không thay thế luồng sử dụng xuyên suốt trên thiết bị thật.

Còn thiếu:

- Bảng yêu cầu → ca kiểm thử → kết quả → bằng chứng.
- Kết quả kiểm thử đầy đủ trên database kiểm thử độc lập.
- Thử đồng thời nhiều đơn, thiếu kho, mất kết nối và khởi động lại giữa job.
- Số liệu thời gian phản hồi, tỷ lệ lỗi, tải đồng thời.
- So sánh thời gian nhận dự kiến với thời gian hoàn tất thực tế.

Với đề tài này, **không cần confusion matrix** nếu không có mô hình phân loại. Nên dùng bảng kết quả nghiệp vụ, biểu đồ thời gian phản hồi và phân tích lỗi.

**Hướng khắc phục:** ghi rõ môi trường, dữ liệu, bước thực hiện, kết quả mong đợi/thực tế, ngày chạy và bằng chứng. Phân biệt unit test, integration test, widget test, end-to-end và nghiệm thu thiết bị.

### 3.5. Tính chuyên nghiệp của báo cáo — 6,0/10

Có tài liệu hướng dẫn chạy, migration, tổng hợp thay đổi và giới hạn triển khai. Điểm tốt là một số tài liệu phân biệt rõ “đã kiểm chứng” với “chưa nghiệm thu”.

Tuy nhiên, đặc tả ngày 07/10 hiện mâu thuẫn với code mới:

| Nội dung | Đặc tả đang ghi | Code/migration mới |
|----------|-----------------|-------------------|
| Tiền đầu ca tự mở | 0đ | Mặc định 200.000đ |
| Ca nhân viên | Một ca chưa kết thúc mỗi chi nhánh | Mỗi nhân viên có ca riêng |
| CEO tự duyệt phiếu nhập | Không được phép | Được phép; phiếu CEO lập tự duyệt |

Đối chiếu tại migration 007. Đây là lỗi tài liệu đáng ưu tiên: hội đồng đọc một quy tắc nhưng demo chạy quy tắc khác sẽ khó xác định thiết kế nào là chính thức.

**Hướng khắc phục:** chọn một tài liệu đặc tả làm nguồn chính thức; cập nhật bảng quyền, sơ đồ, ví dụ và kịch bản nghiệm thu cùng lúc. Nhật ký thay đổi đưa vào phụ lục. Khi có báo cáo chính thức, kiểm thêm đánh số bảng/hình, chú thích, trích dẫn và thuật ngữ.

### 3.6. Tính sáng tạo và hướng phát triển — 8,5/10

Đề tài có chiều sâu nghiệp vụ hơn một hệ thống bán hàng thông thường:

- Giá và khuyến mãi riêng theo chi nhánh.
- Ca/két tiền, COD shipper và phê duyệt nhập kho.
- Thanh toán được xác minh phía máy chủ.
- Simulator chạy riêng, có heartbeat và trạng thái lệnh.
- Quy cách gia công, ảnh thẻ theo kích thước và mẫu đơn trống.

Đây là **sáng tạo trong giải quyết nghiệp vụ và tích hợp**, không phải thuật toán nghiên cứu mới. Điều đó phù hợp với đồ án ứng dụng.

Hướng phát triển hợp lý là connector máy in thật, nhật ký tài chính, push notification và triển khai ổn định. Không nên khẳng định “đổi sang máy thật là chạy ngay” khi chưa kiểm chứng driver, spooler và lỗi thiết bị.

## 4. Điểm mạnh nổi bật

1. **Phạm vi nghiệp vụ có chiều sâu**, không dừng ở thêm–sửa–xóa.
2. **Có kiểm soát tiền và trạng thái**, phân biệt đã thanh toán, đang in và đã bàn giao.
3. **Có kiểm thử tự động đang chạy được**, gồm cả trường hợp sai và ngoại lệ.
4. **Có quan tâm phân quyền và dữ liệu chi nhánh**, không chỉ ẩn nút trên giao diện.
5. **Có ghi nhận giới hạn mô phỏng**, đặc biệt ở máy in và dịch vụ ảnh thẻ.

## 5. Điểm yếu và hướng khắc phục

### 5.1. Ưu tiên cao

**1. Đồng bộ đặc tả với phiên bản code chốt.**

Sửa các quy tắc ca, CEO duyệt phiếu, OTP, QR trong app, quy cách gia công và ảnh thẻ. Chọn một tài liệu làm nguồn chính thức, các nhật ký thay đổi chỉ làm phụ lục.

**2. Hoàn thiện bằng chứng nghiệm thu.**

Chuẩn bị các luồng xuyên suốt có ảnh, log và dữ liệu đối chiếu: thanh toán → xếp in → in xong → bàn giao; COD → thu tiền → kết ca; nhập kho → duyệt → nhận hàng.

**3. Làm rõ giới hạn báo cáo tài chính.**

Truy vấn hiện cộng tiền đơn `HOAN_THANH`; thủ tục `sp_HoanTien` chủ yếu đổi trạng thái, chưa lưu đầy đủ chứng từ hoàn. Vì vậy không nên gọi đây là báo cáo thu–chi hoặc thu ròng hoàn chỉnh. Cần phân biệt doanh thu đơn hoàn thành, tiền đã nhận, tiền đã hoàn và tiền mặt trong ca.

Nếu bổ sung chứng từ hoặc thay đổi CSDL, cần thiết kế và phê duyệt migration trước; tài liệu đánh giá này không tự cấp quyền thay đổi schema.

### 5.2. Ưu tiên tiếp theo

**4. Thu hẹp phạm vi dừng tiến trình Office.**

Hàm `TatOfficeChayNgam` trong `FileInController.cs` hiện tìm các tiến trình Office không có cửa sổ rồi dừng, chưa chứng minh chúng đều do API tạo. Có nguy cơ ảnh hưởng tác vụ khác chạy nền. Nên quản lý đúng tiến trình của tác vụ chuyển đổi.

**5. Chuẩn hóa xử lý lỗi.**

Trả thông báo nghiệp vụ rõ ràng cho người dùng; chi tiết SQL/exception lưu trong log, không trả nguyên văn. Log và ảnh bằng chứng không được chứa mật khẩu, khóa API hoặc OTP.

**6. Bổ sung khả năng tái lập.**

Thử cài trên môi trường sạch, ghi đúng thứ tự SQL/migration và chuẩn bị cấu hình mẫu không chứa bí mật. Launcher chạy được trên máy hiện tại chưa chứng minh người khác cài được.

**7. Bổ sung số liệu thay cho nhận xét định tính.**

Ví dụ: thời gian báo giá, thời gian đồng bộ thanh toán, thời gian chờ in, kết quả khi 10–20 người thao tác đồng thời. Không ghi “nhanh, chính xác, ổn định” khi chưa đo. Không thử tải lên kênh thanh toán thật để tạo số liệu.

## 6. Kết luận và kế hoạch cải thiện trong một tuần

**Xếp loại tạm thời: Khá — 7,0/10.** Phần mềm có nền tảng Tốt; chưa đủ bằng chứng để xếp toàn bộ đồ án ở mức Xuất sắc. Đây là nhận định theo phạm vi đã kiểm tra, không dự đoán điểm bảo vệ chính thức.

Nếu chỉ còn một tuần, **không nên thêm phân hệ mới**.

| Thời gian | Công việc | Kết quả cần có |
|-----------|-----------|----------------|
| Ngày 1 | Chốt phạm vi, sửa đặc tả lệch code | Một phiên bản yêu cầu chính thức |
| Ngày 2–3 | Nghiệm thu các luồng quan trọng trên Web, mobile và DB | Bảng test kèm ảnh/log, lỗi được phân loại |
| Ngày 4 | Sửa lỗi ảnh hưởng tiền, quyền và vận hành | Không thu/trừ kho trùng; ngoại lệ rõ ràng |
| Ngày 5 | Bổ sung kiến trúc, ERD, sequence và lý thuyết | Giải thích được lựa chọn thiết kế |
| Ngày 6 | Đo hiệu năng cơ bản, hoàn thiện chương thực nghiệm | Số liệu và phân tích giới hạn |
| Ngày 7 | Chạy thử cài đặt, chuẩn bị demo và vấn đáp | Demo ổn định, phương án dự phòng rõ ràng |

**Việc đáng làm nhất lúc này là biến các chức năng đã có thành một sản phẩm được chứng minh, tài liệu hóa và giải thích nhất quán.** Điều đó có giá trị chấm điểm cao hơn việc thêm nhiều màn hình nữa.

## 7. Checklist theo dõi khắc phục của nhóm

Các ô dưới đây là **việc cần thực hiện**, chưa được đánh dấu hoàn thành chỉ vì đã có code hoặc test tự động. Nhóm ghi người phụ trách và bằng chứng khi chốt từng mục.

### Tài liệu và lý thuyết

- [ ] Chốt phiên bản code dùng để nộp và ghi commit tương ứng.
- [ ] Đồng bộ đặc tả về đầu ca 200.000đ, ca riêng từng nhân viên và CEO tự duyệt phiếu nhập.
- [ ] Đồng bộ đặc tả về OTP, QR trong app, hướng giấy, gia công, ảnh thẻ và mẫu đơn.
- [ ] Đối chiếu đầy đủ các chương báo cáo với yêu cầu của khoa.
- [ ] Bổ sung dữ liệu gốc, cỡ mẫu, thời gian và phương pháp khảo sát.
- [ ] Hoàn thiện sơ đồ kiến trúc, ERD và luồng tuần tự thanh toán/in/nhập kho.
- [ ] Giải thích lựa chọn công nghệ, transaction, khóa đồng thời và chống xử lý trùng.
- [ ] Thống nhất thuật ngữ, đánh số bảng/hình và tài liệu tham khảo.

### Kiểm thử và vận hành

- [ ] Tạo database kiểm thử riêng; không dùng dữ liệu vận hành làm môi trường thử phá lỗi.
- [ ] Chạy và ghi kết quả các test database phù hợp với dữ liệu kiểm thử.
- [ ] Nghiệm thu VietQR tại quầy và mobile, ghi nhận trạng thái đúng sau xác minh tiền.
- [ ] Nghiệm thu COD, bàn giao, tiền vào ca và kết ca.
- [ ] Nghiệm thu CEO tự duyệt, quản lý chờ duyệt và tồn chỉ tăng sau nhận hàng.
- [ ] Kiểm tra request ngoài quyền và ngoài chi nhánh bị từ chối.
- [ ] Kiểm tra callback lặp, thiếu kho, thiếu file, máy offline và restart giữa job.
- [ ] Nghiệm thu ảnh thẻ và mẫu đơn trên app thực tế; phân biệt kiểm PDF với kiểm bản in vật lý.
- [ ] Thu hẹp phạm vi dừng tiến trình Office và kiểm hồi quy chuyển đổi file.
- [ ] Chuẩn hóa lỗi trả về; kiểm tra log không lộ dữ liệu nhạy cảm.
- [ ] Đo hiệu năng trong môi trường kiểm thử, ghi cấu hình và cách đo.
- [ ] Thử cài từ đầu, ghi thứ tự SQL/migration và cấu hình cần thiết.

### Chốt trước bảo vệ

- [ ] Ghi rõ báo cáo hiện tại phản ánh doanh thu nào, không gọi nhầm thành thu ròng.
- [ ] Ghi rõ simulator không chứng minh đã in giấy thật; kết nối máy thật là hướng phát triển chưa nghiệm thu.
- [ ] Lập bảng yêu cầu → test → kết quả → ảnh/log.
- [ ] Chuẩn bị dữ liệu demo ổn định và phương án khi Internet/tunnel/ngân hàng không sẵn sàng.
- [ ] Kiểm tra không đưa `.env`, khóa API, mật khẩu hoặc dữ liệu khách hàng lên Git/bản nộp công khai.
- [ ] Mỗi thành viên giải thích được phần mình phụ trách và các giới hạn của hệ thống.

