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

    internal sealed class RangeDefinition
    {
        private static readonly RangeDefinition[] Items =
        {
            new RangeDefinition("1D", "1天", "5m", "candle5m", "5分钟K线", 288, false),
            new RangeDefinition("5D", "5天", "30m", "candle30m", "30分钟K线", 240, false),
            new RangeDefinition("1M", "1个月", "4H", "candle4H", "4小时K线", 180, false),
            new RangeDefinition("ALL", "全部", "1D", "candle1D", "日K线", 600, true)
        };

        private RangeDefinition(
            string key,
            string label,
            string restBar,
            string channel,
            string periodLabel,
            int maximumPoints,
            bool allHistory)
        {
            Key = key;
            Label = label;
            RestBar = restBar;
            WebSocketChannel = channel;
            PeriodLabel = periodLabel;
            MaximumPoints = maximumPoints;
            IsAllHistory = allHistory;
        }

        internal string Key { get; private set; }
        internal string Label { get; private set; }
        internal string RestBar { get; private set; }
        internal string WebSocketChannel { get; private set; }
        internal string PeriodLabel { get; private set; }
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
