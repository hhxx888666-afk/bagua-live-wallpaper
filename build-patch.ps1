$ErrorActionPreference = 'Stop'
$p = '.\Program.cs'
$s = Get-Content $p -Raw
$nl = [Environment]::NewLine

# Preserve the existing source-compatible font fix.
$s = $s.Replace('FontStyle.Light', 'FontStyle.Regular')

# COLOR-ONLY TUNING FROM THE CONFIRMED MOTHER DESIGN.
# Brighter ancient ink-green lines: more visible on a black desktop, without changing line geometry/width.
$s = $s.Replace('Color.FromArgb(92, 125, 108)', 'Color.FromArgb(78, 150, 118)')
$s = $s.Replace('Color.FromArgb(125, 158, 138)', 'Color.FromArgb(112, 178, 142)')
# Brighter antique gold-bronze text/symbols: luminous and rich, without increasing font size/weight.
$s = $s.Replace('Color.FromArgb(198, 169, 92)', 'Color.FromArgb(238, 190, 72)')
$s = $s.Replace('Color.FromArgb(218, 187, 105)', 'Color.FromArgb(244, 198, 82)')
$s = $s.Replace('Color.FromArgb(116, 151, 132)', 'Color.FromArgb(92, 160, 126)')

# Keep the existing physical Ctrl+Alt+L polling behavior intact.
$s = $s.Replace(
    ('    private bool _lockMode;' + $nl),
    ('    private bool _lockMode;' + $nl + '    private bool _lockComboLatched;' + $nl)
)

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
Write-Host 'Build patch applied: brighter ancient ink-green lines and brighter antique gold-bronze text; geometry/layout/size/speed unchanged.'
