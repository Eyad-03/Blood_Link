/* =====================================================================
   BloodLink — Donate page (/Donor/Donate)
   The donor fills in blood type, phone and location, then the offer is saved:
     PUT /api/profile/donor-offer   (Controllers/Api/ProfileController.cs)
   After that the donor is "available" and shows on the /Donor/Search map.

   Location:
     - "Approximate area": only the city is saved (the map shows the city area)
     - "Exact point": GPS ("Use my current location") or a click on the map
   ===================================================================== */
document.addEventListener("DOMContentLoaded", () => {
    const $ = id => document.getElementById(id);
    Api.requireLogin(["Donor"]);

    const form = $("donorForm");
    const gpsButton = $("useLocation");

    /* Same city centres as the server (Helpers/Format.cs → Cities) */
    const CITY_CENTRES = {
        irbid: [32.5556, 35.8500], amman: [31.9539, 35.9106], zarqa: [32.0728, 36.0880],
        aqaba: [29.5321, 35.0063], salt: [32.0392, 35.7272], madaba: [31.7160, 35.7939],
        jerash: [32.2747, 35.8961], ajloun: [32.3326, 35.7517], mafraq: [32.3434, 36.2080],
        karak: [31.1853, 35.7048], tafilah: [30.8375, 35.6044], "ma'an": [30.1962, 35.7341],
        "إربد": [32.5556, 35.8500], "اربد": [32.5556, 35.8500], "عمان": [31.9539, 35.9106],
        "عمّان": [31.9539, 35.9106], "الزرقاء": [32.0728, 36.0880], "العقبة": [29.5321, 35.0063],
        "السلط": [32.0392, 35.7272], "مادبا": [31.7160, 35.7939], "جرش": [32.2747, 35.8961],
        "عجلون": [32.3326, 35.7517], "المفرق": [32.3434, 36.2080], "الكرك": [31.1853, 35.7048],
        "الطفيلة": [30.8375, 35.6044], "معان": [30.1962, 35.7341]
    };
    const DEFAULT_CITY = "Irbid";

    let mode = "approximate";          // "approximate" | "precise"
    let exactPoint = null;             // { lat, lng, accuracy, source: "gps" | "map" }
    let map = null, marker = null, areaCircle = null, accuracyCircle = null;
    let locating = false;

    /* ---------- Helpers ---------- */
    function cityText() {
        return ($("city").value || "").split(",")[0].trim() || DEFAULT_CITY;
    }

    function cityCentre() {
        const key = cityText().toLowerCase();
        return CITY_CENTRES[key] || CITY_CENTRES[cityText()] || null;
    }

    function setMode(next) {
        mode = next;
        const radio = form.querySelector(`input[name="locationMode"][value="${next}"]`);
        if (radio) radio.checked = true;
        form.querySelectorAll(".location-option").forEach(o =>
            o.classList.toggle("selected", o.querySelector("input").checked));
    }

    /* ---------- Status box (replaces alert) ---------- */
    function showStatus(kind, title, text, actions = []) {
        const box = $("locStatus");
        box.hidden = false;
        box.className = "loc-status " + kind;
        $("locStatusTitle").textContent = title;
        $("locStatusText").innerHTML = text;
        const holder = $("locStatusActions");
        holder.innerHTML = "";
        actions.forEach(a => {
            const b = document.createElement("button");
            b.type = "button";
            b.className = a.primary ? "status-btn primary" : "status-btn";
            b.textContent = a.label;
            b.addEventListener("click", a.onClick);
            holder.appendChild(b);
        });
    }

    function hideStatus() { $("locStatus").hidden = true; }

    /* ---------- Map ---------- */
    const pinIcon = L => L.divIcon({
        className: "donate-pin",
        html: '<div class="dp"><span class="dp-pulse"></span><svg viewBox="0 0 44 54" aria-hidden="true">' +
            '<path d="M22 52C22 52 4 33.5 4 21a18 18 0 1 1 36 0C40 33.5 22 52 22 52Z"/>' +
            '<circle cx="22" cy="21" r="8"/></svg></div>',
        iconSize: [44, 54],
        iconAnchor: [22, 52]
    });

    function initMap() {
        if (!$("locationMap") || typeof L === "undefined") {
            showStatus("error", "The map could not load", "Check your internet connection and refresh the page.");
            return;
        }

        const start = cityCentre() || CITY_CENTRES.irbid;
        map = L.map($("locationMap"), { zoomControl: false, scrollWheelZoom: false }).setView(start, 12);
        L.control.zoom({ position: "bottomright" }).addTo(map);
        map.attributionControl.setPrefix(false);
        map.attributionControl.setPosition("bottomleft");

        L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap</a>'
        }).addTo(map);

        marker = L.marker(start, { icon: pinIcon(L), draggable: false, keyboard: false }).addTo(map);

        marker.on("dragend", () => {
            const p = marker.getLatLng();
            exactPoint = { lat: p.lat, lng: p.lng, accuracy: null, source: "map" };
            render();
        });

        // Click on the map = choose an exact point
        map.on("click", e => {
            exactPoint = { lat: e.latlng.lat, lng: e.latlng.lng, accuracy: null, source: "map" };
            setMode("precise");
            hideStatus();
            render(true);
        });

        // Scroll-zoom only after the map is clicked (so the page scrolls normally)
        map.once("focus", () => map.scrollWheelZoom.enable());
        setTimeout(() => map.invalidateSize(), 300);
    }

    /* Draw the pin, circles and label for the current choice */
    function render(pan) {
        const label = $("locationLabel");
        const chip = $("locationChip");
        const hint = $("mapHint");

        if (areaCircle) { map?.removeLayer(areaCircle); areaCircle = null; }
        if (accuracyCircle) { map?.removeLayer(accuracyCircle); accuracyCircle = null; }

        if (mode === "precise" && exactPoint) {
            const ll = [exactPoint.lat, exactPoint.lng];
            const acc = exactPoint.accuracy ? ` · ±${Math.round(exactPoint.accuracy)} m` : "";
            label.textContent = `Exact point${exactPoint.source === "gps" ? " (GPS)" : " (picked on map)"}${acc}`;
            chip.className = "map-chip exact";
            hint.textContent = "Drag the pin or click the map to adjust";

            if (map) {
                marker.setLatLng(ll).setOpacity(1);
                marker.dragging?.enable();
                if (exactPoint.accuracy && exactPoint.accuracy < 2000) {
                    accuracyCircle = L.circle(ll, {
                        radius: exactPoint.accuracy, weight: 1, color: "#2f6bff",
                        fillColor: "#2f6bff", fillOpacity: .1, interactive: false
                    }).addTo(map);
                }
                if (pan) map.flyTo(ll, Math.max(map.getZoom(), 15), { duration: .7 });
            }
        } else if (mode === "precise") {
            const centre = cityCentre() || CITY_CENTRES.irbid;
            label.textContent = "Exact point not set yet";
            chip.className = "map-chip waiting";
            hint.textContent = "Use your current location, or click the map to place the pin";
            if (map) {
                marker.setLatLng(centre).setOpacity(.45);
                marker.dragging?.disable();
                if (pan) map.flyTo(centre, 13, { duration: .7 });
            }
        } else {
            const centre = cityCentre();
            label.textContent = centre
                ? `Approximate area · ${cityText()}`
                : `Approximate area · ${cityText()} (not on the map)`;
            chip.className = "map-chip approx";
            hint.textContent = "Tip: click anywhere on the map to set an exact point";
            if (map && centre) {
                marker.setLatLng(centre).setOpacity(1);
                marker.dragging?.disable();
                areaCircle = L.circle(centre, {
                    radius: 3500, weight: 1.5, dashArray: "5 7", color: "#c91835",
                    fillColor: "#c91835", fillOpacity: .06, interactive: false
                }).addTo(map);
                if (pan) map.flyTo(centre, 12, { duration: .7 });
            }
        }

        updateProgress();
    }

    /* ---------- GPS ---------- */
    function setLocating(on) {
        locating = on;
        gpsButton.disabled = on;
        gpsButton.classList.toggle("loading", on);
        gpsButton.querySelector(".gps-text").textContent = on ? "Finding you…" : "Use my current location";
    }

    const pickOnMap = {
        label: "Pick on the map instead",
        onClick: () => {
            setMode("precise");
            hideStatus();
            render(true);
            $("locationMap").scrollIntoView({ behavior: "smooth", block: "center" });
        }
    };
    const tryAgain = { label: "Try again", primary: true, onClick: () => requestGps() };

    async function permissionState() {
        try {
            if (navigator.permissions?.query) return (await navigator.permissions.query({ name: "geolocation" })).state;
        } catch { /* not supported */ }
        return "unknown";
    }

    async function explainError(error) {
        const state = await permissionState();

        if (error.code === 1 && state === "denied") {
            showStatus("error", "Location is blocked for this site",
                "Click the <b>🔒 / ⓘ icon</b> on the left of the address bar → <b>Location</b> → <b>Allow</b>, then press <b>Try again</b>.",
                [tryAgain, pickOnMap]);
        } else if (error.code === 1 || error.code === 2) {
            showStatus("error", "Your device's location is turned off",
                "On Windows open <b>Settings → Privacy &amp; security → Location</b> and turn on <b>Location services</b> " +
                "and <b>Let desktop apps access your location</b>. You can also simply pick your point on the map.",
                [tryAgain, pickOnMap]);
        } else {
            showStatus("error", "Finding your location took too long",
                "Your connection or GPS signal may be weak. Try again, or pick your point on the map.",
                [tryAgain, pickOnMap]);
        }
    }

    function getPosition(options) {
        return new Promise((resolve, reject) => navigator.geolocation.getCurrentPosition(resolve, reject, options));
    }

    async function requestGps() {
        if (locating) return;

        if (!window.isSecureContext) {
            showStatus("error", "Location needs a secure page",
                "Browsers only share location on <b>https://</b> or <b>localhost</b>. Open the site that way, or pick your point on the map.",
                [pickOnMap]);
            return;
        }
        if (!navigator.geolocation) {
            showStatus("error", "Location is not supported", "This browser can't share its location. Pick your point on the map instead.", [pickOnMap]);
            return;
        }

        setMode("precise");
        render();
        setLocating(true);
        showStatus("info", "Finding your location…", "If your browser asks, choose <b>Allow</b>.");

        try {
            let pos;
            try {
                // Fast first try (Wi-Fi / network location), then a precise one
                pos = await getPosition({ enableHighAccuracy: false, timeout: 10000, maximumAge: 120000 });
            } catch (err) {
                if (err.code === 1) throw err;
                pos = await getPosition({ enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
            }

            const { latitude, longitude, accuracy } = pos.coords;
            exactPoint = { lat: latitude, lng: longitude, accuracy, source: "gps" };
            showStatus("success", "Location found",
                `Accuracy about ±${Math.round(accuracy)} m. Drag the pin if it's not exactly right.`);
            setTimeout(() => { if ($("locStatus").classList.contains("success")) hideStatus(); }, 5000);
            render(true);
        } catch (error) {
            await explainError(error);
            render();
        } finally {
            setLocating(false);
        }
    }

    gpsButton?.addEventListener("click", requestGps);

    /* Switching the radio: "Exact point" starts GPS when no point is set yet */
    form.addEventListener("change", e => {
        if (e.target.name === "locationMode") {
            setMode(e.target.value);
            if (mode === "approximate") hideStatus();
            render(true);
            if (mode === "precise" && !exactPoint) requestGps();
            return;
        }
        updateProgress();
    });

    let cityTimer = null;
    $("city").addEventListener("input", () => {
        clearTimeout(cityTimer);
        cityTimer = setTimeout(() => render(mode === "approximate"), 350);
    });

    form.addEventListener("input", updateProgress);

    /* ---------- Progress + live checklist ---------- */
    function updateProgress() {
        const checks = {
            blood: !!$("bloodType").value,
            phone: $("phone").value.trim().length >= 7,
            availability: !!$("availability").value,
            location: mode === "approximate" ? !!$("city").value.trim() : !!exactPoint,
            consent: $("shareConsent").checked && $("availabilityConsent").checked
        };
        const nameOk = $("fullName").value.trim().length > 1;
        const done = Object.values(checks).filter(Boolean).length;
        const total = Object.keys(checks).length;

        document.querySelectorAll("[data-check]").forEach(el =>
            el.classList.toggle("done", !!checks[el.dataset.check]));

        const fill = $("progressFill");
        if (fill) fill.style.width = `${Math.round(done / total * 100)}%`;

        const stepDone = {
            1: nameOk && checks.blood && checks.phone && checks.availability,
            2: checks.location,
            3: checks.consent
        };
        document.querySelectorAll(".progress-step").forEach(el =>
            el.classList.toggle("done", !!stepDone[el.dataset.step]));

        const text = $("progressText");
        if (text) {
            text.textContent = done === total
                ? "All set. You're ready to submit ♥"
                : `${done} of ${total} done`;
        }
    }

    /* ---------- Fill the form with what we already know ---------- */
    initMap();
    setMode("approximate");
    render();

    Api.get("/api/profile").then(p => {
        if (p.name) $("fullName").value = p.name;
        if (p.bloodType) $("bloodType").value = p.bloodType;
        if (p.phone) $("phone").value = p.phone;
        if (p.location) $("city").value = p.area ? `${p.location}, ${p.area}` : p.location;

        if (p.shareLocation && p.latitude != null && p.longitude != null) {
            exactPoint = { lat: p.latitude, lng: p.longitude, accuracy: null, source: "map" };
            setMode("precise");
        }
        render(true);
    }).catch(() => { });

    /* ---------- Submit ---------- */
    function showFormError(message) {
        const box = $("formError");
        box.textContent = message;
        box.hidden = !message;
    }

    form.addEventListener("submit", async e => {
        e.preventDefault();
        showFormError("");
        if (!form.reportValidity()) return;

        const precise = mode === "precise";
        if (precise && !exactPoint) {
            showStatus("error", "Set your exact point first",
                "Press <b>Use my current location</b> or click the map. Or switch to <b>Approximate area</b>.",
                [tryAgain, pickOnMap]);
            $("locationMap").scrollIntoView({ behavior: "smooth", block: "center" });
            return;
        }

        const offer = {
            fullName: $("fullName").value.trim(),
            bloodType: $("bloodType").value,
            phone: $("phone").value.trim(),
            city: $("city").value.trim(),
            contactMethod: $("contactMethod").value,
            shareExactLocation: precise,
            latitude: precise ? exactPoint.lat : null,
            longitude: precise ? exactPoint.lng : null
        };

        const submit = form.querySelector('button[type="submit"]');
        submit.disabled = true;
        try {
            await Api.put("/api/profile/donor-offer", offer);
        } catch (err) {
            showFormError(err.message);
            submit.disabled = false;
            return;
        }
        submit.disabled = false;

        const where = precise ? "Exact point shared" : `Approximate area · ${escapeHtml(offer.city)}`;
        $("successSummary").innerHTML =
            `<p><b>${escapeHtml(offer.bloodType)}</b> · ${escapeHtml(offer.city)}</p>` +
            `<p>${escapeHtml($("availability").selectedOptions[0].text)}</p><p>📍 ${where}</p>`;
        $("successModal").hidden = false;
    });

    function escapeHtml(v) {
        return String(v).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
    }

    $("closeSuccess")?.addEventListener("click", () => { $("successModal").hidden = true; window.location.href = "/Donor/Requests"; });
    $("cancelButton")?.addEventListener("click", () => { window.location.href = "/"; });
});
