using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 최초 1회만 뜨는 <b>사전 조작 튜토리얼</b>. 본게임이 시작되기 전에 안전한 연습 구간을 주고,
    /// 플레이어가 직접 Game Start를 눌렀을 때 비로소 1라운드가 시작된다.
    /// <list type="number">
    /// <item>점프 설명 — 버튼을 <b>홀드해서 충전하고 떼면</b> 점프한다는 걸 패널로 안내. (Next)</item>
    /// <item>막기 설명 — 적이 머리 위에 왔을 때 막기 버튼을 누르면 <b>튕겨낼 수 있다</b>는 걸 안내. (Next)</item>
    /// <item>연습 — 설명 패널과 Dim이 전부 사라지고 화면이 그대로 보인다. 더미를 상대로
    /// 점프·막기를 자유롭게 연습한 뒤 둘 중 하나를 고른다.</item>
    /// <item>선택 — <b>Read Again</b>(1단계부터 다시, 씬은 안 건드리고 플로우만 되돌림)
    /// 또는 <b>Game Start</b>(튜토리얼 종료, 1라운드 시작).</item>
    /// </list>
    /// 설명 단계에서 패널이 화면을 가리는 게 거슬리지 않도록, <b>연습은 별도 단계로 분리</b>했다 —
    /// 설명을 다 읽은 뒤에야 아무것도 안 가린 화면에서 연습하게 된다.
    /// </summary>
    /// <remarks>
    /// ★<b>라운드가 아직 시작되지 않은 상태에서 돈다.</b> <see cref="GameManager"/>가 첫 판에 한해
    /// <see cref="RoundManager.StartRound"/> 호출을 이 튜토리얼이 끝날 때까지 미룬다.
    /// 예전처럼 라운드가 굴러가는 채로 안내를 띄우면, 읽는 동안 무기가 몹을 깨서 XP가 쌓이고
    /// <b>레벨업 카드가 안내 위로 튀어나온다</b>(<c>Boxmon.BreakBox</c>가 처치 시 킬·점수·XP를 한 번에 준다).
    /// 그래서 "미루기"가 이 기능의 핵심이고, 덕분에 타이머 정지 같은 장치가 전부 필요 없어졌다.
    /// <para/>
    /// ★<b>연습용 더미는 HP를 아주 크게 준다</b>(<see cref="dummyHp"/>). 무기에 깨지지 않아야
    /// 보상·XP가 새지 않고, 막기는 어차피 죽이는 게 아니라 위로 튕겨내는 것이라 몇 번이든 반복된다.
    /// 끝낼 때는 <see cref="SpawnManager.Despawn"/>으로 조용히 치운다(처치 경로를 안 타므로 보상 0).
    /// <para/>
    /// ★<b>화면은 스포트라이트 + 씬 패널의 조합이다.</b>
    /// 어둡게 덮고 대상 버튼만 뚫는 Dim 4조각은 버튼 위치에서 계산되는 것이라 런타임 생성이고,
    /// 그 위에 올라가는 <b>설명 문구·참고 이미지·버튼은 사용자가 씬에서 직접 만들어 배치한다</b>
    /// (<see cref="jumpPanel"/>/<see cref="parryPanel"/>). 패널은 자동으로 Dim 위로 올려주므로
    /// 계층 어디에 두든 가려지지 않는다.
    /// <para/>
    /// ★패널이나 버튼이 연결되지 않았으면 튜토리얼을 통째로 건너뛰고 바로 게임이 시작된다
    /// (배선 전에도 게임이 멀쩡히 돌아가야 하므로).
    /// </remarks>
    public class TutorialHintUI : MonoBehaviour
    {
        private enum Step { None, Jump, Parry, Practice }

        #region Variables
        [Header("전체")]
        [Tooltip("Jump/Parry 패널·연습 버튼을 담은 루트(예: \"Tutorial\"). 튜토리얼이 끝나면 " +
                 "이 오브젝트 전체를 비활성화한다. 비워두면 개별 패널만 끈다(안전하게 동작은 하지만, " +
                 "이 루트 아래 나중에 새로 추가한 자식은 안 꺼질 수 있다).")]
        [SerializeField] private GameObject tutorialRoot;

        [Header("Step 1 - Jump 설명")]
        [Tooltip("점프 버튼의 RectTransform. 스포트라이트 구멍 위치로 쓴다.")]
        [SerializeField] private RectTransform jumpButtonRect;
        [Tooltip("점프 설명 패널(사용자 제작). 문구·참고 이미지를 원하는 대로 배치하면 된다. 평소 비활성.")]
        [SerializeField] private GameObject jumpPanel;
        [Tooltip("다음 단계(막기 설명)로 넘어가는 버튼. 이 패널 안에 두면 된다.")]
        [SerializeField] private Button jumpNextButton;

        [Header("Step 2 - Block 설명")]
        [Tooltip("막기 버튼의 RectTransform.")]
        [SerializeField] private RectTransform parryButtonRect;
        [Tooltip("막기 설명 패널(사용자 제작). 평소 비활성.")]
        [SerializeField] private GameObject parryPanel;
        [Tooltip("다음 단계(연습)로 넘어가는 버튼.")]
        [SerializeField] private Button parryNextButton;

        [Header("Step 3 - 연습 (Dim·설명 패널 없음)")]
        [Tooltip("연습 단계에서 보일 UI(선택). 버튼 2개만 있다면 굳이 안 만들어도 된다 — " +
                 "비워두면 Game Start 버튼 자신을 켜고 끈다(Read Again은 같이 켜고 끈다).")]
        [SerializeField] private GameObject practicePanel;
        [Tooltip("튜토리얼을 처음(점프 설명)부터 다시 본다. PlayScene은 그대로 두고 플로우만 되돌린다 — " +
                 "인트로 카메라·플레이어 위치는 안 건드린다.")]
        [SerializeField] private Button readAgainButton;
        [Tooltip("연습을 마치고 1라운드를 시작하는 버튼.")]
        [SerializeField] private Button gameStartButton;

        [Header("연습 중 가릴 HUD")]
        [Tooltip("라운드 전이라 의미 없는 값(라운드 수·타이머·데미지·점수)이 튜토리얼 내내 안 보이게 끈다. " +
                 "UiManager가 쓰는 CurrentRecords 컨테이너. 비워두면 안 건드린다.")]
        [SerializeField] private GameObject currentRecordsHud;

        [Header("연습용 더미")]
        [Tooltip("연습용 더미의 HP. 무기에 깨지면 보상·XP가 새므로 아주 크게 준다.")]
        [SerializeField] private long dummyHp = 999999999L;
        [Tooltip("더미가 사라졌는지 확인하는 주기(초). 없으면 다시 띄운다.")]
        [SerializeField] private float dummyCheckInterval = 0.5f;

        [Header("Spotlight")]
        [Tooltip("구멍 밖을 덮는 어둠의 색/농도.")]
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.78f);
        [Tooltip("대상 버튼 사각형 바깥으로 구멍을 얼마나 더 넉넉하게 낼지(캔버스 단위).")]
        [SerializeField] private Vector2 holePadding = new Vector2(20f, 20f);
        [Tooltip("Dim의 정렬 순서. 설명 패널은 이보다 1 높게 자동 설정되어 항상 Dim 위에 보인다.")]
        [SerializeField] private int spotlightSortingOrder = 500;
        [Tooltip("사라질 때 페이드 시간(초).")]
        [SerializeField] private float fadeDuration = 0.3f;
        #endregion

        #region Variables (runtime)
        private static TutorialHintUI instance;

        private RectTransform canvasRect;
        private RectTransform spotlightRoot;
        private CanvasGroup spotlightGroup;
        private RectTransform dimTop, dimBottom, dimLeft, dimRight;

        private Step current = Step.None;
        private Action onFinished;
        private Boxmon dummy;
        private Coroutine dummyRoutine;
        private Coroutine fadeRoutine;
        #endregion

        #region Properties
        /// <summary>
        /// 이번 판에 튜토리얼을 띄워야 하는지. <see cref="GameManager"/>가 이 값으로
        /// 라운드 시작을 미룰지 결정한다. 배선이 덜 됐으면 false라 게임이 평소대로 시작된다.
        /// </summary>
        public static bool WillRun =>
            instance != null
            && !PlayerStats.TutorialCompleted
            && instance.jumpPanel != null
            && instance.parryPanel != null
            && instance.jumpNextButton != null
            && instance.parryNextButton != null
            && instance.readAgainButton != null
            && instance.gameStartButton != null;

        /// <summary>
        /// 연습 단계에서 켜고 끌 대상. <see cref="practicePanel"/>을 안 만들었으면
        /// Game Start 버튼 자신을 그 자리에 쓴다(패널 없이 버튼 2개만 있어도 되도록).
        /// </summary>
        private GameObject PracticeTarget =>
            practicePanel != null ? practicePanel
            : gameStartButton != null ? gameStartButton.gameObject
            : null;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            instance = this;

            Canvas canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            BuildSpotlight();

            if (jumpPanel != null)
                jumpPanel.SetActive(false);
            if (parryPanel != null)
                parryPanel.SetActive(false);
            if (PracticeTarget != null)
                PracticeTarget.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
        #endregion

        #region Static API
        /// <summary>
        /// 인트로 연출이 끝난 뒤 <see cref="GameManager"/>가 부른다.
        /// <paramref name="onFinished"/>는 마지막 Start 버튼을 눌렀을 때 실행된다(= 1라운드 시작).
        /// </summary>
        public static void Begin(Action onFinished)
        {
            if (instance == null || !WillRun)
            {
                // 배선이 없으면 튜토리얼 없이 바로 게임을 시작한다(안전장치).
                onFinished?.Invoke();
                return;
            }
            instance.BeginInternal(onFinished);
        }
        #endregion

        #region Custom Methods - 진행
        private void BeginInternal(Action finishedCallback)
        {
            onFinished = finishedCallback;

            // ★씬에서 Tutorial 루트 자체가 꺼진 채로 저장돼도 여기서 강제로 켠다.
            // 부모가 꺼져 있으면 자식에 SetActive(true)를 해도 화면에 안 나타나므로(activeInHierarchy),
            // 개별 패널만 열심히 켜봤자 이 한 줄이 없으면 튜토리얼이 통째로 안 뜨는 사고가 난다.
            if (tutorialRoot != null)
                tutorialRoot.SetActive(true);

            // 연습 중에는 더미에 맞아도 아프지 않게 한다(라운드 전이라 목숨을 잃을 이유가 없다).
            if (Player.InstanceExist)
                Player.Instance.ForceInvulnerable = true;

            // 라운드 전이라 의미 없는 값(라운드/타이머/데미지/점수)을 튜토리얼이 끝날 때까지 가린다.
            if (currentRecordsHud != null)
                currentRecordsHud.SetActive(false);

            dummyRoutine = StartCoroutine(KeepDummyAlive());
            ShowExplainStep(Step.Jump);
        }

        /// <summary>
        /// 설명 단계(점프/막기)로 전환한다. 패널 하나만 켜고 스포트라이트를 그 버튼으로 옮긴다.
        /// </summary>
        /// <remarks>
        /// ★<b>이 단계는 게임을 멈춘다</b>(<c>Time.timeScale = 0</c>). 읽기만 하는 단계라 조작이
        /// 반응할 필요가 없고, 오히려 캐릭터·더미가 화면에서 계속 움직이면 집중을 방해한다.
        /// 연습 단계(<see cref="ShowPracticeStep"/>)에서 다시 풀어준다 — 거기서는 실제로 조작해야
        /// 하므로 <see cref="ChargeJump"/>의 충전(Time.deltaTime 기반)과 물리가 살아있어야 한다.
        /// UI 버튼 클릭은 timeScale과 무관하게 동작하므로 Next를 누르는 데는 지장이 없다.
        /// </remarks>
        private void ShowExplainStep(Step step)
        {
            current = step;
            Time.timeScale = 0f;

            bool isJump = step == Step.Jump;
            RectTransform target = isJump ? jumpButtonRect : parryButtonRect;
            GameObject panel = isJump ? jumpPanel : parryPanel;
            GameObject otherPanel = isJump ? parryPanel : jumpPanel;
            Button button = isJump ? jumpNextButton : parryNextButton;

            if (otherPanel != null)
                otherPanel.SetActive(false);
            if (PracticeTarget != null)
                PracticeTarget.SetActive(false);

            if (target != null && spotlightRoot != null)
            {
                LayoutSpotlight(target);
                spotlightRoot.gameObject.SetActive(true);
                spotlightGroup.alpha = 1f;
            }

            if (panel != null)
            {
                panel.SetActive(true);
                LiftAboveDim(panel);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (isJump)
                    button.onClick.AddListener(() => ShowExplainStep(Step.Parry));
                else
                    button.onClick.AddListener(ShowPracticeStep);
            }
        }

        /// <summary>
        /// 연습 단계로 전환한다. 설명 패널과 Dim을 전부 치워 화면을 안 가리고,
        /// Read Again(다시 보기) / Game Start 두 버튼만 띄운다.
        /// 더미는 <see cref="BeginInternal"/>부터 계속 떨어지고 있다.
        /// </summary>
        private void ShowPracticeStep()
        {
            current = Step.Practice;
            Time.timeScale = 1f; // 설명 단계에서 멈춰 둔 것을 여기서 푼다 — 실제로 조작해야 하므로.

            if (jumpPanel != null)
                jumpPanel.SetActive(false);
            if (parryPanel != null)
                parryPanel.SetActive(false);
            HideSpotlight();

            if (PracticeTarget != null)
                PracticeTarget.SetActive(true);

            if (readAgainButton != null)
            {
                readAgainButton.onClick.RemoveAllListeners();
                readAgainButton.onClick.AddListener(RestartFlow);
            }
            if (gameStartButton != null)
            {
                gameStartButton.onClick.RemoveAllListeners();
                gameStartButton.onClick.AddListener(Finish);
            }
        }

        /// <summary>
        /// Read Again. 튜토리얼 플로우만 점프 설명(1단계)로 되돌린다 — PlayScene은 그대로 두고
        /// (씬 리로드 없음), 인트로 카메라나 플레이어 위치도 안 건드린다. 무적·HUD 가림은
        /// 이미 켜져 있던 상태 그대로 이어간다. 더미는 연습하다 이상한 자리에 놓였을 수 있으니
        /// 지우고 새로 하나 띄운다(깨끗한 상태에서 다시 시작하는 느낌을 주려는 것).
        /// </summary>
        private void RestartFlow()
        {
            ResetDummy();
            ShowExplainStep(Step.Jump);
        }

        /// <summary>Start Game. 연습 구간을 정리하고 본게임(1라운드)을 시작시킨다.</summary>
        private void Finish()
        {
            if (current == Step.None)
                return;
            current = Step.None;
            Time.timeScale = 1f; // Practice에서 이미 풀려 있는 게 보통이지만, 1라운드가 멈춘 채 시작되면 안 되므로 방어적으로.

            PlayerStats.TutorialCompleted = true;

            if (jumpNextButton != null)
                jumpNextButton.onClick.RemoveAllListeners();
            if (parryNextButton != null)
                parryNextButton.onClick.RemoveAllListeners();
            if (readAgainButton != null)
                readAgainButton.onClick.RemoveAllListeners();
            if (gameStartButton != null)
                gameStartButton.onClick.RemoveAllListeners();

            // 튜토리얼 오브젝트 자체를 끈다. 루트를 안 만들었으면 개별 패널만 끈다(안전장치).
            if (tutorialRoot != null)
            {
                tutorialRoot.SetActive(false);
            }
            else
            {
                if (jumpPanel != null)
                    jumpPanel.SetActive(false);
                if (parryPanel != null)
                    parryPanel.SetActive(false);
                if (PracticeTarget != null)
                    PracticeTarget.SetActive(false);
            }

            StopDummy();

            // 얼티밋이 자기 몫으로 켜 둔 상태라면 여기서 끄지 않는다(덮어쓰기 방지).
            if (Player.InstanceExist
                && (!UltimateManager.InstanceExist || !UltimateManager.Instance.IsActive))
                Player.Instance.ForceInvulnerable = false;

            // 튜토리얼 내내 가려뒀던 라운드/타이머/데미지/점수 HUD를 되돌린다.
            if (currentRecordsHud != null)
                currentRecordsHud.SetActive(true);

            HideSpotlight(); // 연습 단계에서 이미 꺼져 있는 게 보통이지만, 방어적으로 한 번 더

            Action callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }
        #endregion

        #region Custom Methods - 연습용 더미
        /// <summary>
        /// 연습용 더미를 계속 유지한다. 막기는 죽이는 게 아니라 튕겨내는 것이라 보통은 그대로 살아 있고,
        /// 어떤 이유로 사라지면 다시 띄운다.
        /// </summary>
        private IEnumerator KeepDummyAlive()
        {
            var wait = new WaitForSeconds(dummyCheckInterval);
            while (true)
            {
                if (SpawnManager.InstanceExist
                    && (dummy == null || dummy.IsDead || !dummy.gameObject.activeInHierarchy))
                {
                    dummy = SpawnManager.Instance.Spawn(dummyHp);
                }
                yield return wait;
            }
        }

        /// <summary>
        /// 지금 있는 더미를 지우고 새로 하나 띄운다. <see cref="RestartFlow"/>(Read Again)가
        /// "깨끗하게 다시 시작"하는 느낌을 주려고 부른다. <see cref="KeepDummyAlive"/>의 주기적
        /// 점검과 달리 그 자리에서 즉시 처리한다.
        /// </summary>
        private void ResetDummy()
        {
            if (!SpawnManager.InstanceExist)
                return;
            if (dummy != null && !dummy.IsDead)
                SpawnManager.Instance.Despawn(dummy);
            dummy = SpawnManager.Instance.Spawn(dummyHp);
        }

        private void StopDummy()
        {
            if (dummyRoutine != null)
            {
                StopCoroutine(dummyRoutine);
                dummyRoutine = null;
            }
            // Despawn은 처치 경로(BreakBox)를 타지 않으므로 킬·점수·XP가 붙지 않는다.
            if (dummy != null && !dummy.IsDead && SpawnManager.InstanceExist)
                SpawnManager.Instance.Despawn(dummy);
            dummy = null;
        }
        #endregion

        #region Custom Methods - Spotlight
        private void BuildSpotlight()
        {
            if (canvasRect == null)
                return;

            var rootGo = new GameObject("TutorialSpotlight",
                typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            spotlightRoot = (RectTransform)rootGo.transform;
            spotlightRoot.SetParent(canvasRect, false);
            spotlightRoot.anchorMin = Vector2.zero;
            spotlightRoot.anchorMax = Vector2.one;
            spotlightRoot.pivot = new Vector2(0.5f, 0.5f);
            spotlightRoot.offsetMin = Vector2.zero;
            spotlightRoot.offsetMax = Vector2.zero;

            spotlightGroup = rootGo.GetComponent<CanvasGroup>();

            // 계층 위치와 무관하게 항상 HUD 위에 오도록 정렬 순서를 직접 잡는다.
            Canvas c = rootGo.GetComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = spotlightSortingOrder;

            dimTop = CreateDimPiece("DimTop");
            dimBottom = CreateDimPiece("DimBottom");
            dimLeft = CreateDimPiece("DimLeft");
            dimRight = CreateDimPiece("DimRight");

            rootGo.SetActive(false);
        }

        private RectTransform CreateDimPiece(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(spotlightRoot, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = go.GetComponent<Image>();
            img.color = dimColor;
            img.raycastTarget = true; // 구멍(대상 버튼) 밖의 다른 입력을 막는다
            return rt;
        }

        /// <summary>
        /// 사용자가 만든 패널을 Dim 위로 올린다. 패널이 계층 어디에 있든(SafeArea 안쪽이어도)
        /// 통하도록 형제 순서가 아니라 Canvas 정렬 순서로 처리한다.
        /// </summary>
        private void LiftAboveDim(GameObject panel)
        {
            Canvas c = panel.GetComponent<Canvas>();
            if (c == null)
                c = panel.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = spotlightSortingOrder + 1;

            // 자체 Canvas를 가지면 부모의 레이캐스터로는 입력이 안 잡히므로 같이 붙여준다.
            if (panel.GetComponent<GraphicRaycaster>() == null)
                panel.AddComponent<GraphicRaycaster>();
        }

        /// <summary>대상 버튼 사각형에 맞춰 Dim 4조각(구멍 프레임)을 다시 계산한다.</summary>
        private void LayoutSpotlight(RectTransform target)
        {
            Rect bounds = spotlightRoot.rect;

            Vector3[] corners = new Vector3[4]; // 0=BL, 1=TL, 2=TR, 3=BR
            target.GetWorldCorners(corners);
            Vector2 bl = spotlightRoot.InverseTransformPoint(corners[0]);
            Vector2 tr = spotlightRoot.InverseTransformPoint(corners[2]);

            float holeXMin = Mathf.Min(bl.x, tr.x) - holePadding.x;
            float holeXMax = Mathf.Max(bl.x, tr.x) + holePadding.x;
            float holeYMin = Mathf.Min(bl.y, tr.y) - holePadding.y;
            float holeYMax = Mathf.Max(bl.y, tr.y) + holePadding.y;

            SetPiece(dimTop, (bounds.xMin + bounds.xMax) * 0.5f, (holeYMax + bounds.yMax) * 0.5f,
                bounds.width, bounds.yMax - holeYMax);
            SetPiece(dimBottom, (bounds.xMin + bounds.xMax) * 0.5f, (bounds.yMin + holeYMin) * 0.5f,
                bounds.width, holeYMin - bounds.yMin);
            SetPiece(dimLeft, (bounds.xMin + holeXMin) * 0.5f, (holeYMin + holeYMax) * 0.5f,
                holeXMin - bounds.xMin, holeYMax - holeYMin);
            SetPiece(dimRight, (holeXMax + bounds.xMax) * 0.5f, (holeYMin + holeYMax) * 0.5f,
                bounds.xMax - holeXMax, holeYMax - holeYMin);
        }

        private static void SetPiece(RectTransform rt, float centerX, float centerY, float width, float height)
        {
            rt.anchoredPosition = new Vector2(centerX, centerY);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        /// <summary>
        /// 설명 단계 → 연습 단계로 넘어갈 때 Dim을 걷어낸다. 이미 꺼져 있으면 아무 일도 안 한다
        /// (Finish에서 방어적으로 한 번 더 불러도 안전하도록).
        /// </summary>
        private void HideSpotlight()
        {
            if (spotlightRoot == null || !spotlightRoot.gameObject.activeSelf)
                return;
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeOutSpotlight());
        }

        /// <summary>
        /// unscaled로 도는 건 방어적 조치다 — 이 구간은 게임을 멈추지 않지만,
        /// Start 직후 다른 요인으로 timeScale=0이 겹치면 scaled 페이드는 그 자리에서 얼어붙는다.
        /// </summary>
        private IEnumerator FadeOutSpotlight()
        {
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                spotlightGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            spotlightGroup.alpha = 1f; // 다음에 켤 때를 위해 되돌려 둔다
            fadeRoutine = null;
            spotlightRoot.gameObject.SetActive(false);
        }
        #endregion
    }
}
