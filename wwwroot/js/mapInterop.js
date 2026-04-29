// ══════════════════════════════════════════════════════
//  Leaflet Map Interop — Blazor Server
// ══════════════════════════════════════════════════════

window.mapInterop = (() => {
    let _map         = null;
    let _markers     = [];        // { marker, userName, lat, lng }
    let _markerGroup = null;
    let _routeGroup  = null;      // layer group chứa polyline + stop markers
    let _dotNetRef   = null;      // DotNetObjectReference dùng chung

    const COLORS = [
        '#2563eb','#dc2626','#16a34a','#d97706','#7c3aed',
        '#0891b2','#be185d','#15803d','#b45309','#4f46e5'
    ];
    const colorFor = i => COLORS[i % COLORS.length];

    // ── SVG pin icon ─────────────────────────────────────
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

    // ── Format địa chỉ từ Nominatim address object ───────
    function formatAddress(addr) {
        if (!addr) return null;

        // Số nhà + đường/hẻm/ngõ
        let road = '';
        if (addr.house_number && addr.road)
            road = addr.house_number + ' ' + addr.road;
        else if (addr.road)
            road = addr.road;
        else if (addr.hamlet || addr.neighbourhood || addr.quarter)
            road = addr.hamlet || addr.neighbourhood || addr.quarter;

        // Hẻm / ngõ phụ (nếu có)
        const alley = addr.alley || addr.lane || '';

        // Phường / xã
        const ward = addr.quarter || addr.suburb || addr.village || addr.hamlet || '';

        // Quận / huyện
        const district = addr.city_district || addr.district || addr.county || '';

        // Thành phố / tỉnh
        const city = addr.city || addr.town || addr.municipality || addr.state || '';

        const parts = [alley, road, ward, district, city].filter(Boolean);
        return parts.join(', ');
    }

    // ── Tạo nội dung popup ───────────────────────────────
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

        // Escape userName để dùng trong onclick attribute
        const safeUser = userName.replace(/'/g, "\\'");

        // Doanh số tổng nếu có
        const salesHtml = (loc && loc.totalSales && Number(loc.totalSales) > 0)
            ? '<div style="margin-top:4px;font-weight:700;color:#16a34a;font-size:.8rem">' +
              '💰 ' + formatMoney(Number(loc.totalSales)) + '</div>'
            : '';

        const routeBtn =
            '<div style="margin-top:8px;padding-top:8px;border-top:1px solid #f1f5f9">' +
            '<button onclick="mapInterop.requestRoute(\'' + safeUser + '\')" ' +
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

    // ── Khởi tạo bản đồ ──────────────────────────────────
    function init(elementId, centerLat, centerLng, zoom) {
        if (_map) { _map.remove(); _map = null; }
        _markers = []; _markerGroup = null;

        const el = document.getElementById(elementId);
        if (!el) { console.error('[map] element not found:', elementId); return; }

        // Đảm bảo kích thước absolute
        el.style.cssText = 'position:absolute;top:0;left:0;right:0;bottom:0;';

        _map = L.map(elementId, { zoomControl: true, preferCanvas: true })
                .setView([centerLat, centerLng], zoom);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(_map);

        _markerGroup = L.layerGroup().addTo(_map);
        _routeGroup  = L.layerGroup().addTo(_map);
        setTimeout(() => { if (_map) _map.invalidateSize(); }, 150);
        console.log('[map] init OK', el.offsetWidth + 'x' + el.offsetHeight);
    }

    // ── Đăng ký DotNetRef để popup button có thể gọi Blazor ─
    function setDotNetRef(ref) {
        _dotNetRef = ref;
    }

    // ── Gọi từ popup button: yêu cầu Blazor vẽ lộ trình ─
    function requestRoute(userName) {
        if (!_dotNetRef) { console.warn('[map] dotNetRef not set'); return; }
        // Đóng popup trước
        _map && _map.closePopup();
        _dotNetRef.invokeMethodAsync('ShowRouteByNameAsync', userName)
            .catch(e => console.error('[map] requestRoute error', e));
    }

    // ── Thêm markers từ JSON string ───────────────────────
    function addMarkersJson(jsonStr) {
        if (!_map || !_markerGroup) { console.error('[map] not initialised'); return; }
        _markerGroup.clearLayers();
        _markers = [];

        let locs;
        try { locs = JSON.parse(jsonStr); } catch(e) { console.error('[map] JSON error', e); return; }
        if (!Array.isArray(locs) || locs.length === 0) { console.warn('[map] no data'); return; }

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
        console.log('[map] rendered', _markers.length, 'markers');
    }

    // ── Reverse geocoding — Nominatim OSM ────────────────
    // Lấy địa chỉ chi tiết: số nhà, tên đường, hẻm, phường, quận, thành phố
    async function startGeocoding(jsonStr, dotNetRef) {
        let locs;
        try { locs = JSON.parse(jsonStr); } catch(e) { return; }
        if (!Array.isArray(locs) || locs.length === 0) return;

        for (let i = 0; i < locs.length; i++) {
            const loc = locs[i];
            const lat = Number(loc.lat), lng = Number(loc.lng);
            if (!isFinite(lat) || !isFinite(lng)) continue;

            try {
                // Nominatim — trả về địa chỉ chi tiết nhất (số nhà, đường, hẻm...)
                const url = 'https://nominatim.openstreetmap.org/reverse'
                    + '?lat=' + lat + '&lon=' + lng
                    + '&format=json&accept-language=vi&addressdetails=1';

                const resp = await fetch(url, {
                    headers: { 'Accept': 'application/json' }
                });
                const data = await resp.json();
                const a    = data.address || {};

                // ── Tách từng thành phần địa chỉ ──
                // Số nhà + đường / hẻm / ngõ
                let roadLine = '';
                if (a.house_number) roadLine += a.house_number + ' ';
                roadLine += a.road || a.pedestrian || a.footway ||
                            a.path || a.alley || a.lane || '';

                // Hẻm / ngõ phụ (nếu có và khác đường chính)
                const alley = (a.alley || a.lane || a.hamlet)
                    && (a.alley || a.lane || a.hamlet) !== (a.road || '')
                    ? (a.alley || a.lane || a.hamlet) : '';

                // Phường / xã / khu phố
                const ward = a.quarter || a.suburb || a.neighbourhood ||
                             a.village || a.hamlet || '';

                // Quận / huyện
                const district = a.city_district || a.district ||
                                 a.county || a.borough || '';

                // Thành phố / tỉnh
                const city = a.city || a.town || a.municipality ||
                             a.state_district || a.state || '';

                // Địa chỉ ngắn cho sidebar
                const sidebarAddr = [alley || roadLine, ward, district, city]
                    .filter(Boolean).join(', ');

                // Object đầy đủ cho popup
                const addrObj = {
                    formatted: true,
                    road    : (alley ? alley + ' · ' : '') + (roadLine || ''),
                    ward,
                    district,
                    city
                };

                // Cập nhật popup marker
                const entry = _markers.find(m => m.userName === loc.userName);
                if (entry) {
                    entry.addrObj = addrObj;
                    entry.marker.getPopup()
                        ?.setContent(makePopup(
                            entry.color, entry.userName,
                            entry.timeStr, entry.lat, entry.lng, addrObj, entry.loc));
                }

                // Callback Blazor → cập nhật sidebar
                if (dotNetRef) {
                    await dotNetRef.invokeMethodAsync(
                        'UpdateAddressAsync', loc.userName, sidebarAddr);
                }

            } catch(e) {
                console.warn('[geocode] error for', loc.userName, e);
            }

            // Nominatim policy: tối đa 1 req/giây
            await new Promise(r => setTimeout(r, 1100));
        }
        console.log('[map] geocoding complete');
    }

    // ── Vẽ lộ trình di chuyển ────────────────────────────
    function drawRoute(jsonStr) {
        if (!_map || !_routeGroup) return;
        _routeGroup.clearLayers();

        let pts;
        try { pts = JSON.parse(jsonStr); } catch(e) { return; }
        if (!Array.isArray(pts) || pts.length === 0) return;

        const latLngs = pts.map(p => [Number(p.lat), Number(p.lng)]);
        const n = latLngs.length;

        // ── Tính tổng quãng đường (Haversine) ──
        let totalKm = 0;
        for (let i = 1; i < n; i++) {
            totalKm += haversineKm(latLngs[i-1][0], latLngs[i-1][1],
                                   latLngs[i][0],   latLngs[i][1]);
        }

        // ── Vẽ polyline: shadow + line chính ──
        L.polyline(latLngs, {
            color: '#94a3b8', weight: 6, opacity: 0.4,
            lineJoin: 'round', lineCap: 'round'
        }).addTo(_routeGroup);

        L.polyline(latLngs, {
            color: '#4f46e5', weight: 3, opacity: 0.9,
            dashArray: null,
            lineJoin: 'round', lineCap: 'round'
        }).addTo(_routeGroup);

        // ── Vẽ các điểm dừng ──
        pts.forEach((p, i) => {
            const lat     = Number(p.lat), lng = Number(p.lng);
            const isFirst = i === 0, isLast = i === n - 1;
            const stopNum = i + 1;
            const visit   = p.visit || null;   // thông tin KH nếu tọa độ khớp

            // Màu: Start=xanh lá, End=đỏ, KH=cam, GPS thường=tím
            let bg, border, txtColor, size, label;
            if (isFirst)       { bg='#16a34a'; border='#fff'; txtColor='#fff'; size=28; label='▶'; }
            else if (isLast)   { bg='#dc2626'; border='#fff'; txtColor='#fff'; size=28; label='■'; }
            else if (visit)    { bg='#f59e0b'; border='#fff'; txtColor='#fff'; size=26; label='🏪'; }
            else               { bg='#fff';    border='#4f46e5'; txtColor='#4f46e5'; size=22; label=String(stopNum); }

            const icon = L.divIcon({
                html: `<div style="width:${size}px;height:${size}px;border-radius:50%;
                    background:${bg};border:2.5px solid ${border};color:${txtColor};
                    font-size:${size<26?9:11}px;font-weight:700;font-family:Arial,sans-serif;
                    display:flex;align-items:center;justify-content:center;
                    box-shadow:0 2px 8px rgba(0,0,0,.35);cursor:pointer;">${label}</div>`,
                className:'', iconSize:[size,size],
                iconAnchor:[size/2,size/2], popupAnchor:[0,-size/2-2]
            });

            const timeStr  = p.checktime || '—';
            const stopLabel = isFirst ? 'Xuất phát' : isLast ? 'Kết thúc'
                            : visit ? `Điểm ${stopNum} — Khách hàng` : `Điểm ${stopNum}`;

            // Popup: thêm block thông tin KH nếu có
            const visitHtml = visit ? `
                <div style="margin-top:6px;padding-top:6px;border-top:1px dashed #f59e0b">
                  <div style="font-weight:700;color:#92400e;font-size:.8rem">🏪 ${visit.locationName || visit.customerCD}</div>
                  <div style="font-size:.75rem;color:#78350f;line-height:1.75">
                    <div>📋 Mã KH: <b>${visit.customerCD}</b></div>
                    <div>🗺 Tuyến: ${visit.routeCode || '—'}</div>
                    <div>💰 Doanh số: <b style="color:#16a34a">${formatMoney(visit.orderAmount)}</b></div>
                    <div style="color:#a3a3a3;font-size:.68rem">🕐 ${visit.orderDate}</div>
                  </div>
                </div>` : '';

            const popHtml =
                `<div style="font-family:sans-serif;min-width:180px">
                  <div style="font-weight:700;font-size:.85rem;color:#1e293b;
                              border-bottom:2px solid ${bg};padding-bottom:3px;margin-bottom:5px">
                    ${stopLabel}
                  </div>
                  <div style="font-size:.76rem;color:#475569;line-height:1.8">
                    <div>⏰ ${timeStr}</div>
                    ${visitHtml}
                    <div style="color:#cbd5e1;font-size:.65rem;margin-top:4px">
                      📍 ${lat.toFixed(6)}, ${lng.toFixed(6)}
                    </div>
                  </div>
                </div>`;

            L.marker([lat, lng], { icon })
                .bindPopup(popHtml, { maxWidth: 260 })
                .addTo(_routeGroup);
        });

        // Fit bounds quanh lộ trình
        try { _map.fitBounds(latLngs, { padding: [60, 60], maxZoom: 15 }); } catch(e) {}

        console.log(`[route] ${n} stops, ~${totalKm.toFixed(1)} km`);
        return { stops: n, km: Math.round(totalKm * 10) / 10 };
    }

    // ── Xoá lộ trình ─────────────────────────────────────
    function clearRoute() {
        if (_routeGroup) _routeGroup.clearLayers();
    }

    // ── Format tiền VND — số đầy đủ, không làm tròn ──────
    function formatMoney(amount) {
        if (!amount && amount !== 0) return '—';
        return Number(amount).toLocaleString('vi-VN');
    }

    // ── Haversine distance (km) ───────────────────────────
    function haversineKm(lat1, lon1, lat2, lon2) {
        const R = 6371, toRad = x => x * Math.PI / 180;
        const dLat = toRad(lat2 - lat1), dLon = toRad(lon2 - lon1);
        const a = Math.sin(dLat/2)**2
                + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon/2)**2;
        return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    }

    // ── Bay đến marker ────────────────────────────────────
    function flyTo(lat, lng) {
        if (!_map) return;
        lat = Number(lat); lng = Number(lng);
        _map.setView([lat, lng], 15, { animate: true, duration: 0.5 });

        let closest = null, minDist = Infinity;
        _markers.forEach(m => {
            const ll = m.marker.getLatLng();
            const d  = Math.hypot(ll.lat - lat, ll.lng - lng);
            if (d < minDist) { minDist = d; closest = m.marker; }
        });
        if (closest) setTimeout(() => closest.openPopup(), 400);
    }

    function invalidateSize() { if (_map) _map.invalidateSize(); }

    // ── Fit map đến tập hợp toạ độ [[lat,lng],...] ───────
    function fitBoundsToPoints(pointsJson) {
        if (!_map) return;
        let pts;
        try { pts = JSON.parse(pointsJson); } catch(e) { return; }
        if (!pts || pts.length === 0) return;

        if (pts.length === 1) {
            _map.setView([pts[0][0], pts[0][1]], 14, { animate: true });
            // Tìm và mở popup marker gần nhất
            let closest = null, minD = Infinity;
            _markers.forEach(m => {
                const ll = m.marker.getLatLng();
                const d  = Math.hypot(ll.lat - pts[0][0], ll.lng - pts[0][1]);
                if (d < minD) { minD = d; closest = m.marker; }
            });
            if (closest) setTimeout(() => closest.openPopup(), 450);
        } else {
            _map.fitBounds(pts, { padding: [60, 60], maxZoom: 12, animate: true });
        }
    }

    function destroy() {
        if (_map) { _map.remove(); _map = null; }
        _markers = []; _markerGroup = null; _routeGroup = null;
    }

    return { init, setDotNetRef, addMarkersJson, startGeocoding, drawRoute, clearRoute, requestRoute, flyTo, fitBoundsToPoints, invalidateSize, destroy };
})();
