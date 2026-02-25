// featureService.js
import { updateSavedFeatures, setState } from './stateStore.js';
import { UI } from './uiManager.js';

export const FeatureService = {
    async getFeatures() {
        try {
            const res = await fetch('/api/features?layerId=3', { credentials: 'same-origin' });
            if (!res.ok) throw new Error("Failed to fetch features");
            return await res.json();
        } catch (e) {
            console.error(e);
            UI.showToast("Error", e.message, "danger");
            return [];
        }
    },
    async getSavedFeatures() {
        try {
            const res = await fetch('/api/savedfeatures', { credentials: 'same-origin' });
            if (!res.ok) throw new Error("Failed to fetch saved features");
            const features = await res.json();
            updateSavedFeatures(features);
            return features;
        } catch (e) {
            console.error(e);
            UI.showToast("Error", e.message, "danger");
            return [];
        }
    },
    async saveFeature(feature, description = "") {
        try {
            const payload = {
                LayerId: feature.LayerId || feature.layerId || "3",
                FeatureId: String(feature.FeatureId || feature.featureId),
                Name: feature.displayName || feature.Name || feature.name,
                GeometryJson: feature.GeometryJson || feature.geometryJson,
                Description: description
            };
            const res = await fetch('/api/savedfeatures', {
                method: "POST",
                credentials: 'same-origin',
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            if (res.status === 409) throw new Error("Feature already saved");
            if (!res.ok) {
                let err = "Failed to save feature";
                try { err = (await res.json())?.error || err; } catch {}
                throw new Error(err);
            }
            return await res.json();
        } catch (e) {
            console.error(e);
            UI.showToast("Error", e.message, "danger");
            throw e;
        }
    },
    async deleteFeature(id) {
        try {
            const res = await fetch(`/api/savedfeatures/${id}`, { method: "DELETE", credentials: 'same-origin' });
            if (!res.ok && res.status !== 404) throw new Error("Failed to delete feature");
        } catch (e) {
            console.error(e);
            UI.showToast("Error", e.message, "danger");
            throw e;
        }
    }
};