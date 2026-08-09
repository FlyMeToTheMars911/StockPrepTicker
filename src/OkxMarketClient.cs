using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StockPerpTicker
{
    internal sealed class OkxMarketClient : IDisposable
    {
        private const string RestBaseUrl = "https://www.okx.com";
        private const string WebSocketUrl = "wss://ws.okx.com:8443/ws/v5/business";
        private const int HttpTimeoutSeconds = 12;
        private const int ReceiveBufferBytes = 8192;
        private const int HeartbeatCheckSeconds = 15;
        private const int HeartbeatIdleSeconds = 20;
        private const int HeartbeatFailureSeconds = 12;
        private const int EmptyItemCount = 0;
        private const int NoReconnectDelay = 0;
        private const long UnknownListingTime = 0L;
        private static readonly int[] ReconnectDelaysSeconds = { 1, 2, 5, 10, 30 };

        private bool _disposed;

        internal OkxMarketClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        internal async Task<InstrumentInfo> ValidateInstrumentAsync(string instrumentId, CancellationToken cancellationToken)
        {
            string path = "/api/v5/public/instruments?instType=SWAP&instId=" + Uri.EscapeDataString(instrumentId);
            string json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
            InstrumentInfo instrument = JsonParser.ParseInstrument(json);
            if (instrument == null)
            {
                throw new InvalidOperationException("OKX 未找到合约 " + instrumentId + "。请检查设置中的合约代码。");
            }

            const string LiveState = "live";
            if (!string.Equals(instrument.State, LiveState, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("合约 " + instrumentId + " 当前不可交易，状态：" + instrument.State);
            }

            return instrument;
        }

        internal async Task<List<Candle>> FetchCandlesAsync(
            string instrumentId,
            RangeDefinition range,
            long listingTime,
            CancellationToken cancellationToken)
        {
            Dictionary<long, Candle> unique = new Dictionary<long, Candle>();
            int firstLimit = Math.Min(300, range.MaximumPoints);
            string currentPath = BuildCandlePath("/api/v5/market/candles", instrumentId, range.RestBar, firstLimit, null);
            AddCandles(unique, JsonParser.ParseCandles(await GetStringAsync(currentPath, cancellationToken).ConfigureAwait(false)));

            if (range.IsAllHistory)
            {
                while (unique.Count < range.MaximumPoints && unique.Count > EmptyItemCount)
                {
                    long oldest = unique.Keys.Min();
                    if (listingTime > UnknownListingTime && oldest <= listingTime)
                    {
                        break;
                    }

                    int remaining = Math.Min(300, range.MaximumPoints - unique.Count);
                    string historyPath = BuildCandlePath(
                        "/api/v5/market/history-candles",
                        instrumentId,
                        range.RestBar,
                        remaining,
                        oldest);
                    List<Candle> page = JsonParser.ParseCandles(await GetStringAsync(historyPath, cancellationToken).ConfigureAwait(false));
                    int countBefore = unique.Count;
                    AddCandles(unique, page);
                    if (page.Count == EmptyItemCount || unique.Count == countBefore)
                    {
                        break;
                    }
                }
            }

            List<Candle> result = unique.Values.OrderBy(item => item.Timestamp).ToList();
            if (result.Count > range.MaximumPoints)
            {
                result = result.Skip(result.Count - range.MaximumPoints).ToList();
            }

            return result;
        }

        internal async Task<Candle> FetchLatestCandleAsync(
            string instrumentId,
            RangeDefinition range,
            CancellationToken cancellationToken)
        {
            string path = BuildCandlePath("/api/v5/market/candles", instrumentId, range.RestBar, 2, null);
            List<Candle> candles = JsonParser.ParseCandles(await GetStringAsync(path, cancellationToken).ConfigureAwait(false));
            return candles.OrderBy(item => item.Timestamp).LastOrDefault();
        }

        internal async Task<List<Candle>> FetchMiniTickerCandlesAsync(
            string instrumentId,
            CancellationToken cancellationToken)
        {
            const int MiniTickerCandleCount = 48;
            const string MiniTickerBar = "5m";
            string path = BuildCandlePath(
                "/api/v5/market/candles",
                instrumentId,
                MiniTickerBar,
                MiniTickerCandleCount,
                null);
            List<Candle> candles = JsonParser.ParseCandles(
                await GetStringAsync(path, cancellationToken).ConfigureAwait(false));
            return candles.OrderBy(item => item.Timestamp).ToList();
        }

        internal async Task<MarketSnapshot> FetchTickerAsync(string instrumentId, CancellationToken cancellationToken)
        {
            string path = "/api/v5/market/ticker?instId=" + Uri.EscapeDataString(instrumentId);
            string json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
            return JsonParser.ParseTicker(json);
        }

        internal async Task RunRealtimeLoopAsync(
            string instrumentId,
            RangeDefinition range,
            Action<Candle> candleReceived,
            Action<ConnectionStatus, string> statusChanged,
            CancellationToken cancellationToken)
        {
            int failureCount = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                int reconnectDelay = NoReconnectDelay;
                try
                {
                    statusChanged(
                        failureCount == 0 ? ConnectionStatus.Connecting : ConnectionStatus.Reconnecting,
                        failureCount == 0 ? "连接行情" : "正在重连");
                    await ConnectAndReceiveAsync(instrumentId, range, candleReceived, statusChanged, cancellationToken)
                        .ConfigureAwait(false);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        throw new IOException("OKX WebSocket 已关闭。");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    reconnectDelay = ReconnectDelaysSeconds[Math.Min(failureCount - 1, ReconnectDelaysSeconds.Length - 1)];
                    Logger.Error("WebSocket 连接失败，" + reconnectDelay + " 秒后重连", ex);
                    statusChanged(ConnectionStatus.Offline, "行情已断开，数据可能陈旧");
                }

                if (reconnectDelay > NoReconnectDelay)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(reconnectDelay), cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ConnectAndReceiveAsync(
            string instrumentId,
            RangeDefinition range,
            Action<Candle> candleReceived,
            Action<ConnectionStatus, string> statusChanged,
            CancellationToken cancellationToken)
        {
            using (ClientWebSocket socket = new ClientWebSocket())
            {
                await socket.ConnectAsync(new Uri(WebSocketUrl), cancellationToken).ConfigureAwait(false);
                const string SubscriptionId = "tickerwindow";
                string subscription = "{\"id\":\"" + SubscriptionId + "\",\"op\":\"subscribe\",\"args\":[{\"channel\":\""
                    + range.WebSocketChannel + "\",\"instId\":\"" + instrumentId + "\"}]}";
                await SendTextAsync(socket, subscription, cancellationToken).ConfigureAwait(false);
                Logger.Info("WebSocket 已连接：" + instrumentId + " / " + range.WebSocketChannel);
                statusChanged(ConnectionStatus.Live, "实时行情");

                object heartbeatSync = new object();
                DateTime lastReceived = DateTime.UtcNow;
                DateTime lastPing = DateTime.MinValue;
                SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

                using (Timer heartbeat = new Timer(
                    delegate
                    {
                        DateTime received;
                        DateTime ping;
                        lock (heartbeatSync)
                        {
                            received = lastReceived;
                            ping = lastPing;
                        }

                        TimeSpan idle = DateTime.UtcNow - received;
                        if (ping != DateTime.MinValue && received < ping && DateTime.UtcNow - ping > TimeSpan.FromSeconds(HeartbeatFailureSeconds))
                        {
                            Logger.Info("WebSocket 心跳超时，准备重连。");
                            socket.Abort();
                            return;
                        }

                        if (idle > TimeSpan.FromSeconds(HeartbeatIdleSeconds) && socket.State == WebSocketState.Open)
                        {
                            lock (heartbeatSync)
                            {
                                lastPing = DateTime.UtcNow;
                            }

                            Task sendTask = SendHeartbeatAsync(socket, sendLock, cancellationToken);
                            sendTask.ContinueWith(
                                task => Logger.Error("发送 WebSocket 心跳失败", task.Exception),
                                CancellationToken.None,
                                TaskContinuationOptions.OnlyOnFaulted,
                                TaskScheduler.Default);
                        }
                    },
                    null,
                    TimeSpan.FromSeconds(HeartbeatCheckSeconds),
                    TimeSpan.FromSeconds(HeartbeatCheckSeconds)))
                {
                    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                    {
                        string message = await ReceiveTextAsync(socket, cancellationToken).ConfigureAwait(false);
                        lock (heartbeatSync)
                        {
                            lastReceived = DateTime.UtcNow;
                        }

                        Candle candle = JsonParser.ParseWebSocketCandle(message);
                        if (candle != null)
                        {
                            candleReceived(candle);
                        }
                    }
                }

                sendLock.Dispose();
            }
        }

        private static async Task SendHeartbeatAsync(
            ClientWebSocket socket,
            SemaphoreSlim sendLock,
            CancellationToken cancellationToken)
        {
            await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await SendTextAsync(socket, "ping", cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task<string> GetStringAsync(string relativePath, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            using (TimeoutWebClient client = new TimeoutWebClient(HttpTimeoutSeconds * 1000))
            using (CancellationTokenRegistration registration = cancellationToken.Register(client.CancelAsync))
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.UserAgent] = "StockPerpTicker/1.0";
                try
                {
                    string content = await client.DownloadStringTaskAsync(new Uri(RestBaseUrl + relativePath)).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return content;
                }
                catch (WebException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    throw;
                }
            }
        }

        private static string BuildCandlePath(
            string endpoint,
            string instrumentId,
            string bar,
            int limit,
            long? after)
        {
            StringBuilder path = new StringBuilder(endpoint);
            path.Append("?instId=").Append(Uri.EscapeDataString(instrumentId));
            path.Append("&bar=").Append(Uri.EscapeDataString(bar));
            path.Append("&limit=").Append(limit);
            if (after.HasValue)
            {
                path.Append("&after=").Append(after.Value);
            }

            return path.ToString();
        }

        private static void AddCandles(Dictionary<long, Candle> destination, IEnumerable<Candle> candles)
        {
            foreach (Candle candle in candles)
            {
                destination[candle.Timestamp] = candle;
            }
        }

        private static async Task SendTextAsync(ClientWebSocket socket, string message, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);
        }

        private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[ReceiveBufferBytes];
            using (MemoryStream stream = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        throw new IOException("OKX WebSocket 请求关闭连接。");
                    }

                    stream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("OkxMarketClient");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int _timeoutMilliseconds;

            internal TimeoutWebClient(int timeoutMilliseconds)
            {
                _timeoutMilliseconds = timeoutMilliseconds;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request != null)
                {
                    request.Timeout = _timeoutMilliseconds;
                    HttpWebRequest httpRequest = request as HttpWebRequest;
                    if (httpRequest != null)
                    {
                        httpRequest.ReadWriteTimeout = _timeoutMilliseconds;
                        httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    }
                }

                return request;
            }
        }
    }
}
