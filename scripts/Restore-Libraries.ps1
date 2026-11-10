$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'dependencies.json') -Raw | ConvertFrom-Json
$dependency = $manifest.CUCoreLib
$target = Join-Path $projectRoot ('lib/CUCoreLib/' + $dependency.version)
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($extension in @('dll', 'xml')) {
    $file = Join-Path $target ('CUCoreLib.' + $extension)
    $expected = if ($extension -eq 'dll') { $dependency.dllSha256 } else { $dependency.xmlSha256 }
    if ((Test-Path -LiteralPath $file) -and (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -eq $expected) { continue }
    $temporary = $file + '.download'
    $url = 'https://github.com/jimmyking9999999/CUCoreLib/releases/download/v' + $dependency.version + '/CUCoreLib.' + $extension
    Invoke-WebRequest -Uri $url -OutFile $temporary
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expected) {
        throw ('Checksum mismatch: ' + $temporary + '. Release may have been replaced upstream; review before updating the pin.')
    }
    Move-Item -LiteralPath $temporary -Destination $file -Force
}
Write-Output 'CUCoreLib DLL and XML documentation verified.'
