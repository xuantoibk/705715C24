---
name: Works_Start
description: Bắt đầu phiên làm việc có cấu trúc cho dự án EOL Tester 705/715 — đọc mục "Tóm tắt phiên làm việc" gần nhất trong docs/session-log/history.md, xác nhận trạng thái/ưu tiên đợt trước, hỏi focus task hôm nay. Gọi bằng "/Works_Start" hoặc "Works_Start".
disable-model-invocation: false
---

# 🚀 Works_Start — Bắt đầu phiên làm việc

Skill này giúp bắt đầu một phiên làm việc có cấu trúc cho dự án **EOL Tester 705/715** (WPF/.NET,
xem CLAUDE.md): nắm lại trạng thái đợt trước, xác nhận focus hôm nay, trước khi bắt tay vào code.

Đây là nửa "đầu phiên" của cặp skill `Works_Start`/`Works_End`. Khi kết thúc phiên, dùng skill
`Works_End`. Nguồn lịch sử duy nhất của dự án này là **`docs/session-log/history.md`** — một file
append-only, KHÔNG có tầng "Remember" riêng, KHÔNG chia theo module (khác với dự án khác có thể
dùng `docs/Source/*-Remember.md` — dự án này cố tình chọn cơ chế đơn giản hơn, xem CLAUDE.md mục 1).
Trạng thái/quyết định còn hiệu lực đã nằm sẵn trong chính **CLAUDE.md** (Phần II) — không cần file
Remember riêng.

---

## 📖 CÁCH SỬ DỤNG

```
/Works_Start
```

---

## 🔄 WORKFLOW CHI TIẾT

### Bước 1: Đọc mục "Tóm tắt phiên làm việc" gần nhất

`docs/session-log/history.md` có thể dài (nhiều phiên cộng dồn) — **không Read nguyên file**. Cách
lấy đúng mục cuối:
- Dùng Grep pattern `^## \d+\. Tóm tắt phiên làm việc` trên file này để tìm chỉ số dòng của TẤT CẢ
  các mục, lấy dòng khớp **cuối cùng**.
- Read file với `offset` = dòng đó, không cần `limit` (đọc tới hết file — mục cuối luôn là mục mới
  nhất vì file chỉ append).

Từ mục vừa đọc, trích:
- Tiêu đề + ngày của phiên gần nhất.
- Bảng "Trạng thái cập nhật từng phần" (nếu có).
- "Bước tiếp theo" (danh sách ưu tiên đã ghi lại).
- "Quyết định quan trọng đã đưa ra và lý do" (nếu có, chỉ cần liệt kê tên quyết định, không cần chép
  nguyên văn lý do dài).

Nếu `docs/session-log/history.md` chưa có mục nào (dự án hoàn toàn mới) → báo rõ "Chưa có lịch sử
phiên làm việc nào" thay vì bịa nội dung, rồi bỏ qua thẳng Bước 3.

### Bước 2: Xác nhận trạng thái hiện tại

Hiển thị cho người dùng (bằng tiếng Việt, diễn giải lại — không dán nguyên văn Markdown thô):

```
📋 TRẠNG THÁI PHIÊN LÀM VIỆC HIỆN TẠI

🏁 Đợt gần nhất: [Tiêu đề + ngày của mục cuối trong history.md]

📊 Trạng thái từng phần (nếu bảng có trong mục đó):
- [Phần] — [Trạng thái] — [Ghi chú ngắn]

⏭️ Bước tiếp theo (đã ghi lại từ đợt trước):
1. [Priority 1]
2. [Priority 2]
3. [Priority 3]

---
```

### Bước 3: Hỏi focus task hôm nay

Dùng AskUserQuestion (hoặc hỏi trực tiếp nếu không có ràng buộc chọn 1-trong-N rõ ràng):

```
Hôm nay bạn muốn tập trung vào việc nào từ danh sách "Bước tiếp theo" ở trên?
```
Các lựa chọn = đúng danh sách "Bước tiếp theo" vừa trích + 1 lựa chọn "Việc khác (mô tả)".

### Bước 4: Xác nhận hiểu task (chỉ khi task không hiển nhiên)

Nếu task người dùng chọn/mô tả còn mơ hồ (nhiều cách hiểu, thiếu thông tin) — áp dụng đúng quy tắc
CLAUDE.md Phần I mục 0: **hỏi lại ngay trước khi code**, không tự đoán. Nếu task đã rõ ràng, bỏ qua
bước xác nhận này, đi thẳng vào việc.

### Bước 5: Kiểm tra môi trường (nhẹ, không tốn thời gian)

Dự án này **không phải git repository** (đã xác nhận — `git rev-parse` báo lỗi "not a git
repository"). Nếu sau này người dùng khởi tạo git, mới cần bước kiểm tra `git status`; hiện tại bỏ
qua hẳn bước này, không cố chạy lệnh git vô ích.

Không có bước "cài dependencies" kiểu `npm install` — đây là dự án .NET, NuGet tự phục hồi package
lúc `dotnet build`/`dotnet run`, không có bước cài đặt thủ công riêng.

**Không tự động chạy `dotnet build`/`dotnet run` ở bước này** — build có thể mất thời gian và
`dotnet run` là tiến trình chạy mãi (ứng dụng WPF), gọi trực tiếp sẽ treo phiên nếu không chạy nền.
Chỉ build/chạy khi task hôm nay thực sự cần verify code hoặc xem UI — dùng đúng lệnh ở CLAUDE.md mục
"Lệnh thường dùng", và nếu cần xem UI thật thì dùng skill `verify-ui` (chụp ảnh bằng `PrintWindow`,
không chụp full-screen).

### Bước 6: Startup message

```
🎯 PHIÊN LÀM VIỆC SẴN SÀNG

📌 TASK: [Task name]
💪 Bắt đầu thôi!

---
🔗 Khi xong, dùng: /Works_End
```

---

## 💡 GHI CHÚ

- Nếu bị chặn từ phiên trước (mục "Đang dang dở" trong lịch sử), ưu tiên xử lý/xác nhận hướng đi
  trước khi chuyển sang việc mới.
- Mỗi phiên nên tập trung 1 focus chính — nếu người dùng nêu 2 việc không liên quan, gợi ý tách
  thành 2 phiên (không bắt buộc, chỉ là gợi ý).
