using UnityEngine;

namespace Boxal.Util
{
    /// <summary>
    /// 앱 시작 시 1회 적용되는 런타임 설정. 씬에 오브젝트를 두지 않아도 자동 실행된다.
    /// </summary>
    /// <remarks>
    /// ★모바일에서 <see cref="Application.targetFrameRate"/>는 기본값(-1)일 때 배터리 절약을 위해
    /// 30fps로 제한된다. 에디터에는 이 제한이 없어서 "에디터에선 부드러운데 기기에서만 느린" 현상이 난다.
    /// Boxal은 낙하물을 피하고 타이밍에 맞춰 패링하는 게임이라 입력 반응이 중요해 60을 목표로 둔다.
    /// </remarks>
    public static class AppBootstrap
    {
        /// <summary>목표 프레임. 기기가 못 따라오면 알아서 그 아래로 떨어진다(상한일 뿐이다).</summary>
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // vSync가 켜져 있으면 targetFrameRate가 무시된다. 모바일은 vSync 개념이 없어 무관하지만,
            // 에디터/데스크톱에서도 같은 값이 나오도록 함께 꺼둔다.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
