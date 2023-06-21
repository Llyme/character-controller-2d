using Core.Helpers;
using Pathfinding;
using UnityEngine;

namespace Core.Controllers
{
	public partial class CharacterController2D
	{
		public class Pathfinder
		{
			private readonly CharacterController2D controller;

			public Seeker Seeker { get; private set; }

			public Path Path { get; private set; }

			public int PathIndex { get; private set; }

			/// <summary>
			/// If the pathfinder is busy.
			/// </summary>
			public bool Pathfinding { get; private set; } = false;

			public Pathfinder(CharacterController2D controller)
			{
				this.controller = controller;
			}

			public void Awake()
			{
				Seeker = controller.GetComponent<Seeker>();
			}

			public void FixedUpdate()
			{
				if (Seeker == null)
					return;

				if (Path == null)
					return;

				float dt = Time.deltaTime;
				// Use deceleration as threshold for reaching a destination.
				float deceleration = controller.deceleration * dt;
				Vector2 pos = controller.Position;
				Vector2? target = null;

				while (PathIndex < Path.vectorPath.Count)
				{
					// Get rid of points that are already reached.
					target = Path.vectorPath[PathIndex];

					if (pos.SquareLength(target.Value) > deceleration)
						break;

					PathIndex++;
				}

				if (target != null)
				{
					controller.MoveDirection = pos.Towards(target.Value);
					controller.Collision0.IgnorePlatforms = target.Value.y - pos.y < -1f;

					if (target.Value.y - pos.y > 1f)
						controller.Physics0.jumping = true;
				}
				else
					controller.Stop();
			}

			/// <summary>
			/// Looks for a path towards the target location.
			/// Synchronous.
			/// </summary>
			public void MoveTo(Vector2 pos)
			{
				Pathfinding = true;
				Seeker.StartPath(controller.Position, pos, OnPathFind);
			}

			/// <summary>
			/// Stops pathfinding.
			/// </summary>
			public void Stop()
			{
				Pathfinding = false;
				Path = null;
				PathIndex = 0;
			}

			private void OnPathFind(Path path)
			{
				if (!Pathfinding)
					// Pathfinding was interrupted.
					return;

				PathIndex = 0;
				Path = path;
				Pathfinding = false;
			}
		}
	}
}
