using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    public enum GameUiLanguage { English, Vietnamese }

    // Presentation only. The selected language never changes commands or game data.
    public static class UiLanguage
    {
        private const string PreferenceKey = "TinyKingdom.UiLanguage";
        private static readonly Dictionary<string, string> EnglishToVietnamese = new(
            StringComparer.Ordinal)
        {
            ["MAIN MENU"] = "MENU CHÍNH",
            ["CHOOSE MAP"] = "CHỌN BẢN ĐỒ",
            ["START GAME"] = "VÀO TRẬN",
            ["PLAY GAME"] = "CHƠI GAME",
            ["EXIT GAME"] = "THOÁT GAME",
            ["YOUR KINGDOM AWAITS"] = "VƯƠNG QUỐC ĐANG CHỜ",
            ["Choose your next adventure"] = "Bắt đầu hành trình của bạn",
            ["A TINY WORLD. A GREAT KINGDOM."] = "THẾ GIỚI NHỎ. VƯƠNG QUỐC LỚN.",
            ["Gather. Build. Command. Conquer."] = "Thu thập. Xây dựng. Chỉ huy. Chinh phục.",
            ["Lead the Blue kingdom to victory."] = "Dẫn dắt vương quốc Xanh đến chiến thắng.",
            ["CHOOSE DIFFICULTY"] = "CHỌN ĐỘ KHÓ",
            ["EASY"] = "DỄ",
            ["NORMAL"] = "THƯỜNG",
            ["HARD"] = "KHÓ",
            ["Learn the art of command"] = "Làm quen cách chỉ huy",
            ["A balanced challenge"] = "Thử thách cân bằng",
            ["Prove your strategy"] = "Thể hiện tài chiến thuật",
            ["A smaller enemy economy and army.\nMore time to plan your next move."] =
                "Đối thủ phát triển lực lượng nhỏ hơn.\nBạn có thêm thời gian lên kế hoạch.",
            ["The original Commander experience.\nBuild wisely and lead a balanced army."] =
                "Trải nghiệm Commander ban đầu.\nXây dựng và chỉ huy đội quân cân bằng.",
            ["A larger enemy economy and army.\nA faster Commander keeps you alert."] =
                "Đối thủ phát triển lực lượng lớn hơn.\nCommander phản ứng nhanh hơn.",
            ["Same unit stats. Same starting resources. Different enemy strategy."] =
                "Cùng chỉ số lính và tài nguyên khởi đầu. Đối thủ phát triển khác nhau.",
            ["CHOOSE MAP  ›"] = "CHỌN BẢN ĐỒ  ›",
            ["SELECT"] = "CHỌN",
            ["SELECTED"] = "ĐÃ CHỌN",
            ["SELECT MAP"] = "CHỌN BẢN ĐỒ",
            ["START GAME  ›"] = "VÀO TRẬN  ›",
            ["LOADING..."] = "ĐANG TẢI...",
            ["01  /  DIFFICULTY                         02  /  BATTLEFIELD"] =
                "01  /  ĐỘ KHÓ                         02  /  BẢN ĐỒ",
            ["Match controls are available during a game."] =
                "Các tùy chọn trận đấu sẽ xuất hiện khi vào game.",
            ["Two elevated kingdoms.\nAn open island and a contested gold mine."] =
                "Hai vương quốc trên đồi.\nHòn đảo rộng với mỏ vàng tranh chấp.",
            ["One road. Two kingdoms.\nBuild your army and break the enemy line."] =
                "Một con đường. Hai vương quốc.\nXây dựng quân đội, phá vỡ phòng tuyến.",
            ["Three bridges. One guarded island.\nSecure the mine and face the Goblins."] =
                "Ba cây cầu. Đảo có lính canh.\nChiếm mỏ vàng, đối đầu Goblin.",
            ["SETTINGS"] = "CÀI ĐẶT",
            ["RESOURCES"] = "TÀI NGUYÊN",
            ["COMMANDS"] = "LỆNH",
            ["COST"] = "CHI PHÍ",
            ["Overview"] = "Tổng quan",
            ["Controls"] = "Điều khiển",
            ["Graphics"] = "Đồ họa",
            ["Audio"] = "Âm thanh",
            ["About"] = "Nội dung thêm",
            ["About the project"] = "Giới thiệu dự án",
            ["Gameplay"] = "Lối chơi",
            ["Debug tools"] = "Công cụ kiểm thử",
            ["Match speed"] = "Tốc độ trận đấu",
            ["Language"] = "Ngôn ngữ",
            ["English"] = "Tiếng Anh",
            ["Vietnamese"] = "Tiếng Việt",
            ["Show damage numbers during combat"] = "Hiển thị số sát thương khi giao chiến",
            ["Fog display is available when cheats are enabled"] = "Chỉ đổi hiển thị sương mù khi bật cheat",
            ["Allow Cheats to change Fog"] = "Bật cheat để đổi sương mù",
            ["Add 100 resources to your faction"] = "Thêm 100 tài nguyên cho phe của bạn",
            ["Right Click: move / attack / gather. Esc: cancel command."] =
                "Chuột phải: di chuyển / tấn công / thu thập. Esc: hủy lệnh.",
            ["Restore defaults"] = "Khôi phục mặc định",
            ["Display"] = "Màn hình",
            ["Performance"] = "Hiệu năng",
            ["Keep display settings"] = "Giữ thiết lập màn hình",
            ["Revert"] = "Hoàn tác",
            ["Unlimited"] = "Không giới hạn",
            ["Very Low"] = "Rất thấp",
            ["Low"] = "Thấp",
            ["Medium"] = "Trung bình",
            ["High"] = "Cao",
            ["Very High"] = "Rất cao",
            ["Ultra"] = "Cực cao",
            ["Fastest"] = "Nhanh nhất",
            ["Fast"] = "Nhanh",
            ["Simple"] = "Đơn giản",
            ["Good"] = "Tốt",
            ["Beautiful"] = "Đẹp",
            ["Fantastic"] = "Tuyệt đẹp",
            ["Master"] = "Âm lượng chung",
            ["Sound effects"] = "Hiệu ứng âm thanh",
            ["Music"] = "Nhạc",
            ["Audio service unavailable."] = "Không có dịch vụ âm thanh.",
            ["No music clip is configured."] = "Chưa thiết lập nhạc nền.",
            ["Display change reverted after 15 seconds."] =
                "Đã khôi phục màn hình sau 15 giây.",
            ["Binding cancelled."] = "Đã hủy đổi phím.",
            ["Default keys restored."] = "Đã khôi phục phím mặc định.",
            ["Display settings saved."] = "Đã lưu thiết lập màn hình.",
            ["About Tiny Kingdom RL"] = "Về Tiny Kingdom RL",
            ["How the match works"] = "Cách chơi",
            ["Project and artwork"] = "Dự án và đồ họa",
            ["Attack Move"] = "Di chuyển tấn công",
            ["Guard"] = "Phòng thủ",
            ["Eject All"] = "Cho tất cả ra",
            ["Auto Arrow"] = "Tên tự động",
            ["AUTO ARROW"] = "TÊN TỰ ĐỘNG",
            ["ATTACK MOVE"] = "DI CHUYỂN TẤN CÔNG",
            ["GUARD"] = "PHÒNG THỦ",
            ["EJECT ALL"] = "CHO TẤT CẢ RA",
            ["CANNON"] = "PHÁO",
            ["Cannon"] = "Pháo",
            ["Gold Mine"] = "Mỏ vàng",
            ["Tree"] = "Cây",
            ["Sheep"] = "Cừu",
            ["Gold Stone"] = "Quặng vàng",
            ["Map"] = "Bản đồ",
            ["Castle"] = "Lâu đài",
            ["Scout"] = "Trinh sát",
            ["SCOUT"] = "TRINH SÁT",
            ["PAWN"] = "DÂN",
            ["WARRIOR"] = "CHIẾN BINH",
            ["ARCHER"] = "CUNG THỦ",
            ["LANCER"] = "THƯƠNG BINH",
            ["MONK"] = "TU SĨ",
            ["HOUSE"] = "NHÀ",
            ["BARRACKS"] = "DOANH TRẠI",
            ["ARCHERY"] = "TRẠI CUNG",
            ["MONASTERY"] = "TU VIỆN",
            ["TOWER"] = "THÁP",
            ["CASTLE"] = "LÂU ĐÀI",
            ["HP"] = "Máu",
            ["Move toward a target area and engage enemies encountered."] =
                "Di chuyển tới khu vực chỉ định và giao chiến với kẻ địch gặp trên đường.",
            ["Hold position and reduce incoming damage while the command is held."] =
                "Giữ vị trí và giảm sát thương nhận vào khi đang giữ lệnh.",
            ["Upgrade in progress."] = "Đang nâng cấp.",
            ["Adds a separate explosive cannon attack."] =
                "Thêm một đòn pháo nổ độc lập.",
            ["Adds one automatic arrow to each Tower volley."] =
                "Thêm một mũi tên tự động cho mỗi loạt bắn của Tháp.",
            ["Release every internal occupant and mounted Archer from this building."] =
                "Cho toàn bộ quân trú đóng và Cung thủ trên công trình ra ngoài.",
            ["Release every Pawn currently inside the Gold Mine."] =
                "Cho toàn bộ Dân đang ở trong Mỏ vàng ra ngoài.",
            ["Role: Support / Healer\nCannot attack. Heals damaged friendly units."] =
                "Vai trò: Hỗ trợ / Hồi máu\nKhông tấn công. Hồi máu cho quân đồng minh.",
            ["Role: Worker\nGathers Food, Wood and Gold. Constructs and repairs buildings."] =
                "Vai trò: Dân\nThu thập thức ăn, gỗ và vàng; xây dựng và sửa công trình.",
            ["Role: Heavy melee"] = "Vai trò: Cận chiến hạng nặng",
            ["Role: Ranged damage"] = "Vai trò: Tấn công tầm xa",
            ["Role: Mobile melee"] = "Vai trò: Cận chiến cơ động",
            ["Role: Military"] = "Vai trò: Quân đội",
            ["Purpose: Unit production\nTrains: "] = "Công dụng: Huấn luyện quân\nĐào tạo: ",
            ["Purpose: Building"] = "Công dụng: Công trình",
            ["Completed"] = "Đã hoàn thành",
            ["Neutral"] = "Trung lập",
            ["Blue"] = "Xanh",
            ["Red"] = "Đỏ",
            ["Pawn"] = "Dân",
            ["Warrior"] = "Chiến binh",
            ["Archer"] = "Cung thủ",
            ["Lancer"] = "Thương binh",
            ["Monk"] = "Tu sĩ",
            ["House"] = "Nhà",
            ["Barracks"] = "Doanh trại",
            ["Archery"] = "Trại cung",
            ["Monastery"] = "Tu viện",
            ["Tower"] = "Tháp",
            ["Food"] = "Thức ăn",
            ["Wood"] = "Gỗ",
            ["Gold"] = "Vàng",
            ["Idle"] = "Đứng yên",
            ["Moving"] = "Di chuyển",
            ["Gathering"] = "Thu thập",
            ["Building"] = "Xây dựng",
            ["Repairing"] = "Sửa chữa",
            ["MovingToResource"] = "Đến tài nguyên",
            ["MovingToRememberedResource"] = "Đến tài nguyên đã biết",
            ["ReturningToDropOff"] = "Mang tài nguyên về",
            ["WaitingForDropOff"] = "Chờ điểm giao tài nguyên",
            ["Chasing"] = "Đuổi theo",
            ["Attacking"] = "Đang tấn công",
            ["Dead"] = "Đã chết",
            ["Guarding"] = "Đang phòng thủ",
            ["Owned"] = "Đã kiểm soát",
            ["Depleted"] = "Đã cạn",
            ["Destroyed"] = "Đã phá hủy",
            ["UnderConstruction"] = "Đang xây dựng",
            ["Upgrading"] = "Đang nâng cấp",
            ["Cannon upgraded"] = "Pháo đã nâng cấp",
            ["Auto arrow upgraded"] = "Tên tự động đã nâng cấp",
            ["Guard (G)"] = "Phòng thủ (G)",
            ["Attack"] = "Tấn công",
            ["Range"] = "Tầm",
            ["Speed"] = "Tốc độ",
            ["Damage"] = "Sát thương",
            ["Train"] = "Huấn luyện",
            ["Size"] = "Kích thước",
            ["Build"] = "Xây",
            ["Heal"] = "Hồi máu",
            ["Interval"] = "Chu kỳ",
            ["Population"] = "Dân số",
            ["Capacity"] = "Sức chứa",
            ["ON"] = "BẬT",
            ["OFF"] = "TẮT",
            ["Victory"] = "Chiến thắng",
            ["Defeat"] = "Thất bại",
            ["Play Again"] = "Chơi lại",
            ["Main Menu"] = "Menu chính",
            ["Start Match"] = "Bắt đầu trận",
            ["Map Selection"] = "Chọn bản đồ"
            ,["CHOOSE BATTLEFIELD"] = "CHỌN CHIẾN TRƯỜNG"
            ,["TWO KINGDOMS"] = "HAI VƯƠNG QUỐC"
            ,["STRAIGHT LANE"] = "CON ĐƯỜNG THẲNG"
            ,["GOBLIN ISLAND"] = "ĐẢO GOBLIN"
            ,["The original island battlefield"] = "Chiến trường đảo nguyên bản"
            ,["Two bases connected by one straight road"] =
                "Hai căn cứ nối với nhau bằng một con đường thẳng"
            ,["Three bridges and a guarded island mine"] =
                "Ba cây cầu và mỏ trên đảo được canh giữ"
            ,["CLICK TO PLAY"] = "BẤM ĐỂ CHƠI"
            ,["Blue versus Red  •  One Castle and three Pawns per side"] =
                "Phe Xanh đấu phe Đỏ  •  Mỗi bên có một Lâu đài và ba Dân"
            ,["VICTORY"] = "CHIẾN THẮNG"
            ,["DEFEAT"] = "THẤT BẠI"
            ,["Enemy Castle Destroyed"] = "Đã phá hủy Lâu đài đối phương"
            ,["Your Castle Has Fallen"] = "Lâu đài của bạn đã thất thủ"
            ,["RESTART"] = "CHƠI LẠI"
            ,["MAPS"] = "BẢN ĐỒ"
            ,["NAVIGATION"] = "DANH MỤC"
            ,["Control Group 1"] = "Nhóm điều khiển 1"
            ,["Control Group 2"] = "Nhóm điều khiển 2"
            ,["Control Group 3"] = "Nhóm điều khiển 3"
            ,["Control Group 4"] = "Nhóm điều khiển 4"
            ,["Assign"] = "Gán"
            ,["Select"] = "Chọn"
            ,["Clear Selected Group"] = "Xóa nhóm đang chọn"
            ,["Speed 1x"] = "Tốc độ 1x"
            ,["Speed 2x"] = "Tốc độ 2x"
            ,["Speed 5x"] = "Tốc độ 5x"
            ,["Speed 10x"] = "Tốc độ 10x"
            ,["Camera Left"] = "Camera trái"
            ,["Camera Right"] = "Camera phải"
            ,["Camera Up"] = "Camera lên"
            ,["Camera Down"] = "Camera xuống"
            ,["Wedge"] = "Mũi nhọn"
            ,["Loose"] = "Giãn đội hình"
            ,["Line"] = "Hàng ngang"
            ,["Compact"] = "Đội hình chặt"
            ,["Wedge Formation"] = "Đội hình mũi nhọn"
            ,["Loose Formation"] = "Đội hình giãn"
            ,["Line Formation"] = "Đội hình hàng ngang"
            ,["Compact Formation"] = "Đội hình chặt"
            ,["Forward-pointing formation. Frontline units occupy the tip."] =
                "Đội hình hướng về phía trước; quân tiền tuyến đứng ở mũi nhọn."
            ,["Wider spacing between units while preserving battle roles."] =
                "Giãn khoảng cách giữa các đơn vị nhưng vẫn giữ vai trò chiến đấu."
            ,["One wide rank perpendicular to movement. Frontline roles prefer the center."] =
                "Một hàng rộng vuông góc hướng đi; quân tiền tuyến ưu tiên vị trí giữa."
            ,["Tight rectangular formation. Melee units lead."] =
                "Đội hình chữ nhật chặt; quân cận chiến đi đầu."
            ,["No display resolutions reported by this device."] =
                "Thiết bị không cung cấp độ phân giải màn hình."
            ,["This key is reserved for system controls."] =
                "Phím này được dành cho điều khiển hệ thống."
            ,["None"] = "Không có"
        };

        private static readonly Dictionary<string, string> VietnameseToEnglish = BuildReverse();
        private static GameUiLanguage? current;

        public static GameUiLanguage Current => current ??=
            PlayerPrefs.GetInt(PreferenceKey, 0) == 1
                ? GameUiLanguage.Vietnamese : GameUiLanguage.English;

        public static void Set(GameUiLanguage language)
        {
            if (Current == language) return;
            current = language;
            PlayerPrefs.SetInt(PreferenceKey, language == GameUiLanguage.Vietnamese ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static string Text(string english, string vietnamese)
            => Current == GameUiLanguage.Vietnamese ? vietnamese : english;

        public static string Translate(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (Current == GameUiLanguage.English)
                return VietnameseToEnglish.TryGetValue(source, out string english)
                    ? english : source;
            if (EnglishToVietnamese.TryGetValue(source, out string vietnamese))
                return vietnamese;
            if (VietnameseToEnglish.ContainsKey(source)) return source;

            // Dynamic numbers and unit names are composed by existing presenters.
            // Match complete words so ON does not alter MONK or other labels.
            string value = source;
            value = value.Replace("Damage text: ", "Số sát thương: ")
                .Replace("Allow Cheats: ", "Cho phép cheat: ")
                .Replace("Fog: ", "Sương mù: ")
                .Replace("Fullscreen: ", "Toàn màn hình: ")
                .Replace("Resolution: ", "Độ phân giải: ")
                .Replace("FPS limit: ", "Giới hạn FPS: ")
                .Replace("Quality: ", "Chất lượng: ")
                .Replace("Very Low", "Rất thấp")
                .Replace("Very High", "Rất cao")
                .Replace("Keep display settings within ",
                    "Giữ thiết lập màn hình trong ")
                .Replace("Press a key for ", "Nhấn phím cho ")
                .Replace("Press a shortcut for ", "Nhấn tổ hợp phím cho ")
                .Replace(" (Esc cancels).", " (Esc để hủy).")
                .Replace(" is already assigned to ", " đã được gán cho ")
                .Replace("Control Group ", "Nhóm điều khiển ")
                .Replace("Assign Group ", "Gán nhóm ")
                .Replace("Select Group ", "Chọn nhóm ")
                .Replace("Clear Selected Group", "Xóa nhóm đang chọn")
                .Replace("Camera Left", "Camera trái")
                .Replace("Camera Right", "Camera phải")
                .Replace("Camera Up", "Camera lên")
                .Replace("Camera Down", "Camera xuống")
                .Replace("Cost: ", "Chi phí: ")
                .Replace("Hotkey: ", "Phím tắt: ")
                .Replace("Vision: ", "Tầm nhìn: ")
                .Replace("Maximum active: ", "Tối đa: ")
                .Replace("Only one Scout may be active.",
                    "Chỉ có thể có một Trinh sát.")
                .Replace("This Pawn is already the active Scout.",
                    "Dân này đã là Trinh sát.")
                .Replace("Requires 50 Wood.", "Cần 50 Gỗ.")
                .Replace("Available only while the match is playing.",
                    "Chỉ dùng khi trận đấu đang diễn ra.")
                .Replace("Only a human-controlled friendly Pawn may become a Scout.",
                    "Chỉ Dân đồng minh do người chơi điều khiển mới được làm Trinh sát.")
                .Replace("Scout fire presentation is unavailable.",
                    "Không có hiệu ứng lửa Trinh sát.")
                .Replace("Select a living friendly Pawn.",
                    "Chọn một Dân đồng minh còn sống.")
                .Replace("Provides +", "Cung cấp +")
                .Replace(" when completed.", " khi hoàn thành.")
                .Replace("Internal capacity ", "Sức chứa bên trong ")
                .Replace("mounted Archers ", "Cung thủ trên tháp ")
                .Replace("Occupants fire ", "Quân trú đóng bắn ")
                .Replace("-damage arrows every ", " sát thương mỗi ")
                .Replace(" at range ", " ở tầm ")
                .Replace("Trains: ", "Huấn luyện: ")
                .Replace("Train ", "Huấn luyện ")
                .Replace("Strong against: ", "Mạnh hơn: ")
                .Replace("Weak against: ", "Yếu hơn: ")
                .Replace("Purpose: Population support", "Công dụng: Tăng dân số")
                .Replace("Role: Heavy melee", "Vai trò: Cận chiến hạng nặng")
                .Replace("Role: Ranged damage", "Vai trò: Tấn công tầm xa")
                .Replace("Role: Mobile melee", "Vai trò: Cận chiến cơ động")
                .Replace("Attack Move", "Di chuyển tấn công")
                .Replace("Purpose: Defensive garrison", "Công dụng: Đồn trú phòng thủ")
                .Replace("Purpose: Unit production", "Công dụng: Huấn luyện quân")
                .Replace("Construction  ", "Xây dựng  ")
                .Replace("Garrison  ", "Đồn trú  ")
                .Replace("Mounted ", "Trên tháp ")
                .Replace("Cargo  ", "Mang theo  ")
                .Replace("Pawns  ", "Dân  ")
                .Replace("Cargo: ", "Mang theo: ")
                .Replace("Pop ", "Dân số ")
                .Replace(" production", " sản xuất")
                .Replace("Queue ", "Hàng chờ ")
                .Replace("Cannon upgraded", "Pháo đã nâng cấp")
                .Replace("Auto arrow upgraded", "Tên tự động đã nâng cấp")
                .Replace("HP: ", "Máu: ")
                .Replace("HP  ", "Máu  ");
            value = Regex.Replace(value,
                @"\b(Warrior|Archer|Lancer|Monk|Pawn|House|Barracks|Archery|Monastery|Tower|Castle|HOUSE|BARRACKS|ARCHERY|MONASTERY|TOWER|CASTLE|Food|Wood|Gold|Blue|Red|Neutral|Completed|Idle|Moving|Gathering|Building|Repairing|MovingToResource|MovingToRememberedResource|ReturningToDropOff|WaitingForDropOff|Chasing|Attacking|Dead|Guarding|Owned|Depleted|Destroyed|UnderConstruction|Upgrading|Attack|Range|Speed|Damage|Train|Size|Build|Heal|Interval|Population|Capacity|HP|ON|OFF|Low|Medium|High|Ultra|Fastest|Fast|Simple|Good|Beautiful|Fantastic)\b",
                match => EnglishToVietnamese.TryGetValue(match.Value, out string word)
                    ? word : match.Value);
            return value;
        }

        public static void Bind(Text label)
        {
            if (label == null) return;
            LocalizedUiText binding = label.GetComponent<LocalizedUiText>()
                ?? label.gameObject.AddComponent<LocalizedUiText>();
            binding.Configure(label);
        }

        public static void Set(Text label, string source)
        {
            if (label == null) return;
            LocalizedUiText binding = label.GetComponent<LocalizedUiText>();
            if (binding == null) Bind(label);
            binding = label.GetComponent<LocalizedUiText>();
            binding.SetSource(source);
        }

        private static Dictionary<string, string> BuildReverse()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in EnglishToVietnamese)
                result.TryAdd(pair.Value, pair.Key);
            return result;
        }
    }

    [DisallowMultipleComponent]
    public sealed class LocalizedUiText : MonoBehaviour
    {
        private Text label;
        private string source;
        private string displayed;
        private GameUiLanguage displayedLanguage;

        public void Configure(Text target)
        {
            label = target;
            source = label != null ? label.text : "";
            Apply();
        }

        public void SetSource(string value)
        {
            source = value ?? "";
            Apply();
        }

        private void LateUpdate()
        {
            if (label == null) return;
            if (label.text != displayed)
            {
                if (label.text == source && displayedLanguage == UiLanguage.Current)
                {
                    label.text = displayed;
                    return;
                }
                source = label.text;
                Apply();
            }
            else if (displayedLanguage != UiLanguage.Current)
                Apply();
        }

        private void Apply()
        {
            if (label == null) return;
            displayedLanguage = UiLanguage.Current;
            displayed = UiLanguage.Translate(source);
            label.text = displayed;
        }
    }
}
