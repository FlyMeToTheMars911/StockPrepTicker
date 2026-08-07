# StockPerpTicker

一个面向 Windows 11 的轻量级 OKX 股票/加密资产 USDT 永续行情桌面小窗。程序使用系统自带的 .NET Framework WinForms 和 GDI+ 自绘，不包含浏览器内核，也不需要 API Key。

## 运行

直接运行：

```text
bin\Release\StockPerpTicker.exe
```

首次启动默认显示 `RAM-USDT-SWAP`。窗口右上角按钮可以切换置顶；关闭窗口会隐藏到系统托盘，使用托盘菜单的“彻底退出”才会结束进程。

程序采用单实例运行：再次点击任务栏图标或重复运行 EXE 不会创建新进程，而是恢复并显示已有窗口。

底部观察范围及对应的 K 线周期：

- `1天`：5 分钟 K 线
- `5天`：30 分钟 K 线
- `1个月`：4 小时 K 线
- `全部`：日 K，最多绘制 600 个数据点

当前 K 线周期和界面刷新频率会显示在窗口顶部的连接状态旁，例如：`实时行情 · 5分钟K线 · 1秒刷新`。

## 修改行情品种

使用文本编辑器打开 EXE 同目录下的 `config.json`，修改后重启程序：

```json
{
  "instrumentId": "AAPL-USDT-SWAP",
  "refreshIntervalMilliseconds": 1000,
  "movingAverages": [5, 10, 20, 50],
  "showTaskbarTickerOnMinimize": true,
  "taskbarTickerPosition": "bottomRight"
}
```

配置无效或合约不可交易时，程序会在窗口中显示错误，不会静默切回 RAM。

配置项说明：

- `instrumentId`：OKX 完整永续合约 ID。
- `refreshIntervalMilliseconds`：WebSocket 最新数据合并到界面的间隔，允许 `250` 至 `60000` 毫秒；网络连接仍持续接收数据，不会因为降低界面刷新频率而断开。
- `movingAverages`：需要显示的移动平均线周期。支持 `5、10、20、50、100、200`，分别对应 MA5、MA10、MA20、MA50、MA100、MA200；设置为 `[]` 可关闭全部均线。
- `showTaskbarTickerOnMinimize`：设为 `true` 时，点击最小化会把主窗口隐藏到托盘并显示迷你行情条；设为 `false` 时只隐藏到托盘。
- `taskbarTickerPosition`：迷你行情条的位置。`bottomLeft` 表示屏幕左下角，`bottomRight` 表示屏幕右下角；不填写时默认使用 `bottomRight`。配置值不合法时窗口会显示具体错误。

移动平均线按照当前 K 线周期计算。例如在“1天”视图中使用 5 分钟 K 线，MA5 表示最近 5 根 5 分钟 K 线的收盘价平均；切换到“全部”后，MA5 表示最近 5 根日 K 线的收盘价平均。

迷你行情条采用紧凑布局，只包含代码、最新价格、加粗的 24 小时涨跌幅和最近 K 线收盘价折线。单击行情条可恢复并激活主窗口。受 Windows 11 任务栏扩展限制，行情条采用紧贴任务栏上沿的轻量无边框窗口，不向 Explorer 进程注入插件。

## 构建

在 PowerShell 中执行：

```powershell
.\build.ps1
```

构建脚本使用 Windows 自带的 .NET Framework MSBuild 和 C# 编译器，不下载 SDK、不使用 NuGet，也不会修改 `E:\demoRepo` 中的其他项目。

构建时会运行 `generate-icon.ps1`，生成包含 16～256 像素多尺寸的 `assets\StockPerpTicker.ico`，并把图标作为 Win32 资源嵌入 EXE。更换程序版本后，如果任务栏仍显示旧缓存图标，请先取消固定，再从新版 EXE 重新固定。

## 本地数据

- 窗口状态：`%LocalAppData%\StockPerpTicker\state.json`
- 日志：`%LocalAppData%\StockPerpTicker\logs\app.log`
- 开机启动：当前用户的 Windows `Run` 注册表项，可在托盘菜单中开关

日志只记录启动、配置、连接、重连和异常；单个文件最多 1MB，总共最多保留 3 个文件。

## 说明

本程序只展示 OKX 合约成交行情，不提供交易功能。股票永续合约不代表真实股票或 ETF 所有权，其价格可能与对应证券存在偏差。行情接口或合约状态由 OKX 提供，网络断开时窗口会明确标记数据陈旧。
