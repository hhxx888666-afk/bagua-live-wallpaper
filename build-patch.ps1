$ErrorActionPreference = 'Stop'
$p = '.\Program.cs'
$s = Get-Content $p -Raw

$s = $s.Replace('private static void Main()', 'private static void Main(string[] args)')
$oldMutex = 'using var mutex = new Mutex(true, "BaguaLiveWallpaper.SingleInstance", out bool first);'
$newMutex = 'bool screenSaver = Array.Exists(args, a => string.Equals(a, "/s", StringComparison.OrdinalIgnoreCase));' + [Environment]::NewLine + '        using var mutex = new Mutex(true, screenSaver ? "BaguaLiveWallpaper.ScreenSaver" : "BaguaLiveWallpaper.SingleInstance", out bool first);'
$s = $s.Replace($oldMutex, $newMutex)
$s = $s.Replace('try { Application.Run(new Wallpaper()); }', 'try { Application.Run(new Wallpaper(screenSaver)); }')

$oldCtor = '    public Wallpaper()' + [Environment]::NewLine + '    {'
$newCtor = '    private readonly bool _screenSaver;' + [Environment]::NewLine + '    private bool _lockMode;' + [Environment]::NewLine + [Environment]::NewLine + '    public Wallpaper(bool screenSaver)' + [Environment]::NewLine + '    {' + [Environment]::NewLine + '        _screenSaver = screenSaver;'
$s = $s.Replace($oldCtor, $newCtor)

$oldTop = '        TopMost = false;'
$newTop = @'
        TopMost = _screenSaver;
        KeyPreview = true;
        if (_screenSaver)
        {
            Bounds = SystemInformation.VirtualScreen;
            KeyDown += (_, _) => Close();
            MouseClick += (_, _) => Close();
        }
        else
        {
            KeyDown += Wallpaper_KeyDown;
        }
'@
$s = $s.Replace($oldTop, $newTop.TrimEnd())

$oldShown = '        Shown += (_, _) => AttachToDesktop();'
$newShown = '        Shown += (_, _) => { if (!_screenSaver) { AttachToDesktop(); Native.RegisterHotKey(Handle, LOCK_HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.L); } else { Activate(); Focus(); } };'
$s = $s.Replace($oldShown, $newShown)

$s = $s.Replace('private const int HOTKEY_ID = 0x4247;', 'private const int HOTKEY_ID = 0x4247;' + [Environment]::NewLine + '    private const int LOCK_HOTKEY_ID = 0x4248;')

$oldClosed = 'FormClosed += (_, _) => { _timer.Stop(); if (IsHandleCreated) Native.UnregisterHotKey(Handle, HOTKEY_ID); };'
$newClosed = 'FormClosed += (_, _) => { _timer.Stop(); if (IsHandleCreated) { Native.UnregisterHotKey(Handle, HOTKEY_ID); Native.UnregisterHotKey(Handle, LOCK_HOTKEY_ID); } };'
$s = $s.Replace($oldClosed, $newClosed)

$oldWnd = '        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) { Close(); return; }'
$newWnd = @'
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) { Close(); return; }
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == LOCK_HOTKEY_ID) { EnterLockMode(); return; }
'@
$s = $s.Replace($oldWnd, $newWnd.TrimEnd())

$s = $s.Replace('        g.ResetTransform();', '        g.ResetTransform();' + [Environment]::NewLine + '        if (_lockMode) DrawLockOverlay(g);')

$anchor = '    private void AttachToDesktop()'
$insert = @'
    private void Wallpaper_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_lockMode && e.KeyCode == Keys.Enter)
        {
            ExitLockMode();
            e.Handled = true;
        }
    }

    private void EnterLockMode()
    {
        if (_lockMode || _screenSaver) return;
        _lockMode = true;
        Native.SetParent(Handle, IntPtr.Zero);
        IntPtr style = Native.GetWindowLongPtr(Handle, GWL_STYLE);
        Native.SetWindowLongPtr(Handle, GWL_STYLE, (IntPtr)((style.ToInt64() & ~WS_CHILD) | unchecked((long)0x80000000)));
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        Activate();
        BringToFront();
        Focus();
        Invalidate();
    }

    private void ExitLockMode()
    {
        if (!_lockMode) return;
        _lockMode = false;
        TopMost = false;
        AttachToDesktop();
        Invalidate();
    }

    private void DrawLockOverlay(Graphics g)
    {
        float scale = Math.Min(ClientSize.Width / 1920f, ClientSize.Height / 1080f);
        string time = DateTime.Now.ToString("HH:mm");
        string date = DateTime.Now.ToString("yyyy年M月d日  dddd", new System.Globalization.CultureInfo("zh-CN"));

        using var white = new SolidBrush(Color.FromArgb(245, 245, 245));
        using var soft = new SolidBrush(Color.FromArgb(210, 210, 210));
        using var fontTime = new Font("Microsoft YaHei UI Light", Math.Max(64f, 92f * scale), FontStyle.Regular, GraphicsUnit.Pixel);
        using var fontDate = new Font("Microsoft YaHei UI", Math.Max(20f, 26f * scale), FontStyle.Regular, GraphicsUnit.Pixel);
        using var fontHint = new Font("Microsoft YaHei UI", Math.Max(14f, 18f * scale), FontStyle.Regular, GraphicsUnit.Pixel);

        float left = Math.Max(48f, ClientSize.Width * .045f);
        float bottom = Math.Max(42f, ClientSize.Height * .055f);
        float timeY = ClientSize.Height - bottom - fontTime.Height - fontDate.Height - 16f * scale;
        g.DrawString(time, fontTime, white, left, timeY);
        g.DrawString(date, fontDate, soft, left + 3f * scale, timeY + fontTime.Height + 5f * scale);

        string hint = "动态八卦锁屏  ·  按 Enter 解锁";
        SizeF hs = g.MeasureString(hint, fontHint);
        g.DrawString(hint, fontHint, soft, ClientSize.Width - hs.Width - left, ClientSize.Height - bottom);
    }

'@
$s = $s.Replace($anchor, $insert + $anchor)

Set-Content $p $s -Encoding UTF8
Write-Host 'Build patch applied successfully.'
