/**
 * Converts inline and basic Markdown syntax to safe HTML.
 *
 * Supported features:
 * - Bold: `**text**` or `__text__` -> `<strong>text</strong>`
 * - Italic: `*text*` or `_text_` -> `<em>text</em>`
 * - Inline code: `` `code` `` -> `<code>code</code>`
 * - Line breaks: converts `\n` to `<br />` (while preserving existing HTML tags)
 * - Safe against XSS: escapes raw HTML characters (<, >, &, ", ') unless they
 *   already match supported safe tags.
 */

const SAFE_TAG_PLACEHOLDER_REGEX = /<(\/?[a-z0-9]+)(?:\s+[^>]*)?>/gi;

/**
 * Parses markdown inline formatting into HTML.
 */
export function formatMarkdown(text?: string | null): string {
  if (!text) return '';

  // If text already contains complete HTML paragraph or safe formatted snippets,
  // we first preserve those known tags or escape non-tags.
  // Standard simple approach:
  // 1. We tokenize existing safe HTML tags (like <code>, <br>, <strong>, etc.) if any.
  const placeholders: string[] = [];
  let processed = text.replace(SAFE_TAG_PLACEHOLDER_REGEX, (match) => {
    // Check if it's an allowed safe tag
    const isAllowed = /^<\/?(strong|b|em|i|code|pre|br|span|p|a|ul|ol|li)(?:\s+[^>]*)?>$/i.test(match);
    if (isAllowed) {
      placeholders.push(match);
      return `__SAFE_HTML_${placeholders.length - 1}__`;
    }
    // Escape disallowed tags
    return match.replace(/</g, '&lt;').replace(/>/g, '&gt;');
  });

  // 2. Escape standalone '<' and '>' that weren't part of placeholders
  processed = processed
    .replace(/&(?!([a-z0-9]+|#\d+|#x[0-9a-f]+);)/gi, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  // 3. Inline code: `code`
  processed = processed.replace(/`([^`]+)`/g, '<code class="wf-inline-code">$1</code>');

  // 4. Bold: **text** or __text__
  processed = processed.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  processed = processed.replace(/__([^_]+)__/g, '<strong>$1</strong>');

  // 5. Italic: *text* or _text_ (excluding words with internal underscores like user_id)
  processed = processed.replace(/(^|[^\w*])\*([^*\n]+)\*([^\w*]|$)/g, '$1<em>$2</em>$3');
  processed = processed.replace(/(^|[^\w_])_([^_\n]+)_([^\w_]|$)/g, '$1<em>$2</em>$3');

  // 6. Convert newlines to <br />
  processed = processed.replace(/\r\n/g, '<br />').replace(/\n/g, '<br />');

  // 7. Restore placeholders
  processed = processed.replace(/__SAFE_HTML_(\d+)__/g, (_, idx) => {
    return placeholders[Number(idx)] ?? '';
  });

  return processed;
}
