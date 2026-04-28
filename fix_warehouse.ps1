$content = Get-Content -Raw -Path 'Views/Dashboard/WarehouseManager.cshtml'
$content = $content -replace '<div class="flex flex-col gap-6 w-full h-full pb-8 transition-colors duration-300">', ''
$content = $content -replace '(</main>\s*</div>\s*(<!-- [^>]* -->\s*)*)?</div>\s*$', '' # remove the trailing closing div
$content = $content -replace 'p-5', 'p-6'
$content = $content -replace 'text-5xl', 'text-4xl'
$content = $content -replace 'text-2xl', 'text-2xl' # keep header
# Button borders
$content = $content -replace 'text-xs font-extrabold text-white bg-red-500', 'text-xs font-extrabold text-white bg-red-500 border-none'
$content = $content -replace 'text-xs font-extrabold text-yellow-800 bg-yellow-200', 'text-xs font-extrabold text-yellow-800 bg-yellow-200 border-none'
$content = $content -replace 'text-xs font-extrabold text-blue-700 bg-blue-100', 'text-xs font-extrabold text-blue-700 bg-blue-100 border-none'
Set-Content -Path 'Views/Dashboard/WarehouseManager.cshtml' -Value $content
