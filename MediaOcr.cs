using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if WINDOWS
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
#endif

namespace Presents.Shared.Media;

/// <summary>
/// 画面文字 OCR（Windows 自带引擎）。四个媒体插件共用这一份，避免逻辑漂移。
///
/// 定位：**本地兜底**。有视觉模型时视觉模型更懂版式与图形；没有视觉模型、
/// 或者只想要「画面上的原文」（代码、公式、数字、表格），OCR 更快更准也更省。
/// 零依赖、不出网、不装模型。
/// </summary>
public static class MediaOcr
{
    /// <summary>读一张图的文字（最多 14 行，清理后拼成一行）。</summary>
    public static async Task<string> ReadAsync(string imagePath, string languageTag = "zh-Hans", int maxChars = 600)
    {
#if WINDOWS
        try
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return "";

            OcrEngine? engine = CreateEngine(languageTag);
            if (engine == null) return "";

            StorageFile file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(imagePath));
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync();

            OcrResult result = await engine.RecognizeAsync(bitmap);

            var lines = new List<string>();

            foreach (OcrLine line in result.Lines)
            {
                string text = CleanLine(line.Text ?? "");
                if (text.Length >= 2 && text.Length <= 100) lines.Add(text);
            }

            if (lines.Count == 0) return "";

            string joined = string.Join(" / ", lines.Take(14));
            return joined.Length <= maxChars ? joined : joined[..Math.Max(20, maxChars)];
        }
        catch
        {
            return "";
        }
#else
        await Task.CompletedTask;
        return "";
#endif
    }

    /// <summary>
    /// 清理 OCR 行：合并中文之间的空格（引擎常输出「胜 利」），
    /// 丢掉有效字符占比太低或符号过多的噪音行。
    /// </summary>
    public static string CleanLine(string raw)
    {
        string text = (raw ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        if (text.Length == 0) return "";

        var sb = new StringBuilder(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c != ' ')
            {
                sb.Append(c);
                continue;
            }

            char prev = sb.Length > 0 ? sb[^1] : '\0';
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            bool prevCjk = IsCjk(prev) || char.IsDigit(prev);
            bool nextCjk = IsCjk(next) || char.IsDigit(next);

            if (prevCjk && nextCjk) continue;                                // 「胜 利」→「胜利」
            if (prevCjk && char.IsLetter(next) && !IsCjk(next)) continue;    // 「血 量80」

            sb.Append(' ');
        }

        string result = sb.ToString().Trim();
        if (result.Length == 0) return "";

        int useful = result.Count(c => char.IsLetterOrDigit(c) || IsCjk(c));
        int punct = result.Count(c => !char.IsLetterOrDigit(c) && !IsCjk(c) && c != ' ');

        if (useful < 2) return "";
        if (useful * 100 / result.Length < 60) return "";
        if (punct * 100 / result.Length > 25) return "";

        return result;
    }

    static bool IsCjk(char c)
        => c >= 0x4E00 && c <= 0x9FFF || c >= 0x3400 && c <= 0x4DBF;

#if WINDOWS
    static OcrEngine? CreateEngine(string languageTag)
    {
        try
        {
            var language = new Language(string.IsNullOrWhiteSpace(languageTag) ? "zh-Hans" : languageTag);
            OcrEngine? engine = OcrEngine.TryCreateFromLanguage(language);
            if (engine != null) return engine;
        }
        catch
        {
            // 语言包没装，退回默认
        }

        try
        {
            return OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch
        {
            return null;
        }
    }
#endif
}
