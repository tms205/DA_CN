# Kế hoạch và kết quả đặc tả Use Case phần Mạnh

Đã mở rộng từ yêu cầu làm Task 1 sang hoàn thành toàn bộ theo tin nhắn tiếp theo của người dùng. Đối chiếu bảy ảnh gốc, tài liệu Word tham khảo và code ngày 26/10/2026. Không thay đổi ứng dụng hoặc ảnh gốc.

| Task | Kết quả | Trạng thái |
| --- | --- | --- |
| 1 | Hủy đơn và hoàn tiền: 5 nghiệp vụ, 5 hệ thống | Hoàn thành, đã kiểm tra Word |
| 2 | Tài khoản và xác thực: 0 nghiệp vụ, 10 hệ thống | Hoàn thành, đã kiểm tra Word |
| 3 | Đặt in trực tuyến và thanh toán trước: 6 nghiệp vụ, 10 hệ thống | Hoàn thành, đã kiểm tra Word |
| 4 | Thanh toán VietQR: 0 nghiệp vụ, 6 hệ thống | Hoàn thành, đã kiểm tra Word |
| 5 | Theo dõi đơn cá nhân: 0 nghiệp vụ, 7 hệ thống | Hoàn thành, đã kiểm tra Word |
| 6 | Bao phủ 52 nhãn gốc và 1 nhánh nhận hoàn bổ sung; thống nhất ID, quyền, trạng thái, quan hệ và bản tổng hợp | Hoàn thành, đã gộp và rà soát |

## File sử dụng

- DAC_TA_USECASE_MANH_DAY_DU.docx: bản Word tổng hợp theo mẫu, có mục lục và số trang.
- DAC_TA_USECASE_MANH_DAY_DU.md: toàn bộ nội dung, có liên kết căn cứ source.
- Task_01 đến Task_05 dạng MD: chia nhóm để chỉnh dễ; Task 1 còn bản Word riêng đã tạo trước.
- Task_06_DoiChieu_Va_NghiemThu.md: ma trận 7 hình, điểm lệch cần sửa trên ảnh và tính nhất quán. Từ nghiệm thu ở đây là kiểm tra tài liệu, không phải thử ngân hàng/in thật.

## Số lượng

11 đặc tả nghiệp vụ + 38 đặc tả hệ thống/hành vi = 49. Bao phủ 7/7 hình, 52 nhãn gốc. Một số oval là bước con hoặc nghiệp vụ ngoài phần mềm nên được dẫn vào đúng đặc tả thay vì tạo chức năng giả.

## Điểm cần người vẽ cập nhật

Xem bảng đối chiếu mỗi task và Task 6. Đáng chú ý: quyền CEO hoàn tiền; không duyệt hủy riêng; hủy hai trạng thái trước in; địa chỉ GPS/cước nội bộ; job quá hạn độc lập; thông tin tiền chưa phải hóa đơn thuế. Ảnh gốc không bị ghi đè.

## Kết quả kiểm tra cuối

- Bản tổng hợp Word 72 trang; bản Task 1 riêng 18 trang. Mục lục, số trang, bảng và các trang tiếp nối đã được xuất kiểm tra; không còn trang trắng hoặc trang chỉ có vài từ bị rơi.
- Đủ 49 ID đặc tả, không trùng ID; 11 bảng nghiệp vụ ba phần và 38 bảng hệ thống đủ mười trường.
- Bao phủ 7 hình gốc, 52 nhãn và một nhánh nhận hoàn bổ sung; có đối chiếu quyền, trạng thái, điều kiện và quan hệ từng nhóm.
- Có 60 vị trí căn cứ mã nguồn. Hash các file được dẫn chiếu, tài liệu mẫu và cả 7 ảnh gốc được kiểm tra; ảnh/tài liệu gốc giữ nguyên.
- Phạm vi hoàn thành là đặc tả của phần Mạnh trong thư mục Usecase. Không bao gồm tự sửa hình ảnh, triển khai chức năng còn thiếu hoặc xác nhận giao dịch thật với ngân hàng, máy in và các dịch vụ ngoài.

Bạn có thể dùng bản Word tổng hợp để đưa vào báo cáo; chỉnh nội dung theo từng nhóm trong các file Task dạng MD. Những điểm cần sửa trên hình xem Task_06_DoiChieu_Va_NghiemThu.md.
