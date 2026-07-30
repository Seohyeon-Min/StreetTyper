using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public Image hpFill;

    public GameObject defIcon;
    public TextMeshProUGUI defText;

    public Color normalColor = Color.green;
    public Color defenseColor = Color.gray;

    public void UpdateUI(int currentHP, int maxHP, int defense)
    {
        gameObject.SetActive(true);

        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP;
        }

        if (hpText != null)
        {
            hpText.text = currentHP + " / " + maxHP;
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