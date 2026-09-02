using UnityEngine;

namespace Boxal.Util
{
    public static class NumberUtil
    {
        public static string FormatNumber(long num)
        {
            if (num >= 1_000_000_000)
                return (num / 1_000_000_000f).ToString("0.0") + "B";
            if (num >= 1_000_000)
                return (num / 1_000_000f).ToString("0.0") + "M";
            if (num >= 1_000)
                return (num / 1_000f).ToString("0.0") + "K";

            return num.ToString();
        }

        /// <summary>세 자리마다 쉼표를 찍어 전체 표기한다. 예) 13289 → "13,289". 문화권 무관(항상 쉼표).</summary>
        public static string FormatComma(long num)
        {
            return num.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>초 단위 시간을 "mm : ss" 포맷 문자열로 변환.</summary>
        public static string FormatMinSec(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int m = total / 60;
            int s = total % 60;
            return $"{m:00} : {s:00}";
        }

        /// <summary>
        /// 초 단위 시간을 "m : ss"로 변환한다. 분은 0을 채우지 않는다. 예) 299 → "4 : 59".
        /// <see cref="FormatMinSec"/>와 달리 짧은 대기(스태미나 다음 1개 등)를 겨냥한 표기다.
        /// </summary>
        public static string FormatMinSecShort(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int m = total / 60;
            int s = total % 60;
            return $"{m} : {s:00}";
        }

        /// <summary>
        /// 초 단위 시간을 "2h 29m" / "29m"으로 변환한다. 한 시간 미만이면 시간 자리를 생략한다.
        /// </summary>
        /// <remarks>
        /// 분 단위로 <b>올림</b>한다. 남은 시간을 나타내는 표기라 "0m"이 떠 있는데 아직 안 차 있는 것보다,
        /// 1분으로 보이다가 채워지는 쪽이 덜 이상하기 때문이다.
        /// </remarks>
        public static string FormatHourMin(float seconds)
        {
            int totalMin = Mathf.Max(0, Mathf.CeilToInt(seconds / 60f));
            int h = totalMin / 60;
            int m = totalMin % 60;
            return h > 0 ? $"{h}h {m}m" : $"{m}m";
        }
    }

}
