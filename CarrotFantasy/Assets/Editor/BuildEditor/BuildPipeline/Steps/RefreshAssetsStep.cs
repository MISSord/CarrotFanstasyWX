using UnityEditor;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 显式刷新 AssetDatabase。
    /// <para>
    /// 改造前 <c>AssetDatabase.Refresh()</c> 藏在 GenerateManifest 末尾，
    /// 拆成独立步骤后，"哪一步触发了重导入"在日志里是可见的。
    /// </para>
    /// </summary>
    public sealed class RefreshAssetsStep : IBuildStep
    {
        public string Name
        {
            get { return "RefreshAssets"; }
        }

        public bool Required
        {
            get { return false; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            AssetDatabase.Refresh();
            return BuildStepResult.Success();
        }
    }
}