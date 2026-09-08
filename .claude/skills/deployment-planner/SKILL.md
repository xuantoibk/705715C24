---
name: deployment-planner
description: Lập kế hoạch triển khai công việc chi tiết với phân tích sâu. Sử dụng skill này khi người dùng có một công việc/dự án cần lập kế hoạch, yêu cầu phân tích chi tiết, chia nhỏ thành các phases, và tạo danh sách công việc cụ thể. Skill sẽ phân tích yêu cầu, chia phases/dependencies, lập timeline, và tạo báo cáo progress có checkbox theo dõi.
---

# Lập Kế Hoạch Triển Khai Công Việc (Deployment Planner)

Skill này giúp bạn phân tích công việc phức tạp và lập kế hoạch chi tiết, từng bước, với timeline cụ thể.

## Quy Trình Hoạt Động

### 1️⃣ **Giai Đoạn Phân Tích**
Khi nhận một yêu cầu công việc, skill sẽ:
- 📋 **Phân tích chi tiết** yêu cầu, xác định scope, mục tiêu, constraints
- 🔍 **Xác định dependencies** - công việc nào phải làm trước, cái nào có thể song song
- 💡 **Tự phân tích sâu nhiều góc độ** - kỹ thuật, rủi ro, phụ thuộc phần cứng (PLC/RS485) nếu liên quan
- ⏱️ **Ước lượng timeline** cho từng phase

### 2️⃣ **Giai Đoạn Lập Kế Hoạch**
Lập danh sách chi tiết:
- **Phase 0.X** - Chuẩn bị, setup, requirements
- **Phase 1.X** - Phát triển, implementation chính
- **Phase 2.X** - Testing, refinement, deployment

Mỗi phase có:
- 🎯 Mô tả rõ ràng bằng tiếng Việt có dấu
- ✅ Checkbox để đánh dấu hoàn thành
- ⏳ Timeline ước lượng

### 3️⃣ **Giai Đoạn Báo Cáo**
- 📊 Tạo HTML widget hiển thị danh sách phases (yêu cầu lưu offline trên PC)
- 📈 Tracking progress real-time
- 🔄 Có thể cập nhật, thêm/xóa phases theo yêu cầu

## Cách Sử Dụng

**Người dùng chỉ cần:**
```
Tôi cần [mô tả công việc chi tiết]
```

**Skill sẽ:**
1. Phân tích yêu cầu
2. Tự phân tích sâu các góc độ liên quan (kỹ thuật/rủi ro/phụ thuộc)
3. Tạo danh sách phases và tasks
4. Hiển thị báo cáo dạng checklist interative

## Ví Dụ

**Input:**
> Tôi cần thêm driver MC Protocol (Mitsubishi) cho EOL Tester, dùng chung interface
> IPlcCommunicationDriver hiện có, chọn được qua tab Setup

**Output:** (Danh sách phases chi tiết)
```
Phase 0.1: Xác nhận khung frame MC 3E/4E cần hỗ trợ (binary/ASCII) & bảng địa chỉ device thật
Phase 0.2: Viết codec encode/decode request-response (unit test, không cần PLC thật)
Phase 1.1: Cài McProtocolDriver implement IPlcCommunicationDriver
Phase 1.2: Thêm ProtocolType.Mc vào factory chọn driver theo cấu hình Setup
Phase 1.3: Mở khóa ComboBox Protocol ở tab Setup (hiện đang khóa cứng Modbus RTU)
Phase 2.1: Integration test qua TCP loopback (theo mẫu ModbusSlaveServiceTests)
Phase 2.2: Kiểm thử với PLC Mitsubishi thật (ghi chú trong PR — không tự động hóa hoàn toàn được)
Phase 2.3: Cập nhật CLAUDE.md mục 6/13 theo kết quả thật
```

## Tính Năng Thêm

- 🤖 **AI-powered phân tích** - Gọi Claude để phân tích thêm các góc độ bạn chưa nghĩ tới
- 🔗 **Dependency tracking** - Tự động xác định công việc có thể làm song song
- 📱 **Progress tracking** - Checkbox realtime, báo cáo tiến độ
- 🔄 **Flexible updates** - Dễ dàng thêm/sửa/xóa phases khi có thay đổi
- 📊 **Visual reports** - HTML widget đẹp, dễ theo dõi

---

## Kỹ Thuật Triển Khai (Dành cho Claude)

### Prompt Internal cho AI Analysis
Khi skill trigger, sử dụng prompt này để phân tích công việc:

```
Bạn là project manager chuyên phân tích công việc phức tạp.
Phân tích yêu cầu sau:
[USER REQUEST]

Hãy:
1. Xác định scope, mục tiêu chính, constraints
2. Liệt kê các risks/challenges có thể gặp
3. Xác định các dependencies (công việc nào phải trước, nào có thể song song)
4. Ước lượng timeline cho từng component

Trả lời bằng tiếng Việt, chi tiết và thực tế.
```

### Output Format

HTML widget hiển thị:
```html
<div class="todo-list">
  <h2>📋 [Tên Công Việc]</h2>
  <div class="phase">
    <input type="checkbox">
    <span>Phase X.X: [Mô tả chi tiết]</span>
    <span class="timeline">⏳ Timeline</span>
  </div>
  ...
</div>
```

---

## Quy Trình Sử Dụng Bước-Từng-Bước

1. **Người dùng input**: Mô tả công việc cần lập kế hoạch
2. **Skill triggers** → Phân tích requirements
3. **Tự phân tích chi tiết các khía cạnh** (không gọi công cụ/API ngoài nào — Claude tự suy luận)
4. **Lập danh sách phases** → Tạo timeline, dependencies
5. **Render HTML widget** → Hiển thị checklist interative
6. **Tracking & Updates** → Người dùng có thể update progress, thêm/xóa tasks

---

✨ **Đặc điểm chính**: Skill này sẽ làm việc như một Project Manager AI, tự động phân tích công việc phức tạp thành các phases nhỏ, lập timeline, và giúp bạn tracking progress một cách hiệu quả.
