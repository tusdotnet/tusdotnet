
function setVersionInfo() {
	$assemblyVersion = git describe --abbrev=0 --tags
	$assemblyInformationalVersion = git describe --tags --long
	(Get-Content tusdotnet\Properties\AssemblyInfo.cs) -replace `
		'(\[assembly: AssemblyInformationalVersion\(")([\w\W]*)("\)])', `
			"[assembly: AssemblyInformationalVersion(""$assemblyVersion, $assemblyInformationalVersion"")]" `
		| Out-File tusdotnet\Properties\AssemblyInfo.cs
	(Get-Content tusdotnet\Properties\AssemblyInfo.cs) -replace `
		'(\[assembly: AssemblyVersion\(")([\w\W]*)("\)])', "[assembly: AssemblyVersion(""$assemblyVersion"")]" `
		| Out-File tusdotnet\Properties\AssemblyInfo.cs
	(Get-Content tusdotnet\Properties\AssemblyInfo.cs) -replace `
		'(\[assembly: AssemblyFileVersion\(")([\w\W]*)("\)])', "[assembly: AssemblyFileVersion(""$assemblyVersion"")]" `
		| Out-File tusdotnet\Properties\AssemblyInfo.cs
}

function removeInternalVisibleTo() {
	$replaceTest = '[assembly: InternalsVisibleTo("tusdotnet.test")]';
	$replaceBenchmark = '[assembly: InternalsVisibleTo("tusdotnet.benchmark")]';
	$file = '.\tusdotnet\Properties\AssemblyInfo.cs'
	(Get-Content $file).replace($replaceTest, '').replace($replaceBenchmark, '') | Set-Content $file
}

function makeBuildFolder() {
	if(Test-Path -Path ".\tusdotnet\" ){
		Remove-Item -Recurse -Force .\tusdotnet\
	}
	
	New-Item -ItemType directory .\tusdotnet\
	robocopy /E ..\Source\tusdotnet\ .\tusdotnet\ /MIR
}

function verifyNuget() {
	if(![bool](Get-Command dotnet -errorAction SilentlyContinue)) {
		Throw "Could not find dotnet command"
	}
}

function createPackage() {
	Push-Location tusdotnet\
	$version = git describe --abbrev=0 --tags
	& dotnet pack -c Release tusdotnet.csproj /p:Version=$version -o ..\
	Pop-Location
}

function movePackage() {
	Move-Item tusdotnet\bin\Release\*.nupkg .\
}

function cleanup() {
	Remove-Item -Recurse -Force .\tusdotnet\
}

verifyNuget
Write-Host Copying files for build...
makeBuildFolder
Write-Host Setting version info...
setVersionInfo
Write-Host Version info set
Write-Host Removing InternalsVisibleTo attribute...
removeInternalVisibleTo
Write-Host Creating package...
createPackage
movePackage
Write-Host Package created
Write-Host Cleaning up...
cleanup
Write-Host Done!
