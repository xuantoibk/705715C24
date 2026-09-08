---
name: Works_End
description: Kết thúc phiên làm việc cho dự án EOL Tester 705/715 — hỏi tiến độ/quyết định quan trọng trong phiên, soạn 1 mục "Tóm tắt phiên làm việc" theo đúng khuôn đang dùng, rồi append vào docs/session-log/history.md (không ghi vào CLAUDE.md). Không tự git commit/push. Gọi bằng "/Works_End" hoặc "Works_End".
disable-model-invocation: false
---

# 🏁 Works_End — Kết thúc phiên làm việc

Skill này giúp kết thúc phiên làm việc gọn gàng: chốt lại những gì đã làm, cái gì còn dang dở, ưu
tiên cho phiên sau, và ghi đúng vào **`docs/session-log/history.md`** — nguồn lịch sử duy nhất của
dự án **EOL Tester 705/715** (xem CLAUDE.md Phần I mục 1: mỗi vòng làm việc kết thúc phải append
"Tóm tắt phiên làm việc" vào file lịch sử của dự án, **không bao giờ ghi vào CLAUDE.md** — bài học
rút ra sau khi CLAUDE.md dự án HV356 từng phình tới 506 dòng vì lẫn lịch sử phiên vào đó).

Đây là nửa "cuối phiên" của cặp skill `Works_Start`/`Works_End`. Khi bắt đầu phiên mới, dùng skill
`Works_Start`.

Dự án này **không dùng cơ chế Remember/History phân tầng theo module** (không có `docs/Source/`,
không có file `*-Remember.md` riêng) — CLAUDE.md (Phần II) tự đóng vai trò "trạng thái/quyết định
còn hiệu lực", còn `docs/session-log/history.md` là nơi duy nhất lưu diễn biến/lý do chi tiết theo
từng phiên. Không có script trung gian nào xử lý việc ghi — Claude tự Read + Edit trực tiếp file
này, vì thao tác chỉ là "append 1 khối text vào cuối file", không cần công cụ riêng.

**Nếu một quyết định trong phiên làm thay đổi trạng thái/quy tắc hiện hành của dự án** (không chỉ là
diễn biến lịch sử), phần đó phải được phản ánh vào **CLAUDE.md Phần II** (mục tương ứng — kiến trúc,
đặc tả màn hình, câu hỏi mở...) NGOÀI việc ghi vào history.md — hai việc độc lập, làm cả hai nếu áp
dụng.

---

## 📖 CÁCH SỬ DỤNG

```
/Works_End
```

---

## 🔄 WORKFLOW CHI TIẾT

### Bước 1: Hỏi những gì đã hoàn thành

```
✅ NHỮNG GÌ ĐÃ HOÀN THÀNH TRONG PHIÊN NÀY?
```
Lưu vào biến `COMPLETED`.

### Bước 2: Hỏi trạng thái "đang dang dở"

```
🔄 CÓ CÁI GÌ ĐANG LÀM DANG DỞ KHÔNG? (nếu không, trả lời "Không")
```
Lưu vào biến `IN_PROGRESS`.

### Bước 3: Hỏi bước tiếp theo cho phiên sau

```
⏭️ ƯU TIÊN TIẾP THEO LÀ GÌ? (liệt kê theo thứ tự, tối đa vài mục)
```
Lưu vào biến `NEXT_STEPS`.

### Bước 4: Hỏi quyết định quan trọng

```
🎯 CÓ QUYẾT ĐỊNH QUAN TRỌNG NÀO TRONG PHIÊN NÀY KHÔNG? (lựa chọn cách làm, đánh đổi chấp nhận,
quyết định kiến trúc... — nếu không, trả lời "Không")
```
Lưu vào biến `KEY_DECISIONS`.

### Bước 5: Xác định số thứ tự mục kế tiếp

Grep pattern `^## \d+\. Tóm tắt phiên làm việc` trên `docs/session-log/history.md`, lấy số lớn nhất
tìm được, cộng 1. Nếu file chưa có mục nào, bắt đầu từ `1`.

### Bước 6: Soạn nội dung mục nhật ký

Theo đúng khuôn đang dùng trong `docs/session-log/history.md` (tham khảo 2-3 mục gần nhất để bám
đúng giọng văn/mức độ chi tiết — không bịa cấu trúc mới):

```markdown
## [N]. Tóm tắt phiên làm việc ([yyyy-mm-dd]) — [Tiêu đề ngắn theo focus chính]

[1-2 câu bối cảnh: phiên này làm gì, xuất phát từ đâu.]

### Trạng thái cập nhật từng phần

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| [Việc 1 từ COMPLETED] | ✅ Xong | [chi tiết ngắn/tham chiếu file] |
| [Việc từ IN_PROGRESS, nếu có] | 🔄 Dang dở | [% / đang bị chặn bởi gì] |

### Bước tiếp theo (theo đúng thứ tự phụ thuộc)
1. [Priority 1 từ NEXT_STEPS]
2. [Priority 2 từ NEXT_STEPS]

### Quyết định quan trọng đã đưa ra và lý do
- **[Decision]** — Lý do: [Why]. [Đánh đổi nếu có]
```

Bỏ hẳn bảng "Trạng thái"/mục "Quyết định" nếu không có nội dung tương ứng (`IN_PROGRESS`/
`KEY_DECISIONS` = "Không") — không để mục rỗng. Giữ đúng dấu ✅/🔄/❌ như các mục cũ đã dùng.

### Bước 7: Show người dùng trước khi ghi

Hiển thị preview nội dung mục vừa soạn (Bước 6), hỏi xác nhận:
```
📝 SẼ APPEND VÀO docs/session-log/history.md:

[Nội dung mục "## N. Tóm tắt phiên làm việc — ..." vừa soạn]

a) Đã tốt rồi, ghi vào file
b) Cần sửa lại
```
Chỉ thực hiện Bước 8 sau khi người dùng chọn (a).

### Bước 8: Append vào docs/session-log/history.md

Dùng Edit, nối nội dung đã soạn vào **cuối file** (sau mục cuối cùng hiện có), cách nhau đúng 1 dòng
trống, giữ nguyên định dạng heading `## N. Tóm tắt phiên làm việc (...)` — không cần script/công cụ
trung gian nào khác.

Nếu phiên này có quyết định làm **thay đổi trạng thái/quy tắc hiện hành** (không chỉ là việc đã làm
xong) — cập nhật thêm đúng mục liên quan trong **CLAUDE.md Phần II** bằng Edit (KHÔNG thêm mục lịch
sử/nhật ký vào CLAUDE.md, chỉ cập nhật nội dung đặc tả/trạng thái hiện hành đang mô tả sai/thiếu).

### Bước 9: Git (chỉ nếu áp dụng)

Dự án này hiện **không phải git repository** (không có `.git`) — bỏ qua hoàn toàn bước commit. Nếu
sau này người dùng khởi tạo git cho dự án, mới cần nhắc "đã ghi xong, tự `git add`/`git commit` khi
sẵn sàng — không tự động commit/push trừ khi được yêu cầu rõ ràng".

### Bước 10: End of session message

```
🏁 PHIÊN LÀM VIỆC KẾT THÚC

📊 Tóm tắt: [số việc hoàn thành] hoàn thành, [số việc] đang dang dở
⏭️ Ưu tiên tiếp: [Next steps]
📝 Đã ghi: mục "## N. ..." vào docs/session-log/history.md[ + cập nhật CLAUDE.md mục ... nếu có]

🔗 Phiên sau: /Works_Start
```
