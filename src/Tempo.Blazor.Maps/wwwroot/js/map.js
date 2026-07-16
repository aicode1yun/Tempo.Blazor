// Tempo.Blazor.Maps — Leaflet interop ES module.
// Leaflet 1.9.4 + leaflet.markercluster 1.5.3 are bundled with the package
// (no CDN dependency) and lazily loaded from _content/Tempo.Blazor.Maps/.

const instances = {}; // mapId -> { map, renderer, markerColor, markers, clusterGroups, clusterSeq, dataLayers, dotNetRef }

let leafletLoadPromise = null;

function assetUrl(relative) {
    return new URL(relative, import.meta.url).href;
}

function loadScript(src) {
    return new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[src="${src}"]`);
        if (existing) {
            if (existing.dataset.tmMapsLoaded === "true") {
                resolve();
                return;
            }
            existing.addEventListener("load", () => resolve());
            existing.addEventListener("error", () => reject(new Error(`Failed to load ${src}`)));
            return;
        }
        const script = document.createElement("script");
        script.src = src;
        script.addEventListener("load", () => {
            script.dataset.tmMapsLoaded = "true";
            resolve();
        });
        script.addEventListener("error", () => reject(new Error(`Failed to load ${src}`)));
        document.head.appendChild(script);
    });
}

function ensureStylesheet(href) {
    if (document.querySelector(`link[href="${href}"]`)) return;
    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = href;
    document.head.appendChild(link);
}

function ensureLeaflet() {
    if (window.L && window.L.markerClusterGroup) return Promise.resolve();
    if (!leafletLoadPromise) {
        leafletLoadPromise = (async () => {
            ensureStylesheet(assetUrl("../css/leaflet/leaflet.css"));
            ensureStylesheet(assetUrl("../css/leaflet/MarkerCluster.css"));
            ensureStylesheet(assetUrl("../css/leaflet/MarkerCluster.Default.css"));
            if (!window.L) {
                await loadScript(assetUrl("./leaflet/leaflet.js"));
            }
            if (!window.L.markerClusterGroup) {
                await loadScript(assetUrl("./leaflet/leaflet.markercluster.js"));
            }
        })();
    }
    return leafletLoadPromise;
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

function stopEvent(e) {
    if (e.originalEvent) {
        e.originalEvent.stopPropagation();
        e.originalEvent.preventDefault();
        e.originalEvent.stopImmediatePropagation();
    }
    L.DomEvent.stopPropagation(e);
    L.DomEvent.preventDefault(e);
}

// Canvas-rendered circle marker for an individual point (never a DOM marker).
function createCircleMarker(instance, markerData) {
    const marker = L.circleMarker([markerData.latitude, markerData.longitude], {
        renderer: instance.renderer,
        radius: 8,
        weight: 1.5,
        color: instance.markerColor,
        fillColor: instance.markerColor,
        fillOpacity: 0.8,
        fill: true,
        interactive: true
    });

    if (markerData.title) {
        marker.bindTooltip(markerData.title, { permanent: false, offset: [0, -10] });
    }

    marker._tmId = markerData.id ?? null;

    if (instance.dotNetRef) {
        marker.on("click", e => {
            stopEvent(e);
            const latlng = marker.getLatLng();
            instance.dotNetRef.invokeMethodAsync("HandleMarkerClick", marker._tmId, latlng.lat, latlng.lng)
                .catch(() => { /* circuit may be gone */ });
        });
    }

    return marker;
}

// Server-side aggregated cluster bubble (L.divIcon with the culture-formatted count).
function createClusterBubble(instance, cluster) {
    const icon = L.divIcon({
        html: `<div class="tm-map__cluster" role="img" aria-label="${escapeHtml(cluster.ariaLabel ?? "")}">${escapeHtml(cluster.countText ?? String(cluster.count))}</div>`,
        className: "tm-map__cluster-icon",
        iconSize: [34, 34],
        iconAnchor: [17, 17]
    });

    const marker = L.marker([cluster.latitude, cluster.longitude], { icon });

    if (cluster.tooltip) {
        marker.bindTooltip(cluster.tooltip, { permanent: false, offset: [0, -14] });
    }

    if (instance.dotNetRef) {
        marker.on("click", e => {
            stopEvent(e);
            instance.dotNetRef.invokeMethodAsync("HandleClusterClick", cluster.key ?? null, cluster.latitude, cluster.longitude, cluster.count)
                .catch(() => { /* circuit may be gone */ });
        });
    }

    return marker;
}

// A cluster with Count = 1 renders as a plain Canvas marker (click still reports the key).
function createSingleClusterMarker(instance, cluster) {
    const marker = L.circleMarker([cluster.latitude, cluster.longitude], {
        renderer: instance.renderer,
        radius: 8,
        weight: 1.5,
        color: instance.markerColor,
        fillColor: instance.markerColor,
        fillOpacity: 0.8,
        fill: true,
        interactive: true
    });

    if (cluster.tooltip) {
        marker.bindTooltip(cluster.tooltip, { permanent: false, offset: [0, -10] });
    }

    if (instance.dotNetRef) {
        marker.on("click", e => {
            stopEvent(e);
            instance.dotNetRef.invokeMethodAsync("HandleClusterClick", cluster.key ?? null, cluster.latitude, cluster.longitude, cluster.count)
                .catch(() => { /* circuit may be gone */ });
        });
    }

    return marker;
}

function createMarkerClusterGroup(instance) {
    const group = L.markerClusterGroup({
        chunkedLoading: true,
        maxClusterRadius: 50,
        spiderfyOnMaxZoom: true,
        showCoverageOnHover: false,
        zoomToBoundsOnClick: true
    });

    if (instance.dotNetRef) {
        group.on("clusterclick", e => {
            const latlng = e.layer.getLatLng();
            instance.dotNetRef.invokeMethodAsync("HandleClusterClick", null, latlng.lat, latlng.lng, e.layer.getChildCount())
                .catch(() => { /* circuit may be gone */ });
        });
    }

    return group;
}

export async function init(mapId, centerLat, centerLng, zoom, options, dotNetRef) {
    await ensureLeaflet();

    // Re-initialisation of the same mapId disposes the previous instance first.
    if (instances[mapId]) {
        dispose(mapId);
    }

    const element = document.getElementById(mapId);
    if (!element) return false;

    const map = L.map(element, {
        zoomControl: true,
        doubleClickZoom: true,
        scrollWheelZoom: true,
        boxZoom: true,
        keyboard: true,
        dragging: true,
        touchZoom: true,
        zoomSnap: options?.zoomSnap ?? 0.5
    }).setView([centerLat, centerLng], zoom);

    L.tileLayer(options?.tileUrlTemplate ?? "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: options?.attribution ?? "&copy; OpenStreetMap contributors",
        maxZoom: options?.maxZoom ?? 19
    }).addTo(map);

    // Canvas renderer shared by marker batches — padding keeps edge markers visible.
    const renderer = L.canvas({ padding: 0.5 });

    // Canvas drawing cannot be styled by CSS classes, so resolve the design token here.
    const markerColor = getComputedStyle(element).getPropertyValue("--tm-map-marker-color").trim()
        || getComputedStyle(document.documentElement).getPropertyValue("--tm-color-danger").trim();

    const instance = {
        map,
        renderer,
        markerColor,
        markers: new Map(),
        clusterGroups: {},
        clusterSeq: 0,
        dataLayers: [],
        dotNetRef
    };
    instances[mapId] = instance;

    if (dotNetRef) {
        map.on("zoomend", () => {
            const center = map.getCenter();
            dotNetRef.invokeMethodAsync("HandleZoomChanged", map.getZoom(), center.lat, center.lng)
                .catch(() => { /* circuit may be gone */ });
        });
        map.on("moveend", () => {
            const center = map.getCenter();
            dotNetRef.invokeMethodAsync("HandleViewportChanged", map.getZoom(), center.lat, center.lng)
                .catch(() => { /* circuit may be gone */ });
        });
        map.on("click", e => {
            dotNetRef.invokeMethodAsync("HandleMapClick", e.latlng.lat, e.latlng.lng, map.getZoom())
                .catch(() => { /* circuit may be gone */ });
        });
    }

    return true;
}

// Atomically replaces the declarative data set: all new layers are built and added,
// then the old ones are removed, inside a single synchronous pass — the browser never
// paints old and new data together and the map never flashes empty.
export function setData(mapId, data) {
    const instance = instances[mapId];
    if (!instance) return;

    const markers = data?.markers ?? [];
    const clusters = data?.clusters ?? [];
    const newLayers = [];

    if (markers.length > 0 && data?.clientClustering) {
        const group = createMarkerClusterGroup(instance);
        markers.forEach(m => group.addLayer(createCircleMarker(instance, m)));
        newLayers.push(group);
    } else if (markers.length > 0) {
        newLayers.push(L.layerGroup(markers.map(m => createCircleMarker(instance, m))));
    }

    if (clusters.length > 0) {
        newLayers.push(L.layerGroup(clusters.map(c => c.count === 1
            ? createSingleClusterMarker(instance, c)
            : createClusterBubble(instance, c))));
    }

    const oldLayers = instance.dataLayers;
    newLayers.forEach(layer => layer.addTo(instance.map));
    oldLayers.forEach(layer => instance.map.removeLayer(layer));
    instance.dataLayers = newLayers;
}

// Additive batch of Canvas markers (imperative AddMarkersAsync).
export function addMarkersBatch(mapId, markers) {
    const instance = instances[mapId];
    if (!instance || !Array.isArray(markers)) return;

    markers.forEach(markerData => {
        const lat = markerData.latitude;
        const lng = markerData.longitude;
        if (typeof lat !== "number" || typeof lng !== "number") return;

        const marker = createCircleMarker(instance, markerData).addTo(instance.map);
        instance.markers.set(marker._leaflet_id, marker);
    });
}

// Imperative client clustering: creates an empty leaflet.markercluster group.
export function addClusterGroup(mapId) {
    const instance = instances[mapId];
    if (!instance) return null;

    const group = createMarkerClusterGroup(instance);
    instance.map.addLayer(group);
    instance.clusterSeq += 1;
    const clusterId = `cluster_${mapId}_${instance.clusterSeq}`;
    instance.clusterGroups[clusterId] = group;
    return clusterId;
}

// Adds a batch of Canvas markers into an imperative cluster group.
export function addMarkersToCluster(mapId, clusterId, markers) {
    const instance = instances[mapId];
    if (!instance || !Array.isArray(markers)) return;

    const group = instance.clusterGroups[clusterId];
    if (!group) return;

    markers.forEach(markerData => {
        group.addLayer(createCircleMarker(instance, markerData));
    });
}

// Removes every marker, cluster group, and declarative data layer.
export function clearMarkers(mapId) {
    const instance = instances[mapId];
    if (!instance) return;

    instance.markers.forEach(marker => instance.map.removeLayer(marker));
    instance.markers.clear();

    Object.values(instance.clusterGroups).forEach(group => instance.map.removeLayer(group));
    instance.clusterGroups = {};

    instance.dataLayers.forEach(layer => instance.map.removeLayer(layer));
    instance.dataLayers = [];
}

export function setView(mapId, lat, lng, zoom) {
    const instance = instances[mapId];
    if (!instance) return;

    instance.map.setView([lat, lng], zoom, { animate: false });
}

export function getZoom(mapId) {
    const instance = instances[mapId];
    return instance ? instance.map.getZoom() : 0;
}

export function getCenter(mapId) {
    const instance = instances[mapId];
    if (!instance) return { latitude: 0, longitude: 0 };
    const center = instance.map.getCenter();
    return { latitude: center.lat, longitude: center.lng };
}

export function getViewport(mapId) {
    const instance = instances[mapId];
    if (!instance) return { latitude: 0, longitude: 0, zoom: 0 };
    const center = instance.map.getCenter();
    return { latitude: center.lat, longitude: center.lng, zoom: instance.map.getZoom() };
}

// Call after the map container becomes visible again (e.g. hidden tab shown).
export function invalidateSize(mapId) {
    const instance = instances[mapId];
    if (!instance) return;

    instance.map.invalidateSize();
}

export function dispose(mapId) {
    const instance = instances[mapId];
    if (!instance) return;

    instance.markers.clear();
    instance.clusterGroups = {};
    instance.dataLayers = [];
    instance.map.remove();
    delete instances[mapId];
}
