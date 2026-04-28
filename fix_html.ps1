$files = Get-ChildItem -Path "c:\Users\Cherylou\GadgetVault\Views" -Filter *.cshtml -Recurse
foreach ($f in $files) {
    $content = Get-Content $f.FullName -Raw
    $content = $content -replace '/ style="accent-color: var\(--brand-color\);"', 'style="accent-color: var(--brand-color);" />'
    $content = [regex]::Replace($content, '\bto-(?:emerald|green)-\d{3}\b', 'to-[color-mix(in_srgb,var(--brand-color)_80%,transparent)]')
    Set-Content -Path $f.FullName -Value $content
}
