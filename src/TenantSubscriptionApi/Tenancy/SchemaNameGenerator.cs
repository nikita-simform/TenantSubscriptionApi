using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TenantSubscriptionApi.Domain.Exceptions;

namespace TenantSubscriptionApi.Tenancy;

// Postgres schema names can't be parameterized like ordinary query values — they get
// interpolated directly into DDL (CREATE SCHEMA "x"). AssertSafe() is the one gate every
// derived name must pass through before that happens; Derive() alone is not enough on
// its own, in case a future caller ever builds a schema name a different way.
public static class SchemaNameGenerator
{
    private const int MaxSlugLength = 50;
    private static readonly Regex SafePattern = new("^org_[a-z0-9_]{1,59}$", RegexOptions.Compiled);

    public static string Derive(string organizationName)
    {
        var slug = RemoveDiacritics(organizationName.Trim().ToLowerInvariant());
        slug = Regex.Replace(slug, "[^a-z0-9]+", "_").Trim('_');

        if (slug.Length == 0)
            slug = "org";
        if (slug.Length > MaxSlugLength)
            slug = slug[..MaxSlugLength].Trim('_');

        var schemaName = $"org_{slug}";
        AssertSafe(schemaName);
        return schemaName;
    }

    public static void AssertSafe(string schemaName)
    {
        if (!SafePattern.IsMatch(schemaName))
            throw new ValidationException(
                $"Generated schema name '{schemaName}' did not pass safety validation.");
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
