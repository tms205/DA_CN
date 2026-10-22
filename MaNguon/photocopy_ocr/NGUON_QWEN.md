# Nguồn bộ thử Qwen

Model/trọng số có sẵn của nhóm Qwen; không phải model do nhóm đồ án huấn luyện. Code chạy và ghi kết quả trong `thu_qwen.py` do nhóm tích hợp.

| Thành phần | Nguồn / revision | SHA256 |
|---|---|---|
| Qwen3-VL-2B-Instruct Q4_K_M | https://huggingface.co/Qwen/Qwen3-VL-2B-Instruct-GGUF · `52d6c8ffea26cc873ac5ad116f8631268d7eb503` | `089d75c52f4b7ffc56ba998ffc50aae89fcafc755f9e7208aacca281dca6c2ae` |
| mmproj 2B Q8_0 | Cùng repo/revision | `f9a68fabba69c3b81e153367b2c7521030b0fa8bb0de400c9599c8e6725f9c82` |
| Qwen3-VL-4B-Instruct Q4_K_M | https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct-GGUF · `1cd86afb9a95c410a6038ab3b40d8b578c892266` | `66358cb18bb6b3b1b6675aa412c7a88ef01d228f481184d13668e5201c730a0a` |
| mmproj 4B Q8_0 | Cùng repo/revision | `30ba2c7dd3127a4561b6cba9d13d0f711c91bdb38742e2f56d73c8cb596bd06d` |
| llama.cpp CUDA 12.4 Windows x64 ZIP | https://github.com/ggml-org/llama.cpp/releases/tag/b6907 · build 6907 / commit `bea04522f` | `bbf51c51ad84341e31b78bfdf34a77704084c3eb103b6977b6f6b814b2abb4ba` |

Trọng số/model card Qwen công bố Apache-2.0. Bản LICENSE từ https://github.com/QwenLM/Qwen3-VL/blob/main/LICENSE được lưu cạnh trọng số. llama.cpp có LICENSE MIT, giữ nguyên trong thư mục runtime. README model được lưu từ revision đã chốt; code/model được nạp local, không chạy mã Python từ model repo.

Runtime dùng các DLL CUDA 12.4 đã có trong `.venv/Lib/site-packages/torch/lib`. Không đổi driver, không cài dịch vụ hệ thống. Qwen 2B/4B dùng cùng runtime; không sửa mã nguồn upstream của llama.cpp hay VietOCR. Tài liệu chạy server: https://github.com/ggml-org/llama.cpp/blob/b6907/tools/server/README.md.

Model và runtime nằm trong `models/`, `third_party/`, đã được `.gitignore` loại trừ. Script `tai_qwen.ps1` tải lại từ nguồn chính thức, kiểm SHA256 trước khi dùng. Khi chạy nhận dạng, chương trình chỉ giao tiếp HTTP với `127.0.0.1` do chính nó tạo, dùng ảnh PNG base64, không gọi API AI bên ngoài.

Qwen là model ảnh/ngôn ngữ nên vẫn có thể đoán chữ dựa theo ngữ cảnh. Lời nhắc yêu cầu chép nguyên văn và đánh dấu chỗ không rõ không bảo đảm loại bỏ được hiện tượng này. Không đánh đồng điểm benchmark do tác giả công bố với kết quả trên ảnh của nhóm.
