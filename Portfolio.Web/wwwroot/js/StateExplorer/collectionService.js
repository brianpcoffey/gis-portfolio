// small client wrapper for collection CRUD API
const _COLLECTIONS = window.PortfolioApi.routes.collections;
export const CollectionService = {
    async getCollections() {
        // Collections require auth; for anonymous visitors (or any transient error)
        // return an empty list rather than throwing, so a failed collections fetch
        // never rejects the Promise.all in initialize() and bricks the whole page.
        try {
            const res = await window.apiFetch(_COLLECTIONS);
            if (!res.ok) return [];
            return await res.json();
        } catch {
            return [];
        }
    },

    async createCollection(name, color) {
        const res = await window.apiFetch(_COLLECTIONS, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, color })
        });
        if (!res.ok) {
            const txt = await res.text();
            throw new Error(txt || 'Failed to create collection');
        }
        return res.json();
    },

    async updateCollection(id, { name, color }) {
        const res = await window.apiFetch(`${_COLLECTIONS}/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, color })
        });
        if (!res.ok) throw new Error('Failed to update collection');
        return;
    },

    async deleteCollection(id) {
        const res = await window.apiFetch(`${_COLLECTIONS}/${id}`, {
            method: 'DELETE'
        });
        if (!res.ok) throw new Error('Failed to delete collection');
        return;
    }
};