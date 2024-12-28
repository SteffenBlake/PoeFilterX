param ([switch]$f=$false, [switch]$force=$false, [string]$v, [string]$version)

$FORCE = ($f -or $force)

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
    New-Item $Releases -ItemType Directory
}

$runtimes = "win-x64"

foreach ($runtime in $runtimes) 
{
    $RuntimePath = Join-Path -Path $Releases -ChildPath $runtime
    $VersionPath = Join-Path -Path $RuntimePath -ChildPath $VERSION
    $fullVersion = "$VERSION-$runtime"
    $ArchivePath = "$Releases/PoeFilterX-$fullVersion.zip"

    if ((Test-Path $VersionPath) -and $FORCE) {
        Remove-Item -Recurse -Force $VersionPath
    }
    if ((Test-Path $ArchivePath) -and $FORCE) {
        Remove-Item -Recurse -Force $ArchivePath
    }
    if (-Not (Test-Path $VersionPath)) 
    {
        New-Item $VersionPath -ItemType Directory
    }

    if (-Not (Test-Path $ArchivePath)) 
    {
        dotnet publish "./PoeFilterX/PoeFilterX.csproj" -c Release -r $runtime -o $VersionPath /property:Version=$fullVersion /p:DebugType=None /p:DebugSymbols=false --self-contained true -p:PublishSingleFile=true

        Compress-Archive -Path "$VersionPath/*" -DestinationPath $ArchivePath -CompressionLevel "Optimal"
    }
}

$msInstallerFrom = Join-Path -Path $Releases -ChildPath "PoeFilterX.WindowsInstaller.exe"
$msInstallerTo = "PoeFilterX.WindowsInstaller-$VERSION.exe"
$msInstallerToPath = Join-Path -Path $Releases -ChildPath "PoeFilterX.WindowsInstaller-$VERSION.exe"

if (Test-Path $msInstallerToPath) {
    Remove-Item -Force $msInstallerToPath
}

if (-Not (Test-Path $msInstallerTo)) {
    dotnet publish "./PoeFilterX.WindowsInstaller/PoeFilterX.WindowsInstaller.csproj" -c Release -r "win-x64" -o $Releases /property:Version=$fullVersion /p:DebugType=None /p:DebugSymbols=false --self-contained true -p:PublishSingleFile=true
    Rename-Item -Path $msInstallerFrom -NewName $msInstallerTo
}

