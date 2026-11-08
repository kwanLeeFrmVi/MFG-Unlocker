param([switch]$Test, [string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') { throw 'Version must be MAJOR.MINOR.PATCH.' }
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $taskCompiler)) { throw '.NET Framework 4.x compiler not found. Enable .NET Framework 4.8 on Windows 10/11.' }
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$taskReferences = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Net.Http.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$taskBuild = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force -Path $taskBuild | Out-Null
$taskProgram = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\Program.cs') -Raw
$taskProgram = $taskProgram.Replace('AssemblyVersion("1.0.0.0")', 'AssemblyVersion("' + $Version + '.0")')
$taskStampedProgram = Join-Path $taskBuild 'Program.version.cs'
[System.IO.File]::WriteAllText($taskStampedProgram, $taskProgram, (New-Object System.Text.UTF8Encoding($false)))
$taskSources = @($taskSources | Where-Object { [System.IO.Path]::GetFileName($_) -ne 'Program.cs' }) + @($taskStampedProgram)
& $taskCompiler /nologo /warn:4 /warnaserror /optimize+ /platform:x64 /target:winexe "/win32icon:$(Join-Path $PSScriptRoot 'app.ico')" "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')" "/out:$(Join-Path $taskBuild 'MFGLauncher.exe')" @taskReferences @taskSources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
if ($Test) {
    $taskTests = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
    & $taskCompiler /nologo /warn:4 /warnaserror /optimize+ /platform:x64 /target:exe /main:MfgLauncher.Tests "/out:$(Join-Path $taskBuild 'MFGLauncher.Tests.exe')" @taskReferences @taskSources @taskTests
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $taskCompiler /nologo /warn:4 /warnaserror /optimize+ /platform:x64 /target:winexe /main:MfgLauncher.UpdateProbe "/out:$(Join-Path $taskBuild 'MFGLauncher.UpdateProbe.exe')" @taskReferences @taskSources (Join-Path $PSScriptRoot 'tests\UpdateProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Updater probe compilation failed.' }
    & (Join-Path $taskBuild 'MFGLauncher.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Write-Host "Built $(Join-Path $taskBuild 'MFGLauncher.exe')"


