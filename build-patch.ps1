$ErrorActionPreference = 'Stop'
$p = '.\Program.cs'
$s = Get-Content $p -Raw

# Keep the existing source-compatible font fix.
$s = $s.Replace('FontStyle.Light', 'FontStyle.Regular')

# Add a latch so the physical-key polling triggers only once per key press.
$s = $s.Replace(
'    private bool _lockMode;\n',
'    private bool _lockMode;\n    private bool _lockComboLatched;\n')

# Poll the physical keyboard state from the existing 60 FPS UI timer. This does not depend on\n# whether the wallpaper window is a child of WorkerW or whether RegisterHotKey is available.\n$s = $s.Replace(
'        _timer.Tick += (_, _) => Invalidate();\n',
'        _timer.Tick += (_, _) =>\n        {\n            CheckLockHotkey();\n            Invalidate();\n        };\n')

$method = @'\n    private void CheckLockHotkey()\n    {\n        if (_screenSaver) return;\n\n        bool ctrl = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0;\n        bool alt = (Native.GetAsyncKeyState(0x12) & 0x8000) != 0;\n        bool l = (Native.GetAsyncKeyState(0x4C) & 0x8000) != 0;\n        bool combo = ctrl && alt && l;\n\n        if (combo && !_lockComboLatched)\n        {\n            _lockComboLatched = true;\n            if (!_lockMode) BeginInvoke(new Action(EnterLockMode));\n        }\n        else if (!combo)\n        {\n            _lockComboLatched = false;\n        }\n    }\n\n'@

$s = $s.Replace(
'    private void OnKeyDown(object? sender, KeyEventArgs e)\n',
$method + '    private void OnKeyDown(object? sender, KeyEventArgs e)\n')

$s = $s.Replace(
'        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);\n',
'        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);\n        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);\n')

Set-Content $p $s -Encoding UTF8
Write-Host 'Build patch applied: Ctrl+Alt+L now uses physical keyboard polling in addition to RegisterHotKey.'
