namespace BlazorReporting.Services;

/// <summary>
/// UI Localization — VI / EN / ID
/// Scoped: moi circuit (user) co instance rieng, luu ngon ngu cua user do.
/// </summary>
public sealed class LocalizationService
{
    public string CurrentLang { get; private set; } = "VI";
    public event Action? OnChanged;

    public void SetLang(string lang)
    {
        if (lang == CurrentLang) return;
        CurrentLang = lang;
        OnChanged?.Invoke();
    }

    public string T(string key) => _dict.TryGetValue(key, out var m) && m.TryGetValue(CurrentLang, out var v) ? v : key;

    // ── Dictionary ────────────────────────────────────────────────────
    // Key : { VI, EN, ID }
    private static readonly Dictionary<string, Dictionary<string, string>> _dict = new()
    {
        // ── Navigation / Menu groups ─────────────────────────────────
        ["nav.overview"]       = new(){ ["VI"]="Tổng quan",        ["EN"]="Overview",       ["ID"]="Ikhtisar" },
        ["nav.reports"]        = new(){ ["VI"]="Báo cáo",          ["EN"]="Reports",        ["ID"]="Laporan" },
        ["nav.analysis"]       = new(){ ["VI"]="Phân tích",        ["EN"]="Analysis",       ["ID"]="Analisis" },
        ["nav.mcp"]            = new(){ ["VI"]="MCP",              ["EN"]="MCP",            ["ID"]="MCP" },
        ["nav.distribution"]   = new(){ ["VI"]="Phân phối",        ["EN"]="Distribution",   ["ID"]="Distribusi" },
        ["nav.admin"]          = new(){ ["VI"]="Quản trị",         ["EN"]="Administration", ["ID"]="Administrasi" },
        ["nav.issues"]         = new(){ ["VI"]="Issues",           ["EN"]="Issues",         ["ID"]="Masalah" },
        ["nav.config"]         = new(){ ["VI"]="Cấu hình",         ["EN"]="Configuration",  ["ID"]="Konfigurasi" },
        // ── Topbar ───────────────────────────────────────────────────
        ["topbar.guide"]       = new(){ ["VI"]="Hướng dẫn",        ["EN"]="Help",           ["ID"]="Bantuan" },
        ["topbar.logout"]      = new(){ ["VI"]="Đăng xuất",        ["EN"]="Logout",         ["ID"]="Keluar" },
        // ── Common buttons ───────────────────────────────────────────
        ["btn.view"]           = new(){ ["VI"]="Xem",              ["EN"]="View",           ["ID"]="Lihat" },
        ["btn.search"]         = new(){ ["VI"]="Tìm kiếm",         ["EN"]="Search",         ["ID"]="Cari" },
        ["btn.save"]           = new(){ ["VI"]="Lưu",              ["EN"]="Save",           ["ID"]="Simpan" },
        ["btn.cancel"]         = new(){ ["VI"]="Hủy",              ["EN"]="Cancel",         ["ID"]="Batal" },
        ["btn.add"]            = new(){ ["VI"]="Thêm",             ["EN"]="Add",            ["ID"]="Tambah" },
        ["btn.edit"]           = new(){ ["VI"]="Sửa",              ["EN"]="Edit",           ["ID"]="Edit" },
        ["btn.delete"]         = new(){ ["VI"]="Xóa",              ["EN"]="Delete",         ["ID"]="Hapus" },
        ["btn.refresh"]        = new(){ ["VI"]="Làm mới",          ["EN"]="Refresh",        ["ID"]="Perbarui" },
        ["btn.export"]         = new(){ ["VI"]="Xuất",             ["EN"]="Export",         ["ID"]="Ekspor" },
        ["btn.close"]          = new(){ ["VI"]="Đóng",             ["EN"]="Close",          ["ID"]="Tutup" },
        // ── Common labels ────────────────────────────────────────────
        ["lbl.from_date"]      = new(){ ["VI"]="Từ ngày",          ["EN"]="From date",      ["ID"]="Dari tanggal" },
        ["lbl.to_date"]        = new(){ ["VI"]="Đến ngày",         ["EN"]="To date",        ["ID"]="Sampai tanggal" },
        ["lbl.date"]           = new(){ ["VI"]="Ngày",             ["EN"]="Date",           ["ID"]="Tanggal" },
        ["lbl.salesman"]       = new(){ ["VI"]="Nhân viên BH",     ["EN"]="Salesman",       ["ID"]="Wiraniaga" },
        ["lbl.supervisor"]     = new(){ ["VI"]="Giám sát",         ["EN"]="Supervisor",     ["ID"]="Supervisor" },
        ["lbl.distributor"]    = new(){ ["VI"]="NPP",              ["EN"]="Distributor",    ["ID"]="Distributor" },
        ["lbl.route"]          = new(){ ["VI"]="Tuyến",            ["EN"]="Route",          ["ID"]="Rute" },
        ["lbl.region"]         = new(){ ["VI"]="Khu vực",          ["EN"]="Region",         ["ID"]="Wilayah" },
        ["lbl.outlet"]         = new(){ ["VI"]="Cửa hàng",         ["EN"]="Outlet",         ["ID"]="Toko" },
        ["lbl.status"]         = new(){ ["VI"]="Trạng thái",       ["EN"]="Status",         ["ID"]="Status" },
        ["lbl.all"]            = new(){ ["VI"]="Tất cả",           ["EN"]="All",            ["ID"]="Semua" },
        ["lbl.no_data"]        = new(){ ["VI"]="Không có dữ liệu", ["EN"]="No data",        ["ID"]="Tidak ada data" },
        ["lbl.loading"]        = new(){ ["VI"]="Đang tải...",      ["EN"]="Loading...",     ["ID"]="Memuat..." },
        // ── Dashboard ────────────────────────────────────────────────
        ["dash.title"]         = new(){ ["VI"]="Dashboard",        ["EN"]="Dashboard",      ["ID"]="Dasbor" },
        ["dash.total_sm"]      = new(){ ["VI"]="Tổng NVBH",        ["EN"]="Total Salesmen", ["ID"]="Total Wiraniaga" },
        ["dash.synced"]        = new(){ ["VI"]="Đã đồng bộ",       ["EN"]="Synced",         ["ID"]="Tersinkron" },
        ["dash.not_synced"]    = new(){ ["VI"]="Chưa đồng bộ",     ["EN"]="Not Synced",     ["ID"]="Belum Sinkron" },
        ["dash.selling"]       = new(){ ["VI"]="Đang bán hàng",    ["EN"]="Selling",        ["ID"]="Sedang Berjualan" },
        ["dash.not_selling"]   = new(){ ["VI"]="Chưa bán hàng",    ["EN"]="Not Selling",    ["ID"]="Belum Berjualan" },
        ["dash.has_order"]     = new(){ ["VI"]="Đã có đơn hàng",   ["EN"]="Has Orders",     ["ID"]="Ada Pesanan" },
        ["dash.today"]         = new(){ ["VI"]="hôm nay",          ["EN"]="today",          ["ID"]="hari ini" },
        ["dash.target"]        = new(){ ["VI"]="Chỉ tiêu tháng",   ["EN"]="Monthly Target", ["ID"]="Target Bulanan" },
        ["dash.achieved"]      = new(){ ["VI"]="Doanh số tháng",   ["EN"]="Monthly Sales",  ["ID"]="Penjualan Bulanan" },
        ["dash.achieved_pct"]  = new(){ ["VI"]="% Đạt chỉ tiêu",   ["EN"]="Achievement %",  ["ID"]="% Pencapaian" },
        ["dash.today_sales"]   = new(){ ["VI"]="Doanh số hôm nay", ["EN"]="Today Sales",    ["ID"]="Penjualan Hari Ini" },
        // ── Grid / Table ─────────────────────────────────────────────
        ["grid.search"]        = new(){ ["VI"]="Tìm kiếm...",      ["EN"]="Search...",      ["ID"]="Cari..." },
        ["grid.filter"]        = new(){ ["VI"]="Lọc...",           ["EN"]="Filter...",      ["ID"]="Filter..." },
        ["grid.action"]        = new(){ ["VI"]="Thao tác",         ["EN"]="Actions",        ["ID"]="Aksi" },
        ["grid.rows"]          = new(){ ["VI"]="dòng",             ["EN"]="rows",           ["ID"]="baris" },
        // ── Login ─────────────────────────────────────────────────────
        ["login.title"]        = new(){ ["VI"]="Đăng nhập",        ["EN"]="Sign In",        ["ID"]="Masuk" },
        ["login.username"]     = new(){ ["VI"]="Tên đăng nhập",    ["EN"]="Username",       ["ID"]="Nama Pengguna" },
        ["login.password"]     = new(){ ["VI"]="Mật khẩu",         ["EN"]="Password",       ["ID"]="Kata Sandi" },
        ["login.btn"]          = new(){ ["VI"]="Đăng nhập",        ["EN"]="Sign In",        ["ID"]="Masuk" },
        ["login.loading"]      = new(){ ["VI"]="Đang đăng nhập…",  ["EN"]="Signing in…",    ["ID"]="Sedang masuk…" },
        ["login.error"]        = new(){ ["VI"]="Tên đăng nhập hoặc mật khẩu không đúng.", ["EN"]="Invalid username or password.", ["ID"]="Nama pengguna atau kata sandi salah." },
        // ── AI Chatbot ────────────────────────────────────────────────
        ["ai.title"]           = new(){ ["VI"]="DMSPro AI",        ["EN"]="DMSPro AI",      ["ID"]="DMSPro AI" },
        ["ai.placeholder"]     = new(){ ["VI"]="Nhập câu hỏi… (Enter gửi)", ["EN"]="Type a question… (Enter to send)", ["ID"]="Ketik pertanyaan… (Enter kirim)" },
        ["ai.offline"]         = new(){ ["VI"]="Offline",          ["EN"]="Offline",        ["ID"]="Offline" },
        ["ai.online"]          = new(){ ["VI"]="Online",           ["EN"]="Online",         ["ID"]="Online" },
        ["ai.connecting"]      = new(){ ["VI"]="Kết nối…",         ["EN"]="Connecting…",    ["ID"]="Menghubungkan…" },
        ["ai.clear"]           = new(){ ["VI"]="Xoá hội thoại",    ["EN"]="Clear chat",     ["ID"]="Hapus percakapan" },
        ["ai.greeting_morning"]= new(){ ["VI"]="buổi sáng",        ["EN"]="morning",        ["ID"]="pagi" },
        ["ai.greeting_afternoon"]=new(){["VI"]="buổi chiều",       ["EN"]="afternoon",      ["ID"]="siang" },
        ["ai.greeting_evening"]= new(){ ["VI"]="buổi tối",         ["EN"]="evening",        ["ID"]="malam" },
        ["ai.greeting_emoji_morning"]  = new(){ ["VI"]="🌅",       ["EN"]="🌅",             ["ID"]="🌅" },
        ["ai.greeting_emoji_afternoon"]= new(){ ["VI"]="☀️",       ["EN"]="☀️",             ["ID"]="☀️" },
        ["ai.greeting_emoji_evening"]  = new(){ ["VI"]="🌙",       ["EN"]="🌙",             ["ID"]="🌙" },
        ["ai.hint"]            = new(){ ["VI"]="Enter gửi · Shift+Enter xuống dòng",
                                         ["EN"]="Enter to send · Shift+Enter new line",
                                         ["ID"]="Enter kirim · Shift+Enter baris baru" },
        ["ai.empty_sub"]       = new(){ ["VI"]="Tôi có thể giúp bạn phân tích dữ liệu, điều hướng báo cáo hoặc bản đồ.",
                                         ["EN"]="I can help you analyze data, navigate reports or maps.",
                                         ["ID"]="Saya dapat membantu Anda menganalisis data, menavigasi laporan atau peta." },
        // ── Lang names ────────────────────────────────────────────────
        ["lang.VI"]            = new(){ ["VI"]="Tiếng Việt",       ["EN"]="Vietnamese",     ["ID"]="Bahasa Vietnam" },
        ["lang.EN"]            = new(){ ["VI"]="Tiếng Anh",        ["EN"]="English",        ["ID"]="Bahasa Inggris" },
        ["lang.ID"]            = new(){ ["VI"]="Bahasa Indonesia",  ["EN"]="Indonesian",     ["ID"]="Bahasa Indonesia" },
    };
}
