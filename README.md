# 媒体公共库（presents.Shared.Media）

几个媒体类插件共用的一小块地基：**画面文字 OCR** 与 **OCR 文本清理**。
被视频陪看、游戏陪玩、B站刷视频引用，避免同一套逻辑复制四份。

## 提供什么

| 能力 | 说明 |
|---|---|
| `MediaOcr.ReadAsync(path, language, maxChars)` | 用 Windows 自带 OCR 读图片文字；返回一行（最多 14 行拼起来） |
| `MediaOcr.CleanLine(raw)` | 合并中文字间空格（「胜 利」→「胜利」）、丢掉符号噪音行 |

- **零依赖、不出网**：`Windows.Media.Ocr` 是系统组件，不装模型、不上传图片。
- 语言：`zh-Hans` / `en-US` / `ja` 等；系统没装对应语言包会自动退回用户默认语言。
- 定位：**本地兜底**。视觉模型更懂版式与图形；OCR 更快、更准地拿「原文」（代码、公式、数字、表格）。

## 附带工具：`media_ocr`

```
media_ocr(path="C:\\path\\to\\image.png")                 → 读这张图的文字
media_ocr(path="...", language="en-US")     → 英文图
media_ocr(path="...", maxChars=1500)        → 提高上限
```

用户发图问「上面写的什么」，或需要图片原文时可以直接调。

## 谁在用它

| 插件 | 用法 |
|---|---|
| presents.WatchTogether | 实时帧 OCR：PPT/图表讲解没有语音台词时，读画面文字当素材；`watch_together(ocr=true)` |
| presents.GameWatch | 视觉模型没配时读屏上文字（血量/比分/任务/台词） |
| presents.BiliScroll | 关键帧 OCR：无视觉模型也能读 PPT 原文；有模型时可附原文（可关） |

## 设计取舍

- **为什么不做成 NuGet / 独立 dll**：Alife 插件之间靠 manifest `Dependencies` 引用，公共库做成一个 plugin 最省事，也能单独热重载。
- **为什么 OCR 不带版面结构**：`Windows.Media.Ocr` 只给文本行，没有坐标语义；需要版面理解时应该用视觉模型，OCR 不冒充。
- **文本清理的阈值**（有效字符 ≥60%、符号 ≤25%）是实测调出来的：不放宽会混进边框噪音，不放严会把「血量 80%」这类整行丢掉。

## 文件

```
presents.Shared.Media/
  MediaOcr.cs              OCR + 文本清理（核心）
  MediaLibraryModule.cs    模块壳 + media_ocr 工具
  manifest.json            依赖：Alife.Function.FunctionCaller
  README.md
```
