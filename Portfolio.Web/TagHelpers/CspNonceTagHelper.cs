using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Portfolio.Web.Middleware;

namespace Portfolio.Web.TagHelpers
{
    /// <summary>
    /// Stamps the per-request CSP nonce (see <see cref="SecurityHeadersMiddleware"/>) onto every
    /// <c>&lt;script&gt;</c> and <c>&lt;style&gt;</c> element. Runs after the other tag helpers on
    /// the same element (for example <c>asp-append-version</c>).
    /// </summary>
    [HtmlTargetElement("script")]
    [HtmlTargetElement("style")]
    public class CspNonceTagHelper : TagHelper
    {
        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; } = default!;

        public override int Order => int.MaxValue;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (ViewContext.HttpContext.Items[SecurityHeadersMiddleware.NonceItemKey] is string nonce
                && !output.Attributes.ContainsName("nonce"))
            {
                output.Attributes.SetAttribute("nonce", nonce);
            }
        }
    }
}
