using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace StockPerpTicker
{
    internal static class JsonParser
    {
        private const int EmptyArrayLength = 0;
        private const int MinimumCandleFieldCount = 6;

        internal static InstrumentInfo ParseInstrument(string json)
        {
            Dictionary<string, object> root = ParseRoot(json);
            object[] data = GetArray(root, "data");
            if (data.Length == EmptyArrayLength)
            {
                return null;
            }

            Dictionary<string, object> item = data[0] as Dictionary<string, object>;
            if (item == null)
            {
                throw new InvalidOperationException("OKX 合约信息格式无效。");
            }

            return new InstrumentInfo
            {
                InstrumentId = GetString(item, "instId"),
                State = GetString(item, "state"),
                TickSize = ParseDecimal(GetString(item, "tickSz")),
                ListingTime = ParseLong(GetString(item, "listTime"))
            };
        }

        internal static List<Candle> ParseCandles(string json)
        {
            Dictionary<string, object> root = ParseRoot(json);
            object[] data = GetArray(root, "data");
            List<Candle> candles = new List<Candle>(data.Length);
            foreach (object value in data)
            {
                object[] fields = value as object[];
                if (fields != null && fields.Length >= MinimumCandleFieldCount)
                {
                    candles.Add(ParseCandle(fields));
                }
            }

            return candles;
        }

        internal static MarketSnapshot ParseTicker(string json)
        {
            Dictionary<string, object> root = ParseRoot(json);
            object[] data = GetArray(root, "data");
            if (data.Length == EmptyArrayLength)
            {
                throw new InvalidOperationException("OKX 未返回行情快照。");
            }

            Dictionary<string, object> item = data[0] as Dictionary<string, object>;
            if (item == null)
            {
                throw new InvalidOperationException("OKX 行情快照格式无效。");
            }

            return new MarketSnapshot
            {
                LastPrice = ParseDecimal(GetString(item, "last")),
                Open24Hours = ParseDecimal(GetString(item, "open24h")),
                High24Hours = ParseDecimal(GetString(item, "high24h")),
                Low24Hours = ParseDecimal(GetString(item, "low24h")),
                Volume24Hours = ParseDecimal(GetString(item, "vol24h")),
                UpdatedAt = DateTime.Now
            };
        }

        internal static Candle ParseWebSocketCandle(string json)
        {
            if (string.Equals(json, "pong", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            Dictionary<string, object> root = Deserialize(json);
            object eventValue;
            if (root.TryGetValue("event", out eventValue))
            {
                string eventName = Convert.ToString(eventValue, CultureInfo.InvariantCulture);
                if (string.Equals(eventName, "error", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("OKX WebSocket：" + GetString(root, "msg"));
                }

                return null;
            }

            object[] data = GetArray(root, "data", false);
            if (data.Length == EmptyArrayLength)
            {
                return null;
            }

            object[] fields = data[0] as object[];
            return fields != null && fields.Length >= MinimumCandleFieldCount ? ParseCandle(fields) : null;
        }

        private static Dictionary<string, object> ParseRoot(string json)
        {
            Dictionary<string, object> root = Deserialize(json);
            string code = GetString(root, "code");
            if (!string.IsNullOrEmpty(code) && !string.Equals(code, "0", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("OKX API 错误 " + code + "：" + GetString(root, "msg"));
            }

            return root;
        }

        private static Dictionary<string, object> Deserialize(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 2 * 1024 * 1024;
            Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null)
            {
                throw new InvalidOperationException("OKX 返回的 JSON 格式无效。");
            }

            return root;
        }

        private static Candle ParseCandle(object[] fields)
        {
            return new Candle
            {
                Timestamp = ParseLong(Convert.ToString(fields[0], CultureInfo.InvariantCulture)),
                Open = ParseDecimal(Convert.ToString(fields[1], CultureInfo.InvariantCulture)),
                High = ParseDecimal(Convert.ToString(fields[2], CultureInfo.InvariantCulture)),
                Low = ParseDecimal(Convert.ToString(fields[3], CultureInfo.InvariantCulture)),
                Close = ParseDecimal(Convert.ToString(fields[4], CultureInfo.InvariantCulture)),
                Volume = ParseDecimal(Convert.ToString(fields[5], CultureInfo.InvariantCulture))
            };
        }

        private static object[] GetArray(Dictionary<string, object> source, string key)
        {
            return GetArray(source, key, true);
        }

        private static object[] GetArray(Dictionary<string, object> source, string key, bool required)
        {
            object value;
            if (!source.TryGetValue(key, out value) || value == null)
            {
                if (required)
                {
                    throw new InvalidOperationException("OKX 返回缺少字段：" + key);
                }

                return new object[0];
            }

            object[] array = value as object[];
            if (array == null)
            {
                throw new InvalidOperationException("OKX 返回字段格式无效：" + key);
            }

            return array;
        }

        private static string GetString(Dictionary<string, object> source, string key)
        {
            object value;
            return source.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static decimal ParseDecimal(string text)
        {
            decimal value;
            return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : decimal.Zero;
        }

        private static long ParseLong(string text)
        {
            long value;
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0L;
        }
    }
}
