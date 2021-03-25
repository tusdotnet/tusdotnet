var uploadProgress = document.getElementById('uploadProgress');
var downloadLink = document.getElementById('downloadLink');
var cancelUploadButton = document.getElementById('cancelUploadButton');
var uploadButton = document.getElementById('uploadButton');
var upload;

function uploadFile() {
    var file = document.getElementById('droppedFile').files[0];

    uploadProgress.value = 0;
    uploadProgress.removeAttribute('data');
    uploadProgress.style.display = 'block';

    disableUpload();

    downloadLink.innerHTML = '';

    upload = new tus.Upload(file,
        {
            endpoint: 'files/',
            onError: onTusError,
            onProgress: onTusProgress,
            onSuccess: onTusSuccess,
            metadata: {
                name: file.name,
                contentType: file.type || 'application/octet-stream',
                emptyMetaKey: ''
            }
        });

    setProgressTest('Starting upload...');

    upload.findPreviousUploads().then(function (previousUploads) {

        if (previousUploads.length) {
            upload.resumeFromPreviousUpload(previousUploads[0]);
        }
        upload.start();

    }).catch(function () {
        upload.start();
    });
}

function cancelUpload() {
    upload && upload.abort();
    setProgressTest('Upload aborted');
    uploadProgress.value = 0;
    enableUpload();
}

function resetLocalCache(e) {
    e.preventDefault();
    localStorage.clear();
    alert('Cache cleared');
}

function onTusError(error) {
    alert(error);
    enableUpload();
}

function onTusProgress(bytesUploaded, bytesTotal) {
    var percentage = (bytesUploaded / bytesTotal * 100).toFixed(2);

    uploadProgress.value = percentage;
    setProgressTest(bytesUploaded + '/' + bytesTotal + ' bytes uploaded');
}

function onTusSuccess() {
    downloadLink.innerHTML = '<a href="' + upload.url + '">Download ' + upload.file.name + '</a>';
    enableUpload();
}

function setProgressTest(text) {
    uploadProgress.setAttribute('data-label', text);
}

function enableUpload() {
    uploadButton.removeAttribute('disabled');
    cancelUploadButton.setAttribute('disabled', 'disabled');
}

function disableUpload() {
    uploadButton.setAttribute('disabled', 'disabled');
    cancelUploadButton.removeAttribute('disabled');
}
