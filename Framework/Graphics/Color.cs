namespace Framework.Graphics;

/// <summary>
/// Represents an RGBA color used by framework rendering APIs.
/// </summary>
public readonly record struct Color(float R, float G, float B, float A = 1.0f);
