using Boxal.Game.Leaderboard;
using Boxal.Game.Audio;
using Boxal.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 홈 씬 컨트롤러. 탭 페이저 전환·하이라이트, PLAY 진입, 리더보드 지연 로딩,
    /// 닉네임 최초 입력, UserInfo 갱신을 담당한다.
    /// </summary>
    /// <remarks>
    /// 페이지 인덱스는 인스펙터의 *PageIndex 필드로 지정한다(패널을 추가하면 순서가 통째로 밀리므로
    /// 코드에 박지 않는다). 최종 배치 계획은 <c>Docs/MetaProgression_Design.md</c> 4절 참고.
    /// </remarks>
    public class HomeManager : MonoBehaviour
    {
        #region Variables
        [Header("Pager")]
        [SerializeField] private UiPager pager;

        [Header("Tabs (인덱스 순서: 0=Settings, 1=Main, 2=Leaderboard)")]
        [Tooltip("탭 버튼. 누르면 해당 페이지로 이동. 배열 순서 = 페이지 인덱스.")]
        [SerializeField] private Button[] tabButtons;
        [Tooltip("각 탭의 Activated(선택 강조) 오브젝트. tabButtons와 같은 순서/길이. 현재 페이지의 것만 켜진다.")]
        [SerializeField] private GameObject[] tabActivatedObjects;

        [Header("Play")]
        [SerializeField] private Button playButton;
        [Tooltip("PLAY 시 로드할 씬 이름. Build Settings에 등록돼 있어야 함.")]
        [SerializeField] private string playSceneName = "PlayScene";

        [Header("Leaderboard (지연 로딩)")]
        [SerializeField] private LeaderboardPanelUI leaderboardPanel;
        [Tooltip("리더보드 페이지 인덱스(기본 2). 이 페이지를 처음 볼 때만 로드한다.")]
        [SerializeField] private int leaderboardPageIndex = 2;

        [Header("Settings (페이지 진입 시 재동기화)")]
        [Tooltip("설정 패널. 진동 토글은 퍼즈 팝업에도 있어서, 페이지에 들어올 때마다 현재 값으로 다시 맞춘다.")]
        [SerializeField] private SettingsPanelUI settingsPanel;
        [Tooltip("설정 페이지 인덱스(기본 0).")]
        [SerializeField] private int settingsPageIndex = 0;

        [Header("Unlock (페이지 진입 시 최근 해금 카드로 스크롤)")]
        [Tooltip("업그레이드 해금 패널. 들어올 때마다 상태를 다시 그리고 스크롤을 맞춘다.")]
        [SerializeField] private UnlockPanelUI unlockPanel;
        [Tooltip("해금 페이지 인덱스.")]
        [SerializeField] private int unlockPageIndex = 1;

        [Header("User Info")]
        [SerializeField] private UserInfoUI userInfo;

        [Header("Nickname (선택)")]
        [Tooltip("닉네임 미설정 시 최초 1회 강제 노출할 팝업. 없으면 비워도 됨.")]
        [SerializeField] private NicknamePopupUI nicknamePopup;

        private bool leaderboardLoaded;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            // 탭 버튼 → 해당 페이지로 이동
            if (tabButtons != null)
            {
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    int index = i; // 클로저 캡처 주의
                    if (tabButtons[i] != null)
                        tabButtons[i].onClick.AddListener(() => GoTo(index));
                }
            }

            if (playButton != null)
                playButton.onClick.AddListener(OnPlay);
        }

        private void OnEnable()
        {
            // OnEnable은 모든 Start보다 먼저라, 페이저가 Start에서 발생시키는 초기 PageChanged를 받는다.
            if (pager != null)
                pager.PageChanged += OnPageChanged;
        }

        private void OnDisable()
        {
            if (pager != null)
                pager.PageChanged -= OnPageChanged;
        }

        private void OnDestroy()
        {
            // 구독은 Start에서 1회만 하므로 해제도 파괴 시점에 맞춘다(OnDisable에서 풀면 재활성 때 되살아나지 않는다).
            if (nicknamePopup != null)
                nicknamePopup.NameConfirmed -= OnNameConfirmed;
        }

        private void Start()
        {
            // 게임 중 퍼즈에서 건 음소거는 그 판 한정이다. 홈으로 나오면 항상 소리가 켜진 상태로 시작한다.
            // (BGM보다 먼저 풀어야 홈 BGM이 처음부터 들린다.)
            if (SoundManager.InstanceExist)
                SoundManager.Instance.Muted = false;

            SoundManager.Instance?.PlayBgm(BgmId.Home);

            UpdateTabHighlight(pager != null ? pager.CurrentPage : 1);

            if (nicknamePopup != null)
            {
                // 이름이 확정되면 UserInfo(닉네임/순위)를 다시 그린다.
                nicknamePopup.NameConfirmed += OnNameConfirmed;
                nicknamePopup.ShowIfNeeded();
            }

            if (userInfo != null)
                userInfo.Refresh();
        }
        #endregion

        #region Custom Methods
        /// <summary>닉네임 확정 직후 UserInfo 표시를 갱신한다.</summary>
        private void OnNameConfirmed()
        {
            if (userInfo != null)
                userInfo.Refresh();
        }

        private void GoTo(int index)
        {
            if (pager != null)
                pager.GoToPage(index);
        }

        private void OnPageChanged(int index)
        {
            UpdateTabHighlight(index);

            // 설정 페이지는 들어올 때마다 현재 값으로 표시를 다시 맞춘다(퍼즈 팝업에서 바뀌었을 수 있다).
            if (index == settingsPageIndex && settingsPanel != null)
                settingsPanel.SyncFromCurrent();

            // 해금 페이지는 들어올 때마다 가장 최근에 해금된 카드가 보이도록 스크롤을 다시 맞춘다
            // (누적 처치 수 자체는 홈에서 변하지 않지만, 지난번에 스크롤해둔 위치가 남아 있다).
            if (index == unlockPageIndex && unlockPanel != null)
                unlockPanel.Refresh();

            // 리더보드 페이지를 처음 볼 때만 네트워크 로드(지연 로딩).
            if (index == leaderboardPageIndex && !leaderboardLoaded && leaderboardPanel != null)
            {
                leaderboardLoaded = true;
                leaderboardPanel.Refresh();
            }
        }

        /// <summary>현재 페이지의 Activated만 켜고 나머지는 끈다.</summary>
        private void UpdateTabHighlight(int index)
        {
            if (tabActivatedObjects == null)
                return;
            for (int i = 0; i < tabActivatedObjects.Length; i++)
                if (tabActivatedObjects[i] != null)
                    tabActivatedObjects[i].SetActive(i == index);
        }

        /// <summary>
        /// PLAY. 스태미나를 <see cref="Stamina.PlayCost"/>만큼 쓰고 진입한다.
        /// 부족하면 씬을 로드하지 않으며, 안내는 <see cref="Stamina.NotEnoughForPlay"/>를
        /// 구독한 쪽(광고 팝업)이 맡는다 — 여기서 팝업을 참조하지 않으므로 씬 배선이 필요 없다.
        /// </summary>
        private void OnPlay()
        {
            if (!Stamina.TryConsume())
                return;

            SceneFader.LoadScene(playSceneName);
        }
        #endregion
    }
}
