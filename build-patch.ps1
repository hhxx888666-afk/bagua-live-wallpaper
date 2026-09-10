$ErrorActionPreference = 'Stop'
$p = '.\Program.cs'
$s = Get-Content $p -Raw
$nl = [Environment]::NewLine

# Preserve the existing source-compatible font fix.
$s = $s.Replace('FontStyle.Light', 'FontStyle.Regular')

# LINE TUNING: one consistent fine line treatment for every ring and radial divider.
$s = $s.Replace('Color.FromArgb(92, 125, 108)', 'Color.FromArgb(78, 150, 118)')
$s = $s.Replace('Color.FromArgb(125, 158, 138)', 'Color.FromArgb(78, 150, 118)')

# GOLD TEXT TUNING: brighter antique gold-copper, without increasing font size or weight.
$s = $s.Replace('Color.FromArgb(198, 169, 92)', 'Color.FromArgb(246, 204, 92)')
$s = $s.Replace('Color.FromArgb(218, 187, 105)', 'Color.FromArgb(248, 210, 102)')
$s = $s.Replace('Color.FromArgb(116, 151, 132)', 'Color.FromArgb(92, 160, 126)')

# Make every circular ring use the same fine pen as the outermost ring.
$s = $s.Replace('            bool mediumCircle = i == 0 || i == 3 || i == 6 || i == 10 || i == 13;' + $nl + '            g.DrawEllipse(mediumCircle ? medium : fine, -r, -r, 2 * r, 2 * r);', '            g.DrawEllipse(fine, -r, -r, 2 * r, 2 * r);')

# Make every radial divider use the same fine pen as the outermost ring.
$s = $s.Replace('            Pen p = n <= 16 ? medium : fine;', '            Pen p = fine;')

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
Write-Host 'Build patch applied: uniform fine ink-green lines and brighter antique gold-copper text; geometry/layout/size/speed unchanged.'
