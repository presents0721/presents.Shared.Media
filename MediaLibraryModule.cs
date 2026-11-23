using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Alife.Framework;
using Alife.Function.FunctionCaller;
using Microsoft.Extensions.Logging;

namespace Presents.Shared.Media;

/// <summary>
/// 媒体公共库：本身不干业务，只给其它插件提供 OCR 等共用能力。
/// 顺带暴露一个 media_ocr 工具——主人发张图说「这上面写的啥」时可以直接读。
/// </summary>
[Module("媒体公共库",
    "几个媒体类插件共用的小工具：画面文字 OCR（Windows 自带引擎，零依赖、不出网）与文本清理。"
    + "被视频陪看 / 游戏陪玩 / B站刷视频当作依赖引用；也可以直接调 media_ocr 读任意图片里的文字。",
    defaultCategory: "presents/工具")]
public partial class MediaLibraryModule(
    XmlFunctionCaller functionCaller,
    Interactor<MediaLibraryModule> interactor,
    ILogger<MediaLibraryModule> logger) : ChatBehaviour
{
    public const string Version = "1.0.0";

    protected override Task OnAwake()
    {
        // 只挂 [XmlFunction] 特性还不够：必须把 handler 注册给函数调用器，media_ocr 才会真正出现在工具列表里
        XmlHandler handler = new(this)
        {
            Description = "媒体公共库：读取图片里的文字（Windows 自带 OCR，零依赖、不出网）。",
            Explanation = "用户发图问「上面写的什么」、或需要图片原文时调用 media_ocr(path=\"图片绝对路径\", language=\"zh-Hans/en-US/ja\", maxChars=600)。"
        };
        functionCaller.RegisterHandler(handler, DocumentMode.Implicit, DestroyCancellationToken);

        logger.LogInformation("媒体公共库：已加载（OCR 语言 {Lang}，media_ocr 已注册）", "zh-Hans");
        return Task.CompletedTask;
    }

    [XmlFunction(FunctionMode.OneShot, "media_ocr")]
    [Description("读一张图片里的文字（本机 Windows OCR，零依赖、不出网）。用户发图问「上面写的什么」、或需要图片原文时调用。")]
    public async Task<string> ReadImageText(
        [Description("图片绝对路径")] string path,
        [Description("语言：zh-Hans / en-US / ja，默认 zh-Hans")] string? language = null,
        [Description("最多返回多少字，默认 600")] int? maxChars = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return $"[媒体公共库] 图片不存在：{path}";

            string text = await MediaOcr.ReadAsync(
                path.Trim(),
                string.IsNullOrWhiteSpace(language) ? "zh-Hans" : language.Trim(),
                Math.Clamp(maxChars ?? 600, 80, 4000)).ConfigureAwait(false);

            string answer = text.Length > 0
                ? $"[媒体公共库·OCR]\n{text}\n图片：{path}"
                : $"[媒体公共库·OCR] 这张图没读到文字（可能是纯画面）。\n图片：{path}";

            interactor.Poke(answer);
            return answer;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "媒体公共库：OCR 失败");
            return "[媒体公共库] OCR 失败：" + e.Message;
        }
    }
}
