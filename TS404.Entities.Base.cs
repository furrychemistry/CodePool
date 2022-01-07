using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace TS404.Entities;

#nullable enable

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
///		Attaches to one <see cref="TS404.Entities.Entity"/>, augmenting it with functionality and/or data.
/// <para/>
///		Component in an Entity-Component-System, where Entity is <see cref="TS404.Entities.Entity"/>
///		and System is <see cref="EntityCollective"/>.
/// </summary>
internal interface IComponent
{
	/// <summary>
	///		<see cref="TS404.Entities.Entity"/> this <see cref="Component"/> is attached to.
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

	/// <summary>
	///		Collection of <see cref="Component"/> as attached to a specific <see cref="TS404.Entities.Entity"/>.
	/// </summary>
	public sealed class ComponentCollection : ICollection<Component>
	{
		private readonly Entity m_Entity;
		private List<Component?> m_List = new();
		private int m_Version;

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
		public bool TryGetByType<T>([NotNullWhen(true)] out T? result)
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
		public bool Contains([NotNullWhen(true)] Component? component) => component is not null && component.Entity == m_Entity;

		bool ICollection<Component>.Contains(Component component) => Contains(component);

		/// <summary>
		///		Returns <see langword="true"/> if <paramref name="component"/> was removed, otherwise returns
		///		<see langword="false"/>.
		/// </summary>
		public bool Remove([NotNullWhen(true)] Component? component)
		{
			if (component is null || component.Entity != m_Entity || !m_Entity.ComponentRemoveValidation(component))
				return false;

			bool removed = false;
			lock (m_List)
			{
				if (component.Entity == m_Entity && m_Entity.ComponentRemoveValidation(component))
				{
					int index = m_List.IndexOf(component);
					int lastIndex = Count - 1;
					
					if (index != lastIndex) m_List[index] = m_List[lastIndex];
					m_List.RemoveAt(lastIndex);
					unchecked { m_Version++; }

					((IComponent)component).Entity = null;
					
					removed = true;
				}
			}

			if (removed) m_Entity.OnComponentRemoved(component);

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
			if (component.Entity is not null || !m_Entity.ComponentAddValidation(component))
				return false;

			bool added = false;
			lock (m_List)
			{
				if (component.Entity is null && m_Entity.ComponentAddValidation(component))
				{
					m_List.Add(component);
					unchecked { m_Version++; }

					((IComponent)component).Entity = m_Entity;
					
					added = true;
				}
			}

			if (added) m_Entity.OnComponentAdded(component);
			
			return added;
		}

		void ICollection<Component>.Add(Component component) { if (!Add(component)) throw new Exception("Component not added."); }

		/// <summary>
		///		Removed all contained <see cref="Component"/>.
		/// </summary>
		public void Clear()
		{
			List<Component?> removed;

			lock (this) // Entity.ComponentRemoveValidation might throw...
			{
				removed = m_List;
				m_List = new();
				for (int i = 0; i < removed.Count; i++)
				{
					if (!m_Entity.ComponentRemoveValidation(removed[i]!))
					{
						m_List.Add(removed[i]);
						removed[i] = null;
					}
				}
			}

			foreach(var component in removed)
			{
				if (component is null) continue;
				if (component.Entity == m_Entity) ((IComponent)component).Entity = null;
				m_Entity.OnComponentRemoved(component);
			}
		}

		/// <summary>
		///		Creates a new array of all contained <see cref="Component"/>.
		/// </summary>
		public Component?[] ToArray() => m_List.ToArray();

		public void CopyTo(Component?[] array, int arrayIndex = 0)
		{
			const string ERR_NotEnoughSpace = "Array cannot hold all items.";
			if (arrayIndex + Count >= array.Length) throw new ArgumentException(ERR_NotEnoughSpace);

			Monitor.Enter(m_List);
			if (arrayIndex + Count >= array.Length)
			{
				Monitor.Exit(m_List);
				throw new ArgumentException(ERR_NotEnoughSpace);
			}
			m_List.CopyTo(array, arrayIndex);
			Monitor.Exit(m_List);
		}

		public IEnumerator<Component> GetEnumerator() { foreach (var component in m_List) yield return component!; }

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public struct Enumerator : IEnumerator<Component>
		{
			private const string ERR_BadVersion = "Collection has changed.";
			private readonly ComponentCollection m_Collection;
			private readonly int m_Version;
			private int m_Index = 0;
			private Component? m_Current = null;

			public Component Current => m_Current ?? throw new InvalidOperationException();
			object IEnumerator.Current => Current;

			internal Enumerator(ComponentCollection collection)
			{
				m_Collection = collection;
				m_Version = collection.m_Version;
			}

			void IDisposable.Dispose() { }

			public void Reset()
			{
				if (m_Version != m_Collection.m_Version) throw new InvalidOperationException(ERR_BadVersion);
				m_Index = 0;
				m_Current = null;
			}

			public bool MoveNext()
			{
				if (m_Version != m_Collection.m_Version)
				{
					throw new InvalidOperationException(ERR_BadVersion);
				}
				else if (m_Index < m_Collection.Count)
				{
					m_Current = m_Collection.m_List[m_Index++];
					return true;
				}
				else
				{
					m_Current = null;
					return false;
				}
			}
		}
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
public abstract class EntityCollective : ICollection<Entity>
{
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
	///		Returns an unused <see cref="Serial"/>.
	/// </summary>
	public virtual Serial NewSerial()
	{
		Serial serial;
		while ((serial = ++m_LastSerial).IsZero || Contains(serial)) ;
		return serial;
	}

	#region Add

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> is allowed to proceed with being added,
	///		otherwise returns <see langword="false"/> and prevents adding to the collection.
	/// </summary>
	protected virtual bool CoreAddValidation(Entity entity) => true;

	/// <summary>
	///		Adds <paramref name="pair"/> to the underlying collection. Should do no other actions.
	/// </summary>
	protected abstract bool CoreAdd(KeyValuePair<Serial, Entity> pair);

	/// <summary>
	///		Called when <paramref name="entity"/> is added to the collection.
	/// </summary>
	protected virtual void OnEntityAdded(Entity entity) { }

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> was added, otherwise returns
	///		<see langword="false"/>.
	///	<para/>
	///		If <paramref name="entity"/> has a <see cref="Entity.Serial"/> of zero, a new
	///		<see cref="Serial"/> will be assigned if added to the collection.
	/// </summary>
	public bool Add(Entity entity)
	{
		if (Contains(entity) || Contains(entity.Serial) || !CoreAddValidation(entity))
			return false;

		Serial serial = entity.Serial;
		if (serial.IsZero) serial = NewSerial();

		if (!CoreAdd(new(serial, entity)))
			return false;

		Interlocked.Increment(ref m_Count);

		IEntity cast = entity;
		cast.Container = this;
		cast.Serial = serial;

		// Notify.
		OnEntityAdded(entity);

		return true;
	}

	#endregion Add

	#region ChangeSerial

	/// <summary>
	///		Called when <paramref name="entity"/> changed it's <see cref="Entity.Serial"/> from
	///		<paramref name="oldSerial"/> to <paramref name="newSerial"/>.
	/// </summary>
	protected virtual void OnEntitySerialChanged(Entity entity, Serial oldSerial, Serial newSerial) { }

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> has it's <see cref="Entity.Serial"/>
	///		successfully changed, otherwise returns <see langword="false"/> and no change occurs.
	/// </summary>
	public bool TryChangeSerial(Entity entity, Serial value)
	{
		Serial oldSerial = entity.Serial;
		if (value.IsZero || value == oldSerial || !Contains(entity) || !CoreAdd(new(value, entity)))
			return false;

		// Remove entity using it's old serial.
		if (!CoreRemove(new(oldSerial, entity)))
		{
			// Remove failed, what happened? Let's at least tell what steps we were doing. Debug is needed if we got here.
			const string baseMessage = "Entity added using new serial, but couldn't be removed using it's old serial.";
			const string notRemovedMessage = baseMessage + ".. then entity couldn't be removed using the new serial.";
			throw new ApplicationException(CoreRemove(new(value, entity)) ? baseMessage : notRemovedMessage);
		}

		// Assign new serial.
		((IEntity)entity).Serial = value;

		// Notify.
		OnEntitySerialChanged(entity, oldSerial, value);

		return true;
	}

	#endregion ChangeSerial

	#region Remove

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> is allowed to be removed,
	///		otherwise returns <see langword="false"/> and prevents removing from the collection.
	///	<para/>
	///		<see cref="Remove(Entity?, bool)"/> may bypass this.
	/// </summary>
	protected virtual bool CoreRemoveValidation(Entity entity) => true;

	/// <summary>
	///		Removes <paramref name="pair"/> from the underlying collection. Should do no other actions.
	/// </summary>
	protected abstract bool CoreRemove(KeyValuePair<Serial, Entity> pair);

	/// <summary>
	///		Called when <paramref name="entity"/> is removed from the collection.
	/// </summary>
	protected virtual void OnEntityRemoved(Entity entity) { }

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> was removed, otherwise returns
	///		<see langword="false"/>.
	/// </summary>
	public bool Remove(Entity? entity) => Remove(entity, force: false);

	/// <summary>
	///		Returns <see langword="true"/> is the associated <see cref="Entity"/> was removed,
	///		otherwise returns <see langword="false"/>.
	/// </summary>
	public bool Remove(Serial serial) => Remove(GetEntity(serial));

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> was removed, otherwise returns
	///		<see langword="false"/>. Optionally, allows bypassing <see cref="CoreRemoveValidation(Entity)"/>.
	/// </summary>
	protected bool Remove(Entity? entity, bool force)
	{
		if (entity is null || !Contains(entity) || (force && !CoreRemoveValidation(entity)) || !CoreRemove(new(entity.Serial, entity)))
			return false;

		Interlocked.Decrement(ref m_Count);
		((IEntity)entity).Container = null;

		// Notify.
		OnEntityRemoved(entity);

		return true;
	}

	#endregion Remove

	#region Contains

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="serial"/> is contained by the underlying collection,
	///		otherwise returns <see langword="false"/>. Should do no other actions.
	/// </summary>
	protected abstract bool CoreContains(Serial serial);

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> was found, otherwise returns
	///		<see langword="false"/>.
	/// </summary>
	public bool Contains(Entity? entity) => entity is not null && ReferenceEquals(entity.Container, this);

	/// <summary>
	///		Returns <see langword="true"/> if the <paramref name="serial"/> was found, otherwise returns
	///		<see langword="false"/>.
	/// </summary>
	public bool Contains(Serial serial) => CoreContains(serial);

	#endregion Contains

	#region GetEntity

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> is not null, otherwise returns
	///		<see langword="false"/>. Should do no other actions.
	/// </summary>
	protected abstract bool CoreTryGetEntity(Serial serial, [NotNullWhen(true)] out Entity? entity);

	/// <summary>
	///		Returns <see langword="null"/> if <paramref name="serial"/> was not found, otherwise returns the
	///		<see cref="Entity"/>.
	/// </summary>
	public Entity? GetEntity(Serial serial) => TryGetEntity(serial, out var entity) ? entity : null;

	/// <summary>
	///		Returns <see langword="true"/> if <paramref name="entity"/> is not <see langword="null"/>,
	///		otherwise returns <see langword="false"/>.
	/// </summary>
	public bool TryGetEntity(Serial serial, [NotNullWhen(true)] out Entity? entity) => CoreTryGetEntity(serial, out entity);

	#endregion GetEntity

	#region GetEnumerator

	/// <summary>
	///		Returns an enumerator that iterates through this collecion.
	/// </summary>
	public IEnumerator<Entity> GetEnumerator() => CoreGetEnumerator();

	/// <inheritdoc cref="GetEnumerator"/>
	protected abstract IEnumerator<Entity> CoreGetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	#endregion GetEnumerator

	// Not supported: Clear(), CopyTo(Entity[] array, int arrayIndex)
	#region ICollection<Entity>

	void ICollection<Entity>.Clear() => throw new NotSupportedException();

	void ICollection<Entity>.CopyTo(Entity[] array, int arrayIndex) => throw new NotSupportedException();

	bool ICollection<Entity>.IsReadOnly => false;

	void ICollection<Entity>.Add(Entity entity) { if (!Add(entity)) throw new Exception("Entity not added."); }

	bool ICollection<Entity>.Remove(Entity entity) => Remove(entity);

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
