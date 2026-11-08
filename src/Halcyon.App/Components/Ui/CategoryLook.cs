using Halcyon.Domain.Categories;
using Halcyon.Domain.Tickets;

namespace Halcyon.App.Components.Ui;

/// <summary>
/// How categories, subcategories and priorities look in the UI: a Lucide icon and a color. Presentation only, so it
/// lives here and not in the domain catalog. Class names are written out in full for Tailwind's scanner.
/// </summary>
public static class CategoryLook
{
    /// <param name="Bubble">The icon circle.</param>
    /// <param name="Chosen">The tile when selected.</param>
    public sealed record Look(string Icon, string Bubble, string Chosen);

    public static Look For(string categoryKey) =>
        categoryKey switch
        {
            "it" => new(
                "monitor",
                "bg-halcyon-100 text-halcyon-700",
                "border-halcyon-500 bg-halcyon-50 ring-halcyon-500"
            ),
            "legal" => new("scale", "bg-violet-100 text-violet-700", "border-violet-500 bg-violet-50 ring-violet-500"),
            "marketing" => new(
                "megaphone",
                "bg-orange-100 text-orange-700",
                "border-orange-500 bg-orange-50 ring-orange-500"
            ),
            "hr-abroad" => new("globe", "bg-sky-100 text-sky-700", "border-sky-500 bg-sky-50 ring-sky-500"),
            "hr-poland" => new(
                "briefcase",
                "bg-emerald-100 text-emerald-700",
                "border-emerald-500 bg-emerald-50 ring-emerald-500"
            ),
            "recruitment" => new(
                "users",
                "bg-indigo-100 text-indigo-700",
                "border-indigo-500 bg-indigo-50 ring-indigo-500"
            ),
            "finance" => new(
                "banknote",
                "bg-yellow-100 text-yellow-800",
                "border-yellow-500 bg-yellow-50 ring-yellow-500"
            ),
            "legalization" => new("id-card", "bg-rose-100 text-rose-700", "border-rose-500 bg-rose-50 ring-rose-500"),
            _ => new("inbox", "bg-ink-100 text-ink-700", "border-ink-500 bg-ink-50 ring-ink-500"),
        };

    /// <summary>A short hint under the tile: the markets when the category has them, else its first subcategories.</summary>
    public static string Hint(Category category)
    {
        var active = category.ActiveSubcategories.ToList();
        var groups = active.Select(s => s.Group).OfType<string>().Distinct().ToList();
        // In the reader's language (ADR-0012): the names are joined here, after which they cannot be told apart.
        var parts =
            groups.Count > 0
                ? groups.Select(Localization.Tr.N).ToList()
                : active.Where(s => s.Name != "Feedback").Select(s => Localization.Tr.N(s.Name)).Take(3).ToList();
        return string.Join(", ", parts);
    }

    /// <summary>By the last part of the key, so every market of Kadry Zagranica gets the same icons.</summary>
    public static string SubcategoryIcon(string subcategoryKey) =>
        subcategoryKey[(subcategoryKey.LastIndexOf('.') + 1)..] switch
        {
            "hardware" => "laptop",
            "outage" => "zap",
            "application" => "app-window",
            "contracts" => "file-pen-line",
            "opinion" => "scale",
            "gdpr" => "shield-check",
            "disputes" => "gavel",
            "campaign" => "target",
            "social" => "share-2",
            "graphics" => "palette",
            "website" => "globe",
            "events" => "party-popper",
            "contract" => "file-text",
            "posting" => "stamp",
            "payroll" => "wallet",
            "tax" => "receipt",
            "travel" => "plane",
            "leave" => "tree-palm",
            "certificates" => "file-check",
            "medical" => "stethoscope",
            "benefits" => "gift",
            "offboarding" => "door-open",
            "onboarding" => "user-plus",
            "pesel" => "id-card",
            "lodging" => "bed",
            "housing" => "house",
            "pup" => "file-stack",
            "starosty" => "landmark",
            "work-permit" => "file-check",
            "residence" => "building-2",
            "internal" => "user-plus",
            "client" => "briefcase",
            "candidates" => "contact",
            "invoices" => "receipt",
            "sales-invoices" => "file-text",
            "expenses" => "wallet",
            "advance" => "hand-coins",
            "payments" => "banknote",
            "budget" => "chart-column",
            "feedback" => "message-square",
            _ => "sparkles",
        };

    public static (string Icon, string Bubble, string Chosen) For(Priority priority) =>
        priority switch
        {
            Priority.Critical => ("flame", "bg-red-100 text-red-700", "border-red-500 bg-red-50 ring-red-500"),
            Priority.High => (
                "arrow-up",
                "bg-orange-100 text-orange-700",
                "border-orange-500 bg-orange-50 ring-orange-500"
            ),
            Priority.Medium => ("minus", "bg-amber-100 text-amber-700", "border-amber-500 bg-amber-50 ring-amber-500"),
            _ => ("arrow-down", "bg-ink-100 text-ink-600", "border-ink-400 bg-ink-50 ring-ink-400"),
        };
}
