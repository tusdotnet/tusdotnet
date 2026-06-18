$net10sdk = "10.0"
$runtimesOnNet10 = "net10.0";

$repoRoot = (Get-Item $PSScriptRoot).FullName
$wslTestPath = "/mnt/" + ($repoRoot -replace '\\', '/' -replace '^([A-Za-z]):', { $_.Groups[1].Value.ToLower() }) + "/tusdotnet.test"

Function RunTests([string]$sdkVersion, [string[]]$runtimes)
{
    Write-Host "Going to run tests for ""$runtimes"" using SDK $sdkVersion"

    wsl -- bash -c "cd '$wslTestPath' && dotnet new globaljson --sdk-version $sdkVersion --force > /dev/null && $(($runtimes | ForEach-Object { "dotnet test -c Release -v m --nologo --no-build -f $_" }) -join ' && ') && rm -f global.json"
}

# Build once via WSL
wsl -- bash -c "cd '$wslTestPath' && dotnet build -c release -v q -nowarn:NETSDK1138 -nowarn:NU1902 -nowarn:NU1903 -nowarn:NETSDK1215 -nowarn:NU1904 -nowarn:ASPDEPR004"

RunTests $net10sdk $runtimesOnNet10;
