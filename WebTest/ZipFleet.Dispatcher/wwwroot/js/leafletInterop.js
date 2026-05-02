window.leafletInterop = {
    map: null,
    markers: {},
    dotnetRef: null,

    initMap: function(elementId, lat, lng, zoom) {
        if (this.map) { this.map.remove(); this.map = null; }
        var el = document.getElementById(elementId);
        if (!el) return;
        el.style.height = '100%';
        el.style.width = '100%';
        this.map = L.map(elementId, { zoomControl: true }).setView([lat, lng], zoom);
        L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
            maxZoom: 19
        }).addTo(this.map);
        setTimeout(function() { window.leafletInterop.map.invalidateSize(); }, 300);
    },

    enableClickHandler: function(dotnetObjRef) {
        this.dotnetRef = dotnetObjRef;
        if (!this.map) return;
        this.map.on('click', function(e) {
            dotnetObjRef.invokeMethodAsync('OnMapClicked', e.latlng.lat, e.latlng.lng);
        });
    },

    addMarker: function(id, lat, lng, emoji, popupText) {
        if (!this.map) return;
        this.removeMarker(id);
        var icon = L.divIcon({
            html: '<div style="font-size:28px;text-align:center;line-height:1">' + emoji + '</div>',
            iconSize: [30, 30], iconAnchor: [15, 30], className: ''
        });
        var marker = L.marker([lat, lng], { icon: icon }).addTo(this.map);
        if (popupText) marker.bindPopup(popupText);
        this.markers[id] = marker;
    },

    removeMarker: function(id) {
        if (this.markers[id]) {
            this.map.removeLayer(this.markers[id]);
            delete this.markers[id];
        }
    },

    clearMarkers: function() {
        for (var id in this.markers) {
            this.map.removeLayer(this.markers[id]);
        }
        this.markers = {};
    },

    invalidateSize: function() {
        if (this.map) this.map.invalidateSize();
    }
};

window.appInterop = window.appInterop || {};

window.appInterop.getCurrentPosition = function() {
    return new Promise(function(resolve, reject) {
        if (!navigator.geolocation) {
            reject('Geolocation not supported');
            return;
        }
        navigator.geolocation.getCurrentPosition(
            function(pos) {
                resolve({
                    latitude: pos.coords.latitude,
                    longitude: pos.coords.longitude,
                    accuracy: pos.coords.accuracy
                });
            },
            function(err) { reject(err.message); },
            { enableHighAccuracy: true, timeout: 5000 }
        );
    });
};
