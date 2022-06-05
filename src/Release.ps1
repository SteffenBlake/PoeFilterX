param ([switch]$f=$false, [switch]$force=$false)

$FORCE = ($f -or $force)

$VERSION = git describe --tags --abbrev=0

$Releases = "./Releases"

if (-Not (Test-Path $Releases)) 
{
    New-Item $Releases -ItemType Directory
}

$runtimes = "win-x64", "linux-x64", "osx-x64", "linux-arm64", "win-arm64"

foreach ($runtime in $runtimes) 
{
    $RuntimePath = Join-Path -Path $Releases -ChildPath $runtime
    $VersionPath = Join-Path -Path $RuntimePath -ChildPath $VERSION
    $fullVersion = "$VERSION-$runtime"
    $ArchivePath = "$Releases/$fullVersion.zip"

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

        dotnet publish "./PoeFilterX.Update/PoeFilterX.Update.csproj" -c Release -r $runtime -o $VersionPath /property:Version=$fullVersion /p:DebugType=None /p:DebugSymbols=false --self-contained true -p:PublishSingleFile=true

        Compress-Archive -Path "$VersionPath/*" -DestinationPath $ArchivePath -CompressionLevel "Optimal"
    }
}

