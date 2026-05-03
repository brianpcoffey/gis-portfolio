/**
 * api-fetch.js
 * Shared, authentication-aware fetch helpers for all portfolio frontend pages.
 * Loaded globally via _Layout.cshtml after api-config.js and before page scripts.
 *
 * Globals exposed:
 *   window.apiFetch(url, options)          — base helper; redirects to /Login on 401
 *   window.apiGet(url)                     — GET, returns parsed JSON or throws
 *   window.apiPost(url, body)              — POST JSON body, returns parsed JSON or throws
 *   window.apiPut(url, body)               — PUT JSON body, returns parsed JSON or throws
 *   window.apiDelete(url)                  — DELETE, returns true on 204 or throws
 *   window.apiPostForm(url, formData)      — POST multipart/form-data, returns parsed JSON or throws
 *   window.getApiError(response)           — extracts error message string from an error Response
 *   window.fetchWithAuth(url, options)     — alias of apiFetch (keeps PlantOperationsDashboard compat)
 */
(function (global) {
    "use strict";

    /**
     * Core fetch wrapper.
     * - On 401: redirects the whole page to /Login?returnUrl=<current path>.
     * - Returns the raw Response so callers can inspect status before parsing.
     *
     * @param {string}      url
     * @param {RequestInit} [options]
     * @returns {Promise<Response>}
     */
    function apiFetch(url, options) {
        options = options || {};
        // Always send cookies so authentication and anonymous-identity cookies travel with each request.
        if (!options.credentials) {
            options.credentials = "same-origin";
        }

        return fetch(url, options).then(function (res) {
            if (res.status === 401) {
                var returnUrl = encodeURIComponent(
                    global.location.pathname + global.location.search
                );
                global.location.href = "/Login?returnUrl=" + returnUrl;
                // Return a never-resolving promise so calling code halts
                // while the redirect is in progress.
                return new Promise(function () {});
            }
            return res;
        });
    }

    /**
     * Extracts a human-readable error message from an error Response.
     * Tries to parse JSON { error: "..." }, falls back to statusText.
     *
     * @param {Response} response
     * @returns {Promise<string>}
     */
    function getApiError(response) {
        return response.json()
            .then(function (body) {
                return body && body.error ? body.error : "An unexpected error occurred.";
            })
            .catch(function () {
                return "An unexpected error occurred (HTTP " + response.status + ").";
            });
    }

    /**
     * GET and return parsed JSON. Throws Error with message on non-ok response.
     *
     * @param {string} url
     * @returns {Promise<any>}
     */
    function apiGet(url) {
        return apiFetch(url).then(function (res) {
            if (!res.ok) {
                return getApiError(res).then(function (msg) { throw new Error(msg); });
            }
            return res.json();
        });
    }

    /**
     * POST a JSON body and return parsed JSON. Throws on non-ok response.
     *
     * @param {string} url
     * @param {object} body
     * @returns {Promise<any>}
     */
    function apiPost(url, body) {
        return apiFetch(url, {
            method  : "POST",
            headers : { "Content-Type": "application/json" },
            body    : JSON.stringify(body)
        }).then(function (res) {
            if (!res.ok) {
                return getApiError(res).then(function (msg) { throw new Error(msg); });
            }
            return res.json();
        });
    }

    /**
     * PUT a JSON body and return parsed JSON. Throws on non-ok response.
     *
     * @param {string} url
     * @param {object} body
     * @returns {Promise<any>}
     */
    function apiPut(url, body) {
        return apiFetch(url, {
            method  : "PUT",
            headers : { "Content-Type": "application/json" },
            body    : JSON.stringify(body)
        }).then(function (res) {
            if (!res.ok) {
                return getApiError(res).then(function (msg) { throw new Error(msg); });
            }
            return res.json();
        });
    }

    /**
     * DELETE and return true on success. Throws on non-ok response.
     *
     * @param {string} url
     * @returns {Promise<true>}
     */
    function apiDelete(url) {
        return apiFetch(url, { method: "DELETE" }).then(function (res) {
            if (!res.ok && res.status !== 404) {
                return getApiError(res).then(function (msg) { throw new Error(msg); });
            }
            return true;
        });
    }

    /**
     * POST a FormData body (multipart/form-data) and return parsed JSON.
     * Do NOT set Content-Type — the browser sets it automatically with boundary.
     * Throws on non-ok response.
     *
     * @param {string}   url
     * @param {FormData} formData
     * @returns {Promise<any>}
     */
    function apiPostForm(url, formData) {
        return apiFetch(url, {
            method : "POST",
            body   : formData
        }).then(function (res) {
            if (!res.ok) {
                return getApiError(res).then(function (msg) { throw new Error(msg); });
            }
            return res.json();
        });
    }

    // ── Expose globals ─────────────────────────────────────────────────────────

    global.apiFetch      = apiFetch;
    global.apiGet        = apiGet;
    global.apiPost       = apiPost;
    global.apiPut        = apiPut;
    global.apiDelete     = apiDelete;
    global.apiPostForm   = apiPostForm;
    global.getApiError   = getApiError;

    // Backward-compatible alias used by PlantOperationsDashboard scripts.
    global.fetchWithAuth = apiFetch;

}(window));
