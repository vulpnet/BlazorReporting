// ══════════════════════════════════════════════════════
//  Territory Map Interop — Blazor Server
//  Namespace: window.territoryMapInterop
//  Map element id: territory-map-canvas
// ══════════════════════════════════════════════════════

window.territoryMapInterop = (() => {
    let _map          = null;
    let _markers      = [];
    let _markerGroup  = null;
    let _routeGroup   = null;
    let _dotNetRef    = null;

    // Territory / outlet layers
    let _territoryGroup = null;
    let _outletGroup    = null;
    let _compareGroup   = null;

    // Replay state
    let _replayTimer    = null;
    let _replayPaused   = false;
    let _replayPts      = [];
    let _replayIdx      = 0;
    let _replayMarker   = null;
    let _replayDotNet   = null;
    let _replaySpeed    = 500;

    const COLORS = [
        '#2563eb','#dc2626','#16a34a','#d97706','#7c3aed',
        '#0891b2','#be185d','#15803d','#b45309','#4f46e5'
    ];
    const colorFor = i => COLORS[i % COLORS.length];

    // ── Coverage rate → colour ────────────────────────
    function coverageColor(rate) {
        if (rate >= 80) return '#16a34a';
        if (rate >= 60) return '#65a30d';
        if (rate >= 40) return '#d97706';
        if (rate >= 20) return '#dc2626';
        return '#991b1b';
    }

    // ── SVG pin icon ──────────────────────────────────
    function makeIcon(color, label) {
        const svg = [
            '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 42" width="32" height="42">',
            '<path d="M16 0C9.37 0 4 5.37 4 12c0 9 12 30 12 30S28 21 28 12C28 5.37 22.63 0 16 0z"',
            ' fill="' + color + '" stroke="white" stroke-width="1.5"/>',
            '<circle cx="16" cy="12" r="6" fill="white" opacity="0.9"/>',
            '<text x="16" y="16" text-anchor="middle" font-size="7" font-weight="bold"',
            ' fill="' + color + '" font-family="Arial,sans-serif">' + label + '</text>',
            '</svg>'
        ].join('');
        return L.divIcon({ html: svg, className: '', iconSize: [32,42], iconAnchor: [16,42], popupAnchor: [0,-40] });
    }

    // ── Diamond icon (compare day 2) ──────────────────
    function makeDiamondIcon(label) {
        const svg =
            '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="28" height="28">' +
            '<polygon points="16,2 30,16 16,30 2,16" fill="#94a3b8" stroke="white" stroke-width="1.5"/>' +
            '<text x="16" y="20" text-anchor="middle" font-size="7" font-weight="bold" fill="white" font-family="Arial,sans-serif">' + label + '</text>' +
            '</svg>';
        return L.divIcon({ html: svg, className: '', iconSize: [28,28], iconAnchor: [14,14], popupAnchor: [0,-16] });
    }

    // ── Popup builder ─────────────────────────────────
    function makePopup(color, userName, timeStr, lat, lng, addrObj, loc) {
        let addrHtml;
        if (addrObj && addrObj.formatted) {
            addrHtml =
                '<div style="margin-top:2px">' +
                (addrObj.road    ? '<div>&#128739; ' + addrObj.road    + '</div>' : '') +
                (addrObj.ward    ? '<div>&#127968; ' + addrObj.ward    + '</div>' : '') +
                (addrObj.district? '<div>&#127970; ' + addrObj.district + '</div>' : '') +
                (addrObj.city    ? '<div>&#127961; ' + addrObj.city    + '</div>' : '') +
                '</div>';
        } else {
            addrHtml = '<div style="color:#94a3b8;font-style:italic;margin-top:2px">Đang lấy địa chỉ…</div>';
        }

        const safeUser = userName.replace(/'/g, "\\'");

        const salesHtml = (loc && loc.totalSales && Number(loc.totalSales) > 0)
            ? '<div style="margin-top:4px;font-weight:700;color:#16a34a;font-size:.8rem">' +
              '💰 ' + formatMoney(Number(loc.totalSales)) + '</div>'
            : '';

        const routeBtn =
            '<div style="margin-top:8px;padding-top:8px;border-top:1px solid #f1f5f9">' +
            '<button onclick="territoryMapInterop.requestRoute(\'' + safeUser + '\')" ' +
                'style="width:100%;padding:5px 10px;border:none;border-radius:6px;' +
                'background:linear-gradient(135deg,#4f46e5,#7c3aed);color:#fff;' +
                'font-size:.75rem;font-weight:600;cursor:pointer;' +
                'display:flex;align-items:center;justify-content:center;gap:5px;' +
                'font-family:sans-serif;letter-spacing:.02em;">' +
            '<span style="font-size:.9rem">&#128739;</span> Xem lộ trình' +
            '</button></div>';

        return (
            '<div style="min-width:220px;font-family:sans-serif">' +
            '<div style="font-weight:700;font-size:.95rem;color:#1e293b;' +
                'border-bottom:2px solid ' + color + ';padding-bottom:5px;margin-bottom:6px">' +
            '<span style="display:inline-block;width:10px;height:10px;border-radius:50%;' +
                'background:' + color + ';margin-right:6px;vertical-align:middle"></span>' +
            userName + '</div>' +
            '<div style="font-size:.78rem;color:#475569;line-height:1.85">' +
            '<div>&#128336; ' + timeStr + '</div>' +
            addrHtml +
            salesHtml +
            '<div style="color:#d1d5db;font-size:.66rem;margin-top:4px">&#128205; ' +
                lat.toFixed(6) + ', ' + lng.toFixed(6) + '</div>' +
            '</div>' +
            routeBtn +
            '</div>'
        );
    }

    // ── Init ──────────────────────────────────────────
    function init(elementId, centerLat, centerLng, zoom) {
        if (_map) { _map.remove(); _map = null; }
        _markers = []; _markerGroup = null;

        const el = document.getElementById(elementId);
        if (!el) { console.error('[tmap] element not found:', elementId); return; }

        el.style.cssText = 'position:absolute;top:0;left:0;right:0;bottom:0;';

        _map = L.map(elementId, { zoomControl: true, preferCanvas: true })
                .setView([centerLat, centerLng], zoom);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(_map);

        _markerGroup    = L.layerGroup().addTo(_map);
        _routeGroup     = L.layerGroup().addTo(_map);
        _territoryGroup = L.layerGroup().addTo(_map);
        _outletGroup    = L.layerGroup().addTo(_map);
        _compareGroup   = L.layerGroup().addTo(_map);

        setTimeout(() => { if (_map) _map.invalidateSize(); }, 150);
        console.log('[tmap] init OK');
    }

    function setDotNetRef(ref) { _dotNetRef = ref; }

    function requestRoute(userName) {
        if (!_dotNetRef) return;
        _map && _map.closePopup();
        _dotNetRef.invokeMethodAsync('ShowRouteByNameAsync', userName)
            .catch(e => console.error('[tmap] requestRoute error', e));
    }

    // ── Add markers ───────────────────────────────────
    function addMarkersJson(jsonStr) {
        if (!_map || !_markerGroup) return;
        _markerGroup.clearLayers();
        _markers = [];

        let locs;
        try { locs = JSON.parse(jsonStr); } catch(e) { return; }
        if (!Array.isArray(locs) || locs.length === 0) return;

        const bounds = [];

        locs.forEach((loc, idx) => {
            const lat = Number(loc.lat), lng = Number(loc.lng);
            if (!isFinite(lat) || !isFinite(lng) || (lat===0 && lng===0)) return;

            const name     = String(loc.userName || 'N/A');
            const initials = name.replace(/[^A-Z0-9]/gi, '').slice(-2).toUpperCase() || 'NA';
            const color    = colorFor(idx);
            const timeStr  = loc.checktime || '—';

            const popup = L.popup({ maxWidth: 300 })
                .setContent(makePopup(color, name, timeStr, lat, lng, null, loc));

            const marker = L.marker([lat, lng], { icon: makeIcon(color, initials) })
                .bindPopup(popup);

            _markerGroup.addLayer(marker);
            _markers.push({ marker, userName: name, lat, lng, color, timeStr, idx, loc });
            bounds.push([lat, lng]);
        });

        if (bounds.length > 0) {
            try { _map.fitBounds(bounds, { padding: [50,50], maxZoom: 13 }); } catch(e) {}
        }
        console.log('[tmap] rendered', _markers.length, 'markers');
    }

    // ── Geocoding ─────────────────────────────────────
    async function startGeocoding(jsonStr, dotNetRef) {
        let locs;
        try { locs = JSON.parse(jsonStr); } catch(e) { return; }
        if (!Array.isArray(locs) || locs.length === 0) return;

        for (let i = 0; i < locs.length; i++) {
            const loc = locs[i];
            const lat = Number(loc.lat), lng = Number(loc.lng);
            if (!isFinite(lat) || !isFinite(lng)) continue;

            try {
                const url = 'https://nominatim.openstreetmap.org/reverse'
                    + '?lat=' + lat + '&lon=' + lng
                    + '&format=json&accept-language=vi&addressdetails=1';

                const resp = await fetch(url, { headers: { 'Accept': 'application/json' } });
                const data = await resp.json();
                const a    = data.address || {};

                let roadLine = '';
                if (a.house_number) roadLine += a.house_number + ' ';
                roadLine += a.road || a.pedestrian || a.footway || a.path || a.alley || a.lane || '';

                const alley = (a.alley || a.lane || a.hamlet)
                    && (a.alley || a.lane || a.hamlet) !== (a.road || '')
                    ? (a.alley || a.lane || a.hamlet) : '';

                const ward     = a.quarter || a.suburb || a.neighbourhood || a.village || a.hamlet || '';
                const district = a.city_district || a.district || a.county || a.borough || '';
                const city     = a.city || a.town || a.municipality || a.state_district || a.state || '';

                const sidebarAddr = [alley || roadLine, ward, district, city].filter(Boolean).join(', ');

                const addrObj = {
                    formatted: true,
                    road    : (alley ? alley + ' · ' : '') + (roadLine || ''),
                    ward, district, city
                };

                const entry = _markers.find(m => m.userName === loc.userName);
                if (entry) {
                    entry.addrObj = addrObj;
                    entry.marker.getPopup()?.setContent(
                        makePopup(entry.color, entry.userName, entry.timeStr,
                                  entry.lat, entry.lng, addrObj, entry.loc));
                }

                if (dotNetRef) {
                    await dotNetRef.invokeMethodAsync('UpdateAddressAsync', loc.userName, sidebarAddr);
                }
            } catch(e) { console.warn('[tmap geocode] error', loc.userName, e); }

            await new Promise(r => setTimeout(r, 1100));
        }
        console.log('[tmap] geocoding complete');
    }

    // ── Route ─────────────────────────────────────────
    // ── Popup cửa hàng dùng chung (drawRoute + replay) ──
    function buildVisitPopup(v) {
        const hasOrder = v.orderAmount && Number(v.orderAmount) > 0;
        const visitMin = (v.startTime && v.endTime)
            ? (() => {
                const [h1,m1,s1] = v.startTime.split(':').map(Number);
                const [h2,m2,s2] = v.endTime.split(':').map(Number);
                const diff = (h2*3600+m2*60+s2) - (h1*3600+m1*60+s1);
                return diff > 0 ? diff : null;
              })()
            : null;
        const fmtDur = sec => {
            if (!sec || sec <= 0) return null;
            const m = Math.floor(sec/60), s = sec%60;
            return m > 0 ? `${m} phút ${s > 0 ? s + ' giây' : ''}`.trim() : `${s} giây`;
        };
        return `<div style="font-family:sans-serif;min-width:190px;font-size:.78rem">
            <div style="font-weight:700;font-size:.88rem;border-bottom:2px solid #f59e0b;padding-bottom:4px;margin-bottom:6px;color:#1e293b">
                🏪 ${v.locationName || v.customerCD}
            </div>
            <div style="color:#475569;line-height:2">
                <div>📋 Mã KH: <b>${v.customerCD}</b></div>
                ${v.address ? `<div>📍 ${v.address}</div>` : ''}
                ${v.routeCode ? `<div>🗺 Tuyến: ${v.routeCode}</div>` : ''}
                ${hasOrder ? `<div>💰 Doanh số: <b style="color:#16a34a">${formatMoney(Number(v.orderAmount))}</b></div>` : '<div style="color:#94a3b8">Không có đơn hàng</div>'}
                ${v.startTime ? `<div>⏱ Vào: <b>${v.startTime}</b>${v.endTime ? ' — Ra: <b>' + v.endTime + '</b>' : ''}</div>` : ''}
                ${visitMin ? `<div>🕐 Thời gian viếng thăm: <b>${fmtDur(visitMin)}</b></div>` : ''}
            </div>
        </div>`;
    }

    function drawRoute(jsonStr) {
        if (!_map || !_routeGroup) return;
        _routeGroup.clearLayers();

        let pts;
        try { pts = JSON.parse(jsonStr); } catch(e) { return; }
        if (!Array.isArray(pts) || pts.length === 0) return;

        const latLngs = pts.map(p => [Number(p.lat), Number(p.lng)]);
        const n = latLngs.length;

        let totalKm = 0;
        for (let i = 1; i < n; i++)
            totalKm += haversineKm(latLngs[i-1][0], latLngs[i-1][1], latLngs[i][0], latLngs[i][1]);

        L.polyline(latLngs, { color:'#94a3b8', weight:6, opacity:0.4, lineJoin:'round', lineCap:'round' }).addTo(_routeGroup);
        L.polyline(latLngs, { color:'#4f46e5', weight:3, opacity:0.9, lineJoin:'round', lineCap:'round' }).addTo(_routeGroup);

        pts.forEach((p, i) => {
            const lat = Number(p.lat), lng = Number(p.lng);
            const isFirst = i === 0, isLast = i === n - 1;
            const visit   = p.visit || null;

            let bg, border, txtColor, size, label;
            if (isFirst)    { bg='#16a34a'; border='#fff'; txtColor='#fff'; size=28; label='▶'; }
            else if (isLast){ bg='#dc2626'; border='#fff'; txtColor='#fff'; size=28; label='■'; }
            else if (visit) { bg='#f59e0b'; border='#fff'; txtColor='#fff'; size=26; label='🏪'; }
            else            { bg='#fff';    border='#4f46e5'; txtColor='#4f46e5'; size=22; label=String(i+1); }

            const icon = L.divIcon({
                html: '<div style="width:' + size + 'px;height:' + size + 'px;border-radius:50%;background:' + bg + ';border:2.5px solid ' + border + ';color:' + txtColor + ';font-size:' + (size<26?9:11) + 'px;font-weight:700;font-family:Arial,sans-serif;display:flex;align-items:center;justify-content:center;box-shadow:0 2px 8px rgba(0,0,0,.35);cursor:pointer;">' + label + '</div>',
                className:'', iconSize:[size,size], iconAnchor:[size/2,size/2], popupAnchor:[0,-size/2-2]
            });

            const visitHtml = visit
                ? buildVisitPopup(visit)
                : '';

            const stopLabel = isFirst ? 'Xuất phát' : isLast ? 'Kết thúc'
                            : visit ? ('Điểm ' + (i+1) + ' — Khách hàng') : ('Điểm ' + (i+1));

            const popHtml = visit
                ? visitHtml  // dùng popup đầy đủ cho outlet
                : '<div style="font-family:sans-serif;min-width:160px">' +
                  '<div style="font-weight:700;font-size:.85rem;color:#1e293b;border-bottom:2px solid ' + bg + ';padding-bottom:3px;margin-bottom:5px">' + stopLabel + '</div>' +
                  '<div style="font-size:.76rem;color:#475569;line-height:1.8"><div>⏰ ' + (p.checktime||'—') + '</div>' +
                  '<div style="color:#cbd5e1;font-size:.65rem;margin-top:4px">📍 ' + lat.toFixed(6) + ', ' + lng.toFixed(6) + '</div></div></div>';

            L.marker([lat,lng], { icon }).bindPopup(popHtml, { maxWidth:260 }).addTo(_routeGroup);
        });

        try { _map.fitBounds(latLngs, { padding:[60,60], maxZoom:15 }); } catch(e) {}
        return { stops: n, km: Math.round(totalKm*10)/10 };
    }

    function clearRoute() { if (_routeGroup) _routeGroup.clearLayers(); }

    function flyTo(lat, lng) {
        if (!_map) return;
        lat = Number(lat); lng = Number(lng);
        _map.setView([lat,lng], 15, { animate:true, duration:0.5 });
        let closest=null, minDist=Infinity;
        _markers.forEach(m => {
            const ll=m.marker.getLatLng();
            const d=Math.hypot(ll.lat-lat, ll.lng-lng);
            if (d<minDist) { minDist=d; closest=m.marker; }
        });
        if (closest) setTimeout(() => closest.openPopup(), 400);
    }

    function invalidateSize() { if (_map) _map.invalidateSize(); }

    function fitBoundsToPoints(pointsJson) {
        if (!_map) return;
        let pts;
        try { pts = JSON.parse(pointsJson); } catch(e) { return; }
        if (!pts || pts.length===0) return;
        if (pts.length===1) {
            _map.setView([pts[0][0],pts[0][1]], 14, { animate:true });
            let closest=null, minD=Infinity;
            _markers.forEach(m => {
                const ll=m.marker.getLatLng();
                const d=Math.hypot(ll.lat-pts[0][0], ll.lng-pts[0][1]);
                if (d<minD) { minD=d; closest=m.marker; }
            });
            if (closest) setTimeout(()=>closest.openPopup(), 450);
        } else {
            _map.fitBounds(pts, { padding:[60,60], maxZoom:12, animate:true });
        }
    }

    function renderTerritories(polygonPoints, kpiMap) {
        // Territories disabled
    }

    // ── Outlet markers ────────────────────────────────
    function renderOutlets(outlets) {
        // Outlets disabled
    }

    // ── Layer toggle ──────────────────────────────────
    function setLayerVisible(layer, visible) {
        if (!_map) return;
        const layerMap = {
            territory : _territoryGroup,
            outlet    : _outletGroup,

            compare   : _compareGroup
        };
        const grp = layerMap[layer];
        if (!grp) return;
        if (visible) { if (!_map.hasLayer(grp)) _map.addLayer(grp); }
        else         { if (_map.hasLayer(grp))  _map.removeLayer(grp); }
    }

    // ── Compare markers ───────────────────────────────
    function renderCompare(jsonStr) {
        if (!_map || !_compareGroup) return;
        _compareGroup.clearLayers();

        let locs;
        try { locs = JSON.parse(jsonStr); } catch(e) { return; }
        if (!Array.isArray(locs) || locs.length === 0) return;

        locs.forEach(loc => {
            const lat = Number(loc.lat), lng = Number(loc.lng);
            if (!isFinite(lat) || !isFinite(lng) || (lat===0 && lng===0)) return;

            const name     = String(loc.userName || 'N/A');
            const initials = name.replace(/[^A-Z0-9]/gi, '').slice(-2).toUpperCase() || 'NA';
            const timeStr  = loc.checktime || '—';

            L.marker([lat,lng], { icon: makeDiamondIcon(initials) })
                .bindPopup(
                    '<div style="font-family:sans-serif;min-width:180px;font-size:.8rem">' +
                    '<div style="font-weight:700;color:#475569;margin-bottom:4px">◆ ' + name + ' <span style="font-weight:400">(ngày 2)</span></div>' +
                    '<div>⏰ ' + timeStr + '</div></div>', { maxWidth:220 })
                .addTo(_compareGroup);
        });
        console.log('[tmap] compare rendered', locs.length, 'markers');
    }

    function clearCompare() { if (_compareGroup) _compareGroup.clearLayers(); }

    // ── Filter markers ────────────────────────────────
    function filterMarkers(usernameListJson) {
        if (!usernameListJson) {
            // null/undefined = show all
            _markers.forEach(m => { if (!_markerGroup.hasLayer(m.marker)) _markerGroup.addLayer(m.marker); });
            return;
        }
        let names;
        try { names = JSON.parse(usernameListJson); } catch(e) { return; }

        const showAll = !names || names.length === 0;
        const set = new Set((names || []).map(n => String(n).toLowerCase()));

        _markers.forEach(m => {
            const inGroup = showAll || set.has(m.userName.toLowerCase());
            if (inGroup) {
                if (!_markerGroup.hasLayer(m.marker)) _markerGroup.addLayer(m.marker);
            } else {
                if (_markerGroup.hasLayer(m.marker)) _markerGroup.removeLayer(m.marker);
            }
        });
    }

    // ── Live replay ───────────────────────────────────
    function startReplay(jsonStr, speedMs, dotNetRef) {
        stopReplay();

        let pts;
        try { pts = JSON.parse(jsonStr); } catch(e) { return; }
        if (!Array.isArray(pts) || pts.length === 0) return;

        _replayPts    = pts;
        _replayIdx    = 0;
        _replaySpeed  = speedMs || 500;
        _replayPaused = false;
        _replayDotNet = dotNetRef;

        const motoIcon = L.divIcon({
            html: '<div style="font-size:1.6rem;line-height:1;filter:drop-shadow(0 2px 4px rgba(0,0,0,.4))">🏍️</div>',
            className: '', iconSize: [32,32], iconAnchor: [16,16]
        });

        const first = pts[0];
        _replayMarker = L.marker([Number(first.lat), Number(first.lng)],
            { icon: motoIcon, zIndexOffset: 1000 }).addTo(_map);

        let _replayPopup = null;

        function step() {
            if (_replayPaused) return;
            if (_replayIdx >= _replayPts.length) {
                stopReplay();
                if (_replayDotNet)
                    _replayDotNet.invokeMethodAsync('OnReplayFinished').catch(()=>{});
                return;
            }
            const pt  = _replayPts[_replayIdx];
            const lat = Number(pt.lat);
            const lng = Number(pt.lng);

            _replayMarker.setLatLng([lat, lng]);
            _map.panTo([lat, lng], { animate: true, duration: 0.3 });

            // Đóng popup cũ
            if (_replayPopup) { _replayPopup.remove(); _replayPopup = null; }

            // Mở popup nếu điểm có thông tin outlet (visit)
            if (pt.visit) {
                const v = pt.visit;
                _replayPopup = L.popup({ closeButton:false, autoClose:false, closeOnClick:false, offset:[0,-20] })
                    .setLatLng([lat, lng])
                    .setContent(buildVisitPopup(v))
                    .openOn(_map);

            }

            _replayIdx++;
            _replayTimer = setTimeout(step, _replaySpeed);
        }

        step();
    }

    function stopReplay() {
        if (_replayTimer) { clearTimeout(_replayTimer); _replayTimer = null; }
        if (_replayMarker && _map) { _map.removeLayer(_replayMarker); _replayMarker = null; }
        if (_map) _map.closePopup();
        _replayIdx = 0;
        _replayPaused = false;
    }

    function pauseReplay() {
        _replayPaused = !_replayPaused;
        if (!_replayPaused && _replayTimer === null && _replayMarker) {
            function step() {
                if (_replayPaused) return;
                if (_replayIdx >= _replayPts.length) { stopReplay(); return; }
                const pt = _replayPts[_replayIdx];
                _replayMarker.setLatLng([Number(pt.lat), Number(pt.lng)]);
                _replayIdx++;
                _replayTimer = setTimeout(step, _replaySpeed);
            }
            step();
        }
    }

    // ── CSV download ──────────────────────────────────
    function downloadCsv(filename, csvContent) {
        const blob = new Blob(['﻿' + csvContent], { type: 'text/csv;charset=utf-8;' });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }

    // ── Utilities ─────────────────────────────────────
    function formatMoney(amount) {
        if (!amount && amount !== 0) return '—';
        return Number(amount).toLocaleString('vi-VN');
    }

    function haversineKm(lat1, lon1, lat2, lon2) {
        const R=6371, toRad=x=>x*Math.PI/180;
        const dLat=toRad(lat2-lat1), dLon=toRad(lon2-lon1);
        const a=Math.sin(dLat/2)**2+Math.cos(toRad(lat1))*Math.cos(toRad(lat2))*Math.sin(dLon/2)**2;
        return R*2*Math.atan2(Math.sqrt(a),Math.sqrt(1-a));
    }

    function destroy() {
        stopReplay();
        if (_map) { _map.remove(); _map = null; }
        _markers=[]; _markerGroup=null; _routeGroup=null;
        _territoryGroup=null; _outletGroup=null; _compareGroup=null;
    }

    return {
        init, setDotNetRef, requestRoute,
        addMarkersJson, startGeocoding,
        drawRoute, clearRoute,
        flyTo, fitBoundsToPoints, invalidateSize,
        renderTerritories, renderOutlets, setLayerVisible,
        renderCompare, clearCompare,
        filterMarkers,
        startReplay, stopReplay, pauseReplay,
        downloadCsv, destroy
    };
})();
