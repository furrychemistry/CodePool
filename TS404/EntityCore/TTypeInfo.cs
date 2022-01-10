using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TS404;

/// <summary>
///		Exposes specialized information of a <see cref="System.Type"/>.
/// </summary>
public static class TTypeInfo<T>
{
	/// <summary>
	///		The <see cref="System.Type"/>.
	/// </summary>
	public static Type Type { get; } = typeof(T);

	/// <summary>
	///		Can <see cref="Type"/> cast to <see cref="System.ValueType"/>? This would be true for
	///		<see cref="Enum"/>, <see cref="Nullable{T}"/>, <see cref="ValueType"/>, and types derived
	///		from these (<see langword="enum"/>, <see cref="bool?"/>, <see cref="int"/>, etc.).
	/// </summary>
	public static bool IsValueType => m_IsValueType;

	/// <summary>
	///		Is <see cref="TTypeInfo{T}.Type"/> an <see langword="interface"/>?
	/// </summary>
	public static bool IsInterface => Type.IsInterface;

	/// <summary>
	///		Is <see cref="TTypeInfo{T}.Type"/> <see langword="abstract"/>?
	/// </summary>
	public static bool IsAbstract => Type.IsAbstract;

	/// <summary>
	///		<see cref="Type"/> <see langword="class"/> inheritance chain. Zero index is <see cref="object"/>
	///		and last index is <see cref="Type"/>.
	/// </summary>
	public static ReadOnlyCollection<Type> ClassInheritanceChain
		=> m_ClassInheritanceChain ?? CalculateClassInheritanceChain();

	/// <summary>
	///		Could some <see cref="Component"/> ever be cast into <see cref="Type"/>, whether through implementing
	///		an interface or via class inheritance? See <see cref="Entity.ComponentCollection.TryGetByType{T}(out T)"/>
	///		for a use case.
	/// </summary>
	public static bool CanBeComponent => !IsValueType && (IsInterface || IsComponent);

	/// <summary>
	///		Returns <see langword="true"/> if <see cref="TTypeInfo{T}.Type"/> has <see cref="Component"/>
	///		in it's class inheritance chain. Otherwise returns <see langword="false"/>.
	/// </summary>
	public static bool IsComponent => m_IsComponent ?? CalculateClassInheritanceChain(ref m_IsComponent);

	/// <summary>
	///		Could some <see cref="Entity"/> ever be cast into <see cref="Type"/>, whether through implementing
	///		an interface or via class inheritance?
	/// </summary>
	public static bool CanBeEntity => !IsValueType && (IsInterface || IsEntity);

	/// <summary>
	///		Returns <see langword="true"/> is <see cref="Type"/> has <see cref="Entity"/> in it's class
	///		inheritance chain. Otherwise returns <see langword="false"/>.
	/// </summary>
	public static bool IsEntity => m_IsEntity ?? CalculateClassInheritanceChain(ref m_IsEntity);

	#region Internals

	private static readonly bool m_IsValueType = Type.IsValueType || Type == typeof(ValueType) || Type.BaseType == typeof(ValueType);

	/// <summary>
	///		Deferred; assigned by <see cref="CalculateClassInheritanceChain"/>.
	/// </summary>
	private static bool? m_IsComponent = null;

	/// <summary>
	///		Deferred; assigned by <see cref="CalculateClassInheritanceChain"/>.
	/// </summary>
	private static bool? m_IsEntity = null;

	/// <inheritdoc cref="ClassInheritanceChain"/>
	private static ReadOnlyCollection<Type>? m_ClassInheritanceChain = null;

	/// <summary>
	///		Calculates the entire class-only inheritance chain.
	/// </summary>
	private static ReadOnlyCollection<Type> CalculateClassInheritanceChain()
	{
		List<Type> stack = new();
		Type t = Type;
		stack.Add(t);

		while (t.BaseType is not null) stack.Add(t = t.BaseType!);

		m_IsComponent = t.Equals(typeof(Component));
		m_IsEntity = t.Equals(typeof(Entity));

		stack.Reverse();
		stack.TrimExcess();
		m_ClassInheritanceChain = new(stack);

		return m_ClassInheritanceChain;
	}

	/// <summary>
	///		Calls <see cref="CalculateClassInheritanceChain()"/> (if <see cref="m_ClassInheritanceChain"/> is
	///		<see langword="null"/>. Returns <see cref="Nullable{T}.Value"/> of <paramref name="field"/>.
	/// </summary>
	/// <param name="field">
	///		The field that will not be null after <see cref="CalculateClassInheritanceChain"/> is executed.
	/// </param>
	private static TValue CalculateClassInheritanceChain<TValue>(ref TValue? field) where TValue : struct
	{
		if (m_ClassInheritanceChain is null) CalculateClassInheritanceChain();
		return field!.Value;
	}

	#endregion Internals
}
