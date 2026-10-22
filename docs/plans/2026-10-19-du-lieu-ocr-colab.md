# Chuẩn hóa dữ liệu OCR và nghiệm thu bộ train — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Giúp nhóm dùng dữ liệu chữ viết tay nguồn gốc rõ ràng với bộ Colab đã có; bảo vệ tập test và chạy kiểm tra train/save/load thật trước khi thêm UI.

**Architecture:** Tiếp tục package độc lập `MaNguon/photocopy_ocr`. Ưu tiên VNOnDB cho nghiên cứu vì có điều kiện sử dụng được nguồn gốc công bố, InkML có Writer_ID và Tg_Truth; chuyển nét thành PNG bằng Pillow. Hỗ trợ manifest ảnh hiện có với split công bố; không tự trộn lại test. UIT-HWDB chỉ dùng khi người dùng có quyền và metadata kiểm chứng được.

**Tech Stack:** Python, Pillow, XML ElementTree (từ chối DTD/entity), unittest; PyTorch/Transformers trong venv riêng và Colab.

## Phạm vi đã được xác nhận

- Người dùng chọn fine-tune Colab, tải model về local; cho phép lựa chọn giải pháp phù hợp nhất sau khi được giải thích dữ liệu dòng và luồng cả trang.
- Cách được chọn: reuse bộ đọc dòng; chuẩn hóa dữ liệu và nghiệm thu trước. Không train model từ đầu, không chuyển sang API OCR trả phí.
- Nghiên cứu demo, nguồn học thuật/được phép dùng; chưa triển khai thương mại hoặc upload tài liệu khách. Không tự tạo tài khoản, đồng ý điều khoản thay người dùng, vượt đăng nhập hoặc gửi email cho tác giả.
- Một tiến trình train, checkpoint Drive và resume; không cam kết GPU hoặc tốc độ CPU. Không thay backend, DB hoặc hệ thống thanh toán đang chạy.
- Cả trang/nhiều trang cần detector; hiện mới chuẩn bị đọc dòng. Không dựng detector từ nhãn text không có tọa độ, không báo cả trang hoàn tất khi chỉ crop dòng.
- Các quyết định trên nối tiếp thiết kế đã xác nhận ở kế hoạch fine-tune; không mở rộng hệ thống đơn hàng.

## Bằng chứng nguồn 19/10/2026

- https://sites.google.com/view/icfhr2018-vohtr-vnondb/database-tools: có Writer_ID, Tg_Truth, trace x/y; cho phép học thuật/nghiên cứu, không thương mại.
- https://tc11.cvc.uab.es/datasets/HANDS-VNOnDB_1/: link tải dòng `...section_id=138&file=167`; thử tải trả HTML “You have to be logged to download files”. Không bypass.
- https://github.com/nghiangh/UIT-HWDB-dataset: có link Drive line/paragraph, chưa ghi rõ giấy phép trong README kiểm tra được. Mirror HF ghi CC-BY không đủ để xác nhận quyền từ tác giả gốc.

## TDD theo từng bước

1. Test RED split công bố: giữ train/validation/test, chặn writer rò rỉ, blank/mixed split, test giả, ảnh trùng. Thêm optional Sample.split và chọn split công bố nếu có, giữ hành vi cũ với CSV tự thu thập.
2. Test RED converter InkML: đọc Writer_ID thật, Tg_Truth theo traceGroup, vẽ nét tách rời không nối bừa, ảnh và nhãn tiếng Việt, chặn DTD/entity/NaN/quá lớn; từ chối dữ liệu Word/Paragraph nhầm Line, nhãn thiếu, tên/path không an toàn. Không suy writer từ tên file.
3. Thêm lệnh `prepare-vnondb` nguồn local do người dùng tải từ TC11 sau đăng nhập; ghi nguồn/quyền/hash và labels.csv, không ghi đè output. Nếu có split map được công bố thì giữ nguyên và chặn cross-writer. Không yêu cầu chia ngẫu nhiên lại benchmark.
4. Chạy smoke actual TrOCR nhỏ offline trong venv riêng nếu download dependency thành công; sửa đúng nguyên nhân khi phát hiện lỗi, thêm hồi quy. Không lấy mẫu tổng hợp làm bằng chứng chất lượng tiếng Việt.
5. Sửa notebook thành hai lựa chọn: chuẩn bị từ InkML hoặc ZIP manifest ảnh. Ghi hướng dẫn click tải gốc, input/output, điều kiện dừng và dự kiến bước tiếp theo. Đóng gói release nguồn mới, không xóa gói cũ của lượt trước.

## Nghiệm thu và giới hạn

Unit + converter fixture (không dùng dữ liệu khách), bảo toàn split/nhãn/nguồn; notebook parse và chạy smoke offline nếu có thư viện. Chưa có tài khoản TC11 hoặc dữ liệu gốc được tải thì không báo đã chuẩn hóa toàn bộ dataset/train GPU. Không thay giới hạn/điều khoản bằng metadata mirror.

## Kết quả

- Đã triển khai converter VNOnDB Line nguồn local, CLI, provenance và optional split công bố; không dùng mirror chưa xác minh. Code không suy mã người viết từ tên file và chặn XML không hỗ trợ thay vì bỏ nét.
- TDD RED → GREEN cho importer/split/CLI, và hồi quy Colab Python 3.13 (bộ cũ chặn <3.13; nguồn Google xác nhận nâng Python: https://github.com/googlecolab/colabtools/issues/6081).
- Đã cài thư viện trong `.venv` riêng, pip check qua. 40 test qua khi bật smoke, không skip: actual Trainer CPU, checkpoint, nạp lại so sánh tensor, đánh giá, ngắt mô phỏng sau checkpoint-1 rồi train tiếp tới step 2. Có warning alias tied weight/thứ tự mtime từ thư viện, không che warning hoặc ghi 0 warning.
- Bổ sung RED → GREEN để không dùng nhãn test dài/ngoài vocabulary ép chỉnh tokenizer/max_length trước train. Nhãn test đầy đủ vẫn được giữ trong báo cáo, không lọc bỏ để làm đẹp kết quả.
- Smoke dùng model/ảnh tổng hợp tạm, không có độ chính xác tiếng Việt hoặc benchmark trang vở. Chưa có full dữ liệu gốc vì TC11 yêu cầu đăng nhập. Đã hỏi người dùng tải ZIP, không xin mật khẩu/cookie hay tạo tài khoản thay họ.
- Đóng gói nguồn v3 và tài liệu trong `MaNguon/photocopy_ocr`; không thay API/web/mobile/DB, không dùng tài khoản GPU Colab hoặc giao dịch thật.

## Tiếp tục với ZIP người dùng cung cấp — 20/10/2026

- Đã nhận `MaNguon/photocopy_ocr/VNOnDB_Line.zip` (104.721.322 byte), SHA256 `446c0d95dcbe3609db40205e0f9e62799eb59470c9ff2abac0ec1b84f7aead92`. ZIP giữ nguyên. Phạm vi đã được người dùng xác nhận: tiếp tục chuẩn bị dữ liệu và bộ fine-tune Colab.
- Root cause 1: archive chứa `InkData_line.zip` lồng và ba danh sách split ở ngoài. Thêm `unpack_vnondb` an toàn, không ghi đè, chặn split thiếu/trùng và đường dẫn vượt thư mục. Notebook nhận split gốc tự động.
- Root cause 2: nguồn thật có 4.480.983 điểm hai kênh và 15.523.911 điểm ba kênh (phụ 0–255). Cho phép chiếu x/y khi chỉ định rõ, ghi provenance, kiểm tra hữu hạn và số kênh nhất quán từng trace. Không dựng tên/định dạng tùy ý cho kênh phụ hoặc nhận mọi InkML.
- Root cause 3: 7 traceGroup trong cùng file train là bản lặp đúng ảnh/nhãn/người viết. Đã audit toàn bộ render để xác định, thêm tùy chọn khử lặp chỉ train và lưu mapping/hash từng bản bị loại. Không loại validation/test hay thay split để làm đẹp điểm; không xóa dữ liệu gốc.
- Đã chuẩn bị 7.289 dòng: 4.426 train / 1.229 validation / 1.634 test; 106/35/60 Writer_ID không giao nhau. 201 ID metadata khác mô tả 200 người của nguồn; đã ghi rõ, không tự gộp.
- Root cause 4: tải processor thật phát hiện cần protobuf cho converter small, sau đó kiểm tra mọi nhãn train/validation phát hiện tokenizer small mất một số chữ có dấu. Thử bổ sung token rời cho small vẫn tạo khoảng trắng sai; không đưa thử nghiệm đó vào production. Chọn nguyên bộ base (revision ghim), tokenizer giữ đúng toàn bộ nhãn, max 140/100 token; đặt max_length 192, batch 1/accumulation 8. Tradeoff: cần nhiều GPU/RAM hơn small, chưa đo Colab.
- TDD RED → GREEN cho các lỗi trên; 46 tests qua với smoke Trainer và processor cache offline. Đã validate tất cả PNG/CSV thật, xem mẫu render; chưa kiểm thủ công mọi nhãn và chưa có chất lượng OCR sau fine-tune.
- Gói bàn giao v4, dữ liệu ZIP riêng, BAT_DAU_COLAB.md. Chưa dùng GPU tài khoản người dùng. Bước kế tiếp: chạy notebook, chốt model bằng validation, đo test rồi mới làm detector cả trang/UI.
