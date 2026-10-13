/**
 * scalar-config.js
 * Extra configuration for the Scalar API reference at /scalar (loaded as an ES module through
 * WithJavaScriptConfiguration in Startup/Pipeline.cs).
 *
 * A state-changing /api request that carries an identity cookie must also carry an X-Requested-With header
 * (ApiRequestHeaderMiddleware). Scalar has no "default headers" option, so the onBeforeRequest hook adds it to
 * every Try-it request. The hook is marked experimental in Scalar 2.18; if a Scalar update breaks it, Try-it
 * POST/PUT/DELETE calls from a browser that holds an identity cookie answer 403 with a message naming the header.
 */
export default {
    onBeforeRequest: ({ requestBuilder }) => {
        requestBuilder.headers.set("X-Requested-With", "XMLHttpRequest");
    }
};
