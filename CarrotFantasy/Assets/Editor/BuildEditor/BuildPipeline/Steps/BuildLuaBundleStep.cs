namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 打包 Lua 脚本 bundle。
    /// <para>
    /// 必须早于 AB 打包：<c>lua_bundle.bytes</c> 是 AB 的一个资产，漏打的话包里是旧脚本，
    /// 症状是「改了 Lua 没生效」，比打包失败难查，所以失败即中止整次打包。
    /// </para>
    /// </summary>
    public sealed class BuildLuaBundleStep : IBuildStep
    {
        public string Name
        {
            get { return "BuildLuaBundle"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            if (!CarrotFantasy.Editor.LuaBundleBuilder.Build(out string summary))
            {
                return BuildStepResult.Failed("Lua Bundle 打包失败：\n" + summary);
            }

            return BuildStepResult.Success(summary.Replace('\n', ' '));
        }
    }
}