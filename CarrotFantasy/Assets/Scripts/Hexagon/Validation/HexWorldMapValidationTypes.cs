using System.Collections.Generic;
using System.Text;

/// <summary>地图配置校验严重级别。</summary>
public enum HexWorldMapValidationSeverity
{
	Info = 0,
	Warning = 1,
	Error = 2
}

/// <summary>单条校验结果。</summary>
public sealed class HexWorldMapValidationIssue
{
	public HexWorldMapValidationSeverity severity;
	public string ruleId;
	public string message;
	public int pointId;
	public int q;
	public int r;

	public bool HasLocation {
		get { return q != int.MinValue; }
	}

	public static HexWorldMapValidationIssue Create (
		HexWorldMapValidationSeverity severity,
		string ruleId,
		string message,
		HexMapPointData point = default,
		bool hasPoint = false
	)
	{
		return new HexWorldMapValidationIssue {
			severity = severity,
			ruleId = ruleId,
			message = message,
			pointId = hasPoint ? point.pointId : 0,
			q = hasPoint ? point.q : int.MinValue,
			r = hasPoint ? point.r : int.MinValue
		};
	}

	public static HexWorldMapValidationIssue AtCoord (
		HexWorldMapValidationSeverity severity,
		string ruleId,
		string message,
		int q,
		int r
	)
	{
		return new HexWorldMapValidationIssue {
			severity = severity,
			ruleId = ruleId,
			message = message,
			q = q,
			r = r
		};
	}
}

/// <summary>校验报告汇总。</summary>
public sealed class HexWorldMapValidationReport
{
	public readonly List<HexWorldMapValidationIssue> issues = new List<HexWorldMapValidationIssue>();

	public bool HasErrors {
		get {
			for (int i = 0; i < issues.Count; i++) {
				if (issues[i].severity == HexWorldMapValidationSeverity.Error) {
					return true;
				}
			}
			return false;
		}
	}

	public bool HasWarnings {
		get {
			for (int i = 0; i < issues.Count; i++) {
				if (issues[i].severity == HexWorldMapValidationSeverity.Warning) {
					return true;
				}
			}
			return false;
		}
	}

	public void Add (HexWorldMapValidationIssue issue)
	{
		issues.Add(issue);
	}

	public string BuildSummary (int maxLines = 12)
	{
		var sb = new StringBuilder();
		int shown = 0;
		for (int i = 0; i < issues.Count && shown < maxLines; i++) {
			HexWorldMapValidationIssue issue = issues[i];
			sb.Append('[').Append(issue.severity).Append("] ")
				.Append(issue.ruleId).Append(": ").Append(issue.message);
			if (issue.HasLocation) {
				sb.Append(" (").Append(issue.q).Append(',').Append(issue.r).Append(')');
			}
			sb.AppendLine();
			shown++;
		}
		if (issues.Count > maxLines) {
			sb.Append("... 共 ").Append(issues.Count).Append(" 条，详见 Console。");
		}
		return sb.ToString();
	}

	public void LogToConsole (string mapName)
	{
		UnityEngine.Debug.Log(
			"HexWorldMap validation for '" + mapName + "': " +
			issues.Count + " issue(s).\n" + BuildSummary(64)
		);
	}
}
