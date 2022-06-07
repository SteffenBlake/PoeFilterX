# This is a quick script that replicates the functionality of
# The installer but locally using the zip built via Release.ps1
# NOTE: This script needs to be run as admin to install to Program Files
# Step 1: Run Release.ps1, specifying a version if needed
# Step 2: Run this script, specifying the same version as well
# This will install to C:\Program Files\PoeFilterX and assumings you are in win-x64

param ([string]$v, [string]$version)

$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-Not ($currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))) 
{
    Write-Output "This script requires elevated privileges to install to Program Files. Please run as Admin."
    return
}

$VERSION = "";

if ($version.Length -gt 0) {
    $VERSION = $version;
} elseif ($v.Length -gt 0) {
    $VERSION = $v;
} else {
    $VERSION = git describe --tags --abbrev=0
}

$Releases = "./Releases"

if (-Not (Test-Path $Releases)) 
{
    Write-Output "Please run Release.ps1 first."
    return
}

$runtime = "win-x64"
$fullVersion = "$VERSION-$runtime"
$ArchivePath = "$Releases/PoeFilterX-$fullVersion.zip"

$InstallDir = "C:\Program Files\PoeFilterX"

Expand-Archive -Path $ArchivePath -DestinationPath $InstallDir -Force