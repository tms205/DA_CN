# Phát hiện và kiểm thử máy in — PhotocopyChain

Cập nhật: 22/10/2026. Không sửa schema hay dữ liệu đơn hàng để thêm chức năng này.

## 1. Đã có gì?

- Trang **Vận hành → Điều khiển máy in** có bảng điều khiển máy ảo và mục **Thiết bị Windows tại cửa hàng**.
- Connector liệt kê queue Windows đã cài cho tài khoản đang chạy nó, mỗi 30 giây. Web cập nhật danh sách mỗi 15 giây; trạng thái máy mỗi 3 giây.
- Hiển thị tên queue, driver, cổng, số job, mã trạng thái và khả năng màu/hai mặt do driver cung cấp. Giá trị chưa rõ không được coi là “không hỗ trợ”.
- Vẫn hiển thị queue ảo như Microsoft Print to PDF để chẩn đoán, nhưng không cho liên kết queue đó với đơn sản xuất. Bộ nhận diện dùng tên/driver/cổng, là heuristic chứ không nhận diện tuyệt đối mọi driver ảo.
- CEO chọn máy logic ở bảng điều khiển rồi mở **Liên kết máy đang chọn** để chọn connector, chế độ AO/THAT và queue. Không cần gõ tên queue trong JSON để đổi liên kết.
- Không tự kích hoạt thiết bị chỉ vì vừa phát hiện. Chế độ Windows cần SumatraPDF và xác nhận đã kiểm tra thiết bị.
- Có chẩn đoán độc lập, tạo PDF thử và gửi một trang thử. Không cần đặt đơn hay thanh toán để dùng những lệnh này.

**Không được đồng nhất:** queue tồn tại ≠ máy đang online; driver không báo lỗi ≠ giấy đã in; job biến mất ≠ tài liệu đã in đủ.

## 2. Pipeline

```text
Cài driver USB / thêm máy in LAN vào Windows tại cửa hàng
    ↓
Connector: EnumPrinters → tên, driver, cổng, trạng thái
           DeviceCapabilities → màu / hai mặt (nếu driver cung cấp)
    ↓ POST có khóa connector + ConnectorId
API: inventory tức thời → lọc theo quyền/chi nhánh → Web Admin
    ↓ CEO kiểm tra và liên kết máy logic với queue
API: lưu JSON nguyên tử + sinh phiên bản liên kết mới
    ↓ connector đọc điều khiển, lấy cấu hình liên kết mới
Nhận lệnh: API kiểm tra chủ connector + đúng phiên cấu hình
    ↓ lưu checkpoint trước khi bắt đầu/gửi từng file
AO: mô phỏng tiến độ              THAT: tải PDF → SumatraPDF → Windows Spooler → driver → USB/LAN
    ↓                                     ↓
Hoàn tất theo mô phỏng            Theo dõi job của chính lệnh; kiểm tra bản giấy
                                          ↓ xác nhận đúng mã lệnh
                                 API cập nhật hoàn tất một lần
```

Connector chạy tại cửa hàng. Nếu API triển khai ở máy khác/cloud, API không tự thấy USB tại cửa hàng; vẫn phải giữ connector trên Windows có driver. Không quét dò tùy ý tất cả IP trong mạng LAN.

## 3. Thử ngay khi chưa có máy thật

### A. Thử nghiệp vụ bằng máy ảo

1. Mở web, vào **Điều khiển máy in**, chọn máy có nhãn **Máy ảo · Mô phỏng**.
2. QR đã thanh toán được xếp tự động như trước. COD nhận tại quầy: tiếp nhận → chọn máy phù hợp đang kết nối → **Gửi lệnh tới máy in**. Sau đó connector xử lý PDF và báo kết quả; không còn nút xác nhận bỏ qua bước in ở hàng đợi.
3. Thử tạm dừng/tiếp tục, hết giấy, kẹt giấy, offline mô phỏng và khôi phục.
4. Kiểm tra tiến độ không tăng lúc dừng/lỗi. Tắt/bật connector phải phục hồi checkpoint, không nhận một đơn hai lần.

Lưu ý: **máy ảo đang nối database vẫn có thể cập nhật trạng thái đơn thử và counter như luồng cũ**. Dùng đơn/dữ liệu demo; không đem đơn khách thật ra thử sự cố. Các nút mô phỏng không tác động Windows hay thiết bị vật lý.

### B. Tạo tài liệu thử, không gửi lệnh in

Mở terminal tại thư mục solution:

```powershell
Set-Location 'C:\Users\USER\Desktop\DA_CN\CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD\MaNguon\PhotocopyChain'
dotnet run --no-build --project PhotocopyPrinterSimulator -- --tao-pdf-thu
```

Lệnh in ra đường dẫn PDF vừa tạo. Mở PDF để kiểm tra một trang A4 có nội dung cố định. File giữ ở `%LOCALAPPDATA%\PhotocopyChain\PrinterSimulator\test-prints`; không đọc file khách, không gọi API, không thu tiền hoặc tăng counter.

### C. Kiểm tra queue Windows

```powershell
dotnet run --no-build --project PhotocopyPrinterSimulator -- --kiem-tra
```

Lệnh này chỉ đọc queue, không khởi động worker nhận đơn. Nếu Windows chỉ có Microsoft Print to PDF thì đó là máy in ảo của Windows, không phải máy in vật lý do ứng dụng tạo.

Lỗi ban đầu là **Print Spooler tắt**, công cụ không có quyền bật. Sau khi người dùng bật, Windows đã thấy HP LaserJet MFP M236sdw với Microsoft IPP Class Driver, trạng thái Normal. Nếu gặp lại lỗi dịch vụ, người dùng mở **PowerShell → Run as Administrator**, chạy:

```powershell
Start-Service -Name Spooler
Get-Service -Name Spooler
Get-Printer | Select-Object Name, DriverName, PortName
```

Hoặc mở `services.msc` → **Print Spooler** → Start. Nếu bị chính sách Windows chặn, xử lý theo quyền quản trị của máy; không tắt các cơ chế bảo vệ để ép chạy.

Sau đó chờ connector quét lại tối đa khoảng 30 giây, rồi **Cập nhật danh sách** trên web. Nút này chỉ tải inventory từ API, không ép Windows quét tức thời. CEO thấy cả thiết bị chưa liên kết; nhân viên chỉ thấy connector đã liên kết với chi nhánh của mình. Nếu chưa liên kết, web hướng dẫn nhờ CEO thay vì kết luận không có máy.

### D. Gửi một trang thử qua queue — tùy chọn

Cần cài SumatraPDF từ nguồn chính thức, điền đường dẫn thật trong `.env` của solution (không đưa khóa/mật khẩu lên Git):

```dotenv
Simulator__SumatraPdfPath=C:\Tools\SumatraPDF\SumatraPDF.exe
```

Đường dẫn trên là ví dụ, phải sửa theo nơi cài trên máy. Khởi động lại connector sau khi thay cấu hình bộ gửi PDF.

```powershell
dotnet run --no-build --project PhotocopyPrinterSimulator -- --in-thu "Microsoft Print to PDF"
```

Chọn chính xác tên từ `--kiem-tra`. Lệnh yêu cầu gõ **IN THU**, chỉ gửi một trang A4/đứng/đen trắng/một mặt/một bản; từ chối khi queue đã có job. Không tạo đơn, không gọi payOS, không thay đổi doanh thu/counter. PDF đầu vào được giữ lại để đối chiếu.

**Giới hạn:** Microsoft Print to PDF có thể mở hộp thoại Save As, không phù hợp chạy không tương tác/Windows Service. Sumatra không đảm bảo mọi driver ảo nhận được lệnh im lặng. Khi timeout/lỗi, kiểm tra Windows queue trước, không chạy lại liên tục. Người dùng hiện chọn chưa in thử vật lý; không được coi việc Windows nhận queue là nghiệm thu tờ giấy.

Đã được người dùng đồng ý tải SumatraPDF **3.6.1 portable x64** từ trang chính thức, xác minh Authenticode **Valid**, và cấu hình trong `.env` tới `%LOCALAPPDATA%\PhotocopyChain\Tools\SumatraPDF-3.6.1\SumatraPDF.exe`. SHA-256 của EXE: `719f689b34f47be8ca105ce8484948474dafde0e106bab599e4a89326070c3d0`. Không cài lại driver, không tắt Windows Protected Print Mode, không gửi trang thử, không bật liên kết máy thật.

### E. COD tại quầy và đơn từ phiên bản cũ

- API chỉ cho gửi COD `TAI_QUAY`, trạng thái thanh toán chưa trả hoặc đã trả; chặn đơn hoàn tiền/COD giao hàng. QR vẫn phải trả tiền trước.
- Thao tác gửi kiểm tra quyền chi nhánh, file PDF, máy kết nối; phân công và chuyển `DANG_IN`/trừ kho trong cùng transaction. Bấm gửi lặp không trừ kho hai lần hoặc đổi máy.
- Yêu cầu COD được lưu bền vững tại `%LOCALAPPDATA%\PhotocopyChain\PrinterRequests\yeu-cau-in-cod.json`; có thể đổi bằng `PrinterConnector__ThuMucYeuCauCod`. Không sửa schema CSDL.
- COD cũ đang `DANG_IN` mà chưa có yêu cầu này **không tự được gửi** khi nâng cấp. Nút **Gửi đơn COD này tới máy đã chọn** yêu cầu xác nhận đã kiểm tra chưa in, nhằm tránh in lại giấy của phiên bản thủ công.
- Tiến độ cập nhật mỗi 3 giây ở hàng đợi. Máy ảo có phần trăm mô phỏng; máy thật chỉ hiện số trang driver cung cấp, không bịa phần trăm khi chưa có dữ liệu. Kết thúc in không đồng nghĩa thu tiền COD.
- Không thể gọi API web với `DA_IN` để bỏ qua connector. Nếu gửi PDF lỗi hoặc checkpoint không xác minh đã gửi đủ file, giữ `LOI`, không mở xác nhận hoàn tất và không tự gửi lại.
- Máy Windows vẫn cần đối chiếu bản giấy sau bước gửi thành công và spooler không còn xử lý. Đây là kiểm tra sau khi đã gửi thật, không phải nút đánh dấu xong chỉ vì đã chọn máy. Không cam kết tự xác nhận vật lý với mọi driver.
- Khi chuyển đường hoàn tất sang connector, giữ xử lý in lại miễn phí: đơn kỹ thuật không thu tiền; đơn gốc COD chưa trả vẫn chờ bàn giao/thu tiền. Dọn PDF của đơn kết thúc do job hiện có thực hiện.

## 4. Khi có máy in thật

1. USB: cắm máy, cài driver đúng hãng; LAN: thêm queue bằng IP/IPP/WSD hoặc driver hãng qua Windows. In Test Page trong Windows trước.
2. Chạy connector bằng đúng tài khoản Windows thấy queue. Dịch vụ Windows có thể không thấy các queue chỉ được cài cho tài khoản đăng nhập khác.
3. Cấu hình `Simulator__SumatraPdfPath`, giữ khóa connector chỉ ở máy chủ/connector. Không để khóa trong WebAssembly hoặc JavaScript.
4. Chạy `--kiem-tra`, rồi `--in-thu "Tên queue chính xác"`, đối chiếu tờ giấy.
5. CEO chọn đúng máy logic/chi nhánh → **Liên kết máy đang chọn** → Windows → đúng queue → đọc cảnh báo → xác nhận kiểm tra → Lưu.
6. Driver báo thiếu năng lực màu/hai mặt so với danh mục thì API từ chối liên kết. Khổ tối đa và các khả năng khác vẫn cần đối chiếu thủ công; không suy ra từ tên model.
7. Gửi đơn thử nhỏ, kiểm tra từng quy cách: giấy, hướng, mặt, màu, phạm vi trang, số bản, và thứ tự file. Gia công như đóng lò xo vẫn do nhân viên thực hiện, không tự biến thành lệnh finisher của máy.
8. Theo dõi Windows queue, sau đó kiểm tra đủ toàn bộ bản giấy mới bấm **Xác nhận đã kiểm tra bản giấy**. Máy vật lý không tự hoàn tất chỉ vì job rời spooler.
9. Sau khi kiểm thử xong mới nhận đơn sản xuất.

## 5. Dữ liệu, quyền và bảo vệ khi gián đoạn

- Liên kết lưu mặc định tại `%LOCALAPPDATA%\PhotocopyChain\PrinterLinks\lien-ket-may-in.json` trên máy chạy API. Có thể đổi thư mục bằng `PrinterConnector__ThuMucLienKet`. Sao lưu cùng cấu hình vận hành; không chỉnh lúc đang in.
- Inventory chỉ giữ trong bộ nhớ; API restart phải đợi connector báo lại, không hiển thị trạng thái “online” cũ như dữ liệu mới.
- Chỉ CEO đổi liên kết; quản lý/nhân viên xem thiết bị thuộc chi nhánh đã liên kết. Connector chưa được liên kết chỉ CEO thấy.
- Một connector được ghim vào một chi nhánh khi liên kết đầu tiên. Một queue không gắn đồng thời hai máy logic trên cùng connector, kể cả queue cấu hình thủ công được connector báo lên.
- Phiên bản này dùng các mã máy đã khai báo trong `Simulator:MayIn`. Không tự tạo/xóa máy trong database chỉ vì Windows vừa phát hiện thêm queue. Muốn bổ sung máy logic mới cần thêm danh mục và cấu hình worker tương ứng; popup chỉ thực hiện ánh xạ queue.
- Không cho tự chuyển một máy đã liên kết sang ConnectorId khác. Việc thay máy tính cửa hàng cần quy trình bảo trì/đối chiếu lệnh, không dùng nút tự ép chuyển khi chưa biết giấy đã in hay chưa.
- Chặn đổi liên kết khi API còn quyền lệnh đang giữ. Cấp quyền lệnh và đổi liên kết dùng chung khóa; phiên cấu hình cũ bị từ chối ở bước nhận lệnh.
- Ghi checkpoint trước khi gửi bên ngoài tiến trình. Sau gián đoạn không tự gửi lại file đã bắt đầu; có thể phải kiểm tra phần in dở và xử lý thủ công. Đây là tránh tự in lặp, **không phải bảo đảm exactly-once ở phần cứng**.
- Chỉ tạm dừng/tiếp tục các job đối chiếu đúng ID và tên tài liệu của lệnh, không xóa/pause toàn bộ queue của Windows.
- Không áp dụng kiến trúc nhiều API replica dùng file JSON riêng rẽ. Khi cần nhiều node phải thiết kế kho trạng thái và khóa phân tán riêng.
- Nếu mở API/connector ra ngoài máy cá nhân, phải có HTTPS/VPN và phân phối khóa an toàn. Khóa connector hiện dùng cơ chế chia sẻ của hệ thống cũ, chưa phải chứng thư riêng theo từng cửa hàng.

## 6. Mã nguồn chính

| Phần | Tệp trong solution |
|---|---|
| DTO inventory/liên kết | `PhotocopyShared/Dtos/MayInAo/PhatHienMayInDtos.cs` |
| EnumPrinters/DeviceCapabilities | `PhotocopyPrinterSimulator/PhatHienMayInWindows.cs` |
| Lệnh chẩn đoán và PDF thử | `PhotocopyPrinterSimulator/ChanDoanMayIn.cs` |
| Đồng bộ/worker/checkpoint | `PhotocopyPrinterSimulator/MayInSimulator.cs` |
| Gửi PDF và theo dõi Spooler | `PhotocopyPrinterSimulator/MayInWindows.cs` |
| API inventory/quyền liên kết | `PhotocopyApi/Controllers/PhatHienMayInController.cs` |
| Lưu JSON, phiên bản, khóa liên kết | `PhotocopyApi/MayInAo/PhatHienMayInStore.cs` |
| Kiểm soát quyền nhận lệnh | `PhotocopyApi/Controllers/MayInAoController.cs` |
| UI popup và inventory | `PhotocopyAdmin/Components/PhatHienMayIn.razor` |
| Unit/integration giả lập thiết bị | `PhotocopyApi.Tests/PhatHienMayInTests.cs`, `MayInThatTests.cs`, `MayInSimulatorTests.cs` |
| Browser QA cô lập toàn bộ API | `verification/phat-hien-may-in.cjs` |

## 7. Kết quả kiểm tra trên mã cuối

- Build solution Debug: thành công, 0 warning, 0 error.
- Bộ test tự động trên bản cuối: **532 đạt / 0 lỗi / 27 bỏ qua** vì cần môi trường riêng. Test SQL mới chỉ SELECT biểu thức điều kiện với tham số fixture (QR/COD, trả/chưa trả/hoàn tiền, quầy/giao, có/không yêu cầu gửi); không chạy migration hay tạo/xóa dữ liệu test trên DB thật.
- Test mới bao gồm: persist/restart, dữ liệu quá hạn, phân nhánh, queue trùng/ảo/offline, thiếu bộ gửi PDF, xác nhận năng lực, phiên cấu hình cũ, connector sai chủ, bảo vệ snapshot, kiểm tra PDF bằng PdfPig.
- Giữ các test hiện có cho khôi phục checkpoint, không gửi lại PDF khi restart, tạm dừng lúc gửi chậm và chỉ xác nhận đúng mã lệnh.
- Một lượt chạy có timeout ở test khôi phục máy ảo với ngưỡng 8 giây; chạy riêng và hai lượt kiểm tra tiếp theo đều đạt. Chưa thay ngưỡng test cũ; nếu CI tái hiện cần đo lịch chạy/tải máy, không coi một lần retry là chứng minh không có lỗi timing.
- Browser QA: CEO ở 1440/768/390px và nhân viên ở 390px; popup, xác nhận trước lưu, vô hiệu queue ảo, quyền hiển thị, lỗi inventory không làm hỏng bảng máy ảo, không tràn ngang, không lỗi JavaScript. API được mock/chặn nên không tác động đơn hay liên kết thật.
- Endpoint inventory thật không đăng nhập trả HTTP 401; API/web được nạp lại sau khi người dùng xác nhận không có giao dịch.
- Đã tạo được PDF thử một trang. Khi người dùng bật Spooler, chẩn đoán native thực tế đọc được HP M236sdw qua WSD/Microsoft IPP Class Driver và Microsoft Print to PDF; không gửi lệnh in.
- Browser QA COD ở 1440/768/390px: máy offline bị khóa chọn, chỉ gửi `DANG_IN`, hiển thị tiến độ, lỗi giữ chưa hoàn tất, máy thật không có phần trăm giả, in xong vẫn chưa thu COD, đơn cũ có bước xác nhận gửi và có thể hủy. Mọi API trong browser QA được mock/chặn, không tác động hai đơn người dùng.
- Trước/sau nạp bản mới, chỉ đọc hai COD đang mở: **DH00054 = DA_XAC_NHAN, DH00055 = DANG_IN**, đều chưa thanh toán, máy số 4. Không gửi hai đơn này ra HP, không sửa trạng thái hoặc thu tiền thay người dùng.
- **Chưa nghiệm thu:** bản giấy, gửi PDF qua USB/LAN thực tế, pause/resume ở thiết bị, in hai mặt/màu/phạm vi thực tế, hay Save As từ driver PDF. Người dùng yêu cầu chỉ sửa code/cấu hình, chưa in thử. Cần xác nhận trước khi kích hoạt queue HP nhận đơn thật.

## 8. Tài liệu kỹ thuật chính thức

- [Microsoft: EnumPrinters](https://learn.microsoft.com/en-us/windows/win32/printdocs/enumprinters)
- [Microsoft: DeviceCapabilities](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-devicecapabilitiesw)
- [Microsoft: JOB_INFO_2 và giới hạn xác nhận PRINTED](https://learn.microsoft.com/en-us/windows/win32/printdocs/job-info-2)
- [SumatraPDF: tham số dòng lệnh](https://www.sumatrapdfreader.org/docs/Command-line-arguments)
