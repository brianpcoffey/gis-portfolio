// small client wrapper for collection CRUD API
const _COLLECTIONS = window.PortfolioApi.routes.collections;
export const CollectionService = {
    // True when the visitor is anonymous (FE-A07): the server rendered the page as signed out, or the
    // collections API answered 401 (a session that expired after the page loaded).
    requiresSignIn: false,

    // signedIn: whether the server rendered the page for a signed-in visitor (data-signed-in on the
    // page root). An anonymous visitor gets no request at all, so the console shows no 401.
    async getCollections({ signedIn = true } = {}) {
        // Collections require auth; for anonymous visitors (or any transient error)
        // return an empty list rather than throwing, so a failed collections fetch
        // never rejects the Promise.all in initialize() and bricks the whole page.
        // redirectOn401:false keeps an anonymous visitor on this page instead of
        // bouncing them to /Login; the UI shows a sign-in hint instead.
        this.requiresSignIn = !signedIn;
        if (!signedIn) return [];
        try {
            const res = await window.apiFetch(_COLLECTIONS, { redirectOn401: false });
            if (res.status === 401) {
                this.requiresSignIn = true;
                return [];
            }
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