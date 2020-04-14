<#############################################################################
 # VARIABLES
 #############################################################################>
$release = "Debug"
$src = "$env:GIT_PRJ_SOURCE\log-reader\src\plugins\Probel.LogReader.Plugins.{0}\bin\$release\*.*"
$dst = "$env:APPDATA\probel\log-reader\plugins\{0}"
$plugins = "csv", "oracle", "debug", "text", "mssql", "iis"

<#############################################################################
 # FUNCTIONS 
 #############################################################################>
function Write-Debug($msg) {
    Write-Host $msg -ForegroundColor Green
}
function Write-Info($msg) {
    Write-Host $msg -ForegroundColor Yellow
}

<#############################################################################
 # MAIN 
 #############################################################################>

foreach ($p in $plugins) {
    Write-Info "Copying files for plugin '$p'..."
    $s = $src -f $p
    $d = $dst -f $p
    Write-Debug "Source for $plugin      : $s"
    Write-Debug "Destination for $plugin : $d"
    Write-Host "-------------------------------------"

    $file_exists = Test-Path $d
    if ($file_exists -eq $false) {
        Write-Info "Creating directory '$d'"
        mkdir $d
    }

    Copy-Item $s $d -Recurse
}

