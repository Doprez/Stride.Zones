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

			// I'll change this later to be more efficient.
			if (CheckCollisionTopDown(key.BoundaryNodes.Select(x => x.Position).ToArray(), Position))
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

		var position = _meshBuilder.WithPosition<Vector3>();

		var organizedNodes = GetOrganizedNodes(component);
		var nodes = organizedNodes.ToArray();
		var boundingBox = BoundingBox.FromPoints(nodes);
		var boundingSphere = BoundingSphere.FromPoints(nodes);

		for (int i = 0; i < nodes.Length; i++)
		{
			_meshBuilder.AddVertex();
			_meshBuilder.SetElement(position, nodes[i]);
		}

		// Add the first node again to close the mesh
		_meshBuilder.AddVertex();
		_meshBuilder.SetElement(position, nodes[0]);

		// I hate this... but it works
		for (int i = 0; i < nodes.Length; i++)
		{
			// This might be simpler if I were to organize the nodes differently
			// but I don't want to do that right now.
			if (i % 2 == 0)
			{
				_meshBuilder.AddIndex(i);
				if (i + 2 <= nodes.Length)
				{
					_meshBuilder.AddIndex(i + 2);
				}
				else
				{
					_meshBuilder.AddIndex(1);
				}
				_meshBuilder.AddIndex(i + 1);
			}
			else
			{
				_meshBuilder.AddIndex(i);
				_meshBuilder.AddIndex(i + 1);
				if (i + 2 <= nodes.Length)
				{
					_meshBuilder.AddIndex(i + 2);
				}
				else
				{
					_meshBuilder.AddIndex(1);
				}
			}
		}

		//top nodes are the odd indices ie 0 bottom, 1 top, 2 bottom, 3 top...
		// create top indices for the top of the mesh
		for (int i = 0; i < nodes.Length; i += 2)
		{
			//always add the first top node to close the mesh
			_meshBuilder.AddIndex(1);
			_meshBuilder.AddIndex(i + 1);
			if (i + 3 <= nodes.Length)
			{
				_meshBuilder.AddIndex(i + 3);
			}
			else
			{
				_meshBuilder.AddIndex(1);
			}
		}

		var mesh = _meshBuilder.ToMeshDraw(_graphicsDevice);

		_meshBuilder?.Dispose();

		return new Mesh
		{
			Draw = mesh,
			BoundingBox = boundingBox,
			BoundingSphere = boundingSphere,
		};
	}

	private List<Vector3> GetOrganizedNodes(ZoneComponent component)
	{
		var organizedNodes = new List<Vector3>();

		for (int i = 0; i < component.BoundaryNodes.Count; i++)
		{
			organizedNodes.Add(component.BoundaryNodes[i].Position);
			var node = component.BoundaryNodes[i].Position;
			node.Y += component.BoundaryHeight;
			organizedNodes.Add(node);
		}

		return organizedNodes;
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
