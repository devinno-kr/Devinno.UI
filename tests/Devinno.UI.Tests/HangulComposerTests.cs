using Devinno.UI.Editing;
using Xunit;

// P8a-7 두벌식 오토마타 — 조합, 겹모음·겹받침, 받침 이동, 자모 단위 백스페이스, 확정
public class HangulComposerTests
{
    private static HangulComposer Type(string keys, string initial = "")
    {
        var h = new HangulComposer();
        h.Reset(initial);
        foreach (var k in keys)
            h.Type(k);
        return h;
    }

    [Theory]
    [InlineData("ㄱㅏ", "가")]
    [InlineData("ㄱㅏㄱ", "각")]
    [InlineData("ㄱㅏㄱㅏ", "가가")]
    [InlineData("ㄷㅏㄹㄱ", "닭")]
    [InlineData("ㄷㅏㄹㄱㅏ", "달가")]
    [InlineData("ㅇㅗㅏ", "와")]
    [InlineData("ㅇㅜㅓㄴ", "원")]
    [InlineData("ㄲㅗㅊ", "꽃")]
    [InlineData("ㅎㅏㄴㄱㅡㄹ", "한글")]
    [InlineData("ㅂㅏㄹㅂㅏㅂ", "발밥")]
    [InlineData("ㅇㅓㅂㅅㅇㅓ", "없어")]
    [InlineData("ㅏ", "ㅏ")]
    [InlineData("ㅗㅏ", "ㅘ")]
    [InlineData("ㄱㄱ", "ㄱㄱ")]
    [InlineData("ㄸㅏㄸ", "따ㄸ")]
    [InlineData("ㅏㄱ", "ㅏㄱ")]
    [InlineData("ㄱㅏㅏ", "가ㅏ")]
    public void Composes(string keys, string expected)
        => Assert.Equal(expected, Type(keys).Text);

    [Fact]
    public void Backspace_removes_one_jamo_at_a_time()
    {
        var h = Type("ㄷㅏㄹㄱ");
        var steps = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            h.Backspace();
            steps.Add(h.Text);
        }

        Assert.Equal(new[] { "달", "다", "ㄷ", "" }, steps);
    }

    [Fact]
    public void Backspace_splits_compound_vowel()
    {
        var h = Type("ㅇㅗㅏ");
        h.Backspace();
        Assert.Equal("오", h.Text);
    }

    [Fact]
    public void Backspace_after_moved_final_consonant_works_on_the_new_syllable()
    {
        var h = Type("ㄱㅏㄱㅏ"); // 가가
        h.Backspace();
        Assert.Equal("가ㄱ", h.Text);
        h.Backspace();
        Assert.Equal("가", h.Text);
        h.Backspace(); // 확정된 글자는 글자 단위로 지운다
        Assert.Equal("", h.Text);
    }

    [Fact]
    public void Non_jamo_finishes_the_syllable()
    {
        var h = Type("ㄱㅏ1ㄴㅏ");
        Assert.Equal("가1나", h.Text);
    }

    [Fact]
    public void Finish_commits_and_next_jamo_starts_fresh()
    {
        var h = Type("ㄱㅏ");
        h.Finish();
        h.Type('ㄱ');
        Assert.Equal("가ㄱ", h.Text);
        Assert.Equal(1, h.ComposingLength);
    }

    [Fact]
    public void Reset_keeps_initial_text_and_backspace_deletes_it()
    {
        var h = Type("ㄱㅏ", "abc");
        Assert.Equal("abc가", h.Text);
        h.Backspace();
        h.Backspace();
        h.Backspace();
        Assert.Equal("ab", h.Text);
    }

    [Fact]
    public void Composing_length_is_zero_or_one()
    {
        Assert.Equal(0, Type("").ComposingLength);
        Assert.Equal(1, Type("ㄱ").ComposingLength);
        Assert.Equal(0, Type("ㄱ1").ComposingLength);
    }
}
