using Core.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Controllers
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Rigidbody2D))]
	public partial class CharacterController2D : MonoBehaviour
	{
		/// <summary>
		/// When `true`, stops the next `FixedUpdate`.
		/// Automatically set to `false` every `FixedUpdate`.
		/// </summary>
		[NonSerialized]
		public bool skipFixedUpdate = false;
		/// <summary>
		/// Maximum speed this entity can build up.
		/// </summary>
		[NonSerialized]
		public SquareFloat desiredSpeed = 10f;
		[NonSerialized]
		public SquareFloat acceleration = 10f;
		[NonSerialized]
		public SquareFloat deceleration = 10f;
		[NonSerialized]
		public SquareFloat jumpStrength = 10f;
		/// <summary>
		/// Where this entity is looking towards.
		/// -1 = Left
		/// 1 = Right
		/// </summary>
		[NonSerialized]
		public int lookDirection = 1;
		/// <summary>
		/// When moving, lookDirection will match the moveDirection.
		/// </summary>
		[NonSerialized]
		public bool lookWhenMoving = true;
		/// <summary>
		/// Where this entity is moving towards.
		/// 0f = Not Moving
		/// -1f = Left
		/// 1f = Right
		/// </summary>
		private Vector2 moveDirection = Vector2.zero;

		[Tooltip("Collider for obstacle collisions.")]
		[field: SerializeField]
		public BoxCollider2D Collider { get; private set; }

		[Tooltip("Collider for projectile collisions.")]
		[field: SerializeField]
		public BoxCollider2D Hitbox { get; private set; }

		public Rigidbody2D Rigidbody { get; private set; }

		public Physics Physics0 { get; private set; }

		public Collision Collision0 { get; private set; }

		public Pathfinder Pathfinder0 { get; private set; }

		public Vector2 MoveDirection
		{
			get => moveDirection;
			set
			{
				if (moveDirection == value)
					return;

				if (value.sqrMagnitude == 0f)
				{
					moveDirection = Vector2.zero;
					return;
				}

				if (!Physics0.Flying && value.y != 0)
					// Prevent using y-axis when not flying.
					moveDirection = new(value.x > 0f ? 1f : -1f, 0f);
				else
					moveDirection = value.normalized;

				if (lookWhenMoving)
					if (moveDirection.x < 0)
						lookDirection = -1;
					else
						lookDirection = 1;
			}
		}

		/// <summary>
		/// Center of the hitbox.
		/// <br></br>
		/// Use this for the hitbox's center as the position
		/// is delayed whenever the position is updated.
		/// </summary>
		public Vector2 HitboxCenter
		{
			get =>
				(Vector2)transform.position - Position +
				(Vector2)Hitbox.bounds.center;
			set =>
				Position =
				(Vector2)transform.position -
				(Vector2)Hitbox.bounds.center +
				value;
		}

		/// <summary>
		/// Center of the collider.
		/// <br></br>
		/// Use this for the collider's position as the position
		/// is delayed whenever the position is updated.
		/// </summary>
		public Vector2 ColliderPosition
		{
			get =>
				(Vector2)transform.position - Position +
				(Vector2)Collider.bounds.center;

			set =>
				Position =
				(Vector2)transform.position -
				(Vector2)Collider.bounds.center +
				value;
		}

		/// <summary>
		/// The center position of the rigidbody.
		/// </summary>
		public Vector2 Position
		{
			get => Rigidbody.position;
			set
			{
				Rigidbody.position = value;
				// Just in case when teleporting inside an obstacle.
				Physics0.Unstuck();
			}
		}

		/// <summary>
		/// Position offset between frames.
		/// This will have a value if the rigibody's position changed
		/// in the same frame when this was referenced.
		/// </summary>
		public Vector2 PositionDelta => Rigidbody.position - (Vector2)transform.position;

		/// <summary>
		/// Where this entity is looking towards.
		/// <br></br>
		/// This is either left or right.
		/// </summary>
		public Vector2 LookVector => new(lookDirection, 0f);

		private void Awake()
		{
			Physics0 = new(this);
			Collision0 = new(this);
			Pathfinder0 = new(this);

			Pathfinder0.Awake();
			Collision0.Awake();

			Rigidbody = GetComponent<Rigidbody2D>();
			Rigidbody.isKinematic = true;
			Rigidbody.useFullKinematicContacts = true;
		}

		private void FixedUpdate()
		{
			if (skipFixedUpdate)
			{
				skipFixedUpdate = false;
				return;
			}

			Pathfinder0.FixedUpdate();
			Collision0.FixedUpdate();
			Physics0.FixedUpdate();
		}

		/// <summary>
		/// Stop all movement and rotation.
		/// </summary>
		public void Stop()
		{
			Pathfinder0.Stop();

			MoveDirection = Vector2.zero;
			Physics0.jumping = false;
			Collision0.IgnorePlatforms = false;
		}

		public void LookAt(Vector2 position) =>
			lookDirection = position.x < Position.x ? -1 : 1;
	}
}
