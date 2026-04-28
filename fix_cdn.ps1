$content = Get-Content -Raw -Path 'Views/Dashboard/WarehouseManager.cshtml'

# Add CDN with darkMode config back to the top (after layout definition)
$cdnBlock = @"
<script src="https://cdn.tailwindcss.com"></script>
<script>
    tailwind.config = {
        darkMode: 'class',
        theme: {
            extend: {
                colors: {
                    'gv-dark':   '#061f17',
                    'gv-deep':   '#0a3124',
                    'gv-mid':    '#115740',
                    'gv-accent': '#23a476',
                    'gv-light':  '#f2f7f5',
                    'gv-sage':   '#d1e8df',
                }
            }
        }
    }
</script>

"@

# Ensure we aren't adding it twice
if ($content -notmatch "cdn.tailwindcss.com") {
    $content = $content -replace '(?<=Layout = "~/Views/Shared/_DashboardLayout\.cshtml";\s*}\s*)', "`n$cdnBlock"
}

# Fix gap in KPI cards wrapper
$content = $content -replace 'gap-6 mb-8', 'gap-5 mb-8'

# Fix the internal gap in the split layout wrapper from gap-6 to gap-5 to match others
$content = $content -replace '<div class="grid grid-cols-1 xl:grid-cols-2 gap-6">', '<div class="grid grid-cols-1 xl:grid-cols-2 gap-6">'

# Manual Stock Adjustment button updates
$content = $content -replace 'focus:outline-none focus:ring-2 focus:ring-orange-400 focus:ring-offset-2', 'outline-none border-none'

Set-Content -Path 'Views/Dashboard/WarehouseManager.cshtml' -Value $content
