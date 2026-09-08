---
name: frontend-design-audit
description: Kiểm tra (audit) một màn hình WPF đã có trong dự án EOL Tester 705/715 xem có tuân thủ nguyên tắc thiết kế (skill frontend-design) và các quy ước UI đã chốt trong CLAUDE.md hay không — chấm điểm theo từng tiêu chí, liệt kê lỗi theo mức độ nghiêm trọng, lặp lại chu trình sửa → kiểm tra lại. Dùng khi người dùng muốn "review", "check", "audit", "chấm điểm", "rà soát" một tab/màn hình đã có sẵn (không phải thiết kế mới), khi nghi ngờ giao diện "generic"/rập khuôn control WPF mặc định, hoặc khi dán ảnh chụp màn hình hỏi "giao diện này ổn chưa".
---

# WPF Design Audit — EOL Tester 705/715

Đóng vai một **design lead** được thuê để review (không phải thiết kế mới) 1 màn hình WPF đã xây
trong dự án EOL Tester — tìm khoảng cách giữa hiện trạng và một giao diện công nghiệp rõ ràng, có
chủ đích, đúng quy ước đã chốt trong CLAUDE.md, rồi đưa lộ trình sửa cụ thể theo mức ảnh hưởng.
Không tự ý viết lại toàn bộ trừ khi được yêu cầu — mục tiêu là **chẩn đoán chính xác**, sau đó
**sửa có kiểm chứng bằng ảnh chụp thật**.

Nếu skill `frontend-design` cũng có sẵn, coi nó là kim chỉ nam khi thiết kế mới; skill này là công
cụ **kiểm tra ngược lại** thành phẩm so với kim chỉ nam đó + các quy ước cụ thể đã chốt trong
CLAUDE.md (canvas cố định, MVVM, resx, bảng màu trạng thái...).

## Quy trình tổng quát (vòng lặp bắt buộc)

1. **Thu thập bằng chứng thật** — không audit "bằng trí nhớ":
   - Code XAML/ViewModel thật của màn hình đang xét (Read trực tiếp file `.xaml`/`.xaml.cs`/
     ViewModel liên quan).
   - **Ảnh chụp thật bằng skill `verify-ui`** (dùng `PrintWindow`, không phải chụp toàn màn hình —
     bắt buộc theo CLAUDE.md Phần I mục 7) — không có ảnh thật thì không audit phần thị giác, chỉ
     audit được phần code/cấu trúc.
   - Nếu người dùng tự dán ảnh chụp màn hình sẵn có, dùng ảnh đó thay vì tự chụp lại.
   - Trạng thái tương tác liên quan tới máy công nghiệp: disabled khi mất kết nối PLC
     (`ConnectionState == Error`), trạng thái đang nhấn giữ (`MomentaryButton`), trạng thái pending
     (giá trị Set Spec. chưa xác nhận) — không chỉ trạng thái tĩnh mặc định.
2. **Đối chiếu với checklist** ở phần dưới, theo từng nhóm.
3. **Chấm điểm** mỗi nhóm theo thang 1–5 (xem Rubric).
4. **Liệt kê vấn đề theo mức độ nghiêm trọng**: Blocker / Nên sửa / Tinh chỉnh.
5. **Đề xuất sửa cụ thể** — chỉ rõ tên control/Style/Resource, giá trị hiện tại, giá trị đề xuất,
   không nói chung chung "thiếu nhất quán".
6. **Sửa** (nếu được yêu cầu tự sửa XAML) rồi **audit lại từ bước 1** cho phần vừa sửa — bắt buộc
   chụp ảnh `verify-ui` mới, không tin vào diff code.
7. Dừng vòng lặp khi không còn Blocker nào và điểm trung bình ≥ 4/5, hoặc người dùng chủ động dừng.

## Checklist chi tiết

### A. Bản sắc & tính nguyên bản
- Màn hình có bám sát ngữ cảnh **vận hành máy công nghiệp cụ thể** hay trông như 1 form CRUD chung
  chung/1 trang web dán vào WPF?
- Có rơi vào khuôn mẫu "control WPF mặc định không chủ đích" (Button xám hệ thống, DataGrid viền đen
  mảnh mặc định, không Style riêng) hay đã có bảng màu/Style nhất quán với các tab khác?
- Màu sắc trạng thái (LimeGreen = bật/chọn, đỏ = NG/lỗi, cam = pending) có dùng **đúng và nhất quán**
  với ý nghĩa đã thiết lập ở tab khác, hay bị dùng lệch nghĩa/thêm màu mới không có lý do?
- Chuỗi hiển thị có lấy từ resx (`{loc:Tr Key}`) hay còn literal tiếng Việt/Anh hardcode trong XAML?

### B. Typography
- Có thang cấp rõ ràng (tiêu đề tab > nhãn nhóm > nhãn trường > giá trị) hay mọi chữ cùng 1 cỡ mặc
  định của control?
- Chữ có đủ tương phản/kích thước để đọc từ khoảng cách vận hành thực tế (không chỉ nhìn gần trên
  màn hình dev) — đặc biệt các giá trị đo lường/cảnh báo cần đọc nhanh?
- `TextTrimming`/tooltip có được dùng cho chuỗi dài có nguy cơ bị cắt (bài học từ bản cũ: tiêu đề bị
  cắt "...HV3!") hay vẫn có nguy cơ tràn/cắt chữ khi đổi ngôn ngữ (tiếng Anh thường dài hơn tiếng
  Việt hoặc ngược lại)?

### C. Layout & cấu trúc
- Layout có nằm đúng trong canvas cố định `1400×765` (CLAUDE.md mục 12), hay dùng kích thước/margin
  tùy tiện không tính theo hệ tọa độ đó?
- Card/khung/đường phân chia có mã hoá đúng nhóm dữ liệu thật (vd 3 cột footer = 3 nhóm thông tin)
  hay là trang trí không có ý nghĩa?
- Có bẫy WPF đã biết trong dự án không: `VerticalAlignment` mặc định `Stretch` làm khối thấp bị kéo
  giãn khi cạnh khối cao hơn trong `WrapPanel`/`StackPanel` ngang (CLAUDE.md mục 5); `Grid` tự clip
  theo biên ô nên `Margin` âm không có tác dụng "dịch" phần tử ra ngoài (CLAUDE.md mục 11);
  `DataGridColumn.Width` không nhận `StaticResource` kiểu `double`, phải literal số.
- `ItemsSource` của `DataGrid`/`ListView` cần cho sửa cell trực tiếp có phải `IList`/
  `ObservableCollection` hay còn là `IEnumerable` lazy (khiến `BeginEdit` lỗi âm thầm, xem bẫy đã ghi
  trong CLAUDE.md mục 7 Tab 4)?

### D. Trạng thái & tương tác (thay cho "Motion" ở bản web)
- Các nút điều khiển thủ công có phản hồi thị giác rõ ràng khi nhấn/giữ (`IsPressed`, `Style.Trigger`)
  hay dùng chrome mặc định của `Button` đè lên trigger màu đã khai báo (lỗi đã biết: nút "XÁC NHẬN
  HÀNG NG" ra màu xanh dương nhạt thay vì LimeGreen — xem CLAUDE.md mục 13, chưa sửa)?
- Nút gửi lệnh PLC có tự `IsEnabled=False` đúng lúc `ConnectionState == Error` (không phải lúc
  `Disconnected`/`Connecting`, 2 trạng thái đó vẫn hợp lệ) hay bị thiếu/thừa điều kiện disable?
- Trạng thái pending (giá trị Set Spec. đang gõ khác giá trị PLC) có tô màu cảnh báo rõ ràng hay im
  lặng, dễ khiến Admin tưởng đã áp dụng?

### E. Nội dung & giọng văn
- Nhãn có gọi đúng theo cái operator nhìn thấy/điều khiển, không theo tên biến/địa chỉ PLC nội bộ
  (địa chỉ D chỉ nên hiện ở màn hình Admin như Set Spec., không hiện ở Main — đúng nguyên tắc đã áp
  dụng)?
- Cùng 1 hành động có dùng nhất quán 1 tên xuyên suốt luồng (nút → thông báo) ở cả bản tiếng Việt lẫn
  tiếng Anh không?
- Thông báo lỗi kết nối PLC/lỗi validate (vd Giới hạn dưới > Giới hạn trên) có nói rõ nguyên nhân +
  cách khắc phục hay chỉ báo chung chung?

### F. Nhất quán hệ thống thiết kế (Style/Resource)
- Màu sắc/kiểu chữ dùng lại từ `ResourceDictionary`/`Style` chung, hay hardcode `Color`/`FontSize` số
  lẻ rải rác nhiều nơi trong file XAML?
- Đổi 1 giá trị dùng chung (vd màu LimeGreen trạng thái ON) có lan tỏa toàn bộ chỗ dùng hay phải sửa
  tay từng nơi?
- Style/Template có tái sử dụng đúng cách giữa các tab hay bị copy-paste rồi lệch dần?

### G. Hiệu năng/khả năng mở rộng liên quan tới UI
- Binding có tránh polling/refresh cả `DataGrid` lớn không cần thiết mỗi tick không (đối chiếu với
  chu kỳ polling PLC ~500ms ở CLAUDE.md mục 6 — UI không nên rebuild nặng hơn tốc độ dữ liệu thật)?
- Thêm điểm I/O/tham số mới có tự động phản ánh qua binding collection (data-driven, đúng nguyên tắc
  Phần I mục 3 "không hardcode bảng dữ liệu trong UI") hay vẫn phải sửa XAML thủ công từng dòng?

## Phân loại mức độ nghiêm trọng

- **Blocker** — chữ bị cắt/tràn ở 1 trong 2 ngôn ngữ, control không disable đúng lúc mất kết nối PLC
  (rủi ro vận hành thật), bảng dữ liệu hardcode thay vì data-driven, hoặc bẫy WPF đã biết (mục C)
  chưa được xử lý.
- **Nên sửa** — màu sắc dùng lệch nghĩa với quy ước đã có, thiếu Style dùng chung gây lệch dần giữa
  các tab, chuỗi còn hardcode chưa đưa vào resx.
- **Tinh chỉnh** — thang cấp chữ, khoảng cách, độ tinh tế của các chi tiết đã đúng chức năng nhưng
  chưa đẹp.

Luôn báo Blocker trước tiên và rõ ràng.

## Rubric chấm điểm (mỗi nhóm A–G: thang 1–5)

- **1** — Không đạt yêu cầu cơ bản, cần làm lại.
- **2** — Có cố gắng nhưng còn nhiều lỗi rõ ràng, kể cả lỗi vận hành thật (vd nút không disable đúng
  lúc mất kết nối).
- **3** — Đạt mức chấp nhận được, nhưng còn generic/thiếu chủ đích hoặc còn vài chỗ lệch quy ước.
- **4** — Tốt, có chủ đích rõ ràng, đúng mọi quy ước CLAUDE.md, chỉ còn vài điểm tinh chỉnh.
- **5** — Xuất sắc, nhất quán tuyệt đối với các tab khác, không có lỗi kỹ thuật/bẫy WPF đã biết.

Ngưỡng "đạt" đề xuất: trung bình ≥ 4/5 trên tất cả nhóm, và **không còn Blocker nào**.

## Định dạng báo cáo đầu ra

```
## Tổng quan
Điểm trung bình: X/5 — [Đạt / Chưa đạt]
Số Blocker: N

## A. Bản sắc & tính nguyên bản — điểm X/5
- [Blocker/Nên sửa/Tinh chỉnh] <vấn đề cụ thể, tên file/control> → <đề xuất sửa cụ thể>

## B. Typography — điểm X/5
...

## Ưu tiên xử lý tiếp theo
1. ...
2. ...
```

## Khi lặp lại vòng audit sau khi đã sửa

- Chỉ re-audit **những nhóm có thay đổi**, trừ khi người dùng yêu cầu audit toàn bộ lại.
- So sánh điểm trước/sau, kèm ảnh `verify-ui` trước/sau nếu có thể.
- Nếu 1 sửa lỗi ở nhóm này vô tình phá vỡ tiêu chí ở nhóm khác (vd sửa spacing làm vỡ layout canvas
  cố định) — phải bắt được điều đó, đây là lý do vòng lặp luôn quay lại bước "thu thập bằng chứng"
  chứ không chỉ tin vào diff code.

## Giới hạn của skill này

Skill này đánh giá **chất lượng thiết kế/UI và tuân thủ quy ước CLAUDE.md**, không thay thế cho:
- Review logic nghiệp vụ (dùng `code-review`/`QA-Session`).
- Kiểm thử phần cứng thật (PLC/RS485) — chỉ audit được phần thị giác/binding.
- Kiểm thử tự động (unit test) — đề xuất bổ sung riêng nếu cần.
