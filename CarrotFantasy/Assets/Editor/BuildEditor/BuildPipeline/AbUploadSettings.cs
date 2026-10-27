namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// Profile 注入给"上传步骤"的 SFTP 配置。
    /// <para>
    /// 密码**不放在这里**：它只保存在本机 EditorPrefs（<c>AB_DeployPassword</c>），
    /// 避免把凭据写进会提交到 Git 的资产。
    /// </para>
    /// </summary>
    public sealed class AbUploadSettings
    {
        public bool Enabled;
        public string Host;
        public int Port = AssetBundleDeploySettings.DefaultPort;
        public string User;
        public string RemotePath;
        public bool UsePrivateKey;
        public string PrivateKeyPath;

        /// <summary>把连接信息写入本机 EditorPrefs；密码沿用已保存的值（不经过资产）。</summary>
        public void ApplyToDeploySettings()
        {
            AssetBundleDeploySettings.Host = this.Host;
            AssetBundleDeploySettings.Port = this.Port;
            AssetBundleDeploySettings.User = this.User;
            AssetBundleDeploySettings.RemotePath = this.RemotePath;
            AssetBundleDeploySettings.UsePrivateKey = this.UsePrivateKey;
            AssetBundleDeploySettings.PrivateKeyPath = this.PrivateKeyPath;
        }
    }
}
