namespace TS404;

#nullable enable

/// <summary>
///		Attaches to one <see cref="TS404.Entity"/>, augmenting it with functionality and/or data.
/// </summary>
internal interface IComponent
{
	/// <summary>
	///		<see cref="TS404.Entity"/> this <see cref="Component"/> is attached to.
	/// </summary>
	Entity? Entity { get; set; }

	/// <summary>
	///		Called after <see cref="Entity"/> changed, after <paramref name="oldEntity"/> had
	///		<see cref="Entity.OnComponentAdded(Component)"/> or <see cref="Entity.OnComponentRemoved(Component)"/>
	///		was called.
	/// </summary>
	void EntityChanged(Entity? oldEntity);
}
