using Core.Helpers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Controllers
{
	// Handles collision.
	public partial class CharacterController2D
	{
		public class Collision
		{
			private readonly CharacterController2D controller;

			[NonSerialized]
			public bool ignorePlatforms = false;
			[NonSerialized]
			public readonly HashSet<Collider2D> ignoredColliders = new();

			/// <summary>
			/// Filter for collision-checking.
			/// </summary>
			public ContactFilter2D Filter { get; private set; }

			/// <summary>
			/// If `true`,
			/// this entity will ignore all colliders in the 'Platforms' layer.
			/// </summary>
			public bool IgnorePlatforms
			{
				get => IgnorePlatforms;
				set
				{
					if (ignorePlatforms == value)
						return;

					if (!value && controller.Physics0.Flying)
						// Always ignore platforms when flying.
						return;

					if (ignorePlatforms = value)
						foreach (Collider2D collider in Resource.Self.Platforms)
							IgnoreCollider(collider, true);
				}
			}

			/// <summary>
			/// Get bounding box that encapsulates the rigidbody's
			/// colliders.
			/// </summary>
			public Bounds ColliderBounds
			{
				get
				{
					Bounds bounds = new Bounds(controller.transform.position, Vector3.zero);
					Collider2D[] colliders = new Collider2D[controller.Rigidbody.attachedColliderCount];
					controller.Rigidbody.GetAttachedColliders(colliders);

					foreach (Collider2D collider in colliders)
						if (collider != null)
							bounds.Encapsulate(collider.bounds);

					return bounds;
				}
			}

			public Collision(CharacterController2D controller)
			{
				this.controller = controller;
			}

			public void Awake()
			{
				ContactFilter2D filter = Filter = new ContactFilter2D();
				filter.SetLayerMask(Physics2D.GetLayerCollisionMask(
					controller.Collider.gameObject.layer
				));
				filter.useTriggers = false;
				filter.useLayerMask = true;
			}

			public void FixedUpdate()
			{
				if (ignorePlatforms)
					return;

				List<Collider2D> dump = new List<Collider2D>();
				List<Collider2D> colliders = new List<Collider2D>();
				controller.Rigidbody.OverlapCollider(default, colliders);

				foreach (Collider2D collider in ignoredColliders)
					if (!colliders.Contains(collider))
					{
						Physics2D.IgnoreCollision(controller.Collider, collider, false);
						dump.Add(collider);
					}

				foreach (Collider2D collider in dump)
					ignoredColliders.Remove(collider);
			}

			/// <summary>
			/// Ignore a collider.
			/// Automatically reverted when no longer overlapping.
			/// </summary>
			public void IgnoreCollider(Collider2D other, bool ignore = true)
			{
				if (!other)
					return;

				Physics2D.IgnoreCollision(controller.Collider, other, ignore);

				if (ignore)
					ignoredColliders.Add(other);
				else
					ignoredColliders.Remove(other);
			}

			/// <summary>
			/// If this object's collider should be ignored,
			/// such as entering below a platform.
			/// </summary>
			public bool ShouldIgnoreCollider(GameObject @object, Vector2 normal)
			{
				return @object.layer == PhysicsHelper.PLATFORMS_LAYER &&
					(ignorePlatforms || normal.y <= 0f);
			}
		}
	}
}
