// ══════════════════════════════════════════════════════
//  Chart.js Interop — Sales Area Chart
//  Loại: bar-v | pie | doughnut
//  Click item → gọi Blazor FlyToChartItemAsync
// ══════════════════════════════════════════════════════

window.chartInterop = (() => {
    let _chart     = null;
    let _dotNetRef = null;

    const PALETTE = [
        '#4f46e5','#7c3aed','#2563eb','#0891b2','#16a34a',
        '#d97706','#dc2626','#be185d','#0369a1','#15803d',
        '#92400e','#1e40af','#065f46','#7e22ce','#991b1b',
        '#0f766e','#b45309','#6d28d9','#047857','#9f1239',
    ];

    function color(i, alpha) {
        const hex = PALETTE[i % PALETTE.length];
        const r = parseInt(hex.slice(1,3),16),
              g = parseInt(hex.slice(3,5),16),
              b = parseInt(hex.slice(5,7),16);
        return alpha ? `rgba(${r},${g},${b},${alpha})` : hex;
    }

    function fmt(n)         { return Number(n).toLocaleString('vi-VN'); }
    function short(s, mx=20){ return s && s.length > mx ? s.slice(0,mx)+'…' : (s||'(Khác)'); }

    // ── Đăng ký DotNetRef ────────────────────────────────
    function setDotNetRef(ref) { _dotNetRef = ref; }

    // ── Render ────────────────────────────────────────────
    // chartType: 'bar-v' | 'pie' | 'doughnut'
    function render(canvasId, jsonStr, title, chartType) {
        if (typeof Chart === 'undefined') {
            console.error('[chart] Chart.js chưa load'); return;
        }

        let items;
        try { items = JSON.parse(jsonStr); } catch(e) { return; }
        if (!items || items.length === 0) return;

        const canvas = document.getElementById(canvasId);
        if (!canvas) { console.error('[chart] #' + canvasId + ' not found'); return; }

        if (_chart) { _chart.destroy(); _chart = null; }

        const rawLabels = items.map(d => d.label || d.Label || '(Khác)');
        const labels    = rawLabels.map(l => short(l));
        const amounts   = items.map(d => Number(d.totalAmount || d.TotalAmount || 0));
        const orders    = items.map(d => Number(d.orderCount  || d.OrderCount  || 0));
        const sms       = items.map(d => Number(d.smCount     || d.SmCount     || 0));
        const total     = amounts.reduce((s,v) => s+v, 0);

        const isRound = chartType === 'pie' || chartType === 'doughnut';
        const bgColors  = amounts.map((_, i) => color(i, isRound ? 0.85 : 0.78));
        const brdColors = amounts.map((_, i) => color(i));

        // ── Click handler → Blazor FlyToChartItemAsync ───
        const onClick = (evt, elements) => {
            if (!elements.length || !_dotNetRef) return;
            const i   = elements[0].index;
            const lbl = rawLabels[i];   // label gốc (không rút gọn) để match DB
            _dotNetRef.invokeMethodAsync('FlyToChartItemAsync', lbl)
                      .catch(e => console.error('[chart] click error', e));
        };

        const dataset = isRound
            ? { data: amounts, backgroundColor: bgColors,
                borderColor: '#fff', borderWidth: 2, hoverOffset: 10 }
            : { label: 'Doanh số', data: amounts,
                backgroundColor: bgColors, borderColor: brdColors,
                borderWidth: 1.5, borderRadius: 5, borderSkipped: false };

        const tooltipCallbacks = {
            title: ctx => isRound ? rawLabels[ctx[0].dataIndex] : ctx[0].label,
            label: ctx => {
                const i   = ctx.dataIndex;
                const pct = total > 0 ? ((amounts[i]/total)*100).toFixed(1) : 0;
                return [
                    `  💰 ${fmt(amounts[i])} đ  (${pct}%)`,
                    `  📋 ${orders[i]} đơn  ·  👤 ${sms[i]} SM`,
                    `  ← Click để xem trên bản đồ`
                ];
            }
        };

        const moneyTick = v => {
            if (v >= 1e9) return (v/1e9).toFixed(1)+'B';
            if (v >= 1e6) return (v/1e6).toFixed(0)+'M';
            if (v >= 1e3) return (v/1e3).toFixed(0)+'k';
            return v.toLocaleString('vi-VN');
        };

        // Plugin hiện % trong slice
        const pctPlugin = isRound ? {
            id: 'pctLabel',
            afterDatasetsDraw(chart) {
                const { ctx, data } = chart;
                ctx.save();
                data.datasets[0].data.forEach((val, i) => {
                    const meta = chart.getDatasetMeta(0);
                    if (meta.data[i].hidden) return;
                    const pct = total > 0 ? ((val/total)*100).toFixed(1) : 0;
                    if (Number(pct) < 2) return;
                    const { x, y } = meta.data[i].tooltipPosition();
                    ctx.fillStyle = '#fff';
                    ctx.font = 'bold 10px sans-serif';
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(pct+'%', x, y);
                });
                ctx.restore();
            }
        } : null;

        const config = {
            type   : isRound ? chartType : 'bar',
            data   : { labels, datasets: [dataset] },
            options: {
                responsive          : true,
                maintainAspectRatio : false,
                animation           : { duration: 350 },
                onClick,
                plugins: {
                    legend: {
                        display : isRound,
                        position: 'right',
                        labels  : {
                            boxWidth : 12, font: { size: 11 },
                            generateLabels: chart => {
                                const ds = chart.data.datasets[0];
                                return chart.data.labels.map((lbl, i) => ({
                                    text      : short(lbl, 18),
                                    fillStyle : ds.backgroundColor[i],
                                    strokeStyle: '#fff',
                                    hidden    : false,
                                    index     : i
                                }));
                            }
                        }
                    },
                    title  : { display: !!title, text: title,
                               font: { size: 12, weight:'700' },
                               color: '#1e293b', padding: { bottom: 6 } },
                    tooltip: { callbacks: tooltipCallbacks }
                },
                scales: isRound ? {} : {
                    y: { ticks: { callback: moneyTick, font:{ size:10 } },
                         grid : { color:'#f1f5f9' } },
                    x: { ticks: { font:{ size:10 }, color:'#374151',
                                  maxRotation: 35, minRotation: 0 },
                         grid : { display: false } }
                }
            },
            plugins: [pctPlugin].filter(Boolean)
        };

        // Con trỏ pointer khi hover bar/slice
        canvas.style.cursor = 'pointer';
        _chart = new Chart(canvas, config);
        console.log(`[chart] ${items.length} items, type=${chartType}`);
    }

    function destroy() {
        if (_chart) { _chart.destroy(); _chart = null; }
    }

    return { setDotNetRef, render, destroy };
})();
