// GIS map logic using Leaflet
let map = L.map('map').setView([51.505, -0.09], 13);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '© OpenStreetMap' }).addTo(map);

let features = [];
let featureMarkers = {};

function loadFeatures() {
    fetch('/api/gis/features')
        .then(res => res.json())
        .then(data => {
            features = data;
            updateMap();
            updateList();
        });
}

function updateMap() {
    Object.values(featureMarkers).forEach(m => map.removeLayer(m));
    featureMarkers = {};
    features.forEach(f => {
        if (f.featureType === "Marker") {
            let coords = JSON.parse(f.coordinates);
            let marker = L.marker([coords.lat, coords.lng]).addTo(map)
                .bindPopup(`<b>${f.name}</b><br>${f.description}`);
            featureMarkers[f.id] = marker;
        }
        // Polygon support can be expanded here
    });
}

function updateList() {
    let html = '<ul class="list-group">';
    features.forEach(f => {
        html += `<li class="list-group-item d-flex justify-content-between align-items-center">
            <span>${f.name} (${f.featureType})</span>
            <div>
                <button class="btn btn-sm btn-secondary" onclick="editFeature(${f.id})">Edit</button>
                <button class="btn btn-sm btn-danger" onclick="deleteFeature(${f.id})">Delete</button>
            </div>
        </li>`;
    });
    html += '</ul>';
    document.getElementById('featuresList').innerHTML = html;
}

document.getElementById('featureForm').onsubmit = function(e) {
    e.preventDefault();
    let id = document.getElementById('featureId').value;
    let payload = {
        name: document.getElementById('featureName').value,
        description: document.getElementById('featureDescription').value,
        featureType: document.getElementById('featureType').value,
        coordinates: document.getElementById('featureCoordinates').value
    };
    let method = id ? 'PUT' : 'POST';
    let url = '/api/gis/features' + (id ? `/${id}` : '');
    fetch(url, {
        method: method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    }).then(() => {
        loadFeatures();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('addFeatureModal')).hide();
    });
};

function editFeature(id) {
    let f = features.find(x => x.id === id);
    document.getElementById('featureId').value = f.id;
    document.getElementById('featureName').value = f.name;
    document.getElementById('featureDescription').value = f.description;
    document.getElementById('featureType').value = f.featureType;
    document.getElementById('featureCoordinates').value = f.coordinates;
    bootstrap.Modal.getOrCreateInstance(document.getElementById('addFeatureModal')).show();
}

function deleteFeature(id) {
    fetch(`/api/gis/features/${id}`, { method: 'DELETE' })
        .then(() => loadFeatures());
}

// Dark/Light mode toggle
const toggleModeBtn = document.getElementById('toggleMode');
toggleModeBtn.addEventListener('click', () => {
    document.body.classList.toggle('dark-mode');
});

loadFeatures();
