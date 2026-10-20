using SkiaSharp;

namespace Devinno.UI.Text;

/// <summary>Text line wrapping. Label measuring/painting and multi-line DvInput use the same rules.</summary>
internal static class TextWrap
{
    /// <summary>
    /// Splits first at line breaks (<c>\n</c>, <c>\r\n</c>); a line wider than the width is broken after the last space that fits (or per character if there is no space).
    /// Trailing spaces of a broken line are removed; leading spaces of the next line are kept. If not even one character fits, one character is placed per line.
    /// Surrogate pairs are never split (the only difference from the existing InputDisplay.Wrap).
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, SKFont font, float width)
    {
        var lines = new List<string>();
        // 첫 추정 글자 수 = 폭 / (글자 크기의 절반) — 이후는 직전 줄 글자 수(줄 길이가 비슷하므로 몇 번 재면 찾는다)
        var guess = (int)MathF.Min(int.MaxValue / 4f, width / MathF.Max(1, font.Size * 0.5f));
        foreach (var para in text.Replace("\r\n", "\n").Split('\n'))
        {
            var rest = para;
            while (rest.Length > 0)
            {
                var cut = FitCount(rest, font, width, guess, out var all);
                if (all)
                    break;
                guess = cut;
                // 서로게이트 쌍(이모지 등)은 가르지 않는다 — 앞으로 당기고, 첫 글자면 쌍 전체를 넣는다
                if (cut < rest.Length && char.IsLowSurrogate(rest[cut]) && char.IsHighSurrogate(rest[cut - 1]))
                    cut = cut > 1 ? cut - 1 : cut + 1;
                var space = rest.LastIndexOf(' ', cut - 1);
                if (space > 0)
                    cut = space + 1;
                lines.Add(rest[..cut].TrimEnd());
                rest = rest[cut..];
            }

            lines.Add(rest);
        }

        return lines;
    }

    /// <summary>Width comparison tolerance (px), so that floating-point rounding differences between measured and laid-out width do not split a single-line text.</summary>
    public const float Tolerance = 0.05f;

    // 폭 안에 들어가는 가장 긴 앞부분의 글자 수(최소 1). 전체가 들어가면 all = true(그때만 s.Length 가 "들어감").
    // 앞부분 폭은 글자 수가 늘면 줄지 않으므로: 추정 글자 수부터 두 배씩 늘려 넘는 지점을 찾고(galloping), 그 사이를 이진 탐색.
    // 한 줄에 재는 횟수가 O(log 줄 길이)이고 잰 앞부분 길이도 줄 길이의 몇 배 안이라, 글 전체로 거의 O(n) —
    // 남은 글 전체를 줄마다 재던 방식(O(줄 수 × n))보다 빠르다.
    private static int FitCount(string s, SKFont font, float width, int guess, out bool all)
    {
        var n = s.Length;
        int lo = 0, hi = -1; // lo = 들어간다고 확인한 최대 글자 수, hi = 넘는다고 확인한 최소 글자 수(-1 = 아직 없음)
        var probe = Math.Clamp(guess, 1, n);
        while (true)
        {
            if (font.MeasureText(s.AsSpan(0, probe)) <= width)
            {
                lo = probe;
                if (probe == n)
                {
                    all = true;
                    return n;
                }

                probe = (int)Math.Min(n, probe * 2L);
            }
            else
            {
                hi = probe;
                break;
            }
        }

        while (hi - lo > 1)
        {
            var mid = lo + (hi - lo) / 2;
            if (font.MeasureText(s.AsSpan(0, mid)) <= width)
                lo = mid;
            else
                hi = mid;
        }

        all = false;
        return Math.Max(1, lo);
    }
}

/// <summary>
/// Line info for one text box: the paragraphs split by <c>\n</c>, the widest paragraph width, and the lines for the last wrap width.
/// Measuring creates it and stores it on the box; painting does not re-wrap if the text, font and width are unchanged.
/// </summary>
internal sealed class TextLines
{
    private readonly float _size;
    private readonly bool _bold;
    private readonly string? _family;

    public TextLines(string text, float size, bool bold, string? family, SKFont font)
    {
        Text = text;
        (_size, _bold, _family) = (size, bold, family);
        if (text.Contains('\n'))
        {
            var paras = text.Replace("\r\n", "\n").Split('\n');
            Paras = paras;
            foreach (var p in paras)
                Width = MathF.Max(Width, font.MeasureText(p));
        }
        else
        {
            Paras = new[] { text };
            Width = font.MeasureText(text);
        }
    }

    public string Text { get; }

    /// <summary>Lines split only at line breaks (the original text as a single entry if there are none).</summary>
    public IReadOnlyList<string> Paras { get; }

    /// <summary>Single-line width of the widest paragraph (px).</summary>
    public float Width { get; }

    private IReadOnlyList<string>? _wrapped;
    private float _wrapWidth = float.NaN;

    public bool Matches(string text, float size, bool bold, string? family)
        => Text == text && _size == size && _bold == bold && _family == family;

    /// <summary>
    /// Lines to place in the inner width <paramref name="width"/>. If there is no width (<see langword="null"/>) or the widest paragraph fits, the paragraphs as they are;
    /// otherwise the lines wrapped to that width (not re-wrapped if the width equals the previous one).
    /// </summary>
    public IReadOnlyList<string> At(float? width, SKFont font)
    {
        if (width is not { } w || Width <= w + TextWrap.Tolerance)
            return Paras;
        if (_wrapped is { } done && MathF.Abs(_wrapWidth - w) <= TextWrap.Tolerance)
            return done;
        _wrapWidth = w;
        return _wrapped = TextWrap.Wrap(Text, font, w);
    }
}
