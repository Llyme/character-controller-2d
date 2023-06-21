using Core.Helpers;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Controllers
{
	public partial class CharacterController2D
	{
		public class Physics
		{
			private readonly CharacterController2D controller;

			/// <summary>
			/// Minimum distance this character can move.
			/// </summary>
			public SquareFloat minMoveDistance = 0.01f;
			/// <summary>
			/// Angle in degrees where this entity can rest on.
			/// <br></br>
			/// The lower the degree, the steeper the slope this object can rest.
			/// <br></br>
			/// Value must be between 0.0 and 1.0,
			/// where 1.0 is 180 degrees,
			/// which the entity can rest on any angle.
			/// </summary>
			public float slopeLimit = 0.65f;
			/// <summary>
			/// Extra width for detecting collisions.
			/// </summary>
			public SquareFloat skinWidth = 0.01f;
			/// <summary>
			/// If this entity should jump on the next physics update.
			/// Only works while grounded.
			/// Automatically set to `false`,
			/// even if not grounded.
			/// </summary>
			public bool jumping = false;
			public readonly RaycastHit2D[] buffer = new RaycastHit2D[16];
			private Vector2 prevRigidbodyVelocity;
			/// <summary>
			/// Dedicates 1 frame to unstuck itself.
			/// Unstucks itself when value is more than 0.
			/// Automatically sets to `-1`.
			/// </summary>
			private int unstuck = -1;
			private bool flying = false;
			/// <summary>
			/// Accumulated jump strength before taking off.
			/// <br></br>
			/// This is used to control the velocity
			/// when not holding the jump key.
			/// </summary>
			private float jumpPower = 0f;


			// Ground

			public Collider2D Ground { get; private set; }

			public float GroundedSpeed { get; private set; }

			public Vector2 GroundNormal { get; private set; }

			/// <summary>
			/// Movement along ground normal.
			/// </summary>
			public Vector2 GroundDirection { get; private set; }

			/// <summary>
			/// Current velocity when on the ground.
			/// <br></br>
			/// This is along the ground normal.
			/// <br></br>
			/// The Rigidbody's actual velocity is used when airborne.
			/// </summary>
			public Vector2 GroundedVelocity { get; private set; }
			
			/// <summary>
			/// If this is standing on a ground.
			/// </summary>
			public bool Grounded { get; private set; }

			/// <summary>
			/// If the ground just changed this frame.
			/// </summary>
			public bool GroundChanged { get; private set; }

			/// <summary>
			/// If this entity just jumped in
			/// the previous update.
			/// </summary>
			public bool Jumped { get; private set; } = false;

			/// <summary>
			/// When `true`, this entity will no longer
			/// be affected by natural gravity.
			/// </summary>
			public bool Flying
			{
				get => flying;
				set
				{
					if (flying == value)
						return;

					flying = value;
					controller.Rigidbody.gravityScale = value ? 0f : 1f;
					// Ignore all platforms when flying.
					controller.Collision0.IgnorePlatforms = value;

					if (value)
					{
						// Let default physics do the calculations when flying.
						controller.Rigidbody.isKinematic = false;
						// Prevent this from being grounded.
						Grounded = false;
					}
				}
			}

			/// <summary>
			/// Uses `GroundedVelocity` when grounded,
			/// otherwise uses the rigidbody's velocity.
			/// </summary>
			public Vector2 Velocity
			{
				get =>
					Grounded
					? GroundedVelocity
					: controller.Rigidbody.velocity;
				set
				{
					if (Grounded)
						GroundedVelocity = value;
					else
						controller.Rigidbody.velocity = value;
				}
			}

			public Physics(CharacterController2D controller)
			{
				this.controller = controller;
			}

			public void FixedUpdate()
			{
				if (unstuck > 0)
				{
					if (!Grounded)
					{
						// This should only be used when grounded.
						unstuck = -1;
						return;
					}

					unstuck = 0;
					controller.Rigidbody.isKinematic = false;
				}
				else if (unstuck == 0)
				{
					unstuck = -1;
					controller.Rigidbody.isKinematic = Grounded;
				}

				if (!flying)
				{
					List<ContactPoint2D> contacts =
						FixedUpdate_Contacts();

					FixedUpdate_Kinematic();
					FixedUpdate_Acceleration(contacts);

					FixedUpdate_Grounded();
					FixedUpdate_JumpPower();

					if (Jumped)
						Jumped = false;
						
					if (jumping)
					{
						if (!flying &&
							controller.jumpStrength > 0f)
							// Only jump when not flying and
							// can actually jump.
							Jumped = true;

						jumping = false; // Reset every physics update.
					}
				}
				else
					FixedUpdate_Acceleration_Flying();
			}

			/// <summary>
			/// Check for contact points.
			/// </summary>
			public List<ContactPoint2D> FixedUpdate_Contacts()
			{
				List<ContactPoint2D> contacts = new();
				controller.Rigidbody.GetContacts(contacts);

				// Reset ground state.
				Collider2D prevGround = Ground;
				Grounded = false;

				foreach (ContactPoint2D contact in contacts)
				{
					if (controller.Collision0.ignoredColliders.Contains(contact.collider))
						// Collider is ignored. Skip.
						continue;

					Vector2 normal = contact.normal;

					if (controller.Collision0.ShouldIgnoreCollider(contact.collider.gameObject, normal))
					{
						// Ignore this collider.
						controller.Collision0.IgnoreCollider(contact.collider, true);
						continue;
					}

					if (normal.y <= slopeLimit)
						// Too steep for the entity to rest on.
						continue;

					// Collider is flat enough.
					// Include this collider as a ground.
					Ground = contact.collider;
					GroundNormal = normal;
					GroundDirection = new(normal.y, -normal.x);
					Grounded = true;
					break;
				}

				// Check if ground changed.
				GroundChanged = prevGround != Ground;

				return contacts;
			}

			/// <summary>
			/// Swap grounded velocity and airborne velocity when
			/// switching between kinematic and dynamic.
			/// </summary>
			private void FixedUpdate_Kinematic()
			{
				prevRigidbodyVelocity =
					controller.Rigidbody.velocity;

				if (Grounded &&
					!controller.Rigidbody.isKinematic)
				{
					GroundedSpeed = GroundDirection.x * prevRigidbodyVelocity.x;
					jumpPower = 0f;
					controller.Rigidbody.velocity = Vector2.zero;
				}
				else if (!Grounded && controller.Rigidbody.isKinematic)
				{
					controller.Rigidbody.velocity = new(
						GroundedVelocity.x,
						GroundedVelocity.y + jumpPower
					);
					GroundedVelocity = Vector2.zero;
					GroundedSpeed = 0f;
				}

				controller.Rigidbody.isKinematic = Grounded;
			}

			/// <summary>
			/// Acceleration when flying.
			/// </summary>
			private void FixedUpdate_Acceleration_Flying()
			{
				Vector2 velocity = controller.Rigidbody.velocity;

				if (controller.MoveDirection == Vector2.zero)
					// Not moving. Just decelerate when possible.
					velocity = Vector2.MoveTowards(
						velocity,
						Vector2.zero,
						controller.deceleration * Time.deltaTime
					);
				else
					// Moving.
					velocity = Vector2.MoveTowards(
						velocity,
						controller.MoveDirection * controller.desiredSpeed,
						controller.acceleration * Time.deltaTime
					);


				// Set new velocity.

				controller.Rigidbody.velocity = velocity;
			}

			/// <summary>
			/// Controls jump strength.
			/// </summary>
			private void FixedUpdate_JumpPower()
			{
				if (Grounded)
					return;

				if (jumpPower <= 0f)
					return;

				if (jumping)
					return;

				Vector2 velocity =
					controller.Rigidbody.velocity;

				if (velocity.y <= 0f)
				{
					jumpPower = 0f;
					return;
				}

				float delta = Time.deltaTime * 5f;
				float deceleration =
					Mathf.Max(
						delta,
						jumpPower * delta
					);
				float y = velocity.y - deceleration;
				jumpPower -= deceleration;

				if (y < 0f)
					y = 0f;

				controller.Rigidbody.velocity =
					new(velocity.x, y);
			}

			/// <summary>
			/// X-axis movement changes.
			/// </summary>
			private void FixedUpdate_Acceleration
				(List<ContactPoint2D> contacts)
			{
				float speed = GroundedSpeed;
				float maxSpeed = controller.desiredSpeed;
				float delta = controller.acceleration;

				if (!Grounded)
				{
					// Moving while airborne.

					if (controller.MoveDirection.x == 0f)
						// Let the default physics do the work.
						return;

					speed = controller.Rigidbody.velocity.x;

					foreach (ContactPoint2D contact in contacts)
						if (controller.MoveDirection.x * contact.normal.x < 0f)
						{
							// Moving against a wall or a steep slope.
							delta *= contact.normal.y;

							if (delta == 0f)
								return;
						}

					if (delta < 0f)
						// Hit a ceiling.
						delta *= -1f;
				}

				if (controller.MoveDirection.x > 0f)
				{
					// Right.
					if (speed < maxSpeed)
						speed = Mathf.Min(
							speed +
							delta *
							Time.deltaTime *
							controller.MoveDirection.x,
							maxSpeed
						);
					else if (Grounded && speed > maxSpeed)
						speed = Mathf.Max(
							speed -
							controller.deceleration *
							Time.deltaTime,
							maxSpeed
						);
				}
				else if (controller.MoveDirection.x < 0f)
				{
					// Left.
					maxSpeed = -maxSpeed;

					if (speed > maxSpeed)
						speed = Mathf.Max(
							speed +
							delta *
							Time.deltaTime *
							controller.MoveDirection.x,
							maxSpeed
						);
					else if (Grounded && speed < maxSpeed)
						speed = Mathf.Min(
							speed +
							controller.deceleration *
							Time.deltaTime,
							maxSpeed
						);
				}
				else if (Grounded)
				{
					// Stop.
					if (speed > 0f)
						speed = Mathf.Max(
							speed -
							controller.deceleration *
							Time.deltaTime,
							0f
						);
					else if (speed < 0f)
						speed = Mathf.Min(
							speed +
							controller.deceleration *
							Time.deltaTime,
							0f
						);
				}

				if (Grounded)
					GroundedSpeed = speed;
				else
					controller.Rigidbody.velocity =
						new(
							speed,
							controller.Rigidbody.velocity.y
						);
			}
			
			/// <summary>
			/// Custom physics when grounded.
			/// </summary>
			private void FixedUpdate_Grounded()
			{
				if (!Grounded)
					return;

				GroundedVelocity = GroundDirection * GroundedSpeed;
				
				float velocity_x = GroundedVelocity.x;
				float velocity_y = GroundedVelocity.y;
				
				if (jumping)
					jumpPower = controller.jumpStrength;

				if (!flying && jumpPower > 0f)
					// Attempting to jump.
					velocity_y += jumpPower;

				Vector2 velocity = new(
					velocity_x * Time.deltaTime,
					velocity_y * Time.deltaTime
				);

				if (velocity.sqrMagnitude > minMoveDistance.SquareValue)
					Move(velocity);
			}
			
			private Vector2 Move_Internal(Vector2 offset)
			{
				SquareFloat rawDistance = new(null, offset.sqrMagnitude);
				UnityEngine.Debug.Log($"[QQQ] {offset} | {rawDistance} | {rawDistance.SquareValue}");
				
				if (rawDistance.SquareValue <= minMoveDistance.SquareValue)
					return Vector2.zero;
				
				int count = controller.Rigidbody.Cast(
					offset,
					controller.Collision0.Filter,
					buffer,
					rawDistance + skinWidth	
				);

				for (int i = 0; i < count; i++)
				{
					RaycastHit2D hit = buffer[i];

					if (hit.collider.gameObject.layer == PhysicsHelper.ENTITIES_LAYER)
						// Prevent colliding with entities.
						continue;

					if (hit.collider.gameObject.layer == PhysicsHelper.PROJECTILES_LAYER)
						// Prevent colliding with projectiles.
						continue;

					if (controller.Collision0.ignoredColliders.Contains(hit.collider))
						// Skip ignored colliders.
						continue;

					if (controller.Collision0.ShouldIgnoreCollider(hit.collider.gameObject, hit.normal))
					{
						// Ignore this collider.
						controller.Collision0.IgnoreCollider(hit.collider, true);
						continue;
					}

					if (!GroundChanged && hit.distance <= skinWidth)
					{
						// Looks like the rigidbody is stuck.
						// Let Unity handle it.
						unstuck = 1;
						
						return Vector2.zero;
					}
					
					if (hit.normal.y <= slopeLimit)
						// Wall or steep slope hit.
						// Dampen movement.
						GroundedSpeed *= hit.normal.y;

					// Remove skin width from distance.
					float distance =
						Mathf.Min(
							hit.distance - skinWidth,
							rawDistance
						);
					
					Vector2 direction = offset.normalized;
					float extraDistance = rawDistance - distance;
					
					controller.Rigidbody.MovePosition(hit.centroid);
					
					return new(
						direction.x *
						extraDistance *
						Mathf.Abs(hit.normal.y),
						direction.y *
						extraDistance *
						Mathf.Abs(hit.normal.x)
					);
				}
				
				
				// No obstacles found.
				
				controller.Rigidbody
				.MovePosition(
					controller.Rigidbody.position +
					offset
				);
				
				return Vector2.zero;
			}
			
			/// <summary>
			/// Moves the character towards the given offset.
			/// <br>
			/// Also raycasts.
			/// </summary>
			public void Move(Vector2 offset, int iterations = 10)
			{
				for (int i = 0; i < iterations && offset != Vector2.zero; i++)
					offset = Move_Internal(offset);
			}

			/// <summary>
			/// Dislodges the rigidbody by dedicating
			/// a single frame for the default physics to solve it.
			/// </summary>
			public void Unstuck() =>
				unstuck = 1;
		}
	}
}
