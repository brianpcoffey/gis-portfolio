// stateStore.js
export const appState = {
    features: [],
    savedFeatures: new Map(),
    selectedFeatureId: null,
    compareSet: new Set(),
    collections: [
        { id: 1, name: "Favorites", color: "#dc3545" },
        { id: 2, name: "Research", color: "#0d6efd" },
        { id: 3, name: "To Visit", color: "#198754" }
    ],
    selectedCollectionId: null,
    compareMode: false
};

// Simple pub/sub for state changes
const listeners = [];

export function subscribe(listener) {
    listeners.push(listener);
}

export function notify() {
    listeners.forEach(fn => fn(appState));
}

// State mutation helpers
export function setState(partial) {
    Object.assign(appState, partial);
    notify();
}

export function updateSavedFeatures(features) {
    appState.savedFeatures = new Map(features.map(f => [f.featureId, f]));
    notify();
}

export function updateCollections(collections) {
    appState.collections = collections;
    notify();
}