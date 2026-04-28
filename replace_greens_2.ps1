$files = Get-ChildItem -Path "c:\Users\Cherylou\GadgetVault\Views" -Filter *.cshtml -Recurse
foreach ($f in $files) {
    $content = Get-Content $f.FullName -Raw
    $content = [regex]::Replace($content, '\bshadow-(?:emerald|green)-\d{3}(?:/\d+)?\b', '')
    $content = [regex]::Replace($content, '\bhover:shadow-(?:emerald|green)-\d{3}(?:/\d+)?\b', '')
    $content = [regex]::Replace($content, '\bdark:bg-(?:emerald|green)-900/40\b', 'dark:bg-gv-brand-light')
    Set-Content -Path $f.FullName -Value $content
}
