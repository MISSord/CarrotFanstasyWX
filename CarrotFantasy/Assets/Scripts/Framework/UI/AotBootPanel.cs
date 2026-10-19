using System;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 启动期**统一面板**：一个预制体（<c>Resources/AotUI/BootPanel</c>）内含四个子节点，
    /// 由状态机切换显示，不再为每个阶段各开一个界面。
    /// <para>
    /// 预制体由菜单 <c>Tools/UI/生成启动面板（合并 4 个）</c> 自动生成：
    /// 四个子根分别叫 <c>Confirm</c> / <c>Progress</c> / <c>Error</c> / <c>Fallback</c>，
    /// 节点键名带面板前缀（如 <c>Confirm/Title</c>），避免四个面板的 Title / Btn_Confirm 重名冲突。
    /// </para>
    /// <para>
    /// 为什么需要前缀：<see cref="UINameTableDic"/> 以名字为键，重名会报错并丢弃，
    /// 而原四个预制体有 4 个 Title、3 个 Btn_Confirm。
    /// </para>
    /// </summary>
    public sealed class AotBootPanel : AotResourcesView
    {
        public enum Mode
        {
            None = 0,
            Confirm,
            Progress,
            Error,
            Fallback,
        }

        /// <summary>确认面板：点了"下载"。</summary>
        public event Action Confirmed;

        /// <summary>确认面板：点了"退出"。</summary>
        public event Action Cancelled;

        /// <summary>回退面板：选择继续（用本地清单进游戏）。</summary>
        public event Action ContinueChosen;

        /// <summary>错误 / 回退面板：选择退出进程。</summary>
        public event Action ExitChosen;

        private GameObject confirmRoot;
        private GameObject progressRoot;
        private GameObject errorRoot;
        private GameObject fallbackRoot;

        private Text confirmTitle;
        private Text confirmMessage;

        private Text progressTitle;
        private Text progressStatus;
        private Text progressInfo;
        private Image progressFill;

        private Text errorTitle;
        private Text errorMessage;

        private Text fallbackTitle;
        private Text fallbackMessage;

        /// <summary>当前显示的子面板。</summary>
        public Mode CurrentMode { get; private set; }

        protected override string ResourcesPath
        {
            get { return "AotUI/BootPanel"; }
        }

        /// <summary>
        /// 确保面板已创建并打开；已存在则直接复用。
        /// 返回 null 表示预制体缺失，调用方应转入失败流程。
        /// </summary>
        public static AotBootPanel Ensure(UpdateContext ctx)
        {
            if (ctx == null)
            {
                return null;
            }

            if (ctx.Panel != null && ctx.Panel.IsOpen)
            {
                return ctx.Panel;
            }

            AotBootPanel panel = new AotBootPanel();
            if (!panel.Open(ctx.Parent))
            {
                Debug.LogError("[AotBootPanel] 无法打开启动面板（AotUI/BootPanel 缺失？），启动界面将不可用");
                return null;
            }

            ctx.Panel = panel;
            return panel;
        }

        /// <summary>关闭并释放（由 Finalize / Failed 阶段调用）。</summary>
        public static void CloseIfAny(UpdateContext ctx)
        {
            if (ctx == null || ctx.Panel == null)
            {
                return;
            }

            ctx.Panel.Close();
            ctx.Panel = null;
        }

        protected override void OnOpen()
        {
            this.confirmRoot = this.FindNode("confirm");
            this.progressRoot = this.FindNode("progress");
            this.errorRoot = this.FindNode("error");
            this.fallbackRoot = this.FindNode("fallback");

            this.confirmTitle = this.FindText("confirm_title");
            this.confirmMessage = this.FindText("confirm_message");

            this.progressTitle = this.FindText("progress_title");
            this.progressStatus = this.FindText("progress_status");
            this.progressInfo = this.FindText("progress_info");
            this.progressFill = this.FindImage("progress_fill");

            this.errorTitle = this.FindText("error_title");
            this.errorMessage = this.FindText("error_message");

            this.fallbackTitle = this.FindText("fallback_title");
            this.fallbackMessage = this.FindText("fallback_message");

            if (this.progressFill != null)
            {
                this.progressFill.type = Image.Type.Filled;
                this.progressFill.fillMethod = Image.FillMethod.Horizontal;
                this.progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                this.progressFill.fillAmount = 0f;
            }

            this.SetMode(Mode.None);

            // 按钮只在打开时绑定一次；文案随模式变化。
            this.BindButton("confirm_btn_confirm", () => this.Raise(this.Confirmed));
            this.BindButton("confirm_btn_cancel", () => this.Raise(this.Cancelled));
            this.BindButton("error_btn_confirm", () => this.Raise(this.ExitChosen));
            this.BindButton("error_btn_cancel", null);   // 重启暂无实现，隐藏按钮
            this.BindButton("fallback_btn_confirm", () => this.Raise(this.ContinueChosen));
            this.BindButton("fallback_btn_cancel", () => this.Raise(this.ExitChosen));
        }

        /// <summary>切换显示的子面板（只 SetActive，不销毁重建）。</summary>
        public void SetMode(Mode mode)
        {
            this.CurrentMode = mode;

            SetActiveSafe(this.confirmRoot, mode == Mode.Confirm);
            SetActiveSafe(this.progressRoot, mode == Mode.Progress);
            SetActiveSafe(this.errorRoot, mode == Mode.Error);
            SetActiveSafe(this.fallbackRoot, mode == Mode.Fallback);
        }

        /// <summary>显示下载确认。</summary>
        public void ShowConfirm(long downloadSizeBytes)
        {
            this.SetMode(Mode.Confirm);

            if (this.confirmTitle != null)
            {
                this.confirmTitle.text = "资源更新";
            }

            if (this.confirmMessage != null)
            {
                this.confirmMessage.text =
                    "发现新版本资源，需要下载 " + FormatBytes(downloadSizeBytes) + "。";
            }

            SetButtonLabel("confirm_btn_confirm", "下载");
            SetButtonLabel("confirm_btn_cancel", "退出");
        }

        /// <summary>显示下载进度。</summary>
        public void ShowProgress()
        {
            this.SetMode(Mode.Progress);

            if (this.progressTitle != null)
            {
                this.progressTitle.text = "资源下载中";
            }

            if (this.progressFill != null)
            {
                this.progressFill.fillAmount = 0f;
            }

            if (this.progressStatus != null)
            {
                this.progressStatus.text = "0.0%";
                this.progressStatus.gameObject.SetActive(true);
            }

            if (this.progressInfo != null)
            {
                this.progressInfo.gameObject.SetActive(false);
            }
        }

        /// <summary>刷新进度条与字节信息（每帧调用）。</summary>
        public void SetProgress(float normalized, long downloadedBytes, long totalBytes, string speedText)
        {
            if (this.progressFill != null)
            {
                this.progressFill.fillAmount = Mathf.Clamp01(normalized);
            }

            if (this.progressStatus != null)
            {
                this.progressStatus.text = (Mathf.Clamp01(normalized) * 100f).ToString("F1") + "%";
                this.progressStatus.gameObject.SetActive(true);
            }

            if (this.progressInfo != null)
            {
                this.progressInfo.text =
                    FormatBytes(downloadedBytes) + " / " + FormatBytes(totalBytes) + "   " + (speedText ?? string.Empty);
                this.progressInfo.gameObject.SetActive(true);
            }
        }

        /// <summary>只改状态文案（如"解压中"），进度条保持不动。</summary>
        public void SetStatus(string text, bool fillFull = false)
        {
            if (this.progressStatus != null && !string.IsNullOrEmpty(text))
            {
                this.progressStatus.text = text;
                this.progressStatus.gameObject.SetActive(true);
            }

            if (this.progressInfo != null)
            {
                this.progressInfo.gameObject.SetActive(false);
            }

            if (fillFull && this.progressFill != null)
            {
                this.progressFill.fillAmount = 1f;
            }
        }

        /// <summary>显示失败信息。</summary>
        public void ShowError(string message)
        {
            this.SetMode(Mode.Error);

            if (this.errorTitle != null)
            {
                this.errorTitle.text = "资源更新失败";
            }

            if (this.errorMessage != null)
            {
                this.errorMessage.text = string.IsNullOrEmpty(message)
                    ? "请重启游戏后重试。"
                    : message + "\n请重启游戏后重试。";
            }

            SetButtonLabel("error_btn_confirm", "退出");
        }

        /// <summary>显示"远程不可用，是否用本地清单继续"。</summary>
        public void ShowFallback()
        {
            this.SetMode(Mode.Fallback);

            if (this.fallbackTitle != null)
            {
                this.fallbackTitle.text = "资源列表获取失败";
            }

            if (this.fallbackMessage != null)
            {
                this.fallbackMessage.text =
                    "无法获取最新资源列表，是否使用本地缓存继续进入游戏？\n" +
                    "（可能缺少部分最新内容）";
            }

            SetButtonLabel("fallback_btn_confirm", "继续");
            SetButtonLabel("fallback_btn_cancel", "退出");
        }

        // ---------------------------------------------------------------- 内部

        private GameObject FindNode(string key)
        {
            GameObject node = this.NameTable.GetGameObjectSafely(key);
            if (node == null)
            {
                Debug.LogWarning("[AotBootPanel] 找不到子面板节点: " + key + "（确认 BootPanel 预制体由合并工具生成）");
            }

            return node;
        }

        private Text FindText(string key)
        {
            return this.NameTable.GetComponentSafely<Text>(key);
        }

        private Image FindImage(string key)
        {
            return this.NameTable.GetComponentSafely<Image>(key);
        }

        private void BindButton(string key, Action action)
        {
            Button button = this.NameTable.GetComponentSafely<Button>(key);
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();

            if (action == null)
            {
                // 没有实现的操作直接隐藏按钮，而不是点了没反应。
                button.gameObject.SetActive(false);
                return;
            }

            button.onClick.AddListener(() => action());
        }

        private void SetButtonLabel(string key, string label)
        {
            Button button = this.NameTable.GetComponentSafely<Button>(key);
            if (button == null || string.IsNullOrEmpty(label))
            {
                return;
            }

            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private void Raise(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler();
            }
            catch (Exception e)
            {
                Debug.LogError("[AotBootPanel] 按钮回调异常: " + e);
            }
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }

            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024.0).ToString("F2") + " KB";
            }

            return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
        }
    }
}
