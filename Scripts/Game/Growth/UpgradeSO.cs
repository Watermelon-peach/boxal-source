using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>
    /// 런 한정 로그라이트 업그레이드의 베이스.
    /// 표시 정보 + 추첨 설정은 공통으로 두고, 적용 로직은 축별 서브클래스가 구현한다.
    /// 데이터 주도라 값 튜닝은 .asset 인스턴스에서, 새 효과 축 추가만 클래스 추가로 처리.
    /// </summary>
    public abstract class UpgradeSO : ScriptableObject
    {
        #region Variables
        [Header("표시 정보")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("추첨 설정")]
        public UpgradeGrade grade = UpgradeGrade.Common;
        [Tooltip("중복 획득 가능 여부")]
        public bool stackable = true;
        [Tooltip("추첨 가중치. 클수록 자주 등장")]
        [Min(0f)] public float weight = 1f;

        [Header("해금")]
        [Tooltip("이 업그레이드가 카드 풀에 등장하기 시작하는 누적 처치 수(PlayerStats.TotalKills). " +
                 "0이면 처음부터 해금돼 있다.")]
        [Min(0)] public int unlockKills = 0;

        [Header("자동 선택")]
        [Tooltip("자동 선택이 켜져 있을 때의 선호도. 클수록 먼저 고른다. " +
                 "값은 밸런스 시뮬(Tools/BalanceSim)의 greedy 우선순위를 옮긴 것이다.")]
        public int autoPriority = 0;
        [Tooltip("체크하면 '한방컷' 상태일 때 autoPriority를 무시하고 최우선으로 고른다(회복류).")]
        public bool autoEmergencyPick = false;
        #endregion

        #region Custom Methods
        /// <summary>선택 시 효과 적용. 런 한정이라 세이브 불필요.</summary>
        public abstract void Apply();

        /// <summary>누적 처치 수가 <see cref="unlockKills"/>에 도달해 해금됐는지.</summary>
        /// <remarks>
        /// 해금 상태를 따로 저장하지 않는다 — 누적 처치 수에서 파생되는 값이라
        /// 저장·동기화 버그가 생길 여지가 없다.
        /// </remarks>
        public bool IsUnlocked => unlockKills <= 0 || PlayerStats.TotalKills >= unlockKills;

        /// <summary>
        /// 추첨 후보로 제시 가능한지. <b>해금 판정은 여기서 강제되며 서브클래스가 우회할 수 없다.</b>
        /// 상한 도달 같은 종류별 조건은 <see cref="CanOfferCore"/>를 재정의해서 넣는다.
        /// </summary>
        public bool CanOffer() => IsUnlocked && CanOfferCore();

        /// <summary>종류별 제시 조건. 상한 도달 등으로 무의미하면 false.</summary>
        protected virtual bool CanOfferCore() => true;
        #endregion
    }
}
