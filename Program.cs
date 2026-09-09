using System;
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
        _mutex = new Mutex(true, "BaguaLiveWallpaper.SingleInstance", out bool created);
        if (!created) return;

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);

        using var form = new WallpaperForm();
        Application.Run(form);
        GC.KeepAlive(_mutex);
    }

    private static void Log(Exception? ex)
    {
        try
        {
            if (ex is null) return;
            var dir = PathHelper.GetDataDirectory();
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n");
        }
        catch { }
    }
}

internal static class PathHelper
{
    public static string GetDataDirectory() =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BaguaLiveWallpaper");
}

internal sealed class WallpaperForm : Form
{
    private const int WM_NCHITTEST = 0x84;
    private const int HTTRANSPARENT = -1;
    private const int WM_ERASEBKGND = 0x14;
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0x4247;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const int WS_CHILD = 0x40000000;
    private const int GWL_STYLE = -16;
    private const int SWP_NOACTIVATE = 0x0010;
    private const int SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_BOTTOM = new(1);

    private readonly Timer _timer;
    private readonly Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private IntPtr _desktopHost;
    private bool _attached;
    private const double SecondsPerTurn = 24.0;

    public WallpaperForm()
    {
        Text = "Bagua Live Wallpaper";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(0, 0);
        ClientSize = Screen.PrimaryScreen?.Bounds.Size ?? new Size(1920, 1080);
        BackColor = Color.Black;
        DoubleBuffered = true;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        TopMost = false;

        _timer = new Timer { Interval = 33 };
        _timer.Tick += (_, _) => Invalidate();

        Shown += (_, _) => AttachToDesktop();
        FormClosed += (_, _) => Cleanup();
    }

    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Color.Black);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float scale = Math.Min(ClientSize.Width / 1920f, ClientSize.Height / 1080f);
        float cx = ClientSize.Width / 2f;
        float cy = ClientSize.Height / 2f;
        float radius = 321f * scale;
        double angle = (_clock.Elapsed.TotalSeconds % SecondsPerTurn) / SecondsPerTurn * Math.PI * 2.0;

        DrawBagua(g, cx, cy, radius, angle);
    }

    private static void DrawBagua(Graphics g, float cx, float cy, float r, double angle)
    {
        g.TranslateTransform(cx, cy);
        g.RotateTransform((float)(angle * 180.0 / Math.PI));

        using var penThin = new Pen(Color.White, Math.Max(1f, r / 310f));
        using var penMedium = new Pen(Color.White, Math.Max(1.2f, r / 245f));
        using var penStrong = new Pen(Color.White, Math.Max(1.5f, r / 170f));
        penThin.Alignment = PenAlignment.Center;
        penMedium.Alignment = PenAlignment.Center;
        penStrong.Alignment = PenAlignment.Center;

        float[] rings = { 0.14f, 0.245f, 0.335f, 0.435f, 0.55f, 0.72f, 0.865f, 0.965f, 1.0f };
        foreach (float q in rings)
            g.DrawEllipse(penThin, -r*q, -r*q, 2*r*q, 2*r*q);

        DrawRadialGrid(g, r, 64, 0.55f, 1.0f, penThin);
        DrawRadialGrid(g, r, 32, 0.335f, 0.55f, penThin);
        DrawRadialGrid(g, r, 16, 0.245f, 0.335f, penThin);
        DrawRadialGrid(g, r, 8, 0.14f, 0.245f, penThin);

        DrawRingText(g, r * 0.61f, 64, HexagramNames, Math.Max(7f, r * 0.036f));
        DrawRingText(g, r * 0.80f, 64, OuterCycle, Math.Max(5.5f, r * 0.024f));
        DrawRingText(g, r * 0.91f, 64, OuterCycle2, Math.Max(5f, r * 0.020f));
        DrawRingText(g, r * 0.40f, 32, InnerCycle, Math.Max(5.5f, r * 0.023f));
        DrawRingText(g, r * 0.285f, 16, Trigrams.SelectName, Math.Max(8f, r * 0.033f));

        DrawTrigrams(g, r * 0.665f);
        DrawYinYang(g, r * 0.155f);
        g.ResetTransform();
    }

    private static void DrawRadialGrid(Graphics g, float r, int count, float innerQ, float outerQ, Pen pen)
    {
        for (int i = 0; i < count; i++)
        {
            double a = i * Math.PI * 2 / count;
            float x1 = (float)(Math.Cos(a) * r * innerQ);
            float y1 = (float)(Math.Sin(a) * r * innerQ);
            float x2 = (float)(Math.Cos(a) * r * outerQ);
            float y2 = (float)(Math.Sin(a) * r * outerQ);
            g.DrawLine(pen, x1, y1, x2, y2);
        }
    }

    private static void DrawRingText(Graphics g, float radius, int count, string[] text, float fontSize)
    {
        using var font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        for (int i = 0; i < count; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI * 2 / count;
            string s = text[i % text.Length];
            g.Save();
            g.TranslateTransform((float)(Math.Cos(a) * radius), (float)(Math.Sin(a) * radius));
            g.RotateTransform((float)(a * 180 / Math.PI + 90));
            float box = fontSize * 1.9f;
            g.DrawString(s, font, brush, new RectangleF(-box/2, -box/2, box, box), fmt);
            g.Restore();
        }
    }

    private static void DrawTrigrams(Graphics g, float radius)
    {
        for (int i = 0; i < 8; i++)
        {
            double a = -Math.PI/2 + i*Math.PI/4;
            g.Save();
            g.TranslateTransform((float)(Math.Cos(a)*radius), (float)(Math.Sin(a)*radius));
            g.RotateTransform((float)(a*180/Math.PI + 90));
            DrawTrigram(g, Trigrams.Values[i], radius * 0.18f, radius * 0.028f);
            g.Restore();
        }
    }

    private static void DrawTrigram(Graphics g, bool[] lines, float width, float gap)
    {
        using var brush = new SolidBrush(Color.White);
        float lineH = Math.Max(2.2f, gap);
        for (int i = 0; i < 3; i++)
        {
            float y = (i - 1) * gap * 2.2f;
            if (lines[i])
                g.FillRectangle(brush, -width/2, y-lineH/2, width, lineH);
            else
            {
                float part = width * 0.43f;
                g.FillRectangle(brush, -width/2, y-lineH/2, part, lineH);
                g.FillRectangle(brush, width/2-part, y-lineH/2, part, lineH);
            }
        }
    }

    private static void DrawYinYang(Graphics g, float radius)
    {
        using var white = new SolidBrush(Color.White);
        using var black = new SolidBrush(Color.Black);
        using var outline = new Pen(Color.White, Math.Max(1.5f, radius * 0.025f));
        float d = radius * 2;
        g.FillEllipse(white, -radius, -radius, d, d);
        g.FillPie(black, -radius, -radius, d, d, -90, 180);
        g.FillEllipse(black, -radius/2, -radius, radius, radius);
        g.FillEllipse(white, 0, 0, radius, radius);
        g.FillEllipse(white, -radius*0.14f, -radius*0.64f, radius*0.28f, radius*0.28f);
        g.FillEllipse(black, -radius*0.14f, radius*0.36f, radius*0.28f, radius*0.28f);
        g.DrawEllipse(outline, -radius, -radius, d, d);
    }

    private void AttachToDesktop()
    {
        try
        {
            IntPtr progman = Native.FindWindow("Progman", null);
            if (progman == IntPtr.Zero) throw new InvalidOperationException("Progman not found.");
            Native.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
            IntPtr worker = Native.FindDesktopWorkerW();
            if (worker == IntPtr.Zero) worker = progman;
            _desktopHost = worker;

            IntPtr hwnd = Handle;
            long style = Native.GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            Native.SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style | WS_CHILD));
            Native.SetParent(hwnd, worker);
            Native.SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, ClientSize.Width, ClientSize.Height,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
            Native.ShowWindow(hwnd, 5);
            _attached = true;
            Native.RegisterHotKey(hwnd, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.Q);
            _timer.Start();
        }
        catch (Exception ex)
        {
            System.IO.Directory.CreateDirectory(PathHelper.GetDataDirectory());
            System.IO.File.AppendAllText(System.IO.Path.Combine(PathHelper.GetDataDirectory(), "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Desktop attach failed: {ex}\r\n");
            Close();
        }
    }

    private void Cleanup()
    {
        _timer.Stop();
        if (IsHandleCreated) Native.UnregisterHotKey(Handle, HOTKEY_ID);
        _attached = false;
        // The Windows wallpaper is never replaced; the original wallpaper remains intact.
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

    private static readonly string[] HexagramNames =
    {
        "乾","坤","屯","蒙","需","讼","师","比","小畜","履","泰","否","同人","大有","谦","豫",
        "随","蛊","临","观","噬嗑","贲","剥","复","无妄","大畜","颐","大过","坎","离","咸","恒",
        "遯","大壮","晋","明夷","家人","睽","蹇","解","损","益","夬","姤","萃","升","困","井",
        "革","鼎","震","艮","渐","归妹","丰","旅","巽","兑","涣","节","中孚","小过","既济","未济"
    };

    private static readonly string[] OuterCycle =
    {
        "甲乙丙丁戊己庚辛壬癸","子丑寅卯辰巳午未申酉戌亥","金木水火土","天干地支",
        "阴阳五行","春夏秋冬","东南西北","乾坤坎离震巽艮兑"
    };

    private static readonly string[] OuterCycle2 =
    {
        "天地玄黄宇宙洪荒","日月盈昃辰宿列张","寒来暑往秋收冬藏","闰余成岁律吕调阳",
        "云腾致雨露结为霜","金生丽水玉出昆冈","剑号巨阙珠称夜光","果珍李柰菜重芥姜"
    };

    private static readonly string[] InnerCycle =
    {
        "天","地","人","阴","阳","日","月","星","辰","乾","坤","坎","离","震","巽","艮",
        "兑","木","火","土","金","水","生","克","动","静","虚","实","刚","柔","中","正"
    };

    private static class Trigrams
    {
        public static readonly bool[][] Values =
        {
            new[]{true,true,true}, new[]{false,false,false}, new[]{false,true,false}, new[]{true,false,true},
            new[]{false,false,true}, new[]{true,false,false}, new[]{true,true,false}, new[]{false,true,true}
        };
        public static readonly string[] SelectName = { "乾","坤","坎","离","震","巽","艮","兑" };
    }
}

internal static class Native
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd,nIndex) : GetWindowLong32(hWnd,nIndex);
    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value) => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd,nIndex,value) : SetWindowLong32(hWnd,nIndex,value);

    public static IntPtr FindDesktopWorkerW()
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            IntPtr shellView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                result = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
