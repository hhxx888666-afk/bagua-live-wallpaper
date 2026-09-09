$ErrorActionPreference = 'Stop'
$p = '.\Program.cs'
$s = Get-Content $p -Raw
$s = $s.Replace('FontStyle.Light', 'FontStyle.Regular')
Set-Content $p $s -Encoding UTF8
Write-Host 'Build patch applied successfully.'
