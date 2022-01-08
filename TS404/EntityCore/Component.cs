using System;
using System.Diagnostics.CodeAnalysis;

namespace TS404;

#nullable enable

/// <inheritdoc cref="IComponent"/>
public class Component : IComponent, IEquatable<Component>
{
	#region Entity

	private Entity? m_Entity;

	/// <inheritdoc cref="IComponent.Entity"/>
	public Entity? Entity
	{
		get => m_Entity;
		set => Entity.ComponentCollection.TryChangeEntity(this, value);
	}

	Entity? IComponent.Entity { get => m_Entity; set => m_Entity = value; }

	/// <inheritdoc cref="IComponent.EntityChanged(Entity)"/>
	protected virtual void OnEntityChanged(Entity? oldEntity) { }

	void IComponent.EntityChanged(Entity? oldEntity) => OnEntityChanged(oldEntity);

	#endregion Entity

	public Component()
	{
		m_HashCode = InitializeHashCode();
	}

	#region Equality

	private readonly int m_HashCode;

	/// <summary>
	///		Called during ctor, returns a hash code to assign to this instance.
	/// </summary>
	protected virtual int InitializeHashCode() => HashCodeProvider<Entity>.Next();

	public sealed override int GetHashCode() => m_HashCode;

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="a"/> is the same instance as
	///		<paramref name="b"/>, otherwise returns <see langword="false"/>.
	/// </summary>
	public static bool Equals(Component? a, Component? b) => ReferenceEquals(a, b);

	public bool Equals([NotNullWhen(true)] Component? b) => Equals(this, b);

	public sealed override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as Component);

	public static bool operator ==(Component? a, Component? b) => Equals(a, b);
	public static bool operator !=(Component? a, Component? b) => !Equals(a, b);

	#endregion Equality
}
