# Kết quả thử model local — 21/10/2026

## Kết luận ngắn

Đã tải mã nguồn VietOCR và trọng số DungHugging, nạp được model thật trên RTX 3050 Laptop 4 GB. Chạy được cả GPU và CPU, không train lại. Có thể nghiên cứu tiếp; **chưa đủ căn cứ để đưa vào tự động đọc/in tài liệu thật**.

## Thay đổi đã thực hiện

1. Chuyển toàn bộ OCR cũ sang `C:/Users/USER/Desktop/DA_CN/photocopy_ocr_backup_20261021`. Không xóa vĩnh viễn dữ liệu hoặc checkpoint; ZIP VNOnDB vẫn đúng SHA256 ban đầu `446c0d95dcbe3609db40205e0f9e62799eb59470c9ff2abac0ec1b84f7aead92`.
2. Tạo lại `MaNguon/photocopy_ocr` chỉ để nghiên cứu model có sẵn. Các nguồn/revision/giấy phép/hash nằm trong NGUON_MODEL.md.
3. Clone nguồn upstream nguyên trạng vào third_party/vietocr. Cài venv riêng Python 3.11.9, PyTorch 2.6.0+cu124, torchvision 0.21.0+cu124 và dependency inference tối thiểu.
4. Tải weights/config/model card/evaluation của DungHugging; kiểm hash weights và config trước mỗi lần nạp. Không dùng pickle tùy ý: torch.load(weights_only=True), strict state_dict.
5. Viết bộ CLI thu_ocr.py và chay_thu.cmd; nhận một ảnh dòng, benchmark CSV, trả text/thời gian/cờ cần kiểm tra, xuất JSON không ghi đè.
6. Không sửa API, web, mobile, database, chay_web.cmd hay tiến trình hệ thống đang hoạt động. Chưa có API OCR, tách dòng, xử lý cả trang, xuất Word/PDF hoặc màn hình sản phẩm.

## Điều kiện đo

- Máy người dùng: i7-12700H, RAM 16 GB; xác nhận thực tế CUDA hoạt động trên NVIDIA GeForce RTX 3050 Laptop GPU, driver 566.07.
- Cùng 30 dòng của 18 mã người viết chọn ngẫu nhiên seed 42 từ validation VNOnDB đã có, không chọn lại sau khi nhìn kết quả.
- Batch 1; CPU dùng 4 thread; FP32; greedy decoding, giới hạn 256. Không sửa chính tả bằng LLM.
- Đo từng ảnh gồm đọc/tiền xử lý/nhận dạng/giải mã. Nạp model và lượt warmup đầu được tách khỏi thời gian trung bình.
- Đây là một lượt đo mỗi thiết bị khi các ứng dụng khác vẫn đang mở; không phải benchmark chuẩn hóa nhiều lần.

| Chỉ số | GPU RTX 3050 | CPU |
|---|---:|---:|
| Thời gian trung bình / dòng | 0,483 giây | 1,217 giây |
| Trung vị / dòng | 0,506 giây | 1,186 giây |
| Dòng chậm nhất | 0,726 giây | 1,864 giây |
| Tổng nhận dạng 30 dòng, chưa tính load/warmup | 14,497 giây | 36,521 giây |
| Nạp model và thư viện | 5,137 giây | 5,135 giây |
| CER | 3,9867% | 3,9867% |
| WER | 9,5949% | 9,5949% |
| Đúng nguyên dòng sau chuẩn hóa NFC/khoảng trắng | 18/30 (60%) | 18/30 (60%) |

- Tổng edit lỗi ký tự: 84 trên 2.107 ký tự nhãn; tổng edit lỗi từ: 45 trên 469 từ nhãn.
- GPU nhanh khoảng 2,52 lần CPU trong lượt đo này. Không lấy tỷ lệ này làm cam kết mọi máy/ảnh.
- PyTorch báo peak tensor allocation GPU khoảng **174,96 MiB** trong benchmark. Đây KHÔNG phải tổng VRAM ứng dụng/driver/Windows; CUDA context, cache và các chương trình khác còn chiếm thêm.
- Đã đối chiếu trực tiếp hai báo cáo theo tên ảnh và text: **30/30 chuỗi CPU/GPU giống nhau**, không có dòng chạm giới hạn giải mã (đều gặp EOS).

## Những lỗi cần thấy rõ

Ví dụ đọc đúng hoàn toàn:

> hàng ngàn giấy tờ các loại đã được tìm thấy như vậy. Mỗi sáng, nhân viên

Ví dụ đọc sai rõ rệt, không được che giấu:

| Nhãn gốc | Model đọc |
|---|---|
| thẳng Phú Hữu ". | thẳng phú thũ ". |
| chúng tôi phải lội suối đi vào rừng mò cua bắt cá, kiếm rau, xin đu đủ... ăn cho khỏi | chúng tôi phải đội sáu đi mà rằng ai em bắt cáo kiểm rau xem xem đu đói... và cho khi |

Một dòng dài bị bỏ cụm “tình hình”, tên riêng Kuala Lumpur bị đọc sai. Có dòng CER tới 29,41% dù CER tổng thấp. Cần hiển thị ảnh cạnh text và yêu cầu người dùng duyệt trước khi in.

## Giới hạn của kết quả

- Mẫu là ảnh raster từ nét bút InkML, không phải ảnh chụp điện thoại có bóng, nghiêng hoặc nền vở.
- Không xác minh được việc trùng dữ liệu với quá trình huấn luyện của tác giả. Không gọi đây là kiểm thử độc lập hoặc công bố độ chính xác 96%.
- Chưa kiểm thử độ bền nhiều người dùng/nhiều trang/ảnh chữ quá xấu. Chưa đánh giá tách dòng hoặc thứ tự đọc.
- Không so trực tiếp 3,99% ở đây với 85% của run TrOCR cũ: khác model, dữ liệu đánh giá và điều kiện.
- Giấy phép source, trọng số và dataset là các lớp khác nhau. Giữ ghi công; chưa xác nhận đủ điều kiện triển khai thương mại.

## Kiểm tra phần mềm

- 10 unit test: tính CER/WER có trọng số, giữ dấu/chữ hoa, CER có thể >1, Unicode, nhãn rỗng, hash, file ảnh, chặn đường dẫn thoát manifest.
- 1 integration test với weights thật: chặn socket connect khi nạp model/nhận dạng; model vẫn chạy và trả text. **11/11 test đạt, không skip khi bật OCR_TEST_MODEL=1.**
- pip check: không có dependency hỏng.
- chay_thu.cmd chạy thành công mẫu mặc định, trả đúng dòng văn bản ở ví dụ trên.
- Cảnh báo upstream `enable_nested_tensor ... batch_first ...` là cảnh báo cơ hội tối ưu, không phải lỗi nhận dạng. Giữ nguyên source thay vì sửa kiến trúc chỉ để giấu cảnh báo.

## Xem và chạy lại

- `reports/vnondb-30-gpu.json`, `reports/vnondb-30-cpu.json`: mọi dòng gốc/dự đoán/thời gian/metrics, không chỉ các dòng tốt.
- `samples/labels.csv` và `samples/images`: bộ mẫu chính xác đã đo; không đưa dataset vào Git.
- Nhấp `chay_thu.cmd`, hoặc kéo ảnh **một dòng** vào file đó.
- Chạy lại toàn bộ test:

```powershell
$env:OCR_TEST_MODEL='1'
$env:PYTHONIOENCODING='utf-8'
.\.venv\Scripts\python.exe -m unittest discover -s tests -v
```

Bước tiếp theo: thu 20–30 ảnh thật từ nhiều người trong nhóm, gõ nhãn đối chiếu và kiểm thử riêng. Chỉ sau đó mới quyết định tích hợp và làm tách dòng cả trang.

## Bổ sung ngày 22/10/2026: ảnh vở ô ly thực tế chưa đạt

- Ảnh gửi ngày 21/10, kích thước 863 × 1920, có nền bàn, tay và giấy ô ly. Hai lượt thử cũ đều ra 1 vùng `[0,0,863,1920]`, kết quả `là thanh niên 20 - 29 tuổi.` không khớp ảnh. Đây là lỗi tách dòng, không phải người dùng chọn nhầm chế độ.
- Bổ sung khoanh vùng bằng chuột, lọc đường kẻ OpenCV, tách đỉnh mật độ dòng và chặn vùng tách lớn bất thường. Chế độ một dòng cũng từ chối ảnh có chiều cao lớn hơn chiều rộng.
- Kiểm thử lại dùng bản ảnh PNG toàn khung đã lưu trong `reports/trang-20261021-145510-002219/dong/001.png` (file JPG Desktop ban đầu không còn tại đường dẫn cũ). Vùng chữ chọn cho lần kiểm tra là `[125,610,855,1135]`; đây là tọa độ kiểm thử ảnh này, không chèn cứng vào chương trình.
- Bản mới tìm được 10 dòng. Kết quả và vùng đọc tại `reports/trang-20261022-090707-536961/`. Tổng 14,05 giây gồm 8,67 giây nạp model, CUDA. **Phần nhận dạng vẫn sai nhiều, chưa đạt để sử dụng thực tế.** Không thay kết quả model bằng nội dung người đánh giá tự đọc.
- Không đủ nhãn đối chiếu đầy đủ nên chưa báo CER/WER cho ảnh thật. Lọc đường kẻ có thể làm mất nét/dấu; dữ liệu nét bút raster nền sạch trước đây không đại diện cho ảnh vở điện thoại này.
- Model cần được đánh giá trên tập ảnh chụp có nhãn, hoặc thay/fine-tune phù hợp hơn, trước khi tích hợp vào hệ thống. Không coi 10 vùng tách thành công là 10 dòng đọc đúng.
- Bộ kiểm thử mở rộng: 25/25 qua khi bật cả `OCR_TEST_PAGE=1` và `OCR_TEST_MODEL=1`, gồm model thật, trang ghép 8 dòng, chặn vùng lớn, giấy ô ly giả lập, kéo chọn/hủy Tk và tọa độ ảnh gốc. `pip check` không có dependency hỏng. Đây là bằng chứng phần mềm hoạt động theo kiểm thử, không phải chứng nhận chất lượng OCR trên ảnh thật.
