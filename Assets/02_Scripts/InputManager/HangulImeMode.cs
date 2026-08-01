using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Windows IME의 한글/영문 변환 모드를 직접 지정한다.
///
/// Unity가 제어할 수 있는 건 "IME 조합을 켜는지"까지다(<see cref="Input.imeCompositionMode"/> /
/// <c>Keyboard.SetIMEEnabled</c>). 한글이냐 영문이냐 하는 **변환 모드**는 IME 자신의 상태라
/// Unity API로는 건드릴 수 없고 IMM32로만 바꿀 수 있다. 이게 없으면 플레이어의 IME가 영문으로
/// 남아 있을 때 ASCII가 들어와 <c>InputManager.IsHangul</c> 필터에 걸리고, 결국 사람이 직접
/// 한/영을 눌러야 게임이 시작된다.
///
/// **양방향이어야 한다.** 한글로 켜는 것만 있고 되돌리는 짝이 없으면, 영어 모드로 바꿔도 OS IME는
/// 한글 변환 상태로 남아 친 글자가 한글로 조합되고 라틴 필터에 걸려 통째로 사라진다.
///
/// 한/영 핫키를 흉내내는 <c>ImmSimulateHotKey</c> 방식은 과거에 시도했다 효과가 없어 되돌렸다.
/// 여기서는 핫키를 흉내내지 않고 변환 상태를 직접 쓰는 <c>ImmSetConversionStatus</c>와
/// 열림 상태를 쓰는 <c>ImmSetOpenStatus</c>를 쓴다(한국어 IME에서 열림/닫힘이 곧 한글/영문이다).
/// </summary>
public static class HangulImeMode
{
    // 실패는 조용히 넘기지 않되, 매 턴/매 입력마다 재시도하므로 로그는 첫 번째 실패만 남긴다.
    private static bool _loggedFailure;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // IME_CMODE_NATIVE = 한글 입력 모드. IME_CMODE_FULLSHAPE(0x0008)는 전각 문자가 되므로 넣지 않는다.
    private const uint ImeCmodeNative = 0x0001;

    // IME_CMODE_ALPHANUMERIC = 0. 영문 그대로 통과시키는 모드다.
    private const uint ImeCmodeAlphanumeric = 0x0000;

    private const uint ImeSmodeNone = 0x0000;

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    [DllImport("imm32.dll")]
    private static extern bool ImmSetOpenStatus(IntPtr hIMC, bool open);

    [DllImport("imm32.dll")]
    private static extern bool ImmSetConversionStatus(IntPtr hIMC, uint conversion, uint sentence);
#endif

    /// <summary>
    /// 활성 창의 IME를 열고 한글 모드로 맞춘다. **IME가 활성화된 뒤에** 부를 것 - Unity가 IME를
    /// 꺼둔 상태(<c>SetIMEEnabled(false)</c>)에서는 창에 IME 컨텍스트가 붙어 있지 않아
    /// <c>ImmGetContext</c>가 아무것도 주지 않는다.
    /// </summary>
    /// <returns>한글 모드로 맞추는 데 성공했는지. Windows가 아니면 항상 false.</returns>
    public static bool Force()
    {
        return SetHangul(true);
    }

    /// <summary>IME를 영문 모드로 되돌린다. 영어판에서 한글이 조합되는 걸 막는 짝이다.</summary>
    public static bool SetAlphanumeric()
    {
        return SetHangul(false);
    }

    /// <summary>
    /// 활성 창의 IME 변환 모드를 지정한다. **IME가 활성화된 뒤에** 부를 것 - Unity가 IME를
    /// 꺼둔 상태(<c>SetIMEEnabled(false)</c>)에서는 창에 IME 컨텍스트가 붙어 있지 않아
    /// <c>ImmGetContext</c>가 아무것도 주지 않는다.
    /// </summary>
    public static bool SetHangul(bool hangul)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        var window = GetActiveWindow();
        if (window == IntPtr.Zero)
        {
            LogFailureOnce("활성 창을 찾지 못했다(GetActiveWindow == 0). 창이 포커스를 잃은 상태로 보인다.");
            return false;
        }

        var context = ImmGetContext(window);
        if (context == IntPtr.Zero)
        {
            // IME가 이 창에 붙어 있지 않다. 조합이 아직 활성화되지 않았거나(EnableInput 전),
            // OS에 한국어 IME가 선택되어 있지 않은 경우다.
            LogFailureOnce("IME 컨텍스트가 없다(ImmGetContext == 0). IME 활성화 전이거나 한국어 IME가 선택되지 않았다.");
            return false;
        }

        try
        {
            // 한국어 IME에서는 열림/닫힘이 곧 한글/영문이라 변환 모드와 함께 맞춰준다.
            // 켤 때는 열고 나서 모드를 쓰고, 끌 때는 모드를 먼저 쓰고 닫는다 -
            // 닫힌 컨텍스트에 변환 모드를 쓰면 무시하는 IME가 있다.
            if (hangul)
            {
                ImmSetOpenStatus(context, true);

                if (ImmSetConversionStatus(context, ImeCmodeNative, ImeSmodeNone))
                    return true;
            }
            else
            {
                ImmSetConversionStatus(context, ImeCmodeAlphanumeric, ImeSmodeNone);

                if (ImmSetOpenStatus(context, false))
                    return true;
            }

            LogFailureOnce("IMM32 호출이 실패했다. 사용 중인 IME가 변환 모드 설정을 거부했을 수 있다.");
            return false;
        }
        finally
        {
            ImmReleaseContext(window, context);
        }
#else
        LogFailureOnce("Windows가 아니라 IME 변환 모드를 강제할 수 없다. 플레이어가 직접 한/영을 눌러야 한다.");
        return false;
#endif
    }

    private static void LogFailureOnce(string reason)
    {
        if (_loggedFailure)
            return;

        _loggedFailure = true;
        Debug.LogWarning($"HangulImeMode: 한글 입력 모드를 강제하지 못했다 - {reason} " +
                         "플레이어가 한/영 키를 직접 눌러야 타이핑이 시작된다. (이후 재시도 로그는 생략된다)");
    }
}
