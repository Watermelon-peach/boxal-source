using Lofelt.NiceVibrations;
using UnityEngine;

namespace Boxal.Game.Feedback
{
    /// <summary>세기/의미별 햅틱 종류. 게임 코드가 벤더(NiceVibrations) 타입을 직접 참조하지 않도록 감싼다.</summary>
    public enum HapticType
    {
        None = 0,
        Selection,  // UI 선택 — 가장 약함
        Light,      // 가벼운 확인
        Medium,     // 보통 임팩트
        Heavy,      // 강한 임팩트 (피격, 궁극기)
        Rigid,      // 짧고 날카로운 임팩트 (패링 등 정밀한 순간)
        Success,    // 긍정 패턴 (보상, 신기록)
        Warning,    // 경고 패턴 (왕보스 등장)
        Failure,    // 실패 패턴 (게임오버)
    }

    /// <summary>
    /// 햅틱(진동) 재생의 단일 진입점. 오디오와 달리 GameObject가 필요 없어 정적 클래스로 둔다.
    /// </summary>
    /// <remarks>
    /// ★NiceVibrations의 <see cref="HapticController.hapticsEnabled"/>는 단순 static 필드라
    /// 앱을 재시작하면 true로 돌아간다. 그래서 플레이어의 on/off 선택은 여기서 PlayerPrefs에 저장하고
    /// 시작 시 다시 적용한다(<see cref="Init"/>).
    /// 햅틱은 실기기에서만 동작하며 에디터에서는 아무 일도 일어나지 않는다(정상).
    /// </remarks>
    public static class HapticManager
    {
        private const string KEY_ENABLED = "boxal.haptics.enabled";

        /// <summary>진동 사용 여부. 설정 패널의 토글이 이 값을 읽고 쓴다.</summary>
        public static bool Enabled
        {
            get => HapticController.hapticsEnabled;
            set
            {
                HapticController.hapticsEnabled = value;
                PlayerPrefs.SetInt(KEY_ENABLED, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>저장된 진동 설정을 불러와 적용한다. 씬 로드 전에 자동 실행된다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            HapticController.hapticsEnabled = PlayerPrefs.GetInt(KEY_ENABLED, 1) == 1;
        }

        /// <summary>햅틱을 재생한다. 꺼져 있거나 기기가 지원하지 않으면 아무 일도 하지 않는다.</summary>
        public static void Play(HapticType type)
        {
            if (type == HapticType.None)
                return;

            // PlayPreset이 내부에서 hapticsEnabled와 기기 지원 여부를 확인하므로 여기서 중복 검사하지 않는다.
            HapticPatterns.PlayPreset(ToPreset(type));
        }

        private static HapticPatterns.PresetType ToPreset(HapticType type)
        {
            switch (type)
            {
                case HapticType.Selection: return HapticPatterns.PresetType.Selection;
                case HapticType.Light: return HapticPatterns.PresetType.LightImpact;
                case HapticType.Medium: return HapticPatterns.PresetType.MediumImpact;
                case HapticType.Heavy: return HapticPatterns.PresetType.HeavyImpact;
                case HapticType.Rigid: return HapticPatterns.PresetType.RigidImpact;
                case HapticType.Success: return HapticPatterns.PresetType.Success;
                case HapticType.Warning: return HapticPatterns.PresetType.Warning;
                case HapticType.Failure: return HapticPatterns.PresetType.Failure;
                default: return HapticPatterns.PresetType.None;
            }
        }
    }
}
