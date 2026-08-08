using System.Collections.Generic;
using System.Text;

/// <summary>영문 키 입력을 표준 두벌식 한글로 조합한다.
///
/// 한국어 모드에서는 <b>플랫폼과 무관하게</b> 이 조합기가 한글을 만든다(예전에는 WebGL 전용이었다) -
/// OS IME를 쓰지 않으므로 한/영 상태가 어느 쪽이든 결과가 같다. 자세한 경위는
/// <see cref="InputManager.UsesSyntheticHangul"/> 참조.
///
/// ⚠️ 대문자는 쌍자음·이중모음으로 읽는다('R'→ㄲ). 그래서 CapsLock으로 올라간 대문자는
/// 넘기기 전에 소문자로 되돌려야 한다(<c>InputManager.NormalizeCapsLock</c>).</summary>
public sealed class DubeolsikHangulComposer
{
    private readonly StringBuilder keys = new StringBuilder();

    private static readonly char[] Leads =
        { 'ㄱ','ㄲ','ㄴ','ㄷ','ㄸ','ㄹ','ㅁ','ㅂ','ㅃ','ㅅ','ㅆ','ㅇ','ㅈ','ㅉ','ㅊ','ㅋ','ㅌ','ㅍ','ㅎ' };
    private static readonly char[] Vowels =
        { 'ㅏ','ㅐ','ㅑ','ㅒ','ㅓ','ㅔ','ㅕ','ㅖ','ㅗ','ㅘ','ㅙ','ㅚ','ㅛ','ㅜ','ㅝ','ㅞ','ㅟ','ㅠ','ㅡ','ㅢ','ㅣ' };
    private static readonly char[] Tails =
        { '\0','ㄱ','ㄲ','ㄳ','ㄴ','ㄵ','ㄶ','ㄷ','ㄹ','ㄺ','ㄻ','ㄼ','ㄽ','ㄾ','ㄿ','ㅀ','ㅁ','ㅂ','ㅄ','ㅅ','ㅆ','ㅇ','ㅈ','ㅊ','ㅋ','ㅌ','ㅍ','ㅎ' };

    private static readonly Dictionary<string, char> CompoundVowels = new Dictionary<string, char>
    {
        { "ㅗㅏ", 'ㅘ' }, { "ㅗㅐ", 'ㅙ' }, { "ㅗㅣ", 'ㅚ' },
        { "ㅜㅓ", 'ㅝ' }, { "ㅜㅔ", 'ㅞ' }, { "ㅜㅣ", 'ㅟ' }, { "ㅡㅣ", 'ㅢ' }
    };

    private static readonly Dictionary<string, char> CompoundTails = new Dictionary<string, char>
    {
        { "ㄱㅅ", 'ㄳ' }, { "ㄴㅈ", 'ㄵ' }, { "ㄴㅎ", 'ㄶ' },
        { "ㄹㄱ", 'ㄺ' }, { "ㄹㅁ", 'ㄻ' }, { "ㄹㅂ", 'ㄼ' },
        { "ㄹㅅ", 'ㄽ' }, { "ㄹㅌ", 'ㄾ' }, { "ㄹㅍ", 'ㄿ' },
        { "ㄹㅎ", 'ㅀ' }, { "ㅂㅅ", 'ㅄ' }
    };

    public string Text => Compose(keys.ToString());
    public int KeyCount => keys.Length;

    public bool TryAppend(char key)
    {
        if (!TryMap(key, out _)) return false;
        keys.Append(key);
        return true;
    }

    public bool Backspace()
    {
        if (keys.Length == 0) return false;
        keys.Length--;
        return true;
    }

    public void Clear() => keys.Clear();

    private static string Compose(string raw)
    {
        var jamo = new List<char>(raw.Length);
        foreach (var key in raw)
            if (TryMap(key, out var mapped)) jamo.Add(mapped);

        var result = new StringBuilder();
        for (var i = 0; i < jamo.Count;)
        {
            var lead = IndexOf(Leads, jamo[i]);
            if (lead < 0 || i + 1 >= jamo.Count || IndexOf(Vowels, jamo[i + 1]) < 0)
            {
                result.Append(jamo[i++]);
                continue;
            }

            i++;
            var vowelChar = jamo[i++];
            if (i < jamo.Count && CompoundVowels.TryGetValue(string.Concat(vowelChar, jamo[i]), out var compoundVowel))
            {
                vowelChar = compoundVowel;
                i++;
            }

            var tail = 0;
            if (i < jamo.Count && IndexOf(Tails, jamo[i]) > 0)
            {
                // 뒤에 모음이 바로 오면 이 자음은 다음 음절의 초성이다.
                if (i + 1 >= jamo.Count || IndexOf(Vowels, jamo[i + 1]) < 0)
                {
                    var firstTail = jamo[i];
                    tail = IndexOf(Tails, firstTail);

                    if (i + 1 < jamo.Count &&
                        CompoundTails.TryGetValue(string.Concat(firstTail, jamo[i + 1]), out var compoundTail) &&
                        (i + 2 >= jamo.Count || IndexOf(Vowels, jamo[i + 2]) < 0))
                    {
                        tail = IndexOf(Tails, compoundTail);
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }
            }

            var vowel = IndexOf(Vowels, vowelChar);
            result.Append((char)(0xAC00 + (lead * 21 + vowel) * 28 + tail));
        }

        return result.ToString();
    }

    private static int IndexOf(char[] values, char value)
    {
        for (var i = 0; i < values.Length; i++)
            if (values[i] == value) return i;
        return -1;
    }

    private static bool TryMap(char key, out char value)
    {
        switch (key)
        {
            case 'r': value='ㄱ'; return true; case 'R': value='ㄲ'; return true;
            case 's': case 'S': value='ㄴ'; return true; case 'e': value='ㄷ'; return true; case 'E': value='ㄸ'; return true;
            case 'f': case 'F': value='ㄹ'; return true; case 'a': case 'A': value='ㅁ'; return true;
            case 'q': value='ㅂ'; return true; case 'Q': value='ㅃ'; return true; case 't': value='ㅅ'; return true; case 'T': value='ㅆ'; return true;
            case 'd': case 'D': value='ㅇ'; return true; case 'w': value='ㅈ'; return true; case 'W': value='ㅉ'; return true;
            case 'c': case 'C': value='ㅊ'; return true; case 'z': case 'Z': value='ㅋ'; return true; case 'x': case 'X': value='ㅌ'; return true;
            case 'v': case 'V': value='ㅍ'; return true; case 'g': case 'G': value='ㅎ'; return true;
            case 'k': case 'K': value='ㅏ'; return true; case 'o': value='ㅐ'; return true; case 'O': value='ㅒ'; return true;
            case 'i': case 'I': value='ㅑ'; return true; case 'j': case 'J': value='ㅓ'; return true; case 'p': value='ㅔ'; return true; case 'P': value='ㅖ'; return true;
            case 'u': case 'U': value='ㅕ'; return true; case 'h': case 'H': value='ㅗ'; return true; case 'y': case 'Y': value='ㅛ'; return true;
            case 'n': case 'N': value='ㅜ'; return true; case 'b': case 'B': value='ㅠ'; return true; case 'm': case 'M': value='ㅡ'; return true;
            case 'l': case 'L': value='ㅣ'; return true;
            default: value='\0'; return false;
        }
    }
}
