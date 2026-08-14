# Cursor / PowerShell ターミナルで dotnet build の日本語メッセージが文字化けしないよう UTF-8 に揃える。
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
if ($env:OS -eq "Windows_NT") {
    chcp 65001 | Out-Null
}
