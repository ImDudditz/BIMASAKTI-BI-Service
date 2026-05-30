$text = [IO.File]::ReadAllText('manager.bat')
$text = $text.Replace("`r`n", "`n").Replace("`n", "`r`n")
$utf8NoBom = New-Object System.Text.UTF8Encoding($False)
[IO.File]::WriteAllText('manager.bat', $text, $utf8NoBom)
