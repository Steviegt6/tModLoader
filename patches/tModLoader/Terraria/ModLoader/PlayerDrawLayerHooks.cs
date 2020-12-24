﻿using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using static Terraria.ModLoader.PlayerDrawLayer;

namespace Terraria.ModLoader
{
	public static class PlayerDrawLayerHooks
	{
		private static List<PlayerDrawLayer> _layers = new List<PlayerDrawLayer>(PlayerDrawLayers.VanillaLayers);
		public static IReadOnlyList<PlayerDrawLayer> Layers => _layers;

		private static PlayerDrawLayer[] _drawOrder;
		public static IReadOnlyList<PlayerDrawLayer> DrawOrder => _drawOrder;

		internal static void Add(PlayerDrawLayer layer) => _layers.Add(layer);

		internal static void Unload() {
			_layers = new List<PlayerDrawLayer>(PlayerDrawLayers.VanillaLayers);
			foreach (var layer in _layers) {
				layer.ClearChildren();
			}
		}

		internal static void ResizeArrays() {
			var positions = Layers.ToDictionary(l => l, l => l.GetDefaultPosition());

			PlayerHooks.ModifyDrawLayerOrdering(positions);

			// net core please!!
			//foreach (var (layer, pos) in positions.ToArray()) {
			foreach (var kv in positions.ToArray()) {
				var layer = kv.Key;
				switch (kv.Value) {
					case Between _:
						continue;
					case Before b:
						b.Layer.AddChildBefore(layer);
						break;
					case After a:
						a.Layer.AddChildAfter(layer);
						break;
					case Multiple m:
						int slot = 0;
						foreach (var (pos, cond) in m.Positions)
							positions.Add(new MobilePlayerDrawLayerSlot(layer, cond, slot++), pos);
						break;
					default:
						throw new ArgumentException($"PlayerDrawLayer {layer} has unknown Position type {kv.Value}");
				}

				positions.Remove(kv.Key);
			}

			var sort = new TopoSort<PlayerDrawLayer>(positions.Keys,
				l => new[] { ((Between)positions[l]).Layer1 }.Where(l => l != null),
				l => new[] { ((Between)positions[l]).Layer2 }.Where(l => l != null));

			_drawOrder = sort.Sort().ToArray();
		}

		/// <summary>
		/// Note, not threadsafe
		/// </summary>
		public static PlayerDrawLayer[] GetDrawLayers(PlayerDrawSet drawInfo) {
			foreach (var layer in _drawOrder) {
				layer.ResetVisiblity(drawInfo);
			}

			PlayerHooks.HideDrawLayers(drawInfo);

			return _drawOrder;
		}
	}

	[Autoload(false)]
	public class MobilePlayerDrawLayerSlot : PlayerDrawLayer
	{
		public PlayerDrawLayer Layer { get; }
		public Multiple.Condition Condition { get; }

		private readonly int _slot;

		public override string Name => $"{Layer.Name}_slot{_slot}";

		public MobilePlayerDrawLayerSlot(PlayerDrawLayer layer, Multiple.Condition cond, int slot) {
			Layer = layer;
			Condition = cond;
			_slot = slot;
		}

		public override Position GetDefaultPosition() => throw new NotImplementedException();

		protected override void Draw(ref PlayerDrawSet drawInfo) => Layer.DrawWithTransformationAndChildren(ref drawInfo);

		public override bool GetDefaultVisiblity(PlayerDrawSet drawInfo) => Condition(drawInfo);
	}
}
