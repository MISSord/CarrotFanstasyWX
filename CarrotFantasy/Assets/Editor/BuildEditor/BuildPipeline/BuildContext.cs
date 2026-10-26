using System;
using System.Collections.Generic;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 一次打包流程的上下文：请求、路径、产物、步骤记录，以及步骤之间共享的临时数据。
    /// <para>
    /// 约定：
    /// </para>
    /// <list type="bullet">
    ///   <item>只在**一次** <see cref="BuildExecutor.Execute"/> 内有效，不跨域重载持久化；</item>
    ///   <item>共享数据用 <see cref="Set{T}"/>/<see cref="Get{T}"/> 按类型存取，
    ///         避免这个类慢慢长成 30 个字段的上帝对象；</item>
    ///   <item>下游步骤需要上游产物时用 <see cref="Require{T}"/> 断言，
    ///         缺了会直接报"哪个步骤没跑"，而不是后面 NRE。</item>
    /// </list>
    /// </summary>
    public sealed class BuildContext
    {
        private readonly Dictionary<Type, object> state = new Dictionary<Type, object>();

        /// <summary>打包请求（输出根、平台、压缩、开关）。</summary>
        public AssetBundleBuildPipeline.BuildRequest Request;

        /// <summary>最终结果（由收尾步骤或多个步骤共同填充）。</summary>
        public AssetBundleBuildPipeline.BuildResult Result;

        /// <summary>{工程根}/{Request.OutputRoot} 的绝对路径。</summary>
        public string OutputRootFullPath;

        /// <summary>{OutputRootFullPath}/{平台目录}，AB 实际输出目录。</summary>
        public string PlatformPath;

        /// <summary>步骤执行记录（执行器填写）。</summary>
        public readonly List<BuildStepRecord> Records = new List<BuildStepRecord>();

        public void Set<T>(T value) where T : class
        {
            if (value == null)
            {
                this.state.Remove(typeof(T));
                return;
            }

            this.state[typeof(T)] = value;
        }

        public T Get<T>() where T : class
        {
            object value;
            return this.state.TryGetValue(typeof(T), out value) ? value as T : null;
        }

        /// <summary>
        /// 取上游产物；不存在时抛异常，异常信息会带上调用方步骤名。
        /// <para>用于表达"我依赖某一步的产物"，比注释可靠。</para>
        /// </summary>
        public T Require<T>(string stepName) where T : class
        {
            T value = this.Get<T>();
            if (value == null)
            {
                throw new InvalidOperationException(
                    "步骤 " + stepName + " 需要 " + typeof(T).Name
                    + "，但它不存在——检查该步骤之前的列表顺序是否缺了对应步骤。");
            }

            return value;
        }

        public bool Has<T>() where T : class
        {
            return this.state.ContainsKey(typeof(T));
        }

        public void Record(string name, BuildStepStatus status, string message, long elapsedMs)
        {
            this.Records.Add(new BuildStepRecord
            {
                Name = name,
                Status = status,
                Message = message,
                ElapsedMilliseconds = elapsedMs,
            });
        }
    }
}
