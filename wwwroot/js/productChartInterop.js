// ══════════════════════════════════════════════════════
//  productChartInterop.js
//  Biểu đồ phân tích sản phẩm:
//    renderAreaChart  — grouped bar (sản phẩm × khu vực)
//    renderTrendChart — line + dashed prediction
// ══════════════════════════════════════════════════════

window.productChartInterop = (() => {
    let _areaChart  = null;
    let _trendChart = null;

    const PALETTE = [
        '#4f46e5','#dc2626','#16a34a','#d97706','#7c3aed',
        '#0891b2','#be185d','#f59e0b','#10b981','#3b82f6',
        '#ef4444','#8b5cf6','#06b6d4','#84cc16','#f97316',
        '#ec4899','#6366f1','#14b8a6','#a855f7','#64748b'
    ];

    function hexToRgb(hex) {
        return {
            r: parseInt(hex.slice(1,3),16),
            g: parseInt(hex.slice(3,5),16),
            b: parseInt(hex.slice(5,7),16)
        };
    }

    function color(i, alpha) {
        const c = hexToRgb(PALETTE[i % PALETTE.length]);
        return alpha !== undefined
            ? `rgba(${c.r},${c.g},${c.b},${alpha})`
            : PALETTE[i % PALETTE.length];
    }

    function fmt(n) { return Number(n).toLocaleString('vi-VN'); }

    function yTick(v) {
        if (v >= 1e9) return (v/1e9).toFixed(1)+'B';
        if (v >= 1e6) return (v/1e6).toFixed(0)+'M';
        if (v >= 1e3) return (v/1e3).toFixed(0)+'k';
        return v;
    }

    // ── Grouped bar: areas on X, one dataset per product ────────
    // json: [{inventoryCD, areaLabel, totalQty, totalAmount, orderCount}, ...]
    // metric: "qty" | "amount"
    function renderAreaChart(canvasId, jsonStr, metric) {
        if (typeof Chart === 'undefined') return;
        let data;
        try { data = JSON.parse(jsonStr); } catch { return; }
        if (!data || data.length === 0) return;

        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        if (_areaChart) { _areaChart.destroy(); _areaChart = null; }

        const get  = (d, k1, k2) => d[k1] ?? d[k2] ?? '';
        const val  = metric === 'amount'
            ? d => Number(d.totalAmount ?? d.TotalAmount ?? 0)
            : d => Number(d.totalQty   ?? d.TotalQty   ?? 0);
        // Ưu tiên InventoryName, fallback InventoryCD
        const prodLabel = d =>
            get(d,'inventoryName','InventoryName') || get(d,'inventoryCD','InventoryCD');
        const prodKey   = d => get(d,'inventoryCD','InventoryCD');

        // Unique areas sorted by total desc
        const areaTotals = {};
        data.forEach(d => {
            const a = get(d,'areaLabel','AreaLabel');
            areaTotals[a] = (areaTotals[a] || 0) + val(d);
        });
        const areas = Object.entries(areaTotals)
            .sort((a,b) => b[1]-a[1])
            .map(([a]) => a);

        // Unique products sorted by total desc (key = CD, label = Name)
        const prodTotals = {};
        const prodNames  = {};
        data.forEach(d => {
            const cd = prodKey(d);
            prodTotals[cd] = (prodTotals[cd] || 0) + val(d);
            prodNames[cd]  = prodLabel(d);
        });
        const products = Object.entries(prodTotals)
            .sort((a,b) => b[1]-a[1])
            .map(([cd]) => cd);

        // Lookup map [CD|area] → value
        const lookup = {};
        data.forEach(d => {
            const key = `${prodKey(d)}|${get(d,'areaLabel','AreaLabel')}`;
            lookup[key] = val(d);
        });

        const datasets = products.map((cd, i) => ({
            label           : prodNames[cd] || cd,
            data            : areas.map(area => lookup[`${cd}|${area}`] || 0),
            backgroundColor : color(i, 0.78),
            borderColor     : color(i),
            borderWidth     : 1.5,
            borderRadius    : 4,
            borderSkipped   : false
        }));

        const unit = metric === 'amount' ? 'đ' : 'SP';

        _areaChart = new Chart(canvas, {
            type: 'bar',
            data: { labels: areas, datasets },
            options: {
                responsive         : true,
                maintainAspectRatio: false,
                animation          : { duration: 400 },
                plugins: {
                    legend: {
                        position: 'top',
                        labels  : { boxWidth: 11, font: { size: 10 } }
                    },
                    tooltip: {
                        callbacks: {
                            label: ctx => `  ${ctx.dataset.label}: ${fmt(ctx.raw)} ${unit}`
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: { font: { size: 10 }, maxRotation: 35 },
                        grid : { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: { callback: yTick, font: { size: 10 } },
                        grid : { color: '#f1f5f9' }
                    }
                }
            }
        });
        console.log(`[productChart] area chart: ${products.length} products × ${areas.length} areas`);
    }

    // ── Line chart: actual (solid) + predicted (dashed) ──────────
    // json: {inventoryCD, actual:[{label,qty,amount}], predicted:[{label,qty,amount}]}
    // metric: "qty" | "amount"
    function renderTrendChart(canvasId, jsonStr, metric) {
        if (typeof Chart === 'undefined') return;
        let data;
        try { data = JSON.parse(jsonStr); } catch { return; }
        if (!data) return;

        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        if (_trendChart) { _trendChart.destroy(); _trendChart = null; }

        const actual    = data.actual    || [];
        const predicted = data.predicted || [];
        const field     = metric === 'amount' ? 'amount' : 'qty';
        const unit      = metric === 'amount' ? 'đ' : 'SP';

        const allLabels  = [...actual.map(d => d.label), ...predicted.map(d => d.label)];
        const actualVals = actual.map(d => d[field]);

        // Connect last actual → first predicted
        const actualFull = [...actualVals, ...predicted.map(() => null)];
        const predFull   = [...actual.map(() => null), ...predicted.map(d => d[field])];
        if (actual.length > 0 && predicted.length > 0) {
            actualFull[actual.length] = predicted[0][field];
            predFull[actual.length-1] = actualVals[actualVals.length-1];
        }

        _trendChart = new Chart(canvas, {
            type: 'line',
            data: {
                labels  : allLabels,
                datasets: [
                    {
                        label          : 'Thực tế',
                        data           : actualFull,
                        borderColor    : '#4f46e5',
                        backgroundColor: 'rgba(79,70,229,.07)',
                        borderWidth    : 2.5,
                        pointRadius    : 4,
                        pointHoverRadius: 6,
                        fill           : true,
                        tension        : 0.35,
                        spanGaps       : true
                    },
                    {
                        label          : 'Dự đoán',
                        data           : predFull,
                        borderColor    : '#f59e0b',
                        backgroundColor: 'rgba(245,158,11,.05)',
                        borderWidth    : 2.5,
                        borderDash     : [7, 4],
                        pointRadius    : 5,
                        pointStyle     : 'rectRot',
                        pointHoverRadius: 7,
                        fill           : true,
                        tension        : 0.3,
                        spanGaps       : true
                    }
                ]
            },
            options: {
                responsive         : true,
                maintainAspectRatio: false,
                animation          : { duration: 450 },
                plugins: {
                    legend: {
                        position: 'top',
                        labels  : { boxWidth: 11, font: { size: 10 } }
                    },
                    title: {
                        display: !!(data.inventoryName || data.inventoryCD),
                        text   : data.inventoryName || data.inventoryCD || '',
                        font   : { size: 11, weight: '700' },
                        color  : '#1e293b', padding: { bottom: 4 }
                    },
                    tooltip: {
                        callbacks: {
                            label: ctx => ctx.raw != null
                                ? `  ${ctx.dataset.label}: ${fmt(ctx.raw)} ${unit}`
                                : null
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: { font: { size: 10 }, maxRotation: 30 },
                        grid : { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: { callback: yTick, font: { size: 10 } },
                        grid : { color: '#f1f5f9' }
                    }
                }
            }
        });
    }

    function destroyAll() {
        if (_areaChart)  { _areaChart.destroy();  _areaChart  = null; }
        if (_trendChart) { _trendChart.destroy();  _trendChart = null; }
    }

    return { renderAreaChart, renderTrendChart, destroyAll };
})();
