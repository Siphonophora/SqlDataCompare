// Source: https://chrissainty.com/copy-to-clipboard-in-blazor/
window.clipboardCopy = {
    copyText: function (text) {
        navigator.clipboard.writeText(text).then(function () {
            //alert("Copied to clipboard!");
        })
            .catch(function (error) {
                alert(error);
            });
    }
};
