using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    public TextMeshProUGUI hpText;
    public Image hpFill;

    [Tooltip("게이지 채우기는 UIStyle이 담당한다(fillAmount).")]
    public UIStyle.UIStyle uiStyleFill;

    public GameObject defIcon;
    public TextMeshProUGUI defText;

    public Color normalColor = Color.green;
    public Color defenseColor = Color.gray;

    public void UpdateUI(int currentHP, int maxHP, int defense)
    {
        gameObject.SetActive(true);

        if (hpText != null)
        {
            hpText.text = currentHP + " / " + maxHP;
        }

        if (uiStyleFill != null)
        {
            float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
            uiStyleFill.SetFillAmount(ratio);
        }

        if (defense > 0)
        {
            if (defIcon != null) defIcon.SetActive(true);
            if (defText != null)
            {
                defText.gameObject.SetActive(true);
                defText.text = defense.ToString();
            }
            if (hpFill != null) hpFill.color = defenseColor;
        }
        else
        {
            if (defIcon != null) defIcon.SetActive(false);
            if (defText != null) defText.gameObject.SetActive(false);
            if (hpFill != null) hpFill.color = normalColor;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}