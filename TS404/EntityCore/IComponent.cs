namespace TS404;

#nullable enable

/// <summary>
///		Attaches to one <see cref="TS404.Entity"/>, augmenting it with functionality and/or data.
/// </summary>
internal interface IComponent
{
	/// <summary>
	///		<see cref="TS404.Entity"/> this <see cref="Component"/> is attached to.
	/// </summary>
	Entity? Entity { get; set; }
}
