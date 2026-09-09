$ErrorActionPreference = 'Stop'
$p = '.\Program.cs'
$s = Get-Content $p -Raw
$nl = [Environment]::NewLine

# Keep the existing source-compatible font fix.
$s = $s.Replace('FontStyle.Light', 'FontStyle.Regular')

# Add a latch so the physical-key polling triggers only once per key press.
$s = $s.Replace(
    ('    private bool _lockMode;' + $nl),
    ('    private bool _lockMode;' + $nl + '    private bool _lockComboLatched;' + $nl)
)

# Poll the physical keyboard state from the existing 60 FPS UI timer. This does not depend on
# whether the wallpaper window is a child of WorkerW or whether RegisterHotKey is available.
$s = $s.Replace(
    ('        _timer.Tick += (_, _) => Invalidate();' + $nl),
    ('        _timer.Tick += (_, _) =>' + $nl + '        {' + $nl + '            CheckLockHotkey();' + $nl + '            Invalidate();' + $nl + '        };' + $nl)
)

$method = @'
    private void CheckLockHotkey()
    {
        if (_screenSaver) return;

        bool ctrl = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool alt = (Native.GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool l = (Native.GetAsyncKeyState(0x4C) & 0x8000) != 0;
        bool combo = ctrl && alt && l;

        if (combo && !_lockComboLatched)
        {
            _lockComboLatched = true;
            if (!_lockMode) BeginInvoke(new Action(EnterLockMode));
        }
        else if (!combo)
        {
            _lockComboLatched = false;
        }
    }

'@

$s = $s.Replace(
    ('    private void OnKeyDown(object? sender, KeyEventArgs e)' + $nl),
    ($method + '    private void OnKeyDown(object? sender, KeyEventArgs e)' + $nl)
)

$s = $s.Replace(
    ('        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);' + $nl),
    ('        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);' + $nl + '        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);' + $nl)
)

Set-Content $p $s -Encoding UTF8
Write-Host 'Build patch applied: Ctrl+Alt+L now uses physical keyboard polling in addition to RegisterHotKey.'
