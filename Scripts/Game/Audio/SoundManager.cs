using System.Collections.Generic;
using UnityEngine;
using Boxal.Util;

namespace Boxal.Game.Audio
{
    /// <summary>
    /// 게임 전역 오디오 재생/볼륨의 단일 진입점. 씬을 넘어 유지된다(DontDestroyOnLoad).
    /// - SFX: 보이스 풀로 폴리포니 재생(빠른 연타에도 끊기지 않음), 엔트리별 피치 랜덤화.
    /// - BGM: 채널 1개, 크로스 없이 즉시 교체(같은 트랙 재요청은 무시).
    /// - 루프 SFX(차징 등): ID별 전용 소스로 <see cref="PlayLoop"/>/<see cref="StopLoop"/>.
    /// 볼륨(Master/Bgm/Sfx)은 PlayerPrefs에 저장되며 <see cref="SettingsPanelUI"/> 등에서 실시간 반영된다.
    /// 리소스(클립)가 아직 없어도 모든 호출은 조용히 no-op 하므로, 재생 훅을 먼저 깔아둘 수 있다.
    /// </summary>
    public class SoundManager : Singleton<SoundManager>
    {
        #region Inspector
        [Header("Library")]
        [Tooltip("ID→클립 매핑 에셋. 비어 있으면 모든 재생이 no-op.")]
        [SerializeField] private SoundLibrary library;

        [Header("Voices")]
        [Tooltip("동시에 겹쳐 울릴 수 있는 SFX 최대 수. 후반 라운드 연타를 고려해 넉넉히.")]
        [SerializeField] private int sfxVoiceCount = 12;
        #endregion

        #region Volume (persisted)
        private const string KEY_MASTER = "boxal.audio.master";
        private const string KEY_BGM = "boxal.audio.bgm";
        private const string KEY_SFX = "boxal.audio.sfx";

        private float masterVolume = 1f;
        private float bgmVolume = 1f;
        private float sfxVolume = 1f;
        private bool muted;

        /// <summary>
        /// 음소거. 퍼즈 팝업의 Sound 토글이 쓴다.
        /// <b>플레이 세션 한정이라 저장하지 않는다.</b> 홈으로 나오면 <see cref="Boxal.Game.UI.HomeManager"/>가
        /// 해제하고, 앱을 껐다 켜도 기본값(소리 켜짐)으로 시작한다. "지금 이 판만 조용히" 용도다.
        /// 마스터 볼륨을 0으로 덮지 않고 별도 플래그로 두는 이유: 그렇게 하면 설정에서 맞춰둔
        /// 슬라이더 값이 지워져, 껐다 켰을 때 원래 크기로 돌아오지 못한다.
        /// </summary>
        public bool Muted
        {
            get => muted;
            set
            {
                muted = value;
                ApplyBgmVolume(); // 재생 중인 BGM에 즉시 반영
            }
        }

        /// <summary>실제 재생에 곱해지는 마스터 계수. 음소거면 0.</summary>
        private float MasterFactor => muted ? 0f : masterVolume;

        /// <summary>전체 볼륨(0~1). BGM/SFX에 공통으로 곱해진다.</summary>
        public float MasterVolume
        {
            get => masterVolume;
            set => SetVolume(ref masterVolume, KEY_MASTER, value);
        }

        /// <summary>배경음 볼륨(0~1).</summary>
        public float BgmVolume
        {
            get => bgmVolume;
            set => SetVolume(ref bgmVolume, KEY_BGM, value);
        }

        /// <summary>효과음 볼륨(0~1). 다음에 재생되는 SFX부터 적용된다.</summary>
        public float SfxVolume
        {
            get => sfxVolume;
            set => SetVolume(ref sfxVolume, KEY_SFX, value);
        }

        private void SetVolume(ref float field, string key, float value)
        {
            field = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(key, field); // 메모리에만 기록. 디스크 flush는 SaveVolumes()에서.
            ApplyBgmVolume(); // 재생 중인 BGM은 즉시 반영
        }

        /// <summary>
        /// 볼륨 설정을 디스크에 기록한다.
        /// 세터에서 매번 부르지 않는 이유: 슬라이더 드래그 중에는 프레임마다 값이 바뀌는데
        /// <see cref="PlayerPrefs.Save"/>는 동기 파일 쓰기라 모바일에서 프레임 히칭이 난다.
        /// 조작이 끝난 시점(슬라이더에서 손 뗌)과 앱 이탈 시점에만 호출한다.
        /// </summary>
        public void SaveVolumes()
        {
            PlayerPrefs.Save();
        }
        #endregion

        #region State
        private AudioSource bgmSource;
        private AudioSource[] sfxVoices;
        private int nextVoice;

        // 루프 SFX는 ID별 전용 소스로 관리(중복 시작 방지 + 개별 정지).
        private readonly Dictionary<SoundId, AudioSource> loopSources = new Dictionary<SoundId, AudioSource>();

        private BgmId currentBgm = BgmId.None;
        private BgmEntryCache bgmCache; // 볼륨 재계산용으로 현재 트랙의 엔트리 볼륨 보관
        private struct BgmEntryCache { public float entryVolume; }
        #endregion

        #region Unity
        protected override void Awake()
        {
            base.Awake();
            // base.Awake()가 중복 인스턴스를 Destroy하면 이 인스턴스는 무효 — 초기화하지 않는다.
            if (Instance != this)
                return;

            DontDestroyOnLoad(gameObject);
            LoadVolumes();
            BuildSources();
        }

        private void LoadVolumes()
        {
            masterVolume = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
            bgmVolume = PlayerPrefs.GetFloat(KEY_BGM, 1f);
            sfxVolume = PlayerPrefs.GetFloat(KEY_SFX, 1f);
            // muted는 의도적으로 불러오지 않는다 — 플레이 세션 한정 상태다.
        }

        // 안드로이드는 백그라운드에서 그냥 죽을 수 있어 OnApplicationQuit이 안 올 수 있다.
        // 이탈 시점에 확실히 flush 해두면 설정 패널이 저장을 놓쳐도 값이 남는다.
        private void OnApplicationPause(bool paused)
        {
            if (paused && Instance == this)
                SaveVolumes();
        }

        private void OnApplicationQuit()
        {
            if (Instance == this)
                SaveVolumes();
        }

        private void BuildSources()
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;

            int count = Mathf.Max(1, sfxVoiceCount);
            sfxVoices = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                sfxVoices[i] = src;
            }
        }
        #endregion

        #region SFX
        /// <summary>효과음을 1회 재생한다. 클립/라이브러리가 없으면 아무 일도 하지 않는다.</summary>
        public void PlaySfx(SoundId id)
        {
            if (library == null) return;
            var entry = library.GetSfx(id);
            AudioClip clip = PickClip(entry);
            if (clip == null) return;

            var src = sfxVoices[nextVoice];
            nextVoice = (nextVoice + 1) % sfxVoices.Length; // 라운드로빈

            src.clip = clip;
            src.loop = false;
            src.volume = entry.volume * sfxVolume * MasterFactor;
            src.pitch = RandomPitch(entry);
            src.Play();
        }

        /// <summary>루프 효과음을 시작한다. 이미 같은 ID가 돌고 있으면 무시.</summary>
        public void PlayLoop(SoundId id)
        {
            if (library == null) return;
            var entry = library.GetSfx(id);
            AudioClip clip = PickClip(entry);
            if (clip == null) return;

            if (loopSources.TryGetValue(id, out var existing) && existing != null && existing.isPlaying)
                return;

            var src = GetDedicatedSource(id);
            src.clip = clip;
            src.loop = true;
            src.volume = entry.volume * sfxVolume * MasterFactor;
            src.pitch = RandomPitch(entry);
            src.Play();
        }

        /// <summary>정지 가능한 단발 효과음(예: 차징). 루프하지 않고 한 번만 재생하며,
        /// 이미 재생 중이면 처음부터 다시 재생한다. 도중에 <see cref="StopLoop"/>(id)로 끊을 수 있고,
        /// 끊긴 뒤 다시 호출하면 항상 0초부터 시작한다.</summary>
        public void PlayTracked(SoundId id)
        {
            if (library == null) return;
            var entry = library.GetSfx(id);
            AudioClip clip = PickClip(entry);
            if (clip == null) return;

            var src = GetDedicatedSource(id);
            src.Stop();       // 재생 중이면 초기화 → 아래에서 0초부터 다시
            src.clip = clip;
            src.loop = false;
            src.time = 0f;
            src.volume = entry.volume * sfxVolume * MasterFactor;
            src.pitch = RandomPitch(entry);
            src.Play();
        }

        /// <summary>전용 소스로 재생 중인 효과음(PlayLoop/PlayTracked)을 멈춘다. 안 돌고 있으면 무시.</summary>
        public void StopLoop(SoundId id)
        {
            if (loopSources.TryGetValue(id, out var src) && src != null)
                src.Stop();
        }

        /// <summary>ID 전용 AudioSource를 가져온다(없으면 생성). PlayLoop/PlayTracked가 공유.</summary>
        private AudioSource GetDedicatedSource(SoundId id)
        {
            if (loopSources.TryGetValue(id, out var src) && src != null)
                return src;
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            loopSources[id] = src;
            return src;
        }

        private AudioClip PickClip(SoundLibrary.SfxEntry entry)
        {
            if (entry == null || entry.clips == null || entry.clips.Length == 0)
                return null;
            if (entry.clips.Length == 1)
                return entry.clips[0];
            return entry.clips[Random.Range(0, entry.clips.Length)];
        }

        private float RandomPitch(SoundLibrary.SfxEntry entry)
        {
            if (entry.maxPitch <= entry.minPitch)
                return entry.minPitch <= 0f ? 1f : entry.minPitch;
            return Random.Range(entry.minPitch, entry.maxPitch);
        }
        #endregion

        #region BGM
        /// <summary>배경음을 재생한다. 같은 트랙이 이미 돌고 있으면 무시(재시작 안 함).</summary>
        public void PlayBgm(BgmId id)
        {
            if (id == currentBgm && bgmSource != null && bgmSource.isPlaying)
                return;

            currentBgm = id;
            var entry = library != null ? library.GetBgm(id) : null;
            if (entry == null || entry.clip == null)
            {
                bgmCache.entryVolume = 1f;
                if (bgmSource != null) bgmSource.Stop();
                return;
            }

            bgmCache.entryVolume = entry.volume;
            bgmSource.clip = entry.clip;
            bgmSource.volume = entry.volume * bgmVolume * MasterFactor;
            bgmSource.Play();
        }

        /// <summary>배경음을 멈춘다.</summary>
        public void StopBgm()
        {
            currentBgm = BgmId.None;
            if (bgmSource != null) bgmSource.Stop();
        }

        private void ApplyBgmVolume()
        {
            if (bgmSource != null)
                bgmSource.volume = bgmCache.entryVolume * bgmVolume * MasterFactor;
        }
        #endregion
    }
}
