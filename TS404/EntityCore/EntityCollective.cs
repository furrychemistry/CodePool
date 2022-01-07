using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace TS404;

#nullable enable

/// <summary>
///		Collection of <see cref="Entity"/>. To appear in this collection, each <see cref="Entity"/>
///		must be added; they can also be removed.
/// <para/>
///		System in an Entity-Component-System, where Entity is <see cref="Entity"/> and Component is
///		<see cref="Component"/>.
/// </summary>
public abstract class EntityCollective : ICollection<Entity>
{
	/// <summary>
	///		Returns <see langword="true"/> when <paramref name="entity"/> changed it's <see cref="Entity.Container"/> to
	///		<paramref name="value"/>, otherwise returns <see langword="false"/>.
	///	<para/>
	///		Will throw <see cref="ApplicationException"/> when [remove from non-null initial <see cref="Entity.Container"/>]
	///		and [add to non-null <paramref name="value"/>] both fail, followed by initial <see cref="Entity.Container"/> add
	///		subsequently failing.
	/// </summary>
	/// <exception cref="ApplicationException">
	///		Entity could not be re-added to it's initial container after being removed.
	/// </exception>
	public static bool TryChangeContainer(Entity entity, EntityCollective? value)
	{
		const string ERR_ReAddFailed = "Entity could not be re-added to it's initial container after being removed.";
		var oldValue = entity.Container;

		if (ReferenceEquals(value, oldValue)) return false; // No change can occur.
		else if (oldValue is null) return value?.Add(entity) ?? false; // Both were null.
		else if (value is null) return oldValue.Remove(entity);
		else if (!oldValue.CoreRemoveValidation(entity) || !value.CoreAddValidation(entity)) return false;
		else if (oldValue.Remove(entity) && value.Add(entity)) return true;
		else if(!oldValue.Add(entity)) throw new ApplicationException(ERR_ReAddFailed);
		else return false;
	}

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
