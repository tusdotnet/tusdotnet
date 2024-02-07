
$net6sdk = "6.0.404";
$net8sdk = "8.0.101"
$runtimesOnNet6 = "net452", "netcoreapp2.1", "netcoreapp2.2", "netcoreapp3.0", "netcoreapp3.1", "net6.0";
$runtimesOnNet8 = "net7.0", "net8.0";

Push-Location tusdotnet.test

Function RunTests([string]$sdkVersion, [string[]]$runtimes)
{
    Write-Host "Going to run tests for ""$runtimes"" using SDK $sdkVersion"

    dotnet new globaljson --sdk-version $sdkVersion --force | Out-Null

    Foreach ($item in $runtimes) {
        dotnet test -c Release -v q --nologo --no-build -f $item
    }

   Remove-Item -Force .\global.json | Out-Null
}

# Build all once as this will still build for all runtimes. Ignore warning that some runtimes are EOL.
dotnet build -c release -v q -nowarn:NETSDK1138

# We need to run this on .NET 6.x as the .NET7 SDK does not work with running older netcoreapp (<3.0): Unhandled Exception: System.IO.FileLoadException: Could not load file or assembly 'System.Runtime, Version=4.2.2.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'. The located assembly's manifest definition does not match the assembly reference. (Exception from HRESULT: 0x80131040)
RunTests $net6sdk $runtimesOnNet6;

# Run the rest on .NET7
RunTests $net8sdk $runtimesOnNet8;

Pop-Location