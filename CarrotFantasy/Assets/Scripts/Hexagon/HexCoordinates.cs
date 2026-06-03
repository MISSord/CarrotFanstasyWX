using UnityEngine;

[System.Serializable]
public struct HexCoordinates {

	[SerializeField]
	private int x, z;

	public int X {
		get {
			return x;
		}
	}

	public int Z {
		get {
			return z;
		}
	}

	public int Y {
		get {
			return -X - Z;
		}
	}

	public HexCoordinates (int x, int z) {
		this.x = x;
		this.z = z;
	}

	public static HexCoordinates FromOffsetCoordinates (int x, int z) {
		return new HexCoordinates(x - z / 2, z);
	}

	/// <summary>尖顶六边形、odd-r 布局下的六个轴向邻接方向。</summary>
	public static readonly HexCoordinates[] Directions = {
		new HexCoordinates(1, 0),
		new HexCoordinates(1, -1),
		new HexCoordinates(0, -1),
		new HexCoordinates(-1, 0),
		new HexCoordinates(-1, 1),
		new HexCoordinates(0, 1)
	};

	public HexCoordinates GetNeighbor (int direction) {
		HexCoordinates d = Directions[direction];
		return new HexCoordinates(X + d.X, Z + d.Z);
	}

	public static int GetDistance (HexCoordinates a, HexCoordinates b) {
		return (
			Mathf.Abs(a.X - b.X) +
			Mathf.Abs(a.Y - b.Y) +
			Mathf.Abs(a.Z - b.Z)
		) / 2;
	}

	/// <summary>轴向坐标转本地位置（odd-r），间距由 HexMetrics 控制。</summary>
	public Vector3 ToLocalPosition () {
		int col = X + Z / 2;
		int row = Z;
		Vector3 position;
		position.x = (col + row * 0.5f - row / 2) * HexMetrics.HorizontalSpacing;
		position.y = 0f;
		position.z = row * HexMetrics.VerticalSpacing;
		return position;
	}

	public static HexCoordinates FromPosition (Vector3 position) {
		float horizontal = HexMetrics.HorizontalSpacing;
		float vertical = HexMetrics.VerticalSpacing;

		float q = position.x / horizontal - position.z / (2f * vertical);
		float r = position.z / vertical;
		float s = -q - r;

		int iQ = Mathf.RoundToInt(q);
		int iR = Mathf.RoundToInt(r);
		int iS = Mathf.RoundToInt(s);

		float dQ = Mathf.Abs(q - iQ);
		float dR = Mathf.Abs(r - iR);
		float dS = Mathf.Abs(s - iS);

		if (dQ > dR && dQ > dS) {
			iQ = -iR - iS;
		}
		else if (dR > dS) {
			iR = -iQ - iS;
		}

		return new HexCoordinates(iQ, iR);
	}

	public override string ToString () {
		return "(" +
			X.ToString() + ", " + Y.ToString() + ", " + Z.ToString() + ")";
	}

	public string ToStringOnSeparateLines () {
		return X.ToString() + "\n" + Y.ToString() + "\n" + Z.ToString();
	}
}