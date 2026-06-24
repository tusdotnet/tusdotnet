# Framework to use when generating coverage. Must be a "real" framework, i.e. not netstandard.
$framework = "net10.0";
# The file path passed to coverlet...
$coverletWantedOutput = "$PSScriptRoot\coverage.xml";
# ...which will result in the following filename.
$coverletActualOutput = "$PSScriptRoot\" + "coverage.{framework}.xml".Replace("{framework}", $framework);
If (Test-Path $coverletActualOutput) {
	Remove-Item $coverletActualOutput -Force
}

dotnet test -f $framework /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=$coverletWantedOutput /p:Exclude="[xunit.*]*" ../source/tusdotnet.sln

$key = Read-Host -Prompt 'Input Codecov key'

if(-not (Test-Path $coverletActualOutput)) {
	throw "$coverletActualOutput was not found";
}

# Run the CLI tool from the root of the repo to get the file lists correctly
Push-Location ..
.\Tools\codecov.exe --verbose upload-process --fail-on-error --token $key -f $coverletActualOutput
Pop-Location