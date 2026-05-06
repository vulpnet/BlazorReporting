namespace BlazorReporting.Services;

// ══════════════════════════════════════════════════════════════
//  SeasonalFactorService
//  Tính hệ số điều chỉnh dự đoán dựa trên:
//    · Mùa vụ (Tết, Trung Thu, Hè, lễ tết...)
//    · Khí hậu vùng (Bắc / Trung / Nam)
//    · Danh mục sản phẩm
//    · Tín hiệu tồn kho (từ lịch sử đơn hàng)
// ══════════════════════════════════════════════════════════════

public sealed class SeasonalFactorService
{
    // ── Phân vùng khí hậu ────────────────────────────────────
    public static ClimateRegion GetRegion(string provinceCode)
    {
        var p = provinceCode.ToLower();

        // Miền Bắc
        if (ContainsAny(p, "hà nội", "hà nam", "hải phòng", "hải dương", "hưng yên",
                           "thái bình", "nam định", "ninh bình", "bắc ninh", "bắc giang",
                           "vĩnh phúc", "phú thọ", "thái nguyên", "bắc kạn", "cao bằng",
                           "lạng sơn", "quảng ninh", "tuyên quang", "hà giang", "yên bái",
                           "lào cai", "điện biên", "lai châu", "sơn la", "hòa bình",
                           "thanh hóa", "nghệ an", "hà tĩnh"))
            return ClimateRegion.North;

        // Miền Trung
        if (ContainsAny(p, "quảng bình", "quảng trị", "thừa thiên", "huế", "đà nẵng",
                           "quảng nam", "quảng ngãi", "bình định", "phú yên", "khánh hòa",
                           "ninh thuận", "bình thuận", "kon tum", "gia lai", "đắk lắk",
                           "đắk nông", "lâm đồng"))
            return ClimateRegion.Central;

        // Mặc định Miền Nam
        return ClimateRegion.South;
    }

    // ── Nhận dạng danh mục sản phẩm ──────────────────────────
    public static ProductCategory DetectCategory(string inventoryName)
    {
        var n = inventoryName.ToLower();
        if (ContainsAny(n, "bia", "rượu", "nước ngọt", "nước uống", "nước khoáng",
                           "nước suối", "trà", "cà phê", "sữa", "nước tăng lực", "nước ép"))
            return ProductCategory.Beverage;
        if (ContainsAny(n, "bánh", "kẹo", "mứt", "snack", "socola", "chocolate",
                           "bỏng ngô", "bánh quy", "bánh biscuit"))
            return ProductCategory.Confectionery;
        if (ContainsAny(n, "nước mắm", "nước tương", "tương", "dầu ăn", "dầu hào",
                           "muối", "đường", "bột ngọt", "gia vị", "xốt", "sauce"))
            return ProductCategory.Condiment;
        if (ContainsAny(n, "mì", "bún", "phở", "miến", "cháo", "cơm", "hủ tiếu",
                           "bánh đa", "nui", "pasta", "mì gói", "mì ly"))
            return ProductCategory.InstantFood;
        if (ContainsAny(n, "kem", "đông lạnh", "frozen", "xúc xích", "lạp xưởng"))
            return ProductCategory.FrozenFood;
        if (ContainsAny(n, "dầu gội", "sữa tắm", "xà phòng", "kem đánh răng",
                           "nước hoa hồng", "kem dưỡng", "mỹ phẩm", "tã", "băng vệ sinh"))
            return ProductCategory.PersonalCare;
        if (ContainsAny(n, "bột giặt", "nước rửa chén", "nước lau sàn", "tẩy",
                           "nước xả vải", "chất tẩy rửa"))
            return ProductCategory.Household;
        return ProductCategory.General;
    }

    // ── Phân tích đầy đủ cho 1 cặp Tỉnh × SP ────────────────
    public FactorAnalysis Analyze(
        string provinceCode,
        string inventoryName,
        int lastYear, int lastMonth,
        long[] historyQty)
    {
        var region   = GetRegion(provinceCode);
        var category = DetectCategory(inventoryName);

        // Tính hệ số 3 tháng tới
        var monthFactors = new List<MonthSeasonFactor>();
        for (int step = 1; step <= 3; step++)
        {
            var (yr, mo) = NextMonth(lastYear, lastMonth, step);
            float sf     = ComputeSeasonalFactor(region, category, mo);
            string label = MonthLabel(mo, yr);
            var events   = GetEvents(region, category, mo);
            monthFactors.Add(new MonthSeasonFactor(mo, yr, label, sf, events));
        }

        float avgSF   = monthFactors.Average(f => f.Factor);
        var   invSig  = ComputeInventorySignal(historyQty);
        string climate= GetClimateContext(region, lastMonth);
        string catLbl = CategoryLabel(category);
        string regionLbl = RegionLabel(region);

        return new FactorAnalysis(
            Region         : regionLbl,
            Category       : catLbl,
            MonthFactors   : monthFactors,
            AvgSeasonalFactor: avgSF,
            InventorySignal: invSig,
            ClimateContext : climate,
            RegionEnum     : region,
            CategoryEnum   : category);
    }

    // ── Hệ số mùa vụ ─────────────────────────────────────────
    private static float ComputeSeasonalFactor(
        ClimateRegion region, ProductCategory cat, int month)
    {
        float f = 1.0f;

        // ── Tết Nguyên Đán (thường T1-T2, chuẩn bị từ T12) ──
        f *= (month, cat) switch
        {
            (1 or 2, ProductCategory.Confectionery) => 2.8f,
            (1 or 2, ProductCategory.Condiment)     => 2.0f,
            (1 or 2, ProductCategory.Beverage)      => 1.8f,
            (1 or 2, ProductCategory.InstantFood)   => 1.5f,
            (1 or 2, _)                             => 1.3f,
            (12, ProductCategory.Confectionery)     => 1.8f,  // chuẩn bị Tết
            (12, ProductCategory.Condiment)         => 1.5f,
            (12, ProductCategory.Beverage)          => 1.4f,
            (12, _)                                 => 1.2f,
            _ => 1.0f
        };

        // ── Trung Thu T8-T9 ──
        f *= (month, cat) switch
        {
            (8 or 9, ProductCategory.Confectionery) => 1.8f,
            (8 or 9, _)                             => 1.0f,
            _ => 1.0f
        };

        // ── Mùa hè T6-T8 ──
        f *= (month, cat) switch
        {
            (6 or 7 or 8, ProductCategory.Beverage)   => 1.5f,
            (6 or 7 or 8, ProductCategory.FrozenFood)  => 1.6f,
            (6 or 7 or 8, ProductCategory.PersonalCare)=> 1.1f,
            _ => 1.0f
        };

        // ── Lễ 30/4–1/5 và 2/9 ──
        f *= month is 4 or 5 or 9
            ? cat is ProductCategory.Beverage ? 1.15f : 1.05f
            : 1.0f;

        // ── Khí hậu vùng ──
        f *= (region, month) switch
        {
            // Miền Bắc: đông lạnh T11–T2 → đồ uống lạnh giảm, thực phẩm ấm tăng
            (ClimateRegion.North, 11 or 12 or 1 or 2) =>
                cat is ProductCategory.Beverage ? 0.75f
              : cat is ProductCategory.InstantFood or ProductCategory.Condiment ? 1.15f
              : 1.0f,

            // Miền Bắc: hè ẩm T6–T8
            (ClimateRegion.North, 6 or 7 or 8) =>
                cat is ProductCategory.Household ? 1.1f : 1.0f,

            // Miền Trung: mưa lũ T9–T12 → di chuyển khó, giảm tiêu thụ
            (ClimateRegion.Central, 9 or 10 or 11 or 12) => 0.82f,

            // Miền Trung: hè khô nóng T6–T8 → nhu cầu đồ uống rất cao
            (ClimateRegion.Central, 6 or 7 or 8) =>
                cat is ProductCategory.Beverage ? 1.6f : 1.0f,

            // Miền Nam: mùa mưa T5–T10 → đồ uống tăng nhẹ kể cả mưa
            (ClimateRegion.South, 5 or 6 or 7 or 8 or 9 or 10) =>
                cat is ProductCategory.Beverage ? 1.2f : 0.95f,

            // Miền Nam: mùa khô T11–T4 → đồ uống tăng mạnh
            (ClimateRegion.South, 11 or 12 or 3 or 4) =>
                cat is ProductCategory.Beverage ? 1.35f : 1.05f,

            _ => 1.0f
        };

        return MathF.Round(f, 2);
    }

    // ── Sự kiện nổi bật trong tháng ──────────────────────────
    private static List<string> GetEvents(
        ClimateRegion region, ProductCategory cat, int month)
    {
        var events = new List<string>();

        if (month is 1 or 2) events.Add("🎊 Tết Nguyên Đán — nhu cầu tăng mạnh");
        if (month is 12)     events.Add("🧧 Chuẩn bị Tết — tích trữ hàng sớm");
        if (month is 8 or 9 && cat is ProductCategory.Confectionery)
            events.Add("🥮 Tết Trung Thu — bánh kẹo bùng nổ");
        if (month is 6 or 7 or 8)  events.Add("☀️ Cao điểm mùa hè");
        if (month is 4)    events.Add("🌸 Lễ Giỗ Tổ Hùng Vương (T4)");
        if (month is 4 or 5) events.Add("🎉 Nghỉ lễ 30/4 – 1/5");
        if (month is 9)    events.Add("🇻🇳 Quốc khánh 2/9");
        if (month is 12)   events.Add("🎄 Giáng sinh & Năm mới Dương lịch");

        if (region == ClimateRegion.North && month is 11 or 12 or 1 or 2)
            events.Add("🧥 Đông lạnh Miền Bắc — đồ uống lạnh giảm");
        if (region == ClimateRegion.Central && month is 9 or 10 or 11)
            events.Add("🌧️ Mùa lũ Miền Trung — vận chuyển khó khăn");
        if (region == ClimateRegion.South && month is 5 or 6 or 7 or 8 or 9 or 10)
            events.Add("🌦️ Mùa mưa Miền Nam");

        return events;
    }

    // ── Tín hiệu tồn kho từ lịch sử ─────────────────────────
    public static InventorySignal ComputeInventorySignal(long[] historyQty)
    {
        if (historyQty.Length < 3) return InventorySignal.Unknown;

        var vals = historyQty.Select(v => (float)v).ToArray();
        int n    = vals.Length;

        // Kiểm tra tháng trống (có thể do hết hàng)
        int zeros = vals.Count(v => v == 0);
        if ((float)zeros / n > 0.25f) return InventorySignal.PossibleStockout;

        // So sánh tốc độ 3T gần nhất vs 3T trước đó
        float last3  = vals.TakeLast(3).Average();
        float prev3  = n > 3 ? vals.Skip(n - 6).Take(3).Average() : last3;
        if (prev3 == 0) return InventorySignal.Unknown;

        float velChange = (last3 - prev3) / prev3;

        if (velChange > 0.35f)  return InventorySignal.SurgeBuying;    // mua gom nhiều bất thường
        if (velChange > 0.12f)  return InventorySignal.Increasing;
        if (velChange < -0.35f) return InventorySignal.SharpDecline;
        if (velChange < -0.12f) return InventorySignal.Slowing;
        return InventorySignal.Stable;
    }

    // ── Mô tả khí hậu hiện tại ───────────────────────────────
    private static string GetClimateContext(ClimateRegion region, int month) =>
        (region, month) switch
        {
            (ClimateRegion.North, 12 or 1 or 2)  => "❄️ Đông lạnh Miền Bắc",
            (ClimateRegion.North, 3 or 4)         => "🌸 Xuân ấm Miền Bắc",
            (ClimateRegion.North, 5 or 6 or 7 or 8) => "☀️ Hè nóng ẩm Miền Bắc",
            (ClimateRegion.North, 9 or 10 or 11)  => "🍂 Thu mát Miền Bắc",
            (ClimateRegion.Central, 9 or 10 or 11 or 12) => "🌧️ Mùa mưa lũ Miền Trung",
            (ClimateRegion.Central, 6 or 7 or 8)  => "🌡️ Hè khô nóng Miền Trung",
            (ClimateRegion.Central, _)             => "🌤️ Khô ráo Miền Trung",
            (ClimateRegion.South, 5 or 6 or 7 or 8 or 9 or 10) => "🌦️ Mùa mưa Miền Nam",
            (ClimateRegion.South, _)               => "☀️ Mùa khô Miền Nam",
            _ => "🌍 Không xác định"
        };

    // ── Helpers ───────────────────────────────────────────────
    private static bool ContainsAny(string source, params string[] terms)
        => terms.Any(t => source.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static (int Year, int Month) NextMonth(int year, int month, int step)
    {
        int m = month + step;
        return (year + (m - 1) / 12, ((m - 1) % 12) + 1);
    }

    private static string MonthLabel(int month, int year) => $"T{month}/{year}";

    private static string CategoryLabel(ProductCategory cat) => cat switch
    {
        ProductCategory.Beverage     => "🥤 Đồ uống",
        ProductCategory.Confectionery=> "🍬 Bánh kẹo",
        ProductCategory.Condiment    => "🧂 Gia vị / Nước chấm",
        ProductCategory.InstantFood  => "🍜 Thực phẩm ăn liền",
        ProductCategory.FrozenFood   => "🧊 Đông lạnh",
        ProductCategory.PersonalCare => "🧴 Chăm sóc cá nhân",
        ProductCategory.Household    => "🧹 Tẩy rửa gia dụng",
        _                            => "📦 Hàng tiêu dùng khác"
    };

    private static string RegionLabel(ClimateRegion r) => r switch
    {
        ClimateRegion.North   => "Miền Bắc",
        ClimateRegion.Central => "Miền Trung",
        _                     => "Miền Nam"
    };
}

// ── Enums & Records ───────────────────────────────────────────

public enum ClimateRegion  { North, Central, South }
public enum ProductCategory
{
    Beverage, Confectionery, Condiment,
    InstantFood, FrozenFood, PersonalCare, Household, General
}
public enum InventorySignal
{
    Unknown, Stable, Increasing, Slowing,
    SurgeBuying, SharpDecline, PossibleStockout
}

public sealed record MonthSeasonFactor(
    int          Month,
    int          Year,
    string       Label,
    float        Factor,
    List<string> Events);

public sealed record FactorAnalysis(
    string              Region,
    string              Category,
    List<MonthSeasonFactor> MonthFactors,
    float               AvgSeasonalFactor,
    InventorySignal     InventorySignal,
    string              ClimateContext,
    ClimateRegion       RegionEnum,
    ProductCategory     CategoryEnum);
