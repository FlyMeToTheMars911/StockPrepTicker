using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace StockPerpTicker
{
    /// <summary>Identifies the CNN US stock market sentiment index.</summary>
    /// <remarks>Author: Codex; date: 2026-11-17.</remarks>
    internal static class FearGreedInstrument
    {
        internal const string Id = "CNN-FEAR-GREED";
        internal const string DisplayName = "CNN Fear and Greed Index";
        internal const string DailyPeriodKey = "1D";
        internal const string DefaultTimeRangeKey = "1Y";
        internal const decimal TickSize = 0.01m;
        internal const int RefreshIntervalMilliseconds = 300000;
        internal const string SourcePageUrl = "https://edition.cnn.com/markets/fear-and-greed";
        private const decimal ExtremeFearMaximum = 25m;
        private const decimal NeutralMinimum = 45m;
        private const decimal NeutralMaximum = 55m;
        private const decimal ExtremeGreedMinimum = 75m;
        private const decimal MinimumIndexScore = 0m;
        private const decimal MaximumIndexScore = 100m;
        private static readonly FearGreedBand[] SentimentBands =
        {
            new FearGreedBand(MinimumIndexScore, ExtremeFearMaximum, true, true,
                Color.FromArgb(198, 40, 40), "极度恐惧", "Extreme Fear"),
            new FearGreedBand(ExtremeFearMaximum, NeutralMinimum, false, false,
                Color.FromArgb(180, 83, 9), "恐惧", "Fear"),
            new FearGreedBand(NeutralMinimum, NeutralMaximum, true, true,
                Color.FromArgb(100, 116, 139), "中性", "Neutral"),
            new FearGreedBand(NeutralMaximum, ExtremeGreedMinimum, false, false,
                Color.FromArgb(77, 124, 15), "贪婪", "Greed"),
            new FearGreedBand(ExtremeGreedMinimum, MaximumIndexScore, true, true,
                Color.FromArgb(4, 120, 87), "极度贪婪", "Extreme Greed")
        };
        private static readonly decimal[] SentimentThresholds =
        {
            ExtremeFearMaximum, NeutralMinimum, NeutralMaximum, ExtremeGreedMinimum
        };
        private static readonly string[] SupportedTimeRangeKeys = { "1M", "3M", "6M", DefaultTimeRangeKey, "3Y", "5Y", "ALL" };
        private static readonly string[] TimeRangeLabels = { "1月", "3月", "6月", "1年", "3年", "5年", "最大" };

        /// <summary>Gets the independent CNN history ranges in display order.</summary>
        internal static string[] TimeRangeKeys
        {
            get { return (string[])SupportedTimeRangeKeys.Clone(); }
        }

        /// <summary>Normalizes a CNN history range without changing the OKX chart configuration.</summary>
        /// <param name="rangeKey">The stored range key, or a missing legacy setting.</param>
        /// <returns>A supported key, defaulting to one year for absent or invalid input.</returns>
        internal static string NormalizeTimeRangeKey(string rangeKey)
        {
            if (string.IsNullOrWhiteSpace(rangeKey))
            {
                return DefaultTimeRangeKey;
            }

            string normalizedKey = rangeKey.Trim().ToUpperInvariant();
            foreach (string supportedKey in SupportedTimeRangeKeys)
            {
                if (string.Equals(supportedKey, normalizedKey, StringComparison.Ordinal))
                {
                    return supportedKey;
                }
            }

            Logger.Info("已忽略无效的 CNN 时间范围：" + rangeKey + "，使用默认 1 年。");
            return DefaultTimeRangeKey;
        }

        /// <summary>Returns the compact label used by the CNN history range selector.</summary>
        /// <param name="rangeKey">A history range key.</param>
        /// <returns>The matching label after normalizing missing or invalid settings.</returns>
        internal static string GetTimeRangeLabel(string rangeKey)
        {
            string normalizedKey = NormalizeTimeRangeKey(rangeKey);
            int index = Array.IndexOf(SupportedTimeRangeKeys, normalizedKey);
            return TimeRangeLabels[index];
        }

        /// <summary>Gets the immutable category definitions ordered from fear to greed.</summary>
        internal static FearGreedBand[] Bands
        {
            get { return (FearGreedBand[])SentimentBands.Clone(); }
        }

        /// <summary>Gets a copy of the score values separating sentiment categories.</summary>
        internal static decimal[] Thresholds
        {
            get { return (decimal[])SentimentThresholds.Clone(); }
        }

        /// <summary>Returns the shared display color for a validated CNN index score.</summary>
        /// <param name="score">The index value from zero through one hundred.</param>
        /// <returns>The category color, matching CNN's inclusive boundary rules.</returns>
        internal static Color GetColor(decimal score)
        {
            foreach (FearGreedBand band in SentimentBands)
            {
                bool aboveMinimum = band.MinimumInclusive ? score >= band.Minimum : score > band.Minimum;
                bool belowMaximum = band.MaximumInclusive ? score <= band.Maximum : score < band.Maximum;
                if (aboveMinimum && belowMaximum)
                {
                    return band.Color;
                }
            }

            throw new ArgumentOutOfRangeException("score", "CNN 指数必须在 0～100 范围内。");
        }

        /// <summary>Returns CNN's Chinese sentiment category for a validated score.</summary>
        /// <param name="score">The index value from zero through one hundred.</param>
        /// <returns>The Chinese category, using CNN's inclusive boundary rules.</returns>
        internal static string GetRating(decimal score)
        {
            if (score <= ExtremeFearMaximum)
            {
                return "极度恐惧";
            }

            if (score < NeutralMinimum)
            {
                return "恐惧";
            }

            if (score <= NeutralMaximum)
            {
                return "中性";
            }

            return score < ExtremeGreedMinimum ? "贪婪" : "极度贪婪";
        }

        /// <summary>Returns the English category used by CNN's official gauge.</summary>
        /// <param name="score">The index value from zero through one hundred.</param>
        /// <returns>The English category, using CNN's inclusive boundary rules.</returns>
        internal static string GetEnglishRating(decimal score)
        {
            if (score <= ExtremeFearMaximum)
            {
                return "Extreme Fear";
            }

            if (score < NeutralMinimum)
            {
                return "Fear";
            }

            if (score <= NeutralMaximum)
            {
                return "Neutral";
            }

            return score < ExtremeGreedMinimum ? "Greed" : "Extreme Greed";
        }
    }

    /// <summary>Describes one immutable sentiment interval and its shared display color.</summary>
    /// <remarks>Author: Codex; date: 2026-11-17.</remarks>
    internal sealed class FearGreedBand
    {
        /// <summary>Creates a sentiment interval with explicit endpoint ownership.</summary>
        /// <param name="minimum">The lower index value.</param>
        /// <param name="maximum">The upper index value.</param>
        /// <param name="minimumInclusive">Whether the lower endpoint belongs to this category.</param>
        /// <param name="maximumInclusive">Whether the upper endpoint belongs to this category.</param>
        /// <param name="color">The shared text and line color.</param>
        /// <param name="label">The Chinese category label.</param>
        /// <param name="englishLabel">The English category label.</param>
        /// <returns>A category definition whose properties cannot be changed externally.</returns>
        internal FearGreedBand(decimal minimum, decimal maximum, bool minimumInclusive,
            bool maximumInclusive, Color color, string label, string englishLabel)
        {
            Minimum = minimum;
            Maximum = maximum;
            MinimumInclusive = minimumInclusive;
            MaximumInclusive = maximumInclusive;
            Color = color;
            Label = label;
            EnglishLabel = englishLabel;
        }

        internal decimal Minimum { get; private set; }
        internal decimal Maximum { get; private set; }
        internal bool MinimumInclusive { get; private set; }
        internal bool MaximumInclusive { get; private set; }
        internal Color Color { get; private set; }
        internal string Label { get; private set; }
        internal string EnglishLabel { get; private set; }
    }

    /// <summary>Contains CNN's current index value and available daily observations.</summary>
    /// <remarks>Author: Codex; date: 2026-11-17.</remarks>
    internal sealed class FearGreedData
    {
        internal List<Candle> History { get; set; }
        internal MarketSnapshot Snapshot { get; set; }
        internal DateTime UpdatedAtUtc { get; set; }
        internal string Rating { get; set; }
    }

    /// <summary>Reads public CNN index data with bounded requests and strict validation.</summary>
    /// <remarks>Author: Codex; date: 2026-11-17.</remarks>
    internal sealed class CnnFearGreedClient
    {
        internal const string DataUrl = "https://production.dataviz.cnn.io/index/fearandgreed/graphdata/2021-01-01";
        private const int HttpTimeoutMilliseconds = 20000;
        private const int MaximumResponseBytes = 8 * 1024 * 1024;
        private const int ReadBufferBytes = 8192;
        private const decimal MinimumScore = 0m;
        private const decimal MaximumScore = 100m;
        private const long MinimumTimestamp = 0L;
        private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36";
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Fetches the latest index and CNN's available history since 2021.</summary>
        /// <param name="cancellationToken">Cancels network and response reading.</param>
        /// <returns>Validated observations and a snapshot whose update time is UTC.</returns>
        internal async Task<FearGreedData> FetchAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(DataUrl);
            request.Method = WebRequestMethods.Http.Get;
            request.UserAgent = BrowserUserAgent;
            request.Accept = "application/json";
            request.Referer = FearGreedInstrument.SourcePageUrl;
            request.Headers["Origin"] = "https://edition.cnn.com";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = HttpTimeoutMilliseconds;
            request.ReadWriteTimeout = HttpTimeoutMilliseconds;

            Logger.Info("开始请求 CNN 恐惧贪婪指数及日线历史。");
            using (CancellationTokenSource requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (CancellationTokenRegistration registration = requestCancellation.Token.Register(request.Abort))
            {
                requestCancellation.CancelAfter(HttpTimeoutMilliseconds);
                try
                {
                    using (WebResponse response = await Task.Factory.FromAsync<WebResponse>(
                        request.BeginGetResponse, request.EndGetResponse, null).ConfigureAwait(false))
                    {
                        if (response.ContentLength > MaximumResponseBytes)
                        {
                            throw new InvalidOperationException("CNN 指数响应超过允许的大小。");
                        }

                        using (Stream input = response.GetResponseStream())
                        using (MemoryStream output = new MemoryStream())
                        {
                            if (input == null)
                            {
                                throw new InvalidOperationException("CNN 未返回指数响应内容。");
                            }

                            byte[] buffer = new byte[ReadBufferBytes];
                            int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, requestCancellation.Token)
                                .ConfigureAwait(false)) > default(int))
                            {
                                if (output.Length + count > MaximumResponseBytes)
                                {
                                    throw new InvalidOperationException("CNN 指数响应超过允许的大小。");
                                }

                                output.Write(buffer, 0, count);
                            }

                            requestCancellation.Token.ThrowIfCancellationRequested();
                            FearGreedData result = Parse(Encoding.UTF8.GetString(output.ToArray()));
                            Logger.Info("CNN 指数请求完成：" + result.History.Count + " 个日线数据点 / 更新时间 "
                                + result.UpdatedAtUtc.ToString("u", CultureInfo.InvariantCulture));
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (requestCancellation.IsCancellationRequested)
                    {
                        throw new TimeoutException("CNN 指数请求超时，请稍后重试。", ex);
                    }

                    WebException webException = ex as WebException;
                    HttpWebResponse errorResponse = webException == null ? null : webException.Response as HttpWebResponse;
                    if (errorResponse != null)
                    {
                        using (errorResponse)
                        {
                            throw new InvalidOperationException("CNN 指数服务返回 HTTP "
                                + ((int)errorResponse.StatusCode).ToString(CultureInfo.InvariantCulture)
                                + "，暂时无法读取数据。", ex);
                        }
                    }

                    throw;
                }
            }
        }

        /// <summary>Parses CNN's graphdata schema and keeps the last observation for each UTC date.</summary>
        /// <param name="json">The original JSON returned by CNN.</param>
        /// <returns>Daily history with stable UTC date keys, plus the real update time in the snapshot.</returns>
        internal static FearGreedData Parse(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = MaximumResponseBytes };
            Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null)
            {
                throw new InvalidOperationException("CNN 返回的指数 JSON 格式无效。");
            }

            Dictionary<string, object> current = GetObject(root, "fear_and_greed");
            decimal score = GetScore(current, "score");
            decimal previousClose = GetScore(current, "previous_close");
            object timestampValue = GetRequiredValue(current, "timestamp");
            DateTimeOffset timestamp;
            if (!DateTimeOffset.TryParse(Convert.ToString(timestampValue, CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
            {
                throw new InvalidOperationException("CNN 指数更新时间格式无效。");
            }

            DateTime updatedAtUtc = timestamp.UtcDateTime;
            long currentTimestamp = (long)(updatedAtUtc - UnixEpoch).TotalMilliseconds;
            ValidateTimestamp(currentTimestamp);
            Dictionary<string, object> historical = GetObject(root, "fear_and_greed_historical");
            object[] observations = GetRequiredValue(historical, "data") as object[];
            if (observations == null || observations.Length == default(int))
            {
                throw new InvalidOperationException("CNN 未返回可用的指数历史数据。");
            }

            SortedDictionary<DateTime, Candle> daily = new SortedDictionary<DateTime, Candle>();
            foreach (object observation in observations)
            {
                Dictionary<string, object> point = observation as Dictionary<string, object>;
                if (point == null)
                {
                    throw new InvalidOperationException("CNN 指数历史数据点格式无效。");
                }

                decimal rawTimestamp;
                if (!decimal.TryParse(Convert.ToString(GetRequiredValue(point, "x"), CultureInfo.InvariantCulture),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out rawTimestamp)
                    || rawTimestamp != decimal.Truncate(rawTimestamp)
                    || rawTimestamp > long.MaxValue || rawTimestamp < MinimumTimestamp)
                {
                    throw new InvalidOperationException("CNN 指数历史时间戳格式无效。");
                }

                long pointTimestamp = (long)rawTimestamp;
                ValidateTimestamp(pointTimestamp);
                Candle candle = CreateObservation(pointTimestamp, GetScore(point, "y"));
                DateTime day = UnixEpoch.AddMilliseconds(pointTimestamp).Date;
                Candle existing;
                if (!daily.TryGetValue(day, out existing) || candle.Timestamp >= existing.Timestamp)
                {
                    daily[day] = candle;
                }
            }

            daily[updatedAtUtc.Date] = CreateObservation(currentTimestamp, score);
            List<Candle> history = new List<Candle>(daily.Count);
            foreach (KeyValuePair<DateTime, Candle> observation in daily)
            {
                long dailyTimestamp = (long)(observation.Key - UnixEpoch).TotalMilliseconds;
                history.Add(CreateObservation(dailyTimestamp, observation.Value.Close));
            }

            return new FearGreedData
            {
                History = history,
                Snapshot = new MarketSnapshot
                {
                    LastPrice = score,
                    Open24Hours = previousClose,
                    Volume24Hours = decimal.Zero,
                    UpdatedAt = updatedAtUtc
                },
                UpdatedAtUtc = updatedAtUtc,
                Rating = FearGreedInstrument.GetRating(score)
            };
        }

        /// <summary>Wraps a daily index value for the shared line chart without fabricating OHLC variation.</summary>
        /// <param name="timestamp">CNN's original Unix timestamp in milliseconds.</param>
        /// <param name="score">The validated index value.</param>
        /// <returns>A shared chart observation whose four price fields contain the same value.</returns>
        private static Candle CreateObservation(long timestamp, decimal score)
        {
            return new Candle { Timestamp = timestamp, Open = score, High = score, Low = score, Close = score, Volume = decimal.Zero };
        }

        /// <summary>Reads a required JSON object.</summary>
        /// <param name="source">The containing JSON object.</param>
        /// <param name="key">The required property name.</param>
        /// <returns>The object, or an explicit schema error.</returns>
        private static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key)
        {
            Dictionary<string, object> value = GetRequiredValue(source, key) as Dictionary<string, object>;
            if (value == null)
            {
                throw new InvalidOperationException("CNN 指数字段格式无效：" + key);
            }

            return value;
        }

        /// <summary>Reads a required non-null JSON property.</summary>
        /// <param name="source">The containing JSON object.</param>
        /// <param name="key">The required property name.</param>
        /// <returns>The property's original value.</returns>
        private static object GetRequiredValue(Dictionary<string, object> source, string key)
        {
            object value;
            if (!source.TryGetValue(key, out value) || value == null)
            {
                throw new InvalidOperationException("CNN 指数响应缺少字段：" + key);
            }

            return value;
        }

        /// <summary>Validates a score before it can enter either the main or mini chart.</summary>
        /// <param name="source">The containing JSON object.</param>
        /// <param name="key">The score property name.</param>
        /// <returns>A score in the inclusive range from zero through one hundred.</returns>
        private static decimal GetScore(Dictionary<string, object> source, string key)
        {
            decimal score;
            if (!decimal.TryParse(Convert.ToString(GetRequiredValue(source, key), CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture, out score)
                || score < MinimumScore || score > MaximumScore)
            {
                throw new InvalidOperationException("CNN 指数数值无效或超出 0～100 范围：" + key);
            }

            return score;
        }

        /// <summary>Rejects missing and out-of-range Unix timestamps.</summary>
        /// <param name="timestamp">The timestamp in milliseconds.</param>
        /// <returns>No value; throws if conversion is unsafe.</returns>
        private static void ValidateTimestamp(long timestamp)
        {
            if (timestamp <= MinimumTimestamp)
            {
                throw new InvalidOperationException("CNN 指数时间戳无效。");
            }

            try
            {
                UnixEpoch.AddMilliseconds(timestamp);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new InvalidOperationException("CNN 指数时间戳超出有效范围。", ex);
            }
        }
    }
}
