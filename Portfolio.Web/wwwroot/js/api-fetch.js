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
     * - On 401: redirects the whole page to /Login?returnUrl=<current path>,
     *   unless options.redirectOn401 === false (pages valid for anonymous visitors).
     * - options.timeoutMs aborts the request after that many milliseconds.
     * - redirectOn401 and timeoutMs are stripped before fetch is called.
     * - Returns the raw Response so callers can inspect status before parsing.
     *
     * @param {string}      url
     * @param {RequestInit} [options]
     * @returns {Promise<Response>}
     */
    function apiFetch(url, options) {
        // Copy so the caller's object is untouched, then strip the custom keys before fetch.
        var opts = Object.assign({}, options || {});
        var redirectOn401 = opts.redirectOn401 !== false;
        var timeoutMs = opts.timeoutMs;
        delete opts.redirectOn401;
        delete opts.timeoutMs;

        // Always send cookies so authentication and anonymous-identity cookies travel with each request.
        if (!opts.credentials) {
            opts.credentials = "same-origin";
        }

        var timer = null;
        if (typeof timeoutMs === "number" && timeoutMs > 0 && typeof global.AbortController === "function") {
            var controller = new global.AbortController();
            var callerSignal = opts.signal;
            if (callerSignal) {
                // Honour a caller-supplied signal as well as the timeout.
                if (callerSignal.aborted) { controller.abort(); }
                else { callerSignal.addEventListener("abort", function () { controller.abort(); }); }
            }
            opts.signal = controller.signal;
            timer = setTimeout(function () { controller.abort(); }, timeoutMs);
        }

        return fetch(url, opts).then(function (res) {
            if (timer) { clearTimeout(timer); }
            if (res.status === 401 && redirectOn401) {
                var returnUrl = encodeURIComponent(
                    global.location.pathname + global.location.search
                );
                global.location.href = "/Login?returnUrl=" + returnUrl;
                // Return a never-resolving promise so calling code halts
                // while the redirect is in progress.
                return new Promise(function () {});
            }
            return res;
        }, function (err) {
            if (timer) { clearTimeout(timer); }
            throw err;
        });
    }

    var TOO_MANY_REQUESTS = "Too many requests. Please wait a minute and try again.";

    /**
     * Extracts a human-readable error message from an error Response.
     * 429 returns a dedicated rate-limit message. Otherwise parses JSON and returns
     * body.error, then the ProblemDetails detail, then title; falls back to a generic message.
     *
     * @param {Response} response
     * @returns {Promise<string>}
     */
    function getApiError(response) {
        if (response.status === 429) {
            return Promise.resolve(TOO_MANY_REQUESTS);
        }
        return response.json()
            .then(function (body) {
                return (body && (body.error || body.detail || body.title)) || "An unexpected error occurred.";
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
