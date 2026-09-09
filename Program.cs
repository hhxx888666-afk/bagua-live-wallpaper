using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
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

    internal static void Log(Exception ex)
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
    private const int WM_ERASEBKGND = 0x14;
    private const int WM_HOTKEY = 0x312;
    private const int HTTRANSPARENT = -1;
    private const int HOTKEY_ID = 0x4247;
    private const long WS_CHILD = 0x40000000L;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const double SecondsPerTurn = 24.0;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private static readonly IntPtr Bottom = new(1);

    private static readonly bool[][] Trigrams =
    {
        new[] { true, true, true }, new[] { false, false, false },
        new[] { false, true, false }, new[] { true, false, true },
        new[] { false, false, true }, new[] { true, false, false },
        new[] { true, true, false }, new[] { false, true, true }
    };

    private static readonly string[] TrigramNames = { "乾", "坤", "坎", "离", "震", "巽", "艮", "兑" };

    private static readonly string[] Hexagrams =
    {
        "乾为天","坤为地","水雷屯","山水蒙","水天需","天水讼","地水师","水地比",
        "风天小畜","天泽履","地天泰","天地否","天火同人","火天大有","地山谦","雷地豫",
        "泽雷随","山风蛊","地泽临","风地观","火雷噬嗑","山火贲","山地剥","地雷复",
        "天雷无妄","山天大畜","山雷颐","泽风大过","坎为水","离为火","泽山咸","雷风恒",
        "天山遁","雷天大壮","火地晋","地火明夷","风火家人","火泽睽","水山蹇","雷水解",
        "山泽损","风雷益","泽天夬","天风姤","泽地萃","地风升","泽水困","水风井",
        "泽火革","火风鼎","震为雷","艮为山","风山渐","雷泽归妹","雷火丰","火山旅",
        "巽为风","兑为泽","风水涣","水泽节","风泽中孚","雷山小过","水火既济","火水未济"
    };

    private static readonly string[] Stems = { "甲","乙","丙","丁","戊","己","庚","辛","壬","癸" };
    private static readonly string[] Branches = { "子","丑","寅","卯","辰","巳","午","未","申","酉","戌","亥" };
    private static readonly string[] SolarTerms =
    {
        "立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑",
        "立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至","小寒","大寒"
    };

    // Short classical labels keep the reference video density while remaining readable at 1920x1080.
    private static readonly string[] InnerWords =
    {
        "乾","兑","离","震","巽","坎","艮","坤",
        "天","泽","火","雷","风","水","山","地",
        "阳","阴","少阳","少阴","老阳","老阴","先天","后天",
        "太极","两仪","四象","八卦","六十四卦"
    };

    private static readonly string[] FiveElements = { "木","火","土","金","水" };

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
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            if (IsHandleCreated) Native.UnregisterHotKey(Handle, HOTKEY_ID);
        };
    }

    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Color.Black);

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float scale = Math.Min(ClientSize.Width / 1920f, ClientSize.Height / 1080f);
        float cx = ClientSize.Width / 2f;
        float cy = ClientSize.Height / 2f;
        float r = 330f * scale;
        double rotation = _clock.Elapsed.TotalSeconds / SecondsPerTurn * 360.0;

        g.TranslateTransform(cx, cy);
        g.RotateTransform((float)rotation);

        using var thin = new Pen(Color.White, Math.Max(0.75f, 1.05f * scale));
        using var medium = new Pen(Color.White, Math.Max(1.0f, 1.45f * scale));
        using var strong = new Pen(Color.White, Math.Max(1.5f, 2.1f * scale));

        DrawReferenceGrid(g, r, thin, medium, strong);
        DrawDenseTextRings(g, r, scale);
        DrawTrigrams(g, r, scale);
        DrawYinYang(g, r * .145f, strong);
        g.ResetTransform();
    }

    private static void DrawReferenceGrid(Graphics g, float r, Pen thin, Pen medium, Pen strong)
    {
        // Ten clean concentric rings. The reference uses a dense, evenly stepped radial structure.
        for (int i = 1; i <= 10; i++)
        {
            float rr = r * i / 10f;
            Pen p = i == 1 || i == 10 ? medium : thin;
            g.DrawEllipse(p, -rr, -rr, rr * 2, rr * 2);
        }

        // Main 64 sectors: from the trigram band outward.
        for (int i = 0; i < 64; i++)
        {
            double a = i * Math.PI * 2 / 64.0;
            float x1 = (float)Math.Cos(a) * r * .48f;
            float y1 = (float)Math.Sin(a) * r * .48f;
            float x2 = (float)Math.Cos(a) * r;
            float y2 = (float)Math.Sin(a) * r;
            g.DrawLine(thin, x1, y1, x2, y2);
        }

        // Alternating half-sector boundaries create the small cells seen in the reference.
        for (int i = 0; i < 64; i++)
        {
            double a = (i + .5) * Math.PI * 2 / 64.0;
            float x1 = (float)Math.Cos(a) * r * .69f;
            float y1 = (float)Math.Sin(a) * r * .69f;
            float x2 = (float)Math.Cos(a) * r;
            float y2 = (float)Math.Sin(a) * r;
            g.DrawLine(thin, x1, y1, x2, y2);
        }

        // Inner eight-sector band.
        for (int i = 0; i < 8; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / 4;
            float x1 = (float)Math.Cos(a) * r * .27f;
            float y1 = (float)Math.Sin(a) * r * .27f;
            float x2 = (float)Math.Cos(a) * r * .48f;
            float y2 = (float)Math.Sin(a) * r * .48f;
            g.DrawLine(medium, x1, y1, x2, y2);
        }

        // A strong boundary around the trigram band.
        g.DrawEllipse(strong, -r * .48f, -r * .48f, r * .96f, r * .96f);
    }

    private static void DrawDenseTextRings(Graphics g, float r, float scale)
    {
        float ringWidth = r / 10f;

        // Ring 1: eight trigrams/attributes immediately around the central symbol.
        DrawSectorRing(g, r * .39f, r * .46f, TrigramNames, 8, Math.Max(9.0f, 13f * scale), scale, true, false);

        // Rings 2-5: four dense rows. Each sector contains a compact label, matching the reference's many small cells.
        DrawSectorRing(g, r * .49f, r * .56f, Hexagrams, 64, Math.Max(7.0f, 9.7f * scale), scale, false, false);
        DrawSectorRing(g, r * .57f, r * .64f, Hexagrams, 64, Math.Max(7.0f, 9.2f * scale), scale, false, true);
        DrawSectorRing(g, r * .65f, r * .72f, Stems, 64, Math.Max(7.0f, 9.3f * scale), scale, false, false);
        DrawSectorRing(g, r * .73f, r * .80f, Branches, 64, Math.Max(7.0f, 9.2f * scale), scale, false, true);

        // Ring 6: five-element / polarity labels.
        DrawSectorRing(g, r * .81f, r * .87f, FiveElements, 64, Math.Max(7.0f, 9.0f * scale), scale, false, false);

        // Outer ring: exactly 24 solar terms, spaced like the outer labels of the reference.
        DrawSectorRing(g, r * .88f, r * .995f, SolarTerms, 24, Math.Max(8.0f, 11.5f * scale), scale, false, true);

        // A fine supplementary text ring gives the same dense edge texture without turning the characters into a blur.
        DrawSectorRing(g, r * .835f, r * .875f, InnerWords, 64, Math.Max(6.2f, 8.0f * scale), scale, false, true);
    }

    private static void DrawSectorRing(Graphics g, float innerRadius, float outerRadius, string[] labels, int count, float fontSize, float scale, bool eightWay, bool reverse)
    {
        using var font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);

        float radius = (innerRadius + outerRadius) / 2f;
        float angularCell = (float)(radius * Math.PI * 2 / count);

        for (int i = 0; i < count; i++)
        {
            string text = labels[i % labels.Length];
            double angle = -Math.PI / 2 + (i + .5) * Math.PI * 2 / count;
            if (reverse) angle = -Math.PI / 2 - (i + .5) * Math.PI * 2 / count;

            float x = (float)Math.Cos(angle) * radius;
            float y = (float)Math.Sin(angle) * radius;

            var state = g.Save();
            g.TranslateTransform(x, y);

            float degrees = (float)(angle * 180 / Math.PI + 90);
            if (degrees > 90 && degrees < 270) degrees += 180;
            g.RotateTransform(degrees);

            SizeF sz = g.MeasureString(text, font);
            float maxWidth = angularCell * (eightWay ? .72f : .82f);
            float maxHeight = outerRadius - innerRadius - 1f * scale;

            // Fit multi-character labels into their cell without changing the ring's spacing.
            float drawSize = fontSize;
            if (sz.Width > maxWidth)
            {
                drawSize = Math.Max(fontSize * .72f, fontSize * maxWidth / sz.Width);
                using var fitted = new Font("Microsoft YaHei UI", drawSize, FontStyle.Regular, GraphicsUnit.Pixel);
                sz = g.MeasureString(text, fitted);
                g.DrawString(text, fitted, brush, -sz.Width / 2f, -Math.Min(sz.Height, maxHeight) / 2f);
            }
            else
            {
                g.DrawString(text, font, brush, -sz.Width / 2f, -Math.Min(sz.Height, maxHeight) / 2f);
            }

            g.Restore(state);
        }
    }

    private static void DrawTrigrams(Graphics g, float r, float scale)
    {
        using var brush = new SolidBrush(Color.White);
        using var font = new Font("Microsoft YaHei UI", Math.Max(10f, r * .038f), FontStyle.Bold, GraphicsUnit.Pixel);

        for (int i = 0; i < 8; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / 4;
            float rr = r * .475f;
            float x = (float)Math.Cos(a) * rr;
            float y = (float)Math.Sin(a) * rr;

            var state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform((float)(a * 180 / Math.PI + 90));
            DrawTrigram(g, Trigrams[i], r * .095f, Math.Max(2.1f, r * .0105f));
            g.RotateTransform(-90);
            string text = TrigramNames[i];
            SizeF sz = g.MeasureString(text, font);
            g.DrawString(text, font, brush, -sz.Width / 2f, r * .105f);
            g.Restore(state);
        }
    }

    private static void DrawTrigram(Graphics g, bool[] lines, float width, float height)
    {
        using var brush = new SolidBrush(Color.White);
        for (int i = 0; i < 3; i++)
        {
            float y = (i - 1) * height * 2.25f;
            if (lines[i])
            {
                g.FillRectangle(brush, -width / 2f, y - height / 2f, width, height);
            }
            else
            {
                float part = width * .39f;
                g.FillRectangle(brush, -width / 2f, y - height / 2f, part, height);
                g.FillRectangle(brush, width / 2f - part, y - height / 2f, part, height);
            }
        }
    }

    private static void DrawYinYang(Graphics g, float radius, Pen outline)
    {
        using var white = new SolidBrush(Color.White);
        using var black = new SolidBrush(Color.Black);
        float d = radius * 2;

        g.FillEllipse(white, -radius, -radius, d, d);
        g.FillPie(black, -radius, -radius, d, d, -90, 180);
        g.FillEllipse(black, -radius / 2, -radius, radius, radius);
        g.FillEllipse(white, 0, 0, radius, radius);
        float dot = radius * .28f;
        g.FillEllipse(white, -dot / 2, -radius * .67f - dot / 2, dot, dot);
        g.FillEllipse(black, -dot / 2, radius * .39f - dot / 2, dot, dot);
        g.DrawEllipse(outline, -radius, -radius, d, d);
    }

    private void AttachToDesktop()
    {
        try
        {
            IntPtr progman = Native.FindWindow("Progman", null);
            if (progman == IntPtr.Zero) throw new Exception("Progman not found");

            Native.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
            IntPtr worker = Native.FindWorkerW();
            if (worker == IntPtr.Zero) throw new Exception("WorkerW not found");

            IntPtr h = Handle;
            long style = Native.GetWindowLong(h, GWL_STYLE).ToInt64();
            Native.SetWindowLong(h, GWL_STYLE, new IntPtr(style | WS_CHILD));
            Native.SetParent(h, worker);

            Size size = SystemInformation.VirtualScreen.Size;
            Native.SetWindowPos(h, Bottom, 0, 0, size.Width, size.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            Native.ShowWindow(h, 5);

            if (!Native.RegisterHotKey(h, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.Q))
                throw new Exception("Hotkey registration failed");

            _timer.Start();
        }
        catch (Exception ex)
        {
            Program.Log(ex);
            Close();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = (IntPtr)HTTRANSPARENT;
            return;
        }
        if (m.Msg == WM_HOTKEY && m.WParam == (IntPtr)HOTKEY_ID)
        {
            Close();
            return;
        }
        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }
}

internal static class Native
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SetParent(IntPtr child, IntPtr parent);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    public static IntPtr GetWindowLong(IntPtr hWnd, int index) => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, index) : GetWindowLong32(hWnd, index);
    public static IntPtr SetWindowLong(IntPtr hWnd, int index, IntPtr value) => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, index, value) : SetWindowLong32(hWnd, index, value);

    public static IntPtr FindWorkerW()
    {
        IntPtr worker = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return worker;
    }
}