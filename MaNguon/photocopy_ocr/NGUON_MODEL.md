# Nguồn và tính toàn vẹn

## Mã nguồn upstream (không phải do nhóm tự viết)

- Tác giả: Phạm Bảo Công / pbcquoc.
- Repo: https://github.com/pbcquoc/vietocr
- Commit: `fe8c3a7fc714aec57ab81cec844eb3adf0c1636c`.
- Bản clone nguyên trạng ở `third_party/vietocr`; file `LICENSE` là Apache-2.0.
- `setup.py` có classifier ghi MIT, không thống nhất với LICENSE/README. Giữ nguyên mọi file, ghi nhận khác biệt, không tự thay giấy phép.
- Nhóm không sửa thuật toán/source upstream, không nhận quyền tác giả của model.

## Trọng số có sẵn

- Tác giả đăng tải: DungHugging.
- Repo: https://huggingface.co/DungHugging/vietocr-handwritten-finetune
- Revision: `0827b42e17cb0e1c1bbda5aac11f7cc5c816e0ca`.
- Model card công bố MIT, không có LICENSE riêng trong repo tại revision này. Lưu nguyên README/config/evaluation cùng weights.
- `transformerocr.pth`: 151.811.471 byte, SHA256 `d5357730c328e40dd1aeae7288994d8ad85979f8334c03c9bfad53c320114fa7`.
- `config.yml`: SHA256 `6de3d0e0be9546a120c6919f95c65a8cab9c4965f6bcb9b10c8a77cc46627fa4`.
- `README.md`: SHA256 `a900e17642d4ad15f1ba4adada760ea0351e481cc5faa92866b18c26b687aa48`.
- `evaluation_results.json`: SHA256 `c0ac2e7a368af88e6cb1dee0f0dfc353794513fecb1331c9f5802fc0cd18fc50`.

Model card báo CER 4,01% trên 702 validation; file evaluation_results.json báo CER 7,2326%, WER 17,9875%, đúng nguyên dòng 23,8806% trên 201 mẫu. Hai bộ đánh giá khác nhau, không thay kết quả test của nhóm bằng những số này.

## Phần nhóm bổ sung

`thu_ocr.py`: nạp nguồn/model local, kiểm hash, dùng torch.load(weights_only=True), ảnh có giới hạn, chạy CLI, tính CER/WER và xuất report. Đặt cnn.pretrained=False khi khởi tạo để tránh tải VGG ImageNet dư thừa; load toàn bộ state_dict strict=True. Giữ cấu hình/weights gốc trên đĩa. Greedy decoding của upstream với max_seq_length=256 (mặc định upstream 128); đánh dấu khi không có EOS, không sửa chính tả tự động.

## Dữ liệu kiểm tra

- Dùng 30 mẫu ngẫu nhiên từ validation của VNOnDB người dùng đã cung cấp; PowerShell Get-Random -SetSeed 42 -Count 30; giữ nguyên nhãn và tên ảnh.
- Manifest mẫu SHA256 `795f57405572657516c806a95c51d3f715ae11f0ff756c40f68576a4502caa3f`.
- VNOnDB chỉ dùng nghiên cứu/học thuật theo điều kiện gốc; không đưa samples/model vào Git hoặc tự tái phân phối dataset.
- Không xác minh được tác giả model từng dùng những mẫu này hay chưa. Đây chỉ là smoke benchmark, không phải kết quả độc lập đủ cho kết luận luận văn.
- Ảnh là nét InkML đã raster hóa, KHÔNG phải ảnh chụp vở. Cần bộ ảnh thật riêng của nhóm để đánh giá tiếp.

Nguồn dữ liệu: https://tc11.cvc.uab.es/datasets/HANDS-VNOnDB_1/ và https://sites.google.com/view/icfhr2018-vohtr-vnondb/database-tools
