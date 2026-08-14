using System;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>资源下载确认：Title / Message / Btn_Confirm / Btn_Cancel</summary>
    public sealed class AotDownloadConfirmView : AotResourcesView
    {
        protected override string ResourcesPath
        {
            get { return "AotUI/DownloadConfirm"; }
        }

        Action onConfirm;
        Action onCancel;

        public void Setup(long downloadSizeBytes, Action onConfirm, Action onCancel)
        {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;
            if (this.IsOpen)
            {
                this.Bind(downloadSizeBytes);
            }
        }

        public bool Open(long downloadSizeBytes, Action onConfirm, Action onCancel, Transform parent = null)
        {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;
            if (!base.Open(parent))
            {
                return false;
            }

            this.Bind(downloadSizeBytes);
            return true;
        }

        protected override void OnOpen()
        {
        }

        void Bind(long downloadSizeBytes)
        {
            Text title = this.NameTable.GetComponentSafely<Text>("Title");
            if (title != null)
            {
                title.text = "资源更新";
            }

            Text message = this.NameTable.GetComponentSafely<Text>("Message");
            if (message != null)
            {
                message.text = "发现新版本资源，需要下载 " + FormatBytes(downloadSizeBytes) + "。";
            }

            Button confirm = this.NameTable.GetComponentSafely<Button>("Btn_Confirm");
            if (confirm != null)
            {
                confirm.onClick.RemoveAllListeners();
                confirm.onClick.AddListener(() => this.onConfirm?.Invoke());
                Text confirmLabel = confirm.GetComponentInChildren<Text>();
                if (confirmLabel != null)
                {
                    confirmLabel.text = "下载";
                }
            }

            Button cancel = this.NameTable.GetComponentSafely<Button>("Btn_Cancel");
            if (cancel != null)
            {
                cancel.onClick.RemoveAllListeners();
                cancel.onClick.AddListener(() => this.onCancel?.Invoke());
                Text cancelLabel = cancel.GetComponentInChildren<Text>();
                if (cancelLabel != null)
                {
                    cancelLabel.text = "退出";
                }
            }
        }

        static string FormatBytes(long bytes)
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

    /// <summary>下载进度：Title / Status / Progress / ProgressFill / Info</summary>
    public sealed class AotDownloadProgressView : AotResourcesView
    {
        protected override string ResourcesPath
        {
            get { return "AotUI/DownloadProgress"; }
        }

        AssetBundleDownloader downloader;
        Image progressFill;
        Text statusText;
        Text infoText;
        Text titleText;

        public bool Open(AssetBundleDownloader downloader, Transform parent = null)
        {
            this.downloader = downloader;
            return base.Open(parent);
        }

        protected override void OnOpen()
        {
            this.titleText = this.NameTable.GetComponentSafely<Text>("Title");
            this.statusText = this.NameTable.GetComponentSafely<Text>("Status");
            this.infoText = this.NameTable.GetComponentSafely<Text>("Info");
            this.progressFill = this.NameTable.GetComponentSafely<Image>("ProgressFill");

            if (this.titleText != null)
            {
                this.titleText.text = "资源下载中";
            }

            if (this.progressFill != null)
            {
                this.progressFill.type = Image.Type.Filled;
                this.progressFill.fillMethod = Image.FillMethod.Horizontal;
                this.progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                this.progressFill.fillAmount = 0f;
            }
        }

        /// <summary>由 Runner 每帧调用。</summary>
        public void Refresh()
        {
            if (this.downloader == null || !this.IsOpen)
            {
                return;
            }

            LoaderState loaderState = this.downloader.GetLoaderState();
            bool isConverting = loaderState == LoaderState.Convert;
            bool isIdle = loaderState == LoaderState.Idle;

            if (isConverting || isIdle)
            {
                if (this.statusText != null)
                {
                    this.statusText.text = isIdle ? "下载完成，准备进入游戏" : "解压中，请稍候...";
                    this.statusText.gameObject.SetActive(true);
                }

                if (this.infoText != null)
                {
                    this.infoText.gameObject.SetActive(false);
                }

                if (this.progressFill != null)
                {
                    this.progressFill.fillAmount = isIdle ? 1f : this.progressFill.fillAmount;
                }

                return;
            }

            float progress = this.downloader.GetTotalProgress();
            if (this.progressFill != null)
            {
                this.progressFill.fillAmount = Mathf.Clamp01(progress);
            }

            if (this.statusText != null)
            {
                this.statusText.text = (progress * 100f).ToString("F1") + "%";
                this.statusText.gameObject.SetActive(true);
            }

            if (this.infoText != null)
            {
                long downloaded = this.downloader.GetDownloadedBytes();
                long total = this.downloader.GetTotalDownloadSize();
                this.infoText.text =
                    FormatBytes(downloaded) + " / " + FormatBytes(total) + "   " +
                    this.downloader.GetDownloadSpeedText();
                this.infoText.gameObject.SetActive(true);
            }
        }

        static string FormatBytes(long bytes)
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

    /// <summary>清单失败：Title / Message / Btn_Confirm / Btn_Cancel（退出 / 重启）</summary>
    public sealed class AotUpdateListErrorView : AotResourcesView
    {
        protected override string ResourcesPath
        {
            get { return "AotUI/UpdateListError"; }
        }

        Action onExit;
        Action onRestart;

        public bool Open(Action onExit, Action onRestart, Transform parent = null)
        {
            this.onExit = onExit;
            this.onRestart = onRestart;
            if (!base.Open(parent))
            {
                return false;
            }

            this.Bind();
            return true;
        }

        protected override void OnOpen()
        {
        }

        void Bind()
        {
            Text title = this.NameTable.GetComponentSafely<Text>("Title");
            if (title != null)
            {
                title.text = "热更新异常";
            }

            Text message = this.NameTable.GetComponentSafely<Text>("Message");
            if (message != null)
            {
                message.text = "获取热更新列表有问题，请重启游戏。";
            }

            Button exitBtn = this.NameTable.GetComponentSafely<Button>("Btn_Confirm");
            if (exitBtn != null)
            {
                exitBtn.onClick.RemoveAllListeners();
                exitBtn.onClick.AddListener(() => this.onExit?.Invoke());
                Text label = exitBtn.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = "退出游戏";
                }
            }

            Button restartBtn = this.NameTable.GetComponentSafely<Button>("Btn_Cancel");
            if (restartBtn != null)
            {
                restartBtn.onClick.RemoveAllListeners();
                restartBtn.onClick.AddListener(() => this.onRestart?.Invoke());
                Text label = restartBtn.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = "重启游戏";
                }
            }
        }
    }

    /// <summary>清单失败有缓存：Title / Message / Btn_Confirm / Btn_Cancel（继续 / 退出）</summary>
    public sealed class AotUpdateListFallbackView : AotResourcesView
    {
        protected override string ResourcesPath
        {
            get { return "AotUI/UpdateListFallback"; }
        }

        Action onContinue;
        Action onExit;

        public bool Open(Action onContinue, Action onExit, Transform parent = null)
        {
            this.onContinue = onContinue;
            this.onExit = onExit;
            if (!base.Open(parent))
            {
                return false;
            }

            this.Bind();
            return true;
        }

        protected override void OnOpen()
        {
        }

        void Bind()
        {
            Text title = this.NameTable.GetComponentSafely<Text>("Title");
            if (title != null)
            {
                title.text = "热更新异常";
            }

            Text message = this.NameTable.GetComponentSafely<Text>("Message");
            if (message != null)
            {
                message.text = "拉取最新资源失败，是否依然进行游戏？";
            }

            Button continueBtn = this.NameTable.GetComponentSafely<Button>("Btn_Confirm");
            if (continueBtn != null)
            {
                continueBtn.onClick.RemoveAllListeners();
                continueBtn.onClick.AddListener(() => this.onContinue?.Invoke());
                Text label = continueBtn.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = "继续游戏";
                }
            }

            Button exitBtn = this.NameTable.GetComponentSafely<Button>("Btn_Cancel");
            if (exitBtn != null)
            {
                exitBtn.onClick.RemoveAllListeners();
                exitBtn.onClick.AddListener(() => this.onExit?.Invoke());
                Text label = exitBtn.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = "退出游戏";
                }
            }
        }
    }
}
