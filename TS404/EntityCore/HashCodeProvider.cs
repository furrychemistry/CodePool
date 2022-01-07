using System.Threading;

namespace TS404;

/// <summary>
///		Provides a new <see cref="int"/> hash code. Underlying is just an ever-incrementing <see cref="int"/>
///		specific to <typeparamref name="T"/>.
/// </summary>
public static class HashCodeProvider<T> where T : class
{
	private static int m_NextHashCode;

	public static int Next() => unchecked(Interlocked.Increment(ref m_NextHashCode));
}
