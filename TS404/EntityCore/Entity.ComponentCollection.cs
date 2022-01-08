using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace TS404;

#nullable enable

/// <inheritdoc cref="IEntity"/>
public partial class Entity : IEntity, IEquatable<Entity>
{
	private interface IComponentCollection
	{
		/// <summary>
		///		Returns <see langword="true"/> if <paramref name="component"/> was removed, otherwise returns
		///		<see langword="false"/>. Optionally, allows bypassing <see cref="Entity.ComponentRemoveValidation(Component)"/>.
		/// </summary>
		bool Remove(Component? component, bool force);
	}

	/// <summary>
	///		Collection of <see cref="Component"/> as attached to a specific <see cref="TS404.Entity"/>.
	/// </summary>
	public sealed class ComponentCollection : IComponentCollection, ICollection<Component>
	{
		/// <summary>
		///		Returns <see langword="true"/> when <paramref name="component"/> changed it's <see cref="Component.Entity"/> to
		///		<paramref name="value"/>, otherwise returns <see langword="false"/>.
		///	<para/>
		///		Will throw <see cref="ApplicationException"/> when [remove from non-null initial <see cref="Component.Entity"/>]
		///		and [add to non-null <paramref name="value"/>] both fail, followed by initial <see cref="Component.Entity"/> add
		///		subsequently failing.
		/// </summary>
		/// <exception cref="ApplicationException">
		///		Component could not be re-added to it's initial entity after being removed.
		/// </exception>
		public static bool TryChangeEntity(Component component, Entity? value)
		{
			const string ERR_ReAddFailed = "Component could not be re-added to it's initial entity after being removed.";
			var oldValue = component.Entity;

			if (value == oldValue) return false; // No change can occur.
			else if (oldValue is null) return value?.Components.Add(component) ?? false; // Both were null.
			else if (value is null) return oldValue.Components.Remove(component);
			else if (!oldValue.ComponentRemoveValidation(component) || !value.ComponentAddValidation(component)) return false;
			else if (oldValue.Components.Remove(component) && value.Components.Add(component)) return true;
			else if (!oldValue.Components.Add(component)) throw new ApplicationException(ERR_ReAddFailed);
			else return false;
		}

		private readonly Entity m_Entity;
		private readonly HashSet<Component> m_Collection = new(5); // Use a prime number for capacity (see .net code).

		/// <summary>
		///		Number of <see cref="Component"/> contained.
		/// </summary>
		public int Count => m_Collection.Count;

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
				foreach (var component in m_Collection)
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
			=> TryGetOrCreate<T>(out var result) ? result : throw new Exception("Could not add new component.");

		/// <summary>
		///		Returns <see langword="true"/> if a component matching <typeparamref name="T"/> was found or created,
		///		otherwise returns <see langword="false"/>.
		/// </summary>
		public bool TryGetOrCreate<T>([NotNullWhen(true)] out T? result) where T : Component, new()
		{
			foreach(T component in this)
			{
				if (component is not null)
				{
					result = component;
					return true;
				}
			}

			if (Add(result = new())) return true;

			result = null;
			return false;
		}

		/// <summary>
		///		Returns <see langword="true"/> if <paramref name="component"/> is contained, otherwise
		///		returns <see langword="false"/>.
		/// </summary>
		public bool Contains([NotNullWhen(true)] Component? component) => component is not null && m_Collection.Contains(component);

		bool ICollection<Component>.Contains(Component component) => Contains(component);

		bool IComponentCollection.Remove(Component? component, bool force) => Remove(component, force);

		/// <inheritdoc cref="IComponentCollection.Remove(Component?, bool)"/>
		private bool Remove(Component? component, bool force)
		{
			if (component is null || component.Entity != m_Entity || (!force && !m_Entity.ComponentRemoveValidation(component)))
				return false;

			bool removed = false;
			lock (m_Collection)
			{
				if (component.Entity == m_Entity && (force || m_Entity.ComponentRemoveValidation(component)))
				{
					m_Collection.Remove(component);
					((IComponent)component).Entity = null;
					removed = true;
				}
			}

			if (removed) m_Entity.OnComponentRemoved(component);

			return removed;
		}

		/// <summary>
		///		Returns <see langword="true"/> if <paramref name="component"/> was removed, otherwise returns
		///		<see langword="false"/>.
		/// </summary>
		public bool Remove([NotNullWhen(true)] Component? component) => Remove(component, false);

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
			lock (m_Collection)
			{
				if (component.Entity is null && m_Entity.ComponentAddValidation(component))
				{
					m_Collection.Add(component);
					((IComponent)component).Entity = m_Entity;
					added = true;
				}
			}

			if (added) m_Entity.OnComponentAdded(component);

			return added;
		}

		void ICollection<Component>.Add(Component component) { if (!Add(component)) throw new Exception("Component not added."); }

		/// <summary>
		///		Removes all contained <see cref="Component"/> except those that fail
		///		<see cref="Entity.ComponentRemoveValidation(Component)"/>.
		/// </summary>
		public void Clear()
		{
			HashSet<Component> remove;

			lock (m_Collection) // Entity.ComponentRemoveValidation might throw...
			{
				remove = new(Count);
				foreach (var component in m_Collection)
				{
					if (!m_Entity.ComponentRemoveValidation(component))
					{
						((IComponent)component).Entity = null;
						remove.Add(component);
					}
				}
				m_Collection.ExceptWith(remove);
			}

			foreach (var component in remove) m_Entity.OnComponentRemoved(component);
		}

		public void CopyTo(Component?[] array, int arrayIndex = 0)
		{
			const string ERR_NotEnoughSpace = "Array cannot hold all items.";
			if (arrayIndex + Count >= array.Length) throw new ArgumentException(ERR_NotEnoughSpace);

			Monitor.Enter(m_Collection);
			if (arrayIndex + Count >= array.Length) throw new ArgumentException(ERR_NotEnoughSpace);
#nullable disable
			m_Collection.CopyTo(array, arrayIndex);
#nullable restore
			Monitor.Exit(m_Collection);
		}

		public HashSet<Component>.Enumerator GetEnumerator() => m_Collection.GetEnumerator();

		IEnumerator<Component> IEnumerable<Component>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
