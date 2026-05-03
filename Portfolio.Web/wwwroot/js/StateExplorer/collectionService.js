// small client wrapper for collection CRUD API
const _COLLECTIONS = window.PortfolioApi.routes.collections;
export const CollectionService = {
    async getCollections() {
        const res = await window.apiFetch(_COLLECTIONS);
        if (!res.ok) {
            throw new Error('Failed to load collections');
        }
        return res.json();
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