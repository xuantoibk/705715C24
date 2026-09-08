---
name: QA-Session
description: Tự động tra cứu CLAUDE.md + lịch sử phiên (docs/session-log/history.md) trước khi trả lời — kích hoạt khi người dùng hỏi "vì sao hệ thống làm theo cách này", nêu vấn đề cần đề xuất hướng xử lý, hoặc hỏi về quyết định kiến trúc đã chốt trong dự án EOL Tester 705/715. Lặp lại cho các câu hỏi tiếp theo tới khi người dùng yêu cầu dừng. Gọi bằng "QA Session" hoặc tự động khi phù hợp.
disable-model-invocation: false
---

# 🔎 QA Session — Tra cứu quyết định/lịch sử dự án trước khi trả lời

Skill này giúp câu trả lời bám sát các quyết định/quy tắc **đã chốt trước đó** trong dự án **EOL
Tester 705/715**, thay vì chỉ suy luận từ code hiện tại hoặc trả lời chung chung.

Dự án này **không dùng cơ chế Remember/History phân tầng theo module** — chỉ có 2 nguồn:
- **CLAUDE.md** (đã nạp sẵn đầu mỗi phiên) — đóng vai trò "ghi nhớ": quy tắc chung (Phần I) + trạng
  thái/đặc tả/quyết định **còn hiệu lực hiện hành** của dự án (Phần II, đặc biệt mục 3 phân tích hệ
  thống, mục 6-12 kiến trúc/đặc tả từng phần, mục 13 câu hỏi còn mở).
- **`docs/session-log/history.md`** — đóng vai trò "lịch sử": diễn biến chi tiết, lý do đầy đủ, bẫy
  kỹ thuật đã gặp theo từng phiên. KHÔNG tự nạp toàn bộ vào context.

Đây là "phía đọc"; "phía ghi" là skill `Works_End`. Skill này CHỈ ĐỌC, không sửa CLAUDE.md hay
history.md.

---

## Khi nào kích hoạt

Tự động cân nhắc dùng skill này khi tin nhắn của người dùng thuộc 1 trong các dạng sau:
- Hỏi **"vì sao"/"tại sao"** một phần của hệ thống hoạt động theo 1 cách cụ thể (vd "vì sao PC có
  thể vừa Master vừa Slave", "vì sao Bit 1/Bit 2/Bit 3 lại tách biệt").
- Nêu **1 vấn đề/lỗi/băn khoăn** và cần đề xuất hướng xử lý — nên biết đã từng quyết định gì liên
  quan trước đó, tránh đề xuất lại thứ đã bị bác bỏ hoặc mâu thuẫn với quyết định cũ.
- Hỏi về **ràng buộc nghiệp vụ/kỹ thuật đã chốt** (vd "tại sao Setup lại khóa cứng Modbus RTU",
  "range D0-D99 có ghi được không").
- Câu hỏi mang tính **thảo luận/ra quyết định**, không phải yêu cầu code trực tiếp một tính năng đã
  rõ đặc tả trong CLAUDE.md.

KHÔNG cần kích hoạt cho: yêu cầu thực thi rõ ràng không cần bối cảnh quyết định cũ (vd "sửa lỗi typo
dòng X", "build giúp tôi"), câu trả lời ngắn kiểu xác nhận ("ok", "làm đi"), hoặc khi người dùng đã
yêu cầu dừng tra cứu (xem mục "Dừng lại").

## Quy trình

### Bước 1: Ưu tiên CLAUDE.md trước (đã có sẵn trong context)

CLAUDE.md được nạp sẵn mỗi phiên — không cần đọc lại file, chỉ cần rà lại nội dung đã có trong
context để tìm mục liên quan (Phần II mục 3/6/7/8/9/12/13 là nơi hay chứa quyết định + lý do). Phần
lớn câu hỏi "vì sao hệ thống làm vậy" đã có câu trả lời trực tiếp ở đây, đặc biệt trong các đoạn diễn
giải lý do đi kèm ("Lý do:", "vì...", các mục "Bẫy đã gặp") — không cần tra cứu gì thêm.

### Bước 2: Chỉ tra `docs/session-log/history.md` khi CLAUDE.md không đủ

Dùng khi câu hỏi cần biết **đã xảy ra chuyện gì, khi nào, diễn biến kỹ thuật chi tiết ra sao** (không
chỉ "hiện đang thế nào" — cái đó CLAUDE.md đã có):
- Dùng **Grep** (không phải Read nguyên file — file này cộng dồn nhiều phiên, có thể dài) tìm từ khoá
  liên quan tới câu hỏi trên `docs/session-log/history.md`, lấy ngữ cảnh quanh chỗ khớp (`-C` 10-20
  dòng).
- Nếu Grep khớp nhiều chỗ, đọc thêm bằng Read với `offset`/`limit` đúng đoạn cần (thường là nguyên 1
  mục "## N. Tóm tắt phiên làm việc..." chứa từ khoá), KHÔNG đọc nguyên file vào context.
- Nếu Grep không ra kết quả liên quan: coi như không tìm thấy tiền lệ, nói rõ điều đó với người dùng
  thay vì im lặng bỏ qua bước tra cứu.

### Bước 3: Tổng hợp & đưa vào hội thoại

Trước khi trả lời chính (hoặc lồng ngay đầu câu trả lời), tóm tắt ngắn gọn những gì tìm được, bằng
lời diễn giải tiếng Việt (không dán nguyên văn Markdown thô của CLAUDE.md/history.md). Ví dụ:

```
Theo CLAUDE.md (mục 6): PC có thể làm Master hoặc Slave vì PLC thật sẽ làm Master quản lý nhiều
thiết bị RS-485 khác — PC chỉ là 1 slave trong số đó. [Trả lời/đề xuất chính dựa trên nền đó...]
```

Nếu không tìm thấy gì liên quan trong CLAUDE.md lẫn history.md: nói ngắn 1 câu ("Chưa có ghi nhận
nào về việc này trong dự án") rồi trả lời bằng hiểu biết thông thường (đọc code nếu cần) — KHÔNG để
việc "không tìm thấy" chặn đứng câu trả lời.

### Bước 4: Lặp lại cho các câu hỏi tiếp theo

Áp dụng lại đúng quy trình Bước 1-3 cho MỖI câu hỏi/vấn đề mới người dùng nêu trong cùng phiên, cho
tới khi người dùng yêu cầu dừng.

### Dừng lại

Khi người dùng nói rõ kiểu "không cần tra cứu nữa", "trả lời bình thường thôi" — ngừng tự động kích
hoạt skill này cho phần còn lại của phiên (vẫn có thể áp dụng lại nếu người dùng chủ động gọi lại
"QA Session").

---

## Giới hạn có chủ đích

- Skill này **không ghi** gì — mọi cập nhật CLAUDE.md/history.md luôn qua `/Works_End` (hoặc chỉnh
  sửa CLAUDE.md trực tiếp khi người dùng yêu cầu 1 thay đổi cụ thể, ngoài phạm vi skill này).
- Không coi CLAUDE.md/history.md là "chân lý tuyệt đối không cần kiểm chứng" — nếu tài liệu nhắc tới
  1 file/hàm/flag cụ thể, vẫn nên xác nhận nó còn tồn tại (đọc code thật) trước khi dựa vào đó để đề
  xuất hành động — đúng nguyên tắc chung "trước khi khuyến nghị từ ghi nhớ, phải xác minh vẫn còn
  đúng ở hiện tại".
