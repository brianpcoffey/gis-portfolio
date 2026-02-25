// small client wrapper for collection CRUD API
export const CollectionService = {
    async getCollections() {
        const res = await fetch('/api/collections', { credentials: 'same-origin' });
        if (!res.ok) {
            throw new Error('Failed to load collections');
        }
        return res.json();
    },

    async createCollection(name, color) {
        const res = await fetch('/api/collections', {
            method: 'POST',
            credentials: 'same-origin',
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
        const res = await fetch(`/api/collections/${id}`, {
            method: 'PUT',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, color })
        });
        if (!res.ok) throw new Error('Failed to update collection');
        return;
    },

    async deleteCollection(id) {
        const res = await fetch(`/api/collections/${id}`, {
            method: 'DELETE',
            credentials: 'same-origin'
        });
        if (!res.ok) throw new Error('Failed to delete collection');
        return;
    }
};