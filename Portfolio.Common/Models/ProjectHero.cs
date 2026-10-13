namespace Portfolio.Common.Models;

/// <summary>
/// A pill shown at the right of a project page hero: a Font Awesome icon and a short label.
/// </summary>
/// <param name="IconClass">Font Awesome classes for the pill icon, e.g. "fa-solid fa-satellite-dish".</param>
/// <param name="Text">The pill label.</param>
public sealed record ProjectHeroBadge(string IconClass, string Text);

/// <summary>
/// Model for the shared project page hero partial (<c>_ProjectHero.cshtml</c>).
/// </summary>
/// <param name="Title">The h1 text, also used in the details link's accessible name.</param>
/// <param name="IconClass">Font Awesome classes for the h1 icon, e.g. "fa-solid fa-industry".</param>
/// <param name="Subtitle">One-line description under the title.</param>
/// <param name="Badges">Zero or more pills shown at the right; may be null.</param>
/// <param name="DetailsUrl">The page's Details route; null or empty hides the info button.</param>
/// <param name="DetailsLabel">The info button's tooltip; null or empty falls back to "Technical Details".</param>
public sealed record ProjectHero(
    string Title,
    string IconClass,
    string Subtitle,
    IReadOnlyList<ProjectHeroBadge>? Badges = null,
    string? DetailsUrl = null,
    string? DetailsLabel = null);
