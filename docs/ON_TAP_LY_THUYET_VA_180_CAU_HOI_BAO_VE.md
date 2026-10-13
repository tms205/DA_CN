# Ôn tập lý thuyết và 180 câu hỏi bảo vệ đồ án PhotocopyChain

- **Đề tài:** Xây dựng hệ thống quản lý dịch vụ photocopy trực tuyến — CNTT-KLCN031.
- **Ngày soạn:** 12/10/2026.
- **Đối tượng:** Nhóm thực hiện đồ án, dùng để bổ sung báo cáo và luyện vấn đáp.
- **Phạm vi đối chiếu:** .NET API, Blazor WebAssembly, Flutter, SQL Server và printer simulator trong mã nguồn hiện tại.
- **Liên quan:** [Đánh giá đồ án và kế hoạch khắc phục](DANH_GIA_DO_AN_VA_KE_HOACH_KHAC_PHUC.md).

## Cách sử dụng

Tài liệu có hai phần: **12 câu trả lời lý thuyết trọng tâm** và **180 câu hỏi luyện bảo vệ có gợi ý**. Các câu hỏi do mình xây dựng từ code, nghiệp vụ và tài liệu kỹ thuật chính thức; không phải đề thi hay câu hỏi đã biết trước của hội đồng.

Câu trả lời mẫu dùng ngôi “nhóm em” để dễ luyện nói. **Lý do chọn công nghệ là lập luận gợi ý phù hợp với thiết kế hiện tại, không phải bằng chứng nhóm đã cân nhắc như vậy từ đầu.** Nhóm cần điều chỉnh theo quyết định thật, đọc hiểu code và tự giải thích; không học thuộc rồi nhận là đã làm những phần chưa có.

Mỗi câu nên trả lời theo bốn bước: **khái niệm → áp dụng vào đồ án → bằng chứng → giới hạn**. Khi chưa có số liệu, trả lời “chưa đo/chưa nghiệm thu”, không tự tạo kết quả. Các gợi ý trong ngân hàng câu hỏi là ý chính, không thay thế câu trả lời đầy đủ.

### Thứ tự học khi ít thời gian

1. Học phần A trước, đặc biệt A2–A6 và A12.
2. Luyện các câu phản biện: Q059, Q066, Q075, Q080, Q084, Q090, Q104, Q110, Q115, Q120, Q129, Q153, Q166–Q180.
3. Chuẩn bị mở được code và bằng chứng demo cho từng câu quan trọng.
4. Chia nhóm theo mảng, nhưng mọi thành viên phải hiểu luồng đặt đơn → thanh toán → in → bàn giao.

## A. Trả lời phần độ sâu lý thuyết

### A1. Vì sao chọn Blazor, Flutter, Dapper và SQL Server? Kiến trúc thực tế là gì?

**Trả lời mẫu:** “Nhóm em tách giao diện khỏi máy chủ nghiệp vụ. Web quản trị dùng Blazor WebAssembly, app dùng Flutter; cả hai gọi ASP.NET Core API. API truy cập SQL Server bằng Dapper và thủ tục SQL. Printer simulator là chương trình riêng, trao đổi với API qua HTTP.”

Blazor WebAssembly cho phép dùng C# và DTO chung với API, phù hợp giao diện quản trị. Flutter dùng Dart, nên vẫn phải ánh xạ JSON thành model riêng; nhóm không chia sẻ trực tiếp lớp C# với app. Flutter phù hợp UI di động và hỗ trợ nhiều nền tảng, nhưng khả năng đa nền tảng không có nghĩa nhóm đã nghiệm thu cả Android lẫn iOS. [Blazor hosting models](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-8.0), [Flutter architecture](https://docs.flutter.dev/resources/architectural-overview).

Dapper ánh xạ kết quả SQL sang đối tượng, phù hợp dự án đã dùng nhiều stored procedure và cần kiểm soát câu truy vấn. Đổi lại, nhóm tự quản lý SQL, migration và quan hệ dữ liệu; không có sẵn change tracking giống EF Core. Không nói Dapper “luôn nhanh hơn” nếu chưa đo trên cùng workload. [Dapper chính thức](https://github.com/DapperLib/Dapper).

SQL Server phù hợp dữ liệu có nhiều quan hệ, ràng buộc và giao dịch tiền/kho. Đây là **hệ thống client–server với backend tập trung**, không nên tự gọi là microservices hay Clean Architecture hoàn chỉnh. Tách simulator thành tiến trình riêng chưa đủ để biến toàn hệ thống thành microservices.

**Bằng chứng:** [Web Program.cs](../MaNguon/PhotocopyChain/PhotocopyAdmin/Program.cs), [API Program.cs](../MaNguon/PhotocopyChain/PhotocopyApi/Program.cs), [Flutter dependencies](../MaNguon/photocopy_mobile/pubspec.yaml).

### A2. Xác thực, phân quyền vai trò và quyền từng bản ghi khác nhau thế nào?

**Trả lời mẫu:** “Xác thực trả lời người gọi là ai. Phân quyền vai trò trả lời người đó được làm nhóm thao tác nào. Quyền trên bản ghi trả lời người đó có được thao tác đúng đơn/file/chi nhánh cụ thể này không.”

Ví dụ STAFF có quyền xử lý đơn, nhưng không được xử lý đơn chi nhánh khác. CUSTOMER được xem đơn của chính mình, không phải mọi đơn có mã hợp lệ. Vì vậy `[Authorize]` và ẩn nút chưa đủ: API còn phải kiểm chủ sở hữu, chi nhánh và trạng thái bản ghi. Đây là nguyên tắc phân quyền dựa trên tài nguyên. [Microsoft: resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-8.0).

CEO có thể chọn chi nhánh qua header; middleware chỉ áp dụng header đó khi JWT có quyền SUPER_ADMIN. STAFF/BRANCH_ADMIN không được đổi phạm vi bằng cách tự sửa header. Chọn chi nhánh chỉ đổi ngữ cảnh của request, không phải sửa tài khoản hay ký lại JWT. Tuy nhiên, middleware không thay thế kiểm tra bản ghi ở từng endpoint.

**Bằng chứng:** [PhamViChiNhanhMiddleware.cs](../MaNguon/PhotocopyChain/PhotocopyApi/PhamViChiNhanhMiddleware.cs), [kiểm thử phạm vi](../MaNguon/PhotocopyChain/PhotocopyApi.Tests/PhamViChiNhanhTests.cs).

### A3. Transaction và ACID bảo vệ tiền, kho như thế nào?

Transaction gom các thay đổi liên quan thành một đơn vị: thành công thì commit, thất bại thì rollback. ACID gồm **Atomicity** — nguyên tử; **Consistency** — giữ các ràng buộc; **Isolation** — kiểm soát tương tác giữa giao dịch đồng thời; **Durability** — thay đổi đã commit được lưu bền theo cơ chế DB. [Microsoft: transaction guide](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide?view=sql-server-ver17).

**Trả lời mẫu:** “Khi xếp in, nhóm cần gán máy, chuyển trạng thái và trừ kho nhất quán. Nếu thiếu vật tư thì không được để đơn thành đang in trong khi kho chưa xử lý đúng. Luồng tự động mở transaction rồi gọi các thủ tục liên quan.”

Nhưng có transaction **không tự chứng minh mọi đường xử lý chống race condition**. Phải kiểm vị trí đọc/khóa/cập nhật, điều kiện trạng thái và ràng buộc. Một SQL transaction cũng không rollback được tiền đã vào ngân hàng, file đã ghi ổ đĩa hoặc giấy đã in. Những hệ thống này cần đối soát, retry hoặc thao tác bù có kiểm soát.

**Bằng chứng:** [TuDongInService.cs](../MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs), [stored procedures](../Database/04_StoredProcedure.sql).

### A4. `UPDLOCK`, `HOLDLOCK` và idempotency giải quyết điều gì?

`UPDLOCK` dùng khóa cập nhật khi đọc và giữ đến cuối transaction; `HOLDLOCK` áp dụng ngữ nghĩa SERIALIZABLE cho tham chiếu bảng đó. Chúng giúp bảo vệ bước đọc–quyết định–ghi, nhưng có thể tăng blocking và deadlock; không nên áp dụng tùy tiện toàn hệ thống. [Microsoft: table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17).

**Trả lời mẫu:** “Hai request có thể cùng thấy một đơn đang chờ. Luồng tự xếp in khóa bản ghi đơn trong transaction, kiểm trạng thái rồi mới gán máy và chuyển bước. Khi request sau đọc lại, nó phải dựa trên trạng thái mới.”

Idempotency là thực hiện lại cùng một thao tác không làm phát sinh thêm tác dụng nghiệp vụ. Trong đồ án, xác nhận chuyển khoản chỉ cập nhật thanh toán VietQR đang chưa trả; xếp in kiểm trạng thái đã tiến triển; connector có chủ lệnh và định danh lệnh. Đây là các cơ chế hạn chế xử lý trùng trong phạm vi hiện tại.

Không phát biểu “HTTP bảo đảm exactly-once” hoặc “mọi request đều an toàn khi retry”. Muốn chứng minh phải thử đồng thời, callback lặp và crash tại các điểm chuyển tiếp. File claim hiện chưa bảo đảm chạy nhiều host độc lập không chia sẻ trạng thái.

**Bằng chứng:** `sp_XacNhanChuyenKhoan`, `TuDongInService`, [QuyenNhanLenhInStore.cs](../MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/QuyenNhanLenhInStore.cs).

### A5. Vì sao webhook chứng minh thanh toán, còn return URL và QR thì không?

QR chứa thông tin để khách thực hiện chuyển khoản; nó không chứng minh khách đã chuyển. Return URL phục vụ điều hướng trình duyệt, có thể bị sửa query, đóng trước khi tải hoặc không truy cập được.

**Trả lời mẫu:** “Nhóm chỉ ghi nhận thanh toán khi xác minh webhook bằng SDK/checksum hoặc đối soát trực tiếp payOS. Ngoài tính hợp lệ của chữ ký, nhóm kiểm mã đơn, số tiền và thông tin giao dịch. Việc quay về giao diện không tự đánh dấu đã thanh toán.”

Chữ ký HMAC bảo vệ tính toàn vẹn và nguồn dữ liệu trong phạm vi khóa bí mật, không mã hóa nội dung webhook. Chữ ký đúng vẫn phải đối chiếu nghiệp vụ; không được dùng dữ liệu mẫu xác nhận endpoint làm giao dịch thật. [payOS API và webhook](https://payos.vn/docs/api/).

Nếu webhook bị lỡ vì tunnel/API ngừng, API đối soát lại payOS. Nếu đã nhận tiền nhưng thiếu file, máy hoặc kho, vẫn ghi nhận tiền và giữ đơn chờ xử lý; không xóa kết quả thanh toán chỉ vì chưa in được. Tiền đến sau hủy thủ công là ngoại lệ cần xử lý, chưa có hoàn tiền ngân hàng tự động.

**Bằng chứng:** [ThanhToanController.cs](../MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs), [PayOSWebhookTests.cs](../MaNguon/PhotocopyChain/PhotocopyApi.Tests/PayOSWebhookTests.cs).

### A6. Vì sao tách trạng thái thanh toán, đơn hàng và máy in?

**Trả lời mẫu:** “Đây là ba đối tượng có vòng đời khác nhau. Tiền có thể đã nhận nhưng đơn còn chờ máy; đơn đã in xong nhưng COD chưa thu; máy đang offline không đồng nghĩa đơn đã bị hủy.”

Một trạng thái chung sẽ làm mất thông tin. Ví dụ:

| Tình huống | Thanh toán | Đơn | Ý nghĩa |
|---|---|---|---|
| Nhận đủ VietQR, thiếu vật tư | Đã thanh toán | Đã xác nhận/chờ in | Giữ tiền đã xác minh, chưa bắt đầu in |
| COD in xong chưa bàn giao | Chưa thanh toán | Đã in | Chưa được coi là đã thu tiền |
| Máy báo xong, khách chưa nhận | Có thể đã thanh toán | Đã in | Không tự suy ra hoàn thành bàn giao |

State machine quy định các chuyển bước được phép và điều kiện kèm theo. Callback không được đưa đơn hoàn thành quay lại đang in. Trạng thái `DANG_IN` hiện biểu diễn đã vào quy trình in, có thể còn trong hàng đợi máy; không khẳng định giấy đã chạy đúng thời điểm đổi trạng thái.

**Bằng chứng:** [đặc tả trạng thái](../DacTaChucNang_CNTT-KLCN031.md), thủ tục `sp_CapNhatTrangThaiIn` và controller máy in. Đặc tả cần cập nhật các phần đã lệch code như ca/CEO trước khi nộp.

### A7. ETA báo giá và thời gian simulator được tính như thế nào?

**Đúng theo code hiện tại:**

```text
ETA báo giá = thời điểm hiện tại
              + ceil((trang chờ + trang của đơn) / tốc độ nhanh nhất chi nhánh) phút
In gấp: công thức báo giá đặt trang chờ = 0.

Thời gian simulator (giây)
  = thời gian ra trang đầu + Σ(trang dòng × số bản × 60 / tốc độ chọn cho dòng)
Tốc độ simulator: ưu tiên A3; nếu không A3 thì xét hai mặt; còn lại A4.
```

**Trả lời mẫu:** “ETA là ước lượng để khách biết thời gian, còn simulator tính thời lượng job theo cấu hình máy. Hai công thức hiện chưa dùng chung bộ lập lịch nên không cam kết luôn trùng nhau.”

Ví dụ job 20 trang, 2 bản, máy 20 trang/phút và ra trang đầu 5 giây: simulator tính `5 + 20 × 2 × 60 / 20 = 125 giây`. Đây là ví dụ tính toán, không phải số liệu thực nghiệm.

Giới hạn: ETA dùng máy nhanh nhất, chưa mô hình đủ khả năng máy/heartbeat/khay/giấy/màu, warm-up, gia công và lỗi thực. Simulator hiện A3 được xét trước duplex, chưa có tốc độ riêng cho mọi tổ hợp. In gấp không có nghĩa ngắt một job vật lý đang chạy. Không gọi đây là thuật toán tối ưu hàng đợi đã được chứng minh.

**Bằng chứng:** [fn_UocTinhGioXong](../Database/03_View_Function.sql), [QuyTacMayInAo.cs](../MaNguon/PhotocopyChain/PhotocopyShared/MayInAo/QuyTacMayInAo.cs).

### A8. Chuẩn hóa dữ liệu và snapshot giá có mâu thuẫn không?

**Trả lời mẫu:** “Nhóm tách dịch vụ dùng chung khỏi bảng giá theo chi nhánh để tránh lặp định nghĩa dịch vụ. Quan hệ dịch vụ–chi nhánh có giá riêng. Khi tạo đơn, nhóm lưu giá đã chốt vào chi tiết để việc sửa bảng giá sau đó không đổi lịch sử.”

Đây là phân biệt **dữ liệu cấu hình hiện tại** với **sự kiện nghiệp vụ đã xảy ra**. Snapshot giá không phải lỗi dư thừa tự động cần xóa; nó mang ý nghĩa lịch sử. Snapshot ảnh thẻ còn lưu kích thước và số ảnh vì một tờ PDF có thể chứa nhiều ảnh.

Không nói “toàn DB đạt 3NF” nếu chưa phân tích phụ thuộc hàm và khóa của từng bảng. 1NF/2NF/3NF cần được chứng minh trong phần thiết kế, không suy ra chỉ vì có nhiều bảng. Constraints bảo vệ khóa và điều kiện dữ liệu, không thay thế kiểm quyền hay bảo đảm mọi quy trình đúng. [Microsoft: UNIQUE/CHECK](https://learn.microsoft.com/en-us/sql/relational-databases/tables/unique-constraints-and-check-constraints?view=sql-server-ver17).

**Bằng chứng:** [schema](../Database/01_TaoBang.sql), `BangGiaChiNhanh`, `ChiTietDonHang`, [migration ảnh thẻ](../Database/010-DichVuAnhThe_MauDon.sql).

### A9. JWT, BCrypt và OTP bảo vệ tài khoản như thế nào?

**Trả lời mẫu:** “Mật khẩu lưu dưới dạng BCrypt hash và kiểm bằng Verify. JWT mang claims và chữ ký để API kiểm danh tính/role, issuer, audience và hạn dùng. OTP khôi phục có hạn dùng, giới hạn thử và chỉ dùng một lần khi đặt lại mật khẩu.”

JWT có chữ ký **không đồng nghĩa payload được mã hóa**. HS256 trong code dùng khóa đối xứng; không đưa mật khẩu hoặc khóa bí mật vào token. Cần HTTPS để bảo vệ đường truyền và cơ chế thu hồi phiên nếu triển khai thực tế. [RFC 7519](https://www.rfc-editor.org/rfc/rfc7519).

BCrypt dùng salt nên cùng mật khẩu có thể ra hash khác nhau; muốn phát hiện mật khẩu mới trùng mật khẩu hiện tại phải Verify, không so sánh chuỗi hash. BCrypt không phải mã hóa giải ngược. OWASP hiện ưu tiên Argon2id cho thiết kế mới; việc dự án dùng BCrypt không nên được mô tả là thuật toán tốt nhất trong mọi hoàn cảnh. [OWASP Password Storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).

OTP reset hiện giữ HMAC của mã trong RAM, hết hạn 10 phút, chờ gửi lại 60 giây, khóa 15 phút sau 5 lần sai; có limiter cho các endpoint khôi phục. API restart làm mã cũ mất hiệu lực. Form xác minh trước rồi mới mở đổi mật khẩu; endpoint reset vẫn kiểm OTP, không tin việc client đã mở form. Nhập mật khẩu hai lần tránh gõ nhầm, không phải xác thực hai yếu tố. Việc dùng OTP qua email để reset cũng không chứng minh đăng nhập đã có MFA.

**Giới hạn:** chưa khẳng định đã thu hồi toàn bộ JWT cũ ngay sau đổi mật khẩu; code cấp token theo thời hạn cấu hình, không nên gọi token ghi nhớ dài hạn là refresh token.

**Bằng chứng:** [AuthController](../MaNguon/PhotocopyChain/PhotocopyApi/Controllers/AuthController.cs), [OtpDatLaiStore](../MaNguon/PhotocopyChain/PhotocopyApi/Auth/OtpDatLaiStore.cs).

### A10. SignalR có thay thế database và push notification không?

**Trả lời mẫu:** “SignalR giúp gửi sự kiện cập nhật khi Web/App đang kết nối. Database vẫn là nguồn dữ liệu chính; client nhận sự kiện rồi đọc lại trạng thái khi cần. Nhóm gửi theo người dùng/chi nhánh để hạn chế gửi sai phạm vi.”

Không giả định sự kiện luôn đến hoặc chỉ đến một lần. Khi reconnect cần tải lại API. Groups là cơ chế định tuyến thông điệp, không phải một lớp bảo mật thay thế xác thực/phân quyền. [Microsoft: SignalR groups](https://learn.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-8.0).

SignalR không thay thế FCM/APNs khi app đã tắt. Hiện không nên nói đã có push notification đầy đủ nếu chỉ chứng minh cập nhật khi app mở.

**Bằng chứng:** [CapNhatHub.cs](../MaNguon/PhotocopyChain/PhotocopyApi/Hubs/CapNhatHub.cs), [Flutter realtime.dart](../MaNguon/photocopy_mobile/lib/realtime.dart).

### A11. Đọc trang PDF, trừ tờ giấy và in ảnh thẻ khác nhau ở đâu?

**Trả lời mẫu:** “Nhóm phân biệt trang tài liệu, mặt in, tờ giấy, số bản và số ảnh. Báo giá tài liệu dựa trên cấu hình dịch vụ; ảnh thẻ tính theo từng ảnh, còn PDF dàn ảnh quyết định số tờ A4 cần in.”

Ví dụ 101 trang in hai mặt: mỗi bản cần `ceil(101/2) = 51 tờ`, không đủ điều kiện bấm kim góc tối đa 50 tờ. Hai bản 3 trang cần `2 × ceil(3/2) = 4 tờ`, không phải `ceil(6/2) = 3 tờ` vì mỗi bản là bộ riêng. Quy tắc kim góc đã tính tờ mỗi bản; **thủ tục trừ kho tài liệu hiện vẫn dùng `SoTrang × SoBan`**, nên chưa được tuyên bố mọi trường hợp duplex đã trừ đúng tờ vật lý.

Ảnh thẻ: 8 ảnh 3×4 giá 5.000đ/ảnh → 40.000đ, không phải giá một trang PDF. Bộ dàn ảnh đổi cm sang PDF point theo `cm × 72 / 2,54`, tạo khung raster khoảng 300 dpi, giữ tỉ lệ và không tự cắt khuôn mặt. Tăng DPI không phục hồi chi tiết ảnh gốc mờ. Khi in thật phải dùng Actual size/100%; chưa nghiệm thu mọi máy và loại giấy.

**Bằng chứng:** [QuyTacIn.cs](../MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyTacIn.cs), `sp_CapNhatTrangThaiIn`, [AnhThePdf.cs](../MaNguon/PhotocopyChain/PhotocopyApi/DichVu/AnhThePdf.cs).

### A12. Kiểm thử và báo cáo tài chính cần trình bày thế nào cho trung thực?

**Trả lời mẫu:** “Nhóm phân biệt kiểm thử logic, tích hợp API/DB, widget và nghiệm thu đầu cuối. Test có mock chứng minh hành vi trong điều kiện giả lập, không chứng minh ngân hàng hoặc máy in vật lý đã hoạt động.”

Flutter phân biệt unit, widget và integration test. [Flutter testing overview](https://docs.flutter.dev/testing/overview). Với đồ án quản lý, dùng bảng nghiệp vụ, tỷ lệ ca đạt, phân tích lỗi và thời gian phản hồi; không cần confusion matrix khi không có mô hình phân loại.

Bằng chứng lượt đánh giá trước: .NET 250 đạt/15 DB skip; Flutter 90 đạt/analyze sạch. Đây không phải độ bao phủ phần trăm và không đồng nghĩa mọi yêu cầu đã nghiệm thu. Nhóm phải ghi commit, môi trường và log của lần chạy cuối.

Tài chính cần tách tiền thực nhận, doanh thu đơn hoàn thành, tiền mặt trong ca và khoản hoàn. Báo cáo hiện cộng đơn `HOAN_THANH` theo ngày hoàn thành; `sp_HoanTien` chủ yếu đổi trạng thái. Vì vậy **chưa gọi đây là sổ thu–chi/thu ròng đầy đủ**. Tiền đầu ca 200.000đ là quỹ đầu ca, không phải doanh thu; VietQR không tự cộng vào két tiền mặt.

**Bằng chứng:** [QuanTriController](../MaNguon/PhotocopyChain/PhotocopyApi/Controllers/QuanTriController.cs), `sp_HoanTien`, [bộ kiểm thử .NET](../MaNguon/PhotocopyChain/PhotocopyApi.Tests/PhotocopyApi.Tests.csproj).

## B. Ngân hàng 180 câu hỏi và gợi ý trả lời

Các gợi ý về hướng phát triển phải được nói rõ là **đề xuất**, không phải chức năng hiện có. Không coi ví dụ tính toán hoặc dữ liệu giả lập là số liệu khảo sát/thanh toán thật.

### B1. Đề tài, khảo sát và phạm vi — Q001–Q015

- **Q001. Đồ án giải quyết vấn đề gì?** — Tập trung file, báo giá, theo dõi tiến độ và quản lý tiền/kho thay cho quy trình rời rạc.
- **Q002. Vì sao khách không gửi Zalo rồi chuyển khoản như trước?** — Phần mềm chuẩn hóa cấu hình, lưu lịch sử và theo dõi trạng thái; không khẳng định thay thế hoàn toàn Zalo.
- **Q003. Ai là người dùng và ai là người trả tiền cho hệ thống?** — Phân biệt khách sử dụng dịch vụ với chủ chuỗi vận hành; mô hình kinh doanh chưa được kiểm chứng.
- **Q004. Năm vai trò chính là gì?** — CUSTOMER, SHIPPER, STAFF, BRANCH_ADMIN, SUPER_ADMIN; giải thích nhiệm vụ và ranh giới từng vai trò.
- **Q005. Vì sao mô hình nhiều chi nhánh phức tạp hơn một cửa hàng?** — Giá, kho, máy, ca và quyền bản ghi phải gắn đúng cơ sở.
- **Q006. Nhóm khảo sát bao nhiêu người và khi nào?** — Trả lời từ dữ liệu gốc; tài liệu hiện chưa đủ căn cứ, không suy ngược cỡ mẫu rồi nhận là thật.
- **Q007. Các tỷ lệ khảo sát có đại diện cho mọi khách hàng không?** — Nêu cách lấy mẫu và thiên lệch; mẫu sinh viên không tự đại diện toàn thị trường.
- **Q008. Chức năng nào đáp ứng trực tiếp khó khăn chờ lâu?** — Báo giá, ETA và theo dõi hàng đợi; muốn chứng minh giảm chờ phải đo trước/sau.
- **Q009. Điểm khác biệt với website bán hàng là gì?** — File và quy cách in, gia công, tài nguyên máy/kho và vòng đời xử lý sau thanh toán.
- **Q010. Yêu cầu chức năng và phi chức năng khác nhau thế nào?** — “Đặt đơn” là chức năng; bảo mật, thời gian phản hồi, khả dụng là chất lượng vận hành.
- **Q011. Vì sao chọn phạm vi này cho đồ án tốt nghiệp?** — Giải thích nguồn lực và giá trị nghiệp vụ; không lấy số lượng màn hình làm thước đo duy nhất.
- **Q012. Phần nào nằm ngoài phạm vi hiện tại?** — Máy in vật lý đã nghiệm thu, thu ròng đầy đủ, push lúc app tắt và vận hành đa host.
- **Q013. Vì sao dùng cả web và mobile?** — Web phù hợp thao tác quản trị; mobile phù hợp khách/shipper, nhưng cùng dùng API nghiệp vụ.
- **Q014. Thước đo thành công của đồ án là gì?** — Luồng đúng, quyền đúng, tiền/kho nhất quán và có bằng chứng; không chỉ build hoặc UI đẹp.
- **Q015. Phần đóng góp riêng của từng thành viên là gì?** — Dựa phân công, commit và phần giải thích được; không nhận công việc của người khác hoặc công cụ.

### B2. Kiến trúc, công nghệ và API — Q016–Q030

- **Q016. Trình bày sơ đồ kiến trúc trong một phút.** — Hai client → API → SQL/file; simulator và payOS là các tương tác riêng. Xem A1.
- **Q017. Web đang dùng Blazor Server hay WebAssembly?** — WebAssembly; kiểm `WebAssemblyHostBuilder`, không nhầm SignalR nghiệp vụ thành Blazor Server.
- **Q018. Vì sao Flutter không dùng trực tiếp DTO C#?** — Flutter chạy Dart; trao đổi JSON và ánh xạ model, cần thống nhất hợp đồng API.
- **Q019. Dapper khác EF Core ở đâu?** — Dapper ánh xạ truy vấn SQL; nhóm tự viết SQL/thủ tục, không dựa change tracking và LINQ của EF.
- **Q020. Vì sao giữ stored procedure?** — Phù hợp kiến trúc cũ và tập trung quy tắc DB; đổi lại khó chuyển hệ DB và cần quản lý phiên bản.
- **Q021. Vì sao không kết nối SQL trực tiếp từ app?** — Tránh lộ quyền DB và bỏ qua nghiệp vụ; API làm cổng xác thực/kiểm soát chung.
- **Q022. Đây có phải microservices không?** — Không tự gọi như vậy; backend tập trung và simulator riêng chưa chứng minh kiến trúc microservices.
- **Q023. REST, HTTP và JSON liên hệ thế nào?** — HTTP là giao thức, JSON là định dạng; thiết kế endpoint có tài nguyên/thao tác, không phải cứ JSON là REST chuẩn.
- **Q024. DTO khác entity/model database thế nào?** — DTO là dữ liệu hợp đồng trao đổi; không trả toàn bộ cột nhạy cảm của entity ra client.
- **Q025. Dependency Injection giúp gì trong dự án?** — Cấp cấu hình, logger, hub và service; dễ thay phụ thuộc khi test, nhưng không tự làm code sạch.
- **Q026. Singleton và scoped dùng cho thành phần nào?** — Store chia sẻ trong API là singleton; service nghiệp vụ theo request có scoped; lưu ý thread safety.
- **Q027. `async/await` có tự tăng tốc CPU không?** — Không; hữu ích với chờ I/O, không thay thuật toán và không làm mọi tác vụ chạy song song.
- **Q028. 400, 401, 403, 404, 409, 500 khác nhau thế nào?** — Dữ liệu sai; chưa xác thực; thiếu quyền; không tìm thấy; xung đột; lỗi máy chủ.
- **Q029. Swagger có chứng minh API đúng không?** — Là công cụ mô tả/thử API; cần assertions, phân quyền và kết quả DB để chứng minh đúng.
- **Q030. Nếu cần thay SQL Server bằng PostgreSQL thì đổi gì?** — Không chỉ connection string: SQL dialect, thủ tục, TVP, trigger và migration cần đánh giá lại.

### B3. Thiết kế CSDL và giao dịch — Q031–Q045

- **Q031. Các thực thể trung tâm và quan hệ của chúng?** — Người dùng, chi nhánh, dịch vụ/giá, đơn/chi tiết, file, thanh toán, ca, kho và máy.
- **Q032. Vì sao đơn và chi tiết đơn tách bảng?** — Một đơn có nhiều file/dòng cấu hình; tránh lặp thông tin cấp đơn cho từng dòng.
- **Q033. PK, FK và UNIQUE bảo vệ điều gì?** — Định danh, quan hệ tham chiếu và tính duy nhất; không thay thế kiểm quyền người gọi.
- **Q034. 1NF, 2NF, 3NF nghĩa là gì?** — Giá trị phù hợp mô hình quan hệ; không phụ thuộc một phần khóa; không phụ thuộc bắc cầu thuộc tính không khóa. Cần chứng minh từng bảng.
- **Q035. Vì sao không nói toàn DB đạt 3NF ngay?** — Phải nêu khóa ứng viên, phụ thuộc hàm và ngoại lệ snapshot; số bảng không chứng minh chuẩn hóa.
- **Q036. Vì sao có `ChiTietGiaCong` riêng?** — Một dòng in có nhiều dịch vụ gia công; quan hệ nhiều–nhiều cần lưu lựa chọn và giá lịch sử.
- **Q037. TVP dùng để làm gì?** — Gửi nhiều dòng in/gia công vào thủ tục trong một tham số bảng; phải khớp cấu trúc và thứ tự cột.
- **Q038. Transaction khác một câu UPDATE đơn lẻ thế nào?** — Gom nhiều thay đổi thành đơn vị; trạng thái, kho và thông báo liên quan cần kiểm ranh giới. Xem A3.
- **Q039. Transaction lồng trong SQL Server có phải các giao dịch độc lập?** — Không mặc định; cần hiểu `@@TRANCOUNT`, commit ngoài cùng và ảnh hưởng rollback.
- **Q040. Race condition là gì trong đồ án?** — Hai request cùng quan sát trạng thái cũ rồi ghi; ví dụ cùng bắt đầu in hoặc dùng lượt mã cuối.
- **Q041. Deadlock khác chờ khóa bình thường thế nào?** — Chờ vòng phụ thuộc; cần log, thứ tự khóa nhất quán và retry có kiểm soát, chưa khẳng định không có.
- **Q042. `UPDLOCK/HOLDLOCK` được dùng ở đâu và vì sao?** — Luồng tự xếp in khóa đơn; phân biệt ngữ nghĩa và chi phí blocking. Xem A4.
- **Q043. CHECK có chặn mọi NULL không?** — Không; SQL dùng logic ba giá trị, cần NOT NULL hoặc điều kiện rõ để tránh UNKNOWN lọt qua.
- **Q044. Index làm nhanh mọi thao tác không?** — Có lợi cho truy vấn phù hợp, tăng chi phí ghi/dung lượng; chọn dựa workload và execution plan.
- **Q045. Vì sao rollback test vẫn có thể nhảy mã IDENTITY?** — Bộ đếm identity không bảo đảm liên tục; nhảy số không tự chứng minh có dữ liệu bị mất.

### B4. Báo giá, khuyến mãi và quy cách — Q046–Q060

- **Q046. Công thức tổng tiền đơn là gì?** — Tiền in + gia công + phụ phí gấp + ship − giảm giá; đối chiếu `sp_TinhTien`.
- **Q047. Tính giá ở client có đủ không?** — Client hiển thị dự kiến; máy chủ tính và kiểm lại khi tạo đơn, không tin tổng tiền client gửi.
- **Q048. Vì sao số trang phải lấy từ file đã xử lý?** — Khách có thể sửa request; API dùng metadata file thuộc đúng khách thay số trang khai tùy ý.
- **Q049. Giá theo trang và giá theo ảnh khác nhau thế nào?** — Tài liệu dùng đơn vị dịch vụ/trang/bản; ảnh thẻ dùng số ảnh. Xem A11.
- **Q050. Đổi giá chi nhánh A có làm đổi B và đơn cũ không?** — Bảng giá phân chi nhánh, chi tiết đơn giữ giá chốt; chứng minh bằng test và dữ liệu.
- **Q051. Báo giá rồi quản trị đổi giá trước khi khách đặt thì sao?** — Tạo đơn tính lại; cần thông báo giá mới, không hứa báo giá cũ được giữ vô thời hạn.
- **Q052. Vì sao tiền dùng decimal thay float/double?** — Tránh sai số nhị phân ở phép tính tiền; vẫn cần quy tắc làm tròn rõ ràng.
- **Q053. Khuyến mãi phần trăm áp lên khoản nào?** — Code hiện áp tiền in + gia công, có giới hạn tối đa; không mặc định giảm ship/phụ phí gấp.
- **Q054. Cùng mã giảm giá ở hai chi nhánh có hợp lệ không?** — Có thể là hai bản ghi riêng; tra cả mã và chi nhánh, không chỉ chuỗi code.
- **Q055. Điều kiện khiến mã bị từ chối?** — Sai cơ sở, ngừng, hết hạn, hết lượt hoặc đơn dưới tối thiểu; nêu điều kiện thật trong SQL.
- **Q056. Hai khách dùng lượt khuyến mãi cuối đồng thời thì sao?** — Cần kiểm transaction/khóa và cập nhật có điều kiện; không tuyên bố an toàn nếu chưa test.
- **Q057. 101 trang duplex có bấm kim góc được không?** — Không: 51 tờ mỗi bản vượt 50; tính theo từng bộ, không cộng rồi chia chung.
- **Q058. Ép plastic, lò xo và kim góc tương thích thế nào?** — Plastic được cùng lò xo; lò xo loại trừ kim góc; còn kiểm nhóm gia công theo cấu hình.
- **Q059. Quy tắc kim góc tính tờ đúng thì kho duplex cũng đúng chưa?** — Chưa suy ra được; thủ tục trừ kho tài liệu hiện vẫn dùng trang × bản. Xem A11.
- **Q060. Giấy đứng/ngang có làm thay đổi giá không?** — Là quy cách lưu theo dòng; không tự tăng tiền nếu bảng giá không định nghĩa phụ phí riêng.

### B5. Tài khoản, bảo mật và OTP — Q061–Q075

- **Q061. Authentication và authorization khác nhau thế nào?** — Danh tính và quyền thao tác; quyền từng bản ghi là bước bổ sung. Xem A2.
- **Q062. Ẩn menu quản trị có đủ bảo mật không?** — Không; người gọi vẫn có thể gửi HTTP trực tiếp, API phải kiểm lại.
- **Q063. STAFF tự đổi header chi nhánh được không?** — Middleware chỉ cho CEO dùng header; còn phải kiểm bản ghi ở endpoint.
- **Q064. JWT gồm những phần nào?** — Token JWS thường có header, payload, signature; API phải kiểm chứ không chỉ decode. Xem A9.
- **Q065. HS256 là khóa công khai hay đối xứng?** — Đối xứng trong code; khóa ký không được đưa xuống client.
- **Q066. Có thể đọc payload JWT không, vậy nó bảo mật bằng gì?** — Có thể đọc token ký thông thường; chữ ký chống sửa, HTTPS bảo vệ đường truyền, không đưa bí mật vào payload.
- **Q067. BCrypt khác SHA-256 và mã hóa thế nào?** — Password hashing có salt/work factor; không giải ngược; không dùng hash nhanh đơn thuần thay thế tùy tiện.
- **Q068. Tại sao không so sánh hai BCrypt hash để chặn mật khẩu trùng?** — Salt có thể khác; Verify mật khẩu mới với hash hiện tại.
- **Q069. Có chặn dùng lại mọi mật khẩu trước đây không?** — Hiện chặn trùng mật khẩu hiện tại; chưa chứng minh có lịch sử mật khẩu để chặn toàn bộ quá khứ.
- **Q070. OTP reset có thời hạn và giới hạn gì?** — 10 phút, gửi lại chờ 60 giây, khóa sau 5 lần sai; phân biệt với OTP đăng ký có cấu hình khác.
- **Q071. Vì sao xác minh OTP xong vẫn kiểm lại ở endpoint reset?** — Client có thể gọi thẳng endpoint; server không tin việc UI đã chuyển bước.
- **Q072. Nhập lại mật khẩu hai lần có phải MFA không?** — Không, chỉ tránh gõ sai; MFA cần các yếu tố xác thực độc lập.
- **Q073. Chỉ nhận Gmail khi nhập mới có phải chuẩn email không?** — Là chính sách nghiệp vụ đang yêu cầu, không phải định nghĩa email hợp lệ; tài khoản cũ vẫn đăng nhập.
- **Q074. `.env` và `.gitignore` có bảo vệ tuyệt đối bí mật không?** — Không; chỉ tránh commit theo quy tắc, không mã hóa file hay bảo vệ lịch sử đã lộ.
- **Q075. Đổi mật khẩu có làm JWT cũ hết hiệu lực ngay không?** — Không được khẳng định nếu chưa có revocation/version check; đề xuất token ngắn hạn và quản lý phiên.

### B6. QR, payOS và nhận tiền thật — Q076–Q090

- **Q076. VietQR và payOS đóng vai trò khác nhau thế nào?** — QR hỗ trợ chuyển khoản; payOS cung cấp tạo link/QR và thông tin xác thực giao dịch trong luồng tích hợp.
- **Q077. QR hiển thị trong app có chứa API key không?** — Không được chứa khóa; app nhận dữ liệu QR được API cấp cho đơn hợp lệ.
- **Q078. Quét QR nhưng không xác nhận chuyển tiền thì đơn có trả chưa?** — Chưa; mã QR và hành động quét không phải bằng chứng nhận tiền.
- **Q079. Webhook khác polling/đối soát chủ động thế nào?** — Bên thanh toán gọi đến server; đối soát là server hỏi lại, dùng khi cần khôi phục trạng thái.
- **Q080. Sửa query `status=PAID` có làm đơn đã trả không?** — Không được phép; URL chỉ điều hướng, xác minh tiền ở phía máy chủ. Xem A5.
- **Q081. Webhook ký đúng nhưng số tiền sai xử lý thế nào?** — Không xác nhận đúng đơn bằng chữ ký đơn thuần; giữ ngoại lệ và đối chiếu.
- **Q082. Vì sao cần kiểm mã đơn, tiền tệ và nội dung/reference?** — Liên kết giao dịch với đúng khoản phải thu; tránh nhận nhầm dữ liệu hợp lệ nhưng sai nghiệp vụ.
- **Q083. HMAC có mã hóa số tiền trong webhook không?** — Không; kiểm toàn vẹn/xác thực trong phạm vi khóa, không che nội dung.
- **Q084. Webhook gửi lại mười lần có thu/xếp in mười lần không?** — Cần các guard trạng thái và quyền lệnh; trình bày bằng test lặp, không chỉ nói “có if”.
- **Q085. Tunnel đổi URL thì vấn đề nằm ở đâu?** — Callback công khai/webhook cần địa chỉ hiện tại; link cũ có thể không đến API dù tiền đã nhận.
- **Q086. Tại sao thanh toán thật mà Web vẫn chưa cập nhật?** — Kiểm webhook, DB, đối soát, SignalR và client refresh; không đánh dấu trả tiền bằng tay để che lỗi.
- **Q087. Nhận tiền rồi thiếu kho thì hoàn tiền ngay không?** — Chưa in được không tự đồng nghĩa hủy/hoàn; lưu đã thanh toán, báo chờ và xử lý ngoại lệ.
- **Q088. Khách chuyển hai lần thì hệ thống làm gì?** — Chưa khẳng định đã tự đối soát/hoàn khoản dư; cần quy trình nhận diện và hoàn có chứng từ.
- **Q089. Thế nào là một kiểm thử thanh toán tiền thật đạt?** — Giao dịch thực, webhook/đối soát khớp và DB/Web/App đúng; che thông tin nhạy cảm trong bằng chứng.
- **Q090. Tiền đến sau hủy tự động hoặc hủy thủ công khác nhau thế nào?** — Code có khôi phục điều kiện hủy quá hạn; hủy thủ công không tự khôi phục, chưa hoàn ngân hàng tự động.

### B7. Đơn, kho, ca và COD — Q091–Q105

- **Q091. Trình bày vòng đời đơn nhận tại quầy.** — Chờ xác nhận → xác nhận → đang in → đã in → bàn giao/hoàn thành, kèm điều kiện thanh toán.
- **Q092. Trình bày vòng đời đơn giao COD.** — In xong → nhận giao → giao/giữ tiền → nộp tiền được xác nhận → hoàn thành theo nghiệp vụ.
- **Q093. Vì sao đã thanh toán không tự chuyển hoàn thành?** — Còn sản xuất và bàn giao; tách hai vòng đời. Xem A6.
- **Q094. Đơn COD có phải tự chạy cùng pipeline VietQR không?** — Luồng hiện COD thao tác in thủ công; không nói mọi đơn đều được simulator xử lý.
- **Q095. Khi nào tăng tồn vật tư nhập?** — Sau xác nhận nhận hàng, không phải lúc lập hoặc chỉ duyệt đề nghị.
- **Q096. CEO và quản lý lập phiếu nhập khác nhau gì?** — CEO tạo phiếu tự duyệt; quản lý chờ CEO; cả hai chưa làm tồn tăng ngay.
- **Q097. CEO tự duyệt có trái nguyên tắc kiểm soát nội bộ không?** — Là lựa chọn nghiệp vụ được duyệt; nêu trade-off và đề xuất audit, không gọi kiểm soát hai người tuyệt đối.
- **Q098. Đăng nhập lại STAFF có tạo ca mới không?** — Dùng lại ca đang mở theo quy tắc; không tự đổi tiền đầu ca cũ mỗi lần login.
- **Q099. Hai STAFF cùng chi nhánh có ca riêng không?** — Có theo migration 007; đặc tả cũ một ca phải cập nhật.
- **Q100. 200.000đ đầu ca có tính vào doanh thu không?** — Không; quỹ đầu ca khác tiền bán hàng, cần quản lý quỹ thực tế.
- **Q101. Đăng xuất có phải tự đóng ca không?** — Không mặc định; kết ca là thao tác nghiệp vụ kiểm đếm riêng.
- **Q102. Tại sao VietQR không cộng vào két tiền mặt?** — Tiền vào tài khoản ngân hàng, không vào tiền mặt người trực ca.
- **Q103. Shipper bấm đã nộp tiền có đủ không?** — Người nhận có quyền xác nhận vào ca đúng chi nhánh; không chỉ tin lời shipper.
- **Q104. Báo cáo doanh thu hiện có phải thu ròng không?** — Không; đang tổng hợp đơn hoàn thành, chưa đủ chứng từ hoàn/thu–chi. Xem A12.
- **Q105. Khách tại quầy không có cả tên lẫn số điện thoại được không?** — Cần ít nhất một; có số thì phải đúng quy tắc, không bắt cả hai cho mọi khách.

### B8. Simulator, máy thật và hàng đợi — Q106–Q120

- **Q106. Simulator có thực sự kết nối API hay chỉ chạy bộ đếm UI?** — Là chương trình riêng nhận lệnh/tải PDF/gửi heartbeat/kết quả qua API.
- **Q107. Tại sao simulator nằm ngoài API?** — Tách vòng đời thiết bị khỏi server và tạo điểm nối connector; không tự chứng minh thiết kế đã đạt mọi chuẩn công nghiệp.
- **Q108. Máy phù hợp một đơn được chọn dựa vào gì?** — Chi nhánh, trạng thái, heartbeat và khả năng khổ giấy/màu/duplex; giải thích truy vấn chọn máy.
- **Q109. Máy DB ghi hoạt động nhưng không heartbeat có được chọn không?** — Luồng tự động còn kiểm kết nối; trạng thái cấu hình không bằng trạng thái tức thời.
- **Q110. Heartbeat cũ quá 30 giây chứng minh máy đã ngừng in không?** — Không; chỉ mất kết nối theo ngưỡng, máy có thể vẫn in và giữ job.
- **Q111. Công thức thời gian mô phỏng có xét số bản không?** — Có trang × bản và thời gian đầu; nêu đơn vị và tốc độ chọn. Xem A7.
- **Q112. Giấy hai mặt có giảm một nửa thời gian in không?** — Không tự suy; tốc độ duplex riêng và số mặt in, phụ thuộc định nghĩa đơn vị cấu hình.
- **Q113. PPM và IPM có luôn bằng nhau không?** — Không; phải hiểu đơn vị/điều kiện thông số nhà sản xuất trước khi cấu hình, chưa kiểm mọi máy thật.
- **Q114. Claim và checkpoint dùng để làm gì?** — Ghi chủ lệnh và tiến trình bền qua restart; heartbeat chỉ là trạng thái tức thời.
- **Q115. Máy mất mạng có chuyển ngay job sang máy khác không?** — Không an toàn: có thể in hai bản; cần xác minh dừng/đối soát trước khi chuyển chủ.
- **Q116. Endpoint máy in AllowAnonymous có nghĩa ai cũng gọi được không?** — Không tự suy; controller kiểm khóa connector và định danh/claim; khác JWT người dùng.
- **Q117. Có biết chính xác giấy nào đã ra máy vật lý chưa?** — Chưa; simulator chỉ mô phỏng, cần tín hiệu thiết bị và nghiệm thu riêng.
- **Q118. Callback xong lặp sau khi đơn bàn giao thì sao?** — Không làm lùi trạng thái hoặc cộng bộ đếm lần nữa; chứng minh guard/test.
- **Q119. API restart giữa job thì dữ liệu nào còn, dữ liệu nào mất?** — Claim/checkpoint file có thể còn; heartbeat RAM mất và cần kết nối lại; kiểm đường lưu/quyền.
- **Q120. Nối máy thật có chỉ cần đổi địa chỉ không?** — Không; cần driver/IPP/spooler, chuyển tùy chọn, job ID, lỗi và xác nhận hoàn tất thực. Đây là hướng phát triển.

### B9. File, PDF, ảnh thẻ và mẫu đơn — Q121–Q135

- **Q121. Nhóm kiểm file upload bằng cách nào?** — Nêu giới hạn, loại file và parser thực tế; đuôi file/MIME client khai không đủ chứng minh nội dung an toàn.
- **Q122. Tại sao chuẩn hóa tài liệu sang PDF?** — Ổn định bố cục và đọc số trang; vẫn có rủi ro chuyển đổi font/khổ giấy.
- **Q123. Đọc số trang có chứng minh tài liệu in đẹp không?** — Không; trang trắng, font, lề và nội dung cần preview/kiểm riêng.
- **Q124. Máy chưa có Office/LibreOffice thì Word xử lý sao?** — Báo giới hạn và yêu cầu xuất PDF; không nói mọi định dạng đều xử lý độc lập môi trường.
- **Q125. Hai lượt đổi Office cùng lúc được xử lý thế nào?** — Code dùng khóa chuyển đổi; có thể tạo hàng chờ/blocking, cần đo khi tải tăng.
- **Q126. Vì sao không trả đường dẫn ổ đĩa để client tải file?** — Tải qua endpoint kiểm quyền; đường dẫn nội bộ không phải URL công khai an toàn.
- **Q127. Khôi phục file của đơn đã trả cần giữ điều gì?** — Đúng khách, số trang, trạng thái và metadata ảnh; giữ cấu hình/giá, không thay tùy tiện.
- **Q128. Cùng số trang có chứng minh file mới đúng nội dung cũ không?** — Không; hiện điều kiện metadata không bảo đảm đồng nhất nội dung, cần kiểm người thao tác hoặc hash/chứng từ nếu mở rộng.
- **Q129. Vì sao ảnh 3×4 trong PDF có thể in ra không đúng 3×4?** — PDF có kích thước đúng nhưng driver Fit to page làm scale; cần Actual size và đo bản thật.
- **Q130. 300 dpi có biến ảnh mờ thành ảnh nét không?** — Không; chỉ độ phân giải khung raster, không phục hồi chi tiết mất.
- **Q131. EXIF ảnh điện thoại ảnh hưởng gì?** — Pixel và orientation khác nhau; xử lý hướng trước khi dàn khung để tránh ảnh xoay sai.
- **Q132. Giữ tỉ lệ và crop khuôn mặt khác nhau thế nào?** — Code fit giữ tỉ lệ trong nền trắng; chưa tự crop mặt hay kiểm chuẩn visa.
- **Q133. Dàn nhiều ảnh lên A4 tính số hàng/cột thế nào?** — Dựa kích thước giấy, lề, khe và ảnh; floor sức chứa, ceil số trang theo số ảnh.
- **Q134. Mẫu đơn trống có tự điền thông tin khách không?** — Không; ba mẫu tham khảo để in và viết tay, không cam kết biểu mẫu chính thức mọi cơ quan.
- **Q135. Khi nào được xóa file khách?** — Theo thời hạn và tình trạng các đơn dùng file; phải tránh xóa sớm file đang cần in/phục hồi.

### B10. Web, mobile, UI/UX và thời gian thực — Q136–Q150

- **Q136. UI declarative nghĩa là gì trong Flutter?** — UI xây theo state; trạng thái thay đổi thì rebuild phần liên quan, không cập nhật DOM kiểu web truyền thống.
- **Q137. StatefulWidget và StatelessWidget khác nhau thế nào?** — Một loại có đối tượng State cho dữ liệu thay đổi; không suy Stateless là không bao giờ rebuild.
- **Q138. Vì sao cần kiểm `mounted` sau await?** — Màn hình có thể đã dispose, không được cập nhật state/navigation tùy tiện khi tác vụ trả về.
- **Q139. Validator frontend và backend khác vai trò gì?** — Frontend phản hồi nhanh; backend là kiểm tra bắt buộc vì request có thể bị sửa.
- **Q140. Sáu ô/gạch OTP cần xử lý những thao tác nào?** — Nhập số, paste, xóa/quay lại, focus, bàn phím và lỗi; hình thức đẹp chưa đủ UX.
- **Q141. QR trong app được vẽ và kiểm kết quả như thế nào?** — App hiển thị dữ liệu QR, lấy trạng thái API/real-time; không tự suy từ việc mở ứng dụng ngân hàng.
- **Q142. Dùng cùng điện thoại thì khách quét QR của chính màn hình ra sao?** — Cần trải nghiệm chuyển app/mở link/hướng dẫn tương thích; không đảm bảo mọi ngân hàng hỗ trợ cùng cách.
- **Q143. SignalR mất kết nối có làm mất đơn không?** — DB vẫn giữ đơn; client phải refresh/reconnect và tải lại. Xem A10.
- **Q144. SignalR groups có tự kiểm quyền theo chi nhánh không?** — Không thay auth; code quyết định vào nhóm và dữ liệu gửi, cần kiểm thông tin nhạy cảm.
- **Q145. App tắt có nhận SignalR như push không?** — Không cam kết; FCM/APNs là hướng khác, chưa coi đã triển khai đầy đủ.
- **Q146. CORS có chặn mọi người ngoài website gọi API không?** — Không; cơ chế trình duyệt, không thay JWT/quyền và không chặn curl hay app native.
- **Q147. Responsive không tràn ngang có nghĩa mọi bảng phải ép đủ cột không?** — Không; cần thiết kế ưu tiên thông tin, bảng/card phù hợp, không chỉ thu font gây khó đọc.
- **Q148. Làm sao tránh người dùng bấm đặt đơn hai lần?** — Disable/loading hỗ trợ UX, server vẫn cần kiểm trùng/idempotency; không tin mỗi nút bị khóa.
- **Q149. `adb reverse` giải quyết gì khi test điện thoại?** — Ánh xạ cổng thiết bị về máy tính qua ADB; không phải phương án triển khai production.
- **Q150. App gọi localhost trên điện thoại có phải máy tính không?** — Mặc định là điện thoại; cần reverse hoặc URL server truy cập được, kiểm kết nối/cleartext ở dev.

### B11. Kiểm thử, thực nghiệm và hiệu năng — Q151–Q165

- **Q151. Unit, integration, widget và E2E khác nhau thế nào?** — Phạm vi từ hàm đến nhiều thành phần/UI và luồng thật; nêu bộ test nào thuộc loại nào.
- **Q152. Nhóm có bao nhiêu test và chạy trên phiên bản nào?** — Bằng chứng lượt trước 250 .NET đạt/15 skip và 90 Flutter đạt; cập nhật log của commit chốt.
- **Q153. 340 test đạt có tương đương độ bao phủ 100% không?** — Không; số test khác coverage và yêu cầu nghiệm thu, cần đo và lập mapping. Xem A12.
- **Q154. Test mock payOS có chứng minh nhận tiền thật không?** — Không; chứng minh logic trong giả lập, cần bằng chứng giao dịch thực riêng.
- **Q155. Test DB tại sao bị skip?** — Không có cấu hình DB kiểm thử ở lượt chạy; không được đổi nhãn skip thành pass.
- **Q156. Tại sao DB test cần môi trường riêng?** — An toàn, tái lập, không phụ thuộc dữ liệu khách; rollback không loại bỏ mọi tác dụng bên ngoài.
- **Q157. Cần test biên nào cho kim góc và ảnh thẻ?** — 49/50/51 tờ, trang lẻ duplex; số ảnh 0/1/100/101, thiếu/sai kích thước.
- **Q158. Kiểm callback lặp khác kiểm hai callback đồng thời thế nào?** — Lặp tuần tự không thể hiện mọi race; cần chạy cạnh tranh và assertions tiền/kho/trạng thái.
- **Q159. Kiểm crash/restart ở những điểm nào?** — Sau ghi tiền, trước/sau claim, trước báo xong; so dữ liệu bền và tác dụng nghiệp vụ.
- **Q160. Vì sao không dùng confusion matrix cho đồ án này?** — Không có bài toán phân loại đang nghiệm thu; dùng kết quả nghiệp vụ và thời gian phản hồi phù hợp.
- **Q161. Báo cáo hiệu năng cần những số liệu gì?** — Workload, thiết bị, cỡ dữ liệu, concurrency, số mẫu, p50/p95, tỷ lệ lỗi; chưa có thì ghi chưa đo.
- **Q162. Trung bình thời gian phản hồi có đủ không?** — Có thể che các request rất chậm; bổ sung phân vị và điều kiện đo, không tự bịa p95.
- **Q163. Build/analyze sạch có chứng minh hết lỗi nghiệp vụ không?** — Không; kiểm biên dịch/phân tích tĩnh khác kiểm hành vi và nghiệm thu thực tế.
- **Q164. Test UI cần xét font, bàn phím và accessibility không?** — Có: cỡ chữ lớn, focus, paste OTP, màn hẹp, tương phản; số đo viewport phải ghi rõ.
- **Q165. Làm sao chứng minh đề tài giảm thời gian chờ?** — Đo cùng loại đơn trước/sau hoặc thiết kế thử đối chứng; simulator không tự là số liệu khách thực.

### B12. Phản biện khó, hạn chế và trách nhiệm — Q166–Q180

- **Q166. Code chạy khác đặc tả thì hội đồng tin bản nào?** — Thừa nhận lệch, chốt phiên bản và đồng bộ; không né bằng “code mới nên tự hiểu”.
- **Q167. Em tự viết phần nào, AI hỗ trợ phần nào?** — Trả lời trung thực theo quy định của trường, chứng minh hiểu và tự kiểm được phần nộp.
- **Q168. Hội đồng yêu cầu sửa một quy tắc trực tiếp thì em làm được không?** — Chuẩn bị truy từ DTO → API → SQL → UI/test; không sửa mù trong DB vận hành.
- **Q169. Vì sao controller đơn gần 1.747 dòng?** — Thừa nhận nhiều trách nhiệm, nêu tách theo nghiệp vụ là hướng bảo trì, không nói dài luôn sai.
- **Q170. Nếu khóa API từng xuất hiện trong chat/Git thì giải quyết thế nào?** — Thu hồi/đổi khóa và rà lịch sử; xóa dòng/file không bảo đảm bí mật đã an toàn.
- **Q171. Nếu Office bị treo, dừng mọi tiến trình không có cửa sổ có an toàn không?** — Không chứng minh đúng chủ tiến trình; giới hạn PID/job mình tạo, tránh ảnh hưởng tác vụ khác.
- **Q172. Nếu ngân hàng nhận tiền nhưng DB update thất bại thì ai chịu trách nhiệm?** — Giữ log/bằng chứng, đối soát và xử lý có kiểm soát; không hứa transaction SQL hoàn tiền ngân hàng.
- **Q173. Nếu 10.000 khách dùng cùng lúc thì hệ thống chịu được không?** — Chưa đo thì không khẳng định; nêu bottleneck DB/file/Office và kế hoạch load test.
- **Q174. Nếu chạy hai API server khác nhau thì có in trùng không?** — Có rủi ro nếu store/claim không dùng chung; chưa cam kết phân tán, cần kho bền và cơ chế phối hợp.
- **Q175. Vì sao giá vốn giấy ảnh khởi tạo 0đ?** — Là giá vốn chưa cấu hình, không phải giấy miễn phí; phải nhập giá thật trước khi báo lợi nhuận.
- **Q176. Thông số máy thật và giá ảnh có nguồn xác minh không?** — Trình nguồn/điều kiện; giá suy từ gói và giá đề xuất phải phân biệt giá bán lẻ thực tế.
- **Q177. CSDL backup và script rollback thủ tục khác nhau thế nào?** — Backup phục hồi dữ liệu/trạng thái rộng hơn; script trước migration không tự là rollback toàn schema/dữ liệu.
- **Q178. Nếu chỉ còn một tuần, ưu tiên thêm chức năng hay ổn định?** — Chốt nghiệp vụ lõi, tài liệu, test/bằng chứng, cài đặt; tránh tăng phạm vi sát bảo vệ.
- **Q179. Có thể đưa hệ thống ra cửa hàng dùng ngay không?** — Chỉ sau pilot và xử lý bảo mật, tài chính, backup, máy thật/vận hành; demo tốt chưa bằng production.
- **Q180. Thành công lớn nhất và hạn chế lớn nhất của nhóm là gì?** — Liên kết đặt–trả tiền–in–bàn giao; hạn chế xác minh thực nghiệm/thiết bị/tài chính còn thiếu, kèm lộ trình thật.

## C. Những câu không nên trả lời quá mức

| Không nên nói | Nên diễn đạt đúng |
|---|---|
| “JWT mã hóa nên không đọc được dữ liệu” | Token ký HS256 bảo vệ tính toàn vẹn; payload không mặc định bí mật |
| “Có transaction nên không bao giờ thu/trừ kho trùng” | Có kiểm soát tại các luồng cụ thể; cần kiểm đồng thời và mọi đường ghi |
| “Trang trả về PAID là ngân hàng đã xác nhận” | Chỉ webhook đã xác minh hoặc đối soát máy chủ mới ghi nhận tiền |
| “Simulator xác nhận đã in giấy” | Simulator xác nhận hoàn tất mô phỏng theo hợp đồng API |
| “Cắm máy thật vào là chạy” | Cần connector vật lý và nghiệm thu driver/job/lỗi thực |
| “In hai mặt luôn mất nửa thời gian và trừ nửa giấy” | Tính tờ theo từng bản, tốc độ duplex riêng; code kho hiện còn hạn chế |
| “Đăng nhập lại cộng thêm 200.000đ” | Dùng lại ca; đầu ca không phải doanh thu hoặc khoản tự sinh tiền thật |
| “340 test nghĩa là hoàn thiện 100%” | Có 340 test đạt trong lượt đó, không suy coverage và toàn bộ nghiệm thu |
| “Báo cáo doanh thu là lợi nhuận/thu ròng” | Phải nêu công thức và thiếu chứng từ/chi phí/hoàn đang có |
| “Có SignalR thì app tắt vẫn nhận thông báo” | Cập nhật khi kết nối; push nền cần luồng riêng |
| “Tất cả email hợp lệ phải là Gmail” | Giới hạn Gmail là chính sách nhập mới của dự án |
| “`.env` không ai xem được” | File cấu hình không tự mã hóa; cần quyền file, quản lý khóa và không commit |
| “Mọi ảnh 3×4 đều hợp lệ xin visa” | Có kích thước in, chưa kiểm nền/khuôn mặt/yêu cầu nơi nhận |
| “Dữ liệu khảo sát do nhóm thực hiện” khi không có nguồn | Nêu đúng nguồn và hạn chế; không tạo số liệu hoặc bằng chứng |

## D. Bằng chứng cần chuẩn bị để trả lời tốt

- [ ] Một sơ đồ kiến trúc và một ERD khớp code chốt.
- [ ] Ma trận quyền: vai trò × thao tác × phạm vi bản ghi.
- [ ] Sequence: đặt đơn, xác minh tiền, xếp máy, callback, bàn giao.
- [ ] Demo CEO chọn A không sửa B; STAFF sửa header không vượt quyền.
- [ ] Demo giá A/B khác nhau, giá đơn cũ không đổi sau sửa bảng giá.
- [ ] Demo kim góc 50/51 tờ, plastic cùng lò xo và lò xo loại trừ kim góc.
- [ ] Demo OTP sai/hết hạn/dùng lại, mật khẩu mới trùng hiện tại và nhập xác nhận.
- [ ] Demo ca riêng STAFF, dùng lại ca, CEO tự duyệt nhưng tồn chưa tăng trước nhận hàng.
- [ ] Bằng chứng giao dịch thật đã ẩn thông tin nhạy cảm; có trạng thái DB/Web/App khớp.
- [ ] Demo offline/thiếu kho/thiếu file giữ trạng thái phù hợp; callback lặp không làm lùi đơn.
- [ ] Báo cáo test ghi commit, môi trường, pass/fail/skip và giới hạn mock.
- [ ] Báo cáo hiệu năng nếu đã đo; chưa đo thì để mục kế hoạch, không tạo biểu đồ giả.
- [ ] Danh sách hạn chế: tài chính hoàn, kho duplex, ETA, máy thật, phân tán, push nền.
- [ ] Bảng phân công và minh chứng hiểu code của từng thành viên.

## E. Nguồn lý thuyết chính thức và cách trích dẫn

Nguồn đã tra cứu khi soạn tài liệu ngày 12/10/2026; link đặt cạnh nội dung liên quan ở phần A. Chúng hỗ trợ khái niệm, **không chứng minh mã nguồn của nhóm đã triển khai đúng**. Các hạn chế và hành vi dự án được đối chiếu từ code, không suy từ tài liệu thư viện.

1. [Microsoft — Blazor hosting models](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-8.0): phân biệt WebAssembly và server.
2. [Flutter — Architectural overview](https://docs.flutter.dev/resources/architectural-overview): UI và kiến trúc Flutter.
3. [DapperLib — Dapper](https://github.com/DapperLib/Dapper): object mapping và API truy vấn.
4. [Microsoft — Resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-8.0): quyền theo tài nguyên.
5. [Microsoft — Transaction locking and row versioning](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide?view=sql-server-ver17): giao dịch và đồng thời.
6. [Microsoft — Table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17): `UPDLOCK`, `HOLDLOCK`.
7. [Microsoft — UNIQUE/CHECK constraints](https://learn.microsoft.com/en-us/sql/relational-databases/tables/unique-constraints-and-check-constraints?view=sql-server-ver17): ràng buộc dữ liệu.
8. [payOS — API](https://payos.vn/docs/api/): chữ ký, dữ liệu thanh toán và webhook.
9. [RFC Editor — RFC 7519](https://www.rfc-editor.org/rfc/rfc7519): JWT và claims.
10. [OWASP — Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html): password hashing và giới hạn BCrypt.
11. [Microsoft — SignalR users/groups](https://learn.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-8.0): định tuyến nhóm và giới hạn bảo mật.
12. [Flutter — Testing overview](https://docs.flutter.dev/testing/overview): phân loại kiểm thử.

Khi đưa vào báo cáo, thống nhất chuẩn trích dẫn của khoa, ghi ngày truy cập và chỉ dẫn nguồn cho phát biểu thực sự dựa vào nguồn đó. Không biến nội dung tư vấn hoặc câu trả lời mẫu thành số liệu nghiên cứu của nhóm.

## F. Mẫu trả lời ngắn khi gặp câu chưa làm được

> “Ở phiên bản hiện tại, nhóm em đã làm [phần có code và bằng chứng]. Phần [giới hạn] chưa được nghiệm thu nên nhóm không khẳng định đã hoàn thiện. Nếu phát triển tiếp, nhóm sẽ [giải pháp cụ thể] và kiểm chứng bằng [ca thử/thiết bị/số liệu].”

Ví dụ:

> “Nhóm đã xây simulator riêng nhận lệnh và báo trạng thái qua API. Nhóm chưa nghiệm thu máy in vật lý nên chưa khẳng định phản hồi mô phỏng tương đương giấy đã in thật. Bước tiếp theo là connector spooler/IPP và kiểm thử lỗi, hủy lệnh, job ID trên thiết bị.”

> “Báo cáo hiện phản ánh tổng tiền đơn hoàn thành theo ngày hoàn thành, chưa phải thu ròng sau hoàn. Nhóm cần bổ sung chứng từ thu/hoàn và đối soát theo ngày phát sinh trước khi dùng cho kế toán thực tế.”

**Mục tiêu luyện bảo vệ:** hiểu để giải thích và chứng minh, không phải thuộc nhiều thuật ngữ. Một câu trả lời thừa nhận giới hạn nhưng chỉ đúng code thường tốt hơn một lời khẳng định lớn không có bằng chứng.

