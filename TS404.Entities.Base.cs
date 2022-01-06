using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace TS404.Entities;

#nullable enable

/// Notes?
/// 
/// Entity-Component-System where Entity is <see cref="Entity"/>, Component is <see cref="Component"/>,
/// and System is <see cref="EntityCollective"/>.
/// 
/// System contains multiple entities (keyed by Serial) with an underlying
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> for storage.
/// 
/// Entities may contain multiple components with an underlying <see cref="List{T}"/> for storage,
/// and the same instance is never added twice. Entity may also be inherited and never has to make
/// use of components. Never has to be added to a system.
/// 
/// Components can be attached to only one entity.
/// 
/// <see cref="IEntity"/> and <see cref="IComponent"/> are used to partition internal operations
/// (coupling, notifications) from public ones to prevent accidental 'features'.
/// 
/// Entity Coupling
///		Entity Collection [Table] with Entity records [Key = Serial]
///			<see cref="IEntity.Container"/>, <see cref="IEntity.Serial"/>
///		Entity [Table] with unordered Component records [Key = instance]
///			<see cref="Entity.Components"/>, <see cref="IComponent.Entity"/>
/// 

/// <summary>
///		May contain multiple <see cref="Component"/> to augment functionality and/or data.
/// <para/>
///		Entity in an Entity-Component-System, where Component is <see cref="Component"/>
///		and System is <see cref="EntityCollective"/>.
/// </summary>
internal interface IEntity
{
	/// <summary>
	///		Unique identifier.
	/// </summary>
	Serial Serial { get; set; }

	/// <summary>
	///		<see cref="EntityCollective"/> containing this <see cref="Entity"/>.
	/// </summary>
	EntityCollective? Container { get; set; }
}

/// <summary>
///		Attaches to one <see cref="Entity"/>, augmenting it with functionality and/or data.
/// <para/>
///		Component in an Entity-Component-System, where Entity is <see cref="Entity"/>
///		and System is <see cref="EntityCollective"/>.
/// </summary>
internal interface IComponent
{
	/// <summary>
	///		<see cref="EntityV1.Entity"/> this <see cref="Component"/> is attached to.
	/// </summary>
	Entity? Entity { get; set; }
}

/// <inheritdoc cref="IEntity"/>
public class Entity : IEntity, IEquatable<Entity>
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
	public EntityCollective? Container => m_Container;

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

	/// <summary>
	///		Collection of <see cref="Component"/> as attached to a specific <see cref="Entity"/>.
	/// </summary>
	public sealed class ComponentCollection : ICollection<Component>
	{
		private List<Component?> m_List = new();

		private readonly Entity m_Entity;

		/// <summary>
		///		Number of <see cref="Component"/> contained.
		/// </summary>
		public int Count => m_List.Count;

		bool ICollection<Component>.IsReadOnly => false;

		/// <summary>
		///		Should only be called by <see cref="Entity.Components"/>.<see langword="set"/>.
		/// </summary>
		internal ComponentCollection(Entity entity) => m_Entity = entity;

		/// <summary>
		///		Returns <typeparamref name="T"/> if a <see cref="Component"/> was found that
		///		matches <typeparamref name="T"/>, otherwise returns the type's default value.
		/// </summary>
		public T? GetByType<T>() => TryGetByType<T>(out var component) ? component : default;

		/// <summary>
		///		Returns <see langword="true"/> if a <see cref="Component"/> was found that
		///		matches <typeparamref name="T"/>, otherwise returns <see langword="false"/>.
		/// </summary>
		public bool TryGetByType<T>([MaybeNullWhen(false)] out T result)
		{
			if (TTypeInfo<T>.CanBeComponent)
			{
				foreach (var component in m_List)
				{
					if (component is T cast)
					{
						result = cast;
						return true;
					}
				}
			}

			result = default;
			return false;
		}

		/// <summary>
		///		Returns an existing <see cref="Component"/> matching <typeparamref name="T"/>,
		///		or a new instance of <typeparamref name="T"/> added to the collection.
		/// </summary>
		/// <exception cref="Exception">
		///		New component of type <typeparamref name="T"/> could not be added.
		/// </exception>
		public T GetOrCreate<T>() where T : Component, new()
		{
			foreach (T? cast in m_List) { if (cast is not null) return cast; }
			var component = new T();
			return Add(component) ? component : throw new Exception($"New component of type {typeof(T).FullName} could not be added.");
		}

		/// <summary>
		///		Returns <see langword="true"/> if <paramref name="component"/> is contained, otherwise
		///		returns <see langword="false"/>.
		/// </summary>
		public bool Contains([NotNullWhen(true)] Component? component) => component is not null && m_List.Contains(component);

		bool ICollection<Component>.Contains(Component component) => Contains(component);

		/// <summary>
		///		Returns <see langword="true"/> if <paramref name="component"/> was removed, otherwise returns
		///		<see langword="false"/>.
		/// </summary>
		public bool Remove([NotNullWhen(true)] Component? component)
		{
			bool removed = component is not null && component.Entity == m_Entity && m_List.Remove(component);
			if (removed) ((IComponent)component!).Entity = null;
			return removed;
		}

		bool ICollection<Component>.Remove(Component component) => Remove(component);

		/// <summary>
		///		Returns <see langword="true"/> if <paramref name="component"/> was added, otherwise returns
		///		<see langword="false"/>.
		///	<para/>
		///		<paramref name="component"/> <see cref="Component.Entity"/> must be null to be added.
		/// </summary>
		public bool Add(Component component)
		{
			if (component.Entity is not null || m_List.Contains(component)) return false;
			m_List.Add(component);
			((IComponent)component).Entity = m_Entity;
			return true;
		}

		void ICollection<Component>.Add(Component component) { if (!Add(component)) throw new Exception("Component not added."); }

		/// <summary>
		///		Removed all contained <see cref="Component"/>.
		/// </summary>
		public void Clear()
		{
			static void SetEntityNull(Component? component) => ((IComponent)component!).Entity = null;
			Interlocked.Exchange(ref m_List, new()).ForEach(SetEntityNull);
		}

		/// <summary>
		///		Creates a new array of all contained <see cref="Component"/>.
		/// </summary>
		public Component?[] ToArray() => m_List.ToArray();

		public void CopyTo(Component?[] array, int arrayIndex = 0) => m_List.CopyTo(array, arrayIndex);

		public IEnumerator<Component> GetEnumerator() { foreach (var component in m_List) yield return component!; }

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

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

/// <inheritdoc cref="IComponent"/>
public class Component : IComponent, IEquatable<Component>
{
	#region Entity

	private Entity? m_Entity;

	/// <inheritdoc cref="IComponent.Entity"/>
	public Entity? Entity => m_Entity;

	Entity? IComponent.Entity { get => m_Entity; set => m_Entity = value; }

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

/// <summary>
///		Collection of <see cref="Entity"/>. To appear in this collection, each <see cref="Entity"/>
///		must be added; they can also be removed.
/// <para/>
///		System in an Entity-Component-System, where Entity is <see cref="Entity"/> and Component is
///		<see cref="Component"/>.
/// </summary>
public sealed class EntityCollective : ICollection<Entity>
{
	private readonly ConcurrentDictionary<Serial, Entity> m_Dictionary = new();
	private Serial m_LastSerial;
	private int m_Count;

	/// <summary>
	///		Number of <see cref="Entity"/> contained.
	/// </summary>
	public int Count => m_Count;

	/// <inheritdoc cref="GetEntity(Serial)"/>
	public Entity? this[Serial serial] => GetEntity(serial);

	public EntityCollective() { }

	/// <summary>
	///		Returns <see langword="null"/> if <paramref name="serial"/> was not found, otherwise returns the
	///		<see cref="Entity"/>.
	/// </summary>
	public Entity? GetEntity(Serial serial) => TryGetEntity(serial, out var entity) ? entity : null;

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> is not <see langword="null"/>,
	///		otherwise returns <see langword="false"/>.
	/// </summary>
	public bool TryGetEntity(Serial serial, [MaybeNullWhen(false)] out Entity entity)
		=> m_Dictionary.TryGetValue(serial, out entity);

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> was found, otherwise returns
	///		<see langword="false"/>.
	/// </summary>
	public bool Contains(Entity? entity)
		=> entity is not null && ReferenceEquals(entity.Container, this);

	/// <summary>
	///		Returns <see langword="true"/> if the <paramref name="serial"/> was found, otherwise returns
	///		<see langword="false"/>.
	/// </summary>
	public bool Contains(Serial serial) => !serial.IsZero && m_Dictionary.ContainsKey(serial);

	/// <summary>
	///		Returns an unused <see cref="Serial"/>.
	/// </summary>
	public Serial NewSerial()
	{
		Serial serial;
		while ((serial = ++m_LastSerial).IsZero || m_Dictionary.ContainsKey(serial)) ;
		return serial;
	}

	/// <summary>
	///		Returns a non-zero <see cref="Serial"/>. If <paramref name="entity"/> has a <see cref="Entity.Serial"/>
	///		of zero, <see cref="NewSerial"/> is assigned and that value is returned.
	/// </summary>
	private Serial GetAssignSerial(Entity entity)
	{
		if (entity.Serial.IsZero) ((IEntity)entity).Serial = NewSerial();
		return entity.Serial;
	}

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> was added, otherwise returns
	///		<see langword="false"/>.
	///	<para/>
	///		If <paramref name="entity"/> has a <see cref="Entity.Serial"/> of zero, a new
	///		<see cref="Serial"/> will be assigned even if returning <see langword="false"/>.
	/// </summary>
	public bool Add(Entity entity)
	{
		if (Contains(entity) || Contains(entity.Serial) || !m_Dictionary.TryAdd(GetAssignSerial(entity), entity))
			return false;

		Interlocked.Increment(ref m_Count);
		((IEntity)entity).Container = this;

		return true;
	}

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> has it's <see cref="Entity.Serial"/>
	///		successfully changed, otherwise returns <see langword="false"/> and no change occured.
	/// </summary>
	public bool TryChangeSerial(Entity entity, Serial value)
	{
		if (value.IsZero || value == entity.Serial || !Contains(entity) || !m_Dictionary.TryAdd(value, entity))
			return false;

		// Remove entity using it's old serial.
		if (!m_Dictionary.TryRemove(new KeyValuePair<Serial, Entity>(entity.Serial, entity)))
		{
			// Remove failed, what happened? Let's at least tell what steps we were doing. Debug is needed
			// if we got here.

			if (!m_Dictionary.TryRemove(value, out var other))
				throw new ApplicationException("Entity added using new serial, but couldn't be removed " +
					"using it's old serial... then entity couldn't be removed using the new serial.");

			else if (entity != other)
				throw new ApplicationException("Entity added using new serial, but couldn't be removed " +
					"using it's old serial... then entity removed using the old serial wasn't the original entity.");

			else
				throw new ApplicationException("Entity added using new serial, but couldn't be removed " +
					"using it's old serial.");
		}

		// Assign new serial.
		((IEntity)entity).Serial = value;

		return true;
	}

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> was removed, otherwise returns
	///		<see langword="false"/>.
	/// </summary>
	public bool Remove(Entity? entity)
	{
		if (entity is null || !Contains(entity) || !m_Dictionary.TryRemove(new(entity.Serial, entity)))
			return false;

		Interlocked.Decrement(ref m_Count);
		((IEntity)entity).Container = null;

		return true;
	}

	/// <summary>
	///		Returns <see langword="true"/> is the associated <see cref="Entity"/> was removed,
	///		otherwise returns <see langword="false"/>.
	/// </summary>
	public bool Remove(Serial serial) => Remove(GetEntity(serial));

	/// <summary>
	///		Returns an enumerator from a <see cref="ConcurrentDictionary{TKey, TValue}"/>. See the .net core
	///		documentation for more information.
	/// </summary>
	public IEnumerator<Entity> GetEnumerator() => m_Dictionary.Values.GetEnumerator();

	// Some things are good to know.
	void ICollection<Entity>.Clear() => throw new NotSupportedException();

	#region ICollection<Entity>

	bool ICollection<Entity>.IsReadOnly => false;

	void ICollection<Entity>.Add(Entity entity) { if (!Add(entity)) throw new Exception("Entity not added."); }

	bool ICollection<Entity>.Remove(Entity entity) => Remove(entity);

	void ICollection<Entity>.CopyTo(Entity[] array, int arrayIndex) => m_Dictionary.Values.CopyTo(array, arrayIndex);

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	#endregion ICollection<Entity>
}

/// <summary>
///		Unique identifier for one <see cref="Entity"/> in one <see cref="EntityCollective"/>.
/// </summary>
public readonly struct Serial : IEquatable<Serial>, IComparable<Serial>
{
	/// <summary>
	///		<see cref="Serial"/> with <see cref="Value"/> of zero.
	/// </summary>
	public static readonly Serial Zero = new(0);

	/// <summary>
	///		Underlying value.
	/// </summary>
	public readonly uint Value;

	/// <summary>
	///		Does <see cref="Value"/> equal zero?
	/// </summary>
	public bool IsZero => this == Zero;

	public Serial(uint value) => Value = value;

	#region Comparison

	/// <summary>
	///		Compares two <see cref="Serial"/> to determine their sort order.
	///	<para/>
	///		Zero means <paramref name="value"/> equals <paramref name="to"/>.
	///	<para/>
	///		-1 means <paramref name="value"/> is less than <paramref name="to"/>.
	///	<para/>
	///		+1 means <paramref name="value"/> is greater than <paramref name="to"/>.
	/// </summary>
	public static int Compare(Serial value, Serial to) => value.Value.CompareTo(to);

	public int CompareTo(Serial to) => Compare(this, to);

	public static bool operator <(Serial a, Serial b) => a.Value < b.Value;
	public static bool operator <=(Serial a, Serial b) => a.Value <= b.Value;
	public static bool operator >(Serial a, Serial b) => a.Value > b.Value;
	public static bool operator >=(Serial a, Serial b) => a.Value >= b.Value;

	#endregion Comparison

	#region Equality

	public override int GetHashCode() => (int)Value;

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="a"/> is the same instance as
	///		<paramref name="b"/>, otherwise returns <see langword="false"/>.
	/// </summary>
	public static bool Equals(Serial a, Serial b) => a.Value == b.Value;

	public bool Equals(Serial b) => Equals(this, b);

	public override bool Equals([NotNullWhen(true)] object? obj) => obj is Serial b && Equals(b);

	public static bool operator ==(Serial a, Serial b) => Equals(a, b);
	public static bool operator !=(Serial a, Serial b) => !Equals(a, b);

	#endregion Equality

	/// <summary>
	///		Converts this <see cref="Serial"/> to an 8-digit hex string representation,
	///		with the hex specifier "0x" prefix.
	/// </summary>
	/// <returns></returns>
	public override string ToString() => "0x" + Value.ToString("X8");

	public static Serial operator ++(Serial a) => new(unchecked(a.Value + 1));

	public static implicit operator Serial(uint a) => new(a);

	public static implicit operator uint(Serial a) => a.Value;
}

/// <summary>
///		Exposes specialized information of a <see cref="System.Type"/>.
/// </summary>
internal static class TTypeInfo<T>
{
	/// <summary>
	///		Deferred; assigned by <see cref="IsComponent"/>.
	/// </summary>
	private static bool? m_IsComponent = null;

	/// <summary>
	///		The <see cref="System.Type"/>.
	/// </summary>
	public static Type Type { get; } = typeof(T);

	/// <summary>
	///		Is <see cref="TTypeInfo{T}.Type"/> a <see cref="System.ValueType"/>?
	/// </summary>
	public static bool IsValueType => Type.IsValueType;

	/// <summary>
	///		Is <see cref="TTypeInfo{T}.Type"/> an <see langword="interface"/>?
	/// </summary>
	public static bool IsInterface => Type.IsInterface;

	/// <summary>
	///		Is <see cref="TTypeInfo{T}.Type"/> <see langword="abstract"/>?
	/// </summary>
	public static bool IsAbstract => Type.IsAbstract;

	/// <summary>
	///		Could some <see cref="Component"/> ever be cast into <see cref="TTypeInfo{T}.Type"/>, whether through
	///		implementing an interface or via inheritance? See <see cref="Entity.ComponentCollection.TryGetByType{T}(out T)"/>
	///		for a use case.
	/// </summary>
	public static bool CanBeComponent => !IsValueType && (IsInterface || IsComponent);

	/// <summary>
	///		Returns <see langword="true"/> if <see cref="TTypeInfo{T}.Type"/> is <see cref="Component"/>
	///		or is a subclass of <see cref="Component"/>.
	/// </summary>
	public static bool IsComponent
	{
		get
		{
			if (!m_IsComponent.HasValue)
				m_IsComponent = Type == typeof(Component) || Type.GetInterface(typeof(IComponent).FullName!) is not null;
			return m_IsComponent.Value;
		}
	}
}

/// <summary>
///		Provides a new <see cref="int"/> hash code. Underlying is just an ever-incrementing <see cref="int"/>
///		specific to <typeparamref name="T"/>, with zero always reserved for null.
/// </summary>
internal static class HashCodeProvider<T> where T : class
{
	public const int Null = 0;

	private static int m_NextHashCode;

	public static int Next()
	{
		int hash = unchecked(Interlocked.Increment(ref m_NextHashCode));
		return hash == 0 ? Next() : hash;
	}
}
