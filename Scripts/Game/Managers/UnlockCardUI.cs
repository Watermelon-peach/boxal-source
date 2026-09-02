using Boxal.Game.Growth;
using TMPro;
using UnityEngine;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 해금 패널의 카드 한 장. 누적 처치 수가 <see cref="UpgradeSO.unlockKills"/>에 도달했는지에 따라
    /// 잠금 표시(QuestionMark)와 해금 표시(Card/Label)를 맞바꾼다.
    /// </summary>
    /// <remarks>
    /// 아이콘·이름·등급은 씬에 손으로 채워둔 값을 그대로 쓴다(스크립트가 덮어쓰지 않는다).
    /// 예외는 요구 처치 수 하나뿐이다 — 이 값은 밸런싱으로 세 번 바뀐 이력이 있어서
    /// (2200 → 1000 → 500킬), 씬에 적은 숫자와 <c>.asset</c>이 갈라지면 조용히 틀린 숫자가 뜬다.
    /// 그래서 표시할 때 애셋에서 다시 읽는다. 끄고 싶으면 driveRequireKillText를 해제할 것.
    /// <para/>
    /// 자식 뷰는 이름으로 수집한다(<see cref="UpgradeHistoryUI"/>와 같은 방식). 규격:
    /// <code>
    /// CardFrame_*        (이 컴포넌트가 붙는 루트)
    ///   ├ QuestionMark   잠김 표시
    ///   ├ RequireKill    요구 처치 수 (TMP, 항상 표시)
    ///   ├ Card           해금 아이콘 (기본 비활성)
    ///   └ Label          해금 이름/등급 (기본 비활성)
    /// </code>
    /// </remarks>
    public class UnlockCardUI : MonoBehaviour
    {
        #region Variables
        [Tooltip("이 카드가 나타내는 업그레이드. 해금 여부와 요구 처치 수를 여기서 읽는다.")]
        [SerializeField] private UpgradeSO upgrade;

        [Tooltip("체크하면 RequireKill 텍스트를 애셋의 unlockKills로 다시 쓴다. " +
                 "해제하면 씬에 적어둔 문자열을 그대로 둔다.")]
        [SerializeField] private bool driveRequireKillText = true;

        private GameObject questionMark;
        private GameObject card;
        private GameObject label;
        private TextMeshProUGUI requireKillText;
        #endregion

        #region Properties
        /// <summary>이 카드가 해금됐는지. 업그레이드를 안 물려두면 잠긴 것으로 본다.</summary>
        public bool IsUnlocked => upgrade != null && upgrade.IsUnlocked;

        /// <summary>해금에 필요한 누적 처치 수. "가장 최근에 해금된 카드"를 고를 때 기준이 된다.</summary>
        public int UnlockKills => upgrade != null ? upgrade.unlockKills : 0;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            CollectViews();
        }
        #endregion

        #region Custom Methods
        private void CollectViews()
        {
            questionMark = FindChild("QuestionMark");
            card = FindChild("Card");
            label = FindChild("Label");

            Transform requireKillTf = transform.Find("RequireKill");
            if (requireKillTf != null)
                requireKillText = requireKillTf.GetComponent<TextMeshProUGUI>();

            // 자식 이름이 규격에서 어긋나면 카드가 영영 잠긴 것처럼 보인다. 조용히 넘어가면 찾기 어렵다.
            if (questionMark == null || card == null || label == null)
                Debug.LogWarning($"[UnlockCardUI] '{name}'의 자식 규격이 맞지 않는다 " +
                                 "(QuestionMark / Card / Label 이름 확인).", this);
            if (upgrade == null)
                Debug.LogWarning($"[UnlockCardUI] '{name}'에 업그레이드가 안 물려 있어 항상 잠김으로 표시된다.", this);
        }

        private GameObject FindChild(string childName)
        {
            Transform tf = transform.Find(childName);
            return tf != null ? tf.gameObject : null;
        }

        /// <summary>현재 누적 처치 수 기준으로 잠금/해금 표시를 다시 맞춘다.</summary>
        public void Refresh()
        {
            bool unlocked = IsUnlocked;

            if (questionMark != null)
                questionMark.SetActive(!unlocked);
            if (card != null)
                card.SetActive(unlocked);
            if (label != null)
                label.SetActive(unlocked);

            if (driveRequireKillText && requireKillText != null && upgrade != null)
                requireKillText.text = upgrade.unlockKills.ToString();
        }
        #endregion
    }
}
