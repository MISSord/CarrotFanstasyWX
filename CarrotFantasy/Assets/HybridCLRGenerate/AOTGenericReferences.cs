using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"CarrotFantasy.AOT.dll",
		"Google.Protobuf.dll",
		"System.Core.dll",
		"Unity.ThirdParty.dll",
		"UnityEngine.AssetBundleModule.dll",
		"UnityEngine.CoreModule.dll",
		"UnityEngine.JSONSerializeModule.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// AssetBundleManager.<>c__DisplayClass31_0<object>
	// AssetLoadCallback<object>
	// Google.Protobuf.IMessage<object>
	// Google.Protobuf.MessageParser.<>c__DisplayClass2_0<object>
	// Google.Protobuf.MessageParser<object>
	// System.Action<CarrotFantasy.AssetLoadHandle>
	// System.Action<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Action<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Action<CarrotFantasy.BattleMapGrid.GridState>
	// System.Action<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Action<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Action<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Action<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Action<CarrotFantasy.Fix64Vector2>
	// System.Action<HexCellRenderData>
	// System.Action<HexMapPointData>
	// System.Action<System.ValueTuple<int,int>>
	// System.Action<UINameEntry>
	// System.Action<UnityEngine.Color>
	// System.Action<UnityEngine.Vector3>
	// System.Action<byte>
	// System.Action<float>
	// System.Action<int,int>
	// System.Action<int,object>
	// System.Action<int>
	// System.Action<object,object>
	// System.Action<object>
	// System.Collections.Generic.ArraySortHelper<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.ArraySortHelper<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.ArraySortHelper<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.ArraySortHelper<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.ArraySortHelper<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.ArraySortHelper<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.ArraySortHelper<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.ArraySortHelper<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.ArraySortHelper<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.ArraySortHelper<HexCellRenderData>
	// System.Collections.Generic.ArraySortHelper<HexMapPointData>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,int>>
	// System.Collections.Generic.ArraySortHelper<UINameEntry>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Vector3>
	// System.Collections.Generic.ArraySortHelper<byte>
	// System.Collections.Generic.ArraySortHelper<float>
	// System.Collections.Generic.ArraySortHelper<int>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.Comparer<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.Comparer<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.Comparer<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.Comparer<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.Comparer<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.Comparer<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.Comparer<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.Comparer<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.Comparer<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.Comparer<HexCellRenderData>
	// System.Collections.Generic.Comparer<HexMapPointData>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.Comparer<UINameEntry>
	// System.Collections.Generic.Comparer<UnityEngine.Vector3>
	// System.Collections.Generic.Comparer<byte>
	// System.Collections.Generic.Comparer<float>
	// System.Collections.Generic.Comparer<int>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.Dictionary.Enumerator<System.ValueTuple<int,int>,HexMapPointData>
	// System.Collections.Generic.Dictionary.Enumerator<System.ValueTuple<int,int>,int>
	// System.Collections.Generic.Dictionary.Enumerator<int,HexMapPointData>
	// System.Collections.Generic.Dictionary.Enumerator<int,byte>
	// System.Collections.Generic.Dictionary.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>
	// System.Collections.Generic.Dictionary.Enumerator<object,CarrotFantasy.Fix64>
	// System.Collections.Generic.Dictionary.Enumerator<object,float>
	// System.Collections.Generic.Dictionary.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.Enumerator<ushort,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<System.ValueTuple<int,int>,HexMapPointData>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<System.ValueTuple<int,int>,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,HexMapPointData>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,byte>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,CarrotFantasy.Fix64>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,float>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<ushort,object>
	// System.Collections.Generic.Dictionary.KeyCollection<System.ValueTuple<int,int>,HexMapPointData>
	// System.Collections.Generic.Dictionary.KeyCollection<System.ValueTuple<int,int>,int>
	// System.Collections.Generic.Dictionary.KeyCollection<int,HexMapPointData>
	// System.Collections.Generic.Dictionary.KeyCollection<int,byte>
	// System.Collections.Generic.Dictionary.KeyCollection<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>
	// System.Collections.Generic.Dictionary.KeyCollection<object,CarrotFantasy.Fix64>
	// System.Collections.Generic.Dictionary.KeyCollection<object,float>
	// System.Collections.Generic.Dictionary.KeyCollection<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<ushort,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<System.ValueTuple<int,int>,HexMapPointData>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<System.ValueTuple<int,int>,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,HexMapPointData>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,byte>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,CarrotFantasy.Fix64>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,float>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<ushort,object>
	// System.Collections.Generic.Dictionary.ValueCollection<System.ValueTuple<int,int>,HexMapPointData>
	// System.Collections.Generic.Dictionary.ValueCollection<System.ValueTuple<int,int>,int>
	// System.Collections.Generic.Dictionary.ValueCollection<int,HexMapPointData>
	// System.Collections.Generic.Dictionary.ValueCollection<int,byte>
	// System.Collections.Generic.Dictionary.ValueCollection<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>
	// System.Collections.Generic.Dictionary.ValueCollection<object,CarrotFantasy.Fix64>
	// System.Collections.Generic.Dictionary.ValueCollection<object,float>
	// System.Collections.Generic.Dictionary.ValueCollection<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<ushort,object>
	// System.Collections.Generic.Dictionary<System.ValueTuple<int,int>,HexMapPointData>
	// System.Collections.Generic.Dictionary<System.ValueTuple<int,int>,int>
	// System.Collections.Generic.Dictionary<int,HexMapPointData>
	// System.Collections.Generic.Dictionary<int,byte>
	// System.Collections.Generic.Dictionary<int,int>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>
	// System.Collections.Generic.Dictionary<object,CarrotFantasy.Fix64>
	// System.Collections.Generic.Dictionary<object,float>
	// System.Collections.Generic.Dictionary<object,int>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.Dictionary<ushort,object>
	// System.Collections.Generic.EqualityComparer<CarrotFantasy.BattleViewSpritePreloader.CachedSprite>
	// System.Collections.Generic.EqualityComparer<CarrotFantasy.Fix64>
	// System.Collections.Generic.EqualityComparer<HexMapPointData>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.EqualityComparer<byte>
	// System.Collections.Generic.EqualityComparer<float>
	// System.Collections.Generic.EqualityComparer<int>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.EqualityComparer<ushort>
	// System.Collections.Generic.HashSet.Enumerator<int>
	// System.Collections.Generic.HashSet.Enumerator<object>
	// System.Collections.Generic.HashSet<int>
	// System.Collections.Generic.HashSet<object>
	// System.Collections.Generic.HashSetEqualityComparer<int>
	// System.Collections.Generic.HashSetEqualityComparer<object>
	// System.Collections.Generic.ICollection<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.ICollection<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.ICollection<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.ICollection<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.ICollection<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.ICollection<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.ICollection<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.ICollection<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.ICollection<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.ICollection<HexCellRenderData>
	// System.Collections.Generic.ICollection<HexMapPointData>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,HexMapPointData>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,HexMapPointData>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,byte>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,CarrotFantasy.Fix64>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<ushort,object>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,int>>
	// System.Collections.Generic.ICollection<UINameEntry>
	// System.Collections.Generic.ICollection<UnityEngine.Vector3>
	// System.Collections.Generic.ICollection<byte>
	// System.Collections.Generic.ICollection<float>
	// System.Collections.Generic.ICollection<int>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.IComparer<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.IComparer<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.IComparer<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.IComparer<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.IComparer<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.IComparer<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.IComparer<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.IComparer<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.IComparer<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.IComparer<HexCellRenderData>
	// System.Collections.Generic.IComparer<HexMapPointData>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.IComparer<UINameEntry>
	// System.Collections.Generic.IComparer<UnityEngine.Vector3>
	// System.Collections.Generic.IComparer<byte>
	// System.Collections.Generic.IComparer<float>
	// System.Collections.Generic.IComparer<int>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IDictionary<object,LitJson.ArrayMetadata>
	// System.Collections.Generic.IDictionary<object,LitJson.ObjectMetadata>
	// System.Collections.Generic.IDictionary<object,LitJson.PropertyMetadata>
	// System.Collections.Generic.IDictionary<object,object>
	// System.Collections.Generic.IEnumerable<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.IEnumerable<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.IEnumerable<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.IEnumerable<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.IEnumerable<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.IEnumerable<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.IEnumerable<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.IEnumerable<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.IEnumerable<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.IEnumerable<HexCellRenderData>
	// System.Collections.Generic.IEnumerable<HexMapPointData>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,HexMapPointData>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,HexMapPointData>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,byte>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,CarrotFantasy.Fix64>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<ushort,object>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>
	// System.Collections.Generic.IEnumerable<UINameEntry>
	// System.Collections.Generic.IEnumerable<UnityEngine.Vector3>
	// System.Collections.Generic.IEnumerable<byte>
	// System.Collections.Generic.IEnumerable<float>
	// System.Collections.Generic.IEnumerable<int>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerator<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.IEnumerator<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.IEnumerator<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.IEnumerator<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.IEnumerator<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.IEnumerator<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.IEnumerator<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.IEnumerator<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.IEnumerator<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.IEnumerator<HexCellRenderData>
	// System.Collections.Generic.IEnumerator<HexMapPointData>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,HexMapPointData>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,HexMapPointData>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,byte>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,CarrotFantasy.Fix64>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<ushort,object>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,int>>
	// System.Collections.Generic.IEnumerator<UINameEntry>
	// System.Collections.Generic.IEnumerator<UnityEngine.Vector3>
	// System.Collections.Generic.IEnumerator<byte>
	// System.Collections.Generic.IEnumerator<float>
	// System.Collections.Generic.IEnumerator<int>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEqualityComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.IEqualityComparer<int>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IEqualityComparer<ushort>
	// System.Collections.Generic.IList<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.IList<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.IList<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.IList<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.IList<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.IList<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.IList<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.IList<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.IList<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.IList<HexCellRenderData>
	// System.Collections.Generic.IList<HexMapPointData>
	// System.Collections.Generic.IList<System.ValueTuple<int,int>>
	// System.Collections.Generic.IList<UINameEntry>
	// System.Collections.Generic.IList<UnityEngine.Vector3>
	// System.Collections.Generic.IList<byte>
	// System.Collections.Generic.IList<float>
	// System.Collections.Generic.IList<int>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.IReadOnlyCollection<object>
	// System.Collections.Generic.IReadOnlyDictionary<int,byte>
	// System.Collections.Generic.IReadOnlyDictionary<int,int>
	// System.Collections.Generic.IReadOnlyDictionary<int,object>
	// System.Collections.Generic.IReadOnlyList<object>
	// System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,HexMapPointData>
	// System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,int>
	// System.Collections.Generic.KeyValuePair<int,HexMapPointData>
	// System.Collections.Generic.KeyValuePair<int,byte>
	// System.Collections.Generic.KeyValuePair<int,int>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<object,CarrotFantasy.BattleViewSpritePreloader.CachedSprite>
	// System.Collections.Generic.KeyValuePair<object,CarrotFantasy.Fix64>
	// System.Collections.Generic.KeyValuePair<object,float>
	// System.Collections.Generic.KeyValuePair<object,int>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.KeyValuePair<ushort,object>
	// System.Collections.Generic.List.Enumerator<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.List.Enumerator<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.List.Enumerator<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.List.Enumerator<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.List.Enumerator<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.List.Enumerator<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.List.Enumerator<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.List.Enumerator<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.List.Enumerator<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.List.Enumerator<HexCellRenderData>
	// System.Collections.Generic.List.Enumerator<HexMapPointData>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,int>>
	// System.Collections.Generic.List.Enumerator<UINameEntry>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Vector3>
	// System.Collections.Generic.List.Enumerator<byte>
	// System.Collections.Generic.List.Enumerator<float>
	// System.Collections.Generic.List.Enumerator<int>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.List<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.List<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.List<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.List<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.List<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.List<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.List<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.List<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.List<HexCellRenderData>
	// System.Collections.Generic.List<HexMapPointData>
	// System.Collections.Generic.List<System.ValueTuple<int,int>>
	// System.Collections.Generic.List<UINameEntry>
	// System.Collections.Generic.List<UnityEngine.Vector3>
	// System.Collections.Generic.List<byte>
	// System.Collections.Generic.List<float>
	// System.Collections.Generic.List<int>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.ObjectComparer<CarrotFantasy.AssetLoadHandle>
	// System.Collections.Generic.ObjectComparer<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.Generic.ObjectComparer<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.Generic.ObjectComparer<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.Generic.ObjectComparer<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.Generic.ObjectComparer<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.Generic.ObjectComparer<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.Generic.ObjectComparer<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.Generic.ObjectComparer<CarrotFantasy.Fix64Vector2>
	// System.Collections.Generic.ObjectComparer<HexCellRenderData>
	// System.Collections.Generic.ObjectComparer<HexMapPointData>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.ObjectComparer<UINameEntry>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Vector3>
	// System.Collections.Generic.ObjectComparer<byte>
	// System.Collections.Generic.ObjectComparer<float>
	// System.Collections.Generic.ObjectComparer<int>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<CarrotFantasy.BattleViewSpritePreloader.CachedSprite>
	// System.Collections.Generic.ObjectEqualityComparer<CarrotFantasy.Fix64>
	// System.Collections.Generic.ObjectEqualityComparer<HexMapPointData>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.ObjectEqualityComparer<byte>
	// System.Collections.Generic.ObjectEqualityComparer<float>
	// System.Collections.Generic.ObjectEqualityComparer<int>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<ushort>
	// System.Collections.Generic.Queue.Enumerator<int>
	// System.Collections.Generic.Queue.Enumerator<object>
	// System.Collections.Generic.Queue<int>
	// System.Collections.Generic.Queue<object>
	// System.Collections.Generic.Stack.Enumerator<object>
	// System.Collections.Generic.Stack<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<CarrotFantasy.AssetLoadHandle>
	// System.Collections.ObjectModel.ReadOnlyCollection<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Collections.ObjectModel.ReadOnlyCollection<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Collections.ObjectModel.ReadOnlyCollection<CarrotFantasy.BattleMapGrid.GridState>
	// System.Collections.ObjectModel.ReadOnlyCollection<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Collections.ObjectModel.ReadOnlyCollection<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Collections.ObjectModel.ReadOnlyCollection<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Collections.ObjectModel.ReadOnlyCollection<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Collections.ObjectModel.ReadOnlyCollection<CarrotFantasy.Fix64Vector2>
	// System.Collections.ObjectModel.ReadOnlyCollection<HexCellRenderData>
	// System.Collections.ObjectModel.ReadOnlyCollection<HexMapPointData>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<UINameEntry>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector3>
	// System.Collections.ObjectModel.ReadOnlyCollection<byte>
	// System.Collections.ObjectModel.ReadOnlyCollection<float>
	// System.Collections.ObjectModel.ReadOnlyCollection<int>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Comparison<CarrotFantasy.AssetLoadHandle>
	// System.Comparison<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Comparison<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Comparison<CarrotFantasy.BattleMapGrid.GridState>
	// System.Comparison<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Comparison<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Comparison<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Comparison<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Comparison<CarrotFantasy.Fix64Vector2>
	// System.Comparison<HexCellRenderData>
	// System.Comparison<HexMapPointData>
	// System.Comparison<System.ValueTuple<int,int>>
	// System.Comparison<UINameEntry>
	// System.Comparison<UnityEngine.Vector3>
	// System.Comparison<byte>
	// System.Comparison<float>
	// System.Comparison<int>
	// System.Comparison<object>
	// System.Func<UnityEngine.Color>
	// System.Func<UnityEngine.Vector3>
	// System.Func<byte>
	// System.Func<float>
	// System.Func<int,byte>
	// System.Func<int,float>
	// System.Func<object,byte>
	// System.Func<object,object>
	// System.Func<object>
	// System.IComparable<CarrotFantasy.Fix64>
	// System.IEquatable<CarrotFantasy.Fix64>
	// System.Linq.Enumerable.Iterator<object>
	// System.Linq.Enumerable.WhereArrayIterator<object>
	// System.Linq.Enumerable.WhereEnumerableIterator<object>
	// System.Linq.Enumerable.WhereListIterator<object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,object>
	// System.Linq.Enumerable.WhereSelectListIterator<object,object>
	// System.Nullable<HexMapPointData>
	// System.Predicate<CarrotFantasy.AssetLoadHandle>
	// System.Predicate<CarrotFantasy.BVBattleWorldUiComponent.DamageFloatEntry>
	// System.Predicate<CarrotFantasy.BattleMapGrid.GridIndex>
	// System.Predicate<CarrotFantasy.BattleMapGrid.GridState>
	// System.Predicate<CarrotFantasy.BattleViewPrefabPreloader.PrefabRequest>
	// System.Predicate<CarrotFantasy.BattleViewPrefabPreloader.TrackedHandle>
	// System.Predicate<CarrotFantasy.BattleViewSpritePreloader.SpriteRequest>
	// System.Predicate<CarrotFantasy.BattleViewSpritePreloader.TrackedHandle>
	// System.Predicate<CarrotFantasy.Fix64Vector2>
	// System.Predicate<HexCellRenderData>
	// System.Predicate<HexMapPointData>
	// System.Predicate<System.ValueTuple<int,int>>
	// System.Predicate<UINameEntry>
	// System.Predicate<UnityEngine.Vector3>
	// System.Predicate<byte>
	// System.Predicate<float>
	// System.Predicate<int>
	// System.Predicate<object>
	// System.ValueTuple<int,int>
	// }}

	public void RefMethods()
	{
		// int AssetBundleManager.LoadAsset<object>(string,string,AssetLoadCallback<object>,LoadPriority)
		// object LitJson.JsonMapper.ToObject<object>(string)
		// object System.Activator.CreateInstance<object>()
		// byte[] System.Array.Empty<byte>()
		// int[] System.Array.Empty<int>()
		// object[] System.Array.Empty<object>()
		// byte System.Collections.Generic.CollectionExtensions.GetValueOrDefault<int,byte>(System.Collections.Generic.IReadOnlyDictionary<int,byte>,int,byte)
		// int System.Collections.Generic.CollectionExtensions.GetValueOrDefault<int,int>(System.Collections.Generic.IReadOnlyDictionary<int,int>,int,int)
		// object System.Collections.Generic.CollectionExtensions.GetValueOrDefault<int,object>(System.Collections.Generic.IReadOnlyDictionary<int,object>,int,object)
		// bool System.Linq.Enumerable.All<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// bool System.Linq.Enumerable.Any<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>)
		// System.Collections.Generic.List<object> System.Linq.Enumerable.ToList<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Where<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<object>.Select<object>(System.Func<object,object>)
		// object& System.Runtime.CompilerServices.Unsafe.As<object,object>(object&)
		// System.Void* System.Runtime.CompilerServices.Unsafe.AsPointer<object>(object&)
		// object UnityEngine.AssetBundle.LoadAsset<object>(string)
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.Component.GetComponentInChildren<object>()
		// object[] UnityEngine.Component.GetComponentsInChildren<object>(bool)
		// bool UnityEngine.Component.TryGetComponent<object>(object&)
		// object UnityEngine.GameObject.AddComponent<object>()
		// object UnityEngine.GameObject.GetComponent<object>()
		// object[] UnityEngine.GameObject.GetComponentsInChildren<object>(bool)
		// bool UnityEngine.GameObject.TryGetComponent<object>(object&)
		// object UnityEngine.JsonUtility.FromJson<object>(string)
		// object UnityEngine.Object.FindObjectOfType<object>()
		// object UnityEngine.Object.Instantiate<object>(object)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform,bool)
		// object UnityEngine.Resources.GetBuiltinResource<object>(string)
		// object UnityEngine.Resources.Load<object>(string)
		// string string.Join<int>(string,System.Collections.Generic.IEnumerable<int>)
		// string string.JoinCore<int>(System.Char*,int,System.Collections.Generic.IEnumerable<int>)
	}
}