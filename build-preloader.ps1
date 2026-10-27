param([Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference='Stop'
$gamePath=(Resolve-Path -LiteralPath $GameDirectory).Path
$managed=Join-Path $gamePath 'Sea Power_Data\Managed'
$core=Join-Path $gamePath 'BepInEx\core'
foreach($required in @((Join-Path $managed 'Seapower-Scripts.dll'),(Join-Path $core 'BepInEx.dll'),(Join-Path $core 'Mono.Cecil.dll'))){
 if(-not(Test-Path -LiteralPath $required)){throw 'The selected game folder does not contain the required game and BepInEx references.'}
}
$sdkLine=@(& dotnet --list-sdks | Where-Object {$_ -match '^8\.'})[-1]
if(-not $sdkLine -or $sdkLine -notmatch '^(\S+)\s+\[(.+)\]$'){throw 'Install the .NET 8 SDK to build this source.'}
$compiler=Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$output=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments=@('/nostdlib+','/target:library','/optimize+','/langversion:latest',('/out:"'+(Join-Path $output 'AnchorChain.Preloader.dll')+'"'))
foreach($file in Get-ChildItem -LiteralPath $managed -Filter '*.dll' -File | Sort-Object Name){$arguments+='/reference:"'+$file.FullName+'"'}
foreach($name in @('BepInEx.dll','Mono.Cecil.dll')){$arguments+='/reference:"'+(Join-Path $core $name)+'"'}
$arguments+='"'+(Join-Path $PSScriptRoot 'AnchorChain.Preloader\Preloader.cs')+'"'
$response=Join-Path $output 'preloader-build.rsp'
$arguments | Set-Content -LiteralPath $response
& dotnet $compiler ('@'+$response)
if($LASTEXITCODE -ne 0){throw 'Preloader compilation failed.'}
'Built bin/AnchorChain.Preloader.dll. The game installation was not changed.'
