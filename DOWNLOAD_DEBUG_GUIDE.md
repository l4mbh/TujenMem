# Hướng dẫn Debug vấn đề tải giá từ poe.ninja

## Vấn đề

Một số người dùng báo cáo không tải được giá từ poe.ninja, chỉ thấy progress bar của Currency và Fragment chạy, còn lại không thấy thay đổi.

## Cách kiểm tra

### 1. Kiểm tra Log Level
- Vào Settings của plugin
- Đặt **Log Level** = **"Debug"** 
- Log sẽ xuất hiện trong HUD log của ExileCore

### 2. Kiểm tra League Name
League name phải **CHÍNH XÁC** và **ĐÚNG** với league hiện tại trên poe.ninja

**Cách kiểm tra:**
1. Vào https://poe.ninja
2. Xem league name ở góc trên bên trái (dropdown)
3. Nhập **CHÍNH XÁC** tên league vào Settings → League

**Ví dụ league names phổ biến:**
- `Standard` - permanent softcore
- `Hardcore` - permanent hardcore  
- `SSF Standard` - SSF permanent softcore
- `Settlers` - challenge league (tên thay đổi theo season)
- `Settlers HC` - challenge league hardcore

⚠️ **LƯU Ý:** League name phân biệt HOA/THƯỜNG, phải đúng 100%!

### 3. Xem chi tiết tải từng file

Sau khi bấm nút "Download Data", mở **Ninja Data** settings để xem:

- **File Name**: Tên file đang tải
- **Status**: Trạng thái hiện tại
  - `Connecting...` - Đang kết nối
  - `Downloading...` - Đang tải
  - `Saving...` - Đang lưu file
  - `Validating...` - Đang kiểm tra JSON
  - `✓ Done` - Thành công
  - `✗ HTTP Error` - Lỗi kết nối HTTP
  - `✗ Timeout` - Timeout
  - `✗ Invalid JSON` - JSON không hợp lệ
  - `✗ Error` - Lỗi khác
- **Progress**: Thanh progress (0% → 100%)
- **Integrity**: Valid/Invalid/Unknown

### 4. Đọc Log chi tiết

Log sẽ hiển thị:

```
========================================
Bắt đầu tải 16 files từ poe.ninja
League: YourLeagueName
========================================

[Currency.json] Bắt đầu tải từ: https://poe.ninja/api/data/CurrencyOverview?league=YourLeagueName&type=Currency&language=en
[Currency.json] Nhận được response, status: OK
[Currency.json] Tải xong 12345 ký tự
[Currency.json] ✓ JSON hợp lệ, đã lưu vào: ...

[Fragment.json] Bắt đầu tải từ: https://poe.ninja/api/data/CurrencyOverview?league=YourLeagueName&type=Fragment&language=en
...

========================================
Hoàn tất tải dữ liệu
Thành công: 16/16
✓ Tất cả files đã tải thành công!
========================================
```

### 5. Các lỗi thường gặp

#### Lỗi: "✗ HTTP Error" hoặc Timeout
**Nguyên nhân:** 
- Không có kết nối internet
- poe.ninja bị chặn bởi firewall/antivirus
- poe.ninja đang bảo trì

**Giải pháp:**
1. Kiểm tra internet
2. Tắt tạm thời firewall/antivirus
3. Thử lại sau 5-10 phút

#### Lỗi: "✗ Invalid JSON" 
**Nguyên nhân:**
- League name SAI
- poe.ninja trả về HTML thay vì JSON (league không tồn tại)

**Giải pháp:**
1. Kiểm tra lại League name
2. Test URL trong browser: `https://poe.ninja/api/data/CurrencyOverview?league=LEAGUE_NAME&type=Currency&language=en`
3. Nếu thấy HTML thay vì JSON → League name SAI

#### Lỗi: Response rỗng / Empty data
**Nguyên nhân:**
- League chưa có dữ liệu
- League quá mới, poe.ninja chưa có giá

**Giải pháp:**
- Đợi 1-2 ngày sau khi league bắt đầu
- Dùng league khác có dữ liệu (Standard/Hardcore)

### 6. Test thủ công

Mở browser và truy cập:
```
https://poe.ninja/api/data/CurrencyOverview?league=YOUR_LEAGUE&type=Currency&language=en
```

Thay `YOUR_LEAGUE` bằng league name của bạn.

**Kết quả mong đợi:** 
- Thấy JSON data với `lines` array chứa nhiều item
- VÍ DỤ: `{"lines":[{"currencyTypeName":"Chaos Orb","chaosEquivalent":1,...}]}`

**Nếu thấy HTML hoặc lỗi:**
- League name SAI hoặc không tồn tại

## Liên hệ hỗ trợ

Nếu vẫn gặp vấn đề, vui lòng cung cấp:
1. Screenshot của Ninja Data table (với Status column)
2. Log từ HUD (với Log Level = Debug)
3. League name bạn đang dùng
4. Kết quả test URL thủ công ở mục 6

