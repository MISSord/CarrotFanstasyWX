namespace CarrotFantasy
{
    /// <summary>构建通道相关常量（与 Player Settings Scripting Define 名称一致）。</summary>
    public static class BuildChannelDefines
    {
        /// <summary>开发工具宏：Log / GM 等仅在定义该符号时编入 Player。</summary>
        public const string DevTools = "CF_DEV_TOOLS";

        public const string EnvDev = "dev";
        public const string EnvStaging = "staging";
        public const string EnvProd = "prod";
    }
}
