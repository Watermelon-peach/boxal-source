using Boxal.Game.Audio;
using Boxal.Game.Feedback;
using Boxal.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 홈 씬 설정 페이지(페이저 인덱스 0): 볼륨 3종, 진동 토글, 사운드 크레딧 표기.
    /// 값 자체는 <see cref="SoundManager"/>/<see cref="HapticManager"/>가 PlayerPrefs로 들고 있으므로
    /// 이 클래스는 상태를 저장하지 않는다(표시와 입력만 담당).
    /// </summary>
    /// <remarks>
    /// ★<see cref="UiPager"/>는 모든 페이지를 항상 활성으로 두고 컨테이너만 좌우로 민다.
    /// 즉 OnEnable/OnDisable이 페이지 진입·이탈과 일치하지 않는다(씬 로드 때 1회씩만 발생).
    /// 그래서 초기화는 Start에서 1회, 디스크 저장은 "슬라이더에서 손을 뗀 순간"에 한다.
    /// </remarks>
    public class SettingsPanelUI : MonoBehaviour
    {
        #region Variables
        [Header("Volume Sliders")]
        [Tooltip("슬라이더 값은 0~1. 실제 볼륨은 곡선(t*t)을 거쳐 들어간다.")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Value Labels (선택)")]
        [Tooltip("슬라이더 위치를 %로 표시. 없으면 비워도 된다.")]
        [SerializeField] private TextMeshProUGUI masterValueText;
        [SerializeField] private TextMeshProUGUI bgmValueText;
        [SerializeField] private TextMeshProUGUI sfxValueText;

        [Header("Haptics")]
        [Tooltip("진동 on/off. 실기기에서만 실제로 울린다(에디터는 무반응이 정상).")]
        [SerializeField] private Toggle hapticToggle;

        [Header("Credits")]
        [Tooltip("크레딧 본문을 표시할 텍스트. ★LilitaOne SDF는 ASCII 96자만 구운 정적 아틀라스라 " +
                 "em dash(—)나 ©가 렌더되지 않는다. 본문은 LiberationSans SDF 사용을 권장.")]
        [SerializeField] private TextMeshProUGUI creditsText;

        [Tooltip("표기 문구. CC BY/MIT 항목은 표기가 법적 의무다. 원본은 Assets/Boxal/Sounds/CREDITS.md.")]
        [TextArea(8, 30)]
        [SerializeField] private string creditsBody = DefaultCredits;

        [Tooltip("MIT 전문(Assets/Boxal/Sounds/Karugamo_LICENSE.txt)을 넣으면 크레딧 뒤에 이어 붙는다. " +
                 "MIT는 '허가 문구를 사본에 포함'할 것을 요구하므로 빌드에서 볼 수 있어야 안전하다.")]
        [SerializeField] private TextAsset licenseFullText;

        [Header("Preview")]
        [Tooltip("Master/SFX 슬라이더에서 손을 뗄 때 1회 재생해 볼륨을 들려준다. " +
                 "UiClick(20)은 아직 클립이 없어 BoxmonBreak를 기본값으로 둔다.")]
        [SerializeField] private SoundId previewSfx = SoundId.BoxmonBreak;

        private enum VolumeTarget { Master, Bgm, Sfx }
        #endregion

        #region Unity Event Methods
        private void Start()
        {
            SetupVolumeSlider(masterSlider, masterValueText, VolumeTarget.Master);
            SetupVolumeSlider(bgmSlider, bgmValueText, VolumeTarget.Bgm);
            SetupVolumeSlider(sfxSlider, sfxValueText, VolumeTarget.Sfx);
            SetupHapticToggle();
            BuildCredits();
        }
        #endregion

        #region Sync
        /// <summary>
        /// 표시를 현재 저장값으로 다시 맞춘다(읽기만 하고 값을 쓰지는 않는다).
        /// 설정 페이지로 들어올 때마다 <see cref="HomeManager"/>가 호출한다.
        /// ★진동은 퍼즈 팝업에도 같은 설정을 조작하는 토글이 있어, 한 번만 읽으면 표시가 뒤처질 수 있다.
        /// 퍼즈 팝업이 열 때마다 재동기화하는 것과 같은 규칙을 여기서도 지킨다.
        /// </summary>
        public void SyncFromCurrent()
        {
            SyncSlider(masterSlider, masterValueText, VolumeTarget.Master);
            SyncSlider(bgmSlider, bgmValueText, VolumeTarget.Bgm);
            SyncSlider(sfxSlider, sfxValueText, VolumeTarget.Sfx);
            ToggleBinding.SetWithoutNotify(hapticToggle, HapticManager.Enabled);
        }

        /// <summary>저장된 볼륨 → 슬라이더 위치. 이벤트는 발생시키지 않는다.</summary>
        private static void SyncSlider(Slider slider, TextMeshProUGUI label, VolumeTarget target)
        {
            if (slider == null)
                return;

            float t = VolumeToSlider(GetVolume(target));
            slider.SetValueWithoutNotify(t);
            UpdateLabel(label, t);
        }
        #endregion

        #region Volume
        private void SetupVolumeSlider(Slider slider, TextMeshProUGUI label, VolumeTarget target)
        {
            if (slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            // 저장된 볼륨 → 슬라이더 위치(곡선의 역함수).
            // ★SyncSlider가 SetValueWithoutNotify를 쓰는 이유: 그냥 value에 넣으면 초기화가 곧바로
            //   onValueChanged를 때려 방금 읽은 값을 되쓰게 된다(곡선을 두 번 통과해 값이 어긋난다).
            SyncSlider(slider, label, target);

            slider.onValueChanged.AddListener(value =>
            {
                SetVolume(target, SliderToVolume(value));
                UpdateLabel(label, value);
            });

            // 드래그 중에는 디스크 저장도 미리듣기도 하지 않는다(프레임마다 발생하므로).
            GetRelay(slider).Released += () => OnSliderReleased(target);
        }

        private void OnSliderReleased(VolumeTarget target)
        {
            if (!SoundManager.InstanceExist)
                return;

            SoundManager.Instance.SaveVolumes();

            // BGM은 슬라이더를 움직이는 즉시 반영돼 귀로 확인되지만,
            // SFX 볼륨은 "다음에 재생되는 소리부터" 적용이라 들려주지 않으면 조절할 수가 없다.
            if (target != VolumeTarget.Bgm && previewSfx != SoundId.None)
                SoundManager.Instance.PlaySfx(previewSfx);
        }

        /// <summary>
        /// 슬라이더 위치 → 실제 볼륨. 선형 진폭은 위쪽 절반에서 변화가 거의 안 들리고
        /// 바닥 20%에서 급변해 조작감이 나쁘다. t*t로 굽혀 체감 변화를 고르게 만든다.
        /// </summary>
        private static float SliderToVolume(float t)
        {
            return t * t;
        }

        /// <summary>볼륨 → 슬라이더 위치(<see cref="SliderToVolume"/>의 역함수).</summary>
        private static float VolumeToSlider(float volume)
        {
            return Mathf.Sqrt(Mathf.Clamp01(volume));
        }

        private static float GetVolume(VolumeTarget target)
        {
            if (!SoundManager.InstanceExist)
                return 1f;

            var sound = SoundManager.Instance;
            switch (target)
            {
                case VolumeTarget.Bgm: return sound.BgmVolume;
                case VolumeTarget.Sfx: return sound.SfxVolume;
                default: return sound.MasterVolume;
            }
        }

        private static void SetVolume(VolumeTarget target, float volume)
        {
            if (!SoundManager.InstanceExist)
                return;

            var sound = SoundManager.Instance;
            switch (target)
            {
                case VolumeTarget.Bgm: sound.BgmVolume = volume; break;
                case VolumeTarget.Sfx: sound.SfxVolume = volume; break;
                default: sound.MasterVolume = volume; break;
            }
        }

        private static void UpdateLabel(TextMeshProUGUI label, float t)
        {
            if (label != null)
                label.text = Mathf.RoundToInt(t * 100f) + "%";
        }

        /// <summary>슬라이더에 릴레이를 붙여 반환한다(이미 있으면 그것을 쓴다).</summary>
        private static SliderReleaseRelay GetRelay(Slider slider)
        {
            var relay = slider.GetComponent<SliderReleaseRelay>();
            if (relay == null)
                relay = slider.gameObject.AddComponent<SliderReleaseRelay>();
            return relay;
        }
        #endregion

        #region Haptics
        private void SetupHapticToggle()
        {
            if (hapticToggle == null)
                return;

            ToggleBinding.Bind(hapticToggle, HapticManager.Enabled, value =>
            {
                HapticManager.Enabled = value;

                // 켠 직후 한 번 울려서 "이게 그 진동이다"를 알려준다(끌 때는 당연히 울리지 않는다).
                if (value)
                    HapticManager.Play(HapticType.Selection);
            });
        }
        #endregion

        #region Credits
        private void BuildCredits()
        {
            if (creditsText == null)
                return;

            string body = creditsBody;

            // MIT는 저작권 고지와 허가 문구를 사본에 포함할 것을 요구한다.
            if (licenseFullText != null)
                body += "\n\n- Karugamo BGM : MIT License -\n" + licenseFullText.text;

            creditsText.text = body;
        }

        /// <summary>
        /// 기본 크레딧 문구. 원본은 Assets/Boxal/Sounds/CREDITS.md 의 "게임 내 표기 문구" 블록이고,
        /// 오디오를 추가하면 양쪽을 같이 고쳐야 한다.
        /// ★일부러 ASCII만 쓴다 — 프로젝트의 LilitaOne 폰트 아틀라스에 em dash(—)와 ©가 없어
        ///   그대로 넣으면 글자가 빠진 채로 렌더된다.
        /// </summary>
        private const string DefaultCredits =
@"Sound Credits

- Music -
Karugamo BGM (karugamobgm.com)
  Copyright 2020 Karugamo BGM - MIT License

- Sound Effects -
""custom_short_explosion_impact_sound"" by Artninja
  freesound.org/s/750822/ - CC BY 4.0
""going-up-and-down-chirp"" by luckylittleraven
  freesound.org/s/239503/ - CC BY 3.0
""Modulated Ruler FX"" by Motion_S
  freesound.org/s/177848/ - CC BY 4.0
""Crushing kick_1 x3(17lrs)"" by newlocknew
  freesound.org/s/593909/ - CC BY 4.0
""cartbox kick drum"" by soneproject
  freesound.org/s/118510/ - CC0

Sonniss #GameAudioGDC Bundle (sonniss.com)
Shooting Sound pack by 4crain
Hints, Stars, Points & Rewards SFX Lite Pack
  by Cyberwave Orchestra";
        #endregion
    }
}
