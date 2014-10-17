if(typeof window.AutoReload === 'undefined')
    window.AutoReload = new Object();
window.AutoReload.$ajax = {
    getHTTPObject : function() {
        var http = false;
        if(typeof ActiveXObject != 'undefined') {
            try {http = new ActiveXObject("Msxml2.XMLHTTP");}
            catch (e) {
                try {http = new ActiveXObject("Microsoft.XMLHTTP");}
                catch (E) {http = false;}
            }
        } else if (window.XMLHttpRequest) {
            try {http = new XMLHttpRequest();}
            catch (e) {http = false;}
        }
        return http;
    },
    load : function (url, method, callback, content, format, opt) {
        var http = this.init(); 
        if(!http||!url) return;
        if (http.overrideMimeType) http.overrideMimeType('text/xml');

        if(!method) method = "GET";
        if(!format) format = "text";
        if(!opt) opt = {};
        format = format.toLowerCase();
        method = method.toUpperCase();

        http.open(method, url, true);
        var ths = this;
        if(opt.handler) { 
            http.onreadystatechange = function() { opt.handler(http); };
        } else {
            http.onreadystatechange = function () {
                if (http.readyState == 4) {
                    if(http.status == 200) {
                        var result = "";
                        if(http.responseText) result = http.responseText;
                        if(callback) callback(result);
                    } else {
                        if(opt.loadingIndicator) document.getElementsByTagName("body")[0].removeChild(opt.loadingIndicator);
                        if(opt.loading) document.getElementById(opt.loading).style.display="none";
                    }
                }
            }
        }
        http.send();
    },
    init : function() {return this.getHTTPObject();}
};

window.AutoReload.reload = function() {
    window.AutoReload.$ajax.load("/last-update", "GET", function (data) {
        if (window._update != data) {
            console.log("Updating to " + data);
            location.reload();
        } else {
            setTimeout(window.AutoReload.reload, 300);
        }
    });
}
setTimeout(window.AutoReload.reload, 1500);