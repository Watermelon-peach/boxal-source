using Boxal.Util;
using TMPro;
using UnityEngine;
namespace Boxal.Game.UI
{
    public class UiManager : Singleton<UiManager>
    {
        [Header("Round")]
        public TextMeshProUGUI roundUi;
        public TextMeshProUGUI timerUi;
        [Tooltip("현재 접촉 데미지 HUD (CurrentRecords/Dmg/dmgTxt). 예: DMG 2")]
        public TextMeshProUGUI dmgUi;
        //boss alert

        [Header("Points")]
        [Tooltip("인게임 점수 HUD (CurrentRecords/Points)")]
        public TextMeshProUGUI pointsUi;

        [Header("Gold")]
        [Tooltip("인게임 획득 골드 HUD (HUD/ResourceBar_Golds/Text_Value). 이번 판 누적(GameManager.RunGold)만 보여준다 " +
                 "— 계정 잔액(Gold.Balance)은 게임오버에서 반영되므로 판중에는 안 바뀐다.")]
        public TextMeshProUGUI goldUi;

        /// <summary>인게임 점수 HUD 갱신. 999,999,999 초과 시 축약(FormatNumber), 이하면 쉼표 표기.</summary>
        public void SetPoints(long points)
        {
            if (pointsUi == null)
                return;
            string num = points > 999_999_999L ? NumberUtil.FormatNumber(points) : NumberUtil.FormatComma(points);
            pointsUi.text = num + " Points";
        }

        /// <summary>인게임 획득 골드 HUD 갱신(이번 판 누적, 계정 잔액이 아니다).</summary>
        public void SetGold(long runGold)
        {
            if (goldUi == null)
                return;
            goldUi.text = NumberUtil.FormatNumber(runGold);
        }
    }

}
