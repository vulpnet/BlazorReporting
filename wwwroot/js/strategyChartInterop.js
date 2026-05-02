// ══════════════════════════════════════════════════════
//  strategyChartInterop.js  —  Sales Strategy Charts
// ══════════════════════════════════════════════════════
window.strategyChartInterop = (() => {
    let _imp = null, _exp = null;

    function fmt(n) { return Number(n).toLocaleString('vi-VN'); }

    function horizontal(canvasId, jsonStr, color, title) {
        if (typeof Chart === 'undefined') return;
        let data; try { data = JSON.parse(jsonStr); } catch { return; }
        if (!data || data.length === 0) return;
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        const labels = data.map(d => d.label);
        const values = data.map(d => d.value);

        return new Chart(canvas, {
            type: 'bar',
            data: {
                labels,
                datasets: [{
                    label: title,
                    data : values,
                    backgroundColor: color + 'cc',
                    borderColor    : color,
                    borderWidth    : 1.5,
                    borderRadius   : 4,
                    borderSkipped  : false
                }]
            },
            options: {
                indexAxis: 'y',
                responsive: true, maintainAspectRatio: false,
                animation : { duration: 350 },
                plugins: {
                    legend : { display: false },
                    tooltip: {
                        callbacks: {
                            label: ctx => `  Tăng trưởng dự đoán: +${fmt(ctx.raw.toFixed(1))}%`
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        ticks: { callback: v => v + '%', font: { size: 10 } },
                        grid : { color: '#f1f5f9' }
                    },
                    y: { ticks: { font: { size: 10 } }, grid: { display: false } }
                }
            }
        });
    }

    function renderImport(canvasId, jsonStr) {
        if (_imp) { _imp.destroy(); _imp = null; }
        _imp = horizontal(canvasId, jsonStr, '#16a34a', 'Tăng trưởng dự đoán');
    }

    function renderExport(canvasId, jsonStr) {
        if (_exp) { _exp.destroy(); _exp = null; }
        _exp = horizontal(canvasId, jsonStr, '#dc2626', 'Giảm dự đoán');
    }

    function destroyAll() {
        if (_imp) { _imp.destroy(); _imp = null; }
        if (_exp) { _exp.destroy(); _exp = null; }
    }

    return { renderImport, renderExport, destroyAll };
})();
