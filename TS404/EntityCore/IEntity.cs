namespace TS404;

#nullable enable

/// <summary>
///		May contain multiple <see cref="Component"/> to augment functionality and/or data.
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
