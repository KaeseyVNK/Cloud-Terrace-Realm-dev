Dự án: Cloud Terrace Realm
Nền tảng: Unity

==============================================
1. MÔ TẢ DỰ ÁN
==============================================
Thể loại: Cozy Base-Building, Macro-RTS, Resource Management.
Góc nhìn: 2.5D Isometric (Góc nhìn từ ban công của Lãnh Chúa hướng xuống thung lũng).
Cảm hứng: Age of Empires (Phát triển thời kỳ, công trình), The King is Watching (Góc nhìn cố định, tự động hóa), Dorfromantik (Sự thư giãn, tính thẩm mỹ).

==============================================
2. CẤU TRÚC THƯ MỤC CHÍNH (Thư mục Assets/)
==============================================
- Animations/ : Chứa các Animation Clips và Animator Controllers.
- Audio/      : Chứa toàn bộ âm thanh.
   ├── Music/ : Nhạc nền (BGM).
   └── SFX/   : Hiệu ứng âm thanh (Tiếng nổ, bước chân, v.v.).
- Materials/  : Chứa các vật liệu (Materials) cho đối tượng 3D/2D.
- Models/     : Các file mô hình 3D (FBX, OBJ, .blend,...).
- Prefabs/    : Các đối tượng (GameObject) đã được cấu hình sẵn để tái sử dụng.
- Resources/  : Các tài nguyên đặc biệt cần tải tự động bằng code (Resources.Load).
- Scenes/     : Chứa các phân cảnh game (Scene như MainMenu, Level1, Level2,...).
- Scripts/    : Mã nguồn C# điều khiển game.
   ├── Controllers/ : Logic điều khiển nhân vật, kẻ thù,...
   ├── Managers/    : Các hệ thống quản lý tổng (GameManager, UIManager,...).
   └── UI/          : Logic kịch bản cho giao diện.
- Shaders/    : Các Shader tùy chỉnh cho đồ họa.
- Sprites/    : Hình ảnh 2D dùng trực tiếp cho gameplay hoặc UI.
- Textures/   : Hình ảnh dùng làm bề mặt (Texture) đắp lên các mô hình 3D.
- UI/         : Các thành phần giao diện người dùng (Font chữ, Panel, Button...).

==============================================
3. GHI CHÚ
==============================================
- Dự án sử dụng hệ thống Input System (có file InputSystem_Actions).
- Bất kỳ tài nguyên nào được đưa vào dự án đều nên phân loại vào đúng các thư mục trên để dễ dàng quản lý chức năng.
