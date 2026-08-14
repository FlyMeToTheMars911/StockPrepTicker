using System;
using System.Collections.Generic;
using System.Globalization;

namespace StockPerpTicker
{
    internal sealed class Candle
    {
        internal long Timestamp { get; set; }
        internal decimal Open { get; set; }
        internal decimal High { get; set; }
        internal decimal Low { get; set; }
        internal decimal Close { get; set; }
        internal decimal Volume { get; set; }

        internal DateTime LocalTime
        {
            get
            {
                DateTime utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(Timestamp);
                return utc.ToLocalTime();
            }
        }
    }

    internal sealed class MarketSnapshot
    {
        internal decimal LastPrice { get; set; }
        internal decimal Open24Hours { get; set; }
        internal decimal High24Hours { get; set; }
        internal decimal Low24Hours { get; set; }
        internal decimal Volume24Hours { get; set; }
        internal DateTime UpdatedAt { get; set; }

        internal decimal ChangePercent
        {
            get
            {
                if (Open24Hours == decimal.Zero)
                {
                    return decimal.Zero;
                }

                return (LastPrice - Open24Hours) / Open24Hours * 100m;
            }
        }
    }

    internal sealed class InstrumentInfo
    {
        internal string InstrumentId { get; set; }
        internal string State { get; set; }
        internal decimal TickSize { get; set; }
        internal long ListingTime { get; set; }
    }

    internal enum ConnectionStatus
    {
        Loading,
        Connecting,
        Live,
        Reconnecting,
        Offline,
        Error
    }

    internal sealed class CandlePeriodDefinition
    {
        internal const string AutomaticKey = "AUTO";
        private static readonly CandlePeriodDefinition[] Items =
        {
            new CandlePeriodDefinition(AutomaticKey, "自动", null, null, default(long)),
            new CandlePeriodDefinition("1m", "1分钟", "1m", "candle1m", 1L),
            new CandlePeriodDefinition("3m", "3分钟", "3m", "candle3m", 3L),
            new CandlePeriodDefinition("5m", "5分钟", "5m", "candle5m", 5L),
            new CandlePeriodDefinition("15m", "15分钟", "15m", "candle15m", 15L),
            new CandlePeriodDefinition("30m", "30分钟", "30m", "candle30m", 30L),
            new CandlePeriodDefinition("1H", "1小时", "1H", "candle1H", 60L),
            new CandlePeriodDefinition("2H", "2小时", "2H", "candle2H", 120L),
            new CandlePeriodDefinition("4H", "4小时", "4H", "candle4H", 240L),
            new CandlePeriodDefinition("6H", "6小时", "6H", "candle6H", 360L),
            new CandlePeriodDefinition("12H", "12小时", "12H", "candle12H", 720L),
            new CandlePeriodDefinition("1D", "1天", "1D", "candle1D", 1440L),
            new CandlePeriodDefinition("1W", "1周", "1W", "candle1W", 10080L),
            new CandlePeriodDefinition("1M", "1个月", "1M", "candle1M", 43200L)
        };

        private CandlePeriodDefinition(
            string key,
            string label,
            string restBar,
            string webSocketChannel,
            long durationMinutes)
        {
            Key = key;
            Label = label;
            RestBar = restBar;
            WebSocketChannel = webSocketChannel;
            DurationMinutes = durationMinutes;
        }

        internal string Key { get; private set; }
        internal string Label { get; private set; }
        internal string RestBar { get; private set; }
        internal string WebSocketChannel { get; private set; }
        internal long DurationMinutes { get; private set; }

        internal static IEnumerable<CandlePeriodDefinition> All
        {
            get { return Items; }
        }

        internal static CandlePeriodDefinition Find(string key)
        {
            CandlePeriodDefinition period;
            return TryFind(key, out period) ? period : Items[0];
        }

        internal static bool TryFind(string key, out CandlePeriodDefinition period)
        {
            foreach (CandlePeriodDefinition item in Items)
            {
                if (string.Equals(item.Key, key, StringComparison.Ordinal))
                {
                    period = item;
                    return true;
                }
            }

            CandlePeriodDefinition caseInsensitiveMatch = null;
            foreach (CandlePeriodDefinition item in Items)
            {
                if (!string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (caseInsensitiveMatch != null)
                {
                    period = null;
                    return false;
                }

                caseInsensitiveMatch = item;
            }

            period = caseInsensitiveMatch;
            return period != null;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    internal sealed class RangeDefinition
    {
        internal const string DefaultKey = "1D";
        internal const int MaximumConfigurablePoints = 3000;
        private static readonly RangeDefinition[] Items =
        {
            new RangeDefinition("1D", "1天", 1440L, "5m", false),
            new RangeDefinition("5D", "5天", 7200L, "30m", false),
            new RangeDefinition("1M", "1个月", 43200L, "4H", false),
            new RangeDefinition("3M", "3个月", 129600L, "12H", false),
            new RangeDefinition("6M", "6个月", 259200L, "1D", false),
            new RangeDefinition("1Y", "1年", 525600L, "1D", false),
            new RangeDefinition("3Y", "3年", 1576800L, "1W", false),
            new RangeDefinition("5Y", "5年", 2628000L, "1W", false),
            new RangeDefinition("ALL", "全部", default(long), "1M", true)
        };

        private RangeDefinition(
            string key,
            string label,
            long durationMinutes,
            string defaultPeriodKey,
            bool allHistory)
        {
            Key = key;
            Label = label;
            DurationMinutes = durationMinutes;
            DefaultPeriodKey = defaultPeriodKey;
            IsAllHistory = allHistory;
            ApplyPeriod(CandlePeriodDefinition.AutomaticKey, CandlePeriodDefinition.Find(defaultPeriodKey));
        }

        private RangeDefinition(RangeDefinition source, string selectedPeriodKey, CandlePeriodDefinition effectivePeriod)
        {
            Key = source.Key;
            Label = source.Label;
            DurationMinutes = source.DurationMinutes;
            DefaultPeriodKey = source.DefaultPeriodKey;
            IsAllHistory = source.IsAllHistory;
            ApplyPeriod(selectedPeriodKey, effectivePeriod);
        }

        internal string Key { get; private set; }
        internal string Label { get; private set; }
        internal long DurationMinutes { get; private set; }
        internal string DefaultPeriodKey { get; private set; }
        internal string SelectedPeriodKey { get; private set; }
        internal string RestBar { get; private set; }
        internal string WebSocketChannel { get; private set; }
        internal string PeriodLabel { get; private set; }
        internal long PeriodDurationMinutes { get; private set; }
        internal int MaximumPoints { get; private set; }
        internal bool IsAllHistory { get; private set; }

        internal static IEnumerable<RangeDefinition> All
        {
            get { return Items; }
        }

        internal static RangeDefinition Find(string key)
        {
            foreach (RangeDefinition item in Items)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return Items[0];
        }

        internal static RangeDefinition Create(string rangeKey, string selectedPeriodKey)
        {
            RangeDefinition range;
            string error;
            return TryCreate(rangeKey, selectedPeriodKey, out range, out error)
                ? range
                : Find(rangeKey);
        }

        internal static bool TryCreate(
            string rangeKey,
            string selectedPeriodKey,
            out RangeDefinition range,
            out string error)
        {
            range = null;
            error = null;
            RangeDefinition source = null;
            foreach (RangeDefinition item in Items)
            {
                if (string.Equals(item.Key, rangeKey, StringComparison.OrdinalIgnoreCase))
                {
                    source = item;
                    break;
                }
            }

            if (source == null)
            {
                error = "不支持时间范围 " + rangeKey + "。";
                return false;
            }

            CandlePeriodDefinition selectedPeriod;
            if (!CandlePeriodDefinition.TryFind(selectedPeriodKey, out selectedPeriod))
            {
                error = "不支持 K 线周期 " + selectedPeriodKey + "。";
                return false;
            }

            CandlePeriodDefinition effectivePeriod = string.Equals(
                selectedPeriod.Key,
                CandlePeriodDefinition.AutomaticKey,
                StringComparison.Ordinal)
                ? CandlePeriodDefinition.Find(source.DefaultPeriodKey)
                : selectedPeriod;
            long pointCount = source.CalculatePointCount(effectivePeriod);
            if (pointCount > MaximumConfigurablePoints)
            {
                error = source.Label + "范围使用" + effectivePeriod.Label + "K线预计需要 " + pointCount
                    + " 个数据点，超过性能上限 " + MaximumConfigurablePoints + "。请缩短范围或增大周期。";
                return false;
            }

            range = new RangeDefinition(source, selectedPeriod.Key, effectivePeriod);
            return true;
        }

        public override string ToString()
        {
            return Label;
        }

        private long CalculatePointCount(CandlePeriodDefinition period)
        {
            if (IsAllHistory)
            {
                return MaximumConfigurablePoints;
            }

            return Math.Max(1L, (DurationMinutes + period.DurationMinutes - 1L) / period.DurationMinutes);
        }

        private void ApplyPeriod(string selectedPeriodKey, CandlePeriodDefinition effectivePeriod)
        {
            SelectedPeriodKey = selectedPeriodKey;
            RestBar = effectivePeriod.RestBar;
            WebSocketChannel = effectivePeriod.WebSocketChannel;
            PeriodLabel = effectivePeriod.Label + "K线";
            PeriodDurationMinutes = effectivePeriod.DurationMinutes;
            MaximumPoints = (int)CalculatePointCount(effectivePeriod);
        }
    }

    internal static class FormatHelper
    {
        private const decimal Billion = 1000000000m;
        private const decimal Million = 1000000m;
        private const decimal Thousand = 1000m;

        internal static string Price(decimal value, decimal tickSize)
        {
            int decimals = 2;
            if (tickSize > decimal.Zero)
            {
                string text = tickSize.ToString(CultureInfo.InvariantCulture).TrimEnd('0');
                int point = text.IndexOf('.');
                decimals = point < 0 ? 0 : text.Length - point - 1;
            }

            decimals = Math.Max(0, Math.Min(8, decimals));
            return value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        internal static string CompactNumber(decimal value)
        {
            decimal absolute = Math.Abs(value);
            if (absolute >= Billion)
            {
                return (value / Billion).ToString("0.##", CultureInfo.InvariantCulture) + "B";
            }

            if (absolute >= Million)
            {
                return (value / Million).ToString("0.##", CultureInfo.InvariantCulture) + "M";
            }

            if (absolute >= Thousand)
            {
                return (value / Thousand).ToString("0.##", CultureInfo.InvariantCulture) + "K";
            }

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
