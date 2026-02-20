using Stride.Core.Collections;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine.Design;
using Stride.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stride.Rendering;
using Doprez.Stride.Zones.Systems;

namespace Doprez.Stride.Zones.Components;
[DataContract(nameof(ZoneComponent))]
[ComponentCategory("Utils")]
[DefaultEntityComponentProcessor(typeof(ZoneProcessor))]
public class ZoneComponent : EntityComponent
{
	[DataMemberIgnore]
	public bool IsDirty { get; set; } = true;

	public bool ShowDebugMesh
	{
		get => _showDebugMesh;
		set
		{
			_showDebugMesh = value;
			IsDirty = true;
		}
	}
	private bool _showDebugMesh = true;

	public Material Material
	{
		get
		{
			return _material;
		}
		set
		{
			_material = value;
			IsDirty = true;
		}
	}
	private Material _material;

	public FastTrackingCollection<TransformComponent> BoundaryNodes
	{
		get => _boundaryNodes;
		set
		{
			if (_boundaryNodes != null)
			{
				_boundaryNodes.CollectionChanged -= BoundaryNodesChanged;
			}
			_boundaryNodes = value;
			if (_boundaryNodes != null)
			{
				_boundaryNodes.CollectionChanged += BoundaryNodesChanged;
			}
			IsDirty = true;
		}
	}
	private FastTrackingCollection<TransformComponent> _boundaryNodes = new();

	public float BoundaryHeight
	{
		get
		{
			return _boundaryHeight;
		}
		set
		{
			_boundaryHeight = value;
			IsDirty = true;
		}
	}
	private float _boundaryHeight = 5f;

	public ZoneComponent()
	{
		_boundaryNodes.CollectionChanged += BoundaryNodesChanged;
	}

	private void BoundaryNodesChanged(object sender, ref FastTrackingCollectionChangedEventArgs e)
	{
		IsDirty = true;
	}
}
