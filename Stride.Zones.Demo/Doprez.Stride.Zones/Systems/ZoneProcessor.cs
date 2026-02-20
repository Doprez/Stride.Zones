using Doprez.Stride.Zones.Components;
using Stride.CommunityToolkit.Rendering.Utilities;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;

namespace Doprez.Stride.Zones.Systems;
public class ZoneProcessor : EntityProcessor<ZoneComponent, ZoneRenderData>, IEntityComponentRenderProcessor
{
	public VisibilityGroup VisibilityGroup { get; set; }

	private MeshBuilder _meshBuilder;
	private GraphicsDevice _graphicsDevice;

	protected override void OnSystemAdd()
	{
		_graphicsDevice = Services.GetService<IGraphicsDeviceService>().GraphicsDevice;
	}

	protected override void OnEntityComponentRemoved(Entity entity, [NotNull] ZoneComponent component, [NotNull] ZoneRenderData data)
	{
		DestroyMesh(data);
	}

	/// <summary>
	/// Returns the territory component that the position is inside of. Returns null if no territory is found.
	/// </summary>
	/// <param name="Position"></param>
	/// <returns></returns>
	public ZoneComponent GetTerritoryComponentCollision(Vector3 Position)
	{
		foreach (var component in ComponentDatas)
		{
			var key = component.Key;
			var points = key.BoundaryNodes.Select(x => x.Position).ToArray();

			// Check Y bounds
			float minY = points.Min(p => p.Y);
			float maxY = points.Max(p => p.Y) + key.BoundaryHeight;
			if (Position.Y < minY || Position.Y > maxY)
				continue;

			// Ray casting algorithm handles concave shapes correctly
			if (CheckCollisionTopDown(points, Position))
			{
				return key;
			}
		}
		return null;
	}

	/// <summary>
	/// This method only checks the X Z positions of the points. Returns true if the position is inside the polygon.
	/// </summary>
	/// <param name="Points"></param>
	/// <param name="Position"></param>
	/// <returns></returns>
	public static bool CheckCollisionTopDown(Vector3[] Points, Vector3 Position)
	{
		double MinX = Points.Min(a => a.X);
		double MinZ = Points.Min(a => a.Z);
		double MaxX = Points.Max(a => a.X);
		double MaxZ = Points.Max(a => a.Z);

		if (Position.X < MinX || Position.X > MaxX || Position.Z < MinZ || Position.Z > MaxZ)
			return false;

		int i = 0;
		int j = Points.Length - 1;
		bool IsMatch = false;

		for (; i < Points.Length; j = i++)
		{
			//When the position is right on a point, count it as a match.
			if (Points[i].X == Position.X && Points[i].Z == Position.Z)
				return true;
			if (Points[j].X == Position.X && Points[j].Z == Position.Z)
				return true;

			//When the position is on a horizontal or vertical line, count it as a match.
			if (Points[i].X == Points[j].X && Position.X == Points[i].X && Position.Z >= Math.Min(Points[i].Z, Points[j].Z) && Position.Z <= Math.Max(Points[i].Z, Points[j].Z))
				return true;
			if (Points[i].Z == Points[j].Z && Position.Z == Points[i].Z && Position.X >= Math.Min(Points[i].X, Points[j].X) && Position.X <= Math.Max(Points[i].X, Points[j].X))
				return true;

			if (((Points[i].Z > Position.Z) != (Points[j].Z > Position.Z)) && (Position.X < (Points[j].X - Points[i].X) * (Position.Z - Points[i].Z) / (Points[j].Z - Points[i].Z) + Points[i].X))
			{
				IsMatch = !IsMatch;
			}
		}

		return IsMatch;
	}

	public override void Draw(RenderContext context)
	{
		foreach (var component in ComponentDatas)
		{
			var key = component.Key;
			var data = component.Value;

			// At least 3 nodes are needed to make a convex mesh
			if (key.BoundaryNodes.Count < 3 || key.BoundaryNodes.Contains(null))
			{
				continue;
			}

			if (key.IsDirty || data.IsDirty(key))
			{
				DestroyMesh(data);

				data.Mesh = CreateMesh(key);
				if (key.Material != null)
				{
					if (!data.ModelComponent.Materials.ContainsKey(0))
					{
						data.ModelComponent.Materials.Add(0, key.Material);
					}
					else
					{
						data.ModelComponent.Materials[0] = key.Material;
					}
				}
				data.ModelComponent.Model.Meshes[0] = data.Mesh;
				key.Entity.Add(data.ModelComponent);

				key.IsDirty = false;
			}
		}
	}

	private void DestroyMesh(ZoneRenderData data)
	{
		data.ModelComponent?.Entity?.Remove(data.ModelComponent);

		if (data.Mesh != null)
		{
			var meshDraw = data.Mesh.Draw;
			meshDraw.IndexBuffer?.Buffer.Dispose();
			meshDraw.VertexBuffers[0].Buffer.Dispose();
		}
	}

	private Mesh CreateMesh(ZoneComponent component)
	{
		_meshBuilder = new();
		_meshBuilder.WithPrimitiveType(PrimitiveType.TriangleList);
		_meshBuilder.WithIndexType(IndexingType.Int16);

		var posAttr = _meshBuilder.WithPosition<Vector3>();
		var normAttr = _meshBuilder.WithNormal<Vector3>();

		int nodeCount = component.BoundaryNodes.Count;
		var bottomPos = new Vector3[nodeCount];
		var topPos = new Vector3[nodeCount];

		for (int i = 0; i < nodeCount; i++)
		{
			bottomPos[i] = component.BoundaryNodes[i].Position;
			topPos[i] = bottomPos[i];
			topPos[i].Y += component.BoundaryHeight;
		}

		// Ensure consistent winding for outward side normals and upward top cap normals
		if (SignedAreaXZ(bottomPos) < 0)
		{
			Array.Reverse(bottomPos);
			Array.Reverse(topPos);
		}

		var allPos = new Vector3[nodeCount * 2];
		Array.Copy(bottomPos, 0, allPos, 0, nodeCount);
		Array.Copy(topPos, 0, allPos, nodeCount, nodeCount);
		var boundingBox = BoundingBox.FromPoints(allPos);
		var boundingSphere = BoundingSphere.FromPoints(allPos);

		int vertIdx = 0;

		// Side walls - separate vertices per quad for flat shading normals
		for (int i = 0; i < nodeCount; i++)
		{
			int next = (i + 1) % nodeCount;

			var b0 = bottomPos[i];
			var b1 = bottomPos[next];
			var t0 = topPos[i];
			var t1 = topPos[next];

			var faceNormal = Vector3.Cross(t0 - b0, b1 - b0);
			if (faceNormal.LengthSquared() > 0)
				faceNormal.Normalize();

			int v0 = vertIdx++;
			_meshBuilder.AddVertex();
			_meshBuilder.SetElement(posAttr, b0);
			_meshBuilder.SetElement(normAttr, faceNormal);

			int v1 = vertIdx++;
			_meshBuilder.AddVertex();
			_meshBuilder.SetElement(posAttr, b1);
			_meshBuilder.SetElement(normAttr, faceNormal);

			int v2 = vertIdx++;
			_meshBuilder.AddVertex();
			_meshBuilder.SetElement(posAttr, t0);
			_meshBuilder.SetElement(normAttr, faceNormal);

			int v3 = vertIdx++;
			_meshBuilder.AddVertex();
			_meshBuilder.SetElement(posAttr, t1);
			_meshBuilder.SetElement(normAttr, faceNormal);

			// Triangle 1: b0, b1, t0
			_meshBuilder.AddIndex(v0);
			_meshBuilder.AddIndex(v1);
			_meshBuilder.AddIndex(v2);

			// Triangle 2: b1, t1, t0
			_meshBuilder.AddIndex(v1);
			_meshBuilder.AddIndex(v3);
			_meshBuilder.AddIndex(v2);
		}

		// Top cap - ear clipping triangulation for correct concave rendering
		var topNormal = Vector3.UnitY;
		int topStart = vertIdx;

		for (int i = 0; i < nodeCount; i++)
		{
			_meshBuilder.AddVertex();
			_meshBuilder.SetElement(posAttr, topPos[i]);
			_meshBuilder.SetElement(normAttr, topNormal);
			vertIdx++;
		}

		var topIndices = EarClipTriangulate(topPos);
		for (int i = 0; i < topIndices.Count; i += 3)
		{
			_meshBuilder.AddIndex(topStart + topIndices[i]);
			_meshBuilder.AddIndex(topStart + topIndices[i + 1]);
			_meshBuilder.AddIndex(topStart + topIndices[i + 2]);
		}

		var meshDraw = _meshBuilder.ToMeshDraw(_graphicsDevice);
		_meshBuilder?.Dispose();

		return new Mesh
		{
			Draw = meshDraw,
			BoundingBox = boundingBox,
			BoundingSphere = boundingSphere,
		};
	}

	/// <summary>
	/// Computes the signed area of a polygon projected onto the XZ plane.
	/// Positive means the vertices need to be reversed for outward-facing normals.
	/// </summary>
	private static float SignedAreaXZ(Vector3[] polygon)
	{
		float area = 0;
		for (int i = 0; i < polygon.Length; i++)
		{
			int next = (i + 1) % polygon.Length;
			area += polygon[i].X * polygon[next].Z - polygon[next].X * polygon[i].Z;
		}
		return area;
	}

	/// <summary>
	/// Triangulates a concave or convex polygon using the ear clipping algorithm projected onto the XZ plane.
	/// </summary>
	private static List<int> EarClipTriangulate(Vector3[] polygon)
	{
		var result = new List<int>();
		int n = polygon.Length;
		if (n < 3) return result;

		var remaining = new List<int>(n);
		for (int i = 0; i < n; i++)
			remaining.Add(i);

		int maxAttempts = n * n;
		int current = 0;

		while (remaining.Count > 2 && maxAttempts-- > 0)
		{
			int count = remaining.Count;
			int prev = (current - 1 + count) % count;
			int next = (current + 1) % count;

			var a = polygon[remaining[prev]];
			var b = polygon[remaining[current]];
			var c = polygon[remaining[next]];

			if (IsConvexXZ(a, b, c) && IsEar(polygon, remaining, prev, current, next))
			{
				result.Add(remaining[prev]);
				result.Add(remaining[current]);
				result.Add(remaining[next]);
				remaining.RemoveAt(current);
				if (current >= remaining.Count)
					current = 0;
			}
			else
			{
				current = (current + 1) % remaining.Count;
			}
		}

		return result;
	}

	private static bool IsConvexXZ(Vector3 a, Vector3 b, Vector3 c)
	{
		float cross = (b.Z - a.Z) * (c.X - b.X) - (b.X - a.X) * (c.Z - b.Z);
		return cross < 0;
	}

	private static bool IsEar(Vector3[] polygon, List<int> remaining, int prevIdx, int currIdx, int nextIdx)
	{
		var a = polygon[remaining[prevIdx]];
		var b = polygon[remaining[currIdx]];
		var c = polygon[remaining[nextIdx]];

		for (int i = 0; i < remaining.Count; i++)
		{
			if (i == prevIdx || i == currIdx || i == nextIdx) continue;

			if (IsPointInTriangleXZ(polygon[remaining[i]], a, b, c))
				return false;
		}

		return true;
	}

	private static bool IsPointInTriangleXZ(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
	{
		float d1 = CrossXZ(a, b, p);
		float d2 = CrossXZ(b, c, p);
		float d3 = CrossXZ(c, a, p);

		bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
		bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

		return !(hasNeg && hasPos);
	}

	private static float CrossXZ(Vector3 a, Vector3 b, Vector3 p)
	{
		return (b.X - a.X) * (p.Z - a.Z) - (b.Z - a.Z) * (p.X - a.X);
	}

	protected override ZoneRenderData GenerateComponentData([NotNull] Entity entity, [NotNull] ZoneComponent component)
	{
		return new ZoneRenderData();
	}
}

public class ZoneRenderData
{
	public Mesh Mesh { get; set; }
	public ModelComponent ModelComponent { get; set; } = new ModelComponent();

	public Dictionary<TransformComponent, Vector3> BoundingNodeLookup { get; set; } = new();

	public ZoneRenderData()
	{
		ModelComponent.Model = new Model
		{
			new Mesh(),
		};
		ModelComponent.IsShadowCaster = false;
	}

	public bool IsDirty(ZoneComponent component)
	{
		foreach (var node in component.BoundaryNodes)
		{
			if (BoundingNodeLookup.TryGetValue(node, out Vector3 value))
			{
				if (value != node.Position)
				{
					BoundingNodeLookup[node] = node.Position;
					return true;
				}
			}
			else
			{
				BoundingNodeLookup.Add(node, node.Position);
				return true;
			}
		}

		return false;
	}
}
