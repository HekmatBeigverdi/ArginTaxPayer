namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Explicit localized rule text. Findings expose only stable localization keys;
/// no raw tax identifiers or user-provided text is interpolated into messages.
/// Locale fallback must be chosen by caller, not silently inferred.
/// </summary>
public sealed class InvoiceRuleMessageCatalog
{
    private readonly IReadOnlyDictionary<(string Key, string Locale), string> _messages;

    public InvoiceRuleMessageCatalog(
        IEnumerable<(string MessageKey, string Locale, string Text)> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var values = new Dictionary<(string Key, string Locale), string>();
        foreach (var (key, locale, value) in messages)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(locale) ||
                string.IsNullOrWhiteSpace(value) || value.Length > 500 ||
                value.Any(char.IsControl))
                throw new ArgumentException("Invalid localized error text.");
            if (locale is not ("fa-IR" or "en-US"))
                throw new ArgumentException("Only explicitly registered locales are accepted.");
            if (!values.TryAdd((key, locale), value))
                throw new ArgumentException("Duplicate localized message key and locale.");
        }
        _messages = values;
    }

    public bool TryResolve(InvoiceRuleFinding finding, string locale, out string? message)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (locale is not ("fa-IR" or "en-US"))
            throw new ArgumentException("Unregistered locale.", nameof(locale));
        if (_messages.TryGetValue((finding.MessageKey, locale), out var resolved))
        {
            message = resolved;
            return true;
        }
        message = null;
        return false;
    }
}
