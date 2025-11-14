# ⚠️ Khắc phục lỗi: Chỉ Currency và Fragment tải được, còn lại 0%

## 🔍 Nguyên nhân

Vấn đề này **THƯỜNG DO LEAGUE NAME SAI** hoặc poe.ninja API không trả về dữ liệu cho league đó.

## ✅ Cách khắc phục

### Bước 1: Kiểm tra League Name

1. Vào https://poe.ninja
2. Nhấn dropdown league ở góc trên bên trái
3. Copy **CHÍNH XÁC** tên league (phân biệt HOA/thường)
4. Dán vào Settings → **League** trong plugin

**League names phổ biến:**
- `Standard` (permanent softcore)
- `Hardcore` (permanent hardcore)  
- `Settlers` (challenge league - thay đổi theo season)
- `Settlers HC`

### Bước 2: Dùng Test URLs để Debug

1. Mở plugin settings
2. Mở **Ninja Data** section
3. Bấm nút **"Test URLs (Debug)"**
4. Đặt **Log Level = "Debug"**
5. Đọc log trong HUD

**Log sẽ cho biết:**
- ✓ JSON hợp lệ → URL hoạt động tốt
- ✗ Response là HTML → **League name SAI**
- ✗ Response rỗng → League chưa có dữ liệu
- ✗ HTTP Error → Vấn đề kết nối

### Bước 3: Test thủ công trong Browser

Mở browser và truy cập URL này (thay `YOUR_LEAGUE` bằng league name của bạn):

```
https://poe.ninja/api/data/ItemOverview?league=YOUR_LEAGUE&type=Oil&language=en
```

**Kết quả mong đợi:**
- Thấy JSON với array `lines` chứa nhiều items
- VÍ DỤ: `{"lines":[{"name":"Tainted Oil","chaosValue":5.2,...}]}`

**Nếu thấy HTML hoặc error page:**
- League name **SAI** → Sửa lại league name
- League không có dữ liệu → Đổi sang league khác có dữ liệu

### Bước 4: Tải lại

Sau khi sửa League name:
1. Bấm **"Download Data"**
2. Xem bảng **Status** column
3. Tất cả files phải có status **"✓ Done"**

## 🎯 Tóm tắt

**99% trường hợp lỗi này là do League Name sai!**

Kiểm tra kỹ:
- ✅ League name đúng chính xả (phân biệt HOA/thường)
- ✅ League tồn tại trên poe.ninja
- ✅ League có dữ liệu (không phải league quá mới)

## 📌 Lưu ý

**Currency và Fragment tải được** vì poe.ninja có endpoint riêng cho chúng (`CurrencyOverview`), trong khi **các items khác dùng endpoint `ItemOverview`** - endpoint này yêu cầu league name chính xác hơn.

## 🆘 Vẫn không được?

1. Copy toàn bộ log từ "Test URLs (Debug)"
2. Gửi log + league name bạn đang dùng
3. Screenshot của bảng Ninja Data (cột Status)

