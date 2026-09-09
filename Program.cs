using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main()
    {
        _mutex = new Mutex(true, "BaguaLiveWallpaper.SingleInstance", out bool firstInstance);
        if (!firstInstance) return;

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Logger.Write(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.Write(e.ExceptionObject as Exception);

        try
        {
            using var form = new WallpaperForm();
            Application.Run(form);
        }
        catch (Exception ex)
        {
            Logger.Write(ex);
        }
        finally { GC.KeepAlive(_mutex); }
    }
}

internal static class Logger
{
    public static void Write(Exception? ex)
    {
        if (ex == null) return;
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BaguaLiveWallpaper");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n");
        }
        catch { }
    }
}

internal sealed class WallpaperForm : Form
{
    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0x4247;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000L;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_BOTTOM = new(1);

    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private const double SecondsPerTurn = 36.0;

    public WallpaperForm()
    {
        Text = "Bagua Live Wallpaper";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = SystemInformation.VirtualScreen.Location;
        ClientSize = SystemInformation.VirtualScreen.Size;
        BackColor = Color.Black;
        DoubleBuffered = true;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        TopMost = false;

        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += (_, _) => Invalidate();
        Shown += (_, _) => AttachToDesktopLayer();
        FormClosed += (_, _) => Cleanup();
    }

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
        float radius = 325f * scale;
        double angle = (_clock.Elapsed.TotalSeconds / SecondsPerTurn) * 360.0;
        DrawBagua(g, cx, cy, radius, angle);
    }

    private static void DrawBagua(Graphics g, float cx, float cy, float r, double angle)
    {
        g.TranslateTransform(cx, cy);
        g.RotateTransform((float)angle);

        float w = Math.Max(1.25f, r / 230f);
        float w2 = Math.Max(1.8f, r / 165f);
        using var fine = new Pen(Color.White, w);
        using var strong = new Pen(Color.White, w2);
        fine.Alignment = PenAlignment.Center;
        strong.Alignment = PenAlignment.Center;

        float[] rings = { .13f, .23f, .33f, .43f, .54f, .68f, .81f, .91f, .985f, 1f };
        foreach (float q in rings)
            g.DrawEllipse(fine, -r * q, -r * q, r * q * 2, r * q * 2);

        DrawRadialLines(g, r, 8, .13f, .23f, fine);
        DrawRadialLines(g, r, 16, .23f, .33f, fine);
        DrawRadialLines(g, r, 32, .33f, .54f, fine);
        DrawRadialLines(g, r, 64, .54f, 1f, fine);

        DrawRingText(g, r * .395f, 32, InnerWords, Math.Max(7f, r * .026f));
        DrawRingText(g, r * .605f, 64, Hexagrams, Math.Max(6.5f, r * .026f));
        DrawRingText(g, r * .795f, 64, OuterWords, Math.Max(5.5f, r * .021f));
        DrawRingText(g, r * .90f, 64, OuterWords2, Math.Max(5f, r * .018f));
        DrawTrigrams(g, r * .68f);
        DrawYinYang(g, r * .145f, strong);
        g.ResetTransform();
    }

    private static void DrawRadialLines(Graphics g, float r, int count, float inner, float outer, Pen pen)
    {
        for (int i = 0; i < count; i++)
        {
            double a = i * Math.PI * 2 / count;
            g.DrawLine(pen,
                (float)(Math.Cos(a) * r * inner), (float)(Math.Sin(a) * r * inner),
                (float)(Math.Cos(a) * r * outer), (float)(Math.Sin(a) * r * outer));
        }
    }

    private static void DrawRingText(Graphics g, float radius, int count, string[] values, float fontSize)
    {
        using var font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        float box = Math.Max(fontSize * 2.2f, 8f);
        for (int i = 0; i < count; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI * 2 / count;
            g.Save();
            g.TranslateTransform((float)(Math.Cos(a) * radius), (float)(Math.Sin(a) * radius));
            g.RotateTransform((float)(a * 180 / Math.PI + 90));
            g.DrawString(values[i % values.Length], font, brush, new RectangleF(-box / 2, -box / 2, box, box), format);
            g.Restore();
        }
    }

    private static void DrawTrigrams(Graphics g, float radius)
    {
        for (int i = 0; i < 8; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / 4;
            g.Save();
            g.TranslateTransform((float)(Math.Cos(a) * radius), (float)(Math.Sin(a) * radius));
            g.RotateTransform((float)(a * 180 / Math.PI + 90));
            DrawTrigram(g, TrigramValues[i], radius * .17f, Math.Max(2.2f, radius * .026f));
            g.Restore();
        }
    }

    private static void DrawTrigram(Graphics g, bool[] lines, float width, float gap)
    {
        using var brush = new SolidBrush(Color.White);
        float height = Math.Max(2.5f, gap);
        for (int i = 0; i < 3; i++)
        {
            float y = (i - 1) * gap * 2.15f;
            if (lines[i]) g.FillRectangle(brush, -width / 2, y - height / 2, width, height);
            else
            {
                float part = width * .43f;
                g.FillRectangle(brush, -width / 2, y - height / 2, part, height);
                g.FillRectangle(brush, width / 2 - part, y - height / 2, part, height);
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
        g.FillEllipse(white, -radius * .14f, -radius * .64f, radius * .28f, radius * .28f);
        g.FillEllipse(black, -radius * .14f, radius * .36f, radius * .28f, radius * .28f);
        g.DrawEllipse(outline, -radius, -radius, d, d);
    }

    private void AttachToDesktopLayer()
    {
        try
        {
            IntPtr progman = Native.FindWindow("Progman", null);
            if (progman == IntPtr.Zero) throw new InvalidOperationException("Progman not found.");

            Native.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
            IntPtr worker = Native.FindWorkerWBehindDesktopIcons();
            if (worker == IntPtr.Zero) throw new InvalidOperationException("WorkerW not found.");

            IntPtr hwnd = Handle;
            long style = Native.GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            Native.SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style | WS_CHILD));
            Native.SetParent(hwnd, worker);

            Size size = SystemInformation.VirtualScreen.Size;
            Native.SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, size.Width, size.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            Native.ShowWindow(hwnd, 5);

            if (!Native.RegisterHotKey(hwnd, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.Q))
                throw new InvalidOperationException("Exit hotkey registration failed.");

            _timer.Start();
        }
        catch (Exception ex)
        {
            Logger.Write(ex);
            Close();
        }
    }

    private void Cleanup()
    {
        _timer.Stop();
        if (IsHandleCreated) Native.UnregisterHotKey(Handle, HOTKEY_ID);
        // The Windows wallpaper itself is never changed, so exiting automatically reveals it again.
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

    private static readonly bool[][] TrigramValues =
    {
        new[]{ true, true, true }, new[]{ false, false, false }, new[]{ false, true, false }, new[]{ true, false, true },
        new[]{ false, false, true }, new[]{ true, false, false }, new[]{ true, true, false }, new[]{ false, true, true }
    };

    private static readonly string[] Hexagrams =
    {
        "乾","坤","屯","蒙","需","讼","师","比","小畜","履","泰","否","同人","大有","谦","豫",
        "随","蛊","临","观","噬嗑","贲","剥","复","无妄","大畜","颐","大过","坎","离","咸","恒",
        "遯","大壮","晋","明夷","家人","睽","蹇","解","损","益","夬","姤","萃","升","困","井",
        "革","鼎","震","艮","渐","归妹","丰","旅","巽","兑","涣","节","中孚","小过","既济","未济"
    };

    private static readonly string[] InnerWords =
    {
        "天","地","人","阴","阳","日","月","星","辰","乾","坤","坎","离","震","巽","艮",
        "兑","木","火","土","金","水","生","克","动","静","虚","实","刚","柔","中","正"
    };

    private static readonly string[] OuterWords =
    {
        "甲乙丙丁戊己庚辛壬癸","子丑寅卯辰巳午未申酉戌亥","金木水火土","天干地支",
        "阴阳五行","春夏秋冬","东南西北","乾坤坎离震巽艮兑"
    };

    private static readonly string[] OuterWords2 =
    {
        "天地玄黄宇宙洪荒","日月盈昃辰宿列张","寒来暑往秋收冬藏","闰余成岁律吕调阳",
        "云腾致雨露结为霜","金生丽水玉出昆冈","剑号巨阙珠称夜光","果珍李柰菜重芥姜"
    };
}

internal static class Native
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int index) => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, index) : GetWindowLong32(hWnd, index);
    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value) => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, index, value) : SetWindowLong32(hWnd, index, value);

    public static IntPtr FindWorkerWBehindDesktopIcons()
    {
        IntPtr worker = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            IntPtr shellView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return worker;
    }
}
