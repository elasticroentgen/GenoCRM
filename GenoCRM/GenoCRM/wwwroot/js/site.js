// Shared browser helpers for GenoCRM. Loaded once from App.razor so they are available
// to interactive components as well; inline <script> tags inside components are not
// executed when the component is rendered interactively.
window.downloadFile = function (filename, base64Data) {
    const link = document.createElement('a');
    link.download = filename;
    link.href = 'data:application/octet-stream;base64,' + base64Data;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
