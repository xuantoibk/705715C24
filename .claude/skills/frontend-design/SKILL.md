---
name: frontend-design
description: Hướng dẫn thiết kế giao diện WPF có phong cách riêng biệt và có ý định cho dự án EOL Tester 705/715 — xác định hướng thẩm mỹ, kiểu chữ, bố cục trong khuôn khổ canvas cố định 1400×765, tránh những lựa chọn thiết kế rập khuôn/nhìn như AI tạo sẵn.
---

# Thiết Kế Giao Diện WPF

Tiếp cận công việc này như một giám đốc thiết kế UI công nghiệp — mục tiêu không phải "đẹp kiểu web"
mà là giao diện **rõ ràng, đáng tin cậy cho người vận hành máy**, đồng thời có chủ đích thẩm mỹ thay
vì mặc định của control WPF (Button/DataGrid màu xám hệ thống, không có bản sắc nào). Đưa ra những
lựa chọn có mục đích về màu sắc, kiểu chữ, và bố cục, và chấp nhận rủi ro thẩm mỹ có thể biện minh
được — nhưng không bao giờ đánh đổi sự rõ ràng vận hành (một cảnh báo NG phải luôn nổi bật, một nút
Start/Stop không bao giờ mơ hồ) để lấy vẻ đẹp.

**Ràng buộc cứng của dự án này (không phải gợi ý)** — xem CLAUDE.md để biết chi tiết:
- Toàn bộ layout dựng trên canvas cố định `1400×765` bọc trong `Viewbox Stretch="Fill"` (CLAUDE.md
  mục 12) — không thiết kế theo tư duy "responsive tự do", mọi khoảng cách/kích thước là số pixel cụ
  thể trong hệ canvas đó, được scale đồng bộ khi resize cửa sổ.
- MVVM nghiêm ngặt — mọi trạng thái hiển thị (màu sắc theo trạng thái, text động) đến từ binding/
  `DataTrigger`/property trong ViewModel, không set cứng trong code-behind.
- Chuỗi hiển thị lấy từ `Resources/Strings.resx` (VI) + `Strings.en-US.resx` (EN) qua
  `{loc:Tr Key}` — không hardcode literal tiếng Việt/Anh trong XAML (CLAUDE.md mục 10).
- Đã có 2 logo thật (`Assets/Logo1.png`/`Logo2.png`) và bảng màu trạng thái đã dùng nhất quán
  (LimeGreen = đang bật/đang chọn, đỏ = NG/lỗi, cam nhạt = giá trị đang chờ xác nhận) — giữ đúng ý
  nghĩa màu này khi mở rộng thêm màn hình mới, không tự đặt lại ý nghĩa màu.

## Gắn nó vào chủ đề

Chủ đề ở đây luôn cụ thể: một màn hình vận hành máy kiểm tra công nghiệp (Tự động / Giám sát-Thủ
công / Cài đặt / Set spec.), người dùng là **operator đứng máy** hoặc **admin cấu hình**, không phải
khách hàng duyệt web. Thế giới vật liệu ở đây là bảng điều khiển công nghiệp: chỉ báo trạng thái rõ
ràng, vùng bấm đủ lớn cho thao tác nhanh, tương phản cao để đọc được dưới ánh đèn xưởng, không có
khoảng trắng "sang trọng" kiểu marketing web làm loãng thông tin vận hành.

## Nguyên tắc thiết kế

**Cấu trúc là thông tin.** Khung, đường phân chia, nhãn trên các card (THÔNG TIN LOT / TRẠNG THÁI /
SCAN BARCODE...) phải phản ánh đúng nhóm dữ liệu thật, không phải trang trí. Đã có tiền lệ trong dự
án: khối footer chia 3 cột theo đúng 3 nhóm thông tin (Lot / Trạng thái / Scan+Operation) — mở rộng
màn hình mới nên theo tinh thần này thay vì tự bịa bố cục mới.

**Kiểu chữ mang lại cá tính, nhưng phải đọc được từ xa/dưới ánh sáng công nghiệp.** Ưu tiên độ tương
phản và kích thước rõ ràng hơn là kiểu chữ cầu kỳ. Vẫn nên thiết lập thang cấp rõ ràng (tiêu đề tab >
nhãn nhóm > nhãn trường > giá trị) thay vì dùng cùng 1 cỡ chữ cho mọi thứ như control WPF mặc định.

**Màu sắc mã hoá trạng thái, không chỉ trang trí.** Mỗi màu dùng trong app này nên gắn với đúng 1 ý
nghĩa vận hành nhất quán xuyên suốt mọi tab (xanh lá = đang hoạt động/đã chọn, đỏ = lỗi/NG, cam =
đang chờ xác nhận/pending) — không dùng gradient trang trí không mang ý nghĩa gì, và không phá vỡ
quy ước màu đã có ở các tab khác.

**Tận dụng `DataTrigger`/`Style.Triggers` một cách có chủ đích**, không chỉ đổi màu nền đơn thuần —
suy nghĩ về trạng thái hover/pressed/disabled cho từng nút điều khiển thủ công, đặc biệt các nút có
rủi ro vật lý (ON/OFF ngõ ra, Reset) cần phản hồi thị giác rõ ràng ngay khi nhấn.

**Khớp độ phức tạp với tầm nhìn.** Màn hình vận hành (Main/Monitor) cần rõ ràng/tối giản hơn màn hình
cấu hình (Setup/Set Spec.) — Setup có thể chấp nhận nhiều thông tin kỹ thuật hơn vì đối tượng dùng là
Admin, nhưng CLAUDE.md mục 7 Tab 3 đã ghi rõ nguyên tắc "màn hình khách hàng xem, khó thiết kế đẹp
với nhiều chữ giải thích" — đoạn text hướng dẫn dài đã bị gỡ khỏi UI theo yêu cầu thực tế, giữ tinh
thần đó khi thêm nội dung mới.

## Quy trình: Động não, Khám phá, Lập kế hoạch, Phê bình, Xây dựng, Phê bình lại

Để hiệu chỉnh: giao diện công nghiệp do AI tạo ra dễ rơi vào 1 trong 2 khuôn mẫu nhàm chán: (1) sao
chép y nguyên control WPF mặc định (nút xám, DataGrid viền đen mảnh, không có bản sắc); (2) "web hóa
quá đà" — bo góc lớn, gradient, shadow kiểu Material Design không hợp ngữ cảnh máy công nghiệp. Cả
hai đều là lựa chọn mặc định, không phải lựa chọn có chủ đích.

Làm việc theo 2 lượt:
1. **Lập kế hoạch ngắn gọn trước khi viết XAML**: mô tả bảng màu (tên + mã hex, đối chiếu với màu đã
   dùng trong app để tránh xung đột ý nghĩa), vai trò kiểu chữ (tiêu đề/nhãn/giá trị/cảnh báo), khái
   niệm bố cục trong khung canvas cố định (mô tả 1 câu + phác thảo ASCII toạ độ tương đối nếu cần).
2. **Xem lại kế hoạch trước khi build**: nếu phần nào trông như control WPF mặc định không có chủ
   đích, hoặc trông như bê nguyên 1 thư viện UI web vào — sửa lại, nói rõ đã đổi gì và tại sao.

Chỉ sau khi plan đã ổn mới viết XAML theo đúng plan.

## Tiết chế và tự phê bình

Dành sự nổi bật cho đúng chỗ cần chú ý (cảnh báo NG, trạng thái kết nối PLC) — giữ phần còn lại yên
tĩnh, không để nhiều thứ cùng "gào lên" tranh giành sự chú ý. **Luôn verify bằng ảnh chụp thật qua
skill `verify-ui`** (dùng `PrintWindow`, không phải chụp toàn màn hình — xem CLAUDE.md Phần I mục 7)
trước khi báo hoàn thành, không suy đoán kết quả từ code XAML.

## Viết trong thiết kế (copy)

Chuỗi hiển thị luôn qua resx (VI + EN, xem CLAUDE.md mục 10) — viết cả 2 ngôn ngữ khi thêm chuỗi mới,
không chỉ tiếng Việt rồi để trống bản tiếng Anh.

Đặt tên theo góc nhìn operator, không theo tên biến hệ thống: một nhãn nói "PHÁT HIỆN HÀNG NG", không
phải "NgFlagBit". Dùng giọng chủ động, nhất quán: nút "XÁC NHẬN JOB" → thông báo "Đã xác nhận Job",
không đổi giữa "Xác nhận"/"Confirm"/"OK" cho cùng 1 hành động ở các chỗ khác nhau.

Thông báo lỗi/trạng thái rỗng phải nói rõ đã xảy ra gì và cần làm gì tiếp — đặc biệt quan trọng với
lỗi kết nối PLC (operator cần biết "mất kết nối PLC, đang thử kết nối lại" chứ không phải chỉ 1 chấm
đỏ không giải thích gì).
