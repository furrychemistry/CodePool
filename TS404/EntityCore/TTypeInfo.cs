using System;

namespace TS404;

/// <summary>
///		Exposes specialized information of a <see cref="System.Type"/>.
/// </summary>
public static class TTypeInfo<T>
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
