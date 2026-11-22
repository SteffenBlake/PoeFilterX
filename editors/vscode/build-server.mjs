import { execFileSync } from 'node:child_process';
import { copyFile, mkdir, rm } from 'node:fs/promises';
import * as path from 'node:path';

const runtimes = {
    'win32-x64': 'win-x64',
    'win32-arm64': 'win-arm64',
    'linux-x64': 'linux-x64',
    'linux-arm64': 'linux-arm64',
    'alpine-x64': 'linux-musl-x64',
    'alpine-arm64': 'linux-musl-arm64',
    'darwin-x64': 'osx-x64',
    'darwin-arm64': 'osx-arm64'
};
const target = process.argv[2] ?? `${process.platform}-${process.arch}`;
const runtime = runtimes[target];
if (!runtime) {
    throw new Error(`Unsupported VS Code target '${target}'. Supported targets: ${Object.keys(runtimes).join(', ')}`);
}

await rm('server', { recursive: true, force: true });
const project = path.resolve('../../src/PoeFilterX/PoeFilterX.csproj');
execFileSync('dotnet', [
    'publish', project,
    '--configuration', 'Release',
    '--runtime', runtime,
    '--self-contained', 'true',
    '--output', path.resolve('server'),
    '--nologo',
    '-p:PackAsTool=false',
    '-p:PublishTrimmed=false',
    '-p:PublishAot=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    ...(process.env.PFX_RELEASE_VERSION ? [
        `-p:Version=${process.env.PFX_RELEASE_VERSION}`,
        '-p:ContinuousIntegrationBuild=true'
    ] : [])
], { stdio: 'inherit' });

const resolved = JSON.parse(execFileSync('dotnet', [
    'msbuild', project,
    '-target:ResolveFrameworkReferences',
    '-getItem:ResolvedRuntimePack',
    `-property:RuntimeIdentifier=${runtime}`,
    '-property:SelfContained=true',
    '-property:PackAsTool=false',
    '-property:Configuration=Release',
    '-verbosity:quiet'
], { encoding: 'utf8' }));
const runtimePack = resolved.Items.ResolvedRuntimePack.find(pack => pack.FrameworkName === 'Microsoft.NETCore.App');
if (!runtimePack) {
    throw new Error('The .NET SDK did not resolve the runtime pack for the bundled server.');
}

const licenses = 'server/THIRD-PARTY-LICENSES/dotnet';
await mkdir(licenses, { recursive: true });
await Promise.all(['LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT'].map(file =>
    copyFile(path.join(runtimePack.PackageDirectory, file), path.join(licenses, file))));
