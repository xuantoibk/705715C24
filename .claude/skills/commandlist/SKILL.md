---
name: commandlist
description: Liệt kê toàn bộ lệnh/trigger cố định đã định nghĩa trong dự án (agents, skills, quy ước gọi tắt). Chạy khi bắt đầu phiên mới hoặc sau /init.
---

# commandlist

Khi skill này được gọi (bằng "commandlist" hoặc "list lệnh"):

1. Quét các nguồn sau trong dự án để tìm lệnh/trigger đã định nghĩa:
   - `.claude/agents/*.md` — mỗi file là 1 agent, đọc frontmatter (`name`, `description`) để lấy cách gọi và mục đích.
   - `.claude/skills/*/SKILL.md` — mỗi thư mục là 1 skill, đọc frontmatter (`name`, `description`).
   - Bảng tổng hợp thủ công bên dưới (mục "Bảng lệnh") — các lệnh/trigger không map 1-1 vào agent/skill file (vd quy ước gọi tắt được thống nhất trực tiếp trong hội thoại).
2. In ra một bảng Markdown với các cột: **Lệnh/Trigger | Loại (agent/skill/quy ước) | Mô tả ngắn | Vị trí file**.
3. Nếu chưa có agent/skill nào ngoài `commandlist`, báo rõ "Hiện dự án chưa có lệnh nào khác ngoài `commandlist`" thay vì bịa nội dung.

## Bảng lệnh

Bước 1 ở trên đã tự quét frontmatter của mọi agent/skill để lấy tên + mô tả — **không chép lại**
danh sách đó ra bảng thủ công bên dưới, vì 2 nguồn dữ liệu dễ lệch nhau theo thời gian (khi thêm/xoá
skill mà quên cập nhật bảng tay). Bảng này CHỈ dùng cho các lệnh/trigger **không map được vào 1 file
agent/skill cụ thể** — ví dụ quy ước gọi tắt được thống nhất trực tiếp trong hội thoại, không có
frontmatter để quét.

| Lệnh/Trigger | Loại | Mô tả ngắn | Vị trí file |
|---|---|---|---|

_(Hiện chưa có mục nào thuộc loại này — mọi lệnh/trigger hiện tại đều là agent/skill đã có file,
lấy trực tiếp từ bước quét tự động.)_
