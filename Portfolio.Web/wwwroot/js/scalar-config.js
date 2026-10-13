/**
 * scalar-config.js
 * Extra configuration for the Scalar API reference at /scalar (loaded as an ES module through
 * WithJavaScriptConfiguration in Startup/Pipeline.cs).
 *
 * A state-changing /api request that carries an identity cookie must also carry an X-Requested-With header
 * (ApiRequestHeaderMiddleware). Scalar has no "default headers" option, so the onBeforeRequest hook adds it to
 * every Try-it request. The hook is marked experimental in Scalar 2.18, so it is written defensively: if a Scalar
 * update renames or reshapes requestBuilder, the hook does nothing instead of throwing (a throw would fail every
 * Try-it request). Try-it POST/PUT/DELETE calls from a browser that holds an identity cookie then
 * answer 403 with a message naming the header.
 */
export default {
    onBeforeRequest: (context) => {
        context?.requestBuilder?.headers?.set?.("X-Requested-With", "XMLHttpRequest");
    }
};
