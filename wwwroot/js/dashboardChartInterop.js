// Dashboard Chart Interop — Chart.js 4.x
window.dashboardChart = (() => {
    const _charts = {};

    const COLORS = [
        '#4f46e5','#0891b2','#16a34a','#d97706','#dc2626',
        '#be185d','#7c3aed','#0369a1','#15803d','#92400e'
    ];

    function color(i, a) {
        const h = COLORS[i % COLORS.length];
        const r = parseInt(h.slice(1,3),16), g = parseInt(h.slice(3,5),16), b = parseInt(h.slice(5,7),16);
        return a ? `rgba(${r},${g},${b},${a})` : h;
    }

    function fmt(n) { return Number(n).toLocaleString('vi-VN'); }

    function destroy(id) {
        if (_charts[id]) { _charts[id].destroy(); delete _charts[id]; }
    }

    // Bar chart — doanh thu theo khu vực/tuyến
    function renderBar(id, labelsJson, valuesJson, label) {
        destroy(id);
        const canvas = document.getElementById(id);
        if (!canvas) return;
        const labels = JSON.parse(labelsJson);
        const values = JSON.parse(valuesJson);
        _charts[id] = new Chart(canvas, {
            type: 'bar',
            data: {
                labels,
                datasets: [{
                    label,
                    data: values,
                    backgroundColor: labels.map((_, i) => color(i, 0.75)),
                    borderColor:     labels.map((_, i) => color(i)),
                    borderWidth: 1.5, borderRadius: 4
                }]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                animation: { duration: 300 },
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: ctx => `  ${fmt(ctx.raw)}` } }
                },
                scales: {
                    y: { ticks: { callback: v => v>=1e9?(v/1e9).toFixed(1)+'B':v>=1e6?(v/1e6).toFixed(0)+'M':fmt(v), font:{size:10} }, grid:{color:'#f1f5f9'} },
                    x: { ticks: { font:{size:10}, maxRotation:35 }, grid:{display:false} }
                }
            }
        });
    }

    // Doughnut — SM status
    function renderDoughnut(id, labelsJson, valuesJson, colorsJson) {
        destroy(id);
        const canvas = document.getElementById(id);
        if (!canvas) return;
        const labels = JSON.parse(labelsJson);
        const values = JSON.parse(valuesJson);
        const bgColors = JSON.parse(colorsJson);
        const total = values.reduce((s, v) => s + v, 0);
        _charts[id] = new Chart(canvas, {
            type: 'doughnut',
            data: { labels, datasets: [{ data: values, backgroundColor: bgColors, borderColor:'#fff', borderWidth:2, hoverOffset:6 }] },
            options: {
                responsive: true, maintainAspectRatio: false,
                cutout: '65%',
                animation: { duration: 300 },
                plugins: {
                    legend: { position:'right', labels:{ boxWidth:12, font:{size:11} } },
                    tooltip: { callbacks: { label: ctx => {
                        const pct = total > 0 ? ((ctx.raw/total)*100).toFixed(1) : 0;
                        return `  ${ctx.label}: ${ctx.raw} (${pct}%)`;
                    }}}
                }
            }
        });
    }

    // Line chart — doanh thu theo ngày trong tháng
    function renderLine(id, labelsJson, datasetsJson, title) {
        destroy(id);
        const canvas = document.getElementById(id);
        if (!canvas) return;
        const labels = JSON.parse(labelsJson);
        const datasets = JSON.parse(datasetsJson).map((ds, i) => ({
            ...ds,
            borderColor: color(i),
            backgroundColor: color(i, 0.1),
            tension: 0.3, fill: true, pointRadius: 3, borderWidth: 2
        }));
        _charts[id] = new Chart(canvas, {
            type: 'line',
            data: { labels, datasets },
            options: {
                responsive: true, maintainAspectRatio: false,
                animation: { duration: 300 },
                plugins: {
                    legend: { position:'top', labels:{ boxWidth:12, font:{size:11} } },
                    title: { display: !!title, text: title, font:{size:12, weight:'700'} },
                    tooltip: { callbacks: { label: ctx => `  ${ctx.dataset.label}: ${fmt(ctx.raw)}` } }
                },
                scales: {
                    y: { ticks: { callback: v => v>=1e9?(v/1e9).toFixed(1)+'B':v>=1e6?(v/1e6).toFixed(0)+'M':fmt(v), font:{size:10} }, grid:{color:'#f1f5f9'} },
                    x: { ticks: { font:{size:10} }, grid:{display:false} }
                }
            }
        });
    }

    // Chart 4a — Bar grouped: CH kế hoạch / đã VT / đơn hàng theo ngày
    function renderVisitCountChart(id, labelsJson, planJson, visitedJson, ordersJson) {
        destroy(id);
        const canvas = document.getElementById(id);
        if (!canvas) return;
        const labels  = JSON.parse(labelsJson);
        const plan    = JSON.parse(planJson);
        const visited = JSON.parse(visitedJson);
        const orders  = JSON.parse(ordersJson);
        _charts[id] = new Chart(canvas, {
            type: 'bar',
            data: {
                labels,
                datasets: [
                    { label: 'CH kế hoạch', data: plan,    backgroundColor: 'rgba(79,70,229,0.5)', borderColor: '#4f46e5', borderWidth: 1.5, borderRadius: 3 },
                    { label: 'CH đã VT',    data: visited, backgroundColor: 'rgba(22,163,74,0.7)', borderColor: '#16a34a', borderWidth: 1.5, borderRadius: 3 },
                    { label: 'Đơn hàng',    data: orders,  backgroundColor: 'rgba(217,119,6,0.7)', borderColor: '#d97706', borderWidth: 1.5, borderRadius: 3, type: 'line', tension: 0.3, pointRadius: 4, fill: false }
                ]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                animation: { duration: 300 },
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: ctx => `Ngày ${ctx[0].label}`,
                            label: ctx => `  ${ctx.dataset.label}: ${fmt(ctx.raw)} CH`
                        }
                    }
                },
                scales: {
                    x: { ticks: { font: { size: 10 } }, grid: { display: false } },
                    y: { ticks: { font: { size: 10 }, stepSize: 1 }, grid: { color: '#f1f5f9' },
                         title: { display: true, text: 'Số cửa hàng', font: { size: 10 } } }
                }
            }
        });
    }

    // Chart 4b — Line dual: % VT MCP và % SO MCP theo ngày
    function renderVisitPctChart(id, labelsJson, visitPctJson, soPctJson) {
        destroy(id);
        const canvas = document.getElementById(id);
        if (!canvas) return;
        const labels    = JSON.parse(labelsJson);
        const visitPct  = JSON.parse(visitPctJson);
        const soPct     = JSON.parse(soPctJson);
        _charts[id] = new Chart(canvas, {
            type: 'line',
            data: {
                labels,
                datasets: [
                    {
                        label: '% VT MCP (CH đã thăm / kế hoạch)',
                        data: visitPct,
                        borderColor: '#0891b2', backgroundColor: 'rgba(8,145,178,0.1)',
                        tension: 0.3, fill: true, pointRadius: 4, borderWidth: 2,
                        pointBackgroundColor: '#0891b2'
                    },
                    {
                        label: '% SO MCP (Đơn hàng / kế hoạch)',
                        data: soPct,
                        borderColor: '#7c3aed', backgroundColor: 'rgba(124,58,237,0.08)',
                        tension: 0.3, fill: true, pointRadius: 4, borderWidth: 2,
                        pointBackgroundColor: '#7c3aed'
                    }
                ]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                animation: { duration: 300 },
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: ctx => `Ngày ${ctx[0].label}`,
                            label: ctx => `  ${ctx.dataset.label}: ${Number(ctx.raw).toFixed(1)}%`
                        }
                    },
                    // Đường tham chiếu 100%
                    annotation: null
                },
                scales: {
                    x: { ticks: { font: { size: 10 } }, grid: { display: false } },
                    y: {
                        min: 0, max: 110,
                        ticks: { callback: v => v + '%', font: { size: 10 } },
                        grid: { color: ctx => ctx.tick.value === 100 ? '#dc2626' : '#f1f5f9',
                                lineWidth: ctx => ctx.tick.value === 100 ? 1.5 : 1 },
                        title: { display: true, text: 'Tỷ lệ (%)', font: { size: 10 } }
                    }
                }
            }
        });
    }

    // Multi-series line/bar chart — visit/order theo ngày trong tháng
    // labels: ["01","02",...], series: [{label, data:[...]}, ...]
    function renderMultiLine(id, labelsJson, seriesJson, title) {
        destroy(id);
        const canvas = document.getElementById(id);
        if (!canvas) return;
        const labels  = JSON.parse(labelsJson);
        const series  = JSON.parse(seriesJson);
        const datasets = series.map((s, i) => ({
            label           : s.label,
            data            : s.data,
            borderColor     : color(i),
            backgroundColor : color(i, 0.12),
            tension         : 0.3,
            fill            : false,
            pointRadius     : 3,
            borderWidth     : 2,
            yAxisID         : s.axis || 'y',
        }));
        _charts[id] = new Chart(canvas, {
            type: 'line',
            data: { labels, datasets },
            options: {
                responsive: true, maintainAspectRatio: false,
                animation: { duration: 300 },
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'top', labels: { boxWidth: 12, font: { size: 11 } } },
                    title : { display: !!title, text: title, font: { size: 12, weight: '700' } },
                    tooltip: { callbacks: { label: ctx => `  ${ctx.dataset.label}: ${fmt(ctx.raw)}` } }
                },
                scales: {
                    y : { position: 'left',  ticks: { font: { size: 10 } }, grid: { color: '#f1f5f9' } },
                    y2: { position: 'right', ticks: { callback: v => v + '%', font: { size: 10 } },
                          grid: { display: false }, min: 0, max: 110 }
                }
            }
        });
    }

    // Pie chart level — labels, values, colors đều là JSON array
    function renderPieLevel(id, labelsJson, valuesJson, colorsJson, title) {
        destroy(id);
        const canvas = document.getElementById(id);
        if (!canvas) return;
        const labels = JSON.parse(labelsJson);
        const values = JSON.parse(valuesJson);
        const colors = JSON.parse(colorsJson);
        const total  = values.reduce((s, v) => s + v, 0);
        _charts[id] = new Chart(canvas, {
            type: 'pie',
            data: { labels, datasets: [{ data: values, backgroundColor: colors, borderColor: '#fff', borderWidth: 2 }] },
            options: {
                responsive: true, maintainAspectRatio: false,
                animation: { duration: 300 },
                plugins: {
                    legend: { position: 'right', labels: { boxWidth: 12, font: { size: 11 } } },
                    title : { display: !!title, text: title, font: { size: 12, weight: '700' } },
                    tooltip: { callbacks: { label: ctx => {
                        const pct = total > 0 ? ((ctx.raw / total) * 100).toFixed(1) : 0;
                        return `  ${ctx.label}: ${ctx.raw} TDV (${pct}%)`;
                    }}}
                }
            },
            plugins: [{
                id: 'pctLabel',
                afterDatasetsDraw(chart) {
                    const { ctx, data } = chart;
                    ctx.save();
                    data.datasets[0].data.forEach((val, i) => {
                        const meta = chart.getDatasetMeta(0);
                        if (meta.data[i].hidden || val === 0) return;
                        const pct = total > 0 ? ((val / total) * 100).toFixed(1) : 0;
                        if (Number(pct) < 3) return;
                        const { x, y } = meta.data[i].tooltipPosition();
                        ctx.fillStyle = '#fff';
                        ctx.font = 'bold 10px sans-serif';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'middle';
                        ctx.fillText(pct + '%', x, y);
                    });
                    ctx.restore();
                }
            }]
        });
    }

    function destroyAll() { Object.keys(_charts).forEach(destroy); }

    return { renderBar, renderDoughnut, renderLine, renderMultiLine, renderPieLevel,
             renderVisitCountChart, renderVisitPctChart, destroyAll };
})();
