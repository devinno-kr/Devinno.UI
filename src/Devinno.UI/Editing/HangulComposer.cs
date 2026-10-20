using System.Text;

namespace Devinno.UI.Editing;

/// <summary>
/// Dubeolsik (2-set) Korean Hangul automaton (for the virtual keyboard). Keeps the syllable being composed as <b>the list of typed jamo</b> and recomposes it each time,
/// so Backspace removes one jamo at a time (닭→달→다→ㄷ), and when a vowel follows, the last final-consonant jamo moves to the next syllable (닭+ㅏ→달가).
/// Committed text is deleted one character at a time (as in most IMEs).
/// </summary>
internal sealed class HangulComposer
{
    private const string Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    private const string Jung = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
    private const string Jong = "\0ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

    private static readonly Dictionary<(char, char), char> VowelPairs = new()
    {
        [('ㅗ', 'ㅏ')] = 'ㅘ', [('ㅗ', 'ㅐ')] = 'ㅙ', [('ㅗ', 'ㅣ')] = 'ㅚ',
        [('ㅜ', 'ㅓ')] = 'ㅝ', [('ㅜ', 'ㅔ')] = 'ㅞ', [('ㅜ', 'ㅣ')] = 'ㅟ', [('ㅡ', 'ㅣ')] = 'ㅢ'
    };

    private static readonly Dictionary<(char, char), char> FinalPairs = new()
    {
        [('ㄱ', 'ㅅ')] = 'ㄳ', [('ㄴ', 'ㅈ')] = 'ㄵ', [('ㄴ', 'ㅎ')] = 'ㄶ', [('ㄹ', 'ㄱ')] = 'ㄺ',
        [('ㄹ', 'ㅁ')] = 'ㄻ', [('ㄹ', 'ㅂ')] = 'ㄼ', [('ㄹ', 'ㅅ')] = 'ㄽ', [('ㄹ', 'ㅌ')] = 'ㄾ',
        [('ㄹ', 'ㅍ')] = 'ㄿ', [('ㄹ', 'ㅎ')] = 'ㅀ', [('ㅂ', 'ㅅ')] = 'ㅄ'
    };

    private readonly StringBuilder _done = new();
    private readonly List<char> _keys = new(); // 조합 중인 음절의 자모 입력

    /// <summary>Committed text + the character being composed.</summary>
    public string Text => _done + Compose(_keys);

    /// <summary>Number of characters being composed at the end (0 or 1).</summary>
    public int ComposingLength => _keys.Count > 0 ? 1 : 0;

    public void Reset(string text)
    {
        _done.Clear().Append(text);
        _keys.Clear();
    }

    public static bool IsConsonant(char c) => Cho.Contains(c);
    public static bool IsVowel(char c) => Jung.Contains(c);

    /// <summary>Composes if the character is a jamo; for any other character, commits the composition and appends the character as is.</summary>
    public void Type(char c)
    {
        if (IsConsonant(c))
            TypeConsonant(c);
        else if (IsVowel(c))
            TypeVowel(c);
        else
        {
            Finish();
            _done.Append(c);
        }
    }

    public void Backspace()
    {
        if (_keys.Count > 0)
            _keys.RemoveAt(_keys.Count - 1);
        else if (_done.Length > 0)
            _done.Length--;
    }

    /// <summary>Commits the character being composed (the next jamo starts a new syllable).</summary>
    public void Finish()
    {
        _done.Append(Compose(_keys));
        _keys.Clear();
    }

    private void TypeConsonant(char c)
    {
        var (cho, jung, jong) = Split(_keys);
        var canAppend = cho is not null && jung.Count > 0 && jong.Count switch
        {
            0 => Jong.Contains(c),                                  // 받침 자리(ㄸ·ㅃ·ㅉ 는 받침 불가)
            1 => FinalPairs.ContainsKey((jong[0], c)),               // 겹받침
            _ => false
        };

        if (!canAppend)
            Finish();
        _keys.Add(c);
    }

    private void TypeVowel(char v)
    {
        var (cho, jung, jong) = Split(_keys);
        if (jong.Count > 0)
        {
            // 마지막 받침 자모가 새 음절의 초성으로(각+ㅏ → 가가, 닭+ㅏ → 달가)
            var moved = _keys[^1];
            _keys.RemoveAt(_keys.Count - 1);
            Finish();
            _keys.Add(moved);
            _keys.Add(v);
            return;
        }

        var canAppend = jung.Count switch
        {
            0 => cho is not null,                                    // 초성 뒤 모음
            1 => VowelPairs.ContainsKey((jung[0], v)),               // 겹모음
            _ => false
        };

        if (!canAppend)
            Finish();
        _keys.Add(v);
    }

    // 자모 목록 → (초성, 중성 자모들, 종성 자모들)
    private static (char? Cho, List<char> Jung, List<char> Jong) Split(List<char> keys)
    {
        var i = 0;
        char? cho = keys.Count > 0 && IsConsonant(keys[0]) ? keys[i++] : null;
        var jung = new List<char>();
        while (i < keys.Count && IsVowel(keys[i]))
            jung.Add(keys[i++]);
        var jong = keys.Skip(i).ToList();
        return (cho, jung, jong);
    }

    private static string Compose(List<char> keys)
    {
        if (keys.Count == 0)
            return "";

        var (cho, jungKeys, jongKeys) = Split(keys);
        var jung = jungKeys.Count switch
        {
            0 => (char?)null,
            1 => jungKeys[0],
            _ => VowelPairs[(jungKeys[0], jungKeys[1])]
        };

        if (cho is null)
            return jung?.ToString() ?? "";
        if (jung is null)
            return cho.Value.ToString();

        var jong = jongKeys.Count switch
        {
            0 => '\0',
            1 => jongKeys[0],
            _ => FinalPairs[(jongKeys[0], jongKeys[1])]
        };

        var code = 0xAC00 + (Cho.IndexOf(cho.Value) * 21 + Jung.IndexOf(jung.Value)) * 28 + Jong.IndexOf(jong);
        return ((char)code).ToString();
    }
}
