using System.Collections.Generic;
using UnityEngine;

namespace Boxal.Game.Audio
{
    /// <summary>
    /// ID → 클립/재생 파라미터 매핑. 디자이너가 인스펙터에서 채운다.
    /// 코드는 클립을 모른 채 <see cref="SoundId"/>/<see cref="BgmId"/> 만 넘긴다.
    /// 리소스가 아직 없어도(클립 null) 시스템은 조용히 no-op 하므로, 훅을 먼저 깔아둘 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Boxal/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        /// <summary>효과음 한 종의 클립과 재생 파라미터.</summary>
        [System.Serializable]
        public class SfxEntry
        {
            public SoundId id;

            [Tooltip("여러 개면 재생 시 랜덤으로 하나 고른다(반복 피로 완화).")]
            public AudioClip[] clips;

            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("재생 시 피치를 이 범위에서 랜덤화. 박스몬 파괴음은 0.9~1.1 권장. 같은 값이면 고정.")]
            public float minPitch = 1f;
            public float maxPitch = 1f;

            [Tooltip("루프 재생(예: 차징). PlayLoop/StopLoop 로 제어.")]
            public bool loop = false;
        }

        /// <summary>배경음 한 트랙.</summary>
        [System.Serializable]
        public class BgmEntry
        {
            public BgmId id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [SerializeField] private List<SfxEntry> sfx = new List<SfxEntry>();
        [SerializeField] private List<BgmEntry> bgm = new List<BgmEntry>();

        private Dictionary<SoundId, SfxEntry> sfxMap;
        private Dictionary<BgmId, BgmEntry> bgmMap;

        private void OnEnable()
        {
            // 도메인 리로드/에셋 로드 시 조회용 맵을 다시 만든다.
            sfxMap = null;
            bgmMap = null;
        }

        /// <summary>ID로 SFX 항목을 찾는다. 없으면 null(호출부에서 no-op).</summary>
        public SfxEntry GetSfx(SoundId id)
        {
            if (id == SoundId.None) return null;
            if (sfxMap == null)
            {
                sfxMap = new Dictionary<SoundId, SfxEntry>();
                foreach (var e in sfx)
                    if (e != null && e.id != SoundId.None)
                        sfxMap[e.id] = e; // 중복 시 나중 것이 이긴다
            }
            return sfxMap.TryGetValue(id, out var entry) ? entry : null;
        }

        /// <summary>ID로 BGM 항목을 찾는다. 없으면 null.</summary>
        public BgmEntry GetBgm(BgmId id)
        {
            if (id == BgmId.None) return null;
            if (bgmMap == null)
            {
                bgmMap = new Dictionary<BgmId, BgmEntry>();
                foreach (var e in bgm)
                    if (e != null && e.id != BgmId.None)
                        bgmMap[e.id] = e;
            }
            return bgmMap.TryGetValue(id, out var entry) ? entry : null;
        }
    }
}
