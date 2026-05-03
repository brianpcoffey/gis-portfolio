// featureService.js
// API layer: all network requests centralized here. Shows toasts on error but DOES NOT mutate UI state.

import { UI } from './uiManager.js';

const FEATURES = window.PortfolioApi.routes.features.query;
const SAVED_FEATURES = window.PortfolioApi.routes.features.saved;

async function safeJson(resp) {
    try { return await resp.json(); } catch { return null; }
}

export const FeatureService = {
    async getFeatures(layerId = 3) {
        try {
            const res = await window.apiFetch(`${FEATURES}?layerId=${encodeURIComponent(layerId)}`);
            if (!res.ok) throw new Error((await safeJson(res))?.error || "Failed to fetch features");
            return await res.json();
        } catch (e) {
            console.error("getFeatures", e);
            UI.showToast("Error", e.message || "Failed to load features", "danger");
            return [];
        }
    },

    async getSavedFeatures() {
        try {
            const res = await window.apiFetch(SAVED_FEATURES);
            if (!res.ok) throw new Error((await safeJson(res))?.error || "Failed to fetch saved features");
            return await res.json();
        } catch (e) {
            console.error("getSavedFeatures", e);
            UI.showToast("Error", e.message || "Failed to load saved features", "danger");
            return [];
        }
    },

    async saveFeature(feature, description = "", collectionId = "") {
        try {
            const payload = {
                LayerId: feature.LayerId || feature.layerId || "3",
                FeatureId: String(feature.FeatureId || feature.featureId),
                Name: feature.displayName || feature.Name || feature.name,
                GeometryJson: feature.GeometryJson || feature.geometryJson,
                Description: description || ""
            };
            if (collectionId) payload.CollectionId = collectionId;

            const res = await window.apiFetch(SAVED_FEATURES, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            if (res.status === 409) {
                const body = await safeJson(res);
                throw new Error(body?.error || "Feature already saved");
            }
            if (!res.ok) throw new Error((await safeJson(res))?.error || "Failed to save feature");
            return await res.json();
        } catch (e) {
            console.error("saveFeature", e);
            UI.showToast("Error", e.message || "Failed to save feature", "danger");
            throw e;
        }
    },

    async deleteFeature(id) {
        try {
            const res = await window.apiFetch(`${SAVED_FEATURES}/${encodeURIComponent(id)}`, {
                method: "DELETE"
            });
            if (!res.ok && res.status !== 404) throw new Error((await safeJson(res))?.error || "Failed to delete feature");
            return;
        } catch (e) {
            console.error("deleteFeature", e);
            UI.showToast("Error", e.message || "Failed to delete feature", "danger");
            throw e;
        }
    }
};