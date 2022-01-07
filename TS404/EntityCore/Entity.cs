using System;
using System.Diagnostics.CodeAnalysis;

namespace TS404;

#nullable enable

/// <inheritdoc cref="IEntity"/>
public partial class Entity : IEntity, IEquatable<Entity>
{
	#region Serial

	private Serial m_Serial;

	/// <inheritdoc cref="IEntity.Serial"/>
	public Serial Serial
	{
		get => m_Serial;
		set
		{
			if (Container is null) ((IEntity)this).Serial = value;
			else Container.TryChangeSerial(this, value);
		}
	}

	Serial IEntity.Serial { get => m_Serial; set => m_Serial = value; }

	#endregion Serial

	#region Container

	private EntityCollective? m_Container;

	/// <inheritdoc cref="IEntity.Container"/>
	public EntityCollective? Container
	{
		get => m_Container;
		set => EntityCollective.TryChangeContainer(this, value);
	}

	EntityCollective? IEntity.Container { get => m_Container; set => m_Container = value; }

	#endregion Container

	#region Components

	/// <summary>
	///		<see cref="ComponentCollection"/> maintains a <see langword="readonly"/> reference to
	///		this <see cref="Entity"/>.
	/// </summary>
	private ComponentCollection? m_Components = null;

	/// <summary>
	///		Collection of attached <see cref="Component"/>.
	/// </summary>
	public ComponentCollection Components => m_Components ??= new(this);

	/// <summary>
	///		Returns <see langword="true"/> if the component is allowed to be added, otherwise
	///		returns <see langword="false"/>.
	/// </summary>
	protected virtual bool ComponentAddValidation(Component component) => true;

	/// <summary>
	///		Returns <see langword="true"/> if the component is allowed to be removed, otherwise
	///		returns <see langword="false"/>.
	/// </summary>
	protected virtual bool ComponentRemoveValidation(Component component) => true;

	/// <summary>
	///		Called after <paramref name="component"/> is added to <see cref="Components"/>.
	/// </summary>
	protected virtual void OnComponentAdded(Component component) { }

	/// <summary>
	///		Called after <paramref name="component"/> is removed from <see cref="Components"/>.
	/// </summary>
	protected virtual void OnComponentRemoved(Component component) { }

	/// <inheritdoc cref="ComponentCollection.GetByType{T}"/>
	public T GetComponent<T>() => m_Components is null ? default! : m_Components.GetByType<T>()!;

	/// <inheritdoc cref="ComponentCollection.TryGetByType{T}(out T)"/>
	public bool TryGetComponent<T>([MaybeNullWhen(false)] out T component)
	{
		if (m_Components is null)
		{
			component = default!;
			return false;
		}
		return m_Components.TryGetByType(out component);
	}

	/// <inheritdoc cref="ComponentCollection.GetOrCreate{T}"/>
	public T GetOrCreateComponent<T>() where T : Component, new() => Components.GetOrCreate<T>();

	#endregion Components

	public Entity()
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
	public static bool Equals(Entity? a, Entity? b) => ReferenceEquals(a, b);

	public bool Equals([NotNullWhen(true)] Entity? b) => Equals(this, b);

	public sealed override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as Entity);

	public static bool operator ==(Entity? a, Entity? b) => Equals(a, b);
	public static bool operator !=(Entity? a, Entity? b) => !Equals(a, b);

	#endregion Equality
}
