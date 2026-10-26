using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>一次打包的汇总报告。</summary>
    public sealed class BuildReport
    {
        public bool Success;

        /// <summary>失败步骤名（成功时为 null）。</summary>
        public string FailedStep;

        public readonly List<BuildStepRecord> Records;

        public BuildReport(List<BuildStepRecord> records)
        {
            this.Records = records;
        }

        public long TotalMilliseconds
        {
            get
            {
                long total = 0;
                for (int i = 0; i < this.Records.Count; i++)
                {
                    total += this.Records[i].ElapsedMilliseconds;
                }

                return total;
            }
        }

        /// <summary>生成可直接贴到 Console 的表格。</summary>
        public string ToTable()
        {
            var sb = new StringBuilder();
            sb.Append("[AB Build] 步骤汇总（").Append(this.Records.Count).Append(" 步，总耗时 ")
              .Append(this.TotalMilliseconds).AppendLine(" ms）");

            for (int i = 0; i < this.Records.Count; i++)
            {
                BuildStepRecord r = this.Records[i];
                sb.Append("  ").Append((i + 1).ToString().PadLeft(2)).Append(". ");
                sb.Append(r.Name.PadRight(28));
                sb.Append(r.Status.ToString().PadRight(9));
                sb.Append(r.ElapsedMilliseconds.ToString().PadLeft(7)).Append(" ms");
                if (!string.IsNullOrEmpty(r.Message))
                {
                    sb.Append("   ").Append(r.Message);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 顺序执行步骤列表。
    /// <para>
    /// 这里**不做排序、不做反射发现**：传入的列表就是执行顺序，
    /// 平台差异也通过"传不同的列表"表达（见 <see cref="AbBuildStepCatalog"/>）。
    /// </para>
    /// </summary>
    public static class BuildExecutor
    {
        private const string LogTag = "[AB Build]";

        public static BuildReport Execute(
            BuildContext ctx,
            IReadOnlyList<IBuildStep> steps,
            BuildExecutionOptions options = null)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (steps == null || steps.Count == 0)
            {
                Debug.LogError(LogTag + " 步骤列表为空，什么都不会执行。");
                return new BuildReport(ctx.Records) { Success = false, FailedStep = "<empty>" };
            }

            var report = new BuildReport(ctx.Records);
            Debug.Log(string.Format("{0} 开始执行 {1} 个步骤（{2}）",
                LogTag, steps.Count, ctx.Request.BuildTarget));

            for (int i = 0; i < steps.Count; i++)
            {
                IBuildStep step = steps[i];
                if (step == null)
                {
                    continue;
                }

                if (IsFilteredOut(step.Name, options, out bool skippedByFilter))
                {
                    if (skippedByFilter)
                    {
                        ctx.Record(step.Name, BuildStepStatus.Skipped, "被 Only/Skip 过滤", 0);
                    }

                    continue;
                }

                if (options != null && options.DryRun)
                {
                    ctx.Record(step.Name, BuildStepStatus.Skipped, "DryRun", 0);
                    Debug.Log(string.Format("{0}   [DryRun] {1}", LogTag, step.Name));
                    continue;
                }

                var sw = Stopwatch.StartNew();
                BuildStepResult result;
                try
                {
                    result = step.Run(ctx) ?? BuildStepResult.Success();
                }
                catch (Exception e)
                {
                    sw.Stop();
                    ctx.Record(step.Name, BuildStepStatus.Failed, e.Message, sw.ElapsedMilliseconds);
                    Debug.LogError(string.Format("{0} 步骤 {1} 抛异常（{2} ms）：\n{3}",
                        LogTag, step.Name, sw.ElapsedMilliseconds, e));

                    if (step.Required)
                    {
                        report.Success = false;
                        report.FailedStep = step.Name;
                        Debug.LogError(LogTag + " 关键步骤失败，已中止。\n" + report.ToTable());
                        return report;
                    }

                    continue;
                }

                sw.Stop();
                ctx.Record(step.Name, result.Status, result.Message, sw.ElapsedMilliseconds);

                switch (result.Status)
                {
                    case BuildStepStatus.Success:
                        Debug.Log(string.Format("{0}   ✔ {1} ({2} ms) {3}",
                            LogTag, step.Name, sw.ElapsedMilliseconds, result.Message));
                        break;

                    case BuildStepStatus.Skipped:
                        Debug.Log(string.Format("{0}   - {1} 跳过：{2}", LogTag, step.Name, result.Message));
                        break;

                    case BuildStepStatus.Failed:
                        if (step.Required)
                        {
                            report.Success = false;
                            report.FailedStep = step.Name;
                            Debug.LogError(string.Format("{0} 关键步骤失败：{1} / {2}\n{3}",
                                LogTag, step.Name, result.Message, report.ToTable()));
                            return report;
                        }

                        Debug.LogWarning(string.Format("{0}   ! {1} 失败（非关键，继续）：{2}",
                            LogTag, step.Name, result.Message));
                        break;
                }
            }

            report.Success = true;
            Debug.Log(report.ToTable());
            return report;
        }

        private static bool IsFilteredOut(string name, BuildExecutionOptions options, out bool skippedByFilter)
        {
            skippedByFilter = false;
            if (options == null)
            {
                return false;
            }

            if (options.Only != null && options.Only.Count > 0 && !Contains(options.Only, name))
            {
                skippedByFilter = true;
                return true;
            }

            if (options.Skip != null && Contains(options.Skip, name))
            {
                skippedByFilter = true;
                return true;
            }

            return false;
        }

        private static bool Contains(IReadOnlyCollection<string> set, string name)
        {
            foreach (string item in set)
            {
                if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
