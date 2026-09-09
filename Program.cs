using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var m = new Mutex(true, "BaguaLiveWallpaper.SingleInstance", out bool first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        try { Application.Run(new Wallpaper()); }
        catch (Exception ex) { Log(ex); }
    }

    static void Log(Exception ex)
    {
        try
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BaguaLiveWallpaper");
            Directory.CreateDirectory(d);
            File.AppendAllText(Path.Combine(d, "error.log"), ex + Environment.NewLine);
        }
        catch { }
    }
}

internal sealed class Wallpaper : Form
{
    const int GWL_STYLE = -16, WM_NCHITTEST = 0x84, WM_HOTKEY = 0x312, HTTRANSPARENT = -1, HOTKEY_ID = 0x4247;
    const long WS_CHILD = 0x40000000L;
    const uint MOD_ALT = 1, MOD_CONTROL = 2, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40, WM_SPAWN_WORKER = 0x052C;
    const double TURN = 24.0;

    readonly System.Windows.Forms.Timer timer;
    readonly Stopwatch clock = Stopwatch.StartNew();

    // Full outer-ring structure from the previous good version. Nothing is removed.
    static readonly float[] R = { 56, 73, 87, 101, 116, 130, 144, 160, 174, 189, 203, 218, 231, 246, 260, 287, 303, 319 };
    static readonly int[] Sectors = { 8, 8, 8, 16, 16, 16, 32, 32, 32, 32, 32, 32, 32, 64, 64, 64, 64 };

    static readonly bool[][] TrigramBits =
    {
        new[] { true, true, true }, new[] { true, true, false }, new[] { true, false, true }, new[] { false, true, true },
        new[] { true, false, false }, new[] { false, true, false }, new[] { false, false, true }, new[] { false, false, false }
    };
    static readonly string[] Trigrams = { "乾", "兑", "离", "震", "巽", "坎", "艮", "坤" };
    static readonly string[] Stems = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
    static readonly string[] Branches = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
    static readonly string[] Elements = { "木", "火", "土", "金", "水" };
    static readonly string[] Seasons = { "春", "夏", "秋", "冬", "东", "南", "西", "北" };
    static readonly string[] SolarTerms = { "立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至","小寒","大寒" };
    static readonly string[] Hexagrams =
    {
        "乾为天","坤为地","水雷屯","山水蒙","水天需","天水讼","地水师","水地比","风天小畜","天泽履","地天泰","天地否","天火同人","火天大有","地山谦","雷地豫",
        "泽雷随","山风蛊","地泽临","风地观","火雷噬嗑","山火贲","山地剥","地雷复","天雷无妄","山天大畜","山雷颐","泽风大过","坎为水","离为火","泽山咸","雷风恒",
        "天山遁","雷天大壮","火地晋","地火明夷","风火家人","火泽睽","水山蹇","雷水解","山泽损","风雷益","泽天夬","天风姤","泽地萃","地风升","泽水困","水风井",
        "泽火革","火风鼎","震为雷","艮为山","风山渐","雷泽归妹","雷火丰","火山旅","巽为风","兑为泽","风水涣","水泽节","风泽中孚","雷山小过","水火既济","火水未济"
    };
    static readonly string[] Misc = { "元","亨","利","贞","吉","凶","悔","吝","往","来","进","退","生","旺","休","囚","死","阴","阳","少阴","少阳","老阴","老阳","天","地","人","日","月","山","泽","雷","风","水","火" };

    public Wallpaper()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = SystemInformation.VirtualScreen.Location;
        ClientSize = SystemInformation.VirtualScreen.Size;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        DoubleBuffered = true;
        TopMost = false;
        timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) => Invalidate();
        Shown += (_, _) => Attach();
        FormClosed += (_, _) => { timer.Stop(); if (IsHandleCreated) Native.UnregisterHotKey(Handle, HOTKEY_ID); };
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); timer.Start(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float s = Math.Min(ClientSize.Width / 1920f, ClientSize.Height / 1080f);
        float cx = ClientSize.Width / 2f, cy = ClientSize.Height / 2f;
        double a = clock.Elapsed.TotalSeconds / TURN * 360.0;
        g.TranslateTransform(cx, cy);

        using var fine = new Pen(Color.White, Math.Max(.65f, .95f * s));
        using var medium = new Pen(Color.White, Math.Max(.9f, 1.25f * s));
        using var bold = new Pen(Color.White, Math.Max(1.25f, 1.8f * s));

        // Draw all circular boundaries first. Every original outer ring remains present.
        for (int i = 0; i < R.Length; i++)
        {
            float r = R[i] * s;
            g.DrawEllipse(i == 0 || i == R.Length - 1 ? medium : fine, -r, -r, 2 * r, 2 * r);
        }

        // Each annular band is independently rotated. There are 17 separate rotating bands.
        // Adjacent bands alternate direction and use slightly different speeds so their relative motion is obvious.
        double[] speed = { 1.00, .93, 1.07, .96, 1.04, .91, 1.09, .95, 1.06, .92, 1.10, .97, 1.05, .90, 1.08, .94, 1.02 };
        double[] phase = { 0, 7, 14, 21, 28, 35, 42, 49, 56, 63, 70, 77, 84, 91, 98, 105, 112 };
        string[][] labels = { Trigrams, Stems, Branches, Hexagrams, Hexagrams, Misc, Stems, Branches, Elements, Misc, Seasons, Hexagrams, Misc, Hexagrams, SolarTerms, Misc, Hexagrams };
        float[] fontPx = { 13f, 11.5f, 11.5f, 10.5f, 10f, 9.7f, 9.5f, 9.5f, 9.3f, 9.2f, 9.2f, 9.0f, 8.8f, 8.6f, 8.6f, 8.5f, 8.5f };

        for (int band = 0; band < Sectors.Length; band++)
        {
            double dir = (band % 2 == 0) ? 1.0 : -1.0;
            double angle = a * speed[band] * dir + phase[band];
            DrawBand(g, R[band], R[band + 1], Sectors[band], angle, labels[band], fontPx[band], s, fine, medium);
        }

        // Fixed center: never rotates.
        DrawTaiji(g, 54f * s, bold);
        g.ResetTransform();
    }

    static void DrawBand(Graphics g, float inner, float outer, int count, double angle, string[] labels, float fontPx, float s, Pen fine, Pen medium)
    {
        var state = g.Save();
        g.RotateTransform((float)angle);

        float r1 = inner * s, r2 = outer * s;
        Pen divider = count <= 16 ? medium : fine;
        float radius = (r1 + r2) * .5f;
        float bandWidth = (r2 - r1);
        float cellArc = (float)(radius * 2 * Math.PI / count);

        // Radial dividers belong only to this band and therefore rotate with it.
        for (int i = 0; i < count; i++)
        {
            double q = -Math.PI / 2 + i * 2 * Math.PI / count;
            g.DrawLine(divider,
                (float)Math.Cos(q) * r1, (float)Math.Sin(q) * r1,
                (float)Math.Cos(q) * r2, (float)Math.Sin(q) * r2);
        }

        using var white = new SolidBrush(Color.White);
        using var black = new SolidBrush(Color.Black);
        using var font = new Font("Microsoft YaHei UI", Math.Max(7.5f, fontPx * s), FontStyle.Regular, GraphicsUnit.Pixel);

        for (int i = 0; i < count; i++)
        {
            string text = labels[i % labels.Length];
            double q = -Math.PI / 2 + (i + .5) * 2 * Math.PI / count;
            float x = (float)Math.Cos(q) * radius;
            float y = (float)Math.Sin(q) * radius;

            var ts = g.Save();
            g.TranslateTransform(x, y);
            float deg = (float)(q * 180 / Math.PI + 90);
            if (deg > 90 && deg < 270) deg += 180;
            g.RotateTransform(deg);

            float maxWidth = Math.Max(12f, cellArc * .82f);
            SizeF measured = g.MeasureString(text, font);
            float useSize = font.Size;
            if (measured.Width > maxWidth) useSize = Math.Max(7.5f * s, font.Size * maxWidth / measured.Width);

            using var fit = new Font("Microsoft YaHei UI", useSize, FontStyle.Regular, GraphicsUnit.Pixel);
            measured = g.MeasureString(text, fit);

            // Black knockout behind each glyph prevents radial grid lines from cutting through text.
            float padX = Math.Min(4f * s, Math.Max(1.5f, measured.Width * .08f));
            float padY = Math.Min(3f * s, Math.Max(1f, measured.Height * .12f));
            g.FillRectangle(black, -measured.Width / 2 - padX, -measured.Height / 2 - padY, measured.Width + 2 * padX, measured.Height + 2 * padY);
            g.DrawString(text, fit, white, -measured.Width / 2f, -measured.Height / 2f);
            g.Restore(ts);
        }

        g.Restore(state);
    }

    static void DrawTaiji(Graphics g, float radius, Pen outline)
    {
        float d = radius * 2;
        using var black = new SolidBrush(Color.Black);
        using var white = new SolidBrush(Color.White);
        g.FillEllipse(black, -radius, -radius, d, d);
        g.DrawEllipse(outline, -radius, -radius, d, d);
        g.FillPie(white, -radius, -radius, d, d, 90, 180);
        g.FillPie(black, -radius, -radius, d, d, 270, 180);
        g.FillEllipse(white, -radius / 2, -radius, radius, radius);
        g.FillEllipse(black, -radius / 2, 0, radius, radius);
        float dot = radius * .22f;
        g.FillEllipse(black, -dot / 2, -radius * .55f, dot, dot);
        g.FillEllipse(white, -dot / 2, radius * .33f, dot, dot);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)HTTRANSPARENT; return; }
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) { Close(); return; }
        base.WndProc(ref m);
    }

    void Attach()
    {
        IntPtr prog = Native.FindWindow("Progman", null);
        if (prog == IntPtr.Zero) return;
        Native.SendMessageTimeout(prog, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
        IntPtr worker = IntPtr.Zero;
        Native.EnumWindows((h, _) =>
        {
            if (Native.FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                worker = Native.FindWindowEx(IntPtr.Zero, h, "WorkerW", null);
                return false;
            }
            return true;
        }, IntPtr.Zero);
        if (worker == IntPtr.Zero) worker = prog;
        IntPtr style = Native.GetWindowLongPtr(Handle, GWL_STYLE);
        Native.SetWindowLongPtr(Handle, GWL_STYLE, (IntPtr)(style.ToInt64() | WS_CHILD));
        Native.SetParent(Handle, worker);
        Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, ClientSize.Width, ClientSize.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        Native.RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.Q);
    }

    static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? c, string? n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string? c, string? n);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
        [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr c, IntPtr p);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint m, uint v);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
    }
}
