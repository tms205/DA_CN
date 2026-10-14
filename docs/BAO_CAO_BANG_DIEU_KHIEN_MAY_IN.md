# Bảng điều khiển máy in ảo và kết nối máy thật

Ngày cập nhật: 14/10/2026. Không sửa schema, không tạo migration, không gọi payOS hay gửi giấy thật để kiểm thử.

## 1. Đã triển khai

- Trang **Vận hành → Điều khiển máy in** (`/may-in`), giữ màu navy/font hiện có, bố cục thích ứng desktop/tablet/điện thoại.
- Chọn máy trong phạm vi chi nhánh; màn hình trạng thái, đơn hiện tại, tài liệu/quy cách, counter danh mục, tiến độ và thời gian còn lại của máy ảo.
- Tạm dừng / tiếp tục; máy ảo có hết giấy, kẹt giấy, offline và nạp giấy/khôi phục. Khôi phục lỗi không tự bỏ trạng thái tạm dừng.
- Tự cập nhật khoảng 3 giây trên web. Không báo thao tác thành công ngay khi click; chờ heartbeat connector. API lỗi thì khóa nút và báo rõ.
- Trang ca làm việc có liên kết xem máy đang hoạt động trong chi nhánh. Đây là trạng thái trực tiếp, chưa phải nhật ký thiết bị theo từng ca lịch sử.
- Máy ảo lưu thời gian đã thực sự chạy; downtime, pause và lỗi không được tính là đã in. Khởi động lại không tải lại/in lại lệnh đã bắt đầu.
- Connector hỗ trợ chế độ `AO` hoặc `THAT` cho từng máy. Máy thật dùng Windows Print Queue cho cả USB và mạng LAN/shared printer đã cài driver.
- Đọc job Windows, số trang driver còn ghi nhận và lỗi của job nhận diện được; pause/resume chỉ job khớp tên file định danh do connector tạo. Không pause/purge toàn queue.
- Khi gửi PDF chậm vẫn theo dõi điều khiển và heartbeat. Pause chặn gửi thêm file tiếp theo; trang đã vào bộ nhớ máy có thể vẫn tiếp tục ra giấy.
- Ghi checkpoint **trước** thao tác gửi PDF. Nếu tắt/crash hoặc lỗi gửi một phần, không tự gửi lại; giữ đơn chờ kiểm tra.
- Máy thật **luôn cần nhân viên kiểm tra bản giấy** trước khi báo `DA_IN`. Không tự hoàn tất vì chương trình PDF thoát mã 0 hoặc job biến mất khỏi queue.
- Điều khiển được lưu riêng qua restart API. Token lệnh không trả ra giao diện; API kiểm quyền nhân sự, chi nhánh và đúng đơn khi xác nhận.
- `.env` đã có các khóa cấu hình công khai cho máy in, mặc định tất cả vẫn là `AO`; file này đang được Git ignore.

## 2. Pipeline

```text
Đơn VietQR đã trả đủ tiền + file/vật tư/máy phù hợp
  → Luồng xếp máy hiện có (DANG_IN)
  → Connector nhận quyền độc quyền và lưu lệnh
  ├─ AO: tải/kiểm tra file → mô phỏng theo trang × bản / tốc độ
  │      → pause/lỗi giữ tiến độ → đủ thời gian → callback DA_IN
  └─ THAT: kiểm tra engine + queue → tải PDF có token, kiểm tra header
          → lưu dấu đã bắt đầu gửi → SumatraPDF → Windows driver
          → USB hoặc TCP/IP/shared queue → máy vật lý
          → theo dõi job/lỗi → chờ kiểm tra toàn bộ bản giấy
          → nhân viên xác nhận đúng đơn → callback DA_IN
```

Thanh toán và điều kiện nhận lệnh tự động cũ được giữ nguyên: connector đang nhận **VietQR đã thanh toán, đơn đã phân máy và DANG_IN**. Không tự mở rộng sang COD, không tự xác nhận thanh toán. Đơn đã hoàn tất không tăng counter hai lần khi callback lặp.

Không gửi PDF RAW ra USB/cổng 9100: nhiều máy không hỗ trợ PDF trực tiếp. Windows driver chịu trách nhiệm chuyển dữ liệu sang ngôn ngữ máy. Một connector cho một máy tính tại chi nhánh; không chạy hai instance dùng cùng folder/danh tính, không gắn nhiều mã máy vào cùng queue.

## 3. Cách thử máy ảo ngay

1. Khi không có giao dịch đang làm, chạy lại `MaNguon\scripts\chay_web.cmd` để build/nạp API, Web và connector mới.
2. Mở `http://localhost:5064/may-in`, đăng nhập CEO/quản lý/nhân viên. CEO có thể chọn phạm vi chi nhánh; nhân viên chỉ thao tác ở chi nhánh của mình.
3. Chọn máy. Nếu mất kết nối, kiểm tra cửa sổ connector và API 5268; bản API cũ sẽ chưa có endpoint điều khiển mới.
4. Với đơn đủ điều kiện đang in: thử **Tạm dừng**, **Tiếp tục**, **Kẹt giấy**, **Nạp giấy/khôi phục**. Kiểm tra trang/giây không tăng trong thời gian bị chặn.
5. Máy tự nhận lệnh, không có nút bấm để tạo đơn hoặc giả mạo thanh toán. Không có nút hủy/in lại tự động để tránh mất dấu bản đã in.

## 4. Thiết lập máy thật — USB và mạng

**Chưa nghiệm thu trên thiết bị thật.** Trên máy hiện tại, dịch vụ `Spooler` đang `Stopped`. Chưa tự bật dịch vụ, cài driver hay cài SumatraPDF; chưa gửi bản in thử. Cần thực hiện checklist dưới đây cùng model thực tế.

### A. Windows và driver

1. **USB:** cắm máy, cài driver đúng model từ hãng; xác nhận queue xuất hiện trong Settings → Printers & scanners.
2. **Mạng:** máy in và connector cùng LAN; đặt IP ổn định, thêm máy bằng TCP/IP hoặc queue chia sẻ Windows và cài driver hãng. Tên cấu hình là **tên queue Windows**, không chỉ địa chỉ IP.
3. Nếu Spooler tắt, mở PowerShell quản trị và tự bật khi sẵn sàng:

```powershell
Get-Service Spooler
Start-Service Spooler
Get-Printer | Select-Object Name, DriverName, PortName, PrinterStatus
```

4. Chủ động in trang test từ Windows, không từ đơn khách. Xác nhận khay giấy, A4/A3, màu, duplex và hướng giấy đúng.
5. Cài [SumatraPDF từ trang chính thức](https://www.sumatrapdfreader.org/download-free-pdf-viewer). Connector không tải hoặc chạy file exe do trình duyệt gửi lên. Quyền in PDF phải được giữ nguyên, không vượt hạn chế của tài liệu.

### B. Cấu hình `.env` của PhotocopyChain

Vị trí: `C:\Users\USER\Desktop\DA_CN\CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD\MaNguon\PhotocopyChain\.env`.

Ví dụ máy đầu tiên dùng USB (thay bằng tên thực tế):

```dotenv
Simulator__SumatraPdfPath=C:\Program Files\SumatraPDF\SumatraPDF.exe
Simulator__MayIn__0__CheDo=THAT
Simulator__MayIn__0__TenHangDoiWindows=Ricoh MP 2001L USB
```

Ví dụ máy thứ hai dùng LAN:

```dotenv
Simulator__MayIn__1__CheDo=THAT
Simulator__MayIn__1__TenHangDoiWindows=Canon C3525i LAN
```

`0`, `1` là **vị trí trong danh sách cấu hình**, không phải mã máy trong database. Đối chiếu `MaMayIn` tại `PhotocopyPrinterSimulator\appsettings.json` và danh mục web. Máy chưa có thiết bị thật giữ `AO`; queue để trống. Không đổi chế độ/queue khi đang giữ lệnh; worker sẽ chặn nếu checkpoint khác cấu hình.

**Cảnh báo:** bật `THAT` và khởi động connector có thể tự gửi các đơn đủ điều kiện đang chờ của máy đó. Chỉ bật sau khi kiểm tra đúng queue và không có đơn khách dùng để thử. Không thêm key trùng trong `.env`, không chia sẻ `.env` lên Git hoặc trong báo cáo.

### C. Quy cách và xác nhận

- Gửi đúng phạm vi trang, số bản, A4/A3, màu/đen trắng, simplex/duplex long-edge, portrait/landscape và collate.
- Dùng `noscale` để giữ tỷ lệ mẫu đơn/ảnh thẻ. Nội dung sát mép có thể bị cắt bởi vùng in được của driver; phải kiểm tra trên model thật, không tự coi bản giấy chuẩn vì PDF đúng.
- Sumatra `portrait/landscape` điều khiển xoay nội dung; **không tự đảm bảo hướng giấy trong driver**. Cấu hình mặc định driver/khay giấy cần phù hợp và nghiệm thu cả đứng/ngang. Chưa hỗ trợ điều khiển finisher đóng ghim/lò xo/bìa; các dịch vụ gia công vẫn do nhân viên làm.
- Tiến độ máy thật chỉ là số trang driver trả cho job đang còn trong queue, không giả lập % hoàn thành toàn đơn. Không thấy job có thể do driver đổi tên/tắt lưu job; lúc đó không can thiệp job lạ.
- Khi **Chờ kiểm tra bản giấy**: kiểm tra toàn bộ file, phạm vi, số bản, hướng/màu/giấy; nếu đã gửi một phần hoặc chưa gửi đủ, xử lý thủ công phần thiếu trước. Tick xác nhận rồi bấm **Xác nhận đã kiểm tra bản giấy**.
- Nút này không tạo thanh toán; chỉ xác nhận kết quả in của đúng lệnh và đúng chi nhánh. Sau ACK, connector xóa checkpoint và các PDF tạm bên trong folder do nó quản lý; không xóa file gốc khách hàng bằng thao tác này.

## 5. Lưu trạng thái và phục hồi

- Danh tính/checkpoint connector: `%LOCALAPPDATA%\PhotocopyChain\PrinterSimulator` (hoặc `Simulator__ThuMucTrangThai`).
- Quyền nhận lệnh API: `%LOCALAPPDATA%\PhotocopyChain\PrinterClaims`.
- Điều khiển API: `%LOCALAPPDATA%\PhotocopyChain\PrinterControls\dieu-khien.json` (hoặc `PrinterConnector__ThuMucDieuKhien`).
- Không xóa các file này để “thử lại”: có thể làm mất dấu một job đã gửi. File hỏng phải kiểm tra thủ công; không tự tạo lại danh tính/claim.
- Queue/máy offline, hết giấy, mạng lỗi: giữ lệnh và thanh toán, không báo DA_IN. Khôi phục thiết bị/Windows trước rồi đối chiếu giấy.
- Lệnh gửi dở/crash: connector không tự gửi lại; trạng thái chờ kiểm tra là chủ đích. Chưa có cơ chế exactly-once vật lý vì process/spooler/máy không cùng một transaction.

## 6. Files thay đổi trong lượt này

| Nhóm | File tính từ `MaNguon\PhotocopyChain` | Nội dung |
|---|---|---|
| Web mới | `PhotocopyAdmin/Pages/MayIn.razor`, `.razor.css` | Bảng điều khiển, poll, quyền trang, lỗi API, xác nhận giấy |
| Web tích hợp | `PhotocopyAdmin/Layout/MainLayout.razor`, `Pages/CaLamViec.razor` | Menu và liên kết từ ca |
| Hợp đồng | `PhotocopyShared/Dtos/MayInAo/MayInAoDtos.cs` | Mode, queue, tiến độ, trạng thái điều khiển |
| API | `PhotocopyApi/Controllers/MayInAoController.cs` | Danh sách cho nhân viên, thao tác có phân quyền, đọc điều khiển riêng cho connector |
| Trạng thái API | `PhotocopyApi/MayInAo/TrangThaiMayInAoStore.cs`, `Program.cs` | Giữ metadata mode, lưu điều khiển qua restart |
| Connector | `PhotocopyPrinterSimulator/MayInSimulator.cs`, `CauHinhSimulator.cs` | Pause/lỗi, tiến độ hoạt động, checkpoint, luồng thật, heartbeat khi gửi chậm |
| Driver Windows mới | `PhotocopyPrinterSimulator/MayInWindows.cs` | Tham số PDF an toàn, EnumJobs, pause/resume đúng job |
| Cấu hình | `PhotocopyPrinterSimulator/appsettings.json`, `.env` (không commit) | Mặc định AO, các khóa cấu hình THAT tùy chọn |
| Tests | `PhotocopyApi.Tests/DieuKhienMayInTests.cs`, `MayInThatTests.cs`, `BangDieuKhienMayInWebTests.cs`, `MayInAoControllerTests.cs`, `MayInSimulatorTests.cs` | Điều khiển, restore, chống gửi lặp, queue rỗng không hoàn tất, khóa API |
| Browser QA | `verification/bang-dieu-khien-may-in.cjs` | Fixture chặn mọi request thật, responsive và thao tác |

Không sửa mobile, thanh toán, giá hoặc dữ liệu đơn hàng trong lượt này. Những file đang thay đổi từ thư viện mẫu đơn trước đó được giữ nguyên.

## 7. Kết quả kiểm thử

- Build API, Admin và PrinterSimulator: thành công, 0 warning / 0 error.
- Toàn bộ .NET tests tại bản chốt: **417 pass, 20 skip**. Các bài skip cần CSDL kiểm thử riêng; không tính là đã nghiệm thu DB/hardware.
- Browser Chrome: **1440 / 768 / 390px PASS**, không lỗi JavaScript hoặc tràn ngang trang; pause/resume, kẹt giấy/khôi phục, không có mô phỏng trên máy thật, xác nhận có checkbox/đúng đơn, mất API khóa nút.
- Test worker thực chạy với HTTP/thiết bị giả: gửi thành công nhưng queue rỗng không tự DA_IN; restart không gửi PDF lần hai; xác nhận sai mã lệnh không hoàn tất; gửi PDF chậm vẫn nhận pause. Các test không dùng máy vật lý.
- Chưa kiểm chứng bản giấy, finisher, driver của Ricoh/Canon/HP/Xerox thực tế, LAN mất kết nối thật hoặc tổng số trang/copies/hướng giấy trên phần cứng.
- Chưa tự nạp lại tiến trình sản xuất khi chưa được người dùng xác nhận không có giao dịch; chạy launcher sau khi sẵn sàng.

## 8. Nguồn và ảnh hưởng của skill

- [Microsoft: System.Printing](https://learn.microsoft.com/en-us/dotnet/api/system.printing): không dùng namespace này trực tiếp trong ASP.NET/service; chọn connector riêng.
- [Microsoft: EnumJobs](https://learn.microsoft.com/en-us/windows/win32/printdocs/enumjobs), [JOB_INFO_1](https://learn.microsoft.com/en-us/windows/win32/printdocs/job-info-1), [SetJob](https://learn.microsoft.com/en-us/windows/win32/printdocs/setjob): đọc job và thao tác đúng job, không purge queue.
- [SumatraPDF: command line](https://www.sumatrapdfreader.org/docs/Command-line-arguments): engine PDF và tham số; lỗi xảy ra sau khi vào spooler không được phản ánh đầy đủ qua exit code.
- Skills thiết kế/lập kế hoạch giữ ranh giới trước khi thực thi; .NET giữ connector ngoài API; frontend-design áp dụng hướng industrial utilitarian và màu/font cũ; TDD và browser-automation yêu cầu test restore/lỗi và thao tác thực trên UI thay vì coi build là nghiệm thu phần cứng.
