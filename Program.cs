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
    private static void Main()
    {
        using var mutex = new Mutex(true, "BaguaLiveWallpaper.SingleInstance", out bool first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        try { Application.Run(new Wallpaper()); }
        catch (Exception ex) { Log(ex); }
    }

    private static void Log(Exception ex)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BaguaLiveWallpaper");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), ex + Environment.NewLine);
        }
        catch { }
    }
}

internal sealed class Wallpaper : Form
{
    private const int GWL_STYLE = -16;
    private const int WM_NCHITTEST = 0x84;
    private const int WM_HOTKEY = 0x312;
    private const int HTTRANSPARENT = -1;
    private const int HOTKEY_ID = 0x4247;
    private const long WS_CHILD = 0x40000000L;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint WM_SPAWN_WORKER = 0x052C;
    private const double SecondsPerTurn = 32.0;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private static readonly float[] R = { 56, 76, 96, 116, 150, 175, 200, 225, 260, 290, 320, 350, 385, 420 };
    private static readonly int[] N = { 8, 8, 16, 16, 16, 32, 32, 32, 32, 32, 64, 64, 64 };

    private static readonly bool[][] TrigramBits =
    {
        new[] { true, true, true }, new[] { true, true, false }, new[] { true, false, true }, new[] { false, true, true },
        new[] { true, false, false }, new[] { false, true, false }, new[] { false, false, true }, new[] { false, false, false }
    };
    private static readonly string[] Trigrams = { "乾", "兑", "离", "震", "巽", "坎", "艮", "坤" };
    private static readonly string[] Stems = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
    private static readonly string[] Branches = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
    private static readonly string[] Elements = { "木", "火", "土", "金", "水" };
    private static readonly string[] Directions = { "东", "南", "西", "北", "中", "乾", "坤", "艮", "巽", "离", "坎", "震", "兑" };
    private static readonly string[] SolarTerms =
    {
        "立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑",
        "立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至","小寒","大寒"
    };
    private static readonly string[] HexagramNames =
    {
        "乾","坤","屯","蒙","需","讼","师","比","小畜","履","泰","否","同人","大有","谦","豫",
        "随","蛊","临","观","噬嗑","贲","剥","复","无妄","大畜","颐","大过","坎","离","咸","恒",
        "遁","大壮","晋","明夷","家人","睽","蹇","解","损","益","夬","姤","萃","升","困","井",
        "革","鼎","震","艮","渐","归妹","丰","旅","巽","兑","涣","节","中孚","小过","既济","未济"
    };
    private static readonly string[] Misc =
    {
        "元","亨","利","贞","吉","凶","悔","吝","往","来","进","退","生","旺","休","囚","死",
        "阴","阳","少阴","少阳","老阴","老阳","天","地","人","日","月","山","泽","雷","风","水","火"
    };

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
        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += (_, _) => Invalidate();
        Shown += (_, _) => AttachToDesktop();
        FormClosed += (_, _) => { _timer.Stop(); if (IsHandleCreated) Native.UnregisterHotKey(Handle, HOTKEY_ID); };
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); _timer.Start(); }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Color.Black);

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float scale = Math.Min(ClientSize.Width / 1920f, ClientSize.Height / 1080f);
        float cx = ClientSize.Width / 2f;
        float cy = ClientSize.Height / 2f;
        double angle = _clock.Elapsed.TotalSeconds / SecondsPerTurn * 360.0;

        g.TranslateTransform(cx, cy);
        using var fine = new Pen(Color.White, Math.Max(.8f, 1.05f * scale));
        using var medium = new Pen(Color.White, Math.Max(1.1f, 1.55f * scale));
        using var bold = new Pen(Color.White, Math.Max(1.5f, 2.0f * scale));

        DrawAllBands(g, scale, fine, medium, angle);
        DrawTaiji(g, 56f * scale, bold);
        g.ResetTransform();
    }

    private static void DrawAllBands(Graphics g, float s, Pen fine, Pen medium, double angle)
    {
        // Preserve the exact mother geometry: same circles, same radii and same line weights.
        for (int i = 0; i < R.Length; i++)
        {
            float r = R[i] * s;
            bool mediumCircle = i == 0 || i == 3 || i == 6 || i == 10 || i == 13;
            g.DrawEllipse(mediumCircle ? medium : fine, -r, -r, 2 * r, 2 * r);
        }

        // Only animation is changed: every annular band rotates independently,
        // at the same speed, with adjacent bands alternating direction.
        // The phase values reproduce the mother version's four-layer initial layout exactly.
        double[] phase = { 0, 0, 0, 0, 7.5, 7.5, 7.5, 15.0, 15.0, 15.0, 15.0, 22.5, 22.5 };

        for (int band = 0; band < N.Length; band++)
        {
            double direction = (band % 2 == 0) ? 1.0 : -1.0;
            var state = g.Save();
            g.RotateTransform((float)(angle * direction + phase[band]));

            int n = N[band];
            float r1 = R[band] * s;
            float r2 = R[band + 1] * s;
            Pen p = n <= 16 ? medium : fine;
            for (int i = 0; i < n; i++)
            {
                double a = -Math.PI / 2 + i * 2 * Math.PI / n;
                g.DrawLine(p,
                    (float)Math.Cos(a) * r1, (float)Math.Sin(a) * r1,
                    (float)Math.Cos(a) * r2, (float)Math.Sin(a) * r2);
            }

            DrawBandText(g, s, band);
            if (band == 1) DrawTrigrams(g, s);
            g.Restore(state);
        }
    }

    private static void DrawBandText(Graphics g, float s, int band)
    {
        if (band == 1) Ring(g, 76, 96, Trigrams, 8, 16f, s, .82f);
        else if (band == 2) Ring(g, 96, 116, Misc, 16, 13.5f, s, .82f);
        else if (band == 3) Ring(g, 116, 150, Stems, 16, 15f, s, .80f);
        else if (band == 4) Ring(g, 150, 175, Branches, 16, 14f, s, .80f);
        else if (band == 5) Ring(g, 175, 200, HexagramNames, 32, 12.5f, s, .82f);
        else if (band == 6) Ring(g, 200, 225, Directions, 32, 12.5f, s, .82f);
        else if (band == 7) Ring(g, 225, 260, HexagramNames, 32, 12f, s, .80f);
        else if (band == 8) Ring(g, 260, 290, Misc, 32, 11.5f, s, .82f);
        else if (band == 9) Ring(g, 290, 320, Elements, 32, 11.5f, s, .82f);
        else if (band == 10) Ring(g, 320, 350, Stems, 64, 11.5f, s, .80f);
        else if (band == 11) Ring(g, 350, 385, SolarTerms, 64, 11.2f, s, .78f);
        else if (band == 12) Ring(g, 385, 420, HexagramNames, 64, 11.2f, s, .80f);
    }

    private static void Ring(Graphics g, float inner, float outer, string[] labels, int count, float fontPx, float s, float widthFactor)
    {
        using var brush = new SolidBrush(Color.White);
        using var font = new Font("Microsoft YaHei UI", Math.Max(8f, fontPx * s), FontStyle.Regular, GraphicsUnit.Pixel);
        float radius = (inner + outer) * .5f * s;
        float cellArc = (float)(radius * 2 * Math.PI / count);
        float maxWidth = cellArc * widthFactor;

        for (int i = 0; i < count; i++)
        {
            string text = labels[i % labels.Length];
            double a = -Math.PI / 2 + (i + .5) * 2 * Math.PI / count;
            float x = (float)Math.Cos(a) * radius;
            float y = (float)Math.Sin(a) * radius;

            var state = g.Save();
            g.TranslateTransform(x, y);
            float deg = (float)(a * 180 / Math.PI + 90);
            if (deg > 90 && deg < 270) deg += 180;
            g.RotateTransform(deg);

            SizeF size = g.MeasureString(text, font);
            float fs = font.Size;
            if (size.Width > maxWidth) fs = Math.Max(8f * s, font.Size * maxWidth / size.Width);
            using var fit = new Font("Microsoft YaHei UI", fs, FontStyle.Regular, GraphicsUnit.Pixel);
            size = g.MeasureString(text, fit);
            g.DrawString(text, fit, brush, -size.Width / 2f, -size.Height / 2f);
            g.Restore(state);
        }
    }

    private static void DrawTrigrams(Graphics g, float s)
    {
        for (int i = 0; i < 8; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / 4;
            float radius = 86f * s;
            var state = g.Save();
            g.TranslateTransform((float)Math.Cos(a) * radius, (float)Math.Sin(a) * radius);
            g.RotateTransform((float)(a * 180 / Math.PI + 90));
            DrawTrigram(g, TrigramBits[i], 15f * s, 3.2f * s);
            g.Restore(state);
        }
    }

    private static void DrawTrigram(Graphics g, bool[] bits, float width, float gap)
    {
        using var pen = new Pen(Color.White, Math.Max(1.8f, gap * .8f)) { StartCap = LineCap.Square, EndCap = LineCap.Square };
        for (int i = 0; i < 3; i++)
        {
            float y = (i - 1) * gap * 2.2f;
            if (bits[i]) g.DrawLine(pen, -width, y, width, y);
            else
            {
                g.DrawLine(pen, -width, y, -width * .18f, y);
                g.DrawLine(pen, width * .18f, y, width, y);
            }
        }
    }

    private static void DrawTaiji(Graphics g, float radius, Pen outline)
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

    private void AttachToDesktop()
    {
        IntPtr progman = Native.FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return;
        Native.SendMessageTimeout(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
        IntPtr worker = IntPtr.Zero;
        Native.EnumWindows((hWnd, _) =>
        {
            IntPtr shell = Native.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shell != IntPtr.Zero)
            {
                worker = Native.FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                return false;
            }
            return true;
        }, IntPtr.Zero);
        if (worker == IntPtr.Zero) worker = progman;
        IntPtr style = Native.GetWindowLongPtr(Handle, GWL_STYLE);
        Native.SetWindowLongPtr(Handle, GWL_STYLE, (IntPtr)(style.ToInt64() | WS_CHILD));
        Native.SetParent(Handle, worker);
        Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, ClientSize.Width, ClientSize.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        Native.RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.Q);
    }

    private static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? className, string? windowName);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? windowName);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    }
}
