# Fine-tune chữ viết tay tiếng Việt — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Chuẩn bị bộ train Colab, đánh giá trước/sau và mang mô hình về chạy offline trên máy của nhóm.

**Architecture:** Bộ Python độc lập trong `MaNguon/photocopy_ocr`, chưa nối vào API/web/mobile đang chạy. Huấn luyện bộ nhận dạng dòng chữ trước; cả trang cần bộ tách dòng riêng, không đánh đồng hai bài toán. Khi có model đạt nghiệm thu mới nối vào luồng xem/sửa/xác nhận/PDF của hệ thống.

**Tech Stack:** Python 3.11/3.12, PyTorch, Transformers 4.57.6, Pillow, unittest; Colab notebook. Không database, không API trả phí, không thay launcher.

## Understanding summary và quyết định

- Người dùng xác nhận fine-tune trên GPU Colab, tải model về máy sử dụng, văn bản tiếng Việt thông thường, người dùng duyệt trước khi in.
- Đã cân nhắc: train từ đầu (tốn dữ liệu/tài nguyên), fine-tune rồi tự chạy (được chọn), API OCR cloud (không phù hợp mục tiêu tự sở hữu model/không phí từng lượt).
- Chọn TrOCR-small làm baseline thí nghiệm đầu; base/large chưa chọn vì ưu tiên CPU và GPU Colab có hạn. Không khẳng định small đủ chất lượng trước khi đo.
- Dữ liệu có cặp ảnh dòng/nhãn, mã người viết; tách train/validation/test theo người viết. Không lấy ảnh test để train, không tự gán người viết giả. Chưa tự tải UIT-HWDB vì repo chưa làm rõ giấy phép sử dụng dữ liệu.
- Lưu best model + processor/tokenizer/config và thông tin chạy; checkpoint huấn luyện riêng gồm trạng thái optimizer để resume sau khi Colab ngắt.
- Máy local chỉ inference, không train lại mỗi yêu cầu. Dùng local_files_only, không upload ảnh ra dịch vụ thứ ba khi nhận dạng tại máy. Colab vẫn nhận dữ liệu train được người dùng cho phép upload.
- Giả định: demo từng dòng/lần, không có SLA tốc độ cho tới khi đo CPU thực tế; OCR không hoạt động thì nghiệp vụ in cũ vẫn hoạt động. Chỉ nhóm duy trì notebook/config/model.
- Ngoài phạm vi bước này: công thức/bảng, tự giải mã mọi chữ xấu, tự đóng tiền/in, web/mobile UI khi chưa có model đạt chất lượng, sửa DB và tự restart hệ thống.

## Kế hoạch TDD

1. Viết test dữ liệu: NFC tiếng Việt, nhãn/ảnh thiếu, path traversal, ảnh trùng, writer thiếu, train/val/test rò rỉ người viết; chạy RED rồi thêm `ocr/data.py`.
2. Viết test CER/WER corpus-level, dấu tiếng Việt, khoảng trắng, số lỗi chèn/xóa/thay; chạy RED rồi thêm `ocr/metrics.py`.
3. Viết test giới hạn ảnh, safe extraction, gói model hợp lệ và không tự tải khi thiếu model; chạy RED rồi thêm `ocr/artifacts.py`, `ocr/recognizer.py`.
4. Thêm train/evaluate/predict CLI và notebook gọi cùng mã nguồn (không nhân đôi thuật toán trong cell). Ghi config/seed/hash/version, baseline validation, best checkpoint và đánh giá test sau khi chốt model. Không tự chạy training thật trên dữ liệu chưa được cấp.
5. Chạy test nhẹ bằng Python có Pillow, và nếu cài được môi trường độc lập thì chạy smoke bằng mô hình TrOCR rất nhỏ khởi tạo ngẫu nhiên để chứng minh forward/train/save/load, không coi kết quả đó là tiếng Việt nhận dạng được.
6. Kiểm tra notebook JSON/cell syntax, đóng gói ZIP, ghi hướng dẫn từng bước Colab/Windows, bảng đã làm/chưa làm và bước nạp model sau train. Không sửa API/web/mobile hoặc tạo đơn test.

## Nghiệm thu

- Test dữ liệu/phép đo/đóng gói qua; notebook hợp lệ và có resume checkpoint.
- CPU smoke không gọi mạng, không model giả trong thư mục release.
- Chưa thể nghiệm thu CER/WER hoặc GPU Colab trước khi có dữ liệu hợp lệ và người dùng chạy notebook; báo rõ giới hạn này.

Không tạo worktree/commit hoặc dùng subagent: thay đổi mới độc lập, không ảnh hưởng checkout đang phục vụ web; user không yêu cầu branch/commit.

## Kết quả bước bộ train

- Đã tạo nguồn, test, notebook, README và CSV mẫu; không có ảnh train thật hoặc model đạt chất lượng được tạo.
- 24 test pass, 1 smoke tùy chọn skip. Tải dependency để bật smoke không hoàn tất do mạng chậm/timeout; không tính skip là pass. Notebook có cell chạy smoke offline để dừng sớm nếu train/save/load có lỗi khi nhóm chạy Colab.
- Chỉ sửa/tạo `MaNguon/photocopy_ocr` và tài liệu kế hoạch này; không thay API/web/mobile, schema hoặc dữ liệu, không restart.
- Bước tiếp theo còn thiếu: xác minh quyền dữ liệu, chuẩn bị labels.csv có người viết thật, chạy smoke trên môi trường có torch, train Colab/đo CER-WER, rồi mới tích hợp màn hình đối chiếu/sửa/PDF và bộ tách dòng.
