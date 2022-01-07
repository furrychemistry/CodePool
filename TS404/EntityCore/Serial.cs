using System;
using System.Diagnostics.CodeAnalysis;

namespace TS404;

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
