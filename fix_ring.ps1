$files = Get-ChildItem -Path "c:\Users\Cherylou\GadgetVault\Views" -Filter *.cshtml -Recurse
foreach ($f in $files) {
    $content = Get-Content $f.FullName -Raw
    $content = [regex]::Replace($content, '\bfocus:ring-gv-accent\b', 'focus:ring-[var(--brand-color)]')
    Set-Content -Path $f.FullName -Value $content
}
