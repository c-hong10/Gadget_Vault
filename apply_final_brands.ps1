$files = Get-ChildItem -Path "c:\Users\Cherylou\GadgetVault\Views" -Filter *.cshtml -Recurse

foreach ($f in $files) {
    $content = Get-Content $f.FullName -Raw

    # Remove the hardcoded tailwind configs from the views to prevent overriding _DashboardLayout
    $content = [regex]::Replace($content, '(?s)<script>\s*tailwind\.config = \{.*?</script>', '')

    # Replace gv-accent with gv-brand
    $content = [regex]::Replace($content, '\bbg-gv-accent\b', 'bg-gv-brand')
    $content = [regex]::Replace($content, '\btext-gv-accent\b', 'text-gv-brand')
    $content = [regex]::Replace($content, '\bborder-gv-accent\b', 'border-gv-brand')
    $content = [regex]::Replace($content, '\bfrom-gv-accent\b', 'from-[var(--brand-color)]')
    
    # Checkboxes: They use text-emerald-600 or bg-emerald... wait, checkboxes are native HTML.
    # Tailwind forms use text-emerald-600 for checkbox color. We can add style="color: var(--brand-color);"
    # Let's target input type="checkbox"
    $content = [regex]::Replace($content, '<input type="checkbox"[^>]*class="[^"]*"[^>]*>', {
        param($match)
        $m = $match.Value
        if ($m -notmatch 'style=') {
            return $m -replace '>$', ' style="accent-color: var(--brand-color);">'
        }
        return $m
    })

    # Badge Logic: The user specifically wants inline styles for badges (e.g. Active or role labels)
    # The badges currently use bg-gv-brand-light. Let's add the requested inline style to anything with bg-gv-brand-light.
    # Actually, it's easier to add the inline style to spans that have rounded-full and bg-gv-brand-light.
    $content = [regex]::Replace($content, '(<span[^>]*class="[^"]*bg-gv-brand-light[^"]*"[^>]*)>', {
        param($match)
        $m = $match.Groups[1].Value
        if ($m -notmatch 'style=') {
            return $m + ' style="background-color: color-mix(in srgb, var(--brand-color), transparent 80%); color: var(--brand-color); border-color: color-mix(in srgb, var(--brand-color), transparent 70%);">'
        }
        return $match.Value
    })
    
    # Also for any remaining bg-emerald-100 or text-emerald-700 not caught before
    $content = [regex]::Replace($content, '(<span[^>]*class="[^"]*(?:bg-emerald-100|text-emerald-700)[^"]*"[^>]*)>', {
        param($match)
        $m = $match.Groups[1].Value
        if ($m -notmatch 'style=') {
            return $m + ' style="background-color: color-mix(in srgb, var(--brand-color), transparent 80%); color: var(--brand-color); border-color: color-mix(in srgb, var(--brand-color), transparent 70%);">'
        }
        return $match.Value
    })

    Set-Content -Path $f.FullName -Value $content
}
