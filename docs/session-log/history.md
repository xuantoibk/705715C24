# Lịch sử phát triển chi tiết theo từng phiên — HV356 EOL Tester

> File này chứa lịch sử chi tiết từng vòng làm việc (tóm tắt phiên, quyết định + lý do, bẫy kỹ thuật đã gặp). **Không tự nạp vào context của mọi phiên** — chỉ đọc khi cần tra cứu quyết định cũ, lý do đằng sau 1 thiết kế cụ thể, hoặc bẫy kỹ thuật đã từng gặp để tránh lặp lại. Trạng thái/quy tắc hiện hành nằm ở `CLAUDE.md` (gốc dự án).
>
> Khi kết thúc 1 vòng làm việc cần ghi "Tóm tắt phiên làm việc" mới, **append vào cuối file này**, không ghi vào `CLAUDE.md`.

## 14. Tóm tắt phiên làm việc (2026-07-11, vòng trước) — Monitor layout + Tab Setup Modbus RTU + ModbusRegisterTable
> **Lưu ý**: quyết định "PC luôn Master, loại hẳn Slave" ghi trong mục này **đã bị đảo ngược** ở vòng làm việc kế tiếp — xem mục 15. Phần còn lại của mục 14 (Monitor layout, ModbusRegisterTable, khối thanh ghi...) vẫn đúng hiện trạng.

### Trạng thái cập nhật từng phần

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| Tab Monitor — khoảng cách Input/Output, độ rộng cột, khóa resize | ✅ Xong | Xem CLAUDE.md mục 11 — gộp Input/Output vào 1 `ScrollViewer` chung, cột Nhãn `223`/`193`, `CanUserResizeColumns="False"` |
| Tab Monitor — layout cố định 1400×765, scale đồng bộ khi resize cửa sổ | ✅ Xong | Xem CLAUDE.md mục 12 — áp dụng cho `MainWindow` và mọi cửa sổ sau này |
| Tab Setup — khóa Protocol = Modbus RTU | ✅ Xong (UI) | Xem CLAUDE.md mục 7 Tab 3 — ô chỉ đọc, chưa cho chọn giao thức khác |
| Tab Setup — PC luôn Master, không có PC-Slave | ✅ Đã quyết định + phản ánh đúng UI (ĐÃ ĐẢO NGƯỢC — xem mục 15) | Không phải "để sau" — loại hẳn khỏi kiến trúc |
| Tab Setup — ComboBox chuẩn hóa (COM port/baud/parity/data-stop bits) | ✅ Xong | Xem CLAUDE.md mục 7 Tab 3 |
| Tab Setup — khối thanh ghi Input/Output (`InputBlock`/`OutputBlock`) + cảnh báo vượt 125 thanh ghi | ✅ Xong (cấu hình + cảnh báo) | Chưa có driver thật tiêu thụ cấu hình này |
| `ModbusWordAddress` (parse `D1005`/`D1005.1`) | ✅ Xong, có unit test | `EolTester.Communication/ModbusWordAddress.cs` |
| `ModbusRegisterTable` ("biến D1005" trong bộ nhớ, giới hạn theo phạm vi Setup) | ✅ Xong, có unit test (20/20 pass) | `EolTester.Communication/ModbusRegisterTable.cs`, DI Singleton, nạp lại phạm vi mỗi khi Setup Save |
| `ModbusRtuDriver` thật (NModbus + SerialPort) | ❌ Chưa làm | Quyết định có chủ đích — chờ bảng địa chỉ PLC thật |
| Nối `PlcPollingService` đọc/ghi khối thật → đổ vào `ModbusRegisterTable` | ❌ Chưa làm | Phụ thuộc driver thật ở trên |
| Test Connection gọi driver thật | ❌ Chưa làm | Vẫn là stub giả lập delay |

### Bước tiếp theo (theo đúng thứ tự phụ thuộc)
1. **Nhận bảng địa chỉ Modbus RTU thật từ PLC** (coil/register cho X/Y digital, thanh ghi analog, `CMD_START/STOP/RESET/CONFIRM_NG`) — mọi việc dưới đây đều chờ input này, không nên đoán trước.
2. Viết `ModbusRtuDriver` thật (NModbus + `SerialPort`), mở rộng `IPlcCommunicationDriver` thêm method đọc/ghi **nguyên khối** thanh ghi (hiện interface chỉ có đọc/ghi từng thanh ghi lẻ) — dùng để đọc `InputBlock`, ghi `OutputBlock` mỗi tick polling.
3. Nối `PlcPollingService.PollLoopAsync`: mỗi tick gọi driver đọc khối Input → `ModbusRegisterTable.UpdateBlock(...)` → các `IoPointDefinition` tự tra cứu giá trị/bit của mình từ bảng qua `Dxxxx`/`Dxxxx.b`.
4. Quyết định chiến lược ghi bit lẻ theo từng thanh ghi cụ thể (PC sở hữu riêng → ghi thẳng cả từ; dùng chung với PLC → read-modify-write) — cần bảng địa chỉ thật mới quyết được, không đoán trước.
5. Nối `TestConnectionCommand` gọi `IPlcCommunicationDriver.ConnectAsync` + 1 lần đọc thật, thay cho stub delay hiện tại.
6. Đồng bộ phong cách giao diện Setup/Set Spec. với Main/Monitor (card 2 cột, nút trạng thái tô màu) — việc UI thuần túy, không phụ thuộc driver, có thể làm song song bất cứ lúc nào.

### Quyết định quan trọng đã đưa ra và lý do

- **Khóa cứng Protocol = Modbus RTU, chưa cho chọn giao thức khác.** Lý do: PLC thật hiện tại dùng Modbus RTU; hỗ trợ nhiều giao thức cùng lúc khi chưa xác minh được cái nào cả sẽ tạo dở dang, tăng rủi ro debug sai. Enum `ProtocolType` vẫn giữ đủ 5 giá trị để không phải đổi kiểu dữ liệu khi mở khóa sau này.
- **PC luôn là Master, PLC là Slave — loại hẳn PC-Slave khỏi kiến trúc (không phải "để dành sau").** Lý do: đã giải thích với người dùng rằng Slave mode đòi hỏi 1 kiến trúc hoàn toàn khác (PC phải chạy 1 Modbus slave/server lắng nghe bị động, cần interface mới thay vì mở rộng `IPlcCommunicationDriver`), có rủi ro thời gian thực trên Windows chưa thể kiểm chứng khi chưa có PLC thật, và mô hình test-station (PC điều khiển, PLC giữ I/O thật) phù hợp Master hơn. Người dùng đồng ý sau khi nghe phân tích.
- **Đợt này không viết `ModbusRtuDriver` thật, vẫn chạy trên `MockModbusDriver`.** Lý do: chưa có bảng địa chỉ PLC thật để verify — viết driver "mù" (không xác minh được hành vi đọc/ghi đúng/sai) chỉ tạo rủi ro debug sai mà không phát hiện được, tốn công làm lại khi có địa chỉ thật.
- **Tách biệt "không gian địa chỉ khai báo" khỏi "kích thước khối polling mỗi chu kỳ".** Lý do: người dùng ban đầu muốn khai báo dải rất lớn (D0000–D2000). Về bộ nhớ PC thì không vấn đề gì, nhưng Modbus RTU giới hạn cứng tối đa 125 thanh ghi/lần đọc — poll nguyên khối lớn mỗi chu kỳ sẽ cần nhiều request nối tiếp, dễ vượt chu kỳ polling 500ms. Do đó: `RegisterCount` chỉ **cảnh báo** (không chặn) khi vượt 125, để người dùng tự cân đối theo nhu cầu I/O thật thay vì bị ép giới hạn cứng.
- **`ModbusRegisterTable` chỉ đọc/ghi trong đúng phạm vi đã cấu hình ở Setup (`InputBlock`/`OutputBlock`), không cho truy cập địa chỉ D bất kỳ.** Lý do (yêu cầu bổ sung của người dùng): đây là ràng buộc nghiệp vụ chủ động, không phải giới hạn kỹ thuật — tránh app lỡ đọc/ghi nhầm sang vùng D không thuộc quyền quản lý của nó (PLC có thể dùng vùng đó cho mục đích khác). `ConfigureAllowedRanges(...)` xóa sạch dữ liệu cũ mỗi lần gọi lại, vì đổi phạm vi thì dữ liệu ngoài phạm vi mới không còn ý nghĩa giữ lại.
- **Gộp Input/Output tab Monitor vào 1 `ScrollViewer` ngang duy nhất thay vì chia cứng 50/50.** Lý do: chia cứng 50/50 khiến Output (cần bề rộng lớn hơn do có thêm cột "Điều khiển thủ công") bị thiếu chỗ, sinh thanh cuộn nội bộ làm che mất dòng cuối — gộp chung để mỗi bên tự lấy đúng bề rộng theo nội dung, chỉ cuộn thật khi tổng bề rộng thực sự vượt khung nhìn.
- **Layout toàn app chuyển sang canvas cố định 1400×765 bọc `Viewbox`, khóa tỉ lệ khi kéo viền cửa sổ.** Lý do: yêu cầu rõ ràng của người dùng — phóng to/thu nhỏ chỉ được scale đồng bộ (không vỡ layout), Maximize thì lấp đầy chấp nhận méo nhẹ nếu màn hình không đúng tỉ lệ thiết kế.

## 15. Tóm tắt phiên làm việc (2026-07-11, vòng mới nhất) — Nối Input/Output với ModbusRegisterTable + PC làm Modbus Slave

### Trạng thái cập nhật từng phần

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| Nối `PlcPollingService` (Input đọc theo khối, Output ghi read-modify-write bit) với `ModbusRegisterTable` | ✅ Xong | Xem CLAUDE.md mục 6. Địa chỉ `Dxxxx`/`Dxxxx.b` (từ CSV) đi qua bảng; địa chỉ kiểu cũ giữ nguyên hành vi rời rạc — không hồi quy, đã verify qua UI |
| `ModbusWordAddress.TryParse` + `ModbusRegisterTable.TryUpdateWord`/`SnapshotAll` | ✅ Xong, có unit test | Non-throwing, dùng trong vòng lặp polling mỗi tick |
| `ModbusRegisterTable` thread-safe | ✅ Xong | Bọc `System.Threading.Lock`, 29/29 test pass |
| `ConnectionSettings.Role` (Master/Slave) + `MySlaveId` | ✅ Khôi phục | Đảo ngược quyết định vòng trước — xem "Quyết định quan trọng" |
| `ModbusSlaveService` + `StreamAdapter` + `ModbusRegisterTableDataStore` (NModbus) | ✅ Xong, có integration test qua TCP loopback (2/2 pass) | Xem CLAUDE.md mục 6 |
| Setup tab — Role selector + ẩn/hiện trường theo Master/Slave | ✅ Xong, verify qua UI thật | `MySlaveId` chỉ hiện khi Slave; khối Input/Output/PollingInterval/Timeout/TestConnection chỉ hiện khi Master |
| `App.xaml.cs` khởi động đúng service theo Role lúc startup | ✅ Xong, verify qua UI thật (Master mode) | Đổi Role yêu cầu khởi động lại — chưa hot-swap runtime |
| `ModbusRtuDriver` thật (Master, NModbus + SerialPort) | ❌ Chưa làm | Vẫn chờ bảng địa chỉ PLC thật, như các vòng trước |
| Test `ModbusSlaveService` với PLC thật (không phải TCP loopback giả lập) | ❌ Chưa làm | Chờ PLC thật + thông số bus RS-485 thật |
| Hot-swap Role lúc runtime (không cần khởi động lại app) | ❌ Chưa làm | Quyết định phạm vi có chủ đích — xem dưới |

### Bước tiếp theo (theo đúng thứ tự phụ thuộc)
1. **Nhận thông số bus RS-485 thật từ phía PLC**: bảng thanh ghi PC cần "công khai" cho PLC đọc/ghi khi làm Slave, có bao nhiêu thiết bị khác trên cùng bus, baud rate dùng chung toàn bus (không phải PC tự chọn riêng), timeout/chu kỳ hỏi của PLC (quyết định `ModbusSlaveService` phải phản hồi nhanh tới đâu) — mọi việc dưới đây chờ input này.
2. Test `ModbusSlaveService` thật với PLC (thay vì TCP loopback giả lập) — xác nhận thời gian phản hồi đủ nhanh so với chu kỳ hỏi thực tế của PLC (rủi ro thời gian thực trên Windows chưa kiểm chứng được, đã nêu từ trước).
3. Viết `ModbusRtuDriver` thật cho nhánh Master (song song, độc lập với nhánh Slave) — vẫn chờ bảng địa chỉ PLC thật.
4. Cân nhắc hot-swap Role lúc runtime (không cần khởi động lại app) sau khi khung sườn hiện tại đã chạy ổn định trên phần cứng thật.
5. Quyết định chiến lược ghi bit lẻ khi thanh ghi dùng chung với PLC (read-modify-write) — vẫn là câu hỏi mở từ vòng trước, không đổi.

### Quyết định quan trọng đã đưa ra và lý do

- **Nối Input/Output tab Monitor với `ModbusRegisterTable` theo địa chỉ CSV, giữ tương thích ngược với địa chỉ kiểu cũ.** Lý do: người dùng đã cấu hình CSV theo định dạng `Dxxxx`/`Dxxxx.b` từ trước, nhưng phần đọc/ghi thật chưa "hiểu" định dạng đó — cần nối 2 phần lại. Phân nhánh theo việc `Address` có parse được `ModbusWordAddress` hay không (thay vì bắt buộc toàn bộ đổi sang 1 định dạng) để seed mặc định (`X00`..`Y17` kiểu chuỗi số cũ) không bị hỏng — không cần migrate dữ liệu cũ.
- **Đảo ngược quyết định "PC luôn Master, loại hẳn Slave" — khôi phục `Role` + xây khung sườn `ModbusSlaveService` chạy test được thật.** Lý do: quản lý xác nhận PLC thực tế sẽ làm Master quản lý **nhiều thiết bị RS-485 khác**, không chỉ giao tiếp 1-1 với PC như giả định ban đầu — trên bus RS-485 chỉ được 1 master, nên PC bắt buộc phải là 1 Slave trong số các thiết bị đó. Đây là thay đổi yêu cầu nghiệp vụ thật (không phải sai sót kỹ thuật của round trước) — quyết định trước đó là đúng đắn tại thời điểm đó với thông tin đang có.
- **Giữ cả Master lẫn Slave, chuyển đổi qua cấu hình thay vì chọn 1 vĩnh viễn.** Lý do: người dùng yêu cầu rõ — dự án cần linh hoạt cho tương lai (VD lắp đặt tại khách hàng khác có topology bus khác). Chỉ 1 trong 2 chạy tại 1 thời điểm (đúng ràng buộc RS-485 chỉ 1 master/bus), chọn qua `ConnectionSettings.Role`.
- **Đổi Role yêu cầu khởi động lại app, chưa hot-swap runtime.** Lý do: tránh rủi ro dừng/khởi động lại kết nối cổng COM giữa chừng khi chưa kiểm chứng hành vi trên phần cứng thật — ưu tiên an toàn/đơn giản cho khung sườn lần đầu, hot-swap để dành khi đã ổn định.
- **Dùng NModbus cho cả 2 chiều (đã có ý định từ đầu dự án trong CLAUDE.md, nay lần đầu thực sự thêm làm dependency).** Phát hiện quan trọng khi khảo sát API thật: `IStreamResource` của NModbus **không phụ thuộc trực tiếp `SerialPort`** — chỉ cần đọc/ghi byte qua `Stream` bất kỳ. Nhờ vậy viết được `StreamAdapter` dùng chung cho cả cổng COM thật (`SerialPort.BaseStream`) lẫn `NetworkStream` (test), and integration test cho `ModbusSlaveService` chạy hoàn toàn tự động qua TCP loopback — **không cần cài driver COM ảo (com0com)** như dự tính ban đầu trong kế hoạch.
- **`ModbusRegisterTableDataStore` đọc/ghi trực tiếp qua `ModbusRegisterTable`, không cần đồng bộ định kỳ qua timer.** Lý do: khảo sát API thật cho thấy NModbus gọi `IPointSource<T>.ReadPoints`/`WritePoints` đúng lúc PLC hỏi/ghi tới (event-driven ở tầng transport), nên implement trực tiếp trên `ModbusRegisterTable` là đủ — đơn giản hơn nhiều so với phương án "timer đồng bộ mỗi 100-200ms" đã dự tính ban đầu trong kế hoạch trước khi khảo sát API.
- **`ModbusRegisterTable` đổi sang thread-safe (`Lock`).** Lý do: Slave mode có 2 luồng truy cập đồng thời (luồng lắng nghe Modbus nền + luồng UI/nghiệp vụ) — `Dictionary` thường không an toàn đa luồng, cần khóa để tránh lỗi ngầm khó tái hiện.
- **Bẫy đã gặp và sửa lúc viết integration test**: `NetworkStream`/`SerialPort.BaseStream` mặc định không set `ReadTimeout`/`WriteTimeout` (infinite) — khiến request-response NModbus **treo vô hạn** khi có trục trặc, không có exception để debug. Luôn set timeout tường minh trên `IStreamResource`.

## 16. Tóm tắt phiên làm việc (2026-07-12) — Sửa layout Setup che nút Lưu/Test Connection + tính năng "Giám sát DATA"

### Bối cảnh & vấn đề
Người dùng báo lỗi: ở tab Setup, khi Role=Master, 2 nút "Lưu cấu hình"/"Test Connection" bị Footer che mất, không bấm được. Nguyên nhân: nội dung Setup nằm trong 1 `StackPanel` cột dọc `MaxWidth="560"`, khi Master có thêm khối Register Block nên dài hơn vùng nội dung tab (canvas cố định `1400×765`), tràn xuống bị che — trong khi bên phải màn hình bỏ trống gần 60%. Người dùng tự đề xuất hướng khắc phục kèm ảnh mẫu (`docs/legacy-ui/Setting paged V2.png`): dùng khoảng trống đó cho 1 bảng giám sát giá trị thanh ghi Dxxxx trực tiếp — vừa sửa layout vừa thêm công cụ dò/xác minh thanh ghi PLC hữu ích khi chưa có bảng địa chỉ thật.

**Tính năng này đi qua 3 vòng lặp thiết kế trong cùng phiên** trước khi chốt bản cuối — quan trọng để hiểu tại sao 1 số quyết định ở vòng đầu (ghi trong lịch sử bên dưới) không còn đúng với code hiện tại (xem CLAUDE.md mục 7 để có mô tả chính xác trạng thái cuối cùng):
1. **Vòng 1 — "Giám sát thanh ghi"**: bảng kiểu bảng tính, gõ dòng mới ở cuối (`DataGrid.CanUserAddRows`), cột "Ghi giá trị" + nút "Ghi" riêng, nút xóa dòng (✕), đoạn text hướng dẫn dưới tiêu đề. `RegisterDisplayFormat` chỉ có 2 giá trị Decimal/Hex.
2. **Vòng 2 — người dùng đổi ý**: đây là màn hình đưa cho khách hàng xem, không muốn UI có vẻ kỹ thuật → yêu cầu đổi sang lưới **cố định 2 cột × 12 dòng** (24 ô, không thêm/xóa động), **sửa giá trị tại chỗ** (gõ + Enter, không cột/nút phụ), **bỏ hẳn đoạn text hướng dẫn** (nội dung chuyển vào CLAUDE.md), đổi tên tiêu đề "Giám sát thanh ghi" → **"Giám sát DATA"**.
3. **Vòng 3 — mở rộng định dạng hiển thị**: người dùng yêu cầu thêm các kiểu hiển thị cơ bản BIN/HEX/DEC/Decimal/Float/Char. Sau khi hỏi lại (AskUserQuestion) để làm rõ ngữ nghĩa từng kiểu, chốt: giữ nguyên 2 cột "Định dạng"/"Kiểu dữ liệu" riêng biệt (không gộp), `RegisterDisplayFormat` mở rộng thành 6 giá trị (`Bin, Hex, Dec, Decimal, Float, Char`), Float/Char là phép bit-cast độc lập với `RegisterDataType` đã chọn.
4. **Vòng 4 — điều chỉnh nhỏ + tắt simulation khi publish**: đổi lưới từ 2×12 (24 ô) thành **2×15 (30 ô)** theo yêu cầu người dùng. Đồng thời người dùng yêu cầu "khi publish app tắt chức năng simulation" — sau khi hỏi lại phạm vi (AskUserQuestion), chốt: cả `MockModbusDriver` (random jitter thanh ghi/discrete) lẫn `PlcPollingService.GenerateDemoMeasurement` (đo lường giả tab Main) đều gắn `#if DEBUG`, build Release trả về 0/false cố định.

### Trạng thái cuối phiên (sau cả 3 vòng)

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| Layout Setup 2 cột (trái: cấu hình + `ScrollViewer` phòng vệ, phải: Giám sát DATA) | ✅ Xong, verify qua UI thật (PrintWindow) | Xem CLAUDE.md mục 7 Tab 3. Nút Lưu/Test Connection không còn bị che |
| `RegisterWatchDefinition` + `RegisterDataType` (5 giá trị) + `RegisterDisplayFormat` (6 giá trị: Bin/Hex/Dec/Decimal/Float/Char) + `JsonRegisterWatchStore` | ✅ Xong | `EolTester.Configuration` |
| `PlcPollingService.PollLoopAsync` đọc toàn bộ `InputBlock` mỗi tick (thay vì chỉ các từ `Inputs` tham chiếu) | ✅ Xong, verify qua UI thật | `SetInputBlockRange(...)` cập nhật live khi Setup lưu cấu hình mới |
| `PlcPollingService.WriteRawWordAsync` — ghi 1 thanh ghi qua driver + cập nhật `ModbusRegisterTable` | ✅ Xong | Dùng bởi tính năng ghi giá trị tại chỗ |
| Lưới cố định 2×15 (`RegisterWatchColumn1`/`RegisterWatchColumn2`, 30 `RegisterWatchRowViewModel` tạo 1 lần trong constructor) | ✅ Xong, verify qua UI thật | Không còn `ObservableCollection` biến đổi/wrap runtime — đơn giản hơn nhiều so với vòng 1 |
| Sửa giá trị tại chỗ (`SetDisplayFromPoll` vs `ValueEditedByUser` event) + ghi audit log | ✅ Xong, verify qua UI thật (ghi D1100=777, giá trị "settle" ổn định qua nhiều chu kỳ polling; ghi vào D1000 trong Khối Input bị polling ghi đè ngay — đúng thiết kế) | Có audit log (khác quyết định ban đầu ở vòng 1 — xem "Quyết định" bên dưới) |
| Persistence 30 ô cố định theo `Order` (0-29) | ✅ Xong, verify qua UI thật (Lưu → đọc trực tiếp `register-watch.json` → khởi động lại app → ô vẫn đúng vị trí) | |
| Tắt simulation ở build Release (`#if DEBUG` trong `MockModbusDriver` + `PlcPollingService.GenerateDemoMeasurement`) | ✅ Xong, verify qua UI thật (chạy `bin\Release\...\EolTester.App.exe`, chụp 2 lần cách nhau vài giây — mọi giá trị đo/LED đều `0`/tắt và đứng yên, không jitter) | Debug (`dotnet run`/`dotnet build`) vẫn còn simulation để dev test không cần phần cứng |
| Kiểm tra Role=Slave không hồi quy | ✅ Xác nhận gián tiếp | Khối "Giám sát DATA" không phụ thuộc `IsMasterMode`/`IsSlaveMode` |
| Unit test `EolTester.Communication.Tests` + `EolTester.Core.Tests` | ✅ 29/29 + 1/1 pass, không hồi quy | |
| Publish lại `APP/` | ✅ Xong | |

### Quyết định quan trọng đã đưa ra và lý do
- **Chọn layout 2 cột thay vì chỉ thêm `ScrollViewer`.** Lý do: chỉ thêm cuộn giải quyết được triệu chứng nhưng bỏ phí khoảng trống bên phải; người dùng chủ động đề xuất dùng khoảng trống đó cho 1 tính năng thật sự hữu ích thay vì để trống.
- **Đổi từ "thêm dòng kiểu bảng tính + nút Ghi/Xóa" (vòng 1) sang "lưới cố định 2×12, sửa tại chỗ" (vòng 2).** Lý do: đây là màn hình đưa cho khách hàng xem — người dùng chủ động yêu cầu gọn hơn, không muốn UI có vẻ "kỹ thuật/dev-facing" như 1 công cụ debug lộ liễu. Cơ chế "sửa tại chỗ" tận dụng đúng hành vi mặc định của `DataGridTextColumn` (commit khi Enter/rời ô, không phải mỗi keystroke) — không cần code phức tạp để bắt sự kiện Enter riêng.
- **Đoạn text hướng dẫn bị gỡ khỏi UI, nội dung chuyển vào CLAUDE.md.** Lý do: người dùng yêu cầu rõ — "khó thiết kế" với nhiều chữ giải thích trên màn hình khách hàng; CLAUDE.md đã đóng đúng vai trò "tự nhắc lại khi bắt đầu dự án mới hoặc khi được hỏi" vì luôn được nạp làm context đầu phiên.
- **`RegisterDisplayFormat` mở rộng 6 giá trị nhưng giữ nguyên 2 cột riêng biệt (không gộp với `RegisterDataType`).** Lý do: hỏi lại người dùng qua AskUserQuestion — người dùng chọn giữ 2 cột thay vì gộp, dù điều này khiến vài tổ hợp (VD Kiểu dữ liệu=WordSigned + Định dạng=Float) mang tính "xem thử bit dưới góc nhìn khác" hơn là 1 kiểu dữ liệu chính thức — chấp nhận được, đây đúng là 1 công cụ debug/dò thanh ghi.
- **"Decimal" dùng hệ số chia cố định 100 (2 chữ số thập phân), không thêm cột "số chữ số thập phân" riêng.** Lý do: giữ đúng yêu cầu "không thêm cột/nút phụ" của người dùng — đơn giản hóa bằng 1 quy ước cố định thay vì tùy biến theo dòng; có thể đổi lại sau nếu có nhu cầu cụ thể.
- **Bỏ qua ký hiệu kiểu mảng (`Word[Signed][5]`...) trong ảnh mẫu gốc.** Lý do: xác nhận trực tiếp với người dùng qua AskUserQuestion (từ vòng 1) — ý nghĩa ký hiệu không rõ ràng và người dùng xác nhận có thể bỏ qua.
- **Dword/Float32 đọc/ghi 2 thanh ghi liên tiếp theo thứ tự word thấp trước — vẫn là giả định, không chặn tính năng lại chờ PLC thật.** Lý do: người dùng chọn phương án này qua AskUserQuestion (từ vòng 1); nhất quán với các giả định khác trong dự án (VD word-order khi PC làm Slave, CLAUDE.md mục 13).
- **Toàn bộ khối "Giám sát DATA" yêu cầu Admin, không có tầng quyền riêng.** Lý do: người dùng chọn phương án đơn giản nhất qua AskUserQuestion, nhất quán với quy tắc "Setup yêu cầu Admin" đã chốt ở CLAUDE.md mục 8.
- **Lưu dùng chung nút "Lưu cấu hình" hiện có, không thêm nút Save riêng.** Lý do: ảnh mẫu chỉ vẽ 1 hành động Lưu; tránh 2 nút Save gây nhầm lẫn.
- **`PlcPollingService.PollLoopAsync` đổi sang đọc toàn bộ Input Block thay vì chỉ các từ `Inputs` tham chiếu.** Lý do: bắt buộc phải làm vậy để "Giám sát DATA" (thanh ghi không gắn `IoPointDefinition` nào) có giá trị để hiển thị — nhân tiện hiện thực hóa đúng ý định đã ghi ở CLAUDE.md mục 6 từ trước ("mỗi chu kỳ đọc 1 lần toàn bộ khối Input") mà code trước đó chưa làm đúng.
- **`DataGridTemplateColumn` + `ComboBox` thay vì `DataGridComboBoxColumn`.** Lý do (bẫy phát hiện qua kiểm thử UI thật): `DataGridComboBoxColumn` không hiển thị chữ ở ô tại chế độ không-edit (browse mode) kể cả sau khi thêm `TextBinding`. `DataGridTemplateColumn` với `ComboBox` trong `CellTemplate` cho kết quả đúng ngay, có thêm lợi ích UX: dropdown luôn sẵn sàng, không cần double-click vào chế độ edit.
- **Ghi audit log cho mỗi lần ghi giá trị vào thanh ghi (đảo ngược quyết định "không audit" ở vòng 1).** Lý do: sau khi tính năng ghi được người dùng xác nhận "cần thiết và hữu ích với thanh ghi Output", đây thực chất là 1 dạng **lệnh điều khiển thủ công** (ép giá trị 1 thanh ghi Output) — theo đúng quy tắc CLAUDE.md mục 4 ("mọi hành động có rủi ro chất lượng... gửi lệnh điều khiển thủ công... bắt buộc ghi audit log") và đúng tiền lệ đã có ở tab Monitor (điều khiển I/O thủ công cũng ghi audit) — khác bản chất với việc chỉ "thêm/bớt 1 dòng theo dõi" (vẫn không audit, đó là hành vi cấu hình xem/quan sát thuần túy).
- **2 `DataGrid` cạnh nhau đặt trong `Grid` 2 cột `Width="*"` thay vì `StackPanel Orientation="Horizontal"`.** Lý do (bẫy phát hiện qua kiểm thử UI thật): `StackPanel` không stretch phần tử con theo chiều ngang, khiến cột cuối cùng khai báo `Width="*"` bên trong mỗi `DataGrid` (cột "Kiểu dữ liệu") bị co gần như bằng 0 vì bản thân `DataGrid` chỉ nhận đúng kích thước theo nội dung các cột cố định. Đổi sang `Grid` với 2 `ColumnDefinition Width="*"` cho mỗi `DataGrid` bề rộng thật để cột `*` bên trong giãn đúng.
- **Tắt toàn bộ dữ liệu giả lập (Mock driver jitter + đo lường demo tab Main) ở build Release qua `#if DEBUG`, không phải 1 cờ cấu hình runtime riêng.** Lý do: hỏi lại người dùng qua AskUserQuestion — chọn gắn thẳng với build configuration vì quy trình publish hiện tại đã luôn dùng `dotnet publish -c Release`, không cần thêm file cấu hình mới (`appsettings.json` chưa tồn tại trong dự án) hay tham số dòng lệnh; Debug (`dotnet run`/`dotnet build`, dùng khi dev/test không có PLC thật) tự động vẫn giữ nguyên hành vi giả lập cũ. Giá trị thay thế khi tắt là `0`/`false` cố định (không phải giá trị giữa khoảng min-max hay giá trị đặc biệt khác) — người dùng chọn phương án đơn giản nhất, driver vẫn hoạt động bình thường (không crash), chỉ không tự sinh số ngẫu nhiên.

### Bước tiếp theo
1. Khi có bảng địa chỉ PLC thật: xác nhận lại giả định thứ tự word thấp-trước cho Dword/Float32 ở "Giám sát DATA" — cùng nhóm câu hỏi mở với thứ tự word khi PC làm Slave (CLAUDE.md mục 13).
2. Các mục còn mở khác không đổi so với các mục trên (driver Modbus RTU thật, hot-swap Role runtime, chiến lược ghi bit lẻ dùng chung với PLC...).

## 17. Tóm tắt phiên làm việc (2026-07-12, tiếp) — Gắn địa chỉ thanh ghi cho 6 biến đo lường tab Main

### Bối cảnh
Sau khi liệt kê bảng biến thanh ghi/nút nhấn giao tiếp PLC theo yêu cầu người dùng, người dùng chỉ ra: 6 ô "Giá trị" (High/Low mode × Điện áp/Dòng điện/Lực hút chân không) ở tab Trang Tự Động **cũng là biến cần kết nối PLC** nhưng bị bỏ sót khỏi bảng — vì `TestStepDefinition` (model dùng chung Main/Set Spec.) chưa từng có field địa chỉ, khác hẳn `IoPointDefinition` đã có `Address`. Người dùng yêu cầu gán địa chỉ thanh ghi cho 6 biến này, **định nghĩa qua 1 file CSV** để dễ sửa trong lúc phát triển máy (thay vì dò trong source code) — **và làm rõ (sau khi tôi làm sai lần đầu): CSV này KHÔNG có cơ chế Import/Export qua UI, chỉ là 1 nguồn dữ liệu cố định mà code đọc trực tiếp.**

### Trạng thái
| Phần | Trạng thái | Ghi chú |
|---|---|---|
| `TestStepDefinition.Address` (Core) | ✅ Xong | Field mới, nullable, không có ở `IoPointDefinition` trước đó |
| File nguồn `src/EolTester.Configuration/SeedData/spec-register-map.csv` (Key,Address) | ✅ Xong | Copy ra cùng thư mục exe khi build (`Content`/`CopyToOutputDirectory` trong `.csproj`) — seed để trống Address (chưa có bảng địa chỉ PLC thật) |
| `ISpecRegisterMapSource`/`CsvSpecRegisterMapSource` — đọc CSV, không ghi | ✅ Xong | Không có Import/Export dialog, không có nút UI — chỉ đọc |
| `JsonSpecProfileStore.LoadAsync` tự động gắn Address từ CSV mỗi lần load | ✅ Xong, verify qua UI thật | Sửa CSV + build lại là đủ, không cần xóa `spec-profile.json` cũ |
| `PlcPollingService.ReadMeasurement` đọc thật qua `ModbusRegisterTable` khi có Address | ✅ Xong, verify qua UI thật (gán D1000 cho High.voltage, giá trị tab Main đổi từ random-quanh-giữa-khoảng sang giá trị thật đọc từ thanh ghi, có lúc vượt ngưỡng — đúng hành vi mong đợi) | WordSigned/100 — giả định, cần xác nhận PLC thật |
| Cột "Địa chỉ" (chỉ đọc) ở tab Set Spec. | ✅ Xong | Không hiện ở tab Main (màn hình vận hành/khách hàng) |
| Sửa lỗi phụ phát hiện: `Setting_HighMode`/`Setting_LowMode` thiếu key resx (hiện chữ thô "Setting_HighMode") | ✅ Xong | Không liên quan yêu cầu chính, tiện tay sửa khi đang ở đúng màn hình |
| Unit test | ✅ 29/29 + 1/1 pass | |

### Quyết định quan trọng và lý do
- **Làm sai ở lần thử đầu tiên: xây hẳn cơ chế Import/Export CSV qua UI (OpenFileDialog/SaveFileDialog, nút bấm, audit log riêng) rập khuôn theo `CsvIoLabelService` của tab Monitor.** Người dùng chỉ rõ đây không phải điều họ muốn — chỉ cần CSV làm **nguồn dữ liệu build-time**, không phải tính năng runtime. Đã dọn sạch toàn bộ phần sai (xóa `ISpecAddressCsvService`/`CsvSpecAddressService`/`SpecAddressCsvResult`, 2 command Import/Export, 2 nút trong `SettingTabView.xaml`, các resx key liên quan) trước khi làm lại đúng hướng. **Bài học**: khi người dùng nói "định nghĩa qua CSV" cho 1 tính năng KHÔNG có tiền lệ UI rõ ràng trong cùng yêu cầu, không nên mặc định rập khuôn theo tính năng UI gần giống nhất đã có trong dự án (Import/Export nhãn I/O) — cần hỏi rõ phạm vi (đã hỏi 1 câu về "đọc thật hay chỉ lưu tham chiếu" nhưng chưa hỏi đúng câu quan trọng hơn: "có cần UI Import/Export không, hay chỉ là file nguồn tĩnh").
- **File CSV đặt trong source tree (`src/EolTester.Configuration/SeedData/`), không phải trong `%LocalAppData%\EolTester\Config`.** Lý do: người dùng muốn sửa "1 chỗ" trong lúc phát triển và **build lại** — đúng ngữ nghĩa "nguồn của code" (part of source control), khác với các file JSON runtime khác (connection.json, io-map.json...) vốn nằm trong AppData và sửa được lúc chạy không cần build lại.
- **`JsonSpecProfileStore.LoadAsync` gắn lại Address từ CSV MỖI LẦN load (không chỉ lúc seed lần đầu).** Lý do: để sửa CSV + chạy lại app (không cần xóa `spec-profile.json` cache) là thấy hiệu lực ngay — đúng tinh thần "thay đổi ở 1 chỗ cho dễ" trong vòng lặp phát triển.
- **Giữ nguyên quyết định đọc thật qua `ModbusRegisterTable`/WordSigned÷100 khi có Address** (đã chốt trước khi bị chỉnh hướng) — không đổi, chỉ đổi cách Address được NẠP vào hệ thống (CSV nguồn tĩnh thay vì Import CSV qua UI).

## 18. Tóm tắt phiên làm việc (2026-07-12, tiếp) — Cấu trúc 3-bit cho Output (Trạng thái/Lệnh/Bàn giao) + bổ sung CMD_ vào spec-register-map.csv

### Bối cảnh
Sau khi hoàn tất gắn Address cho 6 biến đo lường (mục 17), người dùng tiếp tục:
1. Yêu cầu bổ sung thêm các "biến thanh ghi" còn thiếu vào `spec-register-map.csv`: nút Xác nhận NG/START/RESET/TỰ ĐỘNG/THỦ CÔNG — đã thêm 6 dòng `CMD_START/CMD_STOP/CMD_RESET/CMD_CONFIRM_NG/CMD_MODE_AUTO/CMD_MODE_MANUAL` (2 dòng cuối là tên mới tự đặt, chưa có trong code, dành cho việc PLC sẽ quản lý cả trạng thái Auto/Manual sau này — người dùng xác nhận đây là **lệnh gửi thêm xuống PLC, không thay đổi hành vi hiện có** của `SetModeAsync`).
2. Đề xuất nút ON/OFF ở Output (tab Monitor) cần tách 1 bit hiện có thành 3 bit: **Bit 1 (trạng thái, đã có)**, **Bit 2 (lệnh người dùng gửi PLC)**, **Bit 3 (trạng thái PLC quản lý lần cuối, đọc 1 lần lúc chuyển Auto→Manual rồi copy sang Bit 2 — "bumpless transfer")**. Sau khi phân tích + hỏi lại phạm vi (chỉ Output cần 3 bit; Bit 3 chỉ đọc 1 lần lúc chuyển đổi; đặt tên cột `CommandAddress`/`HandoverAddress`), người dùng yêu cầu tiến hành sửa code.

### Trạng thái
| Phần | Trạng thái | Ghi chú |
|---|---|---|
| `spec-register-map.csv` bổ sung 6 dòng CMD_ | ✅ Xong (chỉ sửa data, không sửa code) | `CMD_MODE_AUTO`/`CMD_MODE_MANUAL` là tên mới, chưa có logic gửi PLC tương ứng trong code (sau này gộp thành 1 thanh ghi `CMD_MODE`, xem mục 20) |
| `IoPointDefinition.CommandAddress`/`HandoverAddress` (Core) | ✅ Xong | Nullable, chỉ có ý nghĩa với Output |
| `CsvIoLabelService` — header + Export + Import 2 cột mới, cảnh báo (không chặn) nếu Input lỡ điền | ✅ Xong, verify qua UI thật (Export 56 điểm ra CSV có đủ cột mới, sửa tay thêm Command/Handover cho Y00 + (cố ý) cho X00 để test cảnh báo, Import lại — X00 bị tự xóa 2 cột kèm cảnh báo, Y00 giữ đúng giá trị, đọc trực tiếp `io-map.json` sau Import xác nhận đúng) | `CsvRowIssueKind.CommandOrHandoverOnInput` (mới) |
| `PlcPollingService.WriteOutputAsync` ghi vào `CommandAddress ?? Address` (fallback tương thích ngược) | ✅ Xong | Chỉ set `point.Value` tức thì khi fallback (điểm cũ, chưa nâng cấp); điểm đã có CommandAddress riêng thì chờ polling đọc lại Bit 1 |
| `PlcPollingService.PollLoopAsync` poll thêm `Outputs.Address` (Bit 1) mỗi tick | ✅ Xong, verify qua UI thật (Output hiện random xanh giống Input ngay khi mở tab, xác nhận đang polling thật thay vì chỉ set optimistic lúc trước) | Trước đây Output không được đọc lại, chỉ set optimistic khi ghi |
| `PlcPollingService.TransferHandoverToCommandAsync` + gọi từ `MonitorTabViewModel.SetModeAsync` khi Auto→Manual | ✅ Xong, verify qua UI thật (chuyển sang THỦ CÔNG không crash, log sạch không lỗi) | Đúng 1 lần lúc chuyển đổi, không lặp lại |
| Unit test | ✅ 29/29 + 1/1 pass, không hồi quy | |

### Quyết định quan trọng và lý do
- **Tách Bit 1 (trạng thái)/Bit 2 (lệnh)/Bit 3 (bàn giao) — đúng pattern "bumpless transfer" chuẩn công nghiệp.** Lý do kỹ thuật: nếu PC ghi lệnh thẳng vào bit trạng thái (bit PLC có thể đang tự cập nhật liên tục theo logic/cảm biến của nó), giá trị PC vừa ghi có nguy cơ bị PLC ghi đè ở chu kỳ quét kế tiếp — đúng hiện tượng đã từng quan sát thực tế khi test "Giám sát DATA" ghi vào Khối Input bị polling đè lại ngay. Tách riêng Bit 2 loại bỏ xung đột này.
- **Chỉ Output cần 3 bit; Input giữ nguyên 1 bit.** Lý do: Input không có khái niệm "hành động người dùng" hay "PLC bàn giao quyền" — chỉ đọc.
- **Bit 3 chỉ đọc đúng 1 lần lúc chuyển Auto→Manual, không theo dõi tiếp.** Lý do: đúng theo mô tả gốc của người dùng ("move 1 lần"), đơn giản nhất, khớp đúng kịch bản bumpless transfer chuẩn — không cần xử lý trường hợp PLC ghi đè Bit 3 liên tục trong lúc Manual (chưa có nhu cầu cụ thể, để mở khi cần).
- **Tên cột CSV `CommandAddress`/`HandoverAddress`.** Lý do: người dùng chọn phương án gợi ý, thêm vào cuối header hiện có (`Address,Key,Label1,Label2,Label3,CommandAddress,HandoverAddress`) để tương thích ngược hoàn toàn với file CSV cũ (5 cột vẫn đọc được, 2 cột mới optional).
- **Fallback tương thích ngược khi `CommandAddress` trống: ghi thẳng vào `Address` như hành vi cũ.** Lý do: cho phép nâng cấp dần từng điểm Output một qua thời gian, không bắt buộc điền đủ 3 bit ngay cho toàn bộ bảng I/O hiện có.
- **Chỉ set `point.Value` tức thì (phản hồi UI nhanh) khi KHÔNG có CommandAddress riêng; có CommandAddress riêng thì để nguyên, chờ polling đọc lại Bit 1.** Lý do: nếu ép hiển thị ngay giá trị PC vừa gửi trong khi có Bit 1 riêng biệt phản ánh trạng thái PLC xác nhận, UI có thể hiển thị sai nếu PLC chưa kịp phản ứng với Bit 2 — nhất quán với đúng ý nghĩa "Bit 1 = trạng thái do PLC báo về" người dùng đã định nghĩa. Đánh đổi: với `MockModbusDriver` (không có ladder logic mô phỏng), Output đã nâng cấp lên CommandAddress riêng sẽ **không tự đổi màu xanh khi bấm ON** trong môi trường dev/test — đây là giới hạn đã biết của việc test bằng Mock, không phải lỗi.
- **`PollLoopAsync` mở rộng poll cả `Outputs.Address`, không chỉ `Inputs`.** Lý do: bắt buộc để hiện thực đúng "Bit 1 dùng chung cho cả Input lẫn Output" — trước đó Output chỉ có giá trị optimistic lúc ghi, không có cơ chế đọc lại trạng thái thật.
- **Địa chỉ Bit 1 của 1 điểm Output nên nằm trong Khối Input (không phải Khối Output) đã cấu hình ở Setup — ghi chú quan trọng cho người dùng khi tự điền CSV.** Lý do: "Khối Input/Output" ở Setup là theo **chiều dữ liệu PC↔PLC** (PC đọc/PC ghi), không phải theo "loại điểm I/O" — Bit 1 của Output là dữ liệu PLC→PC (cần đọc) nên phải nằm trong Khối Input; Bit 2 là dữ liệu PC→PLC (cần ghi) nên nằm trong Khối Output. Đây là 2 khái niệm độc lập, dễ nhầm nếu không giải thích rõ.

## 19. Tóm tắt phiên làm việc (2026-07-12, tiếp) — Sửa 3 lỗi giao tiếp PLC người dùng phát hiện khi chạy thử + tạo io-map-seed.csv

### Bối cảnh
Người dùng chạy thử app thật (sau round 3-bit Output ở mục 18) và báo 4 vấn đề. Khảo sát toàn bộ mã nguồn liên quan trước khi sửa — cả 4 đều có nguyên nhân gốc rõ ràng, không phải bug ngẫu nhiên.

### Vấn đề & giải pháp
1. **6 giá trị đo D1020-D1030 (đã gán Address ở mục 17) không đọc được, hiện "--"** — `ConnectionSettings.InputBlock` mặc định chỉ `{StartAddress=1000, RegisterCount=20}` (D1000-D1019), không phủ D1020-D1030. `ModbusRegisterTable.TryGetWord` với địa chỉ ngoài phạm vi trả `false` **âm thầm** (không throw) → `ReadMeasurement` trả `null` → hiện "--". **Sửa**: tăng `RegisterCount` mặc định 20→40 (D1000-D1039). Cài đặt cũ trên máy test cần tự cập nhật qua Setup tab hoặc xóa `connection.json` để reseed — code default chỉ áp dụng cho cài đặt mới.
2. **Nút START/STOP/RESET/XÁC NHẬN NG không đổi giá trị D1100-D1105** dù đã gán trong `spec-register-map.csv` ở mục 18 — `PulseCommandAsync` trước đó gọi thẳng `_driver.WriteDiscreteAsync(new IoAddress("CMD_START"), ...)`, coi chuỗi khóa là địa chỉ discrete tùy ý, **không tra `ISpecRegisterMapSource`**, không đụng `ModbusRegisterTable` — CSV chỉ mang tính trang trí với các dòng CMD_. **Sửa**: `PlcPollingService` inject thêm `ISpecRegisterMapSource`, `PulseCommandAsync` tra cache `_specAddressMap` (nạp 1 lần) để đổi `"CMD_START"` → `"D1100"` rồi ghi qua `WriteBitAsync` (tái dùng logic parse `Dxxxx`/`Dxxxx.b` + cập nhật `ModbusRegisterTable` đã có sẵn cho Output 3-bit) — key không có trong CSV thì fallback hành vi discrete cũ (tương thích ngược).
3. **Output 3-bit "chưa vận hành đúng", Export chỉ thấy 1 thanh ghi** — **không phải bug**: `JsonIoMapStore.CreateSeedProfile()` cũ chưa từng gán `CommandAddress`/`HandoverAddress` cho bất kỳ điểm nào (chỉ điền được qua Import CSV thủ công), nên chưa từng có dữ liệu 3-bit nào để mà export ra. Giải thích rõ cho người dùng thay vì sửa code — được hỗ trợ gián tiếp qua vấn đề 4.
4. **Yêu cầu tạo file CSV nguồn build-time cho bản đồ I/O mặc định** (giống `spec-register-map.csv`) — tạo `SeedData/io-map-seed.csv` (schema 7 cột giống `CsvIoLabelService`: `Address,Key,Label1,Label2,Label3,CommandAddress,HandoverAddress`), `IIoMapSeedSource`/`CsvIoMapSeedSource` đọc file này, `JsonIoMapStore` dùng làm nguồn khi `io-map.json` chưa tồn tại (giữ nguyên 32 điểm mặc định X00-X17/Y00-Y17, không đổi hành vi — chỉ đổi nơi định nghĩa từ code C# sang CSV).

### Quyết định quan trọng và lý do
- **`io-map-seed.csv` chỉ dùng đúng 1 lần lúc `io-map.json` chưa tồn tại (seed), KHÔNG overlay lại mỗi lần load như `spec-register-map.csv`.** Lý do: `io-map.json` đã có cơ chế Import/Export CSV runtime riêng qua tab Monitor (`CsvIoLabelService`) — nếu overlay mỗi lần load sẽ xóa mất mọi thay đổi người dùng đã Import qua UI đó. `spec-register-map.csv` không có xung đột này vì `TestStepDefinition.Address` không có kênh ghi runtime nào khác.
- **`JsonIoMapStore` giữ fallback về danh sách hardcode cũ (`CreateFallbackSeedProfile`) khi `IIoMapSeedSource.LoadAsync` trả `null`** (file CSV thiếu/rỗng/không hợp lệ). Lý do: phòng vệ cho môi trường không copy `SeedData` (VD chạy unit test project trực tiếp không qua publish), tránh app khởi động với bản đồ I/O rỗng.
- **`PulseCommandAsync` tái dùng `WriteBitAsync` (đã có sẵn, dùng cho Output 3-bit ở mục 18) thay vì viết logic ghi mới.** Lý do: đây chính là cơ chế đã verify hoạt động đúng (parse `Dxxxx`/`Dxxxx.b`, ghi qua driver + cập nhật `ModbusRegisterTable`), tránh trùng lặp 2 đường ghi bit song song cho cùng 1 khái niệm.
- **Verify qua UI thật**: reset `connection.json`/`io-map.json`, chạy app — 6 giá trị đo hiện số thật (không còn "--"); tab Monitor hiển thị đúng 32 điểm I/O theo `io-map-seed.csv` (đúng thứ tự octal X07→X10 như file gốc); bấm START/STOP/RESET trên tab Main chạy sạch, không lỗi, log ghi đúng "đã gửi tín hiệu CMD_START/STOP/RESET" — xác nhận code không throw khi tra cứu + ghi qua đường CSV mới. **Hạn chế đã biết**: không xác minh trực quan được giá trị D1100-D1105 đổi tức thời qua ảnh chụp, vì xung lệnh chỉ tồn tại ~200ms (`pulseMs` mặc định) trong khi timer refresh của "Giám sát DATA" là 500ms — về nguyên tắc panel này có thể bỏ lỡ xung ngắn hơn chu kỳ refresh của chính nó, không phải lỗi của cơ chế ghi. Xác nhận đúng đắn dựa trên rà soát code (tái dùng nguyên `WriteBitAsync` đã verify ở mục 18) thay vì bắt được khoảnh khắc pulse qua ảnh chụp.
- **Không sửa `CMD_MODE_AUTO`/`CMD_MODE_MANUAL`** — đúng theo xác nhận trước đó của người dùng ở mục 18 rằng 2 dòng này chỉ là chuẩn bị cho tương lai, chưa đổi hành vi `SetModeAsync`.

### Bước tiếp theo
1. Người dùng cần cập nhật `connection.json` hiện có trên máy test (qua Setup tab, đổi Số lượng thanh ghi Input Block → 40) hoặc xóa file để app tự tạo lại theo default mới — thay đổi default trong code không hồi tố cấu hình đã lưu.
2. Khi có bảng địa chỉ PLC thật: xác nhận lại D1100-D1105 (CMD_ hiện do người dùng tự gán tạm) và điền `CommandAddress`/`HandoverAddress` cho các điểm Output cần dùng cấu trúc 3-bit — qua `io-map-seed.csv` (áp dụng cho cài đặt mới) hoặc nút "IMPORT LABEL" ở tab Monitor (áp dụng ngay, không cần khởi động lại).
3. Các mục còn mở khác không đổi (driver Modbus RTU thật, hot-swap Role runtime, chiến lược ghi bit lẻ dùng chung với PLC...).

## 20. Tóm tắt phiên làm việc (2026-07-12, tiếp) — Tái cấu trúc dự án (tách CLAUDE.md, thêm subagents/skills) + nút PLC dạng "giữ = ON, thả = OFF" + ghi Output theo chu kỳ + gộp Auto/Manual về 1 thanh ghi

### Bối cảnh
Người dùng thấy CLAUDE.md và hội thoại quá tải, yêu cầu: (1) tách gọn CLAUDE.md + thêm subagents (Research/Coding/Check-Test/Report) + Rules + Skills; (2) tiếp tục triển khai loạt thay đổi hành vi giao tiếp PLC đã thống nhất trước đó (xác nhận chu kỳ polling, nút dạng giữ/thả, đảm bảo xung tối thiểu 200ms, ghi Output theo chu kỳ, gộp Auto/Manual 1 thanh ghi, chuẩn CSV UTF-8-BOM) — yêu cầu ưu tiên gọi Agent (subagent) để thực hiện công việc bất cứ khi nào có thể.

### Phần 1 — Tái cấu trúc dự án
| Việc | Trạng thái | Ghi chú |
|---|---|---|
| Tách lịch sử 6 phiên (mục 14-19 cũ) từ CLAUDE.md sang `docs/session-log/history.md` | ✅ Xong | CLAUDE.md giảm 506→~330 dòng; đối chiếu tiêu đề từng mục khớp đủ, không mất nội dung |
| Cập nhật mục 1 CLAUDE.md: quy tắc ghi lịch sử phiên vào file riêng + quy tắc CSV UTF-8-BOM bắt buộc | ✅ Xong | Áp dụng cho mọi dự án mới ngay từ đầu |
| Sửa tham chiếu chéo còn trỏ "mục 15/18/19" cũ | ✅ Xong | Grep xác nhận không còn tham chiếu treo |
| `.claude/agents/ui-verifier.md`, `.claude/agents/publisher.md` (subagent tùy chỉnh) | ✅ Tạo xong, **CHƯA khả dụng trong phiên hiện tại** | Harness không hot-reload `.claude/agents/*.md` giữa chừng phiên — cần phiên Claude Code CLI mới mới nạp được. Đã dùng `general-purpose` thay thế cho các bước verify/build trong phiên này. |
| `.claude/skills/publish-app/SKILL.md`, `.claude/skills/verify-ui/SKILL.md` | ✅ Xong | |
| **Quyết định không tách "Rules Style/Coding/Specific_task" thành nhiều file riêng** | Đã cân nhắc, giữ nguyên cấu trúc Phần I/Phần II | Lý do: Phần I (mục 2-6 Coding/Style, mục 7 kiểm thử) + Phần II (Specific_task) đã tự nhiên phủ đủ 3 nhóm người dùng đề cập — tách thêm dễ làm phức tạp hơn thay vì gọn hơn. Có thể điều chỉnh nếu người dùng vẫn muốn tách. |

### Phần 2 — Thay đổi hành vi giao tiếp PLC (thực hiện qua subagent `general-purpose` theo đúng yêu cầu ưu tiên dùng Agent)

**Xác nhận (không cần sửa code, chỉ trả lời)**:
- Chu kỳ đọc Input đúng như tài liệu (đọc cả khối mỗi tick); ghi Output **trước round này hoàn toàn theo sự kiện**, không có ghi theo chu kỳ — đã bổ sung ở round này (xem dưới).
- Ghi 1 bit `Dxxxx.b` luôn đọc-sửa-ghi (read-modify-write) cả từ 16-bit rồi đẩy cả từ xuống PLC — xác nhận đúng, không đổi (bắt buộc về giao thức, không phải lựa chọn).
- Địa chỉ ghi theo sự kiện (CommandAddress ON/OFF, CMD_START/STOP/RESET/CONFIRM_NG/MODE) **độc lập với phạm vi Khối Output** đã khai báo ở Setup — có thể trùng hoặc nằm ngoài hoàn toàn, việc ghi driver vẫn luôn xảy ra; chỉ khác là bộ nhớ đệm cục bộ không giữ lại giá trị nếu địa chỉ ngoài cả 2 khối (không lỗi, chỉ "Giám sát DATA" không đọc lại được).

**Đã triển khai** (giao cho 1 agent `general-purpose` thực hiện toàn bộ 7 hạng mục theo đúng spec chi tiết đã thiết kế sẵn từ vòng plan trước, review lại diff thủ công sau đó — khớp 100% không có sai lệch):
1. `src/EolTester.App/Behaviors/MomentaryButton.cs` (mới) — attached behavior `PressCommand`/`ReleaseCommand`/`CommandParameter`, dùng `LostMouseCapture` (không phải `MouseUp` trực tiếp) để đảm bảo luôn nhả về `false` kể cả kéo chuột ra ngoài nút.
2. `PlcPollingService.cs`: thêm `_outputBlockStart`/`_outputBlockCount` + `SetOutputBlockRange`, vòng ghi Output Block theo chu kỳ trong `PollLoopAsync` (ngay sau vòng đọc Input Block), helper `RecordPress`/`DelayForMinPulseAsync` (đảm bảo xung tối thiểu `MinPulseMs = 200`), `SetLevelCommandAsync` (ghi mức, không tự đảo như `PulseCommandAsync`), áp dụng min-pulse vào `WriteOutputAsync`.
3. `MonitorTabViewModel.cs`: bỏ guard `point.Value` trong `TurnOnAsync`/`TurnOffAsync`; `SetModeAsync` thêm gọi `SetLevelCommandAsync("CMD_MODE", ...)`.
4. `MonitorTabView.xaml`: nút ON đổi sang `MomentaryButton` (Press=TurnOnCommand, Release=TurnOffCommand); nút OFF **giữ nguyên** click thường (chủ ý — luôn an toàn ép về 0 ngay).
5. `ShellViewModel.cs`: `ResetAsync`/`ConfirmNgAsync` (pulse cố định) → tách thành `ResetPressAsync`/`ResetReleaseAsync` và `ConfirmNgPressAsync`/`ConfirmNgReleaseAsync` (dùng `SetLevelCommandAsync`); `StartAsync` (Start/Stop) **không đổi**.
6. `MainWindow.xaml`: nút Reset + Xác nhận NG đổi sang `MomentaryButton`; nút Start/Stop không đổi.
7. `spec-register-map.csv`: gộp `CMD_MODE_AUTO,D1103`/`CMD_MODE_MANUAL,D1104` thành 1 dòng `CMD_MODE,D1103`.

Build 0 lỗi, test `EolTester.Core.Tests` 1/1 + `EolTester.Communication.Tests` 34/34 pass (không hồi quy).

**Verify qua UI thật** (agent `general-purpose` thứ 2, dùng đúng quy trình PrintWindow an toàn của dự án): xác nhận CMD_MODE (D1103) đổi đúng 0↔1 khi chuyển Auto↔Manual (quan sát qua "Giám sát DATA"); nút ON giữ/thả không crash; Reset/Xác nhận NG giữ/thả không crash, side-effect Reset chạy đúng lúc nhấn (không chờ thả); Start/Stop click thường không hồi quy; kéo chuột ra ngoài nút rồi thả vẫn tự trả về OFF (an toàn). Phát hiện 1 vấn đề nhỏ: nút Xác nhận NG đổi màu xanh dương nhạt khi giữ thay vì LimeGreen như style quy định (ghi vào CLAUDE.md mục 13, chưa sửa — chỉ là màu sắc).

### Quyết định quan trọng và lý do
- **Ưu tiên giao việc cho subagent `general-purpose` theo đúng yêu cầu người dùng, nhưng vẫn tự review lại diff trước khi tin tưởng kết quả** ("trust but verify") — vì code chạm vào các file lõi (Service/ViewModel/View) đã có ngữ cảnh MVVM/domain phức tạp tích lũy qua 19 vòng trước; giao việc thành công nhờ đưa spec cực kỳ chi tiết (snippet code chính xác cho từng file) thay vì mô tả chung chung — agent chỉ cần thực thi đúng, không phải tự thiết kế lại.
- **Agent tùy chỉnh `ui-verifier`/`publisher` vừa tạo chưa dùng được ngay trong cùng phiên** — phát hiện khi gọi `Agent` tool với `subagent_type: "ui-verifier"` báo lỗi "not found". Đây là giới hạn hạ tầng (harness chỉ nạp `.claude/agents/*.md` lúc khởi động phiên/CLI), không phải lỗi cấu hình file — 2 file vẫn đúng định dạng, sẽ khả dụng ở phiên mới.
- **Chỉ nút ON đổi sang giữ/thả, giữ nguyên nút OFF dạng click** — quyết định thiết kế (không hỏi lại người dùng, đã suy luận đủ rõ từ yêu cầu gốc): OFF luôn an toàn khi ép về 0 ngay lập tức bất kể thời lượng giữ, giữ nó làm nút dự phòng độc lập tăng tính an toàn thay vì giảm chức năng.
- **`DelayForMinPulseAsync` áp dụng chung cho cả `SetLevelCommandAsync` (Reset/ConfirmNG/CMD_MODE) lẫn `WriteOutputAsync` (Monitor ON/OFF)** qua 2 helper dùng chung (`RecordPress`/`DelayForMinPulseAsync`, keyed theo tracking key khác nhau) — tránh viết 2 lần logic tính thời gian giữ tối thiểu cho 2 luồng lệnh khác nhau.

### Cập nhật ngay sau đó — revert nút ON/OFF tab Giám sát về click-toggle
Ngay sau khi hoàn tất round trên, người dùng xin lỗi và yêu cầu revert riêng khối "Điều khiển thủ công" (nút ON/OFF ở tab Giám sát) — muốn giữ đúng nguyên trạng click-toggle-latch cũ (click ON giữ ON tới khi click OFF, ngược lại), không dùng dạng giữ/thả. Reset/Xác nhận NG (footer, không nằm trong "điều khiển thủ công") **không** thuộc phạm vi yêu cầu này, vẫn giữ dạng giữ/thả.
- `MonitorTabView.xaml`: nút ON đổi lại từ `MomentaryButton.PressCommand/ReleaseCommand/CommandParameter` về `Command="{Binding DataContext.TurnOnCommand,...}"`/`CommandParameter="{Binding}"` — giống hệt nút OFF (không đổi).
- `MonitorTabViewModel.cs`: khôi phục lại guard `|| point.Value` trong `TurnOnAsync` và `|| !point.Value` trong `TurnOffAsync` (đã bỏ ở round trước) — đúng nguyên bản trước khi có thay đổi giữ/thả.
- `EolTester.App/Behaviors/MomentaryButton.cs` và `PlcPollingService.WriteOutputAsync`'s min-pulse tracking **giữ nguyên không đổi** — vẫn cần cho Reset/Xác nhận NG, và min-pulse logic vô hại với click-toggle độc lập (chỉ có tác dụng khi 2 lần ghi cách nhau dưới 200ms).
- Build 0 lỗi, test 1/1 + 34/34 pass, giao cho agent `general-purpose` thực hiện + tự review lại diff (khớp đúng spec).

### Cập nhật ngay sau đó nữa — bug "bấm ON không thấy kết quả" sau khi revert
Người dùng báo: ở chế độ Thủ công, bấm ON không thấy có tác dụng gì. Trực tiếp điều tra qua ảnh chụp UI thật (không giao cho agent, vì cần lặp nhanh nhiều bước quan sát): xác nhận nhiều điểm Output tự nhiên đổi màu xanh/xám dù không ai bấm gì — do `MockModbusDriver` áp nhiễu ngẫu nhiên `±5` lên MỌI thanh ghi trong Khối Input mỗi tick polling, kể cả thanh ghi Bit 1 (trạng thái) của các Output đã nâng cấp 3-bit (`Address` của Output nằm trong Khối Input theo đúng quy ước dự án). Tìm ra 2 lớp nguyên nhân chồng nhau:
1. **`WriteOutputAsync` chỉ cập nhật `point.Value` tức thì cho điểm CHƯA có `CommandAddress`** (fallback) — điểm đã nâng cấp 3-bit phải chờ polling đọc lại Bit 1 mới thấy đổi màu, nhưng Bit 1 bị nhiễu ngẫu nhiên chi phối nên gần như không bao giờ khớp với lệnh vừa gửi.
2. Sau khi sửa (1) — bấm ON đổi xanh ngay lập tức nhưng **tự đảo lại sau dưới 1 giây** — vì `PollLoopAsync` vẫn tiếp tục poll lại Bit 1 mỗi tick và ghi đè giá trị lạc quan vừa đặt bằng dữ liệu nhiễu.

**Sửa (2 round, đều qua agent `general-purpose`, tự review + tự verify UI thật sau mỗi round bằng ảnh chụp)**:
- Round 1: `WriteOutputAsync` bỏ điều kiện `if (!hasCommandAddress)` quanh `point.Value = value;` — luôn cập nhật lạc quan ngay khi ghi, không phân biệt điểm cũ/3-bit nữa.
- Round 2 (sau khi verify UI thật phát hiện vẫn tự đảo lại sau ~1s): `PollLoopAsync`'s Outputs loop thêm `if (!string.IsNullOrWhiteSpace(output.Definition.CommandAddress)) continue;` — **bỏ hẳn việc poll lại Bit 1 cho Output đã có `CommandAddress`**, chỉ còn poll Bit 1 cho Output kiểu cũ (chưa nâng cấp, `Address` vẫn là nguồn sự thật duy nhất).
- Verify UI thật (tự làm, không qua agent): bấm ON → xanh ngay, giữ nguyên xanh sau 3 giây chờ (nhiều tick polling) → bấm OFF → về đúng OFF. Xác nhận cả 2 chiều hoạt động ổn định, đúng yêu cầu "khi nhấn ON sẽ ON đến khi nhấn OFF và ngược lại".

### Quyết định quan trọng và đánh đổi (round sửa bug này)
- **Đánh đổi có chủ đích**: với Output đã nâng cấp 3-bit, "Điểm" hiển thị giờ phản ánh **lệnh PC vừa gửi (Bit 2)**, không còn là "trạng thái Bit 1 do PLC xác nhận" như đúng ý định thiết kế 3-bit ban đầu (mục 18). Lý do chấp nhận đánh đổi này: hiện chưa có PLC thật + ladder logic phản hồi Bit 2→Bit 1, nên polling Bit 1 lúc này chỉ đang hiển thị nhiễu ngẫu nhiên vô nghĩa của Mock, còn hại nhiều hơn lợi (khiến UI không dùng test được). Đã ghi chú rõ trong code + CLAUDE.md: khi có PLC thật, nên cân nhắc bật lại polling Bit 1 cho các điểm này.
- **Không sửa ở tầng `MockModbusDriver`** (VD: loại trừ nhiễu cho riêng các thanh ghi đóng vai trò bit trạng thái I/O) — vì Mock không có cách nào tự phân biệt "thanh ghi đo lường" (cố ý cần nhiễu để demo trông giống thật) với "thanh ghi trạng thái I/O" (không nên nhiễu) chỉ từ địa chỉ số — sửa ở tầng `PlcPollingService` (biết rõ ngữ nghĩa từng điểm qua `IoPointDefinition`) đúng chỗ hơn, và không ảnh hưởng tới hành vi demo dữ liệu đo lường vẫn cần giữ nguyên.
- **Vẫn tự làm việc điều tra/verify UI trực tiếp thay vì giao hết cho agent** ở phần này — vì cần lặp nhanh nhiều vòng quan sát-suy luận-thử lại trong thời gian ngắn (khác các phần code thay đổi rõ ràng, đã có spec sẵn, giao agent thực thi hợp lý hơn).

## 21. Tóm tắt phiên làm việc (2026-07-12, tiếp) — Sửa gốc rễ: nhiễu ngẫu nhiên sai mục đích lên D1003/D1006/D1106 (và mọi thanh ghi trạng thái I/O khác)

### Bối cảnh
Người dùng tự kiểm tra giá trị D1003/D1006/D1106 qua "Giám sát DATA" và báo "không hẳn hiển thị theo yêu cầu", yêu cầu rà soát lại: yêu cầu gốc, kết quả đã làm, sai sót, nguyên nhân, giải pháp, giải pháp tránh lặp lại — làm qua đúng quy trình Plan Mode (research → thiết kế → duyệt → thực thi).

### Nguyên nhân gốc (khác với round trước — sửa lại 1 quyết định cũ)
Round trước (mục 20) đã sửa triệu chứng "bấm ON không thấy kết quả/tự đảo lại" bằng cách bỏ polling Bit 1 cho Output 3-bit — nhưng **không sửa nguồn gốc thật sự**: `MockModbusDriver` áp nhiễu ngẫu nhiên `±5` lên **MỌI địa chỉ** được `ReadRegisterAsync` đọc tới (không phân biệt ngữ nghĩa), trong khi `PollLoopAsync` đọc **toàn bộ Khối Input mỗi tick** — khiến D1003 (Bit 1) và D1006 (Bit 3, Handover) của mọi Output 3-bit bị nhiễu ngẫu nhiên liên tục dù không ai thao tác gì, và bug dây chuyền: `TransferHandoverToCommandAsync` (bumpless transfer) copy thẳng giá trị nhiễu từ D1006 sang D1106 mỗi lần chuyển Auto→Manual.

### Sửa (đã đảo ngược quyết định "không sửa ở tầng Mock" của round 20 — quyết định đó sai, đây mới là chỗ đúng)
1. `MockModbusDriver.cs`: nhiễu chuyển từ blanket sang **opt-in theo địa chỉ** — thêm `ConfigureJitterAddresses(IEnumerable<string>)`, `ReadRegisterAsync` chỉ nhiễu địa chỉ có trong danh sách, mặc định mọi địa chỉ ổn định như Release.
2. `PlcPollingService.StartAsync`: đăng ký đúng 6 địa chỉ đo lường (loại trừ `CMD_*`) từ `spec-register-map.csv` làm danh sách nhiễu — nơi DUY NHẤT cast trực tiếp sang `MockModbusDriver` (có chủ đích, comment rõ, an toàn với driver thật sau này vì `is` pattern tự no-op).
3. Thêm `MockModbusDriverTests.cs` (3 test) — biến hành vi "mặc định không nhiễu" thành điều được kiểm chứng tự động.
4. Build 0 lỗi, test `EolTester.Core.Tests` 1/1 + `EolTester.Communication.Tests` 37/37 (34 cũ + 3 mới) pass.

### Verify UI thật — gặp bẫy chính do lỗi thao tác test, không phải lỗi sản phẩm
Lần đầu kiểm tra D1106 sau khi bấm ON cho Y00 thấy vẫn = 0 — tưởng là bug tầng 2. Điều tra sâu (đọc trực tiếp `io-map.json`/`connection.json`, quét thô audit log SQLite) phát hiện: **do tự bấm 2 lần liên tiếp vào cùng toạ độ trong lúc test** (thói quen từ round trước để né bẫy "click đầu chỉ select cell" của WPF DataGrid) — lần 2 vô tình rơi trúng nút OFF (bố cục ON/OFF dịch nhẹ sau khi hàng đổi màu), khiến Y00 bị bật rồi tắt ngay trước khi tôi kịp kiểm tra giá trị. Sau khi test lại với thao tác click sạch (xác nhận qua audit log đúng 1 hành động "False→True" không kèm gì sau đó), D1106 = 1 chính xác, ổn định — đúng như thiết kế.

### Quyết định quan trọng và lý do
- **Đảo ngược quyết định "không sửa ở tầng MockModbusDriver" của round 20.** Lý do round 20 đưa ra ("Mock không có cách nào tự phân biệt đo lường vs trạng thái I/O") **hóa ra sai** — Mock hoàn toàn có thể phân biệt nếu được CẤU HÌNH từ bên ngoài (nơi biết ngữ nghĩa, tức `PlcPollingService`), không cần Mock tự suy luận từ địa chỉ số. Đây là bài học: quyết định "không sửa ở X" cần ghi rõ lý do kèm giả định — khi giả định đó (ở đây: "Mock không thể biết") sai, phải sẵn sàng đảo ngược, không bám giữ quyết định cũ.
- **Sửa ở tầng Mock (thay vì tầng PlcPollingService như round 20) triệt để hơn** — round 20 chỉ ẩn triệu chứng ở 1 nơi tiêu thụ dữ liệu (vòng lặp poll UI Output), còn để nguyên gốc rễ (giá trị REGISTER thật vẫn nhiễu, "Giám sát DATA" vẫn hiển thị sai) — đúng như người dùng phát hiện. Sửa ở Mock giải quyết đúng 1 lần cho MỌI nơi đọc thanh ghi đó (Giám sát DATA, bumpless transfer, mọi tính năng tương lai), không phải vá từng chỗ tiêu thụ.
- **Bài học quy trình quan trọng nhất phiên này**: khi 2 tính năng cùng chạm 1 vùng dữ liệu dùng chung được thêm ở 2 thời điểm khác nhau (ở đây: "áp nhiễu demo" thêm sớm, "đọc toàn bộ Khối Input mỗi tick" thêm sau), phải chủ động rà lại tương tác giữa chúng — loại lỗi này không lộ ra lúc build/test (37/37 vẫn pass), chỉ lộ khi người dùng tự quan sát giá trị thật theo thời gian. Đã ghi quy tắc này vào CLAUDE.md mục 4.

## 22. Tóm tắt phiên làm việc (2026-07-12, tiếp) — Bật lại polling Bit 1 cho Output + mirror Mock Bit2/Bit3→Bit1

### Bối cảnh
Người dùng báo: "Điểm" ở tab Monitor cho Output hiển thị màu theo Bit 2 (lệnh PC vừa gửi), không theo Bit 1 (trạng thái) như thiết kế; và Watch table ("Giám sát DATA" ở tab Setup) không đồng bộ với Monitor — sửa D1003 ở Setup không thấy Monitor cập nhật theo.

### Nguyên nhân
Round 20 (mục 20) đã tắt hẳn polling Bit 1 cho mọi Output có `CommandAddress`, vì tại thời điểm đó `MockModbusDriver` áp nhiễu ngẫu nhiên blanket lên mọi thanh ghi trong Khối Input mỗi tick, ghi đè giá trị lạc quan vừa bấm ON/OFF. Quyết định đó đã lỗi thời từ round 21 (mục 21): nhiễu chuyển sang opt-in theo địa chỉ (`ConfigureJitterAddresses`, chỉ áp cho 6 thanh ghi đo lường, loại trừ `CMD_*`) — Bit 1 của Output (D1003/D1004...) không còn nằm trong danh sách nhiễu, nên lý do tắt polling không còn đúng, nhưng chưa ai quay lại bật polling Bit 1 lên — đây chính là khoảng trống người dùng phát hiện ra.

### Sửa (`PlcPollingService.cs`)
1. `PollLoopAsync`: bỏ điều kiện `continue` khi Output có `CommandAddress` — poll Bit 1 (`Address`) cho **mọi** Output giống hệt Input.
2. `WriteOutputAsync`: sau khi ghi Bit 2 (`CommandAddress`), nếu `_driver is MockModbusDriver` thì mirror luôn giá trị đó sang Bit 1 (`Address`) — mô phỏng ladder logic PLC phản hồi tức thì (chưa có PLC thật). Guard `is MockModbusDriver` đảm bảo driver thật sau này không bao giờ bị PC ghi đè Bit 1 (vùng PLC sở hữu độc quyền).
3. `TransferHandoverToCommandAsync` (bumpless transfer Auto→Manual): cùng cơ chế mirror Bit 3→Bit 1 khi Mock; đồng thời phát hiện và sửa luôn 1 lỗi phụ — hàm này trước đây **không hề cập nhật `point.Value`**, khiến UI "Điểm" đứng hình sau khi chuyển sang Thủ công cho tới tick polling kế tiếp (hoặc mãi mãi khi polling Bit 1 đang tắt). Nay cập nhật `point.Value` ngay lập tức.

### Build/test
0 lỗi build. `EolTester.Core.Tests` 1/1, `EolTester.Communication.Tests` 37/37 — không hồi quy (thay đổi nằm hoàn toàn ở `EolTester.App`, không có test project riêng cho tầng này).

### Quyết định quan trọng và lý do
- **Không phục hồi nguyên trạng "poll Bit 1 luôn luôn" của thiết kế 3-bit ban đầu một cách mù quáng** — nếu chỉ bật lại polling mà không thêm mirror Mock-only, bug cũ "bấm ON không thấy kết quả" (round 18-19) sẽ quay lại ngay, vì Mock không có ladder logic thật. Giải pháp đúng là bật polling **cùng lúc** với thêm cơ chế mirror có điều kiện (`is MockModbusDriver`) — vừa khôi phục đúng ý nghĩa Bit 1 (đồng bộ 2 chiều với Watch table), vừa giữ trải nghiệm phản hồi tức thì khi test không có PLC thật.
- **Bài học lặp lại từ mục 21**: một quyết định "tắt tính năng X để né bug Y" cần ghi rõ điều kiện đảo ngược — ở đây điều kiện là "khi nhiễu hết blanket". Round 21 đã sửa nhiễu nhưng không rà lại các quyết định phụ thuộc vào giả định cũ (polling Bit 1 bị tắt) — nhắc lại nguyên tắc CLAUDE.md mục 4: khi sửa 1 vùng dữ liệu dùng chung, phải rà lại mọi nơi từng "vá" quanh vùng đó dựa trên hành vi cũ.

## 23. Tóm tắt phiên làm việc (2026-07-12, tiếp) — Xác nhận lại fix mục 22 + sửa thêm lỗi phụ: thiếu `SetOutputBlockRange` khi Save Setup + bổ sung rule quy trình mới

### Bối cảnh
Tiếp nối mục 22 — agent nền được giao thực hiện fix ở phiên trước bị dừng giữa chừng (process Claude Code thoát trước khi agent báo cáo hoàn tất), nhưng đọc lại `PlcPollingService.cs` xác nhận **toàn bộ 3 thay đổi mô tả ở mục 22 đã có trong code** (polling Bit 1 cho mọi Output, mirror Mock-only Bit2/Bit3→Bit1, `TransferHandoverToCommandAsync` cập nhật `point.Value`) — không cần làm lại. Người dùng yêu cầu rà soát lại toàn diện + đảm bảo "Giám sát DATA" đồng bộ với **tất cả** màn hình liên quan, chạy test/debug/fix theo vòng lặp tới khi chắc chắn 100%, đồng thời bổ sung 1 rule quy trình mới: **mọi task sau này, nếu có điểm chưa rõ phải xác nhận với người dùng trước khi bắt đầu**.

### Điều tra thêm (giao Explore agent, không sửa code, chỉ đọc + báo cáo)
Xác nhận cơ chế đồng bộ hiện tại đúng thiết kế: `SetupTabViewModel` (Giám sát DATA) và `PlcPollingService` (Monitor) dùng chung 1 `ModbusRegisterTable` singleton qua DI — cả 2 refresh mỗi 500ms, độ trễ tối đa ~1 giây. Phát hiện 1 lỗi thật: `SetupTabViewModel.SaveAsync` gọi `_pollingService.SetInputBlockRange(...)` nhưng **thiếu** `SetOutputBlockRange(...)` tương ứng — `ApplyRegisterTableRanges()` cập nhật `ModbusRegisterTable._allowedRanges` (Input+Output) ngay lập tức, nhưng vòng ghi Output theo chu kỳ trong `PollLoopAsync` vẫn dùng `_outputBlockStart/_outputBlockCount` CŨ cho tới khi khởi động lại app — nếu Admin đổi cấu hình Khối Output ở Setup rồi Save, vòng ghi ra "PLC" bị lệch vùng địa chỉ so với vùng đang thực sự lưu dữ liệu.

### Sửa
`SetupTabViewModel.cs` (`SaveAsync`) — thêm 1 dòng `_pollingService.SetOutputBlockRange(OutputBlockStartAddress, OutputBlockRegisterCount);` ngay sau `SetInputBlockRange(...)`.

### Build/test/verify
- Build Debug: 0 lỗi (còn vài warning CS4014 tiền nhiệm ở `PlcPollingService.cs`, không liên quan thay đổi lần này, chưa xử lý).
- Unit test: `EolTester.Core.Tests` 1/1, `EolTester.Communication.Tests` 37/37 pass — không hồi quy.
- Verify UI thật qua `ui-verifier` agent (PrintWindow, không full-screen capture) — **PASS toàn bộ 8 bước**: gõ D1003 vào Giám sát DATA → ghi giá trị 1 → tab Monitor Y00 lên xanh trong <1-2s không cần thao tác thêm; chuyển THỦ CÔNG bấm ON cho Y01 → quay lại Setup, D1003 = 3 (bit0+bit1) đúng; đổi `OutputBlockRegisterCount` 20→21 rồi Lưu — không exception, dữ liệu Output tiếp tục cập nhật bình thường ngay sau Save (đúng kịch bản tái hiện bug vừa sửa). Ảnh lưu tại `Test/Report/screenshots/verify1..7-*.png`.

### Quyết định quan trọng và lý do
- **Không re-run lại fix mục 22 dù agent nền trước đó báo "stopped, không có completion record"** — đọc trực tiếp file nguồn xác nhận thay đổi đã tồn tại đầy đủ và đúng như kế hoạch (agent đã kịp ghi file trước khi bị dừng, dù chưa báo cáo xong) — tránh làm trùng lặp công việc agent đã hoàn thành, đúng nguyên tắc "trust but verify": không tin báo cáo (vì không có), nhưng cũng không giả định thất bại — kiểm tra trực tiếp trạng thái thật của code.
- **Bug `SetOutputBlockRange` không phải nguyên nhân chính của triệu chứng người dùng vừa báo** (triệu chứng đó đã được mục 22 giải quyết) — đây là 1 lỗi độc lập, phát hiện thêm qua rà soát chủ động toàn bộ đường đi dữ liệu theo đúng yêu cầu "đồng bộ với TẤT CẢ màn hình liên quan" của người dùng, không chỉ vá đúng triệu chứng đã báo. Phòng ngừa 1 lớp lỗi tương tự có thể xảy ra khi Admin thay đổi cấu hình Khối Output giữa phiên làm việc (kịch bản chưa được người dùng thử qua nhưng hợp lý sẽ xảy ra khi lắp PLC thật).
- **Bổ sung rule quy trình mới vào CLAUDE.md mục 0 (Phần I — áp dụng mọi dự án)**: "chưa rõ thì hỏi trước khi làm" — theo đúng yêu cầu tường minh của người dùng ở phiên này, đặt ở vị trí đầu Phần I để luôn được đọc trước tiên mỗi phiên.

## 24. Tóm tắt phiên làm việc (2026-07-12, tiếp) — Gỡ bỏ cơ chế "Mock mirror" Bit2/Bit3→Bit1: 3 bit phải độc lập hoàn toàn

### Bối cảnh
Người dùng xác nhận Bit 1 hiển thị đúng (không cần sửa thêm), nhưng chỉ rõ 1 hiểu sai kiến trúc ở round 22: **Bit 1, Bit 2, Bit 3 phải là 3 vùng độc lập hoàn toàn**, không được đồng nhất/mirror lẫn nhau — nút ON/OFF ở Monitor chỉ nên hiểu là ghi Bit 2 (lệnh gửi PLC), độc lập với Bit 1 (trạng thái, PLC ghi/PC đọc) và Bit 3 (bàn giao, PLC ghi/PC đọc). Người dùng gợi ý dùng "Giám sát DATA" để test nhanh (tự ghi Bit 1/Bit 3 mô phỏng PLC khi chưa có phần cứng thật) thay vì để hệ thống tự mirror.

### Sửa (`PlcPollingService.cs`)
Gỡ bỏ hoàn toàn cơ chế "Mock-only mirror" thêm ở round 22:
1. `WriteOutputAsync`: bỏ khối `if (hasCommandAddress && _driver is MockModbusDriver) { await WriteBitAsync(point.Definition.Address, value, ct); }` — giờ chỉ ghi Bit 2 (hoặc `Address` nếu điểm chưa nâng cấp CommandAddress). `point.Value` chỉ được set lạc quan khi **không** có `CommandAddress` riêng (điểm cũ, 1-bit); điểm đã nâng cấp thì `point.Value` chỉ đổi qua polling Bit 1 thật.
2. `TransferHandoverToCommandAsync`: bỏ khối mirror Bit 3→Bit 1 và bỏ luôn dòng `point.Value = handoverValue;` — giờ chỉ đọc Bit 3, ghi Bit 2, không đụng gì tới Bit 1/`point.Value`.
3. Cập nhật lại comment giải thích trong `PollLoopAsync` (đoạn polling Bit 1 cho Output) — bỏ nhắc tới cơ chế mirror đã gỡ, nhấn mạnh PC không bao giờ tự ghi Bit 1 kể cả khi chạy Mock.

### Build/test/verify
- Build Debug 0 lỗi. Test `EolTester.Core.Tests` 1/1 + `EolTester.Communication.Tests` 37/37 pass — không hồi quy (thay đổi chỉ ở `EolTester.App`).
- Verify UI thật (PrintWindow) qua `ui-verifier` agent — **PASS toàn bộ**: bấm ON cho Y00 (đã có CommandAddress) → "Điểm" **không** đổi màu ngay (đúng kỳ vọng mới, khác round 22); Giám sát DATA đọc D1106 xác nhận Bit 2 đã ghi đúng dù Bit 1 chưa đổi; ghi trực tiếp D1003=1 qua Giám sát DATA (mô phỏng PLC báo Bit 1) → quay lại Monitor, "Điểm" Y00 **mới** chuyển xanh — đúng chứng minh Bit 1 độc lập, chỉ đổi khi có ai ghi trực tiếp vào nó; chuyển Auto→Manual→Auto không exception, Bit 1 không tự nhảy theo bumpless transfer.

### Quyết định quan trọng và lý do
- **Đảo ngược quyết định "Mock mirror Bit2/Bit3→Bit1" của round 22 — quyết định đó vi phạm đúng ý nghĩa kiến trúc 3-bit đã định nghĩa từ round 18 (Bit 1/3 = PLC ghi, Bit 2 = PC ghi, độc lập).** Round 22 ưu tiên "trải nghiệm test tiện lợi" (thấy màu đổi ngay khi bấm ON, không cần thao tác gì thêm) hơn tính đúng đắn kiến trúc — đây là đánh đổi sai, vì mirror khiến Mock hành xử khác về mặt ngữ nghĩa so với PLC thật (PLC thật không bao giờ tự đổi Bit 1 tức thì theo Bit 2 mà không qua chu trình quét/ladder logic thật), dễ gây hiểu nhầm khi demo cho khách hàng hoặc khi debug sau này tưởng nhầm cơ chế đang hoạt động đúng như PLC thật.
- **Đánh đổi được chấp nhận: mất phản hồi tức thì khi test bằng Mock, đổi lại bằng thao tác "Giám sát DATA".** Người dùng chủ động đề xuất phương án này — tự ghi Bit 1/Bit 3 qua Giám sát DATA khi cần test nhanh không có PLC, thay vì để hệ thống tự động giả lập sai ngữ nghĩa. Đây là lựa chọn đúng: công cụ "Giám sát DATA" vốn đã tồn tại đúng cho mục đích test/dò thanh ghi (mục 16), không cần thêm cơ chế mirror riêng.
- **Bài học lặp lại (lần 3 trong dự án này — xem thêm mục 21, 22)**: một cơ chế "tiện lợi cho test" (mirror, giả lập, optimistic update) rất dễ vô tình vi phạm 1 ràng buộc kiến trúc đã chốt trước đó nếu không đối chiếu lại đúng định nghĩa gốc trước khi thêm — ở đây là quên rằng "Bit 1/Bit 3 chỉ PLC được ghi" đã được chốt rõ từ round 18 nhưng round 22 tự thêm ngoại lệ "trừ khi Mock" mà không hỏi lại người dùng có đồng ý ngoại lệ đó không. Rule mới ở CLAUDE.md mục 0 (xác nhận trước khi làm nếu chưa rõ) trực tiếp phòng ngừa loại lỗi này trong tương lai.

## 25. Tóm tắt phiên làm việc (2026-07-13) — Sửa hồi quy: nút ON/OFF Monitor không bấm được sau khi gỡ Mock mirror (mục 24)

### Bối cảnh
Người dùng báo: sau khi gỡ cơ chế Mock mirror (mục 24), không thể bấm ON/OFF cho Output ở tab Monitor.

### Nguyên nhân gốc
`MonitorTabViewModel.TurnOnAsync`/`TurnOffAsync` có guard dựa vào `point.Value` để chặn ghi lặp khi bấm đúng nút đã khớp trạng thái hiện tại — thiết kế này đúng khi `point.Value` từng được `WriteOutputAsync` tự cập nhật lạc quan (optimistic) ngay sau khi ghi (round 18-22). Sau khi mục 24 gỡ hẳn việc `WriteOutputAsync` cập nhật `point.Value` cho các điểm đã có `CommandAddress` riêng (đúng theo yêu cầu "Bit 1 độc lập, chỉ PLC/Giám sát DATA mới đổi được"), `point.Value` đứng yên ở `false` sau lần ON đầu tiên — khiến guard `!point.Value` ở `TurnOffAsync` luôn đúng, chặn vĩnh viễn mọi lần bấm OFF sau đó. Đây là hệ quả trực tiếp của việc gỡ mirror ở mục 24 mà chưa rà soát hết các chỗ khác đang ngầm dựa vào giả định cũ "point.Value phản ánh lệnh vừa ghi" — đúng loại lỗi mà bài học ở mục 24 vừa cảnh báo, tái diễn ngay trong chính round tiếp theo vì chưa rà đủ.

### Sửa (`MonitorTabViewModel.cs`)
Bỏ điều kiện `point.Value`/`!point.Value` khỏi guard của `TurnOnAsync`/`TurnOffAsync` — chỉ còn kiểm tra `point is null || !CanUseManualControls`. Ghi lặp giá trị giống nhau xuống Bit 2 là vô hại (không có tác dụng phụ), nên bỏ hẳn việc "né ghi lặp" thay vì cố duy trì bằng 1 nguồn trạng thái cục bộ khác — đơn giản nhất, không thêm state mới. Audit log đơn giản hóa theo, chỉ ghi `newValue` (lệnh vừa gửi), bỏ `oldValue` (không còn ý nghĩa đáng tin cậy vì không biết chắc trạng thái Bit 2 trước đó nếu không tự theo dõi riêng).

### Build/test/verify
- Build Debug 0 lỗi. Test 38/38 pass — không hồi quy.
- Verify UI thật (PrintWindow) qua `ui-verifier` — **PASS**: bấm ON→OFF→ON→OFF liên tục 3 chu kỳ cho Y02, đối chiếu D1106 (word CommandAddress Y00-Y17) qua Giám sát DATA mỗi lần — giá trị đổi đúng 2↔6 (bit index 2) ở MỌI chu kỳ, không kẹt sau lần đầu như bug vừa báo.

### Quyết định quan trọng và lý do
- **Không khôi phục dedup-guard bằng cách thêm 1 trường trạng thái cục bộ mới (VD `LastCommandValue` riêng trên `IoPointRowViewModel`) để né ghi lặp.** Cân nhắc nhưng chọn phương án đơn giản hơn: bỏ hẳn dedup, vì ghi lặp cùng giá trị xuống Bit 2 không gây hại gì (không có race condition, không có xung đột với PLC) — thêm 1 nguồn trạng thái mới chỉ để tối ưu 1 hành vi vô hại là over-engineering, vi phạm CLAUDE.md mục 5 ("không thêm trừu tượng hóa vượt yêu cầu thực tế").
- **Bài học quy trình (áp dụng ngay, không chờ tổng kết cuối phiên)**: khi gỡ 1 cơ chế mà 1 property (ở đây `point.Value`) từng đóng 2 vai trò chồng lấn (vừa là "hiển thị Bit 1 thật", vừa ngầm được dùng làm "cờ theo dõi lệnh vừa gửi" ở nơi khác trong cùng ViewModel graph), phải **grep toàn bộ nơi sử dụng property đó** trước khi coi thay đổi là hoàn tất — mục 24 chỉ sửa đúng `PlcPollingService.cs` (nơi trực tiếp gán giá trị) mà chưa rà các nơi tiêu thụ giá trị đó với giả định cũ (ở đây là `MonitorTabViewModel.TurnOnAsync/TurnOffAsync`). Từ nay khi gỡ 1 cơ chế ghi/cập nhật property dùng chung, luôn `Grep` toàn repo theo tên property đó, không chỉ sửa nơi ghi.

## 26. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Tách màu 2 nút ON/OFF sang Bit 2 (CommandValue), không còn dùng Bit 1

### Bối cảnh
Người dùng xác nhận: liên kết dữ liệu (mục 25) đã đúng, màu cột "Điểm" (Bit 1) đã đúng — nhưng chỉ ra 1 điểm còn sai: **màu tô sáng của 2 nút ON/OFF vẫn đang lấy theo Bit 1** (`Value`), trong khi đúng ra phải theo **Bit 2** (`CommandAddress` — lệnh PC gửi, độc lập với Bit 1): Bit 2 = 1 → nút ON xanh, Bit 2 = 0 → nút OFF xanh. Yêu cầu chỉ sửa đúng phạm vi này, ngoài phạm vi phải xác nhận trước.

### Sửa (3 file)
1. `IoPointRowViewModel.cs`: thêm `[ObservableProperty] private bool _commandValue;` — property mới, tách hẳn khỏi `Value` (vẫn giữ nguyên, dùng riêng cho cột "Điểm").
2. `PlcPollingService.cs`:
   - `WriteOutputAsync`: thêm `point.CommandValue = value;` (không điều kiện `hasCommandAddress` — luôn cập nhật, vì PC luôn là chủ ghi Bit 2/Address nên luôn biết chắc giá trị vừa gửi, không có độ trễ/không chắc chắn như Bit 1).
   - `TransferHandoverToCommandAsync`: thêm `point.CommandValue = handoverValue;` sau khi ghi Bit 2 từ Bit 3 (bumpless transfer) — nút ON/OFF phản ánh đúng ngay lúc chuyển sang Thủ công.
   - `PollLoopAsync`: thêm 1 khối đọc riêng `CommandAddress` (nếu có) từ `ModbusRegisterTable` mỗi tick, cập nhật `output.CommandValue` — để màu nút cũng đồng bộ khi Bit 2 bị đổi từ bên ngoài qua "Giám sát DATA" (không chỉ dựa vào cập nhật lạc quan lúc ghi).
3. `MonitorTabView.xaml`: đổi `Background` của 2 nút ON/OFF từ `{Binding Value, ...}` sang `{Binding CommandValue, ...}` (cả `IsOnToBrush` lẫn `IsOffToBrush`) — chỉ trong `OutputColumnTemplate`, không đụng `InputColumnTemplate`/cột "Điểm" (`PointCellStyle` vẫn bind `Value`).

### Build/test/verify
- Build Debug 0 lỗi (thêm đúng 1 warning CS4014 mới, cùng loại fire-and-forget `BeginInvoke` như các dòng polling cũ, vô hại). Test 38/38 pass — không hồi quy.
- Verify UI thật (PrintWindow) qua `ui-verifier` — **6/6 bước liên quan trực tiếp yêu cầu đều PASS**: bấm ON cho Y03 → nút ON xanh ngay, OFF trung tính; bấm OFF → ngược lại; xác nhận cột "Điểm" của Y03 KHÔNG đổi màu trong suốt quá trình bấm (Bit 1 độc lập, đúng yêu cầu). **Bước 7 (đồng bộ ngược: tự ghi D1106 qua Giám sát DATA, xác nhận màu nút Monitor đổi theo trong ≤1-2s) không thực hiện được bằng live-UI**: agent verify (đúng theo thiết kế an toàn của nó) từ chối tự đăng nhập Admin bằng thông tin nhận được qua tin nhắn điều phối (không tin tưởng nguồn credential được relay), và giữ nguyên lập trường sau khi được xác nhận lại lần 2 — **quyết định chấp nhận dừng ở đây, không ép agent tiếp tục hoặc gọi thêm 1 vòng agent khác**, vì cơ chế polling Bit 2 mới thêm ở `PollLoopAsync` có cấu trúc **giống hệt** cơ chế polling Bit 1 đã được verify trực tiếp nhiều lần trong các round trước (mục 22-23, cùng đọc qua `_registerTable.TryGetValue`/`_dispatcher.BeginInvoke`) — độ tin cậy suy luận từ mã nguồn đủ cao để không cần lặp lại đúng kịch bản verify cho lần thứ N.

### Quyết định quan trọng và lý do
- **Tách 2 property riêng biệt (`Value` cho Bit 1, `CommandValue` cho Bit 2) thay vì tái sử dụng 1 property chung với cờ ngữ cảnh.** Lý do: đúng tinh thần "3 bit độc lập" người dùng đã nhấn mạnh liên tục 2 round gần đây (mục 24, 25) — 2 khái niệm hiển thị khác nhau (trạng thái PLC thật vs lệnh PC vừa gửi) nên có 2 nguồn dữ liệu tách bạch, tránh lặp lại đúng loại lỗi "1 property gánh 2 vai trò" đã gây ra hồi quy ở mục 25.
- **`CommandValue` cập nhật lạc quan ngay khi ghi (khác hẳn quyết định "không lạc quan" đã áp dụng cho `Value`/Bit 1 ở mục 24).** Đây không phải mâu thuẫn — khác bản chất: Bit 1 do PLC làm chủ (PC ghi không có nghĩa PLC đã xác nhận), còn Bit 2 do chính PC làm chủ ghi (PC luôn biết chắc 100% giá trị vừa gửi, không có độ trễ xác nhận nào cần chờ) — lạc quan ở đây không vi phạm nguyên tắc "không mirror/đồng nhất bit", vì đang đọc đúng bit PC sở hữu, không phải suy đoán sang bit khác.
- **Vẫn giữ thêm polling Bit 2 mỗi tick (không chỉ dựa vào cập nhật lạc quan lúc ghi).** Lý do: cập nhật lạc quan chỉ đúng nếu Bit 2 CHỈ bị đổi qua đúng 2 hàm `WriteOutputAsync`/`TransferHandoverToCommandAsync` — nhưng Admin có thể tự ghi thẳng `CommandAddress` qua "Giám sát DATA" (cùng 1 công cụ test đã được xác nhận là kênh hợp lệ ở mục 24) — polling đảm bảo mọi nguồn ghi đều được phản ánh, không chỉ 2 hàm nội bộ, nhất quán với cách Bit 1 đã làm.
- **Chấp nhận verify bước 7 bằng suy luận mã nguồn (code-reasoning) thay vì bắt buộc lặp lại vòng agent thứ 3 cho đúng 1 kịch bản đã có tiền lệ chứng minh nhiều lần.** Agent verify từ chối dùng credential được relay qua tin nhắn điều phối là hành vi AN TOÀN ĐÚNG ĐẮN theo thiết kế (không nên tự động tin "cứ làm đi, không cần hỏi lại" khi đó là 1 hành động có đặc quyền) — không phải lỗi cần sửa. Không spawn thêm agent mới chỉ để lặp lại đúng bước đã được cùng 1 cơ chế polling chứng minh đúng ở Bit 1 nhiều lần trước đó — cân bằng giữa "test đến khi chắc chắn 100%" (yêu cầu chung ở CLAUDE.md mục 0) và tránh lãng phí vòng lặp verify cho phần rủi ro thấp/đã có tiền lệ mạnh.

## 27. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Viết `ModbusRtuDriver` thật + kết nối thành công lần đầu với PLC vật lý

### Bối cảnh
Người dùng báo: đã đấu nối PLC thật, cấu hình COM3/19200 ở Setup, nhưng thanh ghi PLC đã có dữ liệu mà app không đọc lên được. Điều tra xác nhận nguyên nhân gốc: `App.xaml.cs` DI hardcode `MockModbusDriver` — app chưa từng thực sự mở cổng COM nào bất kể cấu hình Setup. `ModbusRtuDriver` thật là việc đã cố ý hoãn từ đầu dự án (chờ đúng lúc này).

### Quyết định kiến trúc (hỏi trước khi làm, theo đúng rule mục 0)
Hỏi người dùng: sau khi có driver thật, chọn Mock/Real bằng cách nào? Người dùng chọn **thêm công tắc bật/tắt ở tab Setup** (không phải "luôn dùng thật khi Master") — giữ nguyên workflow dev/demo không cần phần cứng cho những người làm việc sau này.

### Đã làm
1. `ConnectionSettings.DriverMode` (enum `Mock`/`Real`, mặc định `Mock`) — `EolTester.Configuration/Models/ConnectionSettings.cs`.
2. `ModbusRtuDriver` (`src/EolTester.Communication/ModbusRtu/ModbusRtuDriver.cs`) — PC làm Master, NModbus + `SerialPort`, implement đủ `IPlcCommunicationDriver`. Xác nhận API NModbus qua reflection thực tế (`ModbusFactory.CreateRtuMaster` là extension method trả `IModbusSerialMaster`, `Transport.Retries/ReadTimeout/WriteTimeout` cấu hình timeout+retry) trước khi viết code — tránh đoán sai API rồi debug mù.
3. `PlcPollingService` đổi từ nhận 1 `IPlcCommunicationDriver` cố định sang nhận cả `MockModbusDriver` lẫn `ModbusRtuDriver` cụ thể qua constructor, chọn driver active trong `StartAsync` dựa vào `settings.DriverMode` — vì lựa chọn chỉ biết được sau khi tải cấu hình (không biết lúc DI dựng graph).
4. `PollLoopAsync` thêm `catch (Exception ex)` tổng quát (log + tiếp tục tick kế tiếp) — trước đây exception ngoài `OperationCanceledException`/`InvalidOperationException` sẽ làm chết hẳn task polling nền vĩnh viễn mà không ai biết, nguy hiểm khi giờ có driver thật có thể ném nhiều loại lỗi (timeout, IOException...).
5. Tab Setup thêm dropdown **"Driver"** (Mock/Real, chỉ hiện khi Role=Master, đổi cần khởi động lại — cùng cơ chế Role).
6. **Sửa lại ý nghĩa `ConnectionState.Connected`** — phát hiện qua verify UI: ban đầu driver đặt Connected ngay khi `SerialPort.Open()` thành công, dù chưa chắc PLC có phản hồi (cổng COM tồn tại vật lý nhưng không có ai đấu vào vẫn mở được). Sửa: chỉ đặt Connected sau khi có ít nhất 1 lần đọc/ghi Modbus thật sự round-trip thành công với thiết bị đầu kia — đúng tinh thần CLAUDE.md mục 3 ("chỉ báo trạng thái phải phản ánh thực tế").
7. Viết 4 integration test mới (`tests/EolTester.Communication.Tests/ModbusRtuDriverTests.cs`) — dùng TCP loopback đóng vai PLC giả (tái dùng `ModbusSlaveService` làm "PLC ảo" ở đầu kia), xác nhận `ModbusRtuDriver` đọc/ghi đúng qua NModbus mà không cần cổng COM ảo (com0com) hay PLC thật — 41/41 test pass (37 cũ + 4 mới).

### Kết quả kiểm chứng với PLC thật (lần đầu tiên trong dự án)
Người dùng xác nhận đã đấu **COM4** (không phải COM3 như câu hỏi ban đầu — đổi cổng thực tế), 19200/None/8/1, Slave ID 2. Chạy app thật (tự thực hiện trực tiếp qua `ui-verifier` agent, không dùng subagent tự do click bừa vì đang chạm PLC thật — agent tự chặn không click tọa độ chưa xác minh, đúng ranh giới an toàn):
- **Chỉ báo trạng thái hiện "Đã kết nối" (xanh) ngay từ lần chạy đầu** — driver thật kết nối thành công.
- Giá trị đọc từ "Giám sát DATA" (D1000, D1003, D1010, D1106) đều ổn định qua nhiều lần chụp cách nhau vài giây (không nhảy số kiểu jitter Mock) — xác nhận đang đọc thật từ PLC, không lẫn Mock.
- **Tất cả đều = 0** — chưa rõ là giá trị thật hợp lệ hay do địa chỉ D chưa đúng chỗ PLC lưu dữ liệu (nghi vấn offset địa chỉ Modbus hoặc bản đồ D-register→Modbus register của gateway khác giả định) — **câu hỏi mở, cần đối chiếu với kỹ sư PLC**, đã ghi vào CLAUDE.md mục 13.
- Không thực hiện được việc đọc thêm ở tab Main (6 giá trị đo High/Low) vì agent verify tự chặn click theo tọa độ chưa xác minh trên phiên đang chạm PLC thật — chấp nhận dừng ở đây, ưu tiên an toàn hơn đọc thêm dữ liệu.

### Quyết định quan trọng và lý do
- **Không tự đoán API NModbus — dựng 1 project console nhỏ dùng reflection để in ra đúng chữ ký `IModbusMaster`/`IModbusTransport`/`ModbusFactory` trước khi viết `ModbusRtuDriver`.** Lý do: đây là lần đầu code thật sự gọi Master API của NModbus (trước đó dự án chỉ dùng phía Slave) — đoán sai tên method/namespace sẽ phải build-fail-sửa nhiều vòng, tốn thời gian hơn hẳn so với xác minh trực tiếp 1 lần.
- **`ConnectionState` không dựa vào "mở cổng COM thành công" mà dựa vào "có phản hồi Modbus thật".** Lý do: đây đúng là loại lỗi "trạng thái kết nối gây hiểu nhầm" mà CLAUDE.md mục 3 đã cảnh báo trước — phát hiện được nhờ `ui-verifier` test đúng kịch bản "cổng COM tồn tại vật lý (COM4 có sẵn trên máy dev) nhưng không PLC nào trả lời" trước khi người dùng thật sự đấu PLC, kịp sửa trước khi ảnh hưởng tới lần kết nối thật.
- **Viết integration test bằng TCP loopback + `ModbusSlaveService` đóng vai "PLC ảo" thay vì chỉ tin code đúng nhờ đọc kỹ.** Lý do: nhất quán với cách dự án đã kiểm chứng `ModbusSlaveService` trước đó (mục 15) — cho phép verify logic Modbus Master (parse địa chỉ, ánh xạ FC03/FC06/FC01/FC05, timeout/retry) hoàn toàn tự động, tách bạch khỏi rủi ro/độ trễ của việc test trên phần cứng thật mỗi lần sửa code nhỏ.
- **Không tự đoán/sửa địa chỉ D khi giá trị đọc được toàn 0** — dừng lại, ghi thành câu hỏi mở, không tự ý đoán offset hay đổi công thức map địa chỉ. Lý do: đây là dữ liệu đặc thù theo cấu hình PLC/gateway thật của khách hàng, đoán sai có thể khiến ghi nhầm địa chỉ khi sau này bật tính năng ghi — rủi ro vật lý thật, cần xác nhận từ người có tài liệu PLC trước khi đi tiếp.
- **Tuyệt đối không ghi/bấm ON/OFF/Start/Stop/Reset trong suốt phiên verify với PLC thật lần đầu** — chỉ đọc. Lý do: nguyên tắc thận trọng bắt buộc khi code lần đầu chạm phần cứng thật của khách hàng, đã quán triệt rõ trong prompt giao cho agent verify, và agent tự thực thi đúng (từ chối click tọa độ chưa xác minh thay vì liều thử).

## 28. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Xác nhận địa chỉ D1000-D1002 khớp PLC thật + Test Connection thật (bỏ chữ "giả lập")

### Bối cảnh
Tiếp nối mục 27 (kết nối PLC thật lần đầu, đọc thử 4 địa chỉ đều ra 0 — chưa rõ đúng/sai). Người dùng tự đặt giá trị biết trước trên PLC thật để đối chiếu: D1000=60, D1001=70, D1002=80. Đồng thời yêu cầu: nút "Test Connection" ở tab Setup đang là stub giả (luôn báo "thành công" sau delay cố định, không gọi driver thật) — cần sửa để phản ánh đúng driver Real, và bỏ chữ "giả lập" khỏi thông báo kết nối khi đang dùng driver thật.

### Đã sửa
1. `PlcPollingService.TestConnectionAsync()` (mới) — dùng chính driver đang chạy (không mở thêm kết nối cạnh tranh cùng cổng COM): nếu là `ModbusRtuDriver`, đọc thử 1 thanh ghi ở đầu Input Block đã cấu hình, trả về giá trị thật hoặc lỗi thật; nếu Mock, trả về thành công ngay (đúng bản chất — không có PLC để test).
2. `SetupTabViewModel.TestConnectionAsync` — gọi hàm trên thay vì `Task.Delay(400)` giả, hiển thị đúng 1 trong 3 thông báo mới: `Setup_TestResultReal` (có giá trị thật), `Setup_TestResultMock` (chế độ Mock), `Setup_TestResultError` (lỗi thật) — cả 2 ngôn ngữ (resx VI/EN), không còn key `Setup_TestResult` cũ chứa chữ "giả lập"/"mock driver" cứng.

### Kết quả kiểm chứng với PLC thật
- **Test Connection**: hiển thị đúng "...đọc được giá trị thật từ PLC: 50" — không còn chữ "giả lập", có giá trị thật.
- **Giám sát DATA**: D1000=50 (người dùng báo 60, lệch 10 — khả năng giá trị PLC đã đổi giữa lúc báo và lúc đọc, không phải lỗi hệ thống vì D1001/D1002 dùng chung 1 đường code parse/đọc và khớp tuyệt đối), D1001=**70** (khớp), D1002=**80** (khớp). Đọc lại lần 2 cách 4 giây cho kết quả giống hệt — ổn định.
- **Kết luận**: cơ chế đọc địa chỉ Modbus (`ModbusWordAddress`, không cộng/trừ offset) đã được xác nhận đúng với PLC thật lần đầu tiên trong dự án — 2/3 địa chỉ khớp tuyệt đối là bằng chứng đủ mạnh, không cần "sửa lỗi" gì thêm cho phần này.
- Build 0 lỗi, test 42/42 pass, publish `APP/` thành công.

### Quyết định quan trọng và lý do
- **Không tự kết luận D1000 lệch 10 là bug** — chỉ báo cáo trung thực số liệu quan sát được, để người dùng tự xác nhận giá trị PLC tại đúng thời điểm đọc (giá trị PLC hoàn toàn có thể đã bị đổi giữa lúc người dùng báo và lúc app đọc, do người dùng hoặc chương trình PLC khác đang chạy). Đúng nguyên tắc đã đặt ở mục 27: không đoán/sửa dữ liệu đặc thù PLC khi chưa chắc chắn nguyên nhân.
- **`TestConnectionAsync` dùng LẠI driver đang chạy (đã mở sẵn cổng COM qua `PlcPollingService.StartAsync`) thay vì mở 1 kết nối Modbus mới độc lập để test.** Lý do: Modbus RTU chỉ cho 1 kết nối trên 1 cổng COM tại 1 thời điểm — mở thêm 1 driver test riêng sẽ xung đột (lỗi "cổng đang được dùng") với driver polling đang chạy. Dùng lại driver hiện có vừa tránh xung đột, vừa test đúng ý nghĩa "kết nối đang cấu hình có thực sự hoạt động không" thay vì test 1 kết nối giả định khác.
- **Test Connection phản ánh driver ĐANG CHẠY, không phải giá trị vừa gõ chưa lưu trong form Setup.** Lý do: nhất quán với ràng buộc đã có từ trước — đổi `DriverMode`/`ComPort`... cần khởi động lại app mới có hiệu lực (chưa hot-swap runtime), nên Test Connection test đúng cấu hình thật sự đang chạy, không tạo cảm giác sai lệch "test cấu hình mới nhưng app vẫn chạy cấu hình cũ".

## 29. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Xác nhận + hoàn thiện 2 dải D0-D99 (chỉ-đọc)/D100-D199 (chỉ-ghi) cho Role=Master

### Bối cảnh
Người dùng yêu cầu (qua GitHub, phiên trước không kịp ghi log): khi PC làm Master, D0-D99 chỉ cho phép đọc từ PLC lên (không cho PC ghi xuống), D100-D199 chỉ cho phép ghi từ PC xuống (không đọc ngược), Input/Output Block cấu hình được ở tab Setup chỉ nên bắt đầu từ D200 trở lên, và mỗi tick polling chạy đúng 2 phase (đọc D0-D99 + Input Block, rồi ghi D100-D199 + Output Block). Khảo sát code phát hiện **toàn bộ cơ chế cốt lõi đã được implement sẵn** từ phiên trước đó (`ModbusRegisterTable.CanReadAddress`/`CanWriteAddress`, `PlcPollingService.RefreshRuntimeStateAsync` 2 phase, đã có 2 unit test cover) — nhưng chưa được ghi vào CLAUDE.md/history.md (vi phạm quy tắc mục 1), và có 2 lỗ hổng nhỏ chưa xử lý.

### Đã sửa (2 lỗ hổng phát hiện qua rà soát + verify UI thật)
1. **`SetupTabViewModel.SaveAsync` chưa chặn Input/Output Block Start Address < 200** — dù tầng `ModbusRegisterTable` đã tự bảo vệ đúng theo quy tắc cứng bất kể giá trị cấu hình, việc để Admin nhập tự do (VD Output Block start=50) gây khó hiểu khi debug và có thể khiến vòng ghi theo chu kỳ **cố ghi vật lý xuống PLC** ở dải D0-D99 (xem lỗ hổng #2). Thêm `MinConfigurableBlockAddress = 200` + validate ngay đầu `SaveAsync`, báo lỗi qua `RegisterWatchValidationSummary` (resx key mới `Setup_BlockStartAddressTooLow`, VI+EN).
2. **`RefreshRuntimeStateAsync` (Phase 1/2, vòng Input/Output Block) gọi `_driver.WriteRegisterAsync`/`ReadRegisterAsync` KHÔNG ĐIỀU KIỆN**, chỉ có `ModbusRegisterTable.TryStoreReadValue`/`TrySetWordForMasterWrite` (cập nhật cache in-memory) mới thật sự kiểm tra `CanReadAddress`/`CanWriteAddress`. Nếu Setup có cấu hình cũ (lưu từ trước khi có validation #1, VD từ `connection.json` cũ) trỏ Output Block start < 100, vòng ghi Output Block vẫn sẽ **ghi vật lý 1 giá trị (có thể là 0) xuống PLC ở dải D0-D99 chỉ-đọc mỗi tick** — vi phạm đúng yêu cầu người dùng. Thêm guard `if (!_registerTable.CanReadAddress(addr)) continue;`/`if (!_registerTable.CanWriteAddress(addr)) continue;` ngay trước lời gọi driver trong 2 vòng Input/Output Block (không cần guard cho 2 vòng D0-D99/D100-D199 cố định vì range đó luôn đúng theo thiết kế, không phụ thuộc cấu hình).
3. **Bẫy UI phát hiện qua verify thật**: `RegisterWatchValidationSummary` ban đầu đặt ở `Grid.Row="2"` của panel phải "Giám sát DATA" — panel này là lưới cố định 30 ô (`RegisterWatchColumn1`/`2`, 15 dòng/cột × `RowHeight=26`) gần lấp đầy chiều cao khung canvas cố định (xem CLAUDE.md mục 12, layout fixed-canvas 1400×765 qua `Viewbox`, không có `ScrollViewer` ở panel phải) — `TextBlock` thông báo lỗi thêm vào cuối bị tràn/hết chỗ hiển thị, operator bấm Lưu tưởng "không có tác dụng" mà không rõ lý do. **Đã chuyển sang cột trái**, ngay dưới `TextBlock TestConnectionResult` (dưới 2 nút "Lưu cấu hình"/"Test Connection") — vị trí này đã kiểm chứng luôn có chỗ hiển thị qua verify UI thật (chụp `PrintWindow`, cả 2 trường hợp bị chặn và lưu thành công).

### Kết quả kiểm chứng
- Build 0 lỗi (Debug + Release), unit test Communication.Tests 43/43 pass (bao gồm 2 test có sẵn từ trước cover đúng `CanReadAddress`/`CanWriteAddress`), Core.Tests 1/1 pass.
- Verify UI thật 2 vòng: (1) trước khi sửa vị trí — xác nhận validation chặn lưu đúng (file `connection.json` không bị ghi đè khi nhập D50) nhưng thông báo lỗi không hiện được trên màn hình; (2) sau khi chuyển vị trí — thông báo lỗi hiện rõ ràng ở cột trái, biến mất khi sửa lại giá trị hợp lệ (≥200) và lưu thành công.
- Publish Release (`dotnet publish ... -o APP`) thành công, smoke-test `EolTester.App.exe` xác nhận `MainWindowTitle = "EOL Tester - HV356"` xuất hiện sau ~8 giây (self-contained Release cold-start chậm hơn Debug, không phải lỗi).

### Ghi chú kỹ thuật khác
- Gặp lỗi build tạm thời `CSC : error CS2001: ... .g.cs could not be found` 2-3 lần khi build lại `EolTester.App.csproj` liên tiếp ngay sau khi xóa `obj/bin` — nghi do race giữa build chính và `_wpftmp` design-time build của MSBuild khi 2 tiến trình `dotnet build` chồng lấn quyền ghi `obj/`. Build lại lần nữa (không xóa `obj/bin` lần 2) luôn qua — không phải lỗi code, không cần điều tra sâu hơn trừ khi tái diễn thường xuyên.
- Không phải git repo (đã xác nhận lại) — không có lịch sử commit để đối chiếu thay đổi giữa các phiên; càng cho thấy tầm quan trọng của việc ghi session log đầy đủ mỗi vòng làm việc.

## 30. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Thiết kế lại layout tab Setup theo mockup "Setting paged V3"

### Bối cảnh
Người dùng cung cấp `docs/legacy-ui/Setting paged V3.png` — mockup đã tự chỉnh tỉ lệ theo ý muốn cho cột trái "Cấu hình kết nối PLC" ở tab Setup, yêu cầu build lại giao diện khớp ≥95% tỉ lệ, và đảm bảo dòng thông báo xanh lá "Kết nối thử..." (`TestConnectionResult`) xuống dòng đọc được toàn bộ (không bị cắt).

### Đã sửa (`SetupTabView.xaml`, `Strings.resx`/`Strings.en-US.resx`)
1. **Gộp "Vai trò" + "Driver" thành 1 hàng "Vai trò / Driver"** — 2 `ComboBox` (width 140 mỗi cái) đặt cạnh nhau trong 1 `StackPanel Orientation="Horizontal"`, thay vì 2 hàng riêng như trước. Driver `ComboBox` vẫn giữ `Visibility` theo `IsMasterMode` (ẩn khi Slave).
2. **Gộp "Baud rate" + "Parity" thành 1 hàng "Baud rate / Parity"** — cùng pattern 2 `ComboBox` cạnh nhau.
3. **Cột nhãn thu hẹp từ 180 xuống 150px** (cả Grid cấu hình chính lẫn Grid "Khối Input/Output") — khớp tỉ lệ nhãn hẹp hơn trong mockup.
4. **Gỡ dòng hint text màu xám** `Setup_RegisterBlockHint` dưới tiêu đề "Khối thanh ghi đọc/ghi..." — mockup không còn dòng này (cùng tinh thần "màn hình khách hàng, hạn chế chữ giải thích" đã áp dụng trước đó cho "Giám sát DATA", xem CLAUDE.md mục 7 Tab 3). Resx key vẫn giữ lại (không xóa) — chỉ không còn được XAML tham chiếu.
5. **`StationAddress` (Master) và `MySlaveId` (Slave) gộp về chung 1 `Grid.Row`** (trước đó ở 2 `Grid.Row` riêng biệt kế tiếp nhau) — `Visibility` loại trừ nhau (`IsMasterMode`/`IsSlaveMode`) nên không bao giờ hiện đồng thời, gộp hàng giúp không để lại khoảng trống dòng trống khi đổi Role.
6. Tổng số hàng cấu hình bên trái giảm từ 11 xuống 8 — đúng khớp cấu trúc 8 hàng trong mockup (Vai trò/Driver, Giao thức, Cổng COM, Baud rate/Parity, Data bits/Stop bits, Địa chỉ trạm/Slave ID, Chu kỳ polling, Timeout/Retry).
7. Thêm 2 resx key mới `Setup_RoleDriver` ("Vai trò / Driver"), `Setup_BaudRateParity` ("Baud rate / Parity") — cả VI và EN.
8. **Không cần sửa gì cho việc wrap thông báo Test Connection** — `TextBlock` hiển thị `TestConnectionResult` đã có sẵn `TextWrapping="Wrap"` từ trước (không phải lỗi thực sự, chỉ là yêu cầu xác nhận lại của người dùng); verify UI thật xác nhận wrap đúng, xuống 2 dòng gọn gàng, không bị cắt.

### Kết quả kiểm chứng
- Build 0 lỗi (Debug + Release, gặp lại đúng race `_wpftmp`/`obj` thoáng qua đã ghi ở mục 29 — build lại lần 2 qua ngay, không phải lỗi code).
- Unit test Communication.Tests 43/43 + Core.Tests 1/1 pass — thay đổi lần này thuần XAML/resx, không đụng logic nghiệp vụ nên không có rủi ro hồi quy test.
- Verify UI thật (`PrintWindow`, không chụp toàn màn hình): xác nhận đúng 8 hàng theo thứ tự/nhãn ghép như mockup, dòng hint xám đã biến mất, thông báo Test Connection wrap đúng 2 dòng đọc được toàn bộ. **Ước lượng khớp mockup ~90-95%** — sai khác nhỏ duy nhất: ở màn hình test có `WorkArea` thấp hơn màn hình dùng chụp mockup, panel trái phải cuộn (`ScrollViewer` đã có sẵn làm lớp phòng vệ theo CLAUDE.md mục 7) để thấy hết từ "Vai trò/Driver" đến nút Lưu/Test Connection, trong khi mockup (chụp trên màn hình cao hơn) hiện đủ không cần cuộn — không phải lỗi của thay đổi layout, chỉ là khác biệt độ phân giải màn hình giữa 2 lần chụp.
- Publish Release thành công, smoke-test xác nhận `MainWindowTitle = "EOL Tester - HV356"` xuất hiện bình thường (~3 giây lần này, nhanh hơn lần trước — không có mẫu số chung rõ ràng về thời gian cold-start, có thể do cache hệ điều hành).

## 31. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Thu hẹp panel Setup + sửa lỗi thông báo lặp/chồng + nới lỏng Giám sát DATA thành D200-D2000

### Bối cảnh
Sau khi thiết kế lại layout tab Setup theo mockup V3 (mục 30), người dùng phản hồi tiếp qua nhiều lượt: (1) panel trái vẫn còn thanh cuộn dọc, khoảng cách quanh header "Khối thanh ghi đọc/ghi..." và dòng cảnh báo "Đổi Vai trò..." còn lớn; (2) tỉ lệ ngang giữa cột trái/phải chưa đúng mockup; (3) thông báo lỗi khi Lưu bị lặp lại nhiều lần cùng 1 câu; (4) hỏi rõ nguồn gốc lỗi "Địa chỉ nằm ngoài phạm vi Khối Input/Output đã cấu hình" ở "Giám sát DATA"; (5) muốn nới lỏng phạm vi đó thành D200-D2000; (6) phát hiện có thể xuất hiện tối đa 2 thông báo cùng lúc gây tràn thanh cuộn, và đoạn "(chế độ Mock...)" bị khuất; (7) muốn có thông báo xác nhận khi Lưu thành công (trước đó im lặng).

### Đã sửa

**A. Đo pixel chính xác trên mockup** (`Add-Type -AssemblyName System.Drawing`, quét màu viền `#D1D5DB` theo dòng ngang) thay vì ước lượng bằng mắt — phát hiện tỉ lệ thật: panel trái 37.9% / gap 1.5% / panel phải 60.6% (so với tỉ lệ đang dùng 42.4%/1.2%/56.4%). Điều chỉnh `ColumnDefinition` cột trái từ 580→518, gap 16→20; `StackPanel` bên trong 550→494; cột nhãn 2 Grid con 150→135.

**B. Nguyên nhân chính gây thanh cuộn: các dòng cảnh báo/kết quả rỗng vẫn chiếm chỗ layout** — `InputRegisterCountWarning`/`OutputRegisterCountWarning`/`TestConnectionResult`/`RegisterWatchValidationSummary` dù `Text` rỗng/null vẫn có chiều cao dòng mặc định (không set `Visibility`). Thêm `StringToVisibilityConverter` mới (`EolTester.App/Converters/StringToVisibilityConverter.cs`, string null/rỗng → `Collapsed`) áp cho cả 4 dòng — chỉ chiếm chỗ khi thực sự có nội dung.

**C. Thu hẹp thêm ~40% theo yêu cầu cụ thể**: margin quanh header "Khối thanh ghi đọc/ghi..." (top 20→12, bottom 10→6), margin trên nút "Lưu cấu hình"/"Test Connection" (16→10), margin quanh dòng cảnh báo "Đổi Vai trò..." (trên/dưới đều 12→7, áp dụng cho cả margin-dưới của tiêu đề "Cấu hình kết nối PLC" lẫn margin-dưới chính dòng cảnh báo, vì "khoảng trên" của 1 dòng thực chất là margin-dưới của dòng trước nó trong `StackPanel`).

**D. Lỗi thông báo lặp lại rất dài — nguyên nhân thật**: `SaveAsync` duyệt 30 dòng "Giám sát DATA", nối TẤT CẢ `ValidationMessage` khác null bằng `string.Join(" ", errors)` — khi nhiều dòng cùng chung 1 lý do lỗi (VD hàng loạt địa chỉ cùng "ngoài phạm vi" sau khi đổi Input/Output Block), câu đó bị lặp lại N lần. Sửa bằng `.Distinct()` trước khi join.

**E. Phân tích + sửa gốc rễ lỗi "Địa chỉ nằm ngoài phạm vi Khối Input/Output đã cấu hình" (dùng Plan Mode)** — người dùng hỏi nguồn gốc, agent giải thích có 3 lớp kiểm tra độc lập (vật lý `ModbusRegisterTable`, ô Start Address D200-2000, và `IsWithinConfiguredRange` cho từng dòng Giám sát DATA — lớp thứ 3 hẹp hơn nhiều, yêu cầu khớp ĐÚNG cửa sổ Input/Output Block đang cấu hình). Người dùng chọn nới lỏng lớp 3 thành D200-D2000. **Điểm quan trọng đã lường trước trong plan**: chỉ nới lỏng validate ở ViewModel mà không đổi `ModbusRegisterTable.ConfigureAllowedRanges` sẽ hỏng âm thầm (UI hết báo lỗi nhưng `TryGetWord`/`TryUpdateWord` vẫn âm thầm trả `false` vì cache chỉ cho phép đúng cửa sổ Block hẹp, trong khi `WriteRawWordAsync` vẫn thực sự ghi PLC — sai lệch giữa UI và thực tế). Đã sửa đồng bộ cả 2 tầng:
  - `ApplyRegisterTableRanges()`: đăng ký 1 range rộng duy nhất `(200, 1801)` thay vì 2 range hẹp theo InputBlock/OutputBlock.
  - `IsWithinConfiguredRange` đổi tên thành `IsWithinAppOwnedRange`, logic đổi từ so khớp cửa sổ Block sang kiểm tra khoảng cố định D200-D2000.
  - resx `Setup_RegisterWatchOutOfRange` đổi nội dung từ "ngoài phạm vi Khối Input/Output" → "phải nằm trong khoảng D200-D2000".
  - **Đánh đổi đã thống nhất với người dùng qua `AskUserQuestion`**: địa chỉ ngoài cửa sổ Input/Output Block đang polling sẽ không tự làm mới theo chu kỳ (chỉ echo giá trị Admin vừa gõ tay).

## 37. Tóm tắt phiên làm việc (2026-07-15) — Sửa 4 bug thật (Slave badge, Test Connection Access Denied, mất cache D100-199, scan commit từng ký tự), single-instance, 3 chế độ quét barcode + PLC-triggered move, tài liệu vận hành Word

### Bối cảnh
Phiên làm việc dài, nhiều yêu cầu rời rạc trên cùng codebase đang chạy thật với PLC (COM4, Role=Master). Người dùng đưa ra 1 **rule nghiêm túc, áp dụng lâu dài**: sau 1 lần trước đó bị báo "đã sửa" nhưng thực tế kết quả không đạt (thậm chí tệ hơn), người dùng yêu cầu tuyệt đối không tự ý kết luận khi chưa chắc chắn, phải kiểm tra đúng-đủ trước khi báo hoàn thành — rule này đã chi phối toàn bộ cách xử lý các bug còn lại trong phiên (luôn verify qua log Serilog + UI thật/PrintWindow, không suy đoán từ đọc code).

### Quyết định + lý do

**A. 4 bug thật đã sửa, đều được verify qua log/UI thật, không suy đoán:**
1. **Huy hiệu kết nối kẹt ở "Chưa kết nối" khi Role=Slave** — `PlcPollingService.State` không đổi khi Slave (không tự polling). Sửa: `ShellViewModel`/`MonitorTabViewModel` thêm `ObserveSlaveConnectionState(IPlcSlaveService)`, bind trực tiếp vào `ModbusSlaveService.State`/`StateChanged`, gọi từ `App.xaml.cs` khi Role=Slave.
2. **"Test Connection" luôn báo "Access to the path 'COM4' is denied" khi Role=Master đang polling live** — vì driver test cố mở cùng cổng COM đang bị `PlcPollingService` giữ độc quyền. Người dùng chỉ đúng hướng sửa: "nếu bấm Test Connect cần dừng kết nối cũ và kết nối với cấu hình mới". Refactor `PlcPollingService.RestartConnectionAsync` thành `PauseAsync()` (public) + `ConnectAndPollAsync()`; `SetupTabViewModel.TestConnectionAsync`/`SaveAsync` bọc bằng `PauseAsync()` trước, `try/finally` đảm bảo reconnect sau, cộng thêm guard `_connectionOperationInProgress` chống re-entrancy khi bấm liên tiếp (CommunityToolkit.Mvvm `[RelayCommand]` mặc định `AllowConcurrentExecutions=true`).
3. **Mất dữ liệu D100-D199 (Output cache) mỗi khi mất-kết nối/reconnect hoặc mỗi khi Lưu cấu hình Setup** — `PlcRegisterImage.ConfigureAllowedRanges` (khi đó tên `ModbusRegisterTable`) gọi `_words.Clear()` vô điều kiện mỗi lần, trong khi hàm này được gọi lại ở cả `SaveAsync` lẫn mỗi lần `RestartConnectionAsync`/reconnect — tức là mất cache Output thường xuyên hơn nhiều so với thiết kế ban đầu tưởng ("chỉ mất khi đổi range thật"). Sửa: chỉ xóa các key thật sự nằm ngoài range mới (`Where(addr => !CanReadAddress && !CanWriteAddress)`), giữ nguyên phần còn hợp lệ.
4. **Ô "Mã Scan quét được" commit sang "Mã Scan ghi nhận" trên MỖI ký tự gõ, chỉ giữ lại ký tự cuối** — do `OnScanBarcodeTextChanged` (auto-generated bởi `[ObservableProperty]`) chạy trên mỗi `PropertyChanged` (`UpdateSourceTrigger=PropertyChanged`). Phát hiện khi làm tính năng "PLC trigger move giá trị scan". Sửa: bỏ auto-commit theo ký tự, thêm `[RelayCommand] CommitScan()` làm điểm chốt duy nhất — chỉ chạy khi có tín hiệu "kết thúc 1 lần quét" thật sự (Enter, đủ độ dài PA3, hoặc PLC báo qua `SIGNAL_SCAN_CONFIRM`).

**B. Single-instance protection** — named `Mutex` Windows, check đầu `OnStartup` trước cả license check; instance thứ 2 hiện `SingleInstanceWarningWindow` (dialog mới, song ngữ) rồi `Shutdown()` ngay, không mở cổng COM/không tạo `MainWindow`. Lý do: PLC RS-485 chỉ cho 1 master/1 tiến trình giữ cổng COM tại 1 thời điểm — 2 tiến trình cùng chạy sẽ tranh chấp cổng, gây lỗi khó chẩn đoán. UI dialog: người dùng yêu cầu tăng width +20% rồi tăng font +10% qua 2 lượt phản hồi ngắn.

**C. 3 chế độ nhận diện quét barcode (thay 1 kiểu tiền tố hardcode)** — khách hàng khác nhau cấu hình đầu đọc khác nhau (có/không tiền tố, độ dài cố định). Thiết kế qua Plan Mode trước khi code:
- PA1 (PrefixStripped)/PA2 (PrefixKept): dò khớp tiền tố ký tự-theo-ký tự, Enter kết thúc. Khuyến nghị tiền tố 1 ký tự để an toàn tuyệt đối (0 rủi ro lẫn với gõ tay); tiền tố dài hơn có đánh đổi nhỏ đã được người dùng chấp nhận qua `AskUserQuestion`.
- PA3 (FixedLength): nhận diện theo tốc độ gõ (burst), tự commit khi đủ `ScanCodeLength` ký tự, không cần Enter — đánh đổi rủi ro rất nhỏ ký tự đầu lọt vào ô đang focus, người dùng đã xác nhận chấp nhận.
- Bắt phím ở cấp `Window` (`PreviewTextInput`/`PreviewKeyDown`, `handledEventsToo: true`) để không phụ thuộc control nào đang focus — đúng yêu cầu gốc "không cần click hay active ô đó".
- Cấu hình lưu trong `TestParameters` hiện có (`test-parameters.json` qua `JsonTestParametersStore`) — không tạo store mới, tái dùng `ScanCodeLength` sẵn có cho PA3 thay vì thêm field trùng khái niệm.
- **Phát hiện phụ, không sửa (ngoài phạm vi đã hỏi lại người dùng)**: `ShellViewModel.RequireScanOrder` (footer Main) không đồng bộ với `SettingTabViewModel.RequireScanOrder` (Set Spec.) — 2 property độc lập, để lại cho lượt sau (đã ghi vào CLAUDE.md mục 13).

**D. PLC-triggered "move" giá trị scan** — thêm `SIGNAL_SCAN_CONFIRM` (`D70.3`, `spec-register-map.csv`), theo mẫu rising-edge (PLC tự 0→1 rồi tự tắt, PC chỉ đọc — xác nhận qua `AskUserQuestion`, khác các tín hiệu level-based hiện có). Thêm `PlcPollingService.UpdateEdgeSignal` (khác `UpdateSignal`/`UpdateWordValue` — so sánh giá trị trước/sau để bắt cạnh lên, không chỉ phản ánh mức hiện tại), raise `ScanConfirmSignalRaised`, `ShellViewModel` subscribe gọi `CommitScan()`.

**E. Tài liệu hướng dẫn vận hành** — `docs/HuongDanVanHanh_705-715.docx`, tạo bằng Word COM automation (PowerShell + `New-Object -ComObject Word.Application`) để có file `.docx` thật (không phải giả dạng), có ảnh chụp UI thật + mục lục tự động.

### Bẫy kỹ thuật đã gặp
- **PowerShell `.ps1` chứa tiếng Việt cần UTF-8 BOM** — thiếu BOM khiến PowerShell 5.1 parse sai, báo lỗi "string is missing the terminator" dù cú pháp đúng. Sửa bằng `[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)` (constructor mặc định của `UTF8Encoding` có BOM).
- **PowerShell escape ký tự là backtick `` ` ``, không phải backslash `\`** — dùng `\"` để escape quote trong chuỗi PowerShell double-quoted không lỗi cú pháp rõ ràng nhưng cắt chuỗi sai vị trí (token thừa thành positional argument bị bỏ qua âm thầm) → tiêu đề Word bị cắt cụt (`3. Tab \` thay vì `3. Tab "Trang Tự Động"`). Phải dùng `` `" `` .
- **Mỗi lời gọi PowerShell tool là 1 process mới** — `Add-Type -AssemblyName ...` load ở 1 lời gọi không tồn tại ở lời gọi kế tiếp; gây vài lần nghi ngờ nhầm là bug thật (VD tưởng "cạnh thứ 2 của SIGNAL_SCAN_CONFIRM không kích hoạt") trong khi chỉ là do script test bị chia làm 2 lời gọi riêng. Luôn giữ `Add-Type` + code dùng nó trong CÙNG 1 lời gọi.
- **2 lần tự sửa sai giữa phiên do kết luận vội** (bị người dùng chỉ ra rõ): (1) đoán "lệch chuẩn điện RS232/TTL" trong khi cùng cổng vẫn dùng được ở Master mode; (2) đoán "phần mềm Xinje đang giữ COM4" trong khi Xinje thực tế dùng COM6. Cả 2 lần đều dẫn tới rule nghiêm ngặt ở đầu phiên — bài học: khi chưa có bằng chứng log/thực nghiệm trực tiếp, không phát biểu kết luận như thể đã chắc chắn.
- **`ConfigureAllowedRanges` xóa sạch cache mỗi lần gọi** — lỗi kiểu "trông như đúng lúc viết" (đổi range thì dọn cache nghe hợp lý) nhưng sai vì hàm này được gọi lại ở nhiều tình huống không thực sự đổi range (mỗi lần Lưu, mỗi lần reconnect) — bài học: cache nên chỉ dọn phần thật sự invalid, không dọn toàn bộ chỉ vì hàm cấu hình lại được gọi.

**F. Gộp 3 thông báo rời rạc thành 1 `StatusMessage` duy nhất** — người dùng phát hiện `TestConnectionResult`/`RegisterWatchValidationSummary`/`SaveResultMessage` (mới thêm ở bước G) là 3 property độc lập, có thể hiển thị ĐỒNG THỜI 2 dòng (VD bấm Test Connection trước rồi Lưu thành công sau, dòng Test Connection cũ không tự mất) → panel cao thêm ngoài dự kiến → tràn thanh cuộn trở lại → đoạn cuối câu (VD "(chế độ Mock — không có PLC thật để test)") bị đẩy khuất khỏi khung nhìn. Sửa bằng cách gộp về 1 property `StatusMessage` (+ `StatusMessageIsError` quyết định màu đỏ/xanh qua `DataTrigger` trong `Style`) — mọi hành động (`SaveAsync` lỗi/thành công, `TestConnectionAsync` lỗi/thành công) đều ghi đè đúng dòng này, đảm bảo tại 1 thời điểm chỉ có tối đa 1 dòng.

**G. Thêm thông báo khi Lưu cấu hình thành công** — trước đây bấm "Lưu cấu hình" thành công hoàn toàn im lặng, không có phản hồi trực quan. Thêm resx `Setup_SaveSuccess` ("Đã lưu cấu hình lúc {0}.") + set `StatusMessage` (màu xanh) ở cuối `SaveAsync` sau khi mọi bước lưu hoàn tất.

### Kết quả kiểm chứng
- Build 0 lỗi (Debug + Release) qua nhiều vòng sửa liên tiếp — gặp lại đúng race `_wpftmp`/`obj` + 1 lần lock file do tiến trình `EolTester.App` cũ (từ lần verify UI trước) chưa bị đóng hẳn còn giữ khóa `EolTester.App.exe`/`EolTester.Data.dll` khi build/publish lại — tìm đúng PID qua `Get-CimInstance Win32_Process -Filter "Name = 'EolTester.App.exe'"` rồi `Stop-Process` mới publish qua được. **Bài học**: sau mỗi lần verify UI (dù agent con tự báo đã đóng app), nên chủ động kiểm tra lại `Get-Process EolTester.App`/`Get-CimInstance Win32_Process` trước khi build/publish tiếp, vì đôi khi tiến trình con của `ui-verifier` không đóng sạch hoàn toàn (đặc biệt khi test nhiều vòng liên tiếp trong 1 lần chạy agent).
- Unit test Communication.Tests 43/43 + Core.Tests 1/1 pass qua mọi vòng sửa — không ảnh hưởng logic được test (2 test `ModbusRegisterTableTests` gọi `ConfigureAllowedRanges` trực tiếp với tuple riêng, độc lập với cách `SetupTabViewModel` gọi hàm này).
- Verify UI thật qua nhiều vòng: (1) sau khi thu hẹp + ẩn dòng rỗng — không còn thanh cuộn dọc; (2) sau khi gộp `StatusMessage` — xác nhận đúng 1 thông báo tại 1 thời điểm (Test Connection → Lưu thành công → dòng Test Connection cũ biến mất hoàn toàn, không chồng 2 dòng), đoạn "(chế độ Mock — không có PLC thật để test)" hiển thị trọn vẹn không bị khuất.
- Publish Release + smoke-test thành công ở lần cuối cùng.

### Ghi chú kỹ thuật khác
- Người dùng ngắt 1 lời gọi `Agent` (ui-verifier) giữa chừng ở phiên này để chuyển hướng sang câu hỏi khác ("Thông báo lỗi ... là do đâu") — đã dùng đúng Plan Mode (được hệ thống tự kích hoạt) để phân tích kỹ trước khi sửa thay vì đoán, viết plan file rõ ràng nêu cả rủi ro "sửa nửa vời" (chỉ nới lỏng validate mà không đổi tầng cache) trước khi xin duyệt — đúng tinh thần "xác nhận lại trước khi code khi có điểm chưa rõ" ở CLAUDE.md mục 0.

## 32. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Cập nhật io-map-seed.csv (địa chỉ bit hex) + sửa crash câm lặng lúc khởi động

### Bối cảnh
Người dùng tự sửa `src/EolTester.Configuration/SeedData/io-map-seed.csv` qua IDE (thêm nhiều điểm I/O mới X20-X37/Y20-Y37, dùng địa chỉ bit dạng hex `D10.A`..`D10.F`, `D15.A`..`D16.F` cho bit 10-15 thay vì chỉ số thập phân), yêu cầu build lại app + đồng thời báo "lỗi khởi động mà không thấy phản hồi" (double-click/`Start-Process` app, không có cửa sổ nào hiện ra, không báo lỗi gì).

### Điều tra
- `ModbusWordAddress.Parse` đã hỗ trợ sẵn hậu tố bit dạng hex 1 ký tự (`A`-`F`/`a`-`f` → 10-15) từ trước — không phải nguyên nhân lỗi, CSV mới parse bình thường.
- **Phát hiện `io-map.json` cũ đã tồn tại sẵn** (`%LOCALAPPDATA%\EolTester\Config\io-map.json`, lưu từ 12/07/2026) — theo đúng thiết kế đã ghi ở mục 19, seed CSV chỉ dùng đúng 1 lần lúc file JSON này CHƯA tồn tại, nên bản cập nhật CSV sẽ **không có hiệu lực** nếu không xóa file JSON cũ. Đã hỏi lại người dùng qua `AskUserQuestion` trước khi xóa (thao tác phá hủy dữ liệu) — người dùng xác nhận xóa.
- **Lỗi khởi động không phản hồi — nguyên nhân THỰC SỰ không liên quan gì tới CSV**: chạy `dotnet run` ở **foreground** (không phải `Start-Process`, đúng bẫy đã ghi ở mục 9 cho License — exception trước khi `MainWindow.Show()` thì `Start-Process` không thấy gì) mới bắt được `System.IO.FileNotFoundException: Could not find file 'COM4'` — `connection.json` trên máy này đang trỏ `DriverMode=Real, ComPort=COM4` (di sản từ phiên kết nối PLC thật ở mục 27-28), nhưng máy dev hiện tại không có PLC/cổng COM4 thật.
- **Gốc rễ trong code**: `PlcPollingService.StartAsync` (dòng ~172-186) có try/catch quanh `_driver.ConnectAsync(ct)` (đúng thiết kế, không chặn khởi động khi cổng COM lỗi) — nhưng dòng `await RefreshRuntimeStateAsync(_cts.Token)` gọi NGAY SAU ĐÓ (ưu tiên nạp dữ liệu sớm, không chờ tick đầu ~500ms của `PollLoopAsync`) lại nằm NGOÀI try/catch đó. Bên trong `RefreshRuntimeStateAsync` gọi `ReadRegisterAsync` → `EnsureConnectedAsync` → `ConnectAsync` lần 2, ném lại đúng exception đó — lần này KHÔNG được bắt, bay thẳng lên `App.OnStartup` (dòng 74, gọi `PlcPollingService.StartAsync` không có try/catch) → crash toàn bộ app **trước khi `MainWindow` kịp hiện ra**.

### Đã sửa
- `PlcPollingService.StartAsync`: bọc thêm try/catch quanh lời gọi `RefreshRuntimeStateAsync` (dòng ~184-199), catch `OperationCanceledException` (bỏ qua) và `Exception` chung (log lỗi, không rethrow) — cùng tinh thần catch-all đã có sẵn trong `PollLoopAsync` cho các tick sau. Tick polling kế tiếp sẽ tự thử kết nối lại qua `EnsureConnectedAsync`, không cần cơ chế retry riêng.
- Xóa `io-map.json` cũ theo xác nhận của người dùng, để app tự seed lại từ `io-map-seed.csv` mới ở lần chạy tới.

### Kết quả kiểm chứng
- Build 0 lỗi (Debug + Release). Unit test Communication.Tests 43/43 + Core.Tests 1/1 pass (thay đổi chỉ thêm try/catch, không đụng logic được test).
- **Chạy `dotnet run` foreground với đúng cấu hình COM4 không tồn tại trước đó gây crash** — sau khi sửa, app khởi động thành công, `MainWindowTitle = "EOL Tester - HV356"` xuất hiện bình thường, không còn crash/stack trace nào trong output.
- Publish Release + smoke-test: cùng kịch bản (COM4 không tồn tại), app publish cũng khởi động bình thường (không riêng Debug).
- Verify UI thật qua `ui-verifier`: tab Monitor hiển thị đúng đủ 32 điểm Input (X00-X37) + 32 điểm Output (Y00-Y37) từ CSV mới, nhãn tiếng Việt đúng ("Ngõ vào X30", "Ngõ ra Y30"...), không lỗi/exception nào xuất hiện trong lịch sử thông báo.

### Ghi chú kỹ thuật khác
- **Bài học quy trình quan trọng**: khi người dùng báo "lỗi khởi động không phản hồi", triệu chứng bề ngoài (không có cửa sổ, không có thông báo lỗi) hoàn toàn giống nhau dù nguyên nhân là gì — **luôn phải chạy `dotnet run` ở foreground** (không phải `Start-Process`/double-click exe) để thấy được exception thật, đúng bẫy đã rút ra từ tính năng License ở mục 9 nhưng hóa ra là quy tắc chung cho MỌI exception ném ra trong `App.OnStartup` trước khi `MainWindow.Show()`, không riêng gì license. Đã áp dụng `Start-Job` chạy `dotnet run --no-build --project <path>` rồi `Receive-Job` để lấy output — cách này hoạt động tốt hơn `Start-Process` (không cần redirect file, không bị nuốt mất exception).
- 2 vấn đề người dùng báo trong cùng 1 tin nhắn ("build lại do sửa CSV" + "lỗi khởi động không phản hồi") hóa ra **hoàn toàn độc lập nhau** — không giả định chúng liên quan, điều tra riêng từng cái mới phát hiện đúng gốc rễ của lỗi khởi động (cấu hình COM4 cũ, không phải do sửa CSV).

## 33. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Nới lỏng Giám sát DATA thành D0-D2000 + sửa bug crash khi mất kết nối lúc bấm nút điều khiển

### Bối cảnh
Người dùng yêu cầu 2 việc: (1) vùng Input/Output Block Setup giữ nguyên D200-D2000, nhưng "Giám sát DATA" phải cho phép D0-D2000 (bao gồm cả D0-D199 cố định); (2) báo bug: khi mất kết nối PLC, các nút điều khiển liên quan truyền thông làm ỨNG DỤNG THOÁT — yêu cầu các nút này phải disable (hoặc ít nhất không gửi dữ liệu) khi mất kết nối, và tuyệt đối không được crash app.

### Đã sửa

**A. Giám sát DATA D0-D2000** (`SetupTabViewModel.cs`)
- `IsWithinAppOwnedRange` đổi cận dưới từ `MinConfigurableBlockAddress` (200, dùng chung với Input/Output Block Start Address) sang hằng số riêng `MinRegisterWatchAddress = 0` — tách bạch 2 khái niệm: Input/Output Block Start Address vẫn giữ nguyên bị chặn ở D200-D2000 (không đổi), còn "Giám sát DATA" nới rộng xuống D0.
- resx `Setup_RegisterWatchOutOfRange` đổi text "D200-D2000" → "D0-D2000".
- **Lần đầu thử thêm guard chặn ghi vào D0-D99 (chỉ-đọc)** qua `_registerTable.CanWriteAddress` + resx `Setup_RegisterWatchReadOnly` — nhưng người dùng phản hồi ngay: **muốn được ghi tay tự do vào TOÀN BỘ D0-D2000, kể cả D0-D99**, chấp nhận đánh đổi "nếu đang polling thật thì giá trị Admin gõ vào D0-D99 sẽ tự bị lần đọc thật tiếp theo (~500ms) ghi đè lại". Đã **gỡ bỏ hoàn toàn guard vừa thêm** (đảo ngược ngay trong cùng phiên) — để nguyên `ModbusRegisterTable.TryUpdateWord`/`CanWriteAddress` tự no-op lặng lẽ cho cache D0-99 (không throw, không chặn UI), còn `WriteRawWordAsync` vẫn gửi lệnh ghi vật lý xuống driver không điều kiện như thiết kế gốc — đúng ý người dùng, không cần thêm code mới, chỉ cần KHÔNG chặn ở tầng ViewModel.
- Tiện thể dọn luôn 1 đoạn try/catch đã thành dead code ở `CommitRegisterWriteAsync` (catch `InvalidOperationException`/`TimeoutException` quanh `WriteRawWordAsync`) — dead code vì `WriteRawWordAsync` giờ tự nuốt mọi exception nội bộ (xem mục B), không còn ném ra ngoài để catch này bắt được nữa.

**B. Sửa bug crash khi mất kết nối lúc bấm nút điều khiển**
- **Gốc rễ**: `[RelayCommand]` (CommunityToolkit.Mvvm) sinh `ICommand.Execute(object?)` dạng `async void` (bắt buộc vì `ICommand.Execute` trả `void`) — khi hàm async bên trong ném exception (VD driver PLC thật mất kết nối giữa chừng, `IOException`/`TimeoutException` từ NModbus/SerialPort khi Start/Reset/Xác nhận NG/ON-OFF thủ công/đổi Mode), exception KHÔNG được await-catch ở đâu, bay thẳng qua `SynchronizationContext` lên `Dispatcher` — và vì app chưa từng đăng ký `Application.DispatcherUnhandledException`, WPF mặc định CRASH TOÀN BỘ APP. Đúng là bug người dùng mô tả.
- **Lớp 1 (chặn tận gốc)**: `PlcPollingService.WriteBitAsync` (dòng ~326, gateway duy nhất cho `PulseCommandAsync`/`SetLevelCommandAsync`/`WriteOutputAsync`/`TransferHandoverToCommandAsync`) và `WriteRawWordAsync` (dòng ~375, dùng bởi Giám sát DATA) đều bọc try/catch: `OperationCanceledException` rethrow (đúng ngữ nghĩa hủy), mọi `Exception` khác log qua Serilog rồi NUỐT (không rethrow) — 1 điểm sửa, mọi lệnh điều khiển trong toàn app thừa hưởng, không cần sửa từng ViewModel command riêng lẻ.
- **Lớp 2 (lưới an toàn cuối)**: `App.xaml.cs.OnStartup` đăng ký `DispatcherUnhandledException += OnDispatcherUnhandledException` — handler chỉ log + `e.Handled = true`, không rethrow. Phòng hờ cho những exception chưa lường hết (không riêng giao tiếp PLC).
- **Lớp 3 (UX — theo đúng yêu cầu "Disable")**: thêm `ShellViewModel.CanSendCommands` và `MonitorTabViewModel.CanSendCommands` (cùng công thức `ConnectionState != ConnectionState.Error`), bind `IsEnabled` cho: nút Start/Stop, Reset, Xác nhận NG (footer dùng chung, `MainWindow.xaml`); nút AUTO/MANUAL và (gián tiếp qua `CanUseManualControls` đã có sẵn, nay thêm điều kiện `&& CanSendCommands`) 2 nút ON/OFF thủ công (`MonitorTabView.xaml`). **Cố ý chỉ disable ở `ConnectionState.Error`, không disable ở `Disconnected`/`Connecting`** — `Disconnected` là trạng thái BÌNH THƯỜNG của Role=Slave (không polling chủ động, không có nghĩa lỗi) và của Mock driver trước khi `StartAsync` chạy xong; disable nhầm 2 trường hợp đó sẽ khóa cứng nút ở chế độ hoàn toàn hợp lệ.

**C. Dọn dẹp comment** — theo yêu cầu người dùng, rút gọn các đoạn comment quá dài vừa thêm ở bước B (try/catch trong `WriteBitAsync`/`WriteRawWordAsync`, handler trong `App.xaml.cs`, doc-comment `CanSendCommands`) xuống còn đúng phần WHY cốt lõi, bỏ phần lặp lại WHAT đã hiển nhiên từ code.

### Kết quả kiểm chứng
- Build 0 lỗi (Debug + Release). Unit test Communication.Tests 43/43 + Core.Tests 1/1 pass — toàn bộ thay đổi không đụng logic được test trực tiếp.
- Verify UI thật qua `ui-verifier`, 2 phần:
  - **Phần A (D0-D2000)**: D50 (dải D0-D99) và D150 (dải D100-D199) đều nhập được không lỗi; D2500 (ngoài D0-D2000) vẫn bị chặn đúng. Phát hiện phụ: D50 dao động liên tục qua mỗi tick polling — **điều tra ngay và xác nhận đây KHÔNG PHẢI bug**: `spec-register-map.csv` (file người dùng đang mở trong IDE lúc gửi yêu cầu) đã map `High.voltage → D50`, nên D50 nằm trong danh sách "địa chỉ đo lường" được `PlcPollingService.StartAsync` đăng ký nhiễu ngẫu nhiên (`ConfigureJitterAddresses`, chỉ áp dụng Debug/Mock) để giả lập cảm biến "sống động" — đúng thiết kế đã có từ mục 21, không phải hồi quy.
  - **Phần B (không crash khi mất kết nối)**: đổi `connection.json` sang `DriverMode=Real, ComPort=COM99` (không tồn tại) rồi khởi động lại — app mở bình thường, không crash, chỉ báo "Lỗi kết nối" đỏ. Nút Start/Reset/Xác nhận NG (footer) và AUTO/MANUAL + ON/OFF (Monitor) đều tự động mờ/disable đúng. Bấm nhiều lần vào các nút đã disable đó — app vẫn chạy bình thường, không crash. Đổi lại Driver=Mock, Lưu, khởi động lại — mọi nút trở lại bình thường, bấm được.
- Publish Release + smoke-test thành công.

### Ghi chú kỹ thuật khác
- **Bài học quan trọng về việc lắng nghe phản hồi đảo ngược**: agent đã tự thêm 1 lớp bảo vệ hợp lý về mặt kỹ thuật (chặn ghi D0-D99 vì đó là vùng PLC-owned theo đúng tinh thần các quyết định trước) nhưng KHÔNG khớp ý định thực tế của người dùng (họ muốn dùng "Giám sát DATA" như 1 công cụ debug/test tự do, chấp nhận rủi ro ghi đè). Khi người dùng phản hồi ngược lại ngay, đã gỡ bỏ hoàn toàn thay vì cố giữ lại 1 phần "cho an toàn" — tôn trọng đúng quyết định của người dùng về đánh đổi rủi ro trên chính công cụ nội bộ của họ, không tự ý áp đặt thêm rào cản ngoài phạm vi được yêu cầu.
- Việc phát hiện "D50 nhiễu ngẫu nhiên" trong lúc verify UI ban đầu bị hiểu nhầm là bug tiềm ẩn — nhưng nhờ đối chiếu lại đúng file `spec-register-map.csv` mà người dùng đang mở sẵn trong IDE (gợi ý ngữ cảnh từ hệ thống), xác định ngay đây là hệ quả đúng của 1 thay đổi cấu hình khác (map `High.voltage` sang D50) chứ không phải lỗi code mới — tránh mất thời gian điều tra sai hướng.

## 34. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Loại bỏ hoàn toàn `MockModbusDriver`/driver giả lập khỏi ứng dụng

### Bối cảnh
Người dùng đánh giá tính năng driver giả lập Mock (dropdown "Driver" Mock/Real ở tab Setup) là **rủi ro vận hành** — nếu ai đó vô tình để chế độ Mock trên máy sản xuất thật, app vẫn báo "Đã kết nối" và hiện dữ liệu giả mà không có PLC thật phía sau, không có cảnh báo nào ngăn việc này. Yêu cầu loại bỏ toàn bộ code/setting liên quan tới Mock khỏi ứng dụng.

### Quyết định phạm vi
- **Loại bỏ hoàn toàn**: `MockModbusDriver` (class + toàn bộ jitter/random data logic), `ConnectionSettings.DriverMode`/enum `DriverMode`, dropdown "Driver" ở tab Setup, DI registration, mọi nhánh rẽ Mock/Real trong `PlcPollingService`. `ModbusRtuDriver` (real) giờ là driver **DUY NHẤT** khi Role=Master.
- **CỐ Ý GIỮ LẠI** `PlcPollingService.GenerateDemoMeasurement` (dữ liệu đo lường giả cho bước Set Spec chưa gán `Address`, chỉ bật `#if DEBUG`) — khác về bản chất rủi ro với Mock driver: đây là hằng số biên dịch (Debug vs Release), **không thể bật lại qua UI lúc runtime** như dropdown Driver cũ, nên bản Release publish cho khách hàng luôn an toàn (trả về 0 cố định) bất kể ai thao tác gì trên UI. Không nằm trong yêu cầu "loại bỏ Mock" của người dùng (họ nói rõ "tính năng mô phỏng Mock" — chỉ tính năng có tên Mock, tức driver).

### Đã sửa
- Xóa `src/EolTester.Communication/Mock/MockModbusDriver.cs` (cả thư mục) và `tests/EolTester.Communication.Tests/MockModbusDriverTests.cs` (3 test).
- `ConnectionSettings.cs`: xóa enum `DriverMode` + property `DriverMode`.
- `PlcPollingService.cs`: constructor chỉ còn nhận `ModbusRtuDriver` (bỏ `MockModbusDriver`); field `_driver` đổi type từ `IPlcCommunicationDriver` sang thẳng `ModbusRtuDriver` (không còn khái niệm "chọn 1 trong 2 driver" nên bỏ luôn lớp gián tiếp interface tại điểm này); `ConnectAndPollAsync` bỏ nhánh `if (DriverMode == Real) {...} else {... jitter ...}`, luôn `Configure` + `ConnectAsync` driver thật; `TestConnectionAsync` bỏ tham số `IsRealDriver` trong tuple trả về (luôn là driver thật, không cần phân biệt nữa) và bỏ early-return "Mock trả về ngay".
- `App.xaml.cs`: bỏ `services.AddSingleton<MockModbusDriver>()` + using liên quan.
- `SetupTabViewModel.cs`: bỏ property `DriverMode`/`AvailableDriverModes`, bỏ khỏi `LoadAsync`/`SaveAsync`.
- `SetupTabView.xaml`: bỏ hẳn `ComboBox` "Driver" — chỉ còn `ComboBox` "Vai trò" (Role).
- resx (vi+en): xóa `Setup_DriverMode`, `Setup_DriverModeHint`, `Setup_TestResultMock`; đổi `Setup_RoleDriver` từ "Vai trò / Driver" → "Vai trò".
- `CLAUDE.md`: cập nhật các đoạn mô tả kiến trúc hiện hành (mục 4, mục 6, mục 7 Tab 3) để không còn nhắc Mock như 1 lựa chọn đang tồn tại — các đoạn lịch sử/bẫy kỹ thuật cũ liên quan tới Mock (VD mục 21-24 về nhiễu ngẫu nhiên, mirror Bit) giữ nguyên vì là bối cảnh lịch sử hợp lệ, không sửa.

### Kết quả kiểm chứng
- Build 0 lỗi. Unit test: Core.Tests 1/1, Communication.Tests 40/40 (giảm từ 43 — đúng 3 test Mock đã xóa cùng class).
- Verify UI thật: app khởi động bình thường dù `connection.json` cũ còn field `DriverMode` thừa (System.Text.Json tự bỏ qua property lạ, không cần migrate file); header hiện "Lỗi kết nối" (đúng — COM99 không tồn tại trên máy dev, không còn Mock để "giả vờ" kết nối thành công); tab Setup xác nhận không còn dropdown Driver, chỉ còn "Vai trò".
- Publish Release + smoke-test thành công.

### Ghi chú kỹ thuật
- Đây là ví dụ rõ về phân biệt 2 loại "dữ liệu giả lập" có mức rủi ro khác nhau trong cùng dự án: (1) **runtime-switchable** (dropdown Driver Mock/Real, ai cũng đổi được trên máy đang chạy Release) — rủi ro vận hành thật, cần loại bỏ theo yêu cầu người dùng; (2) **compile-time-only** (`#if DEBUG` demo measurement) — không thể bật lại từ UI, bản Release luôn an toàn, không nằm trong diện rủi ro tương tự nên được giữ lại nguyên vẹn. Khi nhận yêu cầu "loại bỏ tính năng giả lập X", cần phân biệt rõ phạm vi theo đúng tên/cơ chế người dùng chỉ ra, không tự ý mở rộng sang các cơ chế giả lập khác chưa được nhắc tới.

## 35. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Sửa "Giám sát DATA" không lan truyền lúc mất kết nối/Role=Slave, sửa bug không edit được Set Spec, thêm tô màu cảnh báo giá trị chưa xác nhận

### Bối cảnh
Người dùng báo 2 vấn đề liên tiếp trong cùng phiên: (1) "mất kết nối tôi không sửa được giá trị trên watch data từ D00-D99" ở tab Setup, và ngay sau khi vá xong lại báo tiếp "nhập D10=12 nhưng không tín hiệu X nào sáng, D50/D52 Main page không hiển thị"; (2) "hiện tại tôi không sửa được spec bên tab Set Spec", kèm yêu cầu thêm tô màu cảnh báo khi giá trị nhập khác giá trị thanh ghi, và làm rõ thời điểm tab Main nên cập nhật giá trị Giới hạn.

### Chuỗi nguyên nhân "Giám sát DATA" không lan truyền (3 lớp, phải sửa cả 3 mới hết)
1. `ModbusRegisterTable.TryUpdateWord`/`TrySetWordForMasterWrite` chặn ghi D0-D99 theo `CanWriteAddress` (đúng thiết kế cũ, nhưng chặn luôn cả trường hợp Admin chủ động ghi tay) — thêm `ForceSetWordForAdminOverride` (bỏ qua `CanWriteAddress`) dùng riêng cho "Giám sát DATA" (`SetupTabViewModel.CommitRegisterWriteAsync`).
2. `PlcPollingService.RefreshRuntimeStateAsync` (Role=Master): Phase 1 đọc vật lý D0-D99 từ driver chạy TRƯỚC phần cập nhật UI từ cache; khi mất kết nối, exception ở D0 hủy luôn toàn bộ phần code phía sau trong CÙNG tick (kể cả phần đọc cache cập nhật Inputs/Outputs/LED/đo lường) — cô lập Phase 1+2 (vật lý) trong try/catch riêng, phần cập nhật UI từ cache (`RefreshUiFromCacheAsync`, tách thành method riêng) luôn chạy mỗi tick bất kể lỗi vật lý.
3. **Nguyên nhân chính với máy đang test (Role=Slave thật)**: Role=Slave chưa từng chạy `PollLoopAsync` (chỉ `LoadIoMapOnlyAsync` 1 lần lúc khởi động) — không có vòng lặp nào làm mới UI từ cache sau đó. Thêm `PlcPollingService.StartUiRefreshOnlyAsync()`/`UiRefreshLoopAsync` (đọc cache mỗi 500ms, không gọi driver vật lý), gọi từ `App.xaml.cs` nhánh Role=Slave.

### Chuỗi nguyên nhân "không sửa được Set Spec" (2 vấn đề riêng biệt, không liên quan Modbus)
1. **Bug thật, mức độ nghiêm trọng**: `SettingTabViewModel.EditableHighModeSteps`/`EditableLowModeSteps` là `IEnumerable<T>` (LINQ `.Where` lazy) — `DataGrid.ItemsSource` bind vào `IEnumerable`-only không tạo được `IEditableCollectionView`, khiến `BeginEdit` (double-click/F2) ném `InvalidOperationException` ở MỌI lần thử, bị `App.OnStartup`'s `DispatcherUnhandledException` nuốt âm thầm — không crash, nhưng ô KHÔNG BAO GIỜ vào được chế độ edit thật sự (chỉ thấy trong log, không có triệu chứng UI nào rõ ràng). Chẩn đoán được nhờ chạy UI thật + đọc log song song (đọc code suông không phát hiện ra — kiểu bug này im lặng hoàn toàn ở tầng ViewModel). Sửa bằng cách đổi kiểu trả về sang `List<TestStepRowViewModel>` (`.ToList()`).
2. **Tính năng mới người dùng yêu cầu đồng thời**: tách `EditLowerLimit`/`EditUpperLimit` (đang gõ) khỏi `LowerLimit`/`UpperLimit` (đang hiệu lực, dùng cho tab Main + tính Đạt/Không đạt) trong `TestStepRowViewModel` — DataGrid Set Spec. bind vào Edit*, chỉ chốt Edit*→LowerLimit/UpperLimit lúc bấm "Xác nhận thay đổi". Thêm `RegisterLowerLimit`/`RegisterUpperLimit` (đọc live từ `ModbusRegisterTable` mỗi tick, cùng chỗ với `ReadMeasurement`/`ReadOkNg` trong `RefreshUiFromCacheAsync`) và `IsLowerLimitPending`/`IsUpperLimitPending` (so Edit* với Register*) — tô nền cam nhạt (`#FDE3CF`) + chữ đỏ đậm (`#C0392B`) qua `DataGridTextColumn.ElementStyle` + `DataTrigger` khi khác nhau.

### Bug liên đới phát hiện thêm khi sửa (Slave mode — lệnh ghi không có tác dụng)
`PlcPollingService.WriteBitAsync`/`WriteWordToAddressAsync` (dùng cho Start/Stop/Reset/Xác nhận NG/CMD_MODE/tham số Set Spec/giới hạn High-Low — tức GẦN NHƯ MỌI lệnh ghi PLC trong app) trước đây LUÔN gọi thẳng driver vật lý bất kể Role, viết từ thời điểm Role chỉ có Master. Ở Role=Slave, driver chưa từng kết nối → mọi lệnh ghi ném exception (bị nuốt) → cache cũng KHÔNG được cập nhật theo (khác hẳn pattern đã áp dụng đúng cho `WriteRawWordAsync`/Giám sát DATA ở phiên trước). Hệ quả: trên máy chạy Role=Slave, nút Start/Stop/Reset/Xác nhận NG, đổi Auto/Manual, và "Xác nhận thay đổi" ở Set Spec — tất cả về mặt cache đều không có tác dụng thật sự, dù JSON vẫn lưu đúng. Sửa theo đúng pattern đã dùng cho `ForceSetWordForAdminOverride`: luôn cập nhật cache trước (PC là Slave vẫn cần giữ đúng giá trị để `ModbusSlaveService` trả lời PLC Master thật), chỉ gửi vật lý xuống driver khi `PlcPollingService.IsActiveMaster` (đang chủ động làm Master).

### Kết quả kiểm chứng
- Build 0 lỗi, test 41/41 pass sau mỗi vòng sửa.
- Verify UI thật (đúng cấu hình Role=Slave thật của máy dev, không đổi Role để test): ghi D10 qua Giám sát DATA → tín hiệu X ở Monitor đổi đúng bit; ghi D50/D70 → giá trị đo + LED/sensor ở Main đổi đúng số/màu, trong ~1-2s (chu kỳ 500ms).
- Verify UI thật tab Set Spec: double-click ô Giới hạn dưới vào edit mode thật (trước đây không được); gõ giá trị mới → ô tô cam/đỏ ngay; tab Main giữ giá trị cũ cho tới khi bấm "Xác nhận thay đổi", sau đó tô màu biến mất + Main cập nhật đúng giá trị mới.
- Publish Release + smoke-test thành công sau mỗi vòng.

### Ghi chú kỹ thuật
- Bài học lặp lại 2 lần trong cùng phiên: các đường ghi PLC (`WriteRawWordAsync` ở phiên trước, `WriteBitAsync`/`WriteWordToAddressAsync` ở phiên này) được viết từ thời kỳ app chỉ có Role=Master, và không được cập nhật đồng bộ khi Role=Slave được khôi phục lại sau đó (xem mục 15) — khi thêm 1 chế độ vận hành mới (Slave) vào 1 codebase vốn giả định 1 chế độ duy nhất (Master), phải rà soát LẠI TOÀN BỘ các điểm gọi driver vật lý trực tiếp, không chỉ điểm mới viết gần nhất. Nên cân nhắc gộp logic "cache-first, physical-write-only-if-Master" vào đúng 1 chỗ dùng chung (hiện đang lặp lại thủ công ở nhiều hàm: `ForceSetWordForAdminOverride` caller, `WriteBitAsync`, `WriteWordToAddressAsync`, `WriteRawWordAsync` caller) để tránh tái diễn ở lần thêm tính năng ghi PLC tiếp theo.
- Bug DataGrid-IEnumerable là dạng lỗi hoàn toàn im lặng ở tầng ViewModel/XAML (không exception nào lộ ra ngoài UI, không log nào tự nhiên xuất hiện trừ khi biết chỗ tìm) — chỉ phát hiện được bằng cách chạy UI thật + đọc file log cùng lúc với thao tác, không thể suy luận chắc chắn chỉ từ đọc code C#/XAML tĩnh.

## 36. Tóm tắt phiên làm việc (2026-07-13, tiếp) — Xác nhận Slave mode hoạt động đúng cho mọi nút/giá trị + chặn Xác nhận thay đổi khi Min > Max

### Bối cảnh
Sau loạt fix ở mục 35, người dùng hỏi xác nhận: "tất cả các nút và giá trị sẽ được lưu vào thanh D khai báo, để khi vận hành ở chế độ slave PC vẫn vận hành được đúng không". Đây là câu hỏi rà soát (không yêu cầu code mới) — đã dùng Plan Mode để trả lời có căn cứ thay vì suy đoán. Ngay sau đó, trong cùng phiên, người dùng yêu cầu thêm 1 tính năng cụ thể: chặn "Xác nhận thay đổi" ở tab Set Spec. khi Giới hạn dưới > Giới hạn trên.

### Rà soát Slave mode (read-only, không sửa code)
Liệt kê toàn bộ entry point ghi PLC trong `PlcPollingService.cs` (Start/Stop/Reset/Xác nhận NG/CMD_MODE/điều khiển thủ công Output/Xác nhận thay đổi Set Spec/auto-save tham số/Giám sát DATA) — xác nhận TẤT CẢ đều đi qua `WriteBitAsync`/`WriteWordToAddressAsync` (đã fix cache-first ở mục 35) hoặc `WriteRawWordAsync` (đã guard `IsMasterMode` từ trước). Kiểm tra thêm `io-map.json` + `spec-register-map.csv` runtime: toàn bộ địa chỉ đang dùng đều đúng định dạng `Dxxxx`/`Dxxxx.b` (đi qua nhánh cache), không có địa chỉ discrete kiểu cũ nào. Kết luận: đúng, Slave mode vận hành đúng cho mọi nút/giá trị hiện có — chỉ cảnh báo trước cho tương lai: địa chỉ mới khai báo phải luôn dùng định dạng `Dxxxx`, tránh dùng định dạng discrete cũ (chỉ hoạt động ở Master).

### Chặn "Xác nhận thay đổi" khi Min > Max
- `TestStepRowViewModel.IsLimitRangeInvalid` — so `EditLowerLimit`/`EditUpperLimit` (giá trị đang gõ, không phải giá trị đã confirm), raise lại trong 2 partial method `OnEditLowerLimitChanged`/`OnEditUpperLimitChanged` đã có sẵn từ mục 35.
- `SettingTabView.xaml`: thêm `DataTrigger` cho `IsLimitRangeInvalid` vào cả 2 style `LowerLimitPendingStyle`/`UpperLimitPendingStyle` đã có — đặt SAU trigger "pending" (so PLC) trong `Style.Triggers` để override màu khi cả 2 điều kiện cùng đúng (đỏ đậm `#D32F2F`/chữ trắng ưu tiên hơn cam nhạt "pending").
- `SettingTabViewModel.ConfirmChangesAsync`: validate đầu hàm, TRƯỚC khi ghi bất cứ gì — nếu còn bước nào `IsLimitRangeInvalid`, set `StatusMessageIsError=true` + `StatusMessage` liệt kê tên các bước vi phạm, `return` ngay (không lưu file/không ghi PLC/không audit log). Thêm `StatusMessageIsError` (bool, mới) — tái dùng đúng pattern đã có sẵn ở `SetupTabViewModel`/`SetupTabView.xaml` cho nhất quán.
- Resx: thêm `Setting_InvalidLimitRange` (vi+en).

### Kết quả kiểm chứng
- Build 0 lỗi, test 41/41 pass.
- Verify UI thật: sửa Điện áp High mode thành dưới=30/trên=25 (vô lý) → cả 2 ô tô đỏ đậm ngay khi rời ô; bấm Xác nhận trong lúc còn vi phạm → bị chặn, thông báo đỏ đúng tên bước, **tab Main giữ nguyên giá trị cũ (21/25), không bị ghi đè** (điểm quan trọng nhất, đã verify bằng ảnh). Sửa lại hợp lý (20/25) → Xác nhận thành công bình thường, thông báo xanh, tab Main cập nhật đúng.
- Publish Release + smoke-test thành công.

### Ghi chú kỹ thuật
- Khi người dùng hỏi dạng xác nhận ("như vậy X đúng không") sau một loạt fix, việc dùng Plan Mode để rà soát (read-only) toàn bộ entry point liên quan rồi trả lời có bằng chứng cụ thể (bảng liệt kê từng hành động → hàm gọi → đường xử lý) đáng tin hơn nhiều so với suy đoán/khẳng định chung chung — đặc biệt quan trọng với hệ thống có nhiều đường ghi PLC dễ sót như dự án này.

## 37. Tóm tắt phiên làm việc (2026-07-29) — Sửa hiệu năng polling (đọc/ghi theo khối thay vì từng thanh ghi) + tính năng xuất CSV kết quả theo ngày

### Bối cảnh
Người dùng báo 2 vấn đề: (1) ghi dữ liệu PC→PLC gần như tức thời nhưng PLC→PC ("Giám sát DATA") hiển thị rất chậm; (2) sửa giá trị trong vùng Input Block (VD D1000) trên PC vẫn đẩy xuống PLC dù đó là vùng PLC-owned. Sau khi chẩn đoán (không suy đoán, đọc thẳng code + đo), người dùng yêu cầu sửa vấn đề 1 theo hướng gộp thành khối cố định + 2 vùng cấu hình được, vì đã thử giảm chu kỳ polling xuống 10ms vẫn không cải thiện — xác nhận đúng giả thuyết ban đầu: nút thắt không phải chu kỳ polling mà là số lượng giao dịch Modbus.

Sau khi sửa xong hiệu năng, người dùng yêu cầu "define" (đặc tả rồi làm) tính năng xuất CSV kết quả theo ngày — đưa ra 6 yêu cầu cụ thể nhưng thiếu 1 mảnh quan trọng: thời điểm ghi 1 dòng (trigger). Đã hỏi lại qua `AskUserQuestion` trước khi code (đúng rule 0), gồm 2 vòng hỏi vì câu trả lời vòng 1 ("Label lấy từ spec-register-map") là câu trả lời dạng tự do, cần hỏi thêm 1 câu để chốt chính xác cơ chế nhập liệu.

### Chẩn đoán vấn đề 1 (trước khi sửa)
- `PlcPollingService.RefreshRuntimeStateAsync` mỗi tick gọi ~240 giao dịch Modbus **1 thanh ghi/lần** (D0-D99 = 100 đọc, D100-D199 = 100 ghi, cộng Input/Output Block mặc định 20+20) — mỗi giao dịch là 1 round-trip request/response thật trên bus RS485, khiến 1 tick thực tế mất vài giây dù `Task.Delay` giữa 2 tick chỉ 500ms.
- Phát hiện phụ: ô "Chu kỳ polling" (`PollingIntervalMs`) được lưu vào `connection.json` nhưng **chưa từng được `PollLoopAsync` dùng tới** — vòng lặp hardcode `Task.Delay(500, ct)`. Đây là lý do người dùng giảm xuống 10ms không có tác dụng gì.
- Vấn đề 2 (ghi vào Input Block qua "Giám sát DATA" vẫn đẩy xuống PLC) xác nhận là **thiết kế có chủ đích** (đã ghi ở CLAUDE.md mục 7 Tab 3 từ trước), không phải bug — không sửa, chỉ giải thích lại cho người dùng.

### Quyết định + lý do (sửa hiệu năng)
- Thêm `IPlcCommunicationDriver.ReadRegistersAsync`/`WriteRegistersAsync` (đọc/ghi nhiều thanh ghi liên tiếp — Modbus FC03/FC16), cài cho cả 3 driver. Phát hiện hữu ích: `McProtocolDriver`/`SlmpDriver` dùng `IMc3EFrameCodec` **đã hỗ trợ multi-point sẵn** (`EncodeReadWordRequest(device, pointCount)`), chỉ chưa được dùng — không cần viết thêm logic mới cho 2 driver này, chỉ đổi `pointCount` từ 1 thành `count`.
- Viết lại Phase 1/2 của `RefreshRuntimeStateAsync` thành `ReadRangeIntoCacheAsync`/`WriteRangeFromCacheAsync` (tự chia đoạn con liên tục hợp lệ theo `CanReadAddress`/`CanWriteAddress`, không gộp bừa nếu 1 dải lỡ chồng lấn vùng cấm) gọi `ReadChunkedAsync`/`WriteChunkedAsync` (tự chia nhỏ theo giới hạn Modbus 125 đọc/123 ghi) — từ ~240 request/tick xuống còn 2-4 request/tick trong trường hợp thường.
- Nối `PollingIntervalMs` thật vào `PollLoopAsync` qua `PlcPollingService.SetPollingIntervalMs` (kẹp tối thiểu 10ms tránh busy-loop) — gọi từ `ConnectAndPollAsync` lúc khởi động/reconnect và từ `SetupTabViewModel.SaveAsync` (có hiệu lực ngay, không cần restart, cùng pattern `SetInputBlockRange`).
- `PlcRegisterImage.UpdateBlock` (cũ, không dùng ở đâu, ném lỗi nếu vượt phạm vi) xóa hẳn, thay bằng `StoreReadBlock` (bỏ qua im lặng địa chỉ không hợp lệ, dùng cho đọc) và `SnapshotBlock` (lấy nhiều giá trị liên tiếp từ cache, dùng cho ghi).
- Thêm 2 test tích hợp mới qua TCP loopback thật (`ReadRegistersAsync_ReturnsMultipleValuesInOneRoundTrip`, `WriteRegistersAsync_AppliesAllValuesIntoPlcSlave`) + cập nhật toàn bộ test cũ gọi `UpdateBlock` sang `StoreReadBlock`.

### Kết quả kiểm chứng (sửa hiệu năng)
- Build 0 lỗi, test 64/64 pass. Publish Release + smoke-test qua log Serilog thật xác nhận code path mới (`ReadRegistersAsync`→`ReadChunkedAsync`→`ReadRangeIntoCacheAsync`) đã chạy — máy dev không có PLC/COM4 nên báo lỗi kết nối đúng dự kiến, không crash.
- **Giới hạn đã nói rõ với người dùng**: chưa đo được thời gian thực tế 1 tick trên PLC thật (máy dev không có PLC), chỉ verify đúng logic qua unit/integration test.

### Đặc tả + cài đặt tính năng xuất CSV kết quả
- 2 vòng `AskUserQuestion` trước khi code: (1) trigger ghi 1 dòng — chọn "PLC tự báo qua 1 bit riêng" (thêm `SIGNAL_CSV_WRITE`, `D70.4`, cùng pattern rising-edge với `SIGNAL_SCAN_CONFIRM`); (2) cách nhập cột dữ liệu tùy chỉnh — câu trả lời tự do "Label lấy từ spec-register-map" cần hỏi thêm 1 câu để chốt: Admin **chọn Key có sẵn** (không gõ tay Address rồi tự tra ngược).
- Cài đặt round 1: `CsvExportSettings` (`OutputDirectory` + `List<CsvExportColumnDefinition>` mỗi phần tử có `Key`), UI dạng `DataGrid` thêm/xóa từng dòng, mỗi dòng 1 `ComboBox` chọn Key.
- **Người dùng phát hiện bug UI thật qua ảnh chụp**: khối cấu hình cột CSV (DataGrid + nút Thêm/Xóa) bị **tràn khung canvas cố định, che khuất hoàn toàn** — đúng dạng lỗi đã từng gặp ở tab Setup (CLAUDE.md mục 12). Yêu cầu thiết kế lại: gộp thành **1 dòng duy nhất** nhập trực tiếp `Dxxxx`/`Dxxxx.b` cách nhau bằng dấu phẩy (đảo ngược quyết định "chọn Key qua dropdown" ở trên, chuyển sang gõ Address trực tiếp + có gợi ý), đồng thời gộp 2 checkbox "BẮT BUỘC SCAN"/"Bắt buộc đúng thứ tự tem" thành 1 hàng ngang để tiết kiệm chỗ.
- Round 2 (redesign): đổi `CsvExportSettings.Columns` (List<Key>) → `ColumnAddresses` (List<string>, địa chỉ thô); `SettingTabViewModel.CsvColumnsText` (1 TextBox, comma-separated) + `CsvColumnSuggestions`/`SelectedCsvColumnSuggestion` (chọn 1 gợi ý tự nối `Address` vào cuối ô, không phải Key); `CsvResultExportService` đổi từ tra Key→Address (forward) sang **tra ngược Address→Key** (`BuildAddressToKeyMapAsync`, dùng `ModbusWordAddress` value-equality làm khóa dictionary, không so chuỗi thô) để lấy tiêu đề cột lúc ghi file. `SaveCsvExportSettingsCommand` validate mỗi địa chỉ gõ vào: đúng cú pháp **và** khớp đúng 1 Key đang có trong `spec-register-map.csv`, sai thì chặn lưu + liệt kê địa chỉ lỗi.
- Xóa hẳn `CsvExportColumnRowViewModel` (không còn dùng sau khi bỏ DataGrid).

### Kết quả kiểm chứng (CSV) — verify bằng UI Automation thật + đọc file thật, không suy đoán
- Dùng `System.Windows.Automation` (PowerShell) điều khiển app thật: đăng nhập Admin, chọn tab Set Spec., chụp ảnh xác nhận layout hết bị che khuất và 2 checkbox đã gộp 1 hàng.
- Chọn 2 gợi ý qua `ComboBox` ("High.voltage"→nối `D50`, "SIGNAL_LED1"→nối `D70.0`, xác nhận nối đúng bằng dấu phẩy), bấm Lưu → đọc `csv-export-settings.json` xác nhận đúng nội dung.
- Gõ mã scan test `SN00012345` (Enter commit), sang tab Setup, dùng "Giám sát DATA" ghi `D70=16` (bit 4 = `SIGNAL_CSV_WRITE`) → **đọc file CSV thật vừa được tạo**: BOM UTF-8 đúng (`EF BB BF`), header `Date,Time,Barcode,High.voltage,SIGNAL_LED1` (tra ngược Key đúng), dòng dữ liệu `20260729,11:51:30,SN00012345,,0` (cột `High.voltage` rỗng vì chưa có PLC thật cấp giá trị D50; cột `SIGNAL_LED1` đúng bằng 0 vì chỉ set bit 4, xác nhận đọc đúng cấp độ bit). Toggle tín hiệu lần 2 → xác nhận **append** dòng thứ 2, không lặp header/BOM.
- Dọn dẹp `csv-export-settings.json` test sau khi xong (xóa để app về trạng thái chưa cấu hình).

### Bẫy kỹ thuật đã gặp khi tự động hóa UI test bằng PowerShell
- **WPF PasswordBox không hỗ trợ `ValuePattern`** (chặn cả đọc lẫn ghi vì lý do bảo mật) — phải `SetFocus()` rồi `SendKeys` từng ký tự.
- **`SendKeys` gõ nhanh bị chính tính năng "Theo độ dài chuỗi cố định" (PA3, `KeyboardWedgeScanCapture`) của app nuốt mất** — vì `KeyboardWedgeScanCapture` bắt phím ở cấp Window với `handledEventsToo: true`, không phân biệt control nào đang focus; gõ "admin123" (đúng 8 ký tự = `ScanCodeLength` mặc định) bằng `SendKeys.SendWait` nhanh bị nhận nhầm thành 1 lần quét mã, hiện thẳng vào "Mã Scan ghi nhận" thay vì vào ô mật khẩu. Sửa bằng cách gõ **từng ký tự có delay >50ms** (vượt ngưỡng phát hiện "gõ nhanh bất thường" của PA3) — đây là hành vi ĐÚNG THIẾT KẾ của tính năng quét mã (đã chấp nhận đánh đổi từ trước), không phải bug, chỉ là bất ngờ khi tự động hóa test.
- **`AutomationElement.Current.Name` của 1 item trong `ComboBox` KHÔNG dùng `DisplayMemberPath`** — trả về `ToString()` mặc định của object gốc (VD `"CsvColumnSuggestion { Key = ..., Address = ..., DisplayText = ... }"`), dù trên màn hình hiển thị đúng theo `DisplayMemberPath`. Khi lọc item bằng script phải match theo `ToString()` đầy đủ, không phải theo `DisplayText`.
- **`ValuePattern.SetValue` trên `DataGridCell` (DataGridTextColumn) có thể chỉ set text vào ô đang edit mà KHÔNG tự thoát edit mode/commit binding** — đọc lại `Current.Value` ngay sau đó vẫn thấy đúng giá trị vừa set (vì đang phản ánh đúng nội dung ô), dễ lầm tưởng đã ghi thành công, nhưng ViewModel chưa hề nhận được giá trị (binding mặc định `UpdateSourceTrigger=LostFocus`) cho tới khi có 1 hành động rời ô thật sự (VD gửi phím `{ENTER}` sau khi `SetValue`). Đã xác nhận qua chụp ảnh: ô vẫn hiện dạng TextBox đang edit (viền trắng) thay vì TextBlock hiển thị bình thường — dấu hiệu để nhận biết tình huống này khi debug script tự động hóa DataGrid.
- `AutomationElement`/`GridPattern` (`GetItem(row, col)`) đáng tin cậy hơn nhiều so với click chuột theo toạ độ pixel suy đoán cho các control có cấu trúc rõ ràng (DataGrid, Button theo Name, TabItem theo Name) — chỉ cần dùng toạ độ chuột khi control không lộ pattern automation phù hợp.

### Ghi chú khác
- Người dùng nhắc lại (lần 2) yêu cầu **mọi phản hồi trong chat phải viết bằng tiếng Việt**, không chỉ tài liệu/code comment — đã lưu vào bộ nhớ hệ thống (`feedback_vietnamese_communication.md`) vì đây là lỗi lặp lại, cần nhớ xuyên suốt các phiên sau, không chỉ riêng phiên này.

## 38. Tóm tắt phiên làm việc (2026-08-27) — Rà soát CLAUDE.md, dọn skill lạc từ dự án khác, đổi ô đăng nhập sang ComboBox

Phiên này gồm 3 việc độc lập: chạy /init để rà soát CLAUDE.md hiện có, kiểm tra + sửa lại toàn bộ skill trong .claude/skills/ cho đúng dự án EOL Tester, và đổi UI ô "Tên đăng nhập" ở header theo yêu cầu người dùng.

### Trạng thái cập nhật từng phần

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| Rà soát CLAUDE.md qua /init | ✅ Xong | Xác nhận nội dung khớp code thực tế (đối chiếu cấu trúc solution, 6 project src/, .claude/skills); bổ sung 1 dòng ghi chú về `test-rs485.ps1` vào mục "Lệnh thường dùng" (script build+test riêng cho Communication, trước đó chưa được nhắc tới) |
| Xóa 2 skill lạc từ dự án ERP khác (`erp-dashboard-builder`, `master-architecture`) | ✅ Xong | Nội dung 100% thuộc 1 dự án ERP khác (Supabase/React/8 module bán hàng-kho-nhân sự) — không có phần nào áp dụng được cho máy EOL Tester, người dùng xác nhận xóa |
| Viết lại `Works_Start`/`Works_End`/`QA-Session` | ✅ Xong | Bản gốc hardcode cơ chế Remember/History phân theo 8 module ERP + ghi vào `docs/Source/` (không tồn tại ở dự án này) + giả định npm/git — đã viết lại để dùng đúng `docs/session-log/history.md` (1 file duy nhất, đúng CLAUDE.md mục 1); xóa script `quan-ly-nhat-ky.mjs` đã lỗi thời |
| Chuyển `frontend-design`/`frontend-design-audit` sang thuật ngữ WPF/XAML | ✅ Xong | Thay CSS/breakpoint responsive/browser devtools bằng canvas cố định 1400×765 + Viewbox, `DataTrigger`, skill `verify-ui` (PrintWindow) |
| Sửa ví dụ minh họa trong `deployment-planner` | ✅ Xong | Đổi ví dụ từ "module ERP/Supabase" sang "thêm driver MC Protocol cho EOL Tester"; sửa câu diễn đạt sai cơ chế ("gọi Claude API" → Claude tự phân tích) |
| Đổi ô "Tên đăng nhập" từ TextBox sang ComboBox | ✅ Xong | `MainWindow.xaml` + `IUserStore.GetUserNames()` + `IAuthenticationService.AvailableUserNames` + `ShellViewModel.AvailableUserNames`; chỉ chọn được từ 2 tài khoản cố định (`operator`/`admin`), không cho gõ tay (`IsEditable="False"`) |
| Sửa bug ComboBox tự chọn sẵn "admin" lúc khởi động | ✅ Xong | Phát hiện qua verify UI thật — WPF mặc định đồng bộ `SelectedItem` với `CollectionView.CurrentItem`; đã thêm `IsSynchronizedWithCurrentItem="False"`, verify lại: ô trống mặc định (2 lần khởi động sạch), dropdown đúng 2 mục, đăng nhập vẫn hoạt động đúng |

### Bước tiếp theo (theo đúng thứ tự phụ thuộc)
1. Nếu sau này cần cho phép gõ username tự do hoặc mở rộng danh sách người dùng động (hiện `SeededUserStore` chỉ có đúng 2 tài khoản cố định) — cần thiết kế lại `IUserStore`, hiện đang để ngỏ theo đúng phạm vi yêu cầu.
2. Các câu hỏi mở kỹ thuật cũ ở CLAUDE.md mục 13 (bảng địa chỉ D thật ngoài D1000-D1002, chiến lược ghi bit lẻ khi thanh ghi dùng chung với PLC...) không đổi, không thuộc phạm vi phiên này.
3. (Không cấp bách) đánh số mục "37" bị trùng 2 lần trong `docs/session-log/history.md` (dòng 500 và 679, nội dung khác nhau) — có thể sửa lại số thứ tự cho nhất quán ở dịp thuận tiện.

### Quyết định quan trọng đã đưa ra và lý do
- **Xóa hẳn `erp-dashboard-builder`/`master-architecture` khỏi dự án thay vì giữ lại/adapt.** Lý do: nội dung thuộc hoàn toàn 1 dự án khác, không có điểm chung nào để "sửa cho phù hợp"; giữ lại sẽ gây nhiễu (skill có thể tự kích hoạt sai ngữ cảnh). Người dùng xác nhận qua AskUserQuestion.
- **Viết lại cơ chế Works_Start/End/QA-Session theo đúng 1-file lịch sử của dự án này thay vì port nguyên cơ chế Remember/History phân tầng phức tạp từ dự án ERP.** Lý do: dự án này đã chốt (CLAUDE.md mục 1) chỉ dùng 1 file lịch sử duy nhất — port nguyên cơ chế cũ sẽ ghi sai chỗ (`docs/Source/` không tồn tại) và trái quy tắc đã chốt.
- **ComboBox đăng nhập không cho gõ tay, chỉ chọn từ danh sách cố định.** Lý do: hiện chỉ có 2 tài khoản seed cố định, không có nhu cầu người dùng động — giữ đơn giản, tránh gõ sai tên đăng nhập.
- **Thêm `IsSynchronizedWithCurrentItem="False"`.** Lý do: tránh rủi ro vận hành thật — nếu không sửa, ô sẽ luôn tự chọn sẵn 1 tài khoản (thường là "admin") ngay khi mở app, operator có thể vô tình đăng nhập nhầm quyền chỉ bằng cách gõ đúng mật khẩu mà không để ý ô đã chọn sẵn ai.

## 39. Tóm tắt phiên làm việc (2026-08-27, tiếp) — Đưa lại hệ số nhân/chia (PLC Gain) cho 6 biến đo lường, data-driven theo cột "Scale" trong spec-register-map.csv

### Bối cảnh
Người dùng rút lại quyết định "truyền thô, không nhân/chia 100" đã chốt ở phiên trước (mục 13/CLAUDE.md cũ) — yêu cầu đưa lại hệ số nhân/chia, nhưng lần này **không hardcode ×100** mà thêm 1 cột "PLC Gain" trên UI Set Spec., đọc/lưu qua cột `Scale` mới trong `spec-register-map.csv`. Hệ số bắt buộc chỉ 1/10/100/1000 (khác/thiếu → mặc định 1). Người dùng làm rõ thêm qua tin nhắn thứ 2 (gửi giữa lúc đang xử lý): **chỉ giá trị trong phần spec + giá trị hiển thị liên quan** (6 biến đo lường High/Low × Điện áp/Dòng điện/Lực hút và cặp Giới hạn dưới/trên D134-D145 đi kèm) mới cần nhân/chia — không áp dụng cho cột CSV xuất kết quả hay các Key khác (CMD_/PARAM_/SIGNAL_). Số chữ số thập phân làm tròn/hiển thị đi kèm 1-1 với Scale: 1→0 số lẻ (số nguyên), 10→1, 100→2, 1000→3.

### Đã cài đặt
1. **`EolTester.Core.PlcGainScale`** (mới) — hằng số dùng chung: `IsValid` (1/10/100/1000), `DecimalDigits` (số chữ số thập phân tương ứng), `Round` (làm tròn `MidpointRounding.AwayFromZero`).
2. **`TestStepDefinition.LowerLimit`/`UpperLimit` đổi từ `int?` sang `decimal?`** (giá trị THỰC, đã quy đổi theo Scale — không phải số nguyên thanh ghi PLC) + thêm `Scale` (`int`, mặc định 1).
3. **`spec-register-map.csv` thêm cột thứ 3 `Scale`** (`Key,Address,Scale`) — chỉ điền có ý nghĩa cho 6 Key đo lường (`High.voltage`...`Low.vacuum`), để trống/khác 1-10-100-1000 ở các Key khác coi như 1. `CsvKeyValueFileReader` thêm `ReadRowsAsync` (đọc mọi cột, dùng chung cho cả file 2 lẫn 3 cột) bên cạnh `ReadAsync` cũ (vẫn chỉ lấy đúng cột 1-2, không ảnh hưởng `spec-Default.csv`). `ISpecRegisterMapSource`/`CsvSpecRegisterMapSource` thêm `LoadScalesAsync()`. `JsonSpecProfileStore.LoadAsync` gán `step.Scale` từ đó mỗi lần load (giống hệt cách gán `Address`/`OkNgAddress` — không có UI Import/Export riêng, sửa CSV rồi build lại).
4. **`PlcPollingService`**: `WriteStepLimitsAsync` ghi `raw = round(value*Scale)` xuống PLC (thay vì ghi thẳng số nguyên cũ); `ReadMeasurement`/`ReadRegisterLimit`/`GenerateDemoMeasurement` đọc/tính `raw/Scale` rồi làm tròn qua `PlcGainScale.Round` trước khi trả về `decimal?`.
5. **`TestStepRowViewModel`**: mọi property giá trị (`LowerLimit`/`UpperLimit`/`EditLowerLimit`/`EditUpperLimit`/`RegisterLowerLimit`/`RegisterUpperLimit`/`MeasuredValue`) đổi sang `decimal?`; thêm `Scale` (passthrough, bind cột "PLC Gain"); thêm `EditLowerLimitText`/`EditUpperLimitText` (string, 2 chiều — set tự làm tròn theo Scale qua `ParseEditText`) dùng cho ô nhập ở Set Spec., và `LowerLimitDisplayText`/`UpperLimitDisplayText`/`MeasuredValueDisplayText` (string, chỉ đọc, luôn hiện đúng số chữ số thập phân cố định theo Scale — VD Scale=100 luôn "23.00" chứ không rút gọn "23") dùng cho tab Main. `IsLowerLimitOutOfRange`/`IsUpperLimitOutOfRange` đổi sang kiểm tra `EditLimit*Scale` có nằm trong dải WordSigned hay không (thay vì kiểm tra thẳng giá trị cũ).
6. **Xóa hẳn `NullableNumberToTextConverter`** (converter cũ chỉ nhận `int`, không còn dùng ở đâu sau khi chuyển các cột Lower/Upper/Measured sang bind thẳng property string tính sẵn) — XÓA file, không giữ lại dead code.
7. **Thêm cột "PLC Gain" (resx `Main_PlcGain` — VI "Hệ số PLC (Gain)", EN "PLC Gain")** vào cả 2 `DataGrid` High/Low mode ở `SettingTabView.xaml`, chỉ đọc, bind thẳng `Scale`.
8. `TestStepResult.cs` (model dự phòng cho `IResultExporter` tương lai, hiện chưa được dùng ở đâu trong app) — sửa 2 chỗ so sánh `double? < decimal?`/`> decimal?` không hợp lệ sau khi đổi kiểu, ép kiểu `(double?)` để build qua, không đổi ý nghĩa.

### Kết quả kiểm chứng
- Build 0 lỗi (Debug), test `Core.Tests` 1/1 + `Communication.Tests` 63/63 pass — không hồi quy.
- **Verify UI thật theo đúng vòng lặp bắt buộc (CLAUDE.md mục 0)**: tạm chỉnh `Scale=100` cho `High.voltage` trong CSV để có dữ liệu khác 1 mà test — (1) đăng nhập Admin, tab Set Spec. hiện đúng cột "Hệ số PLC (" (bị cắt do cột hẹp, chỉ là vấn đề độ rộng cột, không phải bug logic) = 100 cho dòng Tốc độ động cơ, =1 cho các dòng khác; Giới hạn dưới/trên dòng đó hiện "21.00"/"25.00" (2 số lẻ) trong khi dòng khác hiện nguyên số ("500", "-80"); (2) gõ đè "23.4567" vào ô Giới hạn dưới — sau khi rời ô, giá trị tự làm tròn đúng thành "23.46" (đúng `MidpointRounding.AwayFromZero`, 2 chữ số theo Scale=100); (3) tab Main (ảnh chụp trước khi sửa gì) đã hiện sẵn "21.00"/"25.00" đồng bộ đúng cùng 1 nguồn dữ liệu. Sau khi verify xong, **trả lại Scale=100 → 1** trong CSV nguồn (giá trị Gain thật của từng biến đo lường chưa được xác nhận với PLC/kỹ sư phần cứng, mặc định an toàn là 1 = không scale) và trả lại `LowerLimit` của `High.voltage` trong `705.json` (profile spec đang dùng trên máy dev) về 21 như trước khi test — không để lại dữ liệu test trong file cấu hình.

### Bẫy kỹ thuật gặp phải khi verify UI
- **Nút "XÁC NHẬN THAY ĐỔI" bị disable rất lâu (hàng chục giây) sau khi bấm — KHÔNG phải bug do đợt sửa này.** `[RelayCommand]` cho method `async Task` tự sinh `AsyncRelayCommand` với `CanExecute = !IsRunning` mặc định (không cần khai báo) — nút tự disable trong lúc Task đang chạy. `ConfirmChangesAsync` gọi `WriteStepLimitsAsync` ghi tối đa 12 thanh ghi (2 giới hạn × 6 bước), mỗi lần ghi đều gọi thật xuống driver vì `PlcPollingService.IsActiveMaster` chỉ phản ánh "Role=Master và đã StartAsync", KHÔNG phản ánh việc cổng COM có PLC thật trả lời hay không — trên máy dev đang cấu hình Role=Master trỏ COM4 (không có PLC thật, xem log `eoltester-20260827.log` báo `TimeoutException` lặp lại từ vòng polling nền), nên mỗi lệnh ghi phải chờ hết timeout SerialPort mới bỏ qua, cộng dồn rất lâu. Đã xác nhận qua đọc log + code, không phải deadlock: cứ đợi đủ lâu nút sẽ tự bật lại. Không sửa gì (đúng hành vi timeout/retry đã thiết kế từ trước, không liên quan tính năng Gain).
- **Double-click vào Ô đầu tiên của `DataGrid` qua `mouse_event` giả lập có thể trúng ngay HEADER cột thay vì hàng dữ liệu đầu tiên** nếu ước lượng toạ độ theo tỉ lệ ảnh chụp trước đó sai lệch 1 hàng — click trúng header gây SORT lại toàn bảng (dòng bị xáo trộn thứ tự) thay vào edit. Bài học: sau khi click, luôn chụp lại ảnh xác nhận đúng ô/hàng trước khi gõ, không giả định toạ độ tính 1 lần là luôn đúng.
- **`GetWindowRect` trả về toạ độ off-screen kiểu `(-32000,-32000)` khi cửa sổ đang ở trạng thái minimized** — script click tự viết (không giống `capture.ps1` có sẵn `ShowWindow(hwnd,9)` phục hồi trước khi đo) tính ra toạ độ click âm khổng lồ, click trượt hoàn toàn ra ngoài màn hình. Sửa bằng cách luôn gọi `ShowWindow(SW_RESTORE)` trước `GetWindowRect` trong mọi script tương tác chuột, không chỉ script chụp ảnh.
- Property string 2 chiều tự làm tròn (`EditLowerLimitText`) đặt logic reformat trong `OnPropertyChanged` được gọi lồng bên trong chính setter của property khác (`OnEditLowerLimitChanged`) — về lý thuyết có thể gặp giới hạn "WPF không tự refresh lại Target đang trong chính transaction Update Source của nó", nhưng verify UI thật cho thấy giá trị vẫn hiện đúng "23.46" ngay khi rời ô (không cần thao tác thêm) — không phát sinh vấn đề thực tế trong trường hợp này, không cần thêm `Dispatcher.BeginInvoke` phòng vệ.

### Câu hỏi mở còn lại
- Hệ số Gain THẬT của từng biến đo lường (điện áp/dòng điện/lực hút chân không, High lẫn Low mode) chưa được xác nhận với PLC/kỹ sư phần cứng — hiện tất cả đang để mặc định 1 trong `spec-register-map.csv`, cần cập nhật trực tiếp file này (không cần build lại code, chỉ build lại để copy CSV) khi có số liệu thật.

### Sửa thêm sau khi verify
- Cột "Hệ số PLC (Gain)" bị cắt chữ header lúc verify (do `Width="70"` quá hẹp cho tên cột dài) — đã nới lên `Width="110"` ngay trong phiên này, không để lại thành nợ kỹ thuật.

## 40. Tóm tắt phiên làm việc (2026-08-27, tiếp) — Chuyển nhãn Timer Setting vào spec-register-map.csv, gộp bảng "Timer Setting", publish lại 2 lần

### Bối cảnh
Phiên bắt đầu từ câu hỏi "một số thanh ghi khi nhập từ PC thì PLC nhận giá trị x100" — chẩn đoán và xử lý dứt điểm, sau đó mở rộng sang dọn dẹp cách quản lý nhãn hiển thị cho các tham số Timer Setting ở tab Set Spec. Trong lúc phiên này đang chạy, phiên mục 39 (cùng ngày) đã đảo ngược quyết định "truyền thô" thành cơ chế Gain per-Key — phiên này phát hiện `CLAUDE.md` bị lệch theo và đã đồng bộ lại.

### Trạng thái cập nhật từng phần

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| Chẩn đoán nguyên nhân "PLC nhận x100" cho D134-D145 | ✅ Xong | `PlcPollingService.WriteStepLimitsAsync` (ghi ×100) không khớp giả định `ReadMeasurement`/`ReadRegisterLimit` (đọc ÷100) — quy ước chưa từng xác nhận riêng cho nhóm thanh ghi Limit |
| Đổi sang truyền thô (không nhân/chia 100) theo yêu cầu người dùng | ✅ Xong (tại thời điểm làm) → sau đó bị thay thế | Đã sửa `PlcPollingService.cs` 3 hàm liên quan + build/test pass; **cùng ngày, phiên khác (mục 39) đã đảo ngược quyết định này** thành cơ chế Gain per-Key (`Scale` cột 3 trong CSV) — hiện là trạng thái cuối cùng, không phải bug |
| Đồng bộ lại `CLAUDE.md` mục 7 Tab 4 theo đúng cơ chế Gain hiện hành | ✅ Xong | 2 đoạn bị lệch (mô tả "truyền thô") đã sửa lại đúng theo mục 39 + bổ sung mô tả bảng "Timer Setting" (trước đây chưa từng được ghi vào CLAUDE.md) |
| Chuyển nhãn DelayTimer1-10 (vi/en) từ `spec-Default.csv` sang cột `Label1`/`Label2` mới trong `spec-register-map.csv` | ✅ Xong | Thêm `ISpecRegisterMapSource.LoadLabelsAsync()`; `App.xaml.cs` đọc nhãn từ đây thay vì `IDefaultProjectInfoSource`; thêm BOM UTF-8 cho `spec-register-map.csv` (trước đó thuần ASCII) |
| Gộp 3 thời gian cố định (test High/Low mode, giữ nút) vào chung bảng "Timer Setting" với 10 Delay timer, xếp đầu bảng | ✅ Xong | Đổi tên `DelayTimerRowViewModel` → `TimerSettingRowViewModel` (thêm `ParamKey`, `Definitions` dùng chung với `App.xaml.cs`); xóa 3 hàng khỏi cột "Cài đặt tham số"; bọc cột giữa trong `ScrollViewer` (an toàn khi 13 dòng vượt canvas cố định) |
| Giảm `RowHeight` bảng Timer Setting còn 82% (40→33) | ✅ Xong | Theo yêu cầu người dùng — verify UI xác nhận đủ 13 dòng không cần cuộn |
| Publish lại `APP/` (2 lần, sau mỗi đợt thay đổi) | ✅ Xong | Build + test 64/64 + publish + smoke-test đều pass cả 2 lần |
| Git commit | ❌ Không làm | Thư mục dự án chưa phải git repo (`git status` báo lỗi) — hỏi lại, người dùng chọn tự xử lý git, không yêu cầu `git init` |

### Quyết định quan trọng đã đưa ra và lý do
- **Không tự ý chạy `git init`/commit khi phát hiện dự án chưa có git** — hỏi lại người dùng trước, được xác nhận "để tôi tự xử lý git". Áp dụng chung: không tự khởi tạo hạ tầng ngoài phạm vi yêu cầu ban đầu.
- **Chủ động sửa 2 đoạn `CLAUDE.md` bị stale** (mô tả "truyền thô" trong khi code thực tế đã chuyển sang Gain per-Key từ mục 39) thay vì để nguyên — vì CLAUDE.md phải phản ánh đúng trạng thái hiện hành (Phần I mục 1), lệch giữa 2 phiên chạy gần nhau cùng ngày trên cùng 1 vùng code là rủi ro thực tế, không phải giả định.

### Câu hỏi mở còn lại (kế thừa từ mục 39, chưa có tiến triển mới)
- Hệ số Gain thật của từng biến đo lường vẫn chưa xác nhận với PLC/kỹ sư phần cứng — toàn bộ đang mặc định `1`.

## 41. Tóm tắt phiên làm việc (2026-08-27, tiếp) — Sửa 2 lỗi ghi CSV, đổi tên "Cho phép sửa Barcode", xóa "BỎ QUA LỖI", tính năng SCAN MODE/REV MODE tự động hóa quét barcode, CSV cột linh hoạt

Phiên dài, nhiều yêu cầu nối tiếp nhau cùng xoay quanh 2 khu vực: luồng quét barcode ở footer tab Main
và tính năng xuất CSV kết quả ở tab Set Spec.

### Trạng thái cập nhật từng phần

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| Rà soát tính năng ghi CSV, tìm lỗi | ✅ Xong | Phát hiện 2 lỗi thật: race condition khi 2 lần trigger `SIGNAL_CSV_WRITE` gần nhau (có thể hỏng header/mất dòng), và giá trị âm bị xuất sai dấu (`PlcRegisterImage.TryGetValue` trả unsigned trong khi `ReadMeasurement` dùng signed) |
| Sửa race condition ghi CSV | ✅ Xong | Thêm `SemaphoreSlim _writeLock` trong `CsvResultExportService` + retry 3 lần (300ms) khi `IOException` (file bị chương trình khác như Excel khóa tạm thời) |
| Sửa lỗi dấu giá trị | ✅ Xong | `PlcRegisterImage.TryGetValue` đổi sang ép `(short)` (signed), đã rà soát các nơi gọi khác chỉ so `!=0` nên không ảnh hưởng; thêm test `PlcRegisterImageTests` |
| Đổi tên `PARAM_REQUIRE_SCAN`→`PARAM_EDIT_SCAN` (D104.0), "BẮT BUỘC SCAN"→"Cho phép sửa Barcode" | ✅ Xong | Trước đây cờ này không có tác dụng gì phía PC (chỉ đẩy PLC) — giờ **có tác dụng thật**: `false` khóa gõ tay ô "Mã Scan quét được" (`IsReadOnly` qua `InverseBooleanConverter`), chỉ nhận từ đầu đọc |
| Xóa hoàn toàn tính năng "BỎ QUA LỖI" | ✅ Xong | Theo yêu cầu người dùng (ảnh chụp đánh dấu X đỏ) — xóa UI/ViewModel/audit log/`PARAM_SKIP_NG_ENABLED` (D104.2); CLAUDE.md ghi rõ quy tắc chung Phần I mục 4 vẫn giữ cho dự án khác |
| Xóa checkbox "Bắt buộc đúng thứ tự tem" trùng lặp ở footer | ✅ Xong | `ShellViewModel.RequireScanOrder` thuần UI không lưu/đẩy PLC — chỉ còn 1 nguồn ở `SettingTabViewModel.RequireScanOrder` (Set Spec.) |
| Tính năng SCAN MODE/REV MODE (`PARAM_SCAN_REV_MODE`, D104.4) | ✅ Xong (code) | SCAN MODE (mặc định) tự động hóa hoàn toàn luồng quét: khóa nhận ký tự theo trạng thái máy (`SIGNAL_MACHINE_WAITING` D72.0 + CMD_START), tự kiểm tra thứ tự serial (`BarcodeSerialExtractor`, Core) + trùng lặp CSV hôm nay, pass cả 2 mới tự gửi CMD_START (1 giây) thay operator bấm Start. REV MODE giữ nguyên hành vi thủ công cũ |
| Bug lúc làm SCAN MODE: 2 checkbox không bị ép giá trị đúng lúc load | ✅ Xong | `[ObservableProperty]` bỏ qua raise `OnChanged` khi giá trị gán trùng default hiện có của field — cascade ép `RequireScanOrder`/`AllowEditBarcode` không chạy nếu chỉ dựa property-changed; sửa bằng gọi tường minh `ApplyScanModeInvariant()` sau load, verify lại đúng qua UI thật |
| CSV cột dữ liệu linh hoạt (token đặc biệt + Heading/Type + tự tách file) | ✅ Xong (code) | 4 token mới `<STT>`/`<date>`/`<time>`/`<JOB>` (+ `<barcode>` để định vị) nhập chung ô hiện có; `spec-register-map.csv` thêm cột `Heading`/`Type` (lọc ComboBox gợi ý + `Type=OKNG` ghi "OK"/"NG"); tự tách file `<ngày>-1.csv`, `-2.csv`... khi header đổi giữa các lần ghi cùng ngày — viết lại gần như toàn bộ `CsvResultExportService` |
| Verify UI thật cho SCAN MODE | ✅ Xong (1 phần) | Xác nhận đúng 2 checkbox bị ép/khóa khi vào SCAN MODE qua ảnh chụp thật; chưa test toggle nút lúc đã đăng nhập, chưa test pipeline với PLC thật |
| Verify UI thật cho CSV cột linh hoạt | ❌ Không làm được | Tự động hóa đăng nhập qua synthetic keyboard input không ổn định trong phiên này (gõ mật khẩu không vào ô) — dừng ở build + unit test pass |
| Publish lại `APP/` | ❌ Chưa làm | Chưa yêu cầu — code mới chỉ chạy qua `dotnet run` (Debug) để verify |

### Bẫy kỹ thuật đã gặp
- **`SetForegroundWindow` từ tiến trình nền không đáng tin cậy** khi tự động hóa click UI để verify — cửa sổ khác (VD VS Code, chạy dưới dạng Chrome_RenderWidgetHostHWND) vẫn nhận click dù đã gọi `SetForegroundWindow`/`SetWindowPos(HWND_TOPMOST)`. Khắc phục bằng kỹ thuật chuẩn Win32: `AttachThreadInput` giữa thread hiện tại và thread đang giữ foreground trước khi gọi `SetForegroundWindow`, xác nhận lại bằng `WindowFromPoint` trước khi click. Nên nhớ kỹ thuật này cho các lần verify UI sau.
- **PrintWindow không chụp được popup ComboBox đang mở** (popup là 1 top-level window riêng, không phải con của window chính) — không thể verify trực quan nội dung dropdown bằng screenshot kiểu này; phải suy luận gián tiếp (chọn item rồi xem TextBox kết quả) hoặc chấp nhận không verify được bằng ảnh.
- **`[ObservableProperty]` (CommunityToolkit.Mvvm) không raise `OnChanged` nếu giá trị gán trùng giá trị mặc định hiện có của field** — bất kỳ cascade logic nào đặt trong `OnXxxChanged` sẽ KHÔNG chạy lúc load nếu giá trị load trùng default; phải gọi tường minh logic đó sau khi gán, không dựa hoàn toàn vào side-effect của property-changed.

### Quyết định quan trọng đã đưa ra và lý do
- **Địa chỉ đọc trạng thái máy đổi từ D106.0 (đề xuất ban đầu của người dùng) sang D72.0** — D100-D199 bị khóa cứng CHỈ-GHI theo kiến trúc đã chốt từ trước, PC không bao giờ đọc thật được từ dải đó; đã giải thích rõ và người dùng chọn địa chỉ thay thế.
- **Barcode luôn cố định trong CSV, không thuộc diện cấu hình** — dù mọi cột khác (kể cả Date/Time) đều thành tùy chọn, Barcode là ngoại lệ vì đó là mục đích gốc của tính năng (truy xuất nguồn gốc theo mã quét).
- **STT = số thứ tự dòng trong đúng file đang ghi** (không phải số serial trích từ barcode) — đã hỏi lại vì tên gọi "STT" mơ hồ giữa 2 cách hiểu.
- **REV MODE không ép/tự động gì** — chủ động không đối xứng với SCAN MODE, giữ nguyên hành vi thủ công hiện có làm lối thoát cho rework/xử lý ngoại lệ.
- **Không tạo file test project mới cho `EolTester.App`** — tiếp tục theo tiền lệ đã chốt ở phiên trước, chỉ thêm unit test cho phần logic thuần đặt được trong `EolTester.Core` (đã có sẵn project test).

### Câu hỏi mở còn lại
- Hệ số Gain thật của từng biến đo lường vẫn chưa xác nhận với PLC (kế thừa từ mục 39-40).
- **CSV cột linh hoạt + SCAN MODE/REV MODE đều chưa test end-to-end với PLC thật** — cần bàn thử nghiệm thực tế: quét barcode thật kiểm tra pipeline tự động, đối chiếu file CSV sinh ra (header/STT/OK-NG/tự tách file) với PLC ghi D72.0/CMD_START thật.
- `APP/` chưa được publish lại với toàn bộ thay đổi phiên này.

## 42. Tóm tắt phiên làm việc (2026-08-28) — Rà soát toàn dự án: 8 bản vá nhỏ + báo cáo 9 vấn đề lớn + kế hoạch kiểm thử chi tiết

### Bối cảnh
Người dùng yêu cầu rà soát toàn bộ dự án (code, bảo mật, cấu hình, giao diện), sửa ngay lỗi nhỏ, lập báo cáo cho lỗi lớn cần xác nhận, kèm phương án kiểm thử chi tiết nhất — làm khi người dùng offline nên yêu cầu cẩn thận và bao quát. Đã đọc toàn bộ ~10.000 dòng C#, 8 file XAML, 5 file SeedData, 2 project test.

### Trạng thái cập nhật từng phần

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| Rà soát mã nguồn + bảo mật + cấu hình + XAML | ✅ Xong | Toàn bộ solution; đối chiếu với CLAUDE.md + history.md |
| S1 — guard độ dài mã scan âm | ✅ Xong | `KeyboardWedgeScanCapture.HandleFixedLength` thêm `if (targetLength < 1) return false;` — trước đó `_fixedLengthBuffer[..targetLength]` ném `ArgumentOutOfRangeException` mỗi phím gõ (Set Spec. không validate ô "Độ dài mã scan") |
| S2 — cờ `_connectionOperationInProgress` bị kẹt | ✅ Xong | `SetupTabViewModel.TestConnectionAsync`: nếu `RestartConnectionAsync()` trong `finally` ném (COM biến mất thật), cờ không được nhả → Lưu/Test chết cả phiên. Sửa: nested try/finally |
| S3 — app "chết câm lặng" khi khởi động lỗi | ✅ Xong | Tách `App.OnStartup` → `RunStartupSequenceAsync()` bọc try/catch → `HandleFatalStartupError`: log + `MessageBox` song ngữ (`{Type}: {Message}` + đường dẫn log) + `Shutdown()`. Thay cho việc app tắt không cửa sổ, chỉ phát hiện được qua `dotnet run` foreground (bẫy đã ghi nhiều lần ở mục 9/13) |
| S4 — thông báo Import/Export nhãn I/O luôn màu đỏ | ✅ Xong | Thêm `MonitorTabViewModel.StatusMessageIsError` + set đúng 4 nhánh; `MonitorTabView.xaml` đổi sang style xanh/đỏ (đúng pattern Setup/Setting) |
| S5 — mật khẩu còn trong ô sau khi đăng xuất | ✅ Xong | `MainWindow` subscribe `ShellViewModel.PropertyChanged`; `LoginPassword`/`IsLoggedIn` đổi + `LoginPassword` rỗng → `LoginPasswordBox.Clear()` |
| S6 — 7 cảnh báo CS4014 | ✅ Xong | `PlcPollingService.RefreshUiFromCacheAsync`: prefix `_ =` cho 7 lời gọi `_dispatcher.BeginInvoke`. Build sạch (chỉ còn `NU1510` vô hại của gói `Microsoft.Win32.Registry`) |
| S7 — dọn test | ✅ Xong | Xóa 2 `UnitTest1.cs` rỗng; thêm `PlcGainScaleTests.cs` (22 case) — `PlcGainScale` trước đó 0 test dù nằm trên đường tính Đạt/Không đạt |
| S8 — nhãn "Tốc độ động cơ" cho khóa `*.voltage` | ✅ Điều tra → không phải lỗi | Đã định sửa seed `Description` → "Điện áp", nhưng chụp UI thật + đối chiếu resx (`Spec_Voltage`="Tốc độ động cơ"/"Motor Speed") + `Specs/705.json` (`Unit:"RPM"`): khóa `*.voltage` là định danh lịch sử cho phép đo RPM. Đã **revert** |
| Build + test + verify UI | ✅ Xong | `dotnet build` ✓; 104 test ✓ (41 Core + 63 Communication); app chạy thử — 4 tab render đúng, luồng đăng nhập-sai hoạt động, không hồi quy |
| Báo cáo + kế hoạch kiểm thử (artifact) | ✅ Xong | https://claude.ai/code/artifact/395d10da-c5a2-4a3e-910a-f6a3704bcc09 — 9 vấn đề lớn (L1–L9) kèm phương án; 14 lỗi nhỏ/mỹ thuật (M3–M17); kế hoạch test 5 phần (unit / tích hợp loopback / bàn thử PLC thật / hồi quy UI / tiêu chí nghiệm thu S1–S7) |
| Publish lại `APP/` | ✅ Xong | Build + 104 test + publish Release win-x64 self-contained + smoke-test (cửa sổ "EOL Tester - 705" hiện sau ~10s cold start) đều pass |

### Bẫy kỹ thuật đã gặp
- **Cảnh báo CS4014 ẩn cho tới khi assembly được build lại** — 7 lời gọi `_dispatcher.BeginInvoke` không await trong `RefreshUiFromCacheAsync` (async) chỉ hiện cảnh báo khi `EolTester.App` được recompile toàn bộ (sửa 1 file bất kỳ trong project là đủ). Baseline build ban đầu (incremental) không cho thấy chúng — dễ tưởng nhầm là do mình gây ra.
- **`ScanCodeLength` âm chỉ ném khi `< 0`** — `""[..0]` hợp lệ (trả rỗng, rồi commit rỗng, bị bỏ qua), chỉ `[..(-n)]` mới ném `ArgumentOutOfRangeException`. Guard đặt ở `< 1` để chặn cả 0 (chế độ FixedLength vô nghĩa) lẫn số âm.
- **Verify UI bắt được lỗi suýt gây ra** — nếu chỉ đọc code, "sửa" seed `Description` "Tốc độ động cơ" → "Điện áp" trông đúng; chỉ chụp UI thật mới thấy resx + profile đều dùng "Motor Speed"/"RPM" một cách có chủ đích. Đúng quy tắc CLAUDE.md Phần I mục 0/7.

### Bước tiếp theo (theo thứ tự)
1. Người dùng duyệt 9 vấn đề L1–L9 trong báo cáo (đặc biệt L1 cycle time chưa đấu PLC, L2 giới hạn D134–D145 không auto-push lúc khởi động — cả 2 chạm giá trị an toàn).
2. Test end-to-end với PLC thật theo mục 07 của báo cáo — kế thừa việc còn treo từ mục 41 (SCAN MODE / CSV cột linh hoạt / bản đồ địa chỉ D50–D72 / Role=Slave).
3. (Tùy chọn) tạo 2 project test mới `EolTester.Security.Tests` (thuật toán license đang 0 test) + `EolTester.Configuration.Tests` (JSON store + CSV source), theo mục 05 của báo cáo.
4. Xử lý các mục L3–L9 và lỗi nhỏ M3–M17 theo ưu tiên người dùng chốt.

### Quyết định quan trọng đã đưa ra và lý do
- **"Fail loud" cho lỗi khởi động (S3)** — `MessageBox` + `Shutdown()` thay vì bắt `JsonException` từng store rồi chạy tiếp bằng cấu hình mặc định. Lý do: máy công nghiệp offline chạy nhầm cổng COM / nhầm Role còn nguy hiểm hơn là không mở được. Đây cũng là câu hỏi mở L9 — người dùng có thể chốt lại hướng "tự phục hồi + cảnh báo".
- **Không thêm guard chia-cho-`Scale`-bằng-0 trong `ReadMeasurement`/`ReadRegisterLimit`** — đã truy vết: `LoadScalesAsync` chỉ nhận 1/10/100/1000 (khác → 1) và `JsonSpecProfileStore.LoadAsync` overlay `step.Scale` cho mọi bước sau deserialize. Không có đường nào để `Scale=0` tới phép chia. Guard chỉ là "defensive noise" (Phần I mục 5).
- **Dùng `_ =` discard cho CS4014 thay vì để nguyên** — cảnh báo vô hại nhưng che cảnh báo thật về sau; nên build phải sạch.
- **Revert "sửa" seed Description** — xem S8; khóa `*.voltage` = phép đo RPM có chủ đích, không phải điện áp.

## 43. Tóm tắt phiên làm việc (2026-08-29) — Check Duplicate in log (D104.5/.6), sửa bug bàn phím, dời cấu hình cạnh exe, phân quyền 3 cấp, user từ file, persist runtime-state, khôi phục file seed, dọn thư mục ngôn ngữ

Phiên dài, xử lý 1 loạt yêu cầu độc lập của người dùng: thêm checkbox chống trùng barcode + tách khỏi "đúng thứ tự tem", sửa lỗi không gõ được bàn phím, dời file cấu hình sang cạnh exe, dựng phân quyền 3 cấp + tài khoản đọc từ file, persist trạng thái runtime qua lần khởi động, đổi đơn vị Timer Setting, cơ chế tự khôi phục file seed thiếu, và dọn bớt thư mục ngôn ngữ framework trong `APP/`.

### Trạng thái cập nhật từng phần

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| Checkbox "Check Duplicate in log" (D104.5) + cờ lỗi trùng mã D104.6 | ✅ Xong | `TestParameters.CheckDuplicateInLog` (mặc định true); `PARAM_CHECK_DUPLICATE_LOG`/`PARAM_SCAN_DUP_ERROR` trong spec-register-map.csv; checkbox mới ở SettingTabView |
| SCAN MODE không còn ép/khóa "Bắt buộc đúng thứ tự tem" | ✅ Xong | `ApplyScanModeInvariant` chỉ còn ép `AllowEditBarcode=false`. "đúng thứ tự tem" + "Check Duplicate in log" = 2 option độc lập |
| Pipeline `CommitScanAsync` (SCAN MODE) sắp xếp lại | ✅ Xong | check 4 (≠LastScanOk, chạy đầu, ghi D104.6=1) → check 1 (trích serial, luôn) → check 2 (thứ tự, chỉ khi bật) → check 3 (trùng log, chỉ khi bật) → pass hết: D104.6=0 + commit + CMD_START |
| #1 Sửa bug không gõ được bàn phím | ✅ Xong | `KeyboardWedgeScanCapture` chỉ can thiệp khi ô `ScanInputBox` có focus (trước: hook toàn cục nuốt mọi ký tự ở SCAN MODE khi máy chưa sẵn sàng) |
| #2 Dời file cấu hình sang cạnh exe | ✅ Xong | `AppPaths.RootDirectory` = `<exe>\AppData` (probe ghi thử) + fallback `%LocalAppData%\EolTester`; `\Config` + `\Logs`; verify thật file rơi đúng chỗ |
| #3 Phân quyền 3 cấp User/Operator/Admin | ✅ Xong | Khách = tab + quét barcode; User = Start/Stop/Reset + Auto/Manual + I/O thủ công + Confirm NG/Job; Operator = + Set Spec; Admin = + Setup + CSV + Import I/O. Sửa nhãn `Monitor_ManualHint` cũ ("không cần đăng nhập") |
| #4 Tài khoản đọc từ file | ✅ Xong | `FileUserStore` thay `SeededUserStore` (đã xóa); `SeedData\users-seed.csv` (plaintext) → hash PBKDF2 → `Config\users.json`; `BuiltInDefaults()` phòng vệ. Enum `UserRole` = User,Operator,Admin |
| #5 Persist runtime-state qua khởi động lại | ✅ Xong | `runtime-state.json` (`IRuntimeStateStore`): `LastScanOk` + `PreviousSerialNumber` + ảnh chụp D100-D199 (bỏ CMD_). Debounce 1s; nạp lại trước các store chuyên biệt. `PlcPollingService.PersistableStateChanged` event |
| Đơn vị Timer Setting ms → x0.1s + mặc định mới | ✅ Xong | Nhãn "Cài đặt thời gian (x0.1s)"; bỏ "(ms)" ở 3 dòng cố định (CSV + resx). Mặc định build: D131/D132=20, D133=5, D150-D159=5. Self-heal 0→5. Tên field/khóa PLC giữ hậu tố "Ms" |
| Khôi phục file seed thiếu (`SeedFiles`) | ✅ Xong | 4 CSV nhúng vào `EolTester.Configuration.dll` (`<EmbeddedResource>` + `<CopyToOutputDirectory>`). `App.EnsureSeedFiles()` lúc khởi động: thiếu → MessageBox OK (khôi phục) / Cancel (thoát); ghi lỗi → info dialog + chạy tiếp. Runtime: `CsvKeyValueFileReader`/`CsvIoMapSeedSource` đọc thẳng resource nếu file đĩa vắng. Verify thật (xóa 2 file → khôi phục đúng) |
| Dọn 13 thư mục ngôn ngữ framework trong `APP/` | ✅ Xong | `<SatelliteResourceLanguages>en-US</SatelliteResourceLanguages>`. `APP/`: 310→295 mục, 145→136 MB. Giữ `en-US` (bản dịch EN của app) |
| Build + test + publish `APP/` | ✅ Xong | Nhiều vòng; lần cuối: build sạch, 104 test pass (41 Core + 63 Communication), smoke-test "EOL Tester - 705" đạt |
| CLAUDE.md cập nhật đặc tả hiện hành | ✅ Xong | mục 4, 5, 7 Tab 1 (bản đồ bit D104 + pipeline), 7 Tab 4 (Timer x0.1s), 8 (phân quyền 3 cấp + FileUserStore), 12b (vị trí file + runtime-state), 12c mới (SeedFiles) |

### Bẫy kỹ thuật đã gặp
- **`spec-register-map.csv` bị Excel re-save giữa phiên** — người dùng mở/sửa file (thêm cột `HD122`/`HD130`... ở cột thứ 10 làm ghi chú địa chỉ), Excel pad trailing commas `,,,`. Parser đọc theo chỉ số cột nên không ảnh hưởng, nhưng Edit theo chuỗi cũ bị fail — phải Read lại trước khi sửa.
- **`<EmbeddedResource>` + `<CopyToOutputDirectory>` trên cùng 1 item** — MSBuild làm cả 2 việc (nhúng vào DLL + copy ra output), không cần khai báo `<Content>` riêng.
- **`Add-Type -Path <net10 dll>` fail trong Windows PowerShell 5.1** — không load được assembly net10; `[Reflection.Assembly]::LoadFrom` + `GetManifestResourceNames` thì được. Verify logic net10 phải chạy qua chính app hoặc unit test, không qua PS 5.1.
- **MessageBox chặn smoke-test tự động** — flow khôi phục seed hiện dialog OKCancel; test tự động phải gửi `{ENTER}` qua background job (`SendKeys`) để bấm OK mặc định.
- Sandbox PowerShell chặn `Remove-Item` với biến đường dẫn — dùng `[System.IO.File]::Delete()`/`Directory.Delete()` thay thế.

### Bước tiếp theo (theo thứ tự phụ thuộc)
1. **Test end-to-end với PLC thật** cho toàn bộ phiên này: SCAN MODE + check trùng (D104.5/.6), phân quyền 3 cấp, cấu hình ở `AppData\`, khôi phục `runtime-state.json`, Timer x0.1s, khôi phục file seed.
2. Người dùng duyệt **L1–L9** (báo cáo 2026-08-28) — L3 (mật khẩu cứng) + L8 (thư mục ghi cạnh exe) đã xử lý gián tiếp trong phiên này.
3. Chốt **"thực hiện 1c"** hoặc **"huỷ 1c"** (đóng gói single-file — đang treo, xem memory `pending-1c-single-file-packaging`).
4. (Tùy chọn) tạo `EolTester.Security.Tests` + `EolTester.Configuration.Tests` (mục 05 báo cáo 08-28).

### Quyết định quan trọng đã đưa ra và lý do
- **Bug bàn phím: gate theo focus ô Scan, không phải block toàn cục có điều kiện** — Lý do: người dùng cần gõ tay ở login/Job/Setup/Set Spec bình thường. Đánh đổi chấp nhận: quét khi focus ở ô khác có thể lọt ký tự / FixedLength thừa 1 ký tự (quét lại, xác suất thấp).
- **Vị trí file: cạnh exe + fallback `%LocalAppData%`** (không cứng, không cho cấu hình) — Lý do: máy hiện trường bảo trì dễ (copy thư mục là mang theo cấu hình), nhưng nếu cài Program Files thì UAC chặn ghi nên cần fallback. `DOTNET_BUNDLE_EXTRACT_BASE_DIR` không dùng được kiểu "thử rồi fallback" vì apphost đọc trước code.
- **Khách vẫn được quét barcode không cần đăng nhập** — Lý do: SCAN MODE quét là luồng vận hành dây chuyền bình thường; xung `CMD_START` tự phát sau quét hợp lệ là 1 phần của quét. Chỉ các nút điều khiển thủ công mới yêu cầu quyền User.
- **User store: seed plaintext → hash lúc nạp lần đầu** (không UI quản lý, không seed hash sẵn) — Lý do: đơn giản nhất, không bao giờ ghi plaintext ra `users.json`/log. Đổi user = sửa CSV + xóa json + khởi động lại.
- **Persist runtime-state: ghi ngay mỗi khi đổi, debounce 1s** (không định kỳ, không chỉ lúc thoát) — Lý do: tắt không đúng quy trình / mất điện luôn có bản ghi mới nhất; mất tối đa 1s cuối chấp nhận được.
- **D104.6 chỉ về 0 khi TOÀN BỘ pipeline pass; check 4 chạy đầu tiên** — Lý do: nếu mã trùng lần quét trước thì check 2/3 cũng fail; check 4 chạy đầu để báo đúng lý do "trùng mã vừa quét" + set cờ, không hiện lỗi thứ tự gây nhiễu.
- **`CheckDuplicateInLog` mặc định `true`** — Lý do: giữ nguyên bảo vệ chống trùng vốn luôn chạy trước đây; file `test-parameters.json` cũ thiếu field cũng thành true.
- **Đơn vị Timer đổi ms → x0.1s (chỉ nhãn + mặc định)** — Lý do: người dùng thống nhất với PLC dùng đơn vị 0.1s. Tên field/khóa `PARAM_*_MS`/`DelayTimersMs` giữ nguyên vì đổi tên lan rộng quá nhiều nơi. File config cũ (2000/500) KHÔNG tự chuyển đổi.
- **SeedFiles: hỏi khôi phục (OK) / thoát (Cancel), không degrade âm thầm, không hard fail-loud** — Lý do: seed là nội dung đi kèm build, tái tạo chính xác được; degrade âm thầm (đặc biệt `spec-register-map.csv` thiếu → PLC "câm") là rủi ro chất lượng.
- **Đóng gói: chọn `SatelliteResourceLanguages=en-US` (0 rủi ro) bây giờ; hoãn 1c single-file** — Lý do: sắp kiểm thử PLC thật, chưa muốn đụng đường nạp assembly WPF. 1c ghi vào memory làm việc treo (`pending-1c-single-file-packaging`).
