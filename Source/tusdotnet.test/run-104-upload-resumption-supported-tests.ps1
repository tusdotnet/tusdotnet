# Test script to test that 104 Upload Resumption Supported works as intended on different http versions.
# How to:
# 1. Start the .NET 6 test site. Make sure that it responds to :5007 for TLS and :5006 for default http.
# 2. Run this script
# 3. Inspect the output for each http version
# 4. Verify that the files were completely written

# The curl clone that is built into Windows doesn't work, so we need the real deal here. 
$path="C:\Users\Stefan\Downloads\curl-8.1.1_1-win64-mingw\curl-8.1.1_1-win64-mingw\bin\curl.exe"
$url="https://localhost:5007/files-tus-2"
$urlnotls="http://localhost:5006/files-tus-2"
#$url="http://localhost/files" # NOTE: URL when running behing nginx which needs to be configured to reverse proxy $urlnotls
$newline="`n`n"
 
Write-Host "Upload using HTTP/1.0:"
Write-Host "----------------------"
& $path --insecure -v --http1.0 -H 'Expect: 100-continue' --data-binary 'Hello RUGH H1.0' $urlnotls
Write-Host $newline
Write-Host "Upload using HTTP/1.1:"
Write-Host "----------------------"
& $path --insecure -v --http1.1 -H 'Expect: 100-continue' --data-binary 'Hello RUGH H1.1' $url
Write-Host $newline
Write-Host "Upload using HTTP/2.0:"
Write-Host "----------------------"
& $path --insecure -v --http2 -H 'Expect: 100-continue' --data-binary 'Hello RUGH H2' $url
 
# NOTE: HTTP/3 is experimental in dotnet and is currently not supported
#.\curl.exe --insecure -v --http3-only -H 'Expect: 100-continue' --data-binary ' ' $url