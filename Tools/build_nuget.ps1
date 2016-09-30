# Location of nuget.exe which must be present
$nugetLocation = ".\nuget.exe"

function setVersionInfo() {
	$assemblyVersion = git describe --abbrev=0 --tags
	$assemblyInformationalVersion = git describe --tags --long
	(Get-Content nugetbuild\Properties\AssemblyInfo.cs) -replace `
		'(\[assembly: AssemblyInformationalVersion\(")([\w\W]*)("\)])', `
			"[assembly: AssemblyInformationalVersion(""$assemblyVersion, $assemblyInformationalVersion"")]" `
		| Out-File nugetbuild\Properties\AssemblyInfo.cs
	(Get-Content nugetbuild\Properties\AssemblyInfo.cs) -replace `
		'(\[assembly: AssemblyVersion\(")([\w\W]*)("\)])', "[assembly: AssemblyVersion(""$assemblyVersion"")]" `
		| Out-File nugetbuild\Properties\AssemblyInfo.cs
	(Get-Content nugetbuild\Properties\AssemblyInfo.cs) -replace `
		'(\[assembly: AssemblyFileVersion\(")([\w\W]*)("\)])', "[assembly: AssemblyFileVersion(""$assemblyVersion"")]" `
		| Out-File nugetbuild\Properties\AssemblyInfo.cs
}

function makeBuildFolder() {
	if(Test-Path -Path ".\nugetbuild\" ){
		Remove-Item -Recurse -Force .\nugetbuild\
	}
	
	New-Item -ItemType directory .\nugetbuild\
	robocopy /E ..\Source\tusdotnet\ .\nugetbuild\ /MIR
}

function verifyNuget() {
	if(!(Test-Path -Path $nugetLocation)) {
		Throw "Could not find nuget.exe in the provided location: $nugetLocation"
	}
}

function createPackage() {
	& "$nugetLocation" pack .\nugetbuild\tusdotnet.csproj -IncludeReferencedProjects  -Prop Configuration=Release -Build -MSBuildVersion 14
}

function cleanup() {
	Remove-Item -Recurse -Force .\nugetbuild\
}

verifyNuget
Write-Host Copying files for build...
makeBuildFolder
Write-Host Setting version info...
setVersionInfo
Write-Host Version info set
Write-Host Creating package...
createPackage
Write-Host Package created
Write-Host Cleaning up...
cleanup
Write-Host Done!
