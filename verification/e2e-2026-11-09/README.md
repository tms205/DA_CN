# Kiểm chứng Bảng 5.1 và Bảng 5.2

## Kết quả tầng HTTP và SQL

Đợt 09/11/2026 thực thi **55 bài, 55 đạt**, gồm HTTP qua host kiểm thử, kiểm chữ ký webhook và SQL trên bảng tạm. Kết quả chi tiết nằm trong [01-tang-http-va-sql.trx](01-tang-http-va-sql.trx).

Hai yêu cầu bàn giao COD đồng thời chỉ cộng tiền ca một lần. SQL giữ nguyên trạng thái đơn đã hủy khi ghi nhận tiền đến muộn và chặn yêu cầu hủy của khách khi hết 120 giây. Đây là bằng chứng theo tầng, được ghi riêng với lần chạy tích hợp dưới đây.

## Kết quả tích hợp

Ngày 09/11/2026, **11 bài được thực thi, 11 đạt, 0 lỗi, 0 bỏ qua**. Bản build tại thời điểm thử chạy API ở `127.0.0.1:15268` với database `PhotocopyChain_E2E_20261109` và dữ liệu tổng hợp. Các ca gọi qua HTTP, dùng JWT do API thử cấp và đối chiếu kết quả với SQL Server. [Tệp kết quả 02-e2e-lan-1.trx](02-e2e-lan-1.trx)

| Ca | Kết quả thực tế để điền báo cáo | Trạng thái theo phạm vi |
|---|---|---|
| TC-01 | Khách gọi điểm đăng nhập Web và nhân viên gọi điểm đăng nhập App đều nhận HTTP 403, không cấp token. Đúng kênh nhận HTTP 200. | PASS qua HTTP và SQL |
| TC-02 | Chủ file đọc được. Khách khác đọc hoặc dùng mã file đó đặt in đều nhận HTTP 404. Số đơn mới bằng 0. | PASS qua HTTP và SQL |
| TC-05 | Webhook ký hợp lệ báo 19.000đ cho đơn 20.000đ. API báo lệch tiền, đơn vẫn chưa thanh toán, chưa gán máy và thư mục lệnh in trống. | PASS với webhook mô phỏng |
| TC-07 | Hủy đơn thử thành công, sau đó webhook đủ 20.000đ ghi nhận thanh toán. Đơn vẫn đã hủy, không có lệnh in. | PASS với webhook mô phỏng |
| TC-08 | CEO hủy đơn đang in nhận HTTP 400. Trạng thái đơn và thanh toán giữ nguyên. | PASS qua HTTP và SQL |
| TC-09 | Khách hủy từ mốc 120 giây sau thanh toán nhận HTTP 400. Đơn vẫn chờ tiếp nhận và đã thanh toán. | PASS qua HTTP và SQL |
| TC-11 | Webhook ghi nhận tiền khi file đã sẵn sàng và connector chưa kết nối. Đơn chờ xếp in, chưa gán máy, không có lệnh in. | PASS với webhook mô phỏng, connector chủ động ngắt |
| TC-13 | Hai yêu cầu bàn giao cùng đơn COD, một HTTP 200 và một HTTP 400. Tiền ca chỉ tăng 20.000đ, đơn hoàn tất. | PASS qua HTTP và SQL |
| TC-15 | Shipper nhận đơn chưa trả tiền bị HTTP 409. Không tạo lượt giao và đơn giữ trạng thái đã in. | PASS qua HTTP và SQL |
| TC-17 | Thiếu GPS hoặc ảnh đều nhận HTTP 400. Đơn, lượt giao và bằng chứng giữ nguyên. Hai bài kiểm Flutter cũng đạt, gồm chặn gửi khi thiếu GPS và giữ dữ liệu khi lỗi mạng. | PASS API và SQL, đạt kiểm thành phần Flutter. Chưa chạy trên thiết bị Android thật. |
| TC-19 | Nhận kho lần đầu HTTP 200, lần lặp HTTP 400. Tồn kho chỉ tăng 7 đơn vị và phiếu ở trạng thái đã nhập. | PASS qua HTTP và SQL |

Mã kiểm thử nằm trong [E2EBaoCaoTests.cs](../../MaNguon/PhotocopyChain/PhotocopyApi.Tests/E2EBaoCaoTests.cs). Khi cập nhật hai bảng, giữ rõ phạm vi webhook mô phỏng, kiểm tra hàng đợi và kiểm thiết bị. Các ca OCR đã có trạng thái kiểm thành phần trong báo cáo được giữ riêng.

## Cách chạy lại

1. Tạo database riêng với `pwsh -File verification/e2e-2026-11-09/Prepare-E2E.ps1 -CreateIsolatedDatabase`. Script chỉ tạo catalog `PhotocopyChain_E2E_20261109` và giữ database đó để đối chiếu. Cấu hình thử nằm trong thư mục tạm theo đường dẫn script in ra.
2. Mở cửa sổ PowerShell mới, đặt `PHOTOCOPY_ENV_FILE` bằng đường dẫn cấu hình thử và chạy `dotnet run --project MaNguon/PhotocopyChain/PhotocopyApi/PhotocopyApi.csproj --no-launch-profile --urls http://127.0.0.1:15268`.
3. Trong cửa sổ khác, đọc ba dòng bắt đầu bằng `PHOTOCOPY_E2E_` từ cấu hình thử vào biến môi trường. Chạy `dotnet test MaNguon/PhotocopyChain/PhotocopyApi.Tests/PhotocopyApi.Tests.csproj --filter FullyQualifiedName~E2EBaoCaoTests --logger trx`. Mỗi ca tạo tài khoản và dữ liệu tổng hợp riêng trong database thử.
4. Dùng tệp TRX để đối chiếu từng mã TC. Ghi bản chạy, điều kiện mô phỏng và kết quả quan sát trước khi chuyển trạng thái trong báo cáo.
