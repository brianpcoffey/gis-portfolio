// stateStore.js
// Centralized application state with subscribe/notify and safe update helpers.

export const MAX_COMPARE = 4; // maximum features allowed in compare set

const initialState = {
    features: [],
    savedFeatures: new Map(), // Map<featureId, feature>
    selectedFeatureId: null,
    compareSet: new Set(), // Set<featureId>
    collections: [
        { id: 1, name: "Favorites", color: "#dc3545" },
        { id: 2, name: "Research", color: "#0d6efd" },
        { id: 3, name: "To Visit", color: "#198754" }
    ],
    selectedCollectionId: null,
    compareMode: false
};

export const appState = { ...initialState };

// Subscribers
const listeners = new Set();

export function subscribe(fn) {
    listeners.add(fn);
    return () => listeners.delete(fn);
}

function notify() {
    listeners.forEach(fn => {
        try { fn(getState()); } catch (e) { console.error("Subscriber error", e); }
    });
}

// Utilities
export function getState() {
    // Return shallow snapshot to discourage direct mutation
    return {
        ...appState,
        savedFeatures: new Map(appState.savedFeatures),
        compareSet: new Set(appState.compareSet),
        features: Array.from(appState.features),
        collections: Array.from(appState.collections)
    };
}

export function setState(partial) {
    Object.keys(partial).forEach(k => {
        // Avoid replacing Map/Set with plain objects unintentionally
        if (k === "savedFeatures" && partial[k] instanceof Map) {
            appState.savedFeatures = new Map(partial[k]);
        } else if (k === "compareSet" && partial[k] instanceof Set) {
            appState.compareSet = new Set(partial[k]);
        } else {
            appState[k] = partial[k];
        }
    });
    notify();
}

// Helpers for common updates
export function updateFeatures(featuresArray) {
    appState.features = Array.isArray(featuresArray) ? featuresArray : [];
    notify();
}

export function updateSavedFeatures(featuresArray) {
    const map = new Map();
    (featuresArray || []).forEach(f => {
        map.set(String(f.featureId || f.FeatureId), f);
    });
    appState.savedFeatures = map;

    // Update feature list saved flags to match the new savedFeatures map
    const savedKeys = new Set(Array.from(map.keys()));
    appState.features = appState.features.map(f => {
        const key = String(f.featureId || f.FeatureId || "");
        if (savedKeys.has(key)) {
            const saved = map.get(key) || {};
            return { ...f, isSaved: true, savedDbId: String(saved.id || saved.Id || "") };
        } else {
            // remove saved markers if present
            const clone = { ...f };
            delete clone.isSaved;
            delete clone.savedDbId;
            return clone;
        }
    });

    notify();
}

export function addSavedFeature(feature) {
    const key = String(feature.featureId || feature.FeatureId);
    const map = new Map(appState.savedFeatures);
    map.set(key, feature);
    appState.savedFeatures = map;

    // If this feature exists in the features list, mark it as saved and store DB id
    appState.features = appState.features.map(f => {
        const fid = String(f.featureId || f.FeatureId || "");
        if (fid === key) {
            return { ...f, isSaved: true, savedDbId: String(feature.id || feature.Id || "") };
        }
        return f;
    });

    notify();
}

export function removeSavedFeatureById(featureId) {
    const id = String(featureId);
    if (!appState.savedFeatures.has(id)) return;
    const map = new Map(appState.savedFeatures);
    map.delete(id);
    appState.savedFeatures = map;

    // Clear saved marker on matching feature entries
    appState.features = appState.features.map(f => {
        const fid = String(f.featureId || f.FeatureId || "");
        if (fid === id) {
            const clone = { ...f };
            delete clone.isSaved;
            delete clone.savedDbId;
            return clone;
        }
        return f;
    });

    notify();
}

export function setSelectedFeature(featureId) {
    appState.selectedFeatureId = featureId == null ? null : String(featureId);
    notify();
}

export function toggleCompareFeature(featureId) {
    const id = String(featureId);
    const set = new Set(appState.compareSet);
    if (set.has(id)) set.delete(id);
    else set.add(id);
    appState.compareSet = set;
    notify();
}

export function clearSelectionAndCompare() {
    appState.selectedFeatureId = null;
    appState.compareSet = new Set();
    appState.compareMode = false;
    notify();
}

export function setCompareMode(enabled) {
    appState.compareMode = Boolean(enabled);
    notify();
}

export function updateCollections(collections) {
    appState.collections = Array.isArray(collections) ? collections : [];
    notify();
}

export function setSelectedCollection(collectionId) {
    appState.selectedCollectionId = collectionId == null ? null : String(collectionId);
    notify();
}