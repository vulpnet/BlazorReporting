// S1 — Session Timeout: theo doi hoat dong cua user
// Khi user click, di chuot, nhan phim -> goi .NET RecordActivity()
window.sessionInterop = (() => {
    let _dotNetRef = null;
    let _throttle  = null;

    function notify() {
        if (!_dotNetRef) return;
        clearTimeout(_throttle);
        _throttle = setTimeout(() => {
            _dotNetRef.invokeMethodAsync('JsRecordActivity').catch(() => {});
        }, 500); // throttle 500ms tranh goi qua nhieu
    }

    function start(ref) {
        _dotNetRef = ref;
        ['click','mousemove','keydown','scroll','touchstart'].forEach(ev =>
            document.addEventListener(ev, notify, { passive: true }));
    }

    function stop() {
        _dotNetRef = null;
        ['click','mousemove','keydown','scroll','touchstart'].forEach(ev =>
            document.removeEventListener(ev, notify));
    }

    return { start, stop };
})();
