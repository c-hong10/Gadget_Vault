$files = Get-ChildItem -Path "c:\Users\Cherylou\GadgetVault\Views" -Filter *.cshtml -Recurse

foreach ($f in $files) {
    $content = Get-Content $f.FullName -Raw

    # Simple 500/600 shades (the main brand color)
    $content = [regex]::Replace($content, '\bbg-(?:emerald|green)-(?:500|600)\b', 'bg-gv-brand')
    $content = [regex]::Replace($content, '\btext-(?:emerald|green)-(?:500|600)\b', 'text-gv-brand')
    $content = [regex]::Replace($content, '\bborder-(?:emerald|green)-(?:500|600)\b', 'border-gv-brand')
    
    # Focus rings/borders
    $content = [regex]::Replace($content, '\bfocus:border-(?:emerald|green)-(?:500|600)\b', 'focus:border-gv-accent')
    $content = [regex]::Replace($content, '\bfocus:ring-(?:emerald|green)-(?:500|600)(/\d+)?\b', 'focus:ring-gv-accent')
    
    # Light backgrounds (50, 100)
    $content = [regex]::Replace($content, '\bbg-(?:emerald|green)-(?:50|100)(/1[05]|/20|/30)?\b', 'bg-gv-brand-light')
    $content = [regex]::Replace($content, '\bdark:bg-(?:emerald|green)-(?:50|100|500)(/1[05]|/20|/30)?\b', 'dark:bg-gv-brand-light')
    
    # Light borders (100, 200, 300)
    $content = [regex]::Replace($content, '\bborder-(?:emerald|green)-(?:100|200|300)(/20|/30)?\b', 'border-gv-brand-light')
    $content = [regex]::Replace($content, '\bdark:border-(?:emerald|green)-(?:100|200|300|500)(/20|/30|/10)?\b', 'dark:border-gv-brand-light')
    
    # Dark text (700, 800)
    $content = [regex]::Replace($content, '\btext-(?:emerald|green)-(?:700|800)\b', 'text-gv-brand-dark')
    $content = [regex]::Replace($content, '\bdark:text-(?:emerald|green)-(?:400|300)\b', 'dark:text-gv-brand-light')
    
    # Hover states
    $content = [regex]::Replace($content, '\bhover:bg-(?:emerald|green)-(?:500|600)\b', 'hover:bg-gv-brand')
    $content = [regex]::Replace($content, '\bhover:text-(?:emerald|green)-(?:500|600|400)\b', 'hover:text-gv-brand')
    $content = [regex]::Replace($content, '\bdark:hover:text-(?:emerald|green)-(?:500|600|400)\b', 'dark:hover:text-gv-brand')
    
    # Hover borders
    $content = [regex]::Replace($content, '\bhover:border-(?:emerald|green)-(?:200|300|500)(/\d+)?\b', 'hover:border-gv-brand-light')
    $content = [regex]::Replace($content, '\bdark:hover:border-(?:emerald|green)-(?:200|300|500)(/\d+)?\b', 'dark:hover:border-gv-brand-light')
    
    Set-Content -Path $f.FullName -Value $content
}
