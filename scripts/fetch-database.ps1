# Downloads dictionary.db3 from the GitHub Release named in database-release.txt
# into JapaneseDictionary/Resources/Raw/ and verifies its SHA-256.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$lock = @{}
Get-Content (Join-Path $root 'database-release.txt') |
    Where-Object { $_ -match '^\s*\w+=' } |
    ForEach-Object { $k, $v = $_ -split '=', 2; $lock[$k.Trim()] = $v.Trim() }

$target = Join-Path $root 'JapaneseDictionary/Resources/Raw/dictionary.db3'
$url = "https://github.com/aitorsansal/shirabe/releases/download/$($lock.tag)/dictionary.db3"

if ((Test-Path $target) -and ((Get-FileHash $target -Algorithm SHA256).Hash -ieq $lock.sha256)) {
    Write-Host 'dictionary.db3 is already up to date.'
    return
}

Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing

$actual = (Get-FileHash $target -Algorithm SHA256).Hash
if ($actual -ine $lock.sha256) {
    Remove-Item $target
    throw "Checksum mismatch for dictionary.db3: expected $($lock.sha256), got $actual."
}
Write-Host 'dictionary.db3 downloaded and verified.'
