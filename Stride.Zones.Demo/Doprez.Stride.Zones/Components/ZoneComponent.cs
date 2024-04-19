using Stride.Core.Collections;
using Stride.Core;
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

	public FastTrackingCollection<TransformComponent> BoundaryNodes { get; set; } = new();

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
		BoundaryNodes.CollectionChanged += BoundaryNodesChanged;
	}

	private void BoundaryNodesChanged(object sender, ref FastTrackingCollectionChangedEventArgs e)
	{
		IsDirty = true;
	}
}
